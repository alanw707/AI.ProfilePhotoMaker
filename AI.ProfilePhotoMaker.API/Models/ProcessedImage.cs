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
}