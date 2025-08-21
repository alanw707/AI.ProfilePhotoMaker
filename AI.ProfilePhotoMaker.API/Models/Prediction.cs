using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

public class Prediction
{
    [Key]
    public string Id { get; set; } = string.Empty; // Replicate prediction ID

    [Required]
    public string UserId { get; set; } = string.Empty;

    public string? Style { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

