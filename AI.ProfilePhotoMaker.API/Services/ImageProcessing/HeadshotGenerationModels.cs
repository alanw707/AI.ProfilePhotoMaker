namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

public class HeadshotGenerationRequest
{
    public string UserId { get; set; } = string.Empty;
    public string ImageStoragePath { get; set; } = string.Empty;
    public string Style { get; set; } = "professional";
    public string Background { get; set; } = "auto";
    public string? PromptTemplate { get; set; }
    public string? UseCaseCode { get; set; }
    public string? RecipeCode { get; set; }
    public string? Label { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public class HeadshotGenerationResult
{
    public bool Success { get; set; }
    public string DataUrlOrUrl { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
}

public interface IHeadshotGenerationProvider
{
    string ProviderName { get; }
    string ModelName { get; }
    Task<HeadshotGenerationResult> GenerateAsync(HeadshotGenerationRequest request, CancellationToken cancellationToken = default);
}
