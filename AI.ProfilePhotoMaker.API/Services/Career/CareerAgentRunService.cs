using System.Security.Cryptography;
using System.Text;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface ICareerAgentRunService
{
    Task<CareerOutcome<CareerAgentRunDto>> CreateAsync(string ownerId, CreateCareerRunRequest request, string? idempotencyKey, CancellationToken ct = default);
    Task<CareerOutcome<CareerAgentRunDto>> GetAsync(string ownerId, Guid id, CancellationToken ct = default);
    Task<CareerOutcome<CareerRunListDto>> ListAsync(string ownerId, CancellationToken ct = default);
    Task<CareerOutcome<CareerAgentRunDto>> AnswerAsync(string ownerId, Guid id, AnswerCareerRunRequest request, CancellationToken ct = default);
    Task<CareerOutcome<CareerAgentRunDto>> CancelAsync(string ownerId, Guid id, CancellationToken ct = default);
}

/// <summary>
/// The request side of agent runs (ADR 0009): create with idempotency and allowance,
/// read, answer a question, cancel. Executing runs is the runner's job.
/// </summary>
public sealed class CareerAgentRunService : ICareerAgentRunService
{
    public const int MinKeyLength = 8;
    public const int MaxKeyLength = 100;
    public const int MaxAnswerLength = 200;
    public const int MaxListed = 20;
    public const string AudienceQuestionId = "audience";
    public const string AudienceQuestionText = "Who should this summary speak to?";
    public const string OccupationQuestionId = "occupation";
    public const string OccupationQuestionText = "Which of these is closest to the work you want analysed?";
    public const string NoOccupationChoice = "none";
    public const int MaxOccupationAnswerLength = 20;

    // A lost race is retried a few times; more than that means heavy contention.
    private const int MaxCommitAttempts = 5;
    private const string TaskError = "Choose a task the assistant can do: profile_summary, occupation_match, market_brief or pay_analysis.";

    private static readonly IReadOnlyDictionary<string, string> StepLabels = new Dictionary<string, string>
    {
        [CareerStepNames.ReadProfile] = "Read your confirmed profile",
        [CareerStepNames.ReadGoal] = "Read your career goal",
        [CareerStepNames.AskAudience] = "Asked who the summary is for",
        [CareerStepNames.DraftSummary] = "Drafted a summary",
        [CareerStepNames.SaveProposal] = "Saved the draft for your review",
        [CareerStepNames.MatchOccupations] = "Compared your duties with occupation tasks",
        [CareerStepNames.AskOccupation] = "Asked which occupation is closest",
        [CareerStepNames.SaveMatch] = "Saved the matches for your review",
        [CareerStepNames.LookUpWages] = "Looked up wages and employment",
        [CareerStepNames.LookUpOutlook] = "Looked up the job outlook",
        [CareerStepNames.CompareAlternatives] = "Compared related occupations",
        [CareerStepNames.SaveBrief] = "Saved your market brief",
        [CareerStepNames.ReadBenchmark] = "Looked up the occupational benchmark",
        [CareerStepNames.EvaluateCohort] = "Evaluated advertised-pay observations",
        [CareerStepNames.BuildScenario] = "Compared your requested pay",
        [CareerStepNames.SaveAnalysis] = "Saved your pay analysis"
    };

    private readonly ApplicationDbContext _db;
    private readonly CareerAgentOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<CareerAgentRunService> _logger;
    private readonly ICareerTextModel? _model;
    private readonly IOccupationReference? _reference;
    private readonly IMarketReference? _market;

    // The model is optional: with none registered, a profile summary fails closed (503).
    // Occupation matching needs no model, only the verified O*NET snapshot; market briefs only the BLS snapshot.
    public CareerAgentRunService(
        ApplicationDbContext db, IOptions<CareerAgentOptions> options, TimeProvider clock,
        ILogger<CareerAgentRunService> logger, ICareerTextModel? model = null, IOccupationReference? reference = null,
        IMarketReference? market = null)
    {
        _market = market;
        _db = db;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
        _model = model;
        _reference = reference;
    }

