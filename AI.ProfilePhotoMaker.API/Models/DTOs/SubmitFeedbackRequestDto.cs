using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class SubmitFeedbackRequestDto
{
    [Required]
    [StringLength(50)]
    public string Category { get; set; } = "General";

    [Required]
    [StringLength(2000, MinimumLength = 10)]
    public string Message { get; set; } = string.Empty;

    [StringLength(2048)]
    public string? PageUrl { get; set; }

    [StringLength(512)]
    public string? UserAgent { get; set; }
}

