namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class QueueGenerationRequest
{
    public string TrainingId { get; set; } = string.Empty;
    public List<string> Styles { get; set; } = new();
    public int NumOutputsPerStyle { get; set; } = 2;
}
