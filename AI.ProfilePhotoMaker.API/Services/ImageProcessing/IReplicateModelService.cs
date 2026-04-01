using AI.ProfilePhotoMaker.API.Models.Replicate;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// Model lifecycle operations: create, check, delete, discover models on Replicate
/// </summary>
public interface IReplicateModelService
{
    Task<string> CreateModelAsync(string userId, string modelName, string? description = null);
    Task<bool> CheckModelExistsAsync(string modelId);
    Task<bool> CheckModelAvailabilityAsync(string modelId);
    Task<(bool Success, string? ErrorMessage)> DeleteModelAsync(string modelId);
    Task<List<ReplicateModelInfo>> FindUserModelsByPatternAsync(string userId);
    Task<string?> GetModelVersionAsync(string modelId);
    Task<List<string>> GetModelVersionsAsync(string modelId);
    Task<(bool Success, string? ErrorMessage)> DeleteModelVersionAsync(string modelId, string versionId);
    Task<bool> WaitForModelVersionAvailabilityAsync(string modelId, string versionId, TimeSpan? timeout = null, TimeSpan? maxBackoff = null);
}
