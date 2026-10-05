using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface ICareerAgentRunner
{
    /// <summary>Claims at most one claimable run and works it as far as it can. False when nothing was claimed.</summary>
    Task<bool> RunOnceAsync(string workerId, CancellationToken ct = default);
}

/// <summary>
/// Executes agent runs as a durable step machine (ADR 0009). A worker leases a run and
/// bumps its fencing token; every save carries that token, so once the lease is lost or
/// the user cancels, the worker's next write fails and is dropped. The model step's
/// output is saved before it is used, so resuming never calls the provider twice.
/// </summary>
public sealed class CareerAgentRunner : ICareerAgentRunner
{
    /// <summary>The only tools the profile summary task may use; the model is told the same list.</summary>
    public static readonly IReadOnlyList<string> AllowedTools = new[] { CareerStepNames.ReadProfile, CareerStepNames.ReadGoal };

    private const int MaxClaimTries = 3;
    private const int MaxSaveAttempts = 5;
    private const int MaxSummaryLength = CareerInputValidator.MaxSummary;

    private readonly ApplicationDbContext _db;
    private readonly ICareerTextModel? _model;
    private readonly CareerAgentOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<CareerAgentRunner> _logger;

    public CareerAgentRunner(
        ApplicationDbContext db, ICareerTextModel? model, IOptions<CareerAgentOptions> options,
        TimeProvider clock, ILogger<CareerAgentRunner> logger)
    {
        _db = db;
        _model = model;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<bool> RunOnceAsync(string workerId, CancellationToken ct = default)
    {
        if (await ExpireAbandonedQuestionAsync(ct))
        {
            return true;
        }

        // With no model nothing can run; leave runs queued rather than failing them.
        if (_model == null)
        {
            return false;
        }

        var (run, worked) = await ClaimAsync(workerId, ct);
        if (run != null)
        {
            await ExecuteAsync(run, _model, ct);
        }
        return worked;
    }

    // ---- Claim -----------------------------------------------------------------

    private async Task<(CareerAgentRun? Run, bool Worked)> ClaimAsync(string workerId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxClaimTries; attempt++)
        {
            var now = Now();
            // LeaseExpiresAt doubles as "not before" on a queued run waiting out a retry backoff.
            var run = await _db.CareerAgentRuns
                .Where(r => (r.Status == CareerRunStatus.Queued || r.Status == CareerRunStatus.Working)
                    && (r.LeaseExpiresAt == null || r.LeaseExpiresAt < now))
                .OrderBy(r => r.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (run == null)
            {
                return (null, false);
            }

            if (run.Attempts >= _options.MaxAttempts)
            {
                // Claimed only to be failed: this run has used up its claims.
                if (await FinishAsync(run, CareerRunStatus.Failed, CareerAgentErrorCodes.RetryLimit, ct))
                {
                    return (null, true);
                }
            }
            else
            {
                run.Attempts++;
                run.FencingToken++;
                run.Status = CareerRunStatus.Working;
                run.LeaseOwner = workerId;
                run.LeaseExpiresAt = now.AddSeconds(_options.LeaseSeconds);
                run.StartedAt ??= now;
                run.Deadline ??= now.AddSeconds(_options.MaxRunSeconds);
                run.UpdatedAt = now;
                try
                {
                    await _db.SaveChangesAsync(ct);
                    _logger.LogInformation("Career run {RunId} claimed by {WorkerId}, attempt {Attempt}", run.Id, workerId, run.Attempts);
                    return (run, true);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Another worker claimed it first; look for the next candidate.
                }
            }
            _db.ChangeTracker.Clear();
        }
        return (null, false);
    }

    /// <summary>Fails one run whose question went unanswered too long, releasing its unit.</summary>
    private async Task<bool> ExpireAbandonedQuestionAsync(CancellationToken ct)
    {
        var cutoff = Now().AddHours(-_options.QuestionExpiryHours);
        var run = await _db.CareerAgentRuns
            .Where(r => r.Status == CareerRunStatus.NeedsInput && r.UpdatedAt < cutoff)
            .OrderBy(r => r.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        if (run == null)
        {
            return false;
        }

        var expired = await FinishAsync(run, CareerRunStatus.Failed, CareerAgentErrorCodes.QuestionExpired, ct);
        _db.ChangeTracker.Clear();
        if (expired)
        {
            _logger.LogInformation("Career run {RunId} expired waiting for an answer", run.Id);
        }
        // A lost race (answered or cancelled meanwhile) is still progress: look again next loop.
        return true;
    }

    // ---- Execute ---------------------------------------------------------------

    private async Task ExecuteAsync(CareerAgentRun run, ICareerTextModel model, CancellationToken ct)
    {
        var steps = await _db.CareerAgentSteps.AsNoTracking()
            .Where(s => s.RunId == run.Id).OrderBy(s => s.Ordinal).ToListAsync(ct);
        bool Done(string name) => steps.Any(s => s.Name == name);

        if (!Done(CareerStepNames.ReadProfile))
        {
            if (!await CanStartStepAsync(run, steps, ct))
            {
                return;
            }
            // Tools read only this owner's data at the versions the run pinned.
            var profile = await LoadProfileAsync(run, ct);
            if (!await SaveStepAsync(run, steps, CareerStepKinds.Tool, CareerStepNames.ReadProfile,
                    JsonSerializer.Serialize(new { version = run.PinnedProfileVersion, title = profile.CurrentTitle }), ct))
            {
                return;
            }
        }

        if (!Done(CareerStepNames.ReadGoal))
        {
            if (!await CanStartStepAsync(run, steps, ct))
            {
                return;
            }
            if (!await SaveStepAsync(run, steps, CareerStepKinds.Tool, CareerStepNames.ReadGoal,
                    JsonSerializer.Serialize(new { version = run.PinnedGoalVersion }), ct))
            {
                return;
            }
        }

        if (run.PinnedGoalVersion == null && run.Answer == null)
        {
            await AskAudienceAsync(run, steps, ct);
            return;
        }

        var text = await DraftAsync(run, steps, model, ct);
        if (text == null)
        {
            return;
        }

        if (!await CanStartStepAsync(run, steps, ct))
        {
            return;
        }
        await SaveProposalAsync(run, steps, text, ct);
    }

    private async Task AskAudienceAsync(CareerAgentRun run, List<CareerAgentStep> steps, CancellationToken ct)
    {
        if (!await CanStartStepAsync(run, steps, ct))
        {
            return;
        }

        AddStep(run, steps, CareerStepKinds.Question, CareerStepNames.AskAudience,
            JsonSerializer.Serialize(new { questionId = CareerAgentRunService.AudienceQuestionId }));
        run.Status = CareerRunStatus.NeedsInput;
        run.QuestionId = CareerAgentRunService.AudienceQuestionId;
        run.QuestionText = CareerAgentRunService.AudienceQuestionText;
        // Waiting for a person holds no lease.
        run.LeaseOwner = null;
        run.LeaseExpiresAt = null;

        if (await SaveAsync(run, ct))
        {
            _logger.LogInformation("Career run {RunId} is waiting for an answer", run.Id);
        }
    }

    /// <summary>Returns the draft text, or null when the run stopped (failed, retried later, or lost its lease).</summary>
    private async Task<string?> DraftAsync(CareerAgentRun run, List<CareerAgentStep> steps, ICareerTextModel model, CancellationToken ct)
    {
        // A saved model output is replayed, never re-requested: this is the crash-safety rule.
        var saved = steps.FirstOrDefault(s => s.Name == CareerStepNames.DraftSummary);
        if (saved != null)
        {
            using var savedOutput = JsonDocument.Parse(saved.OutputJson!);
            return savedOutput.RootElement.GetProperty("text").GetString();
        }

        if (!await CanStartStepAsync(run, steps, ct))
        {
            return null;
        }
        if (await LostFenceAsync(run, ct))
        {
            // Cancelled or re-claimed while we were between steps: spend nothing.
            _logger.LogInformation("Career run {RunId} abandoned before the model call", run.Id);
            return null;
        }

        var request = new CareerModelRequest(
            run.Task, ToFacts(await LoadProfileAsync(run, ct)), await LoadGoalAsync(run, ct), AllowedTools, run.Answer);

        // Record the call (fenced) before asking the provider: if this worker then crashes,
        // is cancelled or loses its lease, the started call still counts against the
        // allowance. Saving also renews the lease for the call.
        run.ModelCalled = true;
        run.LeaseExpiresAt = Now().AddSeconds(_options.LeaseSeconds);
        run.UpdatedAt = Now();
        if (!await SaveAsync(run, ct))
        {
            return null;
        }

        CareerModelResult result;
        // The call must finish well inside the lease, or another worker could claim the run
        // and ask the provider a second time.
        using var timeout = new CancellationTokenSource(ModelTimeout, _clock);
        using var callToken = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            result = await model.CompleteAsync(request, callToken.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            _logger.LogWarning("Career run {RunId} model call timed out", run.Id);
            await RetryOrFailModelAsync(run, ct);
            return null;
        }
        catch (CareerModelException ex) when (!ex.Retryable)
        {
            // Retrying cannot help (bad key, empty account, refusal): fail now.
            _logger.LogWarning("Career run {RunId} model call failed permanently: {Code}", run.Id, ex.Code);
            await FinishAsync(run, CareerRunStatus.Failed, CareerAgentErrorCodes.ModelFailed, ct);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Log the type only: provider messages can echo the prompt.
            _logger.LogWarning("Career run {RunId} model call failed: {ExceptionType}", run.Id, ex.GetType().Name);
            await RetryOrFailModelAsync(run, ct);
            return null;
        }

        run.CostCents += result.CostCents;

        if (result.ToolCall != null)
        {
            // Tools the task may use are already answered by the pinned facts in the request,
            // so any tool request is either forbidden or pointless; neither has a side effect.
            var code = AllowedTools.Contains(result.ToolCall) ? CareerAgentErrorCodes.ModelFailed : CareerAgentErrorCodes.ToolNotAllowed;
            _logger.LogWarning("Career run {RunId} model asked for a tool: {Code}", run.Id, code);
            await FinishAsync(run, CareerRunStatus.Failed, code, ct);
            return null;
        }
        if (run.CostCents > _options.MaxCostCents)
        {
            await FinishAsync(run, CareerRunStatus.Failed, CareerAgentErrorCodes.CostLimit, ct);
            return null;
        }

        var text = result.FinalText?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > MaxSummaryLength)
        {
            await RetryOrFailModelAsync(run, ct);
            return null;
        }

        // Saved with the run's cost, before anything uses the text.
        var output = JsonSerializer.Serialize(new { text, usageTokens = result.UsageTokens, costCents = result.CostCents });
        return await SaveStepAsync(run, steps, CareerStepKinds.Model, CareerStepNames.DraftSummary, output, ct) ? text : null;
    }

    private async Task SaveProposalAsync(CareerAgentRun run, List<CareerAgentStep> steps, string text, CancellationToken ct)
    {
        var now = Now();
        var item = new ExtractedItem(
            "summary", text, null, null, "Drafted by the career assistant from your confirmed profile.", Array.Empty<string>());
        var proposal = await ProposalStore.AddAsync(_db, run.OwnerId, ProposalSources.Agent, null, new[] { item }, now, ct);
        // Pinned to what the run read, so accepting it after the profile moved answers 412.
        proposal.BaseProfileVersion = run.PinnedProfileVersion;

        AddStep(run, steps, CareerStepKinds.Save, CareerStepNames.SaveProposal,
            JsonSerializer.Serialize(new { proposalId = proposal.Id }));
        run.ProposalId = proposal.Id;

        if (await FinishAsync(run, CareerRunStatus.Completed, null, ct))
        {
            _logger.LogInformation("Career run {RunId} completed with proposal {ProposalId}", run.Id, proposal.Id);
        }
    }

    private async Task RetryOrFailModelAsync(CareerAgentRun run, CancellationToken ct)
    {
        if (run.Attempts >= _options.MaxAttempts)
        {
            await FinishAsync(run, CareerRunStatus.Failed, CareerAgentErrorCodes.ModelFailed, ct);
            return;
        }

        // Back to the queue with the lease released. LeaseExpiresAt holds the run back until
        // the backoff passes, so a provider outage does not burn every attempt at once.
        run.Status = CareerRunStatus.Queued;
        run.LeaseOwner = null;
        run.LeaseExpiresAt = Now().AddSeconds(_options.RetryBackoffSeconds * run.Attempts);
        run.UpdatedAt = Now();
        await SaveAsync(run, ct);
    }

    // ---- Ceilings --------------------------------------------------------------

    /// <summary>Fails the run and returns false when starting another step would break a ceiling.</summary>
    private async Task<bool> CanStartStepAsync(CareerAgentRun run, IReadOnlyCollection<CareerAgentStep> steps, CancellationToken ct)
    {
        string? code = null;
        if (run.Deadline is { } deadline && Now() > deadline)
        {
            code = CareerAgentErrorCodes.TimeLimit;
        }
        else if (steps.Count >= _options.MaxSteps)
        {
            code = CareerAgentErrorCodes.StepLimit;
        }

        if (code == null)
        {
            return true;
        }
        _logger.LogWarning("Career run {RunId} stopped by ceiling {Code}", run.Id, code);
        await FinishAsync(run, CareerRunStatus.Failed, code, ct);
        return false;
    }

    // ---- Tools (owner and version scoped) --------------------------------------

    private async Task<CareerProfileVersion> LoadProfileAsync(CareerAgentRun run, CancellationToken ct) =>
        await _db.CareerProfileVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.OwnerId == run.OwnerId && v.VersionNumber == run.PinnedProfileVersion, ct)
        ?? throw new InvalidOperationException($"Career run {run.Id} lost its pinned profile version.");

