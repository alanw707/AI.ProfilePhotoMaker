using AI.ProfilePhotoMaker.API.Models.Replicate;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// Model training operations: initiate training, check status, create with destination
/// </summary>
public interface IReplicateTrainingService
{
    Task<ReplicateTrainingResult> CreateModelTrainingAsync(string userId, string imageZipUrl, string? modelRequestId = null);
    Task<ReplicateTrainingResult> CreateModelTrainingWithDestinationAsync(string userId, string imageZipUrl, string destination);
    Task<string> InitiateModelCreationAndTrainingAsync(string userId, string imageZipUrl);
    Task<ReplicateTrainingResult> GetTrainingStatusAsync(string trainingId);
}
