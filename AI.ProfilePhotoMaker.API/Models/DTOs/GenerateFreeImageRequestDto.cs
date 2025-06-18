using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class GenerateFreeImageRequestDto
{
    [Required(ErrorMessage = "Gender is required for free image generation")]
    public string Gender { get; set; } = string.Empty;
    
    public UserInfo? UserInfo { get; set; }
}