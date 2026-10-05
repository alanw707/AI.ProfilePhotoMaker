using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>
/// Optional photo handoff (ADR 0008). Read-only over photos and entitlements; the
/// only thing it writes is the owner's <see cref="CareerPhotoSelection"/>. It never
/// calls generation, refinement or purchase code, so it cannot spend anything.
/// </summary>
public interface ICareerPhotoService
{
    Task<CareerOutcome<CareerPhotoListDto>> ListAsync(string ownerId, CancellationToken ct = default);
    Task<CareerOutcome<CareerPhotoSelectionDto>> SelectAsync(string ownerId, int? processedImageId, CancellationToken ct = default);
    Task<CareerOutcome<bool>> ClearAsync(string ownerId, CancellationToken ct = default);
}

public sealed class CareerPhotoService : ICareerPhotoService
{
    private const int MaxPhotos = 24;
    private const string PrivateSegment = "generated-private";

    private readonly ApplicationDbContext _db;
    private readonly IStorageService _storage;
    private readonly IOutcomePackageService _packages;
    private readonly TimeProvider _time;

    public CareerPhotoService(
        ApplicationDbContext db,
        IStorageService storage,
        IOutcomePackageService packages,
        TimeProvider time)
    {
        _db = db;
        _storage = storage;
        _packages = packages;
        _time = time;
    }

    public async Task<CareerOutcome<CareerPhotoListDto>> ListAsync(string ownerId, CancellationToken ct = default)
    {
        var rows = await EligibleImages(ownerId)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .Take(MaxPhotos)
            .Select(i => new { i.Id, i.ProcessedImageUrl, i.CreatedAt, i.Style, i.RawImageStoragePath })
            .ToListAsync(ct);

        var photos = rows
            .Select(i => new CareerPhotoDto(
                i.Id,
                ResolveUrl(i.ProcessedImageUrl),
                i.CreatedAt,
                i.Style,
                IsWatermarkedPreview: !string.IsNullOrEmpty(i.RawImageStoragePath)))
            .ToList();

        var selection = await _db.CareerPhotoSelections.AsNoTracking().FirstOrDefaultAsync(s => s.OwnerId == ownerId, ct);
        var available = false;
        if (selection != null)
        {
            // Checked against the full eligible set, not the 24 listed, so an older choice stays usable.
            var chosen = await EligibleImages(ownerId)
                .Where(i => i.Id == selection.ProcessedImageId)
                .Select(i => i.RawImageStoragePath)
                .ToListAsync(ct);
            available = chosen.Count == 1 && string.IsNullOrEmpty(chosen[0]);
        }

        var entitlements = (await _packages.GetUserEntitlementsAsync(ownerId, ct))
            .Where(e => string.Equals(e.Status, "active", StringComparison.OrdinalIgnoreCase))
            .Select(e => new CareerPhotoEntitlementDto(
                e.PackageCode,
                e.PackageName,
                e.RemainingCandidates,
                e.RemainingRefinements,
                e.RemainingPremiumAugmentations,
                e.PlatformExportKitAvailable,
                e.ExpiresAt))
            .ToList();

        return CareerOutcome<CareerPhotoListDto>.Ok(new CareerPhotoListDto(photos, selection?.ProcessedImageId, available, entitlements));
    }

    public async Task<CareerOutcome<CareerPhotoSelectionDto>> SelectAsync(string ownerId, int? processedImageId, CancellationToken ct = default)
    {
        if (processedImageId is not > 0)
        {
            return CareerOutcome<CareerPhotoSelectionDto>.Invalid(new Dictionary<string, string>
            {
                ["processedImageId"] = "Choose one of your photos."
            });
        }

        var image = await EligibleImages(ownerId)
            .Where(i => i.Id == processedImageId)
            .Select(i => new { i.Id, i.RawImageStoragePath })
            .FirstOrDefaultAsync(ct);

        // Another owner's image looks identical to a missing one so ids cannot be probed.
        if (image == null)
        {
            return CareerOutcome<CareerPhotoSelectionDto>.NotFound(CareerErrorCodes.PhotoNotFound, "We could not find that photo.");
        }

        if (!string.IsNullOrEmpty(image.RawImageStoragePath))
        {
            return new CareerOutcome<CareerPhotoSelectionDto>(
                CareerOutcomeKind.AlreadyExists,
                ErrorCode: CareerErrorCodes.PhotoIsPreview,
                Message: "This is a free preview. Improve it in the photo workspace before using it.");
        }

        var goalId = await _db.CareerGoals.AsNoTracking()
            .Where(g => g.OwnerId == ownerId)
            .Select(g => (Guid?)g.Id)
            .FirstOrDefaultAsync(ct);

        CareerPhotoSelection selection;
        try
        {
            selection = await UpsertSelectionAsync(ownerId, image.Id, goalId, ct);
        }
        catch (DbUpdateException ex) when (CareerProfileService.IsLostRace(ex))
        {
            // Another first-time choice created this owner's row (unique owner index).
            // Choosing is idempotent, so apply this choice to the winner's row.
            _db.ChangeTracker.Clear();
            selection = await UpsertSelectionAsync(ownerId, image.Id, goalId, ct);
        }

        return CareerOutcome<CareerPhotoSelectionDto>.Ok(new CareerPhotoSelectionDto(selection.ProcessedImageId, selection.CareerGoalId, selection.SelectedAt));
    }

    private async Task<CareerPhotoSelection> UpsertSelectionAsync(string ownerId, int imageId, Guid? goalId, CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var selection = await _db.CareerPhotoSelections.FirstOrDefaultAsync(s => s.OwnerId == ownerId, ct);
        if (selection == null)
        {
            selection = new CareerPhotoSelection { Id = Guid.NewGuid(), OwnerId = ownerId };
            _db.CareerPhotoSelections.Add(selection);
        }

        selection.ProcessedImageId = imageId;
        selection.CareerGoalId = goalId;
        selection.SelectedAt = now;
        selection.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return selection;
    }

    public async Task<CareerOutcome<bool>> ClearAsync(string ownerId, CancellationToken ct = default)
    {
        // Deleting nothing is success: the caller only cares that no photo is chosen.
        _db.CareerPhotoSelections.RemoveRange(await _db.CareerPhotoSelections.Where(s => s.OwnerId == ownerId).ToListAsync(ct));
        await _db.SaveChangesAsync(ct);
        return CareerOutcome<bool>.Ok(true);
    }

    /// <summary>The owner's successful generated images, excluding uploads and private raw assets.</summary>
    private IQueryable<ProcessedImage> EligibleImages(string ownerId) =>
        _db.ProcessedImages.AsNoTracking()
            .Where(i => i.UserProfile.UserId == ownerId
                && i.IsGenerated
                && !i.IsOriginalUpload
                && i.GenerationStatus != null
                && i.GenerationStatus.ToLower() == "succeeded"
                && i.ProcessedImageUrl != ""
                && !i.ProcessedImageUrl.ToLower().Contains(PrivateSegment));

    private string ResolveUrl(string path) =>
        path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : _storage.GetImageUrl(path);
}
