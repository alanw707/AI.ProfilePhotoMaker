using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

/// <summary>
/// Implementation of training polling service with automatic image generation
/// </summary>
public class TrainingPollingService : ITrainingPollingService
{
    private readonly ILogger<TrainingPollingService> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public TrainingPollingService(
        ILogger<TrainingPollingService> logger,
        ApplicationDbContext dbContext,
        IReplicateApiClient replicateApiClient,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _dbContext = dbContext;
        _replicateApiClient = replicateApiClient;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <summary>
    /// Start polling for a specific training completion
    /// </summary>
    public async Task StartPollingForTraining(string trainingId, string userId)
    {
        _logger.LogInformation("Starting polling for training {TrainingId} for user {UserId}", trainingId, userId);

        // Store the polling task info - we'll let the background service handle the actual polling
        // This method primarily exists for extensibility if we need immediate polling triggers
        await Task.CompletedTask;
    }

    /// <summary>
    /// Check if a training has completed
    /// </summary>
    public async Task<bool> IsTrainingComplete(string trainingId)
    {
        try
        {
            var status = await _replicateApiClient.GetTrainingStatusAsync(trainingId);
            return status.IsCompleted || status.Status?.ToLower() == "failed" || status.Status?.ToLower() == "canceled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking training status for {TrainingId}", trainingId);
            return false;
        }
    }

    /// <summary>
    /// Process a completed training - update database and generate initial images
    /// This replicates the logic from the old training webhook
    /// </summary>
    public async Task ProcessTrainingCompletion(string trainingId)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var scopedDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scopedReplicateClient = scope.ServiceProvider.GetRequiredService<IReplicateApiClient>();

        try
        {
            _logger.LogInformation("Processing training completion for training {TrainingId}", trainingId);

            // Get the current training status from Replicate
            var trainingStatus = await scopedReplicateClient.GetTrainingStatusAsync(trainingId);

            // Find the model creation request by training ID
            var modelRequest = await scopedDbContext.ModelCreationRequests
                .FirstOrDefaultAsync(r => r.PendingTrainingRequestId == trainingId);

            if (modelRequest == null)
            {
                _logger.LogWarning("No model creation request found for training ID {TrainingId}", trainingId);
                return;
            }

            _logger.LogInformation("Found model creation request {RequestId} for training {TrainingId}",
                modelRequest.Id, trainingId);

            // Update the model creation request based on training status
            if (trainingStatus.IsCompleted && trainingStatus.Status?.ToLower() == "succeeded")
            {
                _logger.LogInformation("Training {TrainingId} completed successfully", trainingId);

                // Extract the trained model version from the training result
                var trainedModelVersion = trainingStatus.Version;
                if (string.IsNullOrEmpty(trainedModelVersion))
                {
                    _logger.LogError("Training completed but no model version found in response for training {TrainingId}", trainingId);
                    modelRequest.Status = ModelCreationStatus.Failed;
                    modelRequest.ErrorMessage = "Training completed but no model version found in response";
                    modelRequest.CompletedAt = DateTime.UtcNow;
                }
                else
                {
                    // Update model creation request to Ready status
                    modelRequest.Status = ModelCreationStatus.Ready;
                    modelRequest.TrainedModelVersion = trainedModelVersion;
                    modelRequest.CompletedAt = DateTime.UtcNow;
                    modelRequest.ErrorMessage = null;

                    _logger.LogInformation("Model creation request {RequestId} marked as Ready with version {Version}",
                        modelRequest.Id, trainedModelVersion);

                    // Save changes first
                    await scopedDbContext.SaveChangesAsync();

                    // Generate automatic sample images with the trained model
                    _ = Task.Run(async () => await GenerateAutomaticSampleImages(modelRequest.UserId, trainedModelVersion));
                }
            }
            else if (trainingStatus.Status?.ToLower() == "failed" || trainingStatus.Status?.ToLower() == "canceled")
            {
                _logger.LogWarning("Training {TrainingId} failed or was canceled with status {Status}", trainingId, trainingStatus.Status);

                modelRequest.Status = ModelCreationStatus.Failed;
                modelRequest.ErrorMessage = trainingStatus.Error ?? $"Training failed with status: {trainingStatus.Status}";
                modelRequest.CompletedAt = DateTime.UtcNow;
            }
            else
            {
                _logger.LogInformation("Training {TrainingId} not yet complete, status: {Status}", trainingId, trainingStatus.Status);
                return; // Training still in progress, don't save changes
            }

            // Save the updated model creation request
            await scopedDbContext.SaveChangesAsync();

            _logger.LogInformation("Successfully processed training completion for {TrainingId}, status: {Status}",
                trainingId, modelRequest.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing training completion for {TrainingId}", trainingId);
        }
    }

    /// <summary>
    /// Generate automatic sample images after training completion
    /// This matches the behavior that was expected after webhook training completion
    /// </summary>
    private async Task GenerateAutomaticSampleImages(string userId, string trainedModelVersion)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var scopedDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scopedReplicateClient = scope.ServiceProvider.GetRequiredService<IReplicateApiClient>();

        try
        {
            _logger.LogInformation("Generating automatic sample images for user {UserId} with model {ModelVersion}",
                userId, trainedModelVersion);

            // Get user profile for generation
            var userProfile = await scopedDbContext.UserProfiles
                .FirstOrDefaultAsync(up => up.UserId == userId);

            if (userProfile == null)
            {
                _logger.LogWarning("No user profile found for user {UserId} for automatic image generation", userId);
                return;
            }

            // Create UserInfo for generation
            var userInfo = new UserInfo
            {
                Gender = userProfile.Gender,
                Ethnicity = userProfile.Ethnicity
            };

            // Generate a "professional" style image as the default sample
            var sampleStyle = "professional";

            _logger.LogInformation("Starting automatic {Style} image generation for user {UserId}", sampleStyle, userId);

            var result = await scopedReplicateClient.GenerateImagesAsync(
                trainedModelVersion,
                userId,
                sampleStyle,
                userInfo,
                numOutputs: 2); // Generate 2 sample images

            _logger.LogInformation("Automatic sample image generation initiated with prediction ID {PredictionId} for user {UserId}",
                result.Id, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate automatic sample images for user {UserId}", userId);
        }
    }
}