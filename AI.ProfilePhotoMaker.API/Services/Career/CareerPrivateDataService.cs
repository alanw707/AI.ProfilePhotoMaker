using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>Settings for the purge job, bound from <c>Career:Privacy</c>.</summary>
public sealed class CareerPrivacyOptions
{
    public const string SectionName = "Career:Privacy";

    /// <summary>Wait after a failed blob-delete round, doubled each round (tests set 0).</summary>
    public int PurgeBackoffMilliseconds { get; set; } = 200;
}

/// <summary>A purge that could not finish. Account deletion aborts on it instead of orphaning career data.</summary>
public sealed class CareerPurgeException : Exception
{
    public CareerPurgeException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>
/// Removes an owner's private career data. Every private career entity must be
/// listed in <see cref="CareerPrivateDataService.CoveredEntityTypes"/>; tests compare this list with the EF
/// model, the purge order and the export, so a new entity cannot ship without purge and export coverage (ADR 0020).
/// </summary>
public interface ICareerPrivateDataService
{
    /// <summary>
    /// Account deletion and the career_profile scope in one call: writes a tombstone, cancels runs, removes
    /// every covered row and blob. Throws <see cref="CareerPurgeException"/> when anything could not be removed.
    /// </summary>
    Task DeleteAllForOwnerAsync(string ownerId, CancellationToken ct = default);

    /// <summary>Creates a deletion request plus tombstone and purges. Storage failures end as status <c>failed</c>, not an exception.</summary>
    Task<CareerDeletionRequest> DeleteAsync(string ownerId, string scope, CancellationToken ct = default);

    /// <summary>Runs the purge of an existing request again (the retry endpoint).</summary>
    Task<CareerDeletionRequest> RetryAsync(CareerDeletionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Re-applies one tombstone: removes rows of the scope created at or before it (a restored backup).
    /// Returns false when something could not be removed.
    /// </summary>
    Task<bool> ApplyTombstoneAsync(CareerTombstone tombstone, CancellationToken ct = default);
}

public sealed class CareerPrivateDataService : ICareerPrivateDataService
{
    public const int MaxBlobRounds = 5;
    public const string StorageFailureCode = "storage_unavailable";
    public const string PurgeFailureCode = "purge_failed";

    /// <summary>
    /// Every private career entity. <see cref="CareerDeletionRequest"/> and <see cref="CareerTombstone"/> are
    /// intentionally absent: they are audit records that must survive the purge they record. They are exported
    /// (as <c>auditRecords</c>) but never purged here.
    /// </summary>
    public static readonly IReadOnlySet<Type> CoveredEntityTypes = new HashSet<Type>
    {
        typeof(CareerProfile),
        typeof(CareerProfileVersion),
        typeof(CareerGoal),
        typeof(CareerGoalVersion),
        typeof(ResumeDocument),
        typeof(CareerProfileProposal),
        typeof(CareerProfileProposalItem),
        typeof(CareerPhotoSelection),
        typeof(CareerAgentRun),
        typeof(CareerAgentStep),
        typeof(CareerAllowance),
        typeof(CareerOccupationMatch),
        typeof(CareerMarketBrief),
        typeof(CareerPayAnalysis),
        typeof(CareerRoadmap),
        typeof(CareerRoadmapTaskProgress),
        typeof(CareerRoadmapReplan),
        typeof(CareerMaterial),
        typeof(CareerMaterialVersion),
        typeof(CareerMaterialProposal),
        typeof(CareerExport)
    };

    /// <summary>
    /// Dependents before their parents (the behaviour is the same on providers that do not enforce foreign
    /// keys). <see cref="ResumeDocument"/> is last on purpose: its row carries the blob key, so it is the work
    /// queue for blob deletes and only goes once its file is gone.
    /// </summary>
    internal static readonly IReadOnlyList<Type> PurgeOrder = new[]
    {
        typeof(CareerProfileVersion), typeof(CareerGoalVersion), typeof(CareerAgentStep), typeof(CareerAgentRun),
        typeof(CareerAllowance), typeof(CareerOccupationMatch), typeof(CareerMarketBrief), typeof(CareerPayAnalysis),
        typeof(CareerRoadmapTaskProgress), typeof(CareerExport), typeof(CareerMaterialProposal),
        typeof(CareerMaterialVersion), typeof(CareerMaterial), typeof(CareerRoadmapReplan), typeof(CareerRoadmap),
        typeof(CareerProfileProposalItem), typeof(CareerProfileProposal),
        // Only the choice is removed; the photo itself belongs to the photo workspace.
        typeof(CareerPhotoSelection), typeof(CareerProfile), typeof(CareerGoal),
        typeof(ResumeDocument)
    };

