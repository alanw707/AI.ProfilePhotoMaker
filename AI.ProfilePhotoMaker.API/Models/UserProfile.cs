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
    public List<ProcessedImage> ProcessedImages { get; set; } = new List<ProcessedImage>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// Models/ProcessedImage.cs