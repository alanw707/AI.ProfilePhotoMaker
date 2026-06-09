using System.ComponentModel.DataAnnotations;
using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class AdminUserListDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class AdminUserDetailDto : AdminUserListDto
{
    public string? Gender { get; set; }
    public string? Ethnicity { get; set; }
    public string SubscriptionTier { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class AdminUserDiagnosticsDto
{
    public AdminUserDetailDto User { get; set; } = new();
    public AdminUserMetricsDto Metrics { get; set; } = new();
    public List<AdminCreditPurchaseHistoryDto> RecentPurchases { get; set; } = new();
    public List<AdminPackageEntitlementDto> PackageEntitlements { get; set; } = new();
    public List<AdminRecentImageDto> RecentImages { get; set; } = new();
    public List<AdminUserActivityDto> ActivityHistory { get; set; } = new();
    public List<AdminUserAdminActionDto> RecentAdminActions { get; set; } = new();
}

public class AdminUserMetricsDto
{
    public int CurrentCredits { get; set; }
    public int TotalProcessedImages { get; set; }
    public int OriginalUploads { get; set; }
    public int GeneratedImages { get; set; }
    public int PurchaseCount { get; set; }
    public int TotalCreditsPurchased { get; set; }
    public int TotalCreditsConsumed { get; set; }
    public DateTime? LastPurchaseAt { get; set; }
    public DateTime? LastImageUploadAt { get; set; }
    public DateTime? LastImageGenerationAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public bool HasUsageHistory { get; set; }
}

public class AdminCreditPurchaseHistoryDto
{
    public int Id { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public int CreditsAwarded { get; set; }
    public decimal AmountPaid { get; set; }
    public string PaymentProvider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class AdminPackageEntitlementDto
{
    public int Id { get; set; }
    public string PackageCode { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RemainingCandidates { get; set; }
    public int RemainingRefinements { get; set; }
    public int RemainingPremiumAugmentations { get; set; }
    public bool PlatformExportKitAvailable { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class AdminRecentImageDto
{
    public int Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? Style { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsGenerated { get; set; }
    public bool IsOriginalUpload { get; set; }
    public string? Provider { get; set; }
    public string? ProviderModel { get; set; }
    public string? GenerationStatus { get; set; }
    public string? FailureReason { get; set; }
}

public class AdminUserActivityDto
{
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Status { get; set; }
    public DateTime OccurredAt { get; set; }
    public int? CreditsDelta { get; set; }
    public int? CreditsRemaining { get; set; }
    public string? ImageUrl { get; set; }
}

public class AdminUserAdminActionDto
{
    public int Id { get; set; }
    public string AdminEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminCreditAdjustmentDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int Amount { get; set; }

    [Required(ErrorMessage = "Reason is required")]
    [MinLength(1, ErrorMessage = "Reason cannot be empty")]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class CouponCreateDto
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    public DiscountType DiscountType { get; set; }

    [Required]
    [Range(0.01, 100)]
    public decimal DiscountValue { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int MaxUsages { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

public class CouponUpdateDto
{
    public int? MaxUsages { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool? IsActive { get; set; }
}

public class CouponListDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public int MaxUsages { get; set; }
    public int CurrentUsages { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminAuditLogDto
{
    public int Id { get; set; }
    public string AdminEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? TargetUserEmail { get; set; }
    public string? Details { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalCreditsOutstanding { get; set; }
    public long TotalCreditsPurchased { get; set; }
    public int ActiveCoupons { get; set; }
}

public class AdminProductHealthDto
{
    public string Window { get; set; } = "7d";
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public AdminProductFunnelDto Funnel { get; set; } = new();
    public List<AdminPackageMixDto> PackageMix { get; set; } = new();
    public AdminPackageFulfillmentDto PackageFulfillment { get; set; } = new();
    public List<AdminProviderHealthDto> ProviderHealth { get; set; } = new();
    public AdminFailureQueueDto FailureQueue { get; set; } = new();
    public AdminReplicateRetirementDto ReplicateRetirement { get; set; } = new();
}

public class AdminProductFunnelDto
{
    public int Uploads { get; set; }
    public int SuccessfulPreviewGenerations { get; set; }
    public int PaidPackagePurchases { get; set; }
    public int StarterPurchases { get; set; }
    public int ProPurchases { get; set; }
    public int? ExportDownloads { get; set; }
    public bool ExportDownloadsAvailable { get; set; }
    public int? MedianGenerationTimeMs { get; set; }
    public int? P95GenerationTimeMs { get; set; }
    public bool GenerationLatencyAvailable { get; set; }
    public decimal? PreviewGenerationSuccessRate { get; set; }
    public bool PreviewGenerationSuccessRateAvailable { get; set; }
    public decimal? PreviewToPaidConversionRate { get; set; }
    public bool PreviewToPaidConversionRateAvailable { get; set; }
}

public class AdminPackageMixDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Purchases { get; set; }
    public decimal RevenueProxy { get; set; }
}

public class AdminPackageFulfillmentDto
{
    public int ActiveEntitlements { get; set; }
    public int ConsumedEntitlements { get; set; }
    public int ExpiredEntitlements { get; set; }
    public int RefundedEntitlements { get; set; }
    public int RemainingCandidates { get; set; }
    public int RemainingRefinements { get; set; }
    public int RemainingPremiumAugmentations { get; set; }
    public int ExportKitsAvailable { get; set; }
}

public class AdminProviderHealthDto
{
    public string Provider { get; set; } = "unknown";
    public string Model { get; set; } = "unknown";
    public int GeneratedImages { get; set; }
    public int FailedImages { get; set; }
    public decimal FailureRate { get; set; }
}

public class AdminFailureQueueDto
{
    public int FailedGenerations { get; set; }
    public List<AdminFailureReasonDto> TopReasons { get; set; } = new();
}

public class AdminFailureReasonDto
{
    public string Reason { get; set; } = "unknown";
    public int Count { get; set; }
}

public class AdminReplicateRetirementDto
{
    public int OpenAiGeneratedImages { get; set; }
    public int ReplicateGeneratedImages { get; set; }
    public decimal ReplicateUsageShare { get; set; }
    public bool LatencySignalAvailable { get; set; }
    public int FallbackGeneratedImages { get; set; }
    public bool FallbackUseSignalAvailable { get; set; }
    public int AdvancedPhotoshootRequests { get; set; }
    public bool AdvancedPhotoshootUsageSignalAvailable { get; set; }
    public bool QualityComplaintSignalAvailable { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

public class AdminActionReasonDto
{
    [Required(ErrorMessage = "Reason is required")]
    [MinLength(1, ErrorMessage = "Reason cannot be empty")]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class ValidateCouponDto
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal OriginalPrice { get; set; }
}