    private async Task<CareerGoalFactsDto?> LoadGoalAsync(CareerAgentRun run, CancellationToken ct)
    {
        if (run.PinnedGoalVersion == null)
        {
            return null;
        }
        var goal = await _db.CareerGoalVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.OwnerId == run.OwnerId && v.VersionNumber == run.PinnedGoalVersion, ct);
        return goal == null
            ? null
            : new CareerGoalFactsDto(goal.TargetRole, goal.TargetLocation, goal.WorkArrangement, goal.DesiredPayMin, goal.DesiredPayMax, goal.WeeklyEffortHours);
    }

    private static CareerProfileFactsDto ToFacts(CareerProfileVersion v) => new(
        v.CurrentTitle, v.Industry, v.YearsExperience, v.Location, v.Summary, v.Skills, v.Highlights, v.WorkArrangement);

    // ---- Fenced saves ----------------------------------------------------------

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    private TimeSpan ModelTimeout => CareerAgentOptions.ModelCallTimeoutFor(_options.LeaseSeconds);

    /// <summary>
    /// True when another worker claimed the run, another writer finished it, or the user
    /// cancelled since we loaded it. Compares with the token we hold (the loaded value),
    /// not a token this save is about to bump.
    /// </summary>
    private async Task<bool> LostFenceAsync(CareerAgentRun run, CancellationToken ct)
    {
        var held = _db.Entry(run).Property(r => r.FencingToken).OriginalValue;
        var current = await _db.CareerAgentRuns.AsNoTracking()
            .Where(r => r.Id == run.Id).Select(r => (long?)r.FencingToken).FirstOrDefaultAsync(ct);
        return current != held;
    }

    /// <summary>Adds a step (unsaved) and moves the run's checkpoint and lease along with it.</summary>
    private void AddStep(CareerAgentRun run, List<CareerAgentStep> steps, string kind, string name, string outputJson)
    {
        var now = Now();
        var ordinal = steps.Count + 1;
        var step = new CareerAgentStep
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            OwnerId = run.OwnerId,
            Ordinal = ordinal,
            OperationId = $"{run.Id}:{ordinal}",
            Kind = kind,
            Name = name,
            Status = "completed",
            OutputJson = outputJson,
            CreatedAt = now,
            CompletedAt = now
        };
        _db.CareerAgentSteps.Add(step);
        steps.Add(step);

        run.CheckpointOrdinal = ordinal + 1;
        run.CheckpointJson = JsonSerializer.Serialize(new { lastStep = name });
        run.UpdatedAt = now;
        // Progress renews the lease.
        run.LeaseExpiresAt = now.AddSeconds(_options.LeaseSeconds);
    }

    private async Task<bool> SaveStepAsync(
        CareerAgentRun run, List<CareerAgentStep> steps, string kind, string name, string outputJson, CancellationToken ct)
    {
        AddStep(run, steps, kind, name, outputJson);
        return await SaveAsync(run, ct);
    }

    /// <summary>
    /// Ends the run in one save with the allowance settlement and anything already added
    /// (the last step and the proposal). False when the fence was lost.
    /// </summary>
    private async Task<bool> FinishAsync(CareerAgentRun run, CareerRunStatus status, string? errorCode, CancellationToken ct)
    {
        var now = Now();
        // Every terminal write moves the fence, so a worker still holding the old token
        // (for example one whose lease expired mid-call) can never write over this ending.
        run.FencingToken++;
        run.Status = status;
        run.ErrorCode = errorCode;
        run.LeaseOwner = null;
        run.LeaseExpiresAt = null;
        run.CompletedAt = now;
        run.UpdatedAt = now;

        var allowance = await _db.CareerAllowances.FirstOrDefaultAsync(
            a => a.OwnerId == run.OwnerId && a.PeriodStart == CareerAllowanceStore.PeriodStart(run.CreatedAt), ct);
        if (allowance != null)
        {
            CareerAllowanceStore.Settle(allowance, run.ModelCalled);
        }
        return await SaveAsync(run, ct, allowance);
    }

    /// <summary>
    /// Saves with the fencing token. A concurrency conflict on the run means the lease was
    /// lost or the run was cancelled, so the work is dropped quietly. A conflict on the
    /// allowance alone only means another run moved the counter: re-read it and settle again.
    /// </summary>
    private async Task<bool> SaveAsync(CareerAgentRun run, CancellationToken ct, CareerAllowance? allowance = null)
    {
        // The token check in the UPDATE is the real fence. Looking first as well keeps a
        // stale worker from writing sibling rows on stores that save row by row.
        if (await LostFenceAsync(run, ct))
        {
            _logger.LogInformation("Career run {RunId} write discarded: lease lost or run cancelled", run.Id);
            return false;
        }

        for (var attempt = 0; attempt < MaxSaveAttempts; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var allowanceOnly = allowance != null && ex.Entries.Count > 0 && ex.Entries.All(e => e.Entity is CareerAllowance);
                if (!allowanceOnly)
                {
                    _logger.LogInformation("Career run {RunId} write discarded: lease lost or run cancelled", run.Id);
                    return false;
                }

                await _db.Entry(allowance!).ReloadAsync(ct);
                CareerAllowanceStore.Settle(allowance!, run.ModelCalled);
            }
        }
        return false;
    }
}
