using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Models.Replicate;

namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// Interface for interacting with the Replicate.com API
/// </summary>
public interface IReplicateApiClient
{
    /// <summary>
    /// Creates a new model in Replicate
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="modelName">The model name</param>
    /// <param name="description">Optional model description</param>
    /// <returns>The created model's full name (owner/model-name)</returns>
    Task<string> CreateModelAsync(string userId, string modelName, string? description = null);

    /// <summary>
    /// Creates a new training for a user's custom model
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="imageZipUrl">URL to the zipped training images</param>
    /// <returns>The training ID and status</returns>
    Task<ReplicateTrainingResult> CreateModelTrainingAsync(string userId, string imageZipUrl);

    /// <summary>
    /// Creates a new training using an existing model destination (for polling-based flow)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="imageZipUrl">URL to the zipped training images</param>
    /// <param name="destination">The model destination (owner/model-name)</param>
    /// <returns>The training ID and status</returns>
    Task<ReplicateTrainingResult> CreateModelTrainingWithDestinationAsync(string userId, string imageZipUrl, string destination);

    /// <summary>
    /// Initiates model creation and training workflow (polling-based)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="imageZipUrl">URL to the zipped training images</param>
    /// <returns>The model creation request ID</returns>
    Task<string> InitiateModelCreationAndTrainingAsync(string userId, string imageZipUrl);

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
    /// <param name="numOutputs">Number of images to generate (1-4)</param>
    /// <returns>The prediction ID and status</returns>
    Task<ReplicatePredictionResult> GenerateImagesAsync(
        string trainedModelVersion,
        string userId,
        string style,
        UserInfo? userInfo = null,
        int numOutputs = 2);

    /// <summary>
    /// Gets the status of an image generation prediction
    /// </summary>
    /// <param name="predictionId">The prediction ID</param>
    /// <returns>The current prediction status</returns>
    Task<ReplicatePredictionResult> GetPredictionStatusAsync(string predictionId);

    /// <summary>
    /// Generates images using a DTO with all parameters
    /// </summary>
    /// <param name="request">The generate images request</param>
    /// <returns>The prediction result URL</returns>
    Task<string> GenerateImagesAsync(GenerateImagesRequestDto request);    

    /// <summary>
    /// Enhances a user's uploaded photo using Flux Kontext Pro for text-based image editing
    /// Provides professional photo enhancement for basic tier users
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="imageUrl">URL to the user's uploaded photo</param>
    /// <param name="enhancementType">Type of enhancement (professional, portrait, linkedin)</param>
    /// <returns>The prediction result with enhanced image</returns>
    Task<ReplicatePredictionResult> EnhancePhotoAsync(string userId, string imageUrl, string enhancementType = "professional");

    /// <summary>
    /// Checks if a model exists and is accessible on Replicate
    /// </summary>
    /// <param name="modelId">The model ID (owner/model-name)</param>
    /// <returns>True if model exists and is accessible, false otherwise</returns>
    Task<bool> CheckModelExistsAsync(string modelId);

    /// <summary>
    /// Deletes a model from Replicate
    /// </summary>
    /// <param name="modelId">The model ID (owner/model-name)</param>
    /// <returns>True if deletion was successful, false otherwise</returns>
    Task<bool> DeleteModelAsync(string modelId);

    /// <summary>
    /// Creates a prediction using a specific model and input parameters
    /// </summary>
    /// <param name="modelId">The model ID to use for prediction</param>
    /// <param name="input">Input parameters for the model</param>
    /// <returns>The prediction result</returns>
    Task<ReplicatePredictionResult> CreatePredictionAsync(string modelId, Dictionary<string, object> input);

    /// <summary>
    /// Finds existing trained models for a user by scanning Replicate API
    /// </summary>
    /// <param name="userId">The user ID to search for</param>
    /// <returns>List of discovered user models</returns>
    Task<List<ReplicateModelInfo>> FindUserModelsByPatternAsync(string userId);

    /// <summary>
    /// Gets the latest version ID for a specific model from Replicate API
    /// </summary>
    /// <param name="modelId">The model ID in format owner/model-name</param>
    /// <returns>The latest version ID hash or null if not found</returns>
    Task<string?> GetModelVersionAsync(string modelId);

    /// <summary>
    /// Checks if a model exists and is available on Replicate
    /// </summary>
    /// <param name="modelId">The model ID in format owner/model-name</param>
    /// <returns>True if model exists and is available</returns>
    Task<bool> CheckModelAvailabilityAsync(string modelId);

}