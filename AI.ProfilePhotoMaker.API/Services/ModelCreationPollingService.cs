using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

/// <summary>
/// Background service that polls for model creation completion and triggers training
/// </summary>
public class ModelCreationPollingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModelCreationPollingService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(1); // Poll every minute

    public ModelCreationPollingService(
        IServiceProvider serviceProvider,
        ILogger<ModelCreationPollingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Model Creation Polling Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckPendingModels(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in model creation polling service");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Model Creation Polling Service stopped");
    }

    private async Task CheckPendingModels(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var replicateClient = scope.ServiceProvider.GetRequiredService<IReplicateApiClient>();

        // Get models that are in "Creating" status
        var pendingModels = await context.ModelCreationRequests
            .Where(m => m.Status == ModelCreationStatus.Creating)
            .ToListAsync(cancellationToken);

        if (!pendingModels.Any())
        {
            _logger.LogDebug("No pending model creation requests found");
            return;
        }

        _logger.LogInformation("Checking {Count} pending model creation requests", pendingModels.Count);

        foreach (var modelRequest in pendingModels)
        {
            try
            {
                await CheckModelStatus(modelRequest, replicateClient, context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking model {ModelId} for user {UserId}", 
                    modelRequest.ReplicateModelId, modelRequest.UserId);
                
                // Mark as failed after multiple attempts could be added here
                continue;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task CheckModelStatus(
        ModelCreationRequest modelRequest, 
        IReplicateApiClient replicateClient, 
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(modelRequest.ReplicateModelId))
        {
            _logger.LogWarning("Model request {RequestId} has no ReplicateModelId", modelRequest.Id);
            return;
        }

        try
        {
            // Check if model is ready by trying to get its information
            var modelInfo = await CheckModelReadiness(modelRequest.ReplicateModelId, replicateClient);
            
            if (modelInfo.IsReady)
            {
                _logger.LogInformation("Model {ModelId} is ready! Triggering training for user {UserId}", 
                    modelRequest.ReplicateModelId, modelRequest.UserId);

                // Update status to Ready
                modelRequest.Status = ModelCreationStatus.Ready;
                modelRequest.CompletedAt = DateTime.UtcNow;

                // Trigger training if we have the training data
                if (!string.IsNullOrEmpty(modelRequest.TrainingImageZipUrl))
                {
                    try
                    {
                        await replicateClient.CreateModelTrainingWithDestinationAsync(
                            modelRequest.UserId,
                            modelRequest.TrainingImageZipUrl,
                            modelRequest.ReplicateModelId);

                        _logger.LogInformation("Training triggered successfully for model {ModelId}", 
                            modelRequest.ReplicateModelId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to trigger training for model {ModelId}", 
                            modelRequest.ReplicateModelId);
                        
                        modelRequest.ErrorMessage = $"Training trigger failed: {ex.Message}";
                    }
                }
            }
            else if (modelInfo.HasError)
            {
                _logger.LogError("Model creation failed for {ModelId}: {Error}", 
                    modelRequest.ReplicateModelId, modelInfo.ErrorMessage);
                
                modelRequest.Status = ModelCreationStatus.Failed;
                modelRequest.CompletedAt = DateTime.UtcNow;
                modelRequest.ErrorMessage = modelInfo.ErrorMessage;
            }
            else
            {
                _logger.LogDebug("Model {ModelId} is still being created", modelRequest.ReplicateModelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking model status for {ModelId}", modelRequest.ReplicateModelId);
            
            // Don't mark as failed immediately - could be a temporary network issue
            // Add retry logic or failure count if needed
        }
    }

    private async Task<ModelReadinessResult> CheckModelReadiness(string modelId, IReplicateApiClient replicateClient)
    {
        try
        {
            // Try to access the model - if successful, it's ready
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Token", GetReplicateToken());

            // If modelId already contains owner, use it as-is, otherwise add alanw707/
            var modelUrl = modelId.Contains("/")
                ? $"https://api.replicate.com/v1/models/{modelId}"
                : $"https://api.replicate.com/v1/models/alanw707/{modelId}";
                
            var response = await httpClient.GetAsync(modelUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Model {ModelId} check response: {Response}", modelId, content);
                
                // If we can successfully get the model, it's ready
                return new ModelReadinessResult { IsReady = true };
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Model is still being created
                return new ModelReadinessResult { IsReady = false };
            }
            else
            {
                // Some other error
                var errorContent = await response.Content.ReadAsStringAsync();
                return new ModelReadinessResult 
                { 
                    HasError = true, 
                    ErrorMessage = $"HTTP {response.StatusCode}: {errorContent}" 
                };
            }
        }
        catch (Exception ex)
        {
            return new ModelReadinessResult 
            { 
                HasError = true, 
                ErrorMessage = ex.Message 
            };
        }
    }

    private string GetReplicateToken()
    {
        using var scope = _serviceProvider.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        return configuration["Replicate:ApiToken"] ?? throw new InvalidOperationException("Replicate API token not configured");
    }

    private class ModelReadinessResult
    {
        public bool IsReady { get; set; }
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
    }
}