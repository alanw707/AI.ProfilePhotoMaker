using System;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class ProcessedImageDto
{
    public int Id { get; set; }
    public string OriginalImageUrl { get; set; } = string.Empty;
    public string ProcessedImageUrl { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsGenerated { get; set; }
    public bool IsOriginalUpload { get; set; }
}


