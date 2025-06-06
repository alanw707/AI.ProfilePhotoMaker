using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.Replicate;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// Interface for interacting with the Replicate.com API
/// </summary>
public interface IReplicateApiClient
{
    /// <summary>
    /// Creates a new training for a user's custom model
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="imageZipUrl">URL to the zipped training images</param>
    /// <returns>The training ID and status</returns>
    Task<ReplicateTrainingResult> CreateModelTrainingAsync(string userId, string imageZipUrl);

    /// <summary>
    /// Gets the status of a model training
    /// </summary>
    /// <param name="trainingId">The training ID</param>
    /// <returns>The current training status</returns>
    Task<ReplicateTrainingResult> GetTrainingStatusAsync(string trainingId);

    /// <summary>
    /// Generates images using the trained model and a specific style
    /// </summary>
    /// <param name="trainedModelVersion">The trained model version</param>
    /// <param name="userId">The user ID</param>
    /// <param name="style">The style to use for generation</param>
    /// <param name="userInfo">Optional user info for style generation</param>
    /// <returns>The prediction ID and status</returns>
    Task<ReplicatePredictionResult> GenerateImagesAsync(
        string trainedModelVersion, 
        string userId, 
        string style,
        UserInfo? userInfo = null);

    /// <summary>
    /// Gets the status of an image generation prediction
    /// </summary>
    /// <param name="predictionId">The prediction ID</param>
    /// <returns>The current prediction status</returns>
    Task<ReplicatePredictionResult> GetPredictionStatusAsync(string predictionId);
}