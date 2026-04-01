using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Models.Replicate;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// Image generation and prediction status operations
/// </summary>
public interface IReplicatePredictionService
{
    Task<ReplicatePredictionResult> GenerateImagesAsync(string trainedModelVersion, string userId, string style, UserInfo? userInfo = null, int numOutputs = 2);
    Task<ReplicatePredictionResult> GenerateBaseStylePreviewAsync(string style, UserInfo? userInfo = null, int numOutputs = 1);
    Task<ReplicatePredictionResult> GetPredictionStatusAsync(string predictionId);
    Task<string> GenerateImagesAsync(GenerateImagesRequestDto request);
    Task<ReplicatePredictionResult> CreatePredictionAsync(string modelId, Dictionary<string, object> input);
}
