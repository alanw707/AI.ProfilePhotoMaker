using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

/// <summary>
/// Background service that polls for training completions and processes them
/// </summary>
public class TrainingPollingBackgroundService : BackgroundService
{
    private readonly ILogger<TrainingPollingBackgroundService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(30); // Poll every 30 seconds
    private readonly TimeSpan _initialDelay = TimeSpan.FromSeconds(10); // Wait 10 seconds after startup

    public TrainingPollingBackgroundService(
        ILogger<TrainingPollingBackgroundService> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Training Polling Background Service starting up");
        
        // Wait a bit after startup to let the application fully initialize
        await Task.Delay(_initialDelay, stoppingToken);

        // Check for any existing in-progress trainings on startup
        await CheckExistingTrainings(stoppingToken);

        // Main polling loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollForTrainingCompletions(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in training polling background service");
            }

            // Wait for the next polling interval
            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
        }

        _logger.LogInformation("Training Polling Background Service shutting down");
    }

    /// <summary>
    /// Check for existing in-progress trainings on startup and resume polling for them
    /// </summary>
    private async Task CheckExistingTrainings(CancellationToken stoppingToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            var inProgressTrainings = await dbContext.ModelCreationRequests
                .Where(r => r.Status == ModelCreationStatus.Creating && 
                           !string.IsNullOrEmpty(r.PendingTrainingRequestId))
                .ToListAsync(stoppingToken);

            if (inProgressTrainings.Any())
            {
                _logger.LogInformation("Found {Count} in-progress trainings to monitor on startup", 
                    inProgressTrainings.Count);
                
                foreach (var training in inProgressTrainings)
                {
                    _logger.LogInformation("Resuming monitoring for training {TrainingId} for user {UserId}", 
                        training.PendingTrainingRequestId, training.UserId);
                }
            }
            else
            {
                _logger.LogInformation("No in-progress trainings found on startup");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for existing in-progress trainings on startup");
        }
    }

    /// <summary>
    /// Poll for training completions and process any completed trainings
    /// </summary>
    private async Task PollForTrainingCompletions(CancellationToken stoppingToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var trainingPollingService = scope.ServiceProvider.GetRequiredService<ITrainingPollingService>();

        try
        {
            // Get all model creation requests that are currently in Creating status
            var inProgressTrainings = await dbContext.ModelCreationRequests
                .Where(r => r.Status == ModelCreationStatus.Creating && 
                           !string.IsNullOrEmpty(r.PendingTrainingRequestId))
                .ToListAsync(stoppingToken);

            if (!inProgressTrainings.Any())
            {
                // No trainings to poll, just return
                return;
            }

            _logger.LogDebug("Polling {Count} in-progress trainings", inProgressTrainings.Count);

            var tasks = inProgressTrainings.Select(async training =>
            {
                try
                {
                    if (string.IsNullOrEmpty(training.PendingTrainingRequestId))
                    {
                        _logger.LogWarning("Training request {RequestId} has no PendingTrainingRequestId", training.Id);
                        return;
                    }

                    // Check if this training is complete
                    var isComplete = await trainingPollingService.IsTrainingComplete(training.PendingTrainingRequestId);
                    
                    if (isComplete)
                    {
                        _logger.LogInformation("Training {TrainingId} for user {UserId} has completed, processing...", 
                            training.PendingTrainingRequestId, training.UserId);

                        // Process the completion
                        await trainingPollingService.ProcessTrainingCompletion(training.PendingTrainingRequestId);
                    }
                    else
                    {
                        _logger.LogDebug("Training {TrainingId} for user {UserId} still in progress", 
                            training.PendingTrainingRequestId, training.UserId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling training {TrainingId} for user {UserId}", 
                        training.PendingTrainingRequestId, training.UserId);
                }
            });

            // Execute all polling tasks with some parallelism but not too much to avoid rate limits
            var semaphore = new SemaphoreSlim(3, 3); // Limit to 3 concurrent requests
            var limitedTasks = tasks.Select(async task =>
            {
                await semaphore.WaitAsync(stoppingToken);
                try
                {
                    await task;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(limitedTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during training polling cycle");
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Training Polling Background Service stop requested");
        await base.StopAsync(stoppingToken);
    }
}