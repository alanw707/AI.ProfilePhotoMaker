using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

public class OutcomePackageService : IOutcomePackageService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OutcomePackageService> _logger;
    private readonly IStorageService? _storageService;

    public OutcomePackageService(
        ApplicationDbContext context,
        ILogger<OutcomePackageService> logger,
        IStorageService? storageService = null)
    {
        _context = context;
        _logger = logger;
        _storageService = storageService;
    }

    public async Task<IReadOnlyList<OutcomePackageDefinitionDto>> GetActivePackageDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var packages = await _context.OutcomePackageDefinitions
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(cancellationToken);

        return packages.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<UserPackageEntitlementDto>> GetUserEntitlementsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var entitlements = await _context.UserPackageEntitlements
            .Include(e => e.OutcomePackageDefinition)
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        return entitlements.Select(ToDto).ToList();
    }

    public async Task<bool> CanPromotePreviewAsync(
        string userId,
        int previewProcessedImageId,
        CancellationToken cancellationToken = default)
    {
        var preview = await FindPreviewAsync(userId, previewProcessedImageId, cancellationToken);
        return preview?.RawImageStoragePath is { Length: > 0 } rawPath &&
               await PreviewRawExistsAsync(rawPath);
    }

    public async Task<bool> ReservePreviewForPurchaseAsync(
        string userId,
        int previewProcessedImageId,
        CancellationToken cancellationToken = default)
    {
        var preview = await FindPreviewAsync(userId, previewProcessedImageId, cancellationToken);
        if (preview?.RawImageStoragePath is not { Length: > 0 } rawPath ||
            !await PreviewRawExistsAsync(rawPath))
        {
            return false;
        }

        var reservationExpiry = DateTime.UtcNow.AddHours(24);
        if (preview.ScheduledDeletionDate < reservationExpiry)
        {
            preview.ScheduledDeletionDate = reservationExpiry;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private Task<ProcessedImage?> FindPreviewAsync(
        string userId,
        int previewProcessedImageId,
        CancellationToken cancellationToken) =>
        _context.ProcessedImages
            .Include(i => i.UserProfile)
            .FirstOrDefaultAsync(i =>
                i.Id == previewProcessedImageId &&
                i.UserProfile.UserId == userId &&
                i.GenerationStatus == "succeeded" &&
                i.GenerationMode == "instant_headshot" &&
                i.RawImageStoragePath != null,
                cancellationToken);

    private async Task<bool> PreviewRawExistsAsync(string rawPath)
    {
        if (_storageService == null)
        {
            return true;
        }

        try
        {
            return await _storageService.ExistsAsync(rawPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to verify preview promotion path {StoragePath}", rawPath);
            return false;
        }
    }

    public async Task<UserPackageEntitlement?> GrantEntitlementForCreditPackageAsync(
        string userId,
        int creditPackageId,
        string? paymentTransactionId,
        int? previewProcessedImageId = null,
        CancellationToken cancellationToken = default)
    {
        if (previewProcessedImageId.HasValue &&
            !await CanPromotePreviewAsync(userId, previewProcessedImageId.Value, cancellationToken))
        {
            throw new InvalidOperationException("The selected preview is unavailable for promotion.");
        }

        var definition = await _context.OutcomePackageDefinitions
            .Where(p => p.IsActive && p.InternalCreditPackageId == creditPackageId)
            .OrderBy(p => p.DisplayOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (definition == null)
        {
            _logger.LogDebug("No outcome package mapping for internal credit package {CreditPackageId}", creditPackageId);
            return null;
        }

        int? transactionId = null;
        if (int.TryParse(paymentTransactionId, out var parsedTransactionId))
        {
            transactionId = parsedTransactionId;
            var existing = await _context.UserPackageEntitlements
                .Include(e => e.OutcomePackageDefinition)
                .FirstOrDefaultAsync(e => e.SourcePaymentTransactionId == parsedTransactionId, cancellationToken);
            if (existing != null)
            {
                var promoted = await PromotePreviewAsync(existing, userId, previewProcessedImageId, cancellationToken);
                if (!promoted)
                {
                    throw new InvalidOperationException("The available preview could not be promoted.");
                }
                return existing;
            }
        }

        var entitlement = new UserPackageEntitlement
        {
            UserId = userId,
            OutcomePackageDefinitionId = definition.Id,
            SourcePaymentTransactionId = transactionId,
            Status = PackageEntitlementStatus.Active,
            RemainingPackageUses = 1,
            RemainingCandidates = definition.IncludedCandidateCount,
            RemainingRefinements = definition.IncludedRefinementCount,
            RemainingPremiumAugmentations = definition.IncludedPremiumAugmentationCount,
            PlatformExportKitAvailable = definition.IncludesPlatformExportKit,
            ActivatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.UserPackageEntitlements.Add(entitlement);
        await _context.SaveChangesAsync(cancellationToken);
        var previewPromoted = await PromotePreviewAsync(entitlement, userId, previewProcessedImageId, cancellationToken);
        if (!previewPromoted)
        {
            throw new InvalidOperationException("The available preview could not be promoted.");
        }

        return entitlement;
    }

    public async Task<ResumableHeadshotPreviewDto?> GetResumablePreviewAsync(
        string userId,
        int? previewId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var paidEntitlements = await _context.UserPackageEntitlements
            .Include(e => e.OutcomePackageDefinition)
            .Where(e =>
                e.UserId == userId &&
                (e.Status == PackageEntitlementStatus.Active ||
                 e.Status == PackageEntitlementStatus.Consumed) &&
                (e.ExpiresAt == null || e.ExpiresAt > now) &&
                (e.OutcomePackageDefinition.Code == "starter_package" ||
                 e.OutcomePackageDefinition.Code == "pro_package"))
            .OrderByDescending(e => e.Status == PackageEntitlementStatus.Active)
            .ThenByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
        var entitlement = paidEntitlements.FirstOrDefault();

        // Free Preview records are the only headshots with both a watermarked display path
        // and a private raw path. Paid candidates are never valid resume anchors.
        var previews = _context.ProcessedImages
            .Include(i => i.UserProfile)
            .Where(i =>
                i.UserProfile.UserId == userId &&
                i.GenerationStatus == "succeeded" &&
                i.GenerationMode == "instant_headshot" &&
                i.RawImageStoragePath != null &&
                i.ProcessedImageUrl != i.RawImageStoragePath);
        ProcessedImage? preview;
        if (previewId.HasValue)
        {
            preview = await previews.FirstOrDefaultAsync(i => i.Id == previewId.Value, cancellationToken);
        }
        else
        {
            preview = null;
            if (entitlement != null)
            {
                var entitlementPromotion = await _context.ProcessedImages.FirstOrDefaultAsync(i =>
                    i.UserProfile.UserId == userId &&
                    i.GenerationMode == "instant_headshot_promoted_preview" &&
                    i.GenerationStatus == "succeeded" &&
                    i.CorrelationId == $"purchase:{entitlement.Id}:promoted-preview",
                    cancellationToken);
                if (entitlementPromotion != null)
                {
                    preview = await previews
                        .Where(i => i.RawImageStoragePath == entitlementPromotion.ProcessedImageUrl)
                        .OrderByDescending(i => i.CreatedAt)
                        .FirstOrDefaultAsync(cancellationToken);
                }
            }
            preview ??= await previews
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (preview == null ||
            !await _context.Styles.AnyAsync(s => s.IsActive && s.Name == preview.Style, cancellationToken))
        {
            return null;
        }

        // A package may already own durable paid candidates even after the
        // watermarked preview display copy has expired. The display copy is
        // required only to resume an unpaid preview; do not hide paid work.
        var previewDisplayExists = await StorageImageExistsAsync(preview.ProcessedImageUrl);
        if (!previewDisplayExists && entitlement == null)
        {
            return null;
        }

        var rawStoragePath = preview.RawImageStoragePath!;
        var rawPreviewExists = await StorageImageExistsAsync(rawStoragePath);
        var sourceExists = await StorageImageExistsAsync(preview.OriginalImageUrl);
        var promotedImage = rawPreviewExists
            ? await _context.ProcessedImages.FirstOrDefaultAsync(i =>
                i.UserProfileId == preview.UserProfileId &&
                i.GenerationMode == "instant_headshot_promoted_preview" &&
                i.GenerationStatus == "succeeded" &&
                i.ProcessedImageUrl == rawStoragePath,
                cancellationToken)
            : null;
        if (promotedImage?.CorrelationId?.Split(':') is ["purchase", var entitlementId, ..] &&
            int.TryParse(entitlementId, out var linkedEntitlementId))
        {
            entitlement = paidEntitlements.FirstOrDefault(item => item.Id == linkedEntitlementId) ?? entitlement;
        }
        var restoredCandidates = new List<ProcessedImage>();
        if (promotedImage != null)
        {
            restoredCandidates.Add(promotedImage);
        }

        if (entitlement != null)
        {
            var packageCandidateLimit = Math.Max(
                entitlement.OutcomePackageDefinition.IncludedCandidateCount - restoredCandidates.Count,
                0);
            var entitlementStartedAt = entitlement.ActivatedAt ?? entitlement.CreatedAt;
            var generatedCandidates = await _context.ProcessedImages
                .Where(i =>
                    i.UserProfileId == preview.UserProfileId &&
                    i.Id != preview.Id &&
                    i.GenerationMode == "instant_headshot" &&
                    i.GenerationStatus == "succeeded" &&
                    i.ReplacesProcessedImageId == null &&
                    i.OriginalImageUrl == preview.OriginalImageUrl &&
                    i.Style == preview.Style &&
                    i.CreatedAt >= entitlementStartedAt)
                .OrderBy(i => i.CreatedAt)
                .ThenBy(i => i.Id)
                .Take(packageCandidateLimit)
                .ToListAsync(cancellationToken);
            restoredCandidates.AddRange(generatedCandidates);

            var refinements = await _context.ProcessedImages
                .Where(i =>
                    i.UserProfileId == preview.UserProfileId &&
                    i.GenerationMode == "instant_headshot" &&
                    i.GenerationStatus == "succeeded" &&
                    i.ReplacesProcessedImageId != null &&
                    i.OriginalImageUrl == preview.OriginalImageUrl &&
                    i.Style == preview.Style &&
                    i.CreatedAt >= entitlementStartedAt)
                .OrderBy(i => i.CreatedAt)
                .ThenBy(i => i.Id)
                .ToListAsync(cancellationToken);
            foreach (var refinement in refinements)
            {
                var replacedIndex = restoredCandidates.FindIndex(candidate =>
                    candidate.Id == refinement.ReplacesProcessedImageId);
                if (replacedIndex >= 0)
                {
                    restoredCandidates[replacedIndex] = refinement;
                }
            }
        }
        var candidates = restoredCandidates.Select(ToCandidateDto).ToList();

        return new ResumableHeadshotPreviewDto
        {
            ProcessedImageId = preview.Id,
            ImageUrl = preview.ProcessedImageUrl,
            StoragePath = preview.ProcessedImageUrl,
            SourceStoragePath = preview.OriginalImageUrl,
            SourceAvailable = sourceExists,
            Style = preview.Style,
            CreatedAt = preview.CreatedAt,
            HasRawPreview = rawPreviewExists,
            CanPromotePreview = entitlement != null && entitlement.RemainingCandidates > 0 && rawPreviewExists && promotedImage == null,
            ActivePackageCode = entitlement?.OutcomePackageDefinition.Code,
            RemainingCandidateCount = entitlement == null
                ? 0
                : Math.Max(entitlement.RemainingCandidates - (rawPreviewExists && promotedImage == null ? 1 : 0), 0),
            PromotedCandidate = promotedImage == null ? null : ToCandidateDto(promotedImage),
            Candidates = candidates,
            Message = entitlement == null
                ? (rawPreviewExists
                    ? "Resume this preview, then unlock Starter or Pro to generate paid candidates."
                    : "This preview can be viewed, but its generation source expired. Start over to create paid candidates.")
                : promotedImage != null
                    ? (sourceExists ? "Your preview is unlocked. Generate the remaining paid candidates when ready." : "Your preview is unlocked. The original upload expired; remaining candidates will continue from it.")
                    : rawPreviewExists
                        ? "Your package is active. Your preview will be unlocked in the workspace."
                        : "Your package is active, but this preview asset expired. Start a new photo set."
        };
    }

    public async Task<bool> AbandonPreviewAsync(
        string userId,
        int previewId,
        CancellationToken cancellationToken = default)
    {
        var preview = await FindPreviewAsync(userId, previewId, cancellationToken);
        if (preview == null || preview.ProcessedImageUrl == preview.RawImageStoragePath)
        {
            return false;
        }

        var isPromoted = await _context.ProcessedImages.AnyAsync(i =>
            i.UserProfileId == preview.UserProfileId &&
            i.GenerationMode == "instant_headshot_promoted_preview" &&
            i.GenerationStatus == "succeeded" &&
            i.ProcessedImageUrl == preview.RawImageStoragePath,
            cancellationToken);
        if (isPromoted)
        {
            return false;
        }

        preview.GenerationStatus = "abandoned";
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PromotedPreviewDownload?> GetPromotedPreviewDownloadAsync(
        string userId,
        int imageId,
        CancellationToken cancellationToken = default)
    {
        var image = await _context.ProcessedImages
            .Include(i => i.UserProfile)
            .FirstOrDefaultAsync(i =>
                i.Id == imageId &&
                i.UserProfile.UserId == userId &&
                (i.GenerationMode == "instant_headshot_promoted_preview" ||
                 i.GenerationMode == "instant_headshot") &&
                i.GenerationStatus == "succeeded",
                cancellationToken);
        if (image == null)
        {
            return null;
        }

        var ownsPackage = await _context.UserPackageEntitlements
            .Include(e => e.OutcomePackageDefinition)
            .AnyAsync(e =>
                e.UserId == userId &&
                (e.OutcomePackageDefinition.Code == "starter_package" || e.OutcomePackageDefinition.Code == "pro_package") &&
                e.Status != PackageEntitlementStatus.Refunded &&
                e.Status != PackageEntitlementStatus.Revoked,
                cancellationToken);
        if (!ownsPackage || _storageService == null)
        {
            return null;
        }

        var stream = await _storageService.GetImageAsync(image.ProcessedImageUrl);
        return stream == null ? null : new PromotedPreviewDownload(stream, image.Id);
    }

    private async Task<bool> StorageImageExistsAsync(string storagePath)
    {
        if (_storageService == null || string.IsNullOrWhiteSpace(storagePath))
        {
            return false;
        }

        try
        {
            return await _storageService.ExistsAsync(storagePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to verify preview storage path {StoragePath}", storagePath);
            return false;
        }
    }

    private static HeadshotCandidateDto ToCandidateDto(ProcessedImage image) => new()
    {
        ImageUrl = $"/api/headshots/images/{image.Id}/original",
        StoragePath = image.ProcessedImageUrl,
        ProcessedImageId = image.Id,
        Provider = image.Provider ?? string.Empty,
        Model = image.ProviderModel ?? string.Empty,
        CorrelationId = image.CorrelationId ?? string.Empty
    };

    private async Task<bool> PromotePreviewAsync(
        UserPackageEntitlement entitlement,
        string userId,
        int? previewProcessedImageId,
        CancellationToken cancellationToken)
    {
        if (entitlement.Status != PackageEntitlementStatus.Active || entitlement.RemainingCandidates <= 0)
        {
            return false;
        }

        var previews = _context.ProcessedImages
            .Include(i => i.UserProfile)
            .Where(i =>
                i.UserProfile.UserId == userId &&
                i.GenerationStatus == "succeeded" &&
                i.GenerationMode == "instant_headshot" &&
                i.RawImageStoragePath != null);
        var preview = previewProcessedImageId.HasValue
            ? await previews.FirstOrDefaultAsync(i => i.Id == previewProcessedImageId.Value, cancellationToken)
            : await previews.OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync(cancellationToken);

        if (preview == null)
        {
            return !previewProcessedImageId.HasValue;
        }

        if (preview.RawImageStoragePath is not { Length: > 0 } rawPath ||
            (_storageService != null && await _storageService.ExistsAsync(rawPath) == false))
        {
            return false;
        }

        var existingPromotion = await _context.ProcessedImages.FirstOrDefaultAsync(i =>
            i.UserProfileId == preview.UserProfileId &&
            i.GenerationMode == "instant_headshot_promoted_preview" &&
            i.GenerationStatus == "succeeded" &&
            i.ProcessedImageUrl == rawPath,
            cancellationToken);
        if (existingPromotion != null)
        {
            return true;
        }

        var promotedImage = new ProcessedImage
        {
            OriginalImageUrl = preview.OriginalImageUrl,
            ProcessedImageUrl = rawPath,
            Style = preview.Style,
            UserProfileId = preview.UserProfileId,
            CreatedAt = DateTime.UtcNow,
            IsGenerated = true,
            Provider = preview.Provider,
            ProviderModel = preview.ProviderModel,
            GenerationMode = "instant_headshot_promoted_preview",
            PromptVersion = preview.PromptVersion,
            CreditCost = 0,
            GenerationStatus = "succeeded",
            CorrelationId = $"purchase:{entitlement.Id}:promoted-preview"
        };
        promotedImage.SetScheduledDeletionDate();
        _context.ProcessedImages.Add(promotedImage);
        entitlement.RemainingCandidates--;
        entitlement.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            _context.Entry(promotedImage).State = EntityState.Detached;
            await _context.Entry(entitlement).ReloadAsync(cancellationToken);

            var concurrentPromotionExists = await _context.ProcessedImages.AnyAsync(i =>
                i.UserProfileId == preview.UserProfileId &&
                i.GenerationMode == "instant_headshot_promoted_preview" &&
                i.GenerationStatus == "succeeded" &&
                i.ProcessedImageUrl == rawPath,
                cancellationToken);
            if (concurrentPromotionExists)
            {
                _logger.LogInformation(
                    "Preview {PreviewId} was promoted concurrently; using the existing paid candidate",
                    preview.Id);
                return true;
            }

            throw;
        }
    }

    public async Task<UserPackageEntitlement?> GetActiveEntitlementAsync(string userId, string packageCode, CancellationToken cancellationToken = default)
    {
        return await QueryActiveEntitlements(userId)
            .Where(e => e.OutcomePackageDefinition.Code == packageCode)
            .Where(e => e.RemainingPackageUses > 0 ||
                        e.RemainingCandidates > 0 ||
                        e.RemainingRefinements > 0 ||
                        e.RemainingPremiumAugmentations > 0 ||
                        e.PlatformExportKitAvailable)
            .OrderByDescending(e => e.RemainingPackageUses > 0 && e.RemainingCandidates > 0)
            .ThenBy(e => e.ExpiresAt ?? DateTime.MaxValue)
            .ThenBy(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ConsumeCandidatesAsync(string userId, string packageCode, int candidateCount, CancellationToken cancellationToken = default)
    {
        if (packageCode == "free_preview")
        {
            return candidateCount == 1;
        }
        if (candidateCount <= 0) return false;

        var entitlement = await GetActiveEntitlementAsync(userId, packageCode, cancellationToken);
        if (entitlement == null || entitlement.RemainingPackageUses <= 0 || entitlement.RemainingCandidates < candidateCount)
        {
            return false;
        }

        entitlement.RemainingCandidates -= candidateCount;
        if (entitlement.RemainingCandidates == 0)
        {
            entitlement.RemainingPackageUses = Math.Max(0, entitlement.RemainingPackageUses - 1);
        }
        return await SaveConsumptionAsync(entitlement, cancellationToken);
    }

    public async Task<bool> ConsumeRefinementAsync(string userId, string? packageCode = null, CancellationToken cancellationToken = default)
    {
        var query = QueryActiveEntitlements(userId).Where(e => e.RemainingRefinements > 0);
        if (!string.IsNullOrWhiteSpace(packageCode))
        {
            query = query.Where(e => e.OutcomePackageDefinition.Code == packageCode);
        }

        var entitlement = await query
            .OrderBy(e => e.ExpiresAt ?? DateTime.MaxValue)
            .ThenBy(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (entitlement == null) return false;

        entitlement.RemainingRefinements--;
        return await SaveConsumptionAsync(entitlement, cancellationToken);
    }

    public async Task<bool> ConsumePremiumAugmentationAsync(string userId, CancellationToken cancellationToken = default)
    {
        var entitlement = await QueryActiveEntitlements(userId)
            .Where(e => e.RemainingPremiumAugmentations > 0)
            .OrderBy(e => e.ExpiresAt ?? DateTime.MaxValue)
            .ThenBy(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (entitlement == null) return false;

        entitlement.RemainingPremiumAugmentations--;
        return await SaveConsumptionAsync(entitlement, cancellationToken);
    }

    public async Task<bool> ConsumeExportKitAsync(string userId, CancellationToken cancellationToken = default)
    {
        var entitlement = await QueryActiveEntitlements(userId)
            .Where(e => e.PlatformExportKitAvailable)
            .OrderBy(e => e.ExpiresAt ?? DateTime.MaxValue)
            .ThenBy(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (entitlement == null) return false;

        entitlement.PlatformExportKitAvailable = false;
        return await SaveConsumptionAsync(entitlement, cancellationToken);
    }

    private async Task<bool> SaveConsumptionAsync(UserPackageEntitlement entitlement, CancellationToken cancellationToken)
    {
        MarkConsumedIfEmpty(entitlement);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another operation consumed or revoked this allowance. Do not let a
            // caller's later save flush our rejected tracked decrement.
            _context.Entry(entitlement).State = EntityState.Detached;
            return false;
        }
    }

    private IQueryable<UserPackageEntitlement> QueryActiveEntitlements(string userId)
    {
        var now = DateTime.UtcNow;
        return _context.UserPackageEntitlements
            .Include(e => e.OutcomePackageDefinition)
            .Where(e => e.UserId == userId &&
                        e.Status == PackageEntitlementStatus.Active &&
                        (e.ExpiresAt == null || e.ExpiresAt > now));
    }

    private static void MarkConsumedIfEmpty(UserPackageEntitlement entitlement)
    {
        entitlement.UpdatedAt = DateTime.UtcNow;
        if (entitlement.RemainingPackageUses <= 0 &&
            entitlement.RemainingCandidates <= 0 &&
            entitlement.RemainingRefinements <= 0 &&
            entitlement.RemainingPremiumAugmentations <= 0 &&
            !entitlement.PlatformExportKitAvailable)
        {
            entitlement.Status = PackageEntitlementStatus.Consumed;
            entitlement.ConsumedAt = DateTime.UtcNow;
        }
    }

    private static OutcomePackageDefinitionDto ToDto(OutcomePackageDefinition package)
    {
        return new OutcomePackageDefinitionDto
        {
            Id = package.Id,
            Code = package.Code,
            Name = package.Name,
            Description = package.Description,
            Price = package.Price,
            Currency = package.Currency,
            InternalCreditPackageId = package.InternalCreditPackageId,
            IncludedCandidateCount = package.IncludedCandidateCount,
            IncludedRefinementCount = package.IncludedRefinementCount,
            IncludedPremiumAugmentationCount = package.IncludedPremiumAugmentationCount,
            IncludesPlatformExportKit = package.IncludesPlatformExportKit,
            IncludesScoreDelta = package.IncludesScoreDelta,
            DisplayOrder = package.DisplayOrder,
            Highlights = BuildHighlights(package)
        };
    }

    private static UserPackageEntitlementDto ToDto(UserPackageEntitlement entitlement)
    {
        return new UserPackageEntitlementDto
        {
            Id = entitlement.Id,
            PackageCode = entitlement.OutcomePackageDefinition.Code,
            PackageName = entitlement.OutcomePackageDefinition.Name,
            Status = entitlement.ExpiresAt.HasValue && entitlement.ExpiresAt <= DateTime.UtcNow
                ? "expired"
                : entitlement.Status.ToString().ToLowerInvariant(),
            RemainingPackageUses = entitlement.RemainingPackageUses,
            RemainingCandidates = entitlement.RemainingCandidates,
            RemainingRefinements = entitlement.RemainingRefinements,
            RemainingPremiumAugmentations = entitlement.RemainingPremiumAugmentations,
            PlatformExportKitAvailable = entitlement.PlatformExportKitAvailable,
            ActivatedAt = entitlement.ActivatedAt,
            ExpiresAt = entitlement.ExpiresAt
        };
    }

    private static string[] BuildHighlights(OutcomePackageDefinition package)
    {
        var highlights = new List<string>();

        if (package.IncludedCandidateCount > 0)
        {
            highlights.Add($"{package.IncludedCandidateCount} generated candidate{(package.IncludedCandidateCount == 1 ? string.Empty : "s")}");
        }

        if (package.IncludedRefinementCount > 0)
        {
            highlights.Add($"{package.IncludedRefinementCount} guided refinement{(package.IncludedRefinementCount == 1 ? string.Empty : "s")}");
        }

        if (package.IncludedPremiumAugmentationCount > 0)
        {
            highlights.Add($"{package.IncludedPremiumAugmentationCount} premium augmentation{(package.IncludedPremiumAugmentationCount == 1 ? string.Empty : "s")}");
        }

        if (package.IncludesPlatformExportKit)
        {
            highlights.Add("Platform export kit");
        }

        if (package.IncludesScoreDelta)
        {
            highlights.Add("Before/after score delta");
        }

        if (highlights.Count == 0)
        {
            highlights.Add("Profile photo score and friendly preview");
        }

        return highlights.ToArray();
    }
}