    /// <summary>
    /// The property that says when a row was created, which decides what a replayed tombstone may remove.
    /// Defaults to <c>CreatedAt</c>; the few entities without one use the closest equivalent. Proposal items have
    /// none (null): they are dated by their proposal.
    /// </summary>
    internal static string? TimestampProperty(Type type) =>
        type == typeof(CareerProfileProposalItem) ? null
        : type == typeof(CareerAllowance) ? nameof(CareerAllowance.PeriodStart)
        : type == typeof(CareerPhotoSelection) ? nameof(CareerPhotoSelection.SelectedAt)
        : type == typeof(CareerRoadmapTaskProgress) ? nameof(CareerRoadmapTaskProgress.UpdatedAt)
        : "CreatedAt";

    private readonly ApplicationDbContext _db;
    private readonly IStorageService _storage;
    private readonly TimeProvider _clock;
    private readonly ILogger<CareerPrivateDataService> _logger;
    private readonly int _backoffMs;

    // Optional trailing parameters keep the original two-argument construction working.
    public CareerPrivateDataService(
        ApplicationDbContext db, IStorageService storage, TimeProvider? clock = null,
        IOptions<CareerPrivacyOptions>? options = null, ILogger<CareerPrivateDataService>? logger = null)
    {
        _db = db;
        _storage = storage;
        _clock = clock ?? TimeProvider.System;
        _backoffMs = Math.Max(0, (options?.Value ?? new CareerPrivacyOptions()).PurgeBackoffMilliseconds);
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CareerPrivateDataService>.Instance;
    }

    public async Task DeleteAllForOwnerAsync(string ownerId, CancellationToken ct = default)
    {
        var request = await DeleteAsync(ownerId, CareerDeletionScopes.CareerProfile, ct);
        if (request.Status != CareerDeletionStatuses.Completed)
        {
            throw new CareerPurgeException($"Career data could not be fully deleted ({request.LastError}).");
        }
    }

    public async Task<CareerDeletionRequest> DeleteAsync(string ownerId, string scope, CancellationToken ct = default)
    {
        var now = Now();
        // The tombstone is written first and on its own: even if the purge dies half way, a replay finishes it.
        _db.CareerTombstones.Add(new CareerTombstone { Id = Guid.NewGuid(), OwnerId = ownerId, Scope = scope, CreatedAt = now });
        var request = new CareerDeletionRequest
        {
            Id = Guid.NewGuid(), OwnerId = ownerId, Scope = scope,
            Status = CareerDeletionStatuses.InProgress, CreatedAt = now, UpdatedAt = now
        };
        _db.CareerDeletionRequests.Add(request);
        await _db.SaveChangesAsync(ct);

        // A request removes everything that exists now, including rows made after the tombstone and before the purge.
        return await RunAsync(request, cutoff: null, ct);
    }

    public async Task<CareerDeletionRequest> RetryAsync(CareerDeletionRequest request, CancellationToken ct = default)
    {
        request.Status = CareerDeletionStatuses.InProgress;
        request.LastError = null;
        request.UpdatedAt = Now();
        await _db.SaveChangesAsync(ct);

        // A retry must not remove what the user created after asking for the deletion.
        return await RunAsync(request, cutoff: request.CreatedAt, ct);
    }

    public async Task<bool> ApplyTombstoneAsync(CareerTombstone tombstone, CancellationToken ct = default)
    {
        var (_, error) = await PurgeAsync(tombstone.OwnerId, tombstone.Scope, tombstone.CreatedAt, ct);
        return error == null;
    }

