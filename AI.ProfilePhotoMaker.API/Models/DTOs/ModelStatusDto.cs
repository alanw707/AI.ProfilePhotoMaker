using System.Text.Json.Serialization;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnifiedModelStatusCode
{
    NotStarted,
    ReadyForTraining,
    Training,
    ModelReady,
    Failed
}

public class ModelStatusResponse
{
    public UnifiedModelStatusCode StatusCode { get; set; }
    public bool HasTrainedModel { get; set; }
    public string? TrainedModelId { get; set; }
    public string? TrainedModelVersion { get; set; }
    public int TotalUploadedImages { get; set; }
    public bool CanStartTraining { get; set; }
    public string? Reason { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public object? CurrentRequest { get; set; }
}
