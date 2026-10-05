using AI.ProfilePhotoMaker.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
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
/// Tombstones are batched: one purge per owner and scope, using the newest cutoff. A group that cannot be
/// applied completely is persisted as failed (<see cref="CareerTombstone.ReplayFailedAt"/>) and the owner's
/// career endpoints answer 503 until a later replay succeeds. Running it again does nothing.
/// </summary>
public sealed class CareerTombstoneReplayer : ICareerTombstoneReplayer
{
    private readonly ApplicationDbContext _db;
    private readonly ICareerPrivateDataService _data;
    private readonly ILogger<CareerTombstoneReplayer> _logger;
    private readonly TimeProvider _clock;

    public CareerTombstoneReplayer(ApplicationDbContext db, ICareerPrivateDataService data, ILogger<CareerTombstoneReplayer> logger, TimeProvider? clock = null)
    {
        _db = db;
        _data = data;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<int> ApplyAsync(CancellationToken ct = default)
    {
        var tombstones = await _db.CareerTombstones.AsNoTracking().OrderBy(t => t.CreatedAt).ToListAsync(ct);
        var groups = tombstones.GroupBy(t => (t.OwnerId, t.Scope)).ToList();
        var failed = 0;
        foreach (var group in groups)
        {
            var newest = group.OrderByDescending(t => t.CreatedAt).First();
            var ok = false;
            try
            {
                ok = await _data.ApplyTombstoneAsync(newest, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One owner's failure must not stop the others.
                _logger.LogError(ex, "Replaying career tombstone {TombstoneId} failed", newest.Id);
            }
            _db.ChangeTracker.Clear();

            var now = _clock.GetUtcNow().UtcDateTime;
            var ids = group.Select(t => t.Id).ToList();
            foreach (var row in await _db.CareerTombstones.Where(t => ids.Contains(t.Id)).ToListAsync(ct))
            {
                row.ReplayedAt = ok ? now : row.ReplayedAt;
                row.ReplayFailedAt = ok ? null : now;
            }
            await _db.SaveChangesAsync(ct);
            if (!ok)
            {
                failed++;
            }
        }
        _logger.LogInformation("Career tombstone replay applied {Groups} owner/scope groups, {Failed} incomplete", groups.Count, failed);
        return failed;
    }
}

/// <summary>True while a failed replay leaves an owner's career data possibly restored (fail closed).</summary>
public sealed class CareerReplayGate
{
    public const string ErrorCode = "CareerPrivacyReplayPending";

    private readonly ApplicationDbContext _db;

    public CareerReplayGate(ApplicationDbContext db) => _db = db;

    public Task<bool> IsPendingAsync(string ownerId, CancellationToken ct = default) =>
        _db.CareerTombstones.AsNoTracking().AnyAsync(t => t.OwnerId == ownerId && t.ReplayFailedAt != null, ct);
}

/// <summary>Marks an action that must keep working while the owner's replay is pending (deletion and its status).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowWhileCareerReplayPendingAttribute : Attribute
{
}

/// <summary>Answers 503 <c>CareerPrivacyReplayPending</c> on career endpoints for an owner whose tombstone replay failed.</summary>
public sealed class CareerReplayGuardFilter : IAsyncActionFilter
{
    private readonly CareerReplayGate _gate;

    public CareerReplayGuardFilter(CareerReplayGate gate) => _gate = gate;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var owner = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(owner)
            && !context.ActionDescriptor.EndpointMetadata.OfType<AllowWhileCareerReplayPendingAttribute>().Any()
            && await _gate.IsPendingAsync(owner, context.HttpContext.RequestAborted))
        {
            context.HttpContext.Response.Headers["Retry-After"] = "60";
            context.Result = new ObjectResult(new
            {
                success = false,
                error = new
                {
                    code = CareerReplayGate.ErrorCode,
                    message = "Your career data is being checked after a restore. Try again shortly.",
                    retryAfterSeconds = 60,
                    correlationId = context.HttpContext.TraceIdentifier
                }
            })
            { StatusCode = StatusCodes.Status503ServiceUnavailable };
            return;
        }
        await next();
    }
}

/// <summary>
/// Runs <see cref="ICareerTombstoneReplayer"/> at startup and, while it reports incomplete work, again in the
/// background with a doubling wait. Program.cs only, so the test host never runs it.
/// </summary>
public sealed class CareerTombstoneReplayHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CareerTombstoneReplayHostedService> _logger;
    private readonly CareerPrivacyOptions _options;
    private readonly CancellationTokenSource _stop = new();
    private Task _retry = Task.CompletedTask;

    public CareerTombstoneReplayHostedService(
        IServiceScopeFactory scopes, ILogger<CareerTombstoneReplayHostedService> logger, IOptions<CareerPrivacyOptions>? options = null)
    {
        _scopes = scopes;
        _logger = logger;
        _options = options?.Value ?? new CareerPrivacyOptions();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!await TryReplayAsync(cancellationToken))
        {
            _retry = Task.Run(() => RetryAsync(_stop.Token));
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stop.Cancel();
        try
        {
            await _retry.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>True when nothing is left incomplete.</summary>
    private async Task<bool> TryReplayAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ICareerTombstoneReplayer>().ApplyAsync(ct) == 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never block startup: the schema may not be migrated yet.
            _logger.LogError(ex, "Career tombstone replay failed");
            return false;
        }
    }

    private async Task RetryAsync(CancellationToken ct)
    {
        var wait = Math.Max(1, _options.ReplayRetryMilliseconds);
        try
        {
            while (true)
            {
                await Task.Delay(wait, ct);
                if (await TryReplayAsync(ct))
                {
                    return;
                }
                wait = (int)Math.Min(Math.Max(wait, _options.MaxReplayRetryMilliseconds), (long)wait * 2);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