    private async Task<CareerDeletionRequest> RunAsync(CareerDeletionRequest request, DateTime? cutoff, CancellationToken ct)
    {
        var attempts = 0;
        string? error;
        try
        {
            (attempts, error) = await PurgeAsync(request.OwnerId, request.Scope, cutoff, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Career purge {RequestId} failed", request.Id);
            _db.ChangeTracker.Clear();
            request = await _db.CareerDeletionRequests.SingleAsync(r => r.Id == request.Id, ct);
            error = PurgeFailureCode;
        }

        var now = Now();
        request.Attempts += attempts;
        request.UpdatedAt = now;
        request.Status = error == null ? CareerDeletionStatuses.Completed : CareerDeletionStatuses.Failed;
        request.LastError = error;
        request.CompletedAt = error == null ? now : null;
        await _db.SaveChangesAsync(ct);
        return request;
    }

    /// <summary>Removes the scope's rows and blobs. Returns the blob rounds used and a stable error code, or null when all is gone.</summary>
    private async Task<(int Attempts, string? Error)> PurgeAsync(string ownerId, string scope, DateTime? cutoff, CancellationToken ct)
    {
        if (scope == CareerDeletionScopes.CareerProfile)
        {
            await CancelActiveRunsAsync(ownerId, cutoff, ct);
            foreach (var type in PurgeOrder.Where(t => t != typeof(ResumeDocument)))
            {
                await PurgeTypeAsync(type, ownerId, cutoff, ct);
            }
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            await ClearExcerptsAsync(ownerId, cutoff, ct);
            await _db.SaveChangesAsync(ct);
        }

        return await DeleteResumesAsync(ownerId, cutoff, ct);
    }

    /// <summary>
    /// The fence: a worker that loaded one of these runs loses its token on its next write. The rows go
    /// right after, so even a worker that never writes again leaves nothing behind.
    /// </summary>
    private async Task CancelActiveRunsAsync(string ownerId, DateTime? cutoff, CancellationToken ct)
    {
        var now = Now();
        var runs = await _db.CareerAgentRuns
            .Where(r => r.OwnerId == ownerId && (cutoff == null || r.CreatedAt <= cutoff)
                && (r.Status == CareerRunStatus.Queued || r.Status == CareerRunStatus.Working || r.Status == CareerRunStatus.NeedsInput))
            .ToListAsync(ct);
        foreach (var run in runs)
        {
            run.Status = CareerRunStatus.Cancelled;
            run.LeaseOwner = null;
            run.LeaseExpiresAt = null;
            run.QuestionId = null;
            run.QuestionText = null;
            run.CompletedAt = now;
            run.UpdatedAt = now;
            run.FencingToken++;
        }
        if (runs.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private static readonly System.Reflection.MethodInfo PurgeTypeMethod =
        typeof(CareerPrivateDataService).GetMethod(nameof(PurgeTypeAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, new[] { typeof(string), typeof(DateTime?), typeof(CancellationToken) })!;

    private Task PurgeTypeAsync(Type type, string ownerId, DateTime? cutoff, CancellationToken ct) =>
        type == typeof(CareerProfileProposalItem) ? PurgeItemsAsync(ownerId, cutoff, ct) : (Task)PurgeTypeMethod.MakeGenericMethod(type).Invoke(this, new object?[] { ownerId, cutoff, ct })!;

    private async Task PurgeTypeAsync<T>(string ownerId, DateTime? cutoff, CancellationToken ct) where T : class
    {
        var timestamp = TimestampProperty(typeof(T))!;
        var rows = await _db.Set<T>()
            .Where(e => EF.Property<string>(e, "OwnerId") == ownerId
                && (cutoff == null || EF.Property<DateTime>(e, timestamp) <= cutoff))
            .ToListAsync(ct);
        _db.Set<T>().RemoveRange(rows);
    }

    private async Task PurgeItemsAsync(string ownerId, DateTime? cutoff, CancellationToken ct)
    {
        var proposalIds = await _db.CareerProfileProposals
            .Where(p => p.OwnerId == ownerId && (cutoff == null || p.CreatedAt <= cutoff))
            .Select(p => p.Id).ToListAsync(ct);
        _db.CareerProfileProposalItems.RemoveRange(await _db.CareerProfileProposalItems
            .Where(i => i.OwnerId == ownerId && (cutoff == null || proposalIds.Contains(i.ProposalId)))
            .ToListAsync(ct));
    }

    /// <summary>raw_documents also removes the source line copied into each proposal item.</summary>
    private async Task ClearExcerptsAsync(string ownerId, DateTime? cutoff, CancellationToken ct)
    {
        var proposalIds = await _db.CareerProfileProposals
            .Where(p => p.OwnerId == ownerId && (cutoff == null || p.CreatedAt <= cutoff))
            .Select(p => p.Id).ToListAsync(ct);
        var items = await _db.CareerProfileProposalItems
            .Where(i => i.OwnerId == ownerId && proposalIds.Contains(i.ProposalId) && i.Excerpt != "")
            .ToListAsync(ct);
        foreach (var item in items)
        {
            item.Excerpt = string.Empty;
            item.Page = null;
            item.Section = null;
        }
    }

    /// <summary>
    /// Raw files go before their rows: a row without its file is recoverable, a file without its row is not.
    /// Each round retries only what is still there, with a growing wait between rounds.
    /// </summary>
    private async Task<(int Attempts, string? Error)> DeleteResumesAsync(string ownerId, DateTime? cutoff, CancellationToken ct)
    {
        var attempts = 0;
        for (var round = 1; round <= MaxBlobRounds; round++)
        {
            var resumes = await _db.CareerResumeDocuments
                .Where(d => d.OwnerId == ownerId && (cutoff == null || d.CreatedAt <= cutoff))
                .ToListAsync(ct);
            if (resumes.Count == 0)
            {
                return (attempts, null);
            }

            attempts++;
            var failed = 0;
            foreach (var resume in resumes)
            {
                if (await TryDeleteBlobAsync(resume.StorageKey))
                {
                    _db.CareerResumeDocuments.Remove(resume);
                }
                else
                {
                    failed++;
                }
            }
            await _db.SaveChangesAsync(ct);

            if (failed == 0)
            {
                return (attempts, null);
            }
            if (round < MaxBlobRounds && _backoffMs > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(_backoffMs * Math.Pow(2, round - 1)), ct);
            }
        }
        return (attempts, StorageFailureCode);
    }

    private async Task<bool> TryDeleteBlobAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return true;
        }
        try
        {
            // A false answer may only mean "already gone"; only a file that still exists is a failure.
            return await _storage.DeleteImageAsync(key) || !await _storage.ExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Career blob delete failed");
            return false;
        }
    }

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;
}
