namespace AI.ProfilePhotoMaker.API.Models.DTOs;

/// <summary>
/// Request DTO for manually setting a trained model ID
/// </summary>
public class SetTrainedModelRequest
{
    /// <summary>
    /// The trained model ID (e.g., "alanw707/user-{userId}-{timestamp}")
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Optional model version ID for generation calls
    /// </summary>
    public string? VersionId { get; set; }

    /// <summary>
    /// Whether to verify the model exists on Replicate before setting
    /// </summary>
    public bool VerifyExists { get; set; } = true;
}