using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class EnhancePhotoRequestDto
{
    [Required(ErrorMessage = "Image URL is required for photo enhancement")]
    [Url(ErrorMessage = "Image URL must be a valid URL")]
    public string ImageUrl { get; set; } = string.Empty;
    
    public string? EnhancementType { get; set; } = "professional";
}