namespace AI.ProfilePhotoMaker.API.Models.Replicate;

/// <summary>
/// Information about a discovered model from Replicate API
/// </summary>
public class ReplicateModelInfo
{
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? LatestVersion { get; set; }
    public string Visibility { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public int RunCount { get; set; }
}