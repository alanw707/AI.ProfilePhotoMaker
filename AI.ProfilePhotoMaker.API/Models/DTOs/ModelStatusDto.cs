using System.Text.Json.Serialization;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UnifiedModelStatusCode
{
    NotStarted,
    ReadyForTraining,
    Training,
    Generating,
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
    public GenerationStatusDto? GenerationStatus { get; set; }
    public CreditImpactDto? CreditImpact { get; set; }
}

public class GenerationStatusDto
{
    public string? PredictionId { get; set; }
    public string Status { get; set; } = "unknown"; // queued | processing | succeeded | failed | canceled | unknown
    public string? Style { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? OutputCount { get; set; }
    public string? Error { get; set; }
}

public class CreditImpactDto
{
    public int ChargedCredits { get; set; }
    public int RefundedCredits { get; set; }
    public int NetCredits { get; set; }
}
