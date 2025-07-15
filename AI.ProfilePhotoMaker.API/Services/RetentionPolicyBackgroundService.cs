using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AI.ProfilePhotoMaker.API.Services;

public class RetentionPolicyBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RetentionPolicyBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6); // Check every 6 hours

    public RetentionPolicyBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<RetentionPolicyBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Retention Policy Background Service started");

        // Wait a bit before starting the first check to let the application start up
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformRetentionPolicyCheck();
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation token is triggered
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during retention policy check");
                // Wait a shorter time before retrying if there was an error
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        _logger.LogInformation("Retention Policy Background Service stopped");
    }

    private async Task PerformRetentionPolicyCheck()
    {
        using var scope = _serviceProvider.CreateScope();
        var retentionService = scope.ServiceProvider.GetRequiredService<IRetentionPolicyService>();

        try
        {
            _logger.LogDebug("Starting retention policy check...");

            // First, set retention dates for any existing images that don't have them
            await retentionService.SetRetentionDatesForExistingImagesAsync();

            // Get expired images for logging purposes
            var expiredImages = await retentionService.GetExpiredImagesAsync();
            if (expiredImages.Any())
            {
                _logger.LogInformation(
                    "Found {ExpiredCount} expired images to delete",
                    expiredImages.Count);

                // Log details about what will be deleted
                foreach (var image in expiredImages.Take(10)) // Log first 10 for debugging
                {
                    _logger.LogDebug(
                        "Expired image {ImageId} ({ImageType}): scheduled for deletion on {ScheduledDeletion}",
                        image.ImageId, image.ImageType, image.ScheduledDeletion);
                }

                if (expiredImages.Count > 10)
                {
                    _logger.LogDebug("... and {RemainingCount} more", expiredImages.Count - 10);
                }
            }

            // Delete expired images
            var deletedCount = await retentionService.DeleteExpiredImagesAsync();

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Retention policy check completed: deleted {DeletedCount} expired images",
                    deletedCount);
            }
            else
            {
                _logger.LogDebug("Retention policy check completed: no expired images found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform retention policy check");
            throw; // Re-throw to trigger the retry logic in ExecuteAsync
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retention Policy Background Service is stopping...");
        await base.StopAsync(cancellationToken);
    }
}