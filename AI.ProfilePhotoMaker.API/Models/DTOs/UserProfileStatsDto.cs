using System;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class UserProfileStatsDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public string? Ethnicity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Credits { get; set; }
    public int TotalProcessedImages { get; set; }
    public int OriginalUploads { get; set; }
    public int GeneratedImages { get; set; }
    public DateTime? LastImageUpload { get; set; }
    public DateTime? LastImageGeneration { get; set; }
}


