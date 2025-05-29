namespace AI.ProfilePhotoMaker.API.Models;

public class UserProfile
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public ApplicationUser User { get; set; }
    public List<ProcessedImage> ProcessedImages { get; set; } = new List<ProcessedImage>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// Models/ProcessedImage.cs