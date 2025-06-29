using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

public class Subscription
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    [Required]
    public string PlanId { get; set; } = string.Empty;
    public SubscriptionPlan Plan { get; set; } = null!;
    
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; } // Null for active subscriptions
    public bool IsActive { get; set; } = true;
    
    [Required]
    [StringLength(50)]
    public string PaymentProvider { get; set; } = "stripe";
    
    [Required]
    public string ExternalSubscriptionId { get; set; } = string.Empty; // Stripe Subscription ID
    
    public string? ExternalCustomerId { get; set; } // Stripe Customer ID
    
    // Payment status tracking
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTime? LastPaymentDate { get; set; }
    public DateTime? NextBillingDate { get; set; }
    
    // Cancellation handling
    public DateTime? CancelledAt { get; set; }
    public DateTime? CancelAtPeriodEnd { get; set; }
    public string? CancellationReason { get; set; }
    
    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum SubscriptionStatus
{
    Active = 0,
    PastDue = 1,
    Cancelled = 2,
    Incomplete = 3,
    IncompleteExpired = 4,
    Trialing = 5,
    Unpaid = 6,
    PendingPayment = 7
}
