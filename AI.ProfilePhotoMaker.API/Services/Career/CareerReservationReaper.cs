using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface ICareerReservationReaper
{
    /// <summary>
    /// Ends queued or working runs untouched for the abandonment window and settles their reservation
    /// exactly once (spent if a model call started, else released). Returns how many runs were reaped.
    /// </summary>
    Task<int> ReapAsync(CancellationToken ct = default);
}

public sealed class CareerReservationReaper : ICareerReservationReaper
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;
    private readonly CareerUsagePolicy _policy;
    private readonly ILogger<CareerReservationReaper> _logger;

    public CareerReservationReaper(ApplicationDbContext db, TimeProvider clock, IOptions<CareerUsagePolicy> policy, ILogger<CareerReservationReaper> logger)
    {
        _db = db;
        _clock = clock;
        _policy = policy.Value;
        _logger = logger;
    }

    public async Task<int> ReapAsync(CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var cutoff = now.AddMinutes(-_policy.AbandonedReservationMinutes);
        // A run waiting for the user's answer (needs_input) is not abandoned; the runner expires it itself.
        var stale = await _db.CareerAgentRuns
            .Where(r => (r.Status == CareerRunStatus.Queued || r.Status == CareerRunStatus.Working) && r.UpdatedAt < cutoff)
            .OrderBy(r => r.UpdatedAt).Take(100).ToListAsync(ct);

        var reaped = 0;
        foreach (var run in stale)
        {
            run.Status = CareerRunStatus.Failed;
            run.ErrorCode = CareerAgentErrorCodes.Abandoned;
            run.LeaseOwner = null;
            run.LeaseExpiresAt = null;
            run.CompletedAt = now;
            run.UpdatedAt = now;
            // The fence also stops a late worker from settling the same reservation again.
            run.FencingToken++;
            var allowance = await _db.CareerAllowances.FirstOrDefaultAsync(
                a => a.OwnerId == run.OwnerId && a.PeriodStart == CareerAllowanceStore.PeriodStart(run.CreatedAt), ct);
            if (allowance != null)
            {
                CareerAllowanceStore.Settle(allowance, run.ModelCalled);
            }

            try
            {
                await _db.SaveChangesAsync(ct);
                reaped++;
                _logger.LogInformation("Career run {RunId} reaped as abandoned", run.Id);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A worker or a cancel settled it first; nothing to do.
                _db.ChangeTracker.Clear();
            }
        }
        return reaped;
    }
}

/// <summary>Runs the reaper on a timer.</summary>
public sealed class CareerReservationReaperService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly CareerUsagePolicy _policy;
    private readonly ILogger<CareerReservationReaperService> _logger;
    private readonly ICareerFeatureGate _gate;

    public CareerReservationReaperService(IServiceScopeFactory scopes, IOptions<CareerUsagePolicy> policy, ILogger<CareerReservationReaperService> logger,
        ICareerFeatureGate gate)
    {
        _gate = gate;
        _scopes = scopes;
        _policy = policy.Value;
        _logger = logger;
    }

    /// <summary>One pass; idle while the career flag is off (no runs are worked then, so nothing goes stale).</summary>
    public async Task ReapOnceAsync(CancellationToken ct)
    {
        if (!_gate.IsEnabled)
        {
            return;
        }
        using var scope = _scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICareerReservationReaper>().ReapAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReapOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _policy.ReaperPollSeconds)), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError("Career reservation reaper failed: {ExceptionType}", ex.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch (OperationCanceledException) { return; }
            }
        }
    }
}
