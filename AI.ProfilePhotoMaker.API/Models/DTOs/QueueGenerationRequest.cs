using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

public class QueueGenerationRequest
{
    public string TrainingId { get; set; } = string.Empty;
    public List<string> Styles { get; set; } = new();
    [Range(1, 4, ErrorMessage = "numOutputsPerStyle must be between 1 and 4")]
    public int NumOutputsPerStyle { get; set; } = 2;
}
