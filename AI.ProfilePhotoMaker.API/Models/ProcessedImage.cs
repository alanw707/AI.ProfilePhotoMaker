namespace AI.ProfilePhotoMaker.API.Models;

public class ProcessedImage
{
    public int Id { get; set; }
    public string OriginalImageUrl { get; set; }
    public string ProcessedImageUrl { get; set; }
    public string Style { get; set; }
    public int UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // New fields to distinguish image types
    public bool IsGenerated { get; set; } = false; // True for AI-generated images, false for uploaded
    public bool IsOriginalUpload { get; set; } = false; // True for user's original uploads
    
    // Retention policy fields
    public DateTime ScheduledDeletionDate { get; set; }
    public bool IsMarkedForDeletion { get; set; } = false;
    public DateTime? UserRequestedDeletionDate { get; set; } // For user-initiated deletions
    public bool IsDeleted { get; set; } = false; // Soft delete flag
    public DateTime? DeletedAt { get; set; } // When the soft delete occurred
    
    /// <summary>
    /// Calculates the scheduled deletion date based on image type:
    /// - Original uploads (input photos): 7 days from creation
    /// - AI generated headshots: 30 days from creation
    /// </summary>
    public void SetScheduledDeletionDate()
    {
        if (IsOriginalUpload)
        {
            // Input photos (original uploads): Delete after 7 days
            ScheduledDeletionDate = CreatedAt.AddDays(7);
        }
        else if (IsGenerated)
        {
            // AI headshots (generated photos): Delete after 30 days
            ScheduledDeletionDate = CreatedAt.AddDays(30);
        }
        else
        {
            // Default retention for other types: 30 days
            ScheduledDeletionDate = CreatedAt.AddDays(30);
        }
    }
}