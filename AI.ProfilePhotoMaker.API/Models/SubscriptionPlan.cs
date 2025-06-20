using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

public class SubscriptionPlan
{
    [Key]
    public string Id { get; set; } = string.Empty; // Stripe Price ID
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public decimal Price { get; set; }
    
    [Required]
    [StringLength(20)]
    public string BillingPeriod { get; set; } = string.Empty; // "monthly" or "yearly"
    
    // Feature limits
    public int ImagesPerMonth { get; set; }
    public bool CanTrainCustomModels { get; set; }
    public bool CanBatchGenerate { get; set; }
    public bool HighResolutionOutput { get; set; }
    public int MaxTrainingImages { get; set; } = 0;
    public int MaxStylesAccess { get; set; } = 1;
    
    // Stripe integration
    public string? StripeProductId { get; set; }
    public string? StripePriceId { get; set; }
    
    // Metadata
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public List<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
