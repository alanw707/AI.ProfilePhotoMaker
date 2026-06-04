namespace AI.ProfilePhotoMaker.API.Models;

public class OutcomePackageDefinition
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string? StripePriceId { get; set; }
    public int? InternalCreditPackageId { get; set; }
    public CreditPackage? InternalCreditPackage { get; set; }
    public int IncludedCandidateCount { get; set; }
    public int IncludedRefinementCount { get; set; }
    public int IncludedPremiumAugmentationCount { get; set; }
    public bool IncludesPlatformExportKit { get; set; }
    public bool IncludesScoreDelta { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<UserPackageEntitlement> Entitlements { get; set; } = new List<UserPackageEntitlement>();
}
