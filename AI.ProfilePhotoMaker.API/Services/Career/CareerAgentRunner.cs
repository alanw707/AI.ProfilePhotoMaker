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
    private readonly IOccupationReference? _reference;
    private readonly IMarketReference? _market;
    private readonly IPayObservationSource _paySource;

    public CareerAgentRunner(
        ApplicationDbContext db, ICareerTextModel? model, IOptions<CareerAgentOptions> options,
        TimeProvider clock, ILogger<CareerAgentRunner> logger, IOccupationReference? reference = null,
        IMarketReference? market = null, IPayObservationSource? paySource = null)
    {
        _reference = reference;
        _market = market;
        _paySource = paySource ?? new NoQualifiedPayObservationSource();
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

        // With no model a profile summary cannot run: leave it queued rather than failing it.
        // Occupation matching and market briefs never call a model, so they still run.
        var (run, worked) = await ClaimAsync(workerId, ct);
        if (run == null)
        {
            return worked;
        }
        if (run.Task == CareerAgentTasks.OccupationMatch)
        {
            await ExecuteOccupationMatchAsync(run, ct);
        }
        else if (run.Task == CareerAgentTasks.MarketBrief)
        {
            await ExecuteMarketBriefAsync(run, ct);
        }
        else if (run.Task == CareerAgentTasks.PayAnalysis)
        {
            await ExecutePayAnalysisAsync(run, ct);
        }
        else if (run.Task == CareerAgentTasks.Roadmap)
        {
            await ExecuteRoadmapAsync(run, ct);
        }
        else if (run.Task == CareerAgentTasks.TargetedResume)
        {
            await ExecuteTargetedResumeAsync(run, ct);
        }
        else if (run.Task == CareerAgentTasks.ProfessionalSummary)
        {
            await ExecuteProfessionalSummaryAsync(run, ct);
        }
        else
        {
            await ExecuteAsync(run, _model!, ct);
        }
        return worked;
    }

    // ---- Claim -----------------------------------------------------------------

    private async Task<(CareerAgentRun? Run, bool Worked)> ClaimAsync(string workerId, CancellationToken ct)
    {
        var modelAvailable = _model != null;
        for (var attempt = 0; attempt < MaxClaimTries; attempt++)
        {
            var now = Now();
            // LeaseExpiresAt doubles as "not before" on a queued run waiting out a retry backoff.
            var run = await _db.CareerAgentRuns
                .Where(r => (r.Status == CareerRunStatus.Queued || r.Status == CareerRunStatus.Working)
                    && (r.LeaseExpiresAt == null || r.LeaseExpiresAt < now)
                    && (modelAvailable || r.Task == CareerAgentTasks.OccupationMatch || r.Task == CareerAgentTasks.MarketBrief || r.Task == CareerAgentTasks.PayAnalysis || r.Task == CareerAgentTasks.Roadmap || r.Task == CareerAgentTasks.TargetedResume || r.Task == CareerAgentTasks.ProfessionalSummary))
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
        var steps = await LoadStepsAsync(run, ct);
        if (!await ReadFactsAsync(run, steps, ct))
        {
            return;
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

    private async Task<List<CareerAgentStep>> LoadStepsAsync(CareerAgentRun run, CancellationToken ct) =>
        await _db.CareerAgentSteps.AsNoTracking().Where(s => s.RunId == run.Id).OrderBy(s => s.Ordinal).ToListAsync(ct);

    /// <summary>The two read tools every task starts with. False when the run stopped (ceiling or lost fence).</summary>
    private async Task<bool> ReadFactsAsync(CareerAgentRun run, List<CareerAgentStep> steps, CancellationToken ct)
    {
        bool Done(string name) => steps.Any(s => s.Name == name);

        if (!Done(CareerStepNames.ReadProfile))
        {
            if (!await CanStartStepAsync(run, steps, ct))
            {
                return false;
            }
            // Tools read only this owner's data at the versions the run pinned.
            var profile = await LoadProfileAsync(run, ct);
            if (!await SaveStepAsync(run, steps, CareerStepKinds.Tool, CareerStepNames.ReadProfile,
                    JsonSerializer.Serialize(new { version = run.PinnedProfileVersion, title = profile.CurrentTitle }), ct))
            {
                return false;
            }
        }

        if (!Done(CareerStepNames.ReadGoal))
        {
            if (!await CanStartStepAsync(run, steps, ct))
            {
                return false;
            }
            if (!await SaveStepAsync(run, steps, CareerStepKinds.Tool, CareerStepNames.ReadGoal,
                    JsonSerializer.Serialize(new { version = run.PinnedGoalVersion }), ct))
            {
                return false;
            }
        }
        return true;
    }

    // ---- Occupation match (ADR 0010) -------------------------------------------

    private const int MaxOccupationChoices = 3;

    /// <summary>
    /// Plan: read profile, read goal, match (a deterministic tool; the result is saved as a step),
    /// ask which occupation when the result is ambiguous, then save the match. No model call is
    /// made, so <c>ModelCalled</c> stays false and the allowance unit is released at the end.
    /// </summary>
    private async Task ExecuteOccupationMatchAsync(CareerAgentRun run, CancellationToken ct)
    {
        var steps = await LoadStepsAsync(run, ct);
        if (!await ReadFactsAsync(run, steps, ct))
        {
            return;
        }

        var reference = _reference?.Data;
        if (reference == null)
        {
            _logger.LogWarning("Career run {RunId} failed: occupation reference unavailable", run.Id);
            await FinishAsync(run, CareerRunStatus.Failed, CareerAgentErrorCodes.ReferenceUnavailable, ct);
            return;
        }

        OccupationMatchResult result;
        var matched = steps.FirstOrDefault(s => s.Name == CareerStepNames.MatchOccupations);
        if (matched != null)
        {
            // A saved result is replayed, so a resumed run answers the question about the same candidates.
            result = JsonSerializer.Deserialize<OccupationMatchResult>(matched.OutputJson!, OccupationMatchJson.Options)!;
        }
        else
        {
            if (!await CanStartStepAsync(run, steps, ct))
            {
                return;
            }
            var profile = await LoadProfileAsync(run, ct);
            result = OccupationMatcher.Match(
                reference, new OccupationMatchInput(profile.CurrentTitle, profile.Industry, profile.Summary, profile.Skills, profile.Highlights));
            if (!await SaveStepAsync(run, steps, CareerStepKinds.Tool, CareerStepNames.MatchOccupations,
                    JsonSerializer.Serialize(result, OccupationMatchJson.Options), ct))
            {
                return;
            }
        }

        var asked = steps.Any(s => s.Name == CareerStepNames.AskOccupation);
        if (result.Ambiguous && !asked)
        {
            await AskOccupationAsync(run, steps, result, ct);
            return;
        }

        if (!await CanStartStepAsync(run, steps, ct))
        {
            return;
        }
        await SaveMatchAsync(run, steps, reference, result, asked ? run.Answer : null, ct);
    }

    private async Task AskOccupationAsync(CareerAgentRun run, List<CareerAgentStep> steps, OccupationMatchResult result, CancellationToken ct)
    {
        if (!await CanStartStepAsync(run, steps, ct))
        {
            return;
        }

        var choices = OccupationMatcher.ClarificationChoices(result, MaxOccupationChoices)
            .Select(c => new CareerRunChoiceDto(c.Code, c.Title))
            .Append(new CareerRunChoiceDto(CareerAgentRunService.NoOccupationChoice, "None of these"))
            .ToList();
        // The choices are saved with the step, so the answer is checked against what was offered.
        await AskAsync(run, steps, CareerStepNames.AskOccupation, CareerAgentRunService.OccupationQuestionId,
            CareerAgentRunService.OccupationQuestionText,
            JsonSerializer.Serialize(new { questionId = CareerAgentRunService.OccupationQuestionId, choices }, OccupationMatchJson.Options), ct);
    }

    private async Task SaveMatchAsync(
        CareerAgentRun run, List<CareerAgentStep> steps, OccupationReferenceData reference,
        OccupationMatchResult result, string? answer, CancellationToken ct)
    {
        var candidates = result.Candidates;
        var status = result.Status == OccupationMatcher.Candidates ? CareerMatchStatuses.Proposed : CareerMatchStatuses.Unsupported;
        if (answer == CareerAgentRunService.NoOccupationChoice)
        {
            // "None of these" never invents a candidate: the result becomes unsupported.
            candidates = Array.Empty<OccupationCandidate>();
            status = CareerMatchStatuses.Unsupported;
        }
        else if (answer != null && candidates.FirstOrDefault(c => c.Code == answer) is { } chosen)
        {
            candidates = candidates.Where(c => c.Code == chosen.Code).Concat(candidates.Where(c => c.Code != chosen.Code)).ToList();
        }

        var match = new CareerOccupationMatch
        {
            Id = Guid.NewGuid(),
            OwnerId = run.OwnerId,
            RunId = run.Id,
            PinnedProfileVersion = run.PinnedProfileVersion!.Value,
            PinnedGoalVersion = run.PinnedGoalVersion,
            ReferenceRelease = reference.Source.Release,
            MatcherVersion = result.MatcherVersion,
            Status = status,
            ResultJson = JsonSerializer.Serialize(
                new StoredOccupationResult(candidates, status == CareerMatchStatuses.Unsupported ? OccupationMatcher.UnsupportedGuidance : null),
                OccupationMatchJson.Options),
            ClarificationJson = answer == null
                ? null
                : JsonSerializer.Serialize(new CareerMatchClarificationDto(CareerAgentRunService.OccupationQuestionText, answer), OccupationMatchJson.Options),
            CreatedAt = Now()
        };
        // Saved in the same fenced write that completes the run, so a lost lease leaves no match behind.
        _db.CareerOccupationMatches.Add(match);
        AddStep(run, steps, CareerStepKinds.Save, CareerStepNames.SaveMatch, JsonSerializer.Serialize(new { matchId = match.Id }));

        if (await FinishAsync(run, CareerRunStatus.Completed, null, ct))
        {
            _logger.LogInformation("Career run {RunId} completed with occupation match {MatchId}", run.Id, match.Id);
        }
    }

    // ---- Roadmap (ADR 0016) ------------------------------------------------------

    private sealed record EvidenceStepOutput(Guid? MarketBriefId, Guid? PayAnalysisId);

    /// <summary>
    /// Plan: read the pinned goal, read the market and pay evidence (pinning the latest ids), build the options,
    /// plan tasks around the weekly hours, save. Everything is a deterministic read of saved data and the BLS
    /// snapshot; no model is called, so the allowance unit is released.
    /// </summary>
    private async Task ExecuteRoadmapAsync(CareerAgentRun run, CancellationToken ct)
    {
        var steps = await LoadStepsAsync(run, ct);
        var goal = await _db.CareerGoalVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.OwnerId == run.OwnerId && v.VersionNumber == run.PinnedGoalVersion, ct);
        if (goal?.OccupationCode == null || goal.OccupationTitle == null)
        {
            await FinishAsync(run, CareerRunStatus.Failed, CareerOccupationErrorCodes.OccupationRequired, ct);
            return;
        }

        if (!steps.Any(s => s.Name == CareerStepNames.ReadGoal))
        {
            if (!await CanStartStepAsync(run, steps, ct)
                || !await SaveStepAsync(run, steps, CareerStepKinds.Tool, CareerStepNames.ReadGoal,
                    JsonSerializer.Serialize(new { version = run.PinnedGoalVersion, occupationCode = goal.OccupationCode }), ct))
            {
                return;
            }
        }

        var evidenceStep = steps.FirstOrDefault(s => s.Name == CareerStepNames.ReadEvidence);
        EvidenceStepOutput evidence;
        if (evidenceStep != null)
        {
            evidence = JsonSerializer.Deserialize<EvidenceStepOutput>(evidenceStep.OutputJson!, RoadmapJson.Options)!;
        }
        else
        {
            if (!await CanStartStepAsync(run, steps, ct))
            {
                return;
            }
            var briefId = await _db.CareerMarketBriefs.AsNoTracking()
                .Where(b => b.OwnerId == run.OwnerId && b.OccupationCode == goal.OccupationCode)
                .OrderByDescending(b => b.CreatedAt).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
            var payId = await _db.CareerPayAnalyses.AsNoTracking()
                .Where(p => p.OwnerId == run.OwnerId && p.OccupationCode == goal.OccupationCode)
                .OrderByDescending(p => p.CreatedAt).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
            evidence = new EvidenceStepOutput(briefId, payId);
            if (!await SaveStepAsync(run, steps, CareerStepKinds.Tool, CareerStepNames.ReadEvidence,
                    JsonSerializer.Serialize(evidence, RoadmapJson.Options), ct))
            {
                return;
            }
        }

        var builtStep = steps.FirstOrDefault(s => s.Name == CareerStepNames.BuildOptions);
        RoadmapContent content;
        if (builtStep != null)
        {
            content = JsonSerializer.Deserialize<RoadmapContent>(builtStep.OutputJson!, RoadmapJson.Options)!;
        }
        else
        {
            if (!await CanStartStepAsync(run, steps, ct))
            {
                return;
            }
            OccupationReleaseAndResult? match = null;
            if (goal.OccupationMatchId != null)
            {
                var row = await _db.CareerOccupationMatches.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == goal.OccupationMatchId && m.OwnerId == run.OwnerId, ct);
                var stored = row == null ? null : JsonSerializer.Deserialize<StoredOccupationResult>(row.ResultJson, OccupationMatchJson.Options);
                match = row == null || stored == null ? null : new OccupationReleaseAndResult(stored.Candidates, row.ReferenceRelease);
            }
            content = RoadmapBuilder.Build(RoadmapEvidence.Build(goal, match, _market, evidence.MarketBriefId != null, evidence.PayAnalysisId != null));
            if (!await SaveStepAsync(run, steps, CareerStepKinds.Tool, CareerStepNames.BuildOptions,
                    JsonSerializer.Serialize(content, RoadmapJson.Options), ct))
            {
                return;
            }
        }

        if (!steps.Any(s => s.Name == CareerStepNames.PlanTasks))
        {
            if (!await CanStartStepAsync(run, steps, ct)
                || !await SaveStepAsync(run, steps, CareerStepKinds.Tool, CareerStepNames.PlanTasks,
                    JsonSerializer.Serialize(new { weeklyEffortHours = content.WeeklyEffortHours }), ct))
            {
                return;
            }
        }
        if (!await CanStartStepAsync(run, steps, ct))
        {
            return;
        }

        var nextVersion = (await _db.CareerRoadmaps.AsNoTracking().Where(r => r.OwnerId == run.OwnerId)
            .Select(r => (int?)r.Version).MaxAsync(ct) ?? 0) + 1;
        var roadmap = new CareerRoadmap
        {
            Id = Guid.NewGuid(),
            OwnerId = run.OwnerId,
            RunId = run.Id,
            Version = nextVersion,
            Status = CareerRoadmapStatuses.Proposed,
            PinnedProfileVersion = run.PinnedProfileVersion!.Value,
            PinnedGoalVersion = run.PinnedGoalVersion!.Value,
            OccupationCode = goal.OccupationCode,
            OccupationTitle = goal.OccupationTitle,
            MarketBriefId = evidence.MarketBriefId,
            PayAnalysisId = evidence.PayAnalysisId,
            WeeklyEffortHours = content.WeeklyEffortHours,
            LowTimeNote = content.LowTimeNote,
            OptionsJson = JsonSerializer.Serialize(content.Options, RoadmapJson.Options),
            OmittedJson = JsonSerializer.Serialize(content.Omitted, RoadmapJson.Options),
            CreatedAt = Now()
        };
        // Saved in the same fenced write that completes the run, so a lost lease leaves no roadmap behind.
        _db.CareerRoadmaps.Add(roadmap);
        AddStep(run, steps, CareerStepKinds.Save, CareerStepNames.SaveRoadmap, JsonSerializer.Serialize(new { roadmapId = roadmap.Id }));
        if (await FinishAsync(run, CareerRunStatus.Completed, null, ct))
        {
            _logger.LogInformation("Career run {RunId} completed with roadmap {RoadmapId}", run.Id, roadmap.Id);
        }
    }

    // ---- Targeted resume (ADR 0018) ----------------------------------------------

    private sealed record DraftStepOutput(ResumeDraft Draft, string Title);

    /// <summary>
    /// Plan: read profile and goal, choose the facts that overlap the target occupation, draft, save. Deterministic and
    /// model-free, so the allowance unit is released. Every generated line is checked against the pinned profile
    /// before anything is saved, and a run for an existing resume saves a proposal, never a version.
    /// </summary>
    private async Task ExecuteTargetedResumeAsync(CareerAgentRun run, CancellationToken ct)
    {
        var steps = await LoadStepsAsync(run, ct);
        var goal = await _db.CareerGoalVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.OwnerId == run.OwnerId && v.VersionNumber == run.PinnedGoalVersion, ct);
        var occupation = goal?.OccupationCode == null ? null : _reference?.Data?.Find(goal.OccupationCode);
        if (goal?.OccupationCode == null || goal.OccupationTitle == null)
        {
            await FinishAsync(run, CareerRunStatus.Failed, CareerOccupationErrorCodes.OccupationRequired, ct);
            return;
        }
        if (occupation == null)
        {
            await FinishAsync(run, CareerRunStatus.Failed, CareerAgentErrorCodes.ReferenceUnavailable, ct);
            return;
        }
        if (!await ReadFactsAsync(run, steps, ct))
        {
            return;
        }
        var profile = await LoadProfileAsync(run, ct);

        if (!steps.Any(s => s.Name == CareerStepNames.SelectFacts))
        {
            if (!await CanStartStepAsync(run, steps, ct)
                || !await SaveStepAsync(run, steps, CareerStepKinds.Tool, CareerStepNames.SelectFacts,
                    JsonSerializer.Serialize(new { highlights = profile.Highlights.Count, skills = profile.Skills.Count, occupationCode = goal.OccupationCode }), ct))
            {
                return;
            }
        }

        var draftStep = steps.FirstOrDefault(s => s.Name == CareerStepNames.DraftResume);
        DraftStepOutput drafted;
        if (draftStep != null)
        {
            drafted = JsonSerializer.Deserialize<DraftStepOutput>(draftStep.OutputJson!, ResumeJson.Options)!;
        }
        else
        {
            if (!await CanStartStepAsync(run, steps, ct))
            {
                return;
            }
            drafted = new DraftStepOutput(
                ResumeAssembler.Assemble(profile, goal.OccupationTitle, ResumeAssembler.OccupationWords(occupation)),
                $"Resume for {goal.OccupationTitle}");
            if (!await SaveStepAsync(run, steps, CareerStepKinds.Tool, CareerStepNames.DraftResume,
                    JsonSerializer.Serialize(drafted, ResumeJson.Options), ct))
            {
                return;
            }
        }
        if (!await CanStartStepAsync(run, steps, ct))
        {
            return;
        }

        // The server rejects any line whose fact ids do not resolve, whatever produced it.
        if (ResumeFacts.UnsupportedLineIds(profile, drafted.Draft.Sections).Count > 0)
        {
            await FinishAsync(run, CareerRunStatus.Failed, ResumeErrorCodes.UnsupportedClaim, ct);
            return;
        }

        await SaveMaterialAsync(run, steps, CareerMaterialKinds.Resume, drafted.Draft, drafted.Title, goal.OccupationCode, CareerStepNames.SaveResume, ct);
    }

    /// <summary>
    /// Plan: read profile and goal, choose the facts, draft a short bio and a long summary, save. Deterministic and
    /// model-free, so the allowance unit is released. A target occupation is not needed.
    /// </summary>
    private async Task ExecuteProfessionalSummaryAsync(CareerAgentRun run, CancellationToken ct)
    {
        var steps = await LoadStepsAsync(run, ct);
        if (!await ReadFactsAsync(run, steps, ct))
        {
            return;
        }
        var profile = await LoadProfileAsync(run, ct);
        if (!steps.Any(s => s.Name == CareerStepNames.SelectFacts))
        {
            if (!await CanStartStepAsync(run, steps, ct)
                || !await SaveStepAsync(run, steps, CareerStepKinds.Tool, CareerStepNames.SelectFacts,
                    JsonSerializer.Serialize(new { highlights = profile.Highlights.Count, skills = profile.Skills.Count }), ct))
            {
                return;
            }
        }

        var draftStep = steps.FirstOrDefault(s => s.Name == CareerStepNames.DraftSummary);
        DraftStepOutput drafted;
        if (draftStep != null)
        {
            drafted = JsonSerializer.Deserialize<DraftStepOutput>(draftStep.OutputJson!, ResumeJson.Options)!;
        }
        else
        {
            if (!await CanStartStepAsync(run, steps, ct))
            {
                return;
            }
            drafted = new DraftStepOutput(SummaryAssembler.Assemble(profile), $"Professional summary for {profile.CurrentTitle}");
            if (!await SaveStepAsync(run, steps, CareerStepKinds.Tool, CareerStepNames.DraftSummary,
                    JsonSerializer.Serialize(drafted, ResumeJson.Options), ct))
            {
                return;
            }
        }
        if (!await CanStartStepAsync(run, steps, ct))
        {
            return;
        }
        if (ResumeFacts.UnsupportedLineIds(profile, drafted.Draft.Sections).Count > 0)
        {
            await FinishAsync(run, CareerRunStatus.Failed, ResumeErrorCodes.UnsupportedClaim, ct);
            return;
        }
        await SaveMaterialAsync(run, steps, CareerMaterialKinds.Summary, drafted.Draft, drafted.Title, "", CareerStepNames.SaveSummary, ct);
    }

    /// <summary>Saves the draft as a new material, or as a proposal when the run targets an existing one, and completes the run.</summary>
    private async Task SaveMaterialAsync(
        CareerAgentRun run, List<CareerAgentStep> steps, string kind, ResumeDraft draft, string title, string occupationCode, string saveStep, CancellationToken ct)
    {
        var now = Now();
        Guid savedId;
        if (run.MaterialId is { } materialId)
        {
            var material = await _db.CareerMaterials.AsNoTracking().FirstOrDefaultAsync(m => m.Id == materialId && m.OwnerId == run.OwnerId && m.Kind == kind, ct);
            var current = material == null ? null : await _db.CareerMaterialVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.MaterialId == materialId && v.OwnerId == run.OwnerId && v.Number == material.CurrentVersion, ct);
            if (material == null || current == null)
            {
                await FinishAsync(run, CareerRunStatus.Failed, ResumeErrorCodes.MaterialNotFound, ct);
                return;
            }
            var currentSections = JsonSerializer.Deserialize<List<ResumeSection>>(current.SectionsJson, ResumeJson.Options) ?? new();
            var proposal = new CareerMaterialProposal
            {
                Id = Guid.NewGuid(),
                OwnerId = run.OwnerId,
                MaterialId = material.Id,
                RunId = run.Id,
                BaseVersion = material.CurrentVersion,
                ChangesJson = JsonSerializer.Serialize(ResumeAssembler.Diff(currentSections, draft.Sections, ResumeSectionKeys.For(kind)), ResumeJson.Options),
                ProposedJson = JsonSerializer.Serialize(new CareerMaterialService.StoredProposal(
                    draft.Sections, draft.Questions, run.PinnedProfileVersion!.Value, run.PinnedGoalVersion ?? 0, occupationCode), ResumeJson.Options),
                CreatedAt = now
            };
            _db.CareerMaterialProposals.Add(proposal);
            savedId = proposal.Id;
        }
        else
        {
            var material = new CareerMaterial
            {
                Id = Guid.NewGuid(),
                OwnerId = run.OwnerId,
                RunId = run.Id,
                Kind = kind,
                Title = title.Length > 200 ? title[..200] : title,
                CurrentVersion = 1,
                PinnedProfileVersion = run.PinnedProfileVersion!.Value,
                PinnedGoalVersion = run.PinnedGoalVersion ?? 0,
                OccupationCode = occupationCode,
                CreatedAt = now,
                UpdatedAt = now
            };
            // Name and email only; phone, location and links stay off until the user turns them on.
            _db.CareerMaterials.Add(material);
            _db.CareerMaterialVersions.Add(CareerMaterialService.NewVersion(
                material, draft.Sections, draft.Questions, new ResumeContact(), ResumeAuthors.Agent, null, now));
            savedId = material.Id;
        }

        // Saved in the same fenced write that completes the run, so a lost lease leaves nothing behind.
        AddStep(run, steps, CareerStepKinds.Save, saveStep,
            JsonSerializer.Serialize(new { materialId = run.MaterialId ?? savedId, proposalId = run.MaterialId == null ? (Guid?)null : savedId }));
        if (await FinishAsync(run, CareerRunStatus.Completed, null, ct))
        {
            _logger.LogInformation("Career run {RunId} completed with {Kind} {SavedId}", run.Id, kind, savedId);
        }
    }

    // ---- Market brief (ADR 0011) -----------------------------------------------

    private async Task ExecutePayAnalysisAsync(CareerAgentRun run, CancellationToken ct)
    {
        var steps = await LoadStepsAsync(run, ct);
        var goal = await _db.CareerGoalVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.OwnerId == run.OwnerId && v.VersionNumber == run.PinnedGoalVersion, ct);
        if (goal?.OccupationCode == null || goal.OccupationTitle == null)
        {
            await FinishAsync(run, CareerRunStatus.Failed, CareerOccupationErrorCodes.OccupationRequired, ct);
            return;
        }
        if (_market?.Oews == null && _market?.Projections == null)
        {
            await FinishAsync(run, CareerRunStatus.Failed, CareerAgentErrorCodes.ReferenceUnavailable, ct);
            return;
        }
        foreach (var name in new[] { CareerStepNames.ReadGoal, CareerStepNames.ReadBenchmark,
                     CareerStepNames.EvaluateCohort, CareerStepNames.BuildScenario })
        {
            if (steps.Any(s => s.Name == name)) continue;
            if (!await CanStartStepAsync(run, steps, ct) ||
                !await SaveStepAsync(run, steps, CareerStepKinds.Tool, name, "{}", ct)) return;
        }
        if (!await CanStartStepAsync(run, steps, ct)) return;
        var location = MarketBriefBuilder.ResolveLocation(goal.TargetLocation, _market!);
        IReadOnlyList<PayObservation> rows;
        var sourceFailed = false;
        try { rows = _paySource.ObservationsFor(goal.OccupationCode, location.Local?.Code ?? "99"); }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Pay observation source failed for run {RunId}", run.Id);
            rows = Array.Empty<PayObservation>();
            sourceFailed = true;
        }
        var asOf = Now();
        // Record which end of the goal's desired pay the scenario compares against, so the stored
        // row and a recompute agree and the page can say where the target came from.
        var paySource = goal.DesiredPayMin.HasValue ? "desiredPayMin" : goal.DesiredPayMax.HasValue ? "desiredPayMax" : null;
        var input = new PayAnalysisInput(run.PinnedProfileVersion!.Value, run.PinnedGoalVersion!.Value,
            goal.OccupationCode, goal.OccupationTitle, goal.TargetLocation, goal.DesiredPayMin ?? goal.DesiredPayMax,
            location.Local?.Code, location.Local?.Title, location.Resolution,
            _market!.Oews?.Source.ReferencePeriod, EmbeddedMarketReference.ExpectedSha256,
            _market.Projections?.Source.ReferencePeriod, PayEvidenceRules.RuleVersion, _paySource.SourceId, rows, asOf,
            paySource);
        var content = PayAnalysisBuilder.Build(input, _market, sourceFailed);
        var row = new CareerPayAnalysis
        {
            Id = Guid.NewGuid(), OwnerId = run.OwnerId, RunId = run.Id,
            PinnedProfileVersion = input.ProfileVersion, PinnedGoalVersion = input.GoalVersion,
            OccupationCode = input.OccupationCode, OccupationTitle = input.OccupationTitle,
            AreaCode = input.AreaCode, AreaTitle = input.AreaTitle, AreaResolution = input.AreaResolution,
            LocationInput = input.LocationText, RequestedAnnual = input.RequestedAnnual, RequestedPaySource = input.RequestedPaySource,
            OewsRelease = input.OewsRelease, OewsSnapshotSha256 = input.OewsSnapshotSha256,
            ProjectionsRelease = input.ProjectionsRelease, RuleVersion = input.RuleVersion,
            ObservationSourceId = input.ObservationSourceId, ObservationCount = rows.Count,
            InputHash = content.InputHash, InputJson = PayAnalysisBuilder.CanonicalInputJson(input), Status = content.Status,
            SectionsJson = JsonSerializer.Serialize(PayAnalysisBuilder.AsList(content.Sections), MarketBriefJson.Options),
            QualificationJson = JsonSerializer.Serialize(content.Qualification, MarketBriefJson.Options),
            SourcesJson = JsonSerializer.Serialize(content.Sources, MarketBriefJson.Options), CreatedAt = asOf
        };
        _db.CareerPayAnalyses.Add(row);
        AddStep(run, steps, CareerStepKinds.Save, CareerStepNames.SaveAnalysis, JsonSerializer.Serialize(new { payAnalysisId = row.Id }));
        await FinishAsync(run, CareerRunStatus.Completed, null, ct);
    }

    private sealed record WagesStepOutput(MarketLocationDto Location, MarketSectionDto Wages, MarketSectionDto Employment);

    private sealed record SectionStepOutput(MarketSectionDto Section);

    /// <summary>
    /// Plan: read the pinned goal, look up wages and employment, look up the outlook, compare the match's
    /// other candidates, save the brief. Every step is a deterministic read of the BLS snapshot; each
    /// result is saved as a step before it is used, and no model is called, so the allowance unit is released.
    /// </summary>
    private async Task ExecuteMarketBriefAsync(CareerAgentRun run, CancellationToken ct)
    {
        var steps = await LoadStepsAsync(run, ct);
        var reference = _market;
        if (reference?.Oews == null && reference?.Projections == null)
        {
            _logger.LogWarning("Career run {RunId} failed: market reference unavailable", run.Id);
            await FinishAsync(run, CareerRunStatus.Failed, CareerAgentErrorCodes.ReferenceUnavailable, ct);
            return;
        }

        var goal = await _db.CareerGoalVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.OwnerId == run.OwnerId && v.VersionNumber == run.PinnedGoalVersion, ct);
        if (goal?.OccupationCode == null || goal.OccupationTitle == null)
        {
            // The pinned version is immutable and was checked at creation; this guards a lost row only.
            await FinishAsync(run, CareerRunStatus.Failed, CareerOccupationErrorCodes.OccupationRequired, ct);
            return;
        }

        if (!steps.Any(s => s.Name == CareerStepNames.ReadGoal))
        {
            if (!await CanStartStepAsync(run, steps, ct)
                || !await SaveStepAsync(run, steps, CareerStepKinds.Tool, CareerStepNames.ReadGoal,
                    JsonSerializer.Serialize(new { version = run.PinnedGoalVersion, occupationCode = goal.OccupationCode }), ct))
            {
                return;
            }
        }

        var wages = await MarketStepAsync(run, steps, CareerStepNames.LookUpWages, ct, () =>
        {
            var location = MarketBriefBuilder.ResolveLocation(goal.TargetLocation, reference!);
            return new WagesStepOutput(
                location,
                MarketBriefBuilder.BuildWages(goal.OccupationCode, location, reference!),
                MarketBriefBuilder.BuildEmployment(goal.OccupationCode, location, reference!));
        });
        if (wages == null)
        {
            return;
        }

        var outlook = await MarketStepAsync(run, steps, CareerStepNames.LookUpOutlook, ct,
            () => new SectionStepOutput(MarketBriefBuilder.BuildOutlook(goal.OccupationCode, reference!)));
        if (outlook == null)
        {
            return;
        }

        // The other candidates of the confirmed match; none when the match is gone.
        var alternatives = await LoadAlternativesAsync(run.OwnerId, goal, ct);
        var compared = await MarketStepAsync(run, steps, CareerStepNames.CompareAlternatives, ct,
            () => new SectionStepOutput(MarketBriefBuilder.BuildAlternatives(alternatives, reference!)));
        if (compared == null || !await CanStartStepAsync(run, steps, ct))
        {
            return;
        }

        var content = MarketBriefBuilder.Assemble(
            goal.OccupationCode, goal.OccupationTitle, wages.Location,
            new[] { wages.Wages, wages.Employment, outlook.Section, compared.Section }, reference!);
        var brief = new CareerMarketBrief
        {
            Id = Guid.NewGuid(),
            OwnerId = run.OwnerId,
            RunId = run.Id,
            PinnedProfileVersion = run.PinnedProfileVersion!.Value,
            PinnedGoalVersion = run.PinnedGoalVersion!.Value,
            OccupationCode = goal.OccupationCode,
            OccupationTitle = goal.OccupationTitle,
            OewsRelease = content.OewsRelease,
            ProjectionsRelease = content.ProjectionsRelease,
            Status = content.Status,
            PublishedJson = JsonSerializer.Serialize(content.Occupation.Published, MarketBriefJson.Options),
            LocationJson = JsonSerializer.Serialize(content.Location, MarketBriefJson.Options),
            SectionsJson = JsonSerializer.Serialize(content.Sections, MarketBriefJson.Options),
            SourcesJson = JsonSerializer.Serialize(content.Sources, MarketBriefJson.Options),
            NextActionJson = JsonSerializer.Serialize(content.NextAction, MarketBriefJson.Options),
            CreatedAt = Now()
        };
        // Saved in the same fenced write that completes the run, so a lost lease leaves no brief behind.
        _db.CareerMarketBriefs.Add(brief);
        AddStep(run, steps, CareerStepKinds.Save, CareerStepNames.SaveBrief, JsonSerializer.Serialize(new { briefId = brief.Id }));

        if (await FinishAsync(run, CareerRunStatus.Completed, null, ct))
        {
            _logger.LogInformation("Career run {RunId} completed with market brief {BriefId}", run.Id, brief.Id);
        }
    }

    /// <summary>Replays a saved step's result, or computes it and saves the step. Null when the run stopped.</summary>
    private async Task<T?> MarketStepAsync<T>(
        CareerAgentRun run, List<CareerAgentStep> steps, string name, CancellationToken ct, Func<T> compute) where T : class
    {
        var saved = steps.FirstOrDefault(s => s.Name == name);
        if (saved != null)
        {
            return JsonSerializer.Deserialize<T>(saved.OutputJson!, MarketBriefJson.Options);
        }

        if (!await CanStartStepAsync(run, steps, ct))
        {
            return null;
        }
        var result = compute();
        return await SaveStepAsync(run, steps, CareerStepKinds.Tool, name, JsonSerializer.Serialize(result, MarketBriefJson.Options), ct)
            ? result
            : null;
    }

    private async Task<IReadOnlyList<MarketAlternative>> LoadAlternativesAsync(string ownerId, CareerGoalVersion goal, CancellationToken ct)
    {
        if (goal.OccupationMatchId == null)
        {
            return Array.Empty<MarketAlternative>();
        }
        var json = await _db.CareerOccupationMatches.AsNoTracking()
            .Where(m => m.Id == goal.OccupationMatchId && m.OwnerId == ownerId)
            .Select(m => m.ResultJson).FirstOrDefaultAsync(ct);
        if (json == null)
        {
            return Array.Empty<MarketAlternative>();
        }

        var stored = JsonSerializer.Deserialize<StoredOccupationResult>(json, OccupationMatchJson.Options);
        return (stored?.Candidates ?? Array.Empty<OccupationCandidate>())
            .Where(c => c.Code != goal.OccupationCode)
            .Take(MaxMarketAlternatives)
            .Select(c => new MarketAlternative(c.Code, c.Title))
            .ToList();
    }

    private const int MaxMarketAlternatives = 3;

    private async Task AskAudienceAsync(CareerAgentRun run, List<CareerAgentStep> steps, CancellationToken ct)
    {
        if (!await CanStartStepAsync(run, steps, ct))
        {
            return;
        }

        await AskAsync(run, steps, CareerStepNames.AskAudience, CareerAgentRunService.AudienceQuestionId,
            CareerAgentRunService.AudienceQuestionText,
            JsonSerializer.Serialize(new { questionId = CareerAgentRunService.AudienceQuestionId }), ct);
    }

    /// <summary>Records the question step and parks the run in needs_input, holding no lease.</summary>
    private async Task AskAsync(
        CareerAgentRun run, List<CareerAgentStep> steps, string stepName, string questionId, string text, string outputJson, CancellationToken ct)
    {
        AddStep(run, steps, CareerStepKinds.Question, stepName, outputJson);
        run.Status = CareerRunStatus.NeedsInput;
        run.QuestionId = questionId;
        run.QuestionText = text;
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
        catch (CareerModelException ex)
        {
            // A paid answer that could not be used still counts toward the cost ceiling.
            run.CostCents += ex.CostCents;
            if (run.CostCents > _options.MaxCostCents)
            {
                _logger.LogWarning("Career run {RunId} stopped by the cost ceiling after {Code}", run.Id, ex.Code);
                await FinishAsync(run, CareerRunStatus.Failed, CareerAgentErrorCodes.CostLimit, ct);
                return null;
            }
            if (!ex.Retryable)
            {
                // Retrying cannot help (bad key, empty account, refusal, output limit): fail now.
                _logger.LogWarning("Career run {RunId} model call failed permanently: {Code}", run.Id, ex.Code);
                await FinishAsync(run, CareerRunStatus.Failed, CareerAgentErrorCodes.ModelFailed, ct);
                return null;
            }
            _logger.LogWarning("Career run {RunId} model call failed, will retry: {Code}", run.Id, ex.Code);
            await RetryOrFailModelAsync(run, ct);
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
