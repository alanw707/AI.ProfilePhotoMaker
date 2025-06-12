namespace AI.ProfilePhotoMaker.API.Models.DTOs;

using AI.ProfilePhotoMaker.API.Models;

public class GenerateImagesRequestDto
{
    public string TrainedModelVersion { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public UserInfo? UserInfo { get; set; }
}
