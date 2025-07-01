namespace AI.ProfilePhotoMaker.API.Models.Replicate;

/// <summary>
/// Response from Replicate API models list endpoint
/// </summary>
public class ReplicateModelsResponse
{
    public string? Next { get; set; }
    public string? Previous { get; set; }
    public List<ReplicateModelApiResult> Results { get; set; } = new List<ReplicateModelApiResult>();
}

/// <summary>
/// Individual model result from Replicate API
/// </summary>
public class ReplicateModelApiResult
{
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ReplicateModelVersion? LatestVersion { get; set; }
    public string Visibility { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public int RunCount { get; set; }
}

/// <summary>
/// Model version information from Replicate API
/// </summary>
public class ReplicateModelVersion
{
    public string Id { get; set; } = string.Empty;
    public string? CreatedAt { get; set; }
    public string? Status { get; set; }
}