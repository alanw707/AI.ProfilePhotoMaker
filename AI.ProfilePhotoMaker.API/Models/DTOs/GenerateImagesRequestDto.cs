namespace AI.ProfilePhotoMaker.API.Models.DTOs;

using AI.ProfilePhotoMaker.API.Models;

public class GenerateImagesRequestDto
{
    public string TrainedModelVersion { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public UserInfo? UserInfo { get; set; }
    public int NumOutputs { get; set; } = 2; // Number of images to generate (1-4)
}

public class GenerateBatchImagesRequestDto
{
    public string TrainedModelVersion { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<string> Styles { get; set; } = new List<string>();
    public UserInfo? UserInfo { get; set; }
    public int NumOutputsPerStyle { get; set; } = 2; // Number of images to generate per style (1-4)
}
