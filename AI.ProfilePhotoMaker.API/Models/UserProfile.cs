using System.ComponentModel.DataAnnotations.Schema;

namespace AI.ProfilePhotoMaker.API.Models;

public class UserProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public string? Ethnicity { get; set; }
    public string? TrainedModelId { get; set; }
    public DateTime? ModelTrainedAt { get; set; }
    
    // Style relationship
    public int? StyleId { get; set; }
    public Style? Style { get; set; }
    
    // Basic tier and subscription management
    public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Basic;
    public int Credits { get; set; } = 3; // Weekly credits (max 3, resets weekly)
    public int PurchasedCredits { get; set; } = 0; // Purchased credits (no expiration, accumulates)
    public DateTime LastCreditReset { get; set; } = DateTime.UtcNow;
    
    public List<ProcessedImage> ProcessedImages { get; set; } = new List<ProcessedImage>();
    public List<UsageLog> UsageLogs { get; set; } = new List<UsageLog>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// Models/ProcessedImage.cs