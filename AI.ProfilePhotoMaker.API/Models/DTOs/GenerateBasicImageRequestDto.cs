using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class GenerateBasicImageRequestDto
{
    [Required(ErrorMessage = "Gender is required for basic image generation")]
    public string Gender { get; set; } = string.Empty;
    
    public UserInfo? UserInfo { get; set; }
}