using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>
/// Server-side usage policy (ADR 0022), bound from <c>Career:Usage</c>. Values are provisional until the
/// owner approves the budget; bump <see cref="PolicyVersion"/> whenever one changes.
/// </summary>
public sealed class CareerUsagePolicy
{
    public const string SectionName = "Career:Usage";

    public string PolicyVersion { get; set; } = "2026-10-provisional";

    /// <summary>Null keeps <c>Career:Agent:MonthlyRunAllowance</c> (the current value).</summary>
    public int? MonthlyRunAllowance { get; set; }
    public int PerMinuteRunLimit { get; set; } = 6;
    public int MaxConcurrentRunsPerUser { get; set; } = 2;
    public int MaxQueuedRunsGlobal { get; set; } = 200;
    public decimal MonthlyModelCostCapUsd { get; set; } = 50m;
    public decimal PerUserMonthlyModelCostCapUsd { get; set; } = 2m;
    /// <summary>Global model spend allowed per UTC day; null leaves only the monthly caps.</summary>
    public decimal? DailyModelCostCapUsd { get; set; }
    /// <summary>Estimated model cost of one run per task, in USD; a task not listed is model-free and costs 0.</summary>
    public Dictionary<string, decimal> TaskCostEstimatesUsd { get; set; } = new() { [CareerAgentTasks.ProfileSummary] = 0.01m };
    public int ControlsCacheSeconds { get; set; } = 30;

    /// <summary>A queued or working run untouched this long is abandoned and its reservation released.</summary>
    public int AbandonedReservationMinutes { get; set; } = 30;
    public int ReaperPollSeconds { get; set; } = 300;
}

public sealed record CareerControlsDto(bool GenerationDisabled, bool SourcesDisabled, DateTime? UpdatedAt, string? UpdatedBy);

public interface ICareerOperatorControls
{
    /// <summary>The switches, cached for <see cref="CareerUsagePolicy.ControlsCacheSeconds"/>.</summary>
    Task<CareerControlsDto> GetAsync(CancellationToken ct = default);

    /// <summary>Writes the switches (null leaves one as it is) and drops the cache.</summary>
    Task<CareerControlsDto> UpdateAsync(bool? generationDisabled, bool? sourcesDisabled, string? updatedBy, CancellationToken ct = default);
}

public sealed class CareerOperatorControls : ICareerOperatorControls
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly CareerUsagePolicy _policy;
    private readonly object _gate = new();
    private CareerControlsDto? _cached;
    private DateTimeOffset _expires;

    public CareerOperatorControls(IServiceScopeFactory scopes, TimeProvider clock, IOptions<CareerUsagePolicy> policy)
    {
        _scopes = scopes;
        _clock = clock;
        _policy = policy.Value;
    }

    public async Task<CareerControlsDto> GetAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_cached != null && _clock.GetUtcNow() < _expires)
            {
                return _cached;
            }
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.CareerOperatorStates.AsNoTracking().FirstOrDefaultAsync(s => s.Id == CareerOperatorState.SingletonId, ct);
        var dto = ToDto(row);
        lock (_gate)
        {
            _cached = dto;
            _expires = _clock.GetUtcNow().AddSeconds(_policy.ControlsCacheSeconds);
        }
        return dto;
    }

    public async Task<CareerControlsDto> UpdateAsync(bool? generationDisabled, bool? sourcesDisabled, string? updatedBy, CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.CareerOperatorStates.FirstOrDefaultAsync(s => s.Id == CareerOperatorState.SingletonId, ct);
        if (row == null)
        {
            row = new CareerOperatorState();
            db.CareerOperatorStates.Add(row);
        }
        row.GenerationDisabled = generationDisabled ?? row.GenerationDisabled;
        row.SourcesDisabled = sourcesDisabled ?? row.SourcesDisabled;
        row.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        row.UpdatedBy = updatedBy;
        await db.SaveChangesAsync(ct);

        var dto = ToDto(row);
        lock (_gate)
        {
            _cached = dto;
            _expires = _clock.GetUtcNow().AddSeconds(_policy.ControlsCacheSeconds);
        }
        return dto;
    }

    private static CareerControlsDto ToDto(CareerOperatorState? row) => row == null
        ? new CareerControlsDto(false, false, null, null)
        : new CareerControlsDto(row.GenerationDisabled, row.SourcesDisabled, DateTime.SpecifyKind(row.UpdatedAt, DateTimeKind.Utc), row.UpdatedBy);
}

/// <summary>Builds usage rows. Callers pass counts and codes only, never text.</summary>
internal static class CareerUsage
{
    public static CareerUsageEvent Event(
        string ownerId, Guid? runId, string action, string outcome, DateTime now, int latencyMs,
        string? model = null, int tokens = 0, int costCents = 0, int? sourceAgeSeconds = null) => new()
    {
        Id = Guid.NewGuid(), OwnerId = ownerId, RunId = runId, Action = action, Outcome = outcome, CreatedAt = now,
        LatencyMs = Math.Max(0, latencyMs), Model = model, Tokens = Math.Max(0, tokens), CostCents = Math.Max(0, costCents),
        SourceAgeSeconds = sourceAgeSeconds
    };

    public static CareerOutcome<T> Paused<T>() =>
        CareerOutcome<T>.Busy(CareerAgentErrorCodes.GenerationPaused, "The career assistant is paused right now. Your saved work is unaffected.", 300);
}
