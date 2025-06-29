using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

public class PaymentTransaction
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public int? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    
    [Required]
    public string ExternalTransactionId { get; set; } = string.Empty; // Stripe Payment Intent ID
    
    [Required]
    public decimal Amount { get; set; }
    
    [Required]
    [StringLength(3)]
    public string Currency { get; set; } = "USD";
    
    [Required]
    [StringLength(50)]
    public string PaymentProvider { get; set; } = "stripe";
    
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public PaymentType Type { get; set; } = PaymentType.Subscription;
    
    // Metadata
    public string? Description { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Completed = 1, // Alias for Succeeded
    Failed = 2,
    Cancelled = 3,
    Refunded = 4
}

public enum PaymentType
{
    Subscription = 0,
    OneTime = 1,
    Refund = 2
}