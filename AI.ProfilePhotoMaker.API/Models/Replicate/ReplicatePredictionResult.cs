using System.Text.Json.Serialization;

namespace AI.ProfilePhotoMaker.API.Models.Replicate;

/// <summary>
/// Represents a result from a Replicate prediction (image generation) operation
/// </summary>
public class ReplicatePredictionResult
{
    /// <summary>
    /// The unique identifier for the prediction
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// The version of the model used for prediction
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// The status of the prediction (e.g., starting, processing, succeeded, failed)
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Input parameters provided to the prediction
    /// </summary>
    [JsonPropertyName("input")]
    public Dictionary<string, object>? Input { get; set; }

    /// <summary>
    /// The output of the prediction (typically image URLs)
    /// </summary>
    [JsonPropertyName("output")]
    public List<string>? Output { get; set; }

    /// <summary>
    /// Any error messages if the prediction failed
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Webhook URL for notifications about prediction status changes
    /// </summary>
    [JsonPropertyName("webhook")]
    public string? Webhook { get; set; }

    /// <summary>
    /// The URLs associated with this prediction
    /// </summary>
    [JsonPropertyName("urls")]
    public ReplicateUrls? Urls { get; set; }

    /// <summary>
    /// When the prediction was created
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the prediction was started
    /// </summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the prediction completed
    /// </summary>
    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Returns the generated image URLs
    /// </summary>
    [JsonIgnore]
    public IEnumerable<string> GeneratedImageUrls => Output ?? Enumerable.Empty<string>();

    /// <summary>
    /// Checks if the prediction has completed successfully
    /// </summary>
    [JsonIgnore]
    public bool IsCompleted => Status?.ToLower() == "succeeded";

    /// <summary>
    /// Checks if the prediction is still in progress
    /// </summary>
    [JsonIgnore]
    public bool IsInProgress => Status?.ToLower() == "processing" || Status?.ToLower() == "starting";

    /// <summary>
    /// Checks if the prediction has failed
    /// </summary>
    [JsonIgnore]
    public bool HasFailed => Status?.ToLower() == "failed" || Status?.ToLower() == "canceled";
}