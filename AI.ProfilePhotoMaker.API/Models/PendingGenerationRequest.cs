using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI.ProfilePhotoMaker.API.Models;

public enum PendingGenerationStatus
{
    Pending,
    Started,
    Succeeded,
    Failed
}

public class PendingGenerationRequest
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Links to the training request that will produce the model (PendingTrainingRequestId)
    /// </summary>
    [Required]
    public string TrainingRequestId { get; set; } = string.Empty;

    /// <summary>
    /// JSON serialized list of styles to generate
    /// </summary>
    [Required]
    public string StylesJson { get; set; } = "[]";

    public int NumOutputsPerStyle { get; set; } = 2;

    public PendingGenerationStatus Status { get; set; } = PendingGenerationStatus.Pending;

    public string? ErrorMessage { get; set; }

    public string? LastPredictionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
