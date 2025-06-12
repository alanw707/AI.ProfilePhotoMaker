namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class TrainModelRequestDto
{
    public string UserId { get; set; } = string.Empty;
    public string ImageZipUrl { get; set; } = string.Empty;
}
