using System;
using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models;

public class FeedbackSubmission
{
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [StringLength(50)]
    public string Category { get; set; } = "General";

    [Required]
    [StringLength(2000)]
    public string Message { get; set; } = string.Empty;

    [StringLength(2048)]
    public string? PageUrl { get; set; }

    [StringLength(512)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