    public async Task<CareerOutcome<CareerAgentRunDto>> CreateAsync(
        string ownerId, CreateCareerRunRequest request, string? idempotencyKey, CancellationToken ct = default)
    {
        var task = request.Task?.Trim().ToLowerInvariant();
        var key = idempotencyKey?.Trim();
        var keyValid = key != null && key.Length >= MinKeyLength && key.Length <= MaxKeyLength;
        if (!keyValid)
        {
            var errors = new Dictionary<string, string>
            {
                ["idempotencyKey"] = $"Send an Idempotency-Key header of {MinKeyLength}-{MaxKeyLength} characters."
            };
            if (!IsKnownTask(task))
            {
                errors["task"] = TaskError;
            }
            return CareerOutcome<CareerAgentRunDto>.Invalid(errors);
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"task={task}"))).ToLowerInvariant();

        for (var attempt = 0; attempt < MaxCommitAttempts; attempt++)
        {
            var existing = await _db.CareerAgentRuns.AsNoTracking()
                .FirstOrDefaultAsync(r => r.OwnerId == ownerId && r.IdempotencyKey == key, ct);
            if (existing != null)
            {
                // A replay answers with the same run and never reserves a second unit. The key is
                // checked before the body, so reusing it with any other body is a mismatch.
                return existing.RequestHash == hash
                    ? CareerOutcome<CareerAgentRunDto>.Ok(await ToDtoAsync(existing, ct))
                    : CareerOutcome<CareerAgentRunDto>.AlreadyExists(
                        CareerAgentErrorCodes.IdempotencyMismatch, "That Idempotency-Key was already used for a different request.");
            }

            if (!IsKnownTask(task))
            {
                return CareerOutcome<CareerAgentRunDto>.Invalid(new Dictionary<string, string> { ["task"] = TaskError });
            }

            if (task == CareerAgentTasks.ProfileSummary && _model == null)
            {
                return CareerOutcome<CareerAgentRunDto>.ModelUnavailable();
            }
            if (task == CareerAgentTasks.OccupationMatch && _reference?.Data == null)
            {
                return CareerOutcome<CareerAgentRunDto>.ReferenceUnavailable();
            }

            if ((task is CareerAgentTasks.MarketBrief or CareerAgentTasks.PayAnalysis) && _market?.Oews == null && _market?.Projections == null)
            {
                return CareerOutcome<CareerAgentRunDto>.ReferenceUnavailable();
            }

            var profileVersion = await _db.CareerProfiles.AsNoTracking()
                .Where(p => p.OwnerId == ownerId).Select(p => (int?)p.ActiveVersionNumber).FirstOrDefaultAsync(ct);
            if (profileVersion == null)
            {
                return CareerOutcome<CareerAgentRunDto>.AlreadyExists(
                    CareerAgentErrorCodes.ProfileRequired, "Save and confirm your career profile before asking the assistant.");
            }
            var goalVersion = await _db.CareerGoals.AsNoTracking()
                .Where(g => g.OwnerId == ownerId).Select(g => (int?)g.ActiveVersionNumber).FirstOrDefaultAsync(ct);
            if ((task is CareerAgentTasks.MarketBrief or CareerAgentTasks.PayAnalysis)
                && (goalVersion == null || !await _db.CareerGoalVersions.AsNoTracking().AnyAsync(
                    v => v.OwnerId == ownerId && v.VersionNumber == goalVersion && v.OccupationCode != null, ct)))
            {
                return CareerOutcome<CareerAgentRunDto>.AlreadyExists(
                    CareerOccupationErrorCodes.OccupationRequired, task == CareerAgentTasks.PayAnalysis
                        ? "Confirm your target occupation before asking for a pay analysis."
                        : "Confirm your target occupation before asking for a market brief.");
            }

            var now = Now();
            var period = CareerAllowanceStore.PeriodStart(now);
            // Tracked before the run is added: the allowance is the row a racing create contends for.
            var allowance = await _db.CareerAllowances.FirstOrDefaultAsync(a => a.OwnerId == ownerId && a.PeriodStart == period, ct);
            var committed = (allowance?.Reserved ?? 0) + (allowance?.Used ?? 0);
            if (committed >= _options.MonthlyRunAllowance)
            {
                return CareerOutcome<CareerAgentRunDto>.QuotaExceeded(
                    CareerAgentErrorCodes.AllowanceExhausted, "You have used this month's assistant runs.");
            }
            if (allowance == null)
            {
                allowance = new CareerAllowance { Id = Guid.NewGuid(), OwnerId = ownerId, PeriodStart = period };
                _db.CareerAllowances.Add(allowance);
            }
            CareerAllowanceStore.Reserve(allowance);

            var run = new CareerAgentRun
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                Task = task!,
                Status = CareerRunStatus.Queued,
                IdempotencyKey = key!,
                RequestHash = hash,
                PinnedProfileVersion = profileVersion,
                PinnedGoalVersion = goalVersion,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.CareerAgentRuns.Add(run);

            try
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Career run {RunId} queued", run.Id);
                return CareerOutcome<CareerAgentRunDto>.Ok(await ToDtoAsync(run, ct));
            }
            catch (DbUpdateException ex) when (CareerProfileService.IsLostRace(ex))
            {
                // Another request took the key (unique index) or the allowance row moved.
                // Start over: the loop returns the winner's run, or re-checks the allowance.
                _db.ChangeTracker.Clear();
            }
        }

        return CareerOutcome<CareerAgentRunDto>.Busy("CareerRunBusy", "Too many requests at once. Try again shortly.", 1);
    }

    public async Task<CareerOutcome<CareerAgentRunDto>> GetAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        var run = await _db.CareerAgentRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, ct);
        return run == null ? NotFound() : CareerOutcome<CareerAgentRunDto>.Ok(await ToDtoAsync(run, ct));
    }

    public async Task<CareerOutcome<CareerRunListDto>> ListAsync(string ownerId, CancellationToken ct = default)
    {
        var runs = await _db.CareerAgentRuns.AsNoTracking()
            .Where(r => r.OwnerId == ownerId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(MaxListed)
            .ToListAsync(ct);

        // One query each for steps, profile version and allowance, however many runs are listed.
        var runIds = runs.Select(r => r.Id).ToList();
        var stepsByRun = (await _db.CareerAgentSteps.AsNoTracking()
                .Where(s => s.OwnerId == ownerId && runIds.Contains(s.RunId))
                .ToListAsync(ct))
            .ToLookup(s => s.RunId);
        var currentProfile = await CurrentProfileVersionAsync(ownerId, ct);
        var allowance = await AllowanceDtoAsync(ownerId, ct);

        var matchIds = await _db.CareerOccupationMatches.AsNoTracking()
            .Where(m => m.OwnerId == ownerId && runIds.Contains(m.RunId))
            .ToDictionaryAsync(m => m.RunId, m => m.Id, ct);
        var briefIds = await _db.CareerMarketBriefs.AsNoTracking()
            .Where(b => b.OwnerId == ownerId && runIds.Contains(b.RunId))
            .ToDictionaryAsync(b => b.RunId, b => b.Id, ct);

        var payIds = await _db.CareerPayAnalyses.AsNoTracking()
            .Where(b => b.OwnerId == ownerId && runIds.Contains(b.RunId))
            .ToDictionaryAsync(b => b.RunId, b => b.Id, ct);

        var dtos = runs.Select(run => ToDto(
            run, stepsByRun[run.Id], currentProfile, allowance,
            matchIds.TryGetValue(run.Id, out var matchId) ? matchId : null,
            briefIds.TryGetValue(run.Id, out var briefId) ? briefId : null,
            payIds.TryGetValue(run.Id, out var payId) ? payId : null)).ToList();
        return CareerOutcome<CareerRunListDto>.Ok(new CareerRunListDto(dtos, allowance));
    }

    public async Task<CareerOutcome<CareerAgentRunDto>> AnswerAsync(
        string ownerId, Guid id, AnswerCareerRunRequest request, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxCommitAttempts; attempt++)
        {
            // Ownership first, so another owner's run reveals nothing.
            var run = await _db.CareerAgentRuns.FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, ct);
            if (run == null)
            {
                return NotFound();
            }

            var occupation = run.Task == CareerAgentTasks.OccupationMatch;
            var expectedQuestion = occupation ? OccupationQuestionId : AudienceQuestionId;
            var maxAnswer = occupation ? MaxOccupationAnswerLength : MaxAnswerLength;
            var errors = new Dictionary<string, string>();
            if (request.QuestionId != expectedQuestion)
            {
                errors["questionId"] = "That is not the question this run is asking.";
            }
            var answer = request.Answer?.Trim();
            if (string.IsNullOrEmpty(answer) || answer.Length > maxAnswer)
            {
                errors["answer"] = $"Answer in 1-{maxAnswer} characters.";
            }
            if (errors.Count > 0)
            {
                return CareerOutcome<CareerAgentRunDto>.Invalid(errors);
            }

            if (run.Status != CareerRunStatus.NeedsInput)
            {
                return CareerOutcome<CareerAgentRunDto>.AlreadyExists(
                    CareerAgentErrorCodes.RunNotWaiting, "This run is not waiting for an answer.");
            }

            if (occupation)
            {
                // The answer picks one of the offered choices; it can never invent a candidate.
                var steps = await _db.CareerAgentSteps.AsNoTracking().Where(s => s.RunId == run.Id && s.OwnerId == ownerId).ToListAsync(ct);
                if (ChoicesFrom(steps)?.Any(c => c.Value == answer) != true)
                {
                    return CareerOutcome<CareerAgentRunDto>.Invalid(new Dictionary<string, string>
                    {
                        ["answer"] = "Choose one of the listed occupations, or none of these."
                    });
                }
            }

            var now = Now();
            run.Answer = answer;
            run.QuestionId = null;
            run.QuestionText = null;
            run.Status = CareerRunStatus.Queued;
            // A fresh budget for the part after the question; waiting for the user is not the worker's time.
            run.Attempts = 0;
            run.Deadline = now.AddSeconds(_options.MaxRunSeconds);
            run.UpdatedAt = now;
            // Bumped so a double submit or a cancel racing this answer loses cleanly.
            run.FencingToken++;

            try
            {
                await _db.SaveChangesAsync(ct);
                return CareerOutcome<CareerAgentRunDto>.Ok(await ToDtoAsync(run, ct));
            }
            catch (DbUpdateException ex) when (CareerProfileService.IsLostRace(ex))
            {
                _db.ChangeTracker.Clear();
            }
        }

        return CareerOutcome<CareerAgentRunDto>.Busy("CareerRunBusy", "Too many requests at once. Try again shortly.", 1);
    }

    public async Task<CareerOutcome<CareerAgentRunDto>> CancelAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxCommitAttempts; attempt++)
        {
            var run = await _db.CareerAgentRuns.FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, ct);
            if (run == null)
            {
                return NotFound();
            }
            if (run.Status is CareerRunStatus.Completed or CareerRunStatus.Failed or CareerRunStatus.Cancelled)
            {
                // Idempotent: a finished run is returned as it is.
                return CareerOutcome<CareerAgentRunDto>.Ok(await ToDtoAsync(run, ct));
            }

            var now = Now();
            run.Status = CareerRunStatus.Cancelled;
            run.LeaseOwner = null;
            run.LeaseExpiresAt = null;
            run.QuestionId = null;
            run.QuestionText = null;
            run.CompletedAt = now;
            run.UpdatedAt = now;
            // The fence: a worker still holding the old token can no longer save anything.
            run.FencingToken++;

            var allowance = await _db.CareerAllowances.FirstOrDefaultAsync(
                a => a.OwnerId == ownerId && a.PeriodStart == CareerAllowanceStore.PeriodStart(run.CreatedAt), ct);
            if (allowance != null)
            {
                CareerAllowanceStore.Settle(allowance, run.ModelCalled);
            }

            try
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Career run {RunId} cancelled", run.Id);
                return CareerOutcome<CareerAgentRunDto>.Ok(await ToDtoAsync(run, ct));
            }
            catch (DbUpdateException ex) when (CareerProfileService.IsLostRace(ex))
            {
                // The worker (or another cancel) moved the run first; look again.
                _db.ChangeTracker.Clear();
            }
        }

        return CareerOutcome<CareerAgentRunDto>.Busy("CareerRunBusy", "Too many requests at once. Try again shortly.", 1);
    }

    // ---- Mapping ---------------------------------------------------------------

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    private static bool IsKnownTask(string? task) => task is CareerAgentTasks.ProfileSummary or CareerAgentTasks.OccupationMatch or CareerAgentTasks.MarketBrief or CareerAgentTasks.PayAnalysis;

    /// <summary>The offered choices, saved with the question step so the answer can be checked against them.</summary>
    internal static IReadOnlyList<CareerRunChoiceDto>? ChoicesFrom(IEnumerable<CareerAgentStep> steps)
    {
        var step = steps.FirstOrDefault(s => s.Name == CareerStepNames.AskOccupation);
        if (step?.OutputJson == null)
        {
            return null;
        }
        using var document = System.Text.Json.JsonDocument.Parse(step.OutputJson);
        return document.RootElement.TryGetProperty("choices", out var choices)
            ? System.Text.Json.JsonSerializer.Deserialize<List<CareerRunChoiceDto>>(choices.GetRawText(), OccupationMatchJson.Options)
            : null;
    }

    private static CareerOutcome<CareerAgentRunDto> NotFound() =>
        CareerOutcome<CareerAgentRunDto>.NotFound(CareerAgentErrorCodes.RunNotFound, "That run was not found.");

    internal static string StatusName(CareerRunStatus status) => status switch
    {
        CareerRunStatus.Queued => "queued",
        CareerRunStatus.Working => "working",
        CareerRunStatus.NeedsInput => "needs_input",
        CareerRunStatus.Completed => "completed",
        CareerRunStatus.Failed => "failed",
        _ => "cancelled"
    };

    private async Task<CareerAllowanceDto> AllowanceDtoAsync(string ownerId, CancellationToken ct)
    {
        var period = CareerAllowanceStore.PeriodStart(Now());
        var allowance = await _db.CareerAllowances.AsNoTracking().FirstOrDefaultAsync(a => a.OwnerId == ownerId && a.PeriodStart == period, ct);
        return new CareerAllowanceDto(allowance?.Used ?? 0, allowance?.Reserved ?? 0, _options.MonthlyRunAllowance, period);
    }

    private async Task<CareerAgentRunDto> ToDtoAsync(CareerAgentRun run, CancellationToken ct)
    {
        var steps = await _db.CareerAgentSteps.AsNoTracking()
            .Where(s => s.RunId == run.Id && s.OwnerId == run.OwnerId).ToListAsync(ct);
        var matchId = await _db.CareerOccupationMatches.AsNoTracking()
            .Where(m => m.RunId == run.Id && m.OwnerId == run.OwnerId).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);
        var briefId = await _db.CareerMarketBriefs.AsNoTracking()
            .Where(b => b.RunId == run.Id && b.OwnerId == run.OwnerId).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        var payId = await _db.CareerPayAnalyses.AsNoTracking()
            .Where(b => b.RunId == run.Id && b.OwnerId == run.OwnerId).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        return ToDto(run, steps, await CurrentProfileVersionAsync(run.OwnerId, ct), await AllowanceDtoAsync(run.OwnerId, ct), matchId, briefId, payId);
    }

    private async Task<int?> CurrentProfileVersionAsync(string ownerId, CancellationToken ct) =>
        await _db.CareerProfiles.AsNoTracking()
            .Where(p => p.OwnerId == ownerId).Select(p => (int?)p.ActiveVersionNumber).FirstOrDefaultAsync(ct);

    private static CareerAgentRunDto ToDto(
        CareerAgentRun run, IEnumerable<CareerAgentStep> steps, int? currentProfile, CareerAllowanceDto allowance, Guid? occupationMatchId,
        Guid? marketBriefId, Guid? payAnalysisId)
    {
        var stepList = steps.ToList();
        return new CareerAgentRunDto(
            run.Id,
            run.Task,
            StatusName(run.Status),
            Utc(run.CreatedAt),
            Utc(run.UpdatedAt),
            run.CompletedAt is { } completed ? Utc(completed) : null,
            run.PinnedProfileVersion,
            run.PinnedGoalVersion,
            stepList.OrderBy(s => s.Ordinal).Select(s => new CareerRunStepDto(
                s.Ordinal, s.Kind, s.Name, StepLabels.GetValueOrDefault(s.Name, s.Name), s.Status,
                s.CompletedAt is { } done ? Utc(done) : null)).ToList(),
            run.Status == CareerRunStatus.NeedsInput && run.QuestionId != null
                ? new CareerRunQuestionDto(
                    run.QuestionId, run.QuestionText ?? AudienceQuestionText,
                    run.Task == CareerAgentTasks.OccupationMatch ? MaxOccupationAnswerLength : MaxAnswerLength,
                    run.Task == CareerAgentTasks.OccupationMatch ? ChoicesFrom(stepList) : null)
                : null,
            run.ProposalId,
            occupationMatchId,
            marketBriefId,
            ProfileChanged: run.PinnedProfileVersion != null && currentProfile != run.PinnedProfileVersion,
            run.ErrorCode,
            allowance,
            payAnalysisId);
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
