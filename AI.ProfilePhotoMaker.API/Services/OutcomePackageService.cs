using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

public class OutcomePackageService : IOutcomePackageService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OutcomePackageService> _logger;

    public OutcomePackageService(ApplicationDbContext context, ILogger<OutcomePackageService> logger)
    {
        _context = context;
        _logger = logger;
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

    public async Task<UserPackageEntitlement?> GrantEntitlementForCreditPackageAsync(string userId, int creditPackageId, string? paymentTransactionId, CancellationToken cancellationToken = default)
    {
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
                .FirstOrDefaultAsync(e => e.SourcePaymentTransactionId == parsedTransactionId, cancellationToken);
            if (existing != null)
            {
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

        return entitlement;
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

        var entitlement = await GetActiveEntitlementAsync(userId, packageCode, cancellationToken);
        if (entitlement == null || entitlement.RemainingPackageUses <= 0 || entitlement.RemainingCandidates < candidateCount)
        {
            return false;
        }

        entitlement.RemainingCandidates -= candidateCount;
        entitlement.RemainingPackageUses = Math.Max(0, entitlement.RemainingPackageUses - 1);
        MarkConsumedIfEmpty(entitlement);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
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
        MarkConsumedIfEmpty(entitlement);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
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
        MarkConsumedIfEmpty(entitlement);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
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
        MarkConsumedIfEmpty(entitlement);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
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
            Status = entitlement.Status.ToString().ToLowerInvariant(),
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
