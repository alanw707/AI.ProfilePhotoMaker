using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

public class Prediction
{
    [Key]
    public string Id { get; set; } = string.Empty; // Replicate prediction ID

    [Required]
    public string UserId { get; set; } = string.Empty;

    public string? Style { get; set; }

    [MaxLength(100)]
    public string? RequestedStyle { get; set; }

    [MaxLength(100)]
    public string? ResolvedStyle { get; set; }

    public int? ResolvedStyleId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
