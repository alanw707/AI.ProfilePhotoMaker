using System.Text.Json.Serialization;

namespace AI.ProfilePhotoMaker.API.Models.Replicate;

/// <summary>
/// Represents a result from a Replicate model training operation
/// </summary>
public class ReplicateTrainingResult
{
    /// <summary>
    /// The unique identifier for the training
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// The status of the training (e.g., starting, processing, succeeded, failed)
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// The ID of the trained model version (available when training is complete)
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// Input parameters provided to the training
    /// </summary>
    [JsonPropertyName("input")]
    public Dictionary<string, object>? Input { get; set; }

    /// <summary>
    /// The output of the training (available when training is complete)
    /// </summary>
    [JsonPropertyName("output")]
    public Dictionary<string, object>? Output { get; set; }

    /// <summary>
    /// Any error messages if the training failed
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Webhook URL for notifications about training status changes
    /// </summary>
    [JsonPropertyName("webhook")]
    public string? Webhook { get; set; }

    /// <summary>
    /// URL for the trained model
    /// </summary>
    [JsonPropertyName("urls")]
    public ReplicateUrls? Urls { get; set; }

    /// <summary>
    /// When the training was created
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the training was started
    /// </summary>
    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the training completed
    /// </summary>
    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Checks if the training has completed successfully
    /// </summary>
    [JsonIgnore]
    public bool IsCompleted => Status?.ToLower() == "succeeded";

    /// <summary>
    /// Checks if the training is still in progress
    /// </summary>
    [JsonIgnore]
    public bool IsInProgress => Status?.ToLower() == "processing" || Status?.ToLower() == "starting";

    /// <summary>
    /// Checks if the training has failed
    /// </summary>
    [JsonIgnore]
    public bool HasFailed => Status?.ToLower() == "failed" || Status?.ToLower() == "canceled";
}

/// <summary>
/// URLs related to a Replicate training or prediction
/// </summary>
public class ReplicateUrls
{
    /// <summary>
    /// URL to retrieve the training or prediction
    /// </summary>
    [JsonPropertyName("get")]
    public string? Get { get; set; }

    /// <summary>
    /// URL to cancel the training or prediction
    /// </summary>
    [JsonPropertyName("cancel")]
    public string? Cancel { get; set; }
}