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

                // Extract the trained model version for the user's custom model
                // Note: Replicate training status "version" points to the trainer model (fast-flux-trainer)
                // We MUST resolve the actual latest version of the created destination model
                string? resolvedVersionId = null;

                try
                {
                    if (!string.IsNullOrEmpty(modelRequest.ReplicateModelId))
                    {
                        // Construct full model ID for API call (add owner prefix for API)
                        var fullModelId = modelRequest.ReplicateModelId!.Contains("/")
                            ? modelRequest.ReplicateModelId!
                            : $"alanw707/{modelRequest.ReplicateModelId}";
                        resolvedVersionId = await scopedReplicateClient.GetModelVersionAsync(fullModelId);
                        _logger.LogInformation("Resolved model version for {ModelId}: {VersionId}", fullModelId, resolvedVersionId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to resolve version for model {ModelId}", modelRequest.ReplicateModelId);
                }

                // Critical: Do NOT fall back to trainingStatus.Version (it's the trainer model's version)
                if (string.IsNullOrEmpty(resolvedVersionId))
                {
                    _logger.LogError("Could not resolve model version for {ModelId}. Training marked as failed.", modelRequest.ReplicateModelId);
                    modelRequest.Status = ModelCreationStatus.Failed;
                    modelRequest.ErrorMessage = "Could not determine trained model version";
                    modelRequest.CompletedAt = DateTime.UtcNow;
                    await scopedDbContext.SaveChangesAsync();
                    return;
                }

                var trainedModelVersion = resolvedVersionId;
                
                // Validate version format (should be 64-character hex string)
                if (!System.Text.RegularExpressions.Regex.IsMatch(trainedModelVersion, @"^[a-fA-F0-9]{64}$"))
                {
                    _logger.LogError("Invalid version format received: {Version} for model {ModelId}. Expected 64-character hex string.", 
                        trainedModelVersion, modelRequest.ReplicateModelId);
                    modelRequest.Status = ModelCreationStatus.Failed;
                    modelRequest.ErrorMessage = $"Invalid version format received: {trainedModelVersion}";
                    modelRequest.CompletedAt = DateTime.UtcNow;
                    await scopedDbContext.SaveChangesAsync();
                    return;
                }
                
                // Wait for version to become visible/available on Replicate before marking Ready
                var modelIdForApi = modelRequest.ReplicateModelId!;
                var waitStart = DateTime.UtcNow;
                var completedAtUtc = trainingStatus.CompletedAt;
                var waitSucceeded = await scopedReplicateClient.WaitForModelVersionAvailabilityAsync(
                    modelIdForApi, trainedModelVersion, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(20));

                if (!waitSucceeded)
                {
                    // Defer flipping to Ready; allow background poller to try again next cycle
                    var waited = DateTime.UtcNow - waitStart;
                    double? sinceCompletion = completedAtUtc.HasValue ? (DateTime.UtcNow - completedAtUtc.Value).TotalSeconds : null;
                    _logger.LogWarning(
                        "Version {Version} for model {ModelId} not visible yet after {WaitedSec:F1}s{SinceCompletion}",
                        trainedModelVersion,
                        modelIdForApi,
                        waited.TotalSeconds,
                        sinceCompletion.HasValue ? $", ~{sinceCompletion.Value:F1}s since training completion" : string.Empty);
                    return;
                }
                else
                {
                    var waited = DateTime.UtcNow - waitStart;
                    double? sinceCompletion = completedAtUtc.HasValue ? (DateTime.UtcNow - completedAtUtc.Value).TotalSeconds : null;
                    _logger.LogInformation(
                        "Version {Version} for model {ModelId} became available after {WaitedSec:F1}s{SinceCompletion}",
                        trainedModelVersion,
                        modelIdForApi,
                        waited.TotalSeconds,
                        sinceCompletion.HasValue ? $", ~{sinceCompletion.Value:F1}s since training completion" : string.Empty);
                }

                // Update model creation request to Ready status
                modelRequest.Status = ModelCreationStatus.Ready;
                modelRequest.TrainedModelVersion = trainedModelVersion;
                modelRequest.CompletedAt = DateTime.UtcNow;
                modelRequest.ErrorMessage = null;

                _logger.LogInformation("Model creation request {RequestId} marked as Ready with version {Version}",
                    modelRequest.Id, trainedModelVersion);

                // Save changes first
                await scopedDbContext.SaveChangesAsync();

                // Note: Removed automatic sample generation - users should only get what they explicitly request
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

}
