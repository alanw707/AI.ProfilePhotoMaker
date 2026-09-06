using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

public class HeadshotGenerationOperation
{
    public int Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string CorrelationId { get; set; } = string.Empty;

    public HeadshotGenerationOperationStatus Status { get; set; } = HeadshotGenerationOperationStatus.Processing;
    public DateTime LeaseExpiresAt { get; set; }

    [MaxLength(64)]
    public string? FailureCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum HeadshotGenerationOperationStatus
{
    Processing = 0,
    Succeeded = 1,
    Failed = 2
}
