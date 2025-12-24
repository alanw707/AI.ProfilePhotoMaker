using AI.ProfilePhotoMaker.API.Services.Notifications;
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

        try
        {
            // Wait a bit before starting the first check to let the application start up
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformRetentionPolicyCheck();
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancellation token is triggered during Task.Delay
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation token is triggered
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during retention policy check");

                    // Check if we should still continue
                    if (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            // Wait a shorter time before retrying if there was an error
                            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                        }
                        catch (TaskCanceledException)
                        {
                            // Expected during shutdown
                            break;
                        }
                    }
                }
            }
        }
        catch (TaskCanceledException)
        {
            // This is expected when the service is stopping
            _logger.LogInformation("Retention Policy Background Service cancellation requested");
        }
        catch (OperationCanceledException)
        {
            // This is expected when the service is stopping
            _logger.LogInformation("Retention Policy Background Service cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Retention Policy Background Service");
            throw; // Re-throw to ensure the host knows about the failure
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

            // Send deletion warning notifications
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();
            await SendDeletionWarningNotificationsAsync(retentionService, emailService);

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

            // Clean up orphaned enhanced images older than 1 hour
            var enhancedCleanupCount = await retentionService.CleanupOrphanedEnhancedImagesAsync(TimeSpan.FromHours(1));

            if (deletedCount > 0 || enhancedCleanupCount > 0)
            {
                _logger.LogInformation(
                    "Retention policy check completed: deleted {DeletedCount} expired images and {EnhancedCleanupCount} orphaned enhanced images",
                    deletedCount, enhancedCleanupCount);
            }
            else
            {
                _logger.LogDebug("Retention policy check completed: no expired images or orphaned enhanced images found");
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

    private async Task SendDeletionWarningNotificationsAsync(
        IRetentionPolicyService retentionService,
        IEmailNotificationService emailService)
    {
        try
        {
            // Notification windows: 14 days and 7 days before deletion
            var notificationWindows = new[] { 14, 7 };
            var totalSent = 0;
            var totalSkipped = 0;
            var totalErrors = 0;

            foreach (var daysBeforeDeletion in notificationWindows)
            {
                // Use 1-day window to check for images approaching deletion
                var imagesByUser = await retentionService.GetImagesApproachingDeletionAsync(daysBeforeDeletion, windowSizeDays: 1);

                if (!imagesByUser.Any())
                {
                    _logger.LogDebug("No images found for {DaysBeforeDeletion}-day deletion warning notification", daysBeforeDeletion);
                    continue;
                }

                _logger.LogInformation(
                    "Found {UserCount} user(s) with images approaching deletion for {DaysBeforeDeletion}-day warning",
                    imagesByUser.Count, daysBeforeDeletion);

                var sentForWindow = 0;
                var skippedForWindow = 0;
                var errorsForWindow = 0;

                foreach (var (userId, (email, images)) in imagesByUser)
                {
                    // Skip users without email addresses
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        _logger.LogDebug("Skipping {DaysBeforeDeletion}-day notification for user {UserId} - no email address", daysBeforeDeletion, userId);
                        skippedForWindow++;
                        continue;
                    }

                    // Determine the earliest deletion date for this user's images (use the first image's deletion date)
                    var earliestDeletionDate = images.Min(img => img.ScheduledDeletionDate);

                    try
                    {
                        await emailService.SendRetentionDeletionWarningAsync(
                            userId,
                            email,
                            images.Count,
                            earliestDeletionDate,
                            daysBeforeDeletion);

                        sentForWindow++;
                        _logger.LogDebug(
                            "Sent {DaysBeforeDeletion}-day deletion warning to user {UserId} for {ImageCount} image(s)",
                            daysBeforeDeletion, userId, images.Count);
                    }
                    catch (Exception ex)
                    {
                        errorsForWindow++;
                        _logger.LogWarning(ex,
                            "Failed to send {DaysBeforeDeletion}-day deletion warning to user {UserId}: {Error}",
                            daysBeforeDeletion, userId, ex.Message);
                    }
                }

                totalSent += sentForWindow;
                totalSkipped += skippedForWindow;
                totalErrors += errorsForWindow;

                if (sentForWindow > 0 || errorsForWindow > 0)
                {
                    _logger.LogInformation(
                        "{DaysBeforeDeletion}-day deletion warnings: {SentCount} sent, {SkippedCount} skipped (no email), {ErrorCount} errors",
                        daysBeforeDeletion, sentForWindow, skippedForWindow, errorsForWindow);
                }
            }

            if (totalSent > 0 || totalErrors > 0)
            {
                _logger.LogInformation(
                    "Deletion warning notifications completed: {TotalSent} sent, {TotalSkipped} skipped, {TotalErrors} errors",
                    totalSent, totalSkipped, totalErrors);
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - notification failures shouldn't block deletion process
            _logger.LogError(ex, "Error sending deletion warning notifications: {Error}", ex.Message);
        }
    }
}