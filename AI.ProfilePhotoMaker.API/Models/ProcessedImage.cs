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
}