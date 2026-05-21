namespace AI.ProfilePhotoMaker.API.Models;

public class UserPackageEntitlement
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public int OutcomePackageDefinitionId { get; set; }
    public OutcomePackageDefinition OutcomePackageDefinition { get; set; } = null!;
    public int? SourcePaymentTransactionId { get; set; }
    public PaymentTransaction? SourcePaymentTransaction { get; set; }
    public PackageEntitlementStatus Status { get; set; } = PackageEntitlementStatus.Active;
    public int RemainingPackageUses { get; set; } = 1;
    public int RemainingCandidates { get; set; }
    public int RemainingRefinements { get; set; }
    public int RemainingPremiumAugmentations { get; set; }
    public bool PlatformExportKitAvailable { get; set; }
    public DateTime? ActivatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConsumedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum PackageEntitlementStatus
{
    Active = 0,
    Consumed = 1,
    Expired = 2,
    Refunded = 3
}
