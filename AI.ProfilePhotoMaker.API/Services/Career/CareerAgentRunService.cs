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

    // A lost race is retried a few times; more than that means heavy contention.
    private const int MaxCommitAttempts = 5;
    private const string TaskError = "Choose a task the assistant can do: profile_summary.";

    private static readonly IReadOnlyDictionary<string, string> StepLabels = new Dictionary<string, string>
    {
        [CareerStepNames.ReadProfile] = "Read your confirmed profile",
        [CareerStepNames.ReadGoal] = "Read your career goal",
        [CareerStepNames.AskAudience] = "Asked who the summary is for",
        [CareerStepNames.DraftSummary] = "Drafted a summary",
        [CareerStepNames.SaveProposal] = "Saved the draft for your review"
    };

    private readonly ApplicationDbContext _db;
    private readonly CareerAgentOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<CareerAgentRunService> _logger;
    private readonly ICareerTextModel? _model;

    // The model is optional: with none registered, creating a run fails closed (503).
    public CareerAgentRunService(
        ApplicationDbContext db, IOptions<CareerAgentOptions> options, TimeProvider clock,
        ILogger<CareerAgentRunService> logger, ICareerTextModel? model = null)
    {
        _db = db;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
        _model = model;
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
            if (task != CareerAgentTasks.ProfileSummary)
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

            if (task != CareerAgentTasks.ProfileSummary)
            {
                return CareerOutcome<CareerAgentRunDto>.Invalid(new Dictionary<string, string> { ["task"] = TaskError });
            }

            if (_model == null)
            {
                return CareerOutcome<CareerAgentRunDto>.ModelUnavailable();
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
                Task = CareerAgentTasks.ProfileSummary,
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

        var dtos = runs.Select(run => ToDto(run, stepsByRun[run.Id], currentProfile, allowance)).ToList();
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

            var errors = new Dictionary<string, string>();
            if (request.QuestionId != AudienceQuestionId)
            {
                errors["questionId"] = "That is not the question this run is asking.";
            }
            var answer = request.Answer?.Trim();
            if (string.IsNullOrEmpty(answer) || answer.Length > MaxAnswerLength)
            {
                errors["answer"] = $"Answer in 1-{MaxAnswerLength} characters.";
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
        return ToDto(run, steps, await CurrentProfileVersionAsync(run.OwnerId, ct), await AllowanceDtoAsync(run.OwnerId, ct));
    }

    private async Task<int?> CurrentProfileVersionAsync(string ownerId, CancellationToken ct) =>
        await _db.CareerProfiles.AsNoTracking()
            .Where(p => p.OwnerId == ownerId).Select(p => (int?)p.ActiveVersionNumber).FirstOrDefaultAsync(ct);

    private static CareerAgentRunDto ToDto(
        CareerAgentRun run, IEnumerable<CareerAgentStep> steps, int? currentProfile, CareerAllowanceDto allowance)
    {
        return new CareerAgentRunDto(
            run.Id,
            run.Task,
            StatusName(run.Status),
            Utc(run.CreatedAt),
            Utc(run.UpdatedAt),
            run.CompletedAt is { } completed ? Utc(completed) : null,
            run.PinnedProfileVersion,
            run.PinnedGoalVersion,
            steps.OrderBy(s => s.Ordinal).Select(s => new CareerRunStepDto(
                s.Ordinal, s.Kind, s.Name, StepLabels.GetValueOrDefault(s.Name, s.Name), s.Status,
                s.CompletedAt is { } done ? Utc(done) : null)).ToList(),
            run.Status == CareerRunStatus.NeedsInput && run.QuestionId != null
                ? new CareerRunQuestionDto(run.QuestionId, run.QuestionText ?? AudienceQuestionText, MaxAnswerLength)
                : null,
            run.ProposalId,
            ProfileChanged: run.PinnedProfileVersion != null && currentProfile != run.PinnedProfileVersion,
            run.ErrorCode,
            allowance);
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
