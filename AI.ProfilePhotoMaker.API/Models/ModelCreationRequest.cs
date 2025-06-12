using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

/// <summary>
/// Tracks model creation requests and their status
/// </summary>
public class ModelCreationRequest
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string UserId { get; set; } = string.Empty;
    
    public string ModelName { get; set; } = string.Empty;
    
    public string? ReplicateModelId { get; set; }
    
    /// <summary>
    /// The trained model version ID (the part after colon in alanw707/model:version)
    /// </summary>
    public string? TrainedModelVersion { get; set; }
    
    public ModelCreationStatus Status { get; set; } = ModelCreationStatus.Pending;
    
    public string? TrainingImageZipUrl { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedAt { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Training request ID that's waiting for this model to be ready
    /// </summary>
    public string? PendingTrainingRequestId { get; set; }
}

/// <summary>
/// Status of model creation
/// </summary>
public enum ModelCreationStatus
{
    Pending,
    Creating,
    Ready,
    Failed
}