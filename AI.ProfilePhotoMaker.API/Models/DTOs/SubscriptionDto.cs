using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class SubscriptionPlanDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string BillingPeriod { get; set; } = string.Empty;
    
    // Features
    public int ImagesPerMonth { get; set; }
    public bool CanTrainCustomModels { get; set; }
    public bool CanBatchGenerate { get; set; }
    public bool HighResolutionOutput { get; set; }
    public int MaxTrainingImages { get; set; }
    public int MaxStylesAccess { get; set; }
    
    public bool IsActive { get; set; }
    public bool IsRecommended { get; set; } = false;
}

public class UserSubscriptionDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public SubscriptionPlanDto Plan { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    public SubscriptionStatus Status { get; set; }
    public DateTime? NextBillingDate { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
}

public class CreateSubscriptionRequestDto
{
    [Required]
    public string PlanId { get; set; } = string.Empty;
    
    [Required]
    public string PaymentMethodId { get; set; } = string.Empty; // Stripe Payment Method ID
    
    public string? CouponCode { get; set; }
}

public class UpdateSubscriptionRequestDto
{
    [Required]
    public string NewPlanId { get; set; } = string.Empty;
    
    public bool ProrationBehavior { get; set; } = true;
}

public class CancelSubscriptionRequestDto
{
    public bool CancelAtPeriodEnd { get; set; } = true;
    public string? Reason { get; set; }
}

public class SubscriptionUsageDto
{
    public int ImagesGeneratedThisMonth { get; set; }
    public int ImagesRemainingThisMonth { get; set; }
    public int TotalImagesAllowed { get; set; }
    public DateTime NextResetDate { get; set; }
    public bool CanTrainModels { get; set; }
    public bool CanBatchGenerate { get; set; }
    public bool HasHighResolution { get; set; }
}