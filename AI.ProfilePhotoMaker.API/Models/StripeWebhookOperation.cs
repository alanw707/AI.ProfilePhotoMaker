using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

public class StripeWebhookOperation
{
    public int Id { get; set; }

    [Required]
    [MaxLength(320)]
    public string OperationKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string StripeEventId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? PaymentIntentId { get; set; }

    public StripeWebhookOperationStatus Status { get; set; } = StripeWebhookOperationStatus.Processing;
    public int AttemptCount { get; set; } = 1;
    public DateTime LeaseExpiresAt { get; set; }

    [MaxLength(128)]
    public string? FailureCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public enum StripeWebhookOperationStatus
{
    Processing = 0,
    Succeeded = 1,
    Failed = 2
}
