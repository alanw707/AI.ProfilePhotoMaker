using AI.ProfilePhotoMaker.API.Data;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface ICareerTombstoneReplayer
{
    /// <summary>Re-applies every tombstone. Idempotent; returns how many could not be fully applied.</summary>
    Task<int> ApplyAsync(CancellationToken ct = default);
}

/// <summary>
/// Backup restore protection (ADR 0020): each tombstone removes rows of its scope that were created at or
/// before it, so a restored backup cannot bring deleted career data back, while data created afterwards stays.
/// Running it again does nothing. Cost grows with the number of tombstones, not with data size.
/// </summary>
public sealed class CareerTombstoneReplayer : ICareerTombstoneReplayer
{
    private readonly ApplicationDbContext _db;
    private readonly ICareerPrivateDataService _data;
    private readonly ILogger<CareerTombstoneReplayer> _logger;

    public CareerTombstoneReplayer(ApplicationDbContext db, ICareerPrivateDataService data, ILogger<CareerTombstoneReplayer> logger)
    {
        _db = db;
        _data = data;
        _logger = logger;
    }

    public async Task<int> ApplyAsync(CancellationToken ct = default)
    {
        var tombstones = await _db.CareerTombstones.AsNoTracking().OrderBy(t => t.CreatedAt).ToListAsync(ct);
        var failed = 0;
        foreach (var tombstone in tombstones)
        {
            try
            {
                if (!await _data.ApplyTombstoneAsync(tombstone, ct))
                {
                    failed++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One owner's failure must not stop the others; the next startup tries again.
                failed++;
                _db.ChangeTracker.Clear();
                _logger.LogError(ex, "Replaying career tombstone {TombstoneId} failed", tombstone.Id);
            }
        }
        _logger.LogInformation("Career tombstone replay applied {Count} tombstones, {Failed} incomplete", tombstones.Count, failed);
        return failed;
    }
}

/// <summary>Runs <see cref="ICareerTombstoneReplayer"/> once at startup. Program.cs only, so the test host never runs it.</summary>
public sealed class CareerTombstoneReplayHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CareerTombstoneReplayHostedService> _logger;

    public CareerTombstoneReplayHostedService(IServiceScopeFactory scopes, ILogger<CareerTombstoneReplayHostedService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ICareerTombstoneReplayer>().ApplyAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never block startup: the schema may not be migrated yet; the replay is retried on the next start.
            _logger.LogError(ex, "Career tombstone replay failed at startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
