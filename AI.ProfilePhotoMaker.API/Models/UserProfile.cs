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
    public DateTime? LastModelSyncCheck { get; set; }
    // Removed LastModelDeletedAt to avoid schema dependency


    // Basic tier and subscription management
    public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Basic;
    public int Credits { get; set; } = 25; // Unified credits balance (tops up to 5 weekly if below)
    public DateTime LastCreditReset { get; set; } = DateTime.UtcNow;

    public List<ProcessedImage> ProcessedImages { get; set; } = new List<ProcessedImage>();
    public List<UsageLog> UsageLogs { get; set; } = new List<UsageLog>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// Models/ProcessedImage.cs
