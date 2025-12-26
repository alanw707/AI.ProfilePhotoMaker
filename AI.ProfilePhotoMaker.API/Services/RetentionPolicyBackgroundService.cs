using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.Extensions.Options;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AI.ProfilePhotoMaker.API.Services;

public class RetentionPolicyBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RetentionPolicyBackgroundService> _logger;
    private readonly RetentionPolicyBackgroundServiceOptions _options;
    private readonly RetentionNotificationOptions _notificationOptions;
    private readonly TimeProvider _timeProvider;

    public RetentionPolicyBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<RetentionPolicyBackgroundService> logger,
        IOptions<RetentionPolicyBackgroundServiceOptions> options,
        IOptions<RetentionNotificationOptions> notificationOptions,
        TimeProvider timeProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _notificationOptions = notificationOptions.Value;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Retention Policy Background Service started");

        try
        {
            // Wait a bit before starting the first check to let the application start up
            await Task.Delay(_options.InitialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformRetentionPolicyCheck();
                    await Task.Delay(_options.CheckInterval, stoppingToken);
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
                            await Task.Delay(_options.ErrorRetryDelay, stoppingToken);
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

    // Internal for integration tests; scheduled execution occurs via ExecuteAsync.
    internal async Task PerformRetentionPolicyCheck()
    {
        using var scope = _serviceProvider.CreateScope();
        var retentionService = scope.ServiceProvider.GetRequiredService<IRetentionPolicyService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            _logger.LogDebug("Starting retention policy check...");

            // First, set retention dates for any existing images that don't have them
            await retentionService.SetRetentionDatesForExistingImagesAsync();

            // Send deletion warning notifications
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();
            await SendDeletionWarningNotificationsAsync(retentionService, emailService, dbContext);

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
        IEmailNotificationService emailService,
        ApplicationDbContext dbContext)
    {
        try
        {
            // Notification windows: 14 days and 7 days before deletion
            var notificationWindows = _notificationOptions.NotificationDays?.Length > 0
                ? _notificationOptions.NotificationDays
                : new[] { 14, 7 };
            const int notificationWindowSizeDays = 1;
            var totalSent = 0;
            var totalSkipped = 0;
            var totalErrors = 0;
            var totalDuplicates = 0;

            foreach (var daysBeforeDeletion in notificationWindows.Distinct())
            {
                // Use 2-day window to check for images approaching deletion
                var imagesByUser = await retentionService.GetImagesApproachingDeletionAsync(
                    daysBeforeDeletion,
                    windowSizeDays: notificationWindowSizeDays);

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
                var duplicatesForWindow = 0;

                foreach (var (userId, (email, images)) in imagesByUser)
                {
                    // Skip users without email addresses
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        _logger.LogDebug("Skipping {DaysBeforeDeletion}-day notification for user {UserId} - no email address", daysBeforeDeletion, userId);
                        skippedForWindow++;
                        continue;
                    }

                    var imagesByDeletionDate = images
                        .GroupBy(img => img.ScheduledDeletionDate.Date)
                        .OrderBy(group => group.Key);

                    foreach (var imageGroup in imagesByDeletionDate)
                    {
                        var deletionDate = imageGroup.Key;
                        var imageCount = imageGroup.Count();
                        if (await IsDeletionWarningAlreadySentAsync(dbContext, daysBeforeDeletion, userId, deletionDate))
                        {
                            duplicatesForWindow++;
                            _logger.LogDebug(
                                "Skipping duplicate {DaysBeforeDeletion}-day deletion warning for user {UserId} (deletion date {DeletionDate})",
                                daysBeforeDeletion, userId, deletionDate);
                            continue;
                        }

                        try
                        {
                            await emailService.SendRetentionDeletionWarningAsync(
                                userId,
                                email,
                                imageCount,
                                deletionDate,
                                daysBeforeDeletion);

                            sentForWindow++;
                            _logger.LogDebug(
                                "Sent {DaysBeforeDeletion}-day deletion warning to user {UserId} for {ImageCount} image(s)",
                                daysBeforeDeletion, userId, imageCount);

                            await TryLogDeletionWarningAsync(dbContext, daysBeforeDeletion, userId, deletionDate);
                        }
                        catch (Exception ex)
                        {
                            errorsForWindow++;
                            _logger.LogWarning(ex,
                                "Failed to send {DaysBeforeDeletion}-day deletion warning to user {UserId}: {Error}",
                                daysBeforeDeletion, userId, ex.Message);
                        }
                    }
                }

                totalSent += sentForWindow;
                totalSkipped += skippedForWindow;
                totalErrors += errorsForWindow;
                totalDuplicates += duplicatesForWindow;

                if (sentForWindow > 0 || errorsForWindow > 0 || duplicatesForWindow > 0)
                {
                    _logger.LogInformation(
                        "{DaysBeforeDeletion}-day deletion warnings: {SentCount} sent, {SkippedCount} skipped (no email), {DuplicateCount} duplicates, {ErrorCount} errors",
                        daysBeforeDeletion, sentForWindow, skippedForWindow, duplicatesForWindow, errorsForWindow);
                }
            }

            if (totalSent > 0 || totalErrors > 0 || totalDuplicates > 0)
            {
                _logger.LogInformation(
                    "Deletion warning notifications completed: {TotalSent} sent, {TotalSkipped} skipped, {TotalDuplicates} duplicates, {TotalErrors} errors",
                    totalSent, totalSkipped, totalDuplicates, totalErrors);
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - notification failures shouldn't block deletion process
            _logger.LogError(ex, "Error sending deletion warning notifications: {Error}", ex.Message);
        }
    }

    private async Task<bool> IsDeletionWarningAlreadySentAsync(
        ApplicationDbContext dbContext,
        int daysBeforeDeletion,
        string userId,
        DateTime deletionDate)
    {
        var deletionDay = deletionDate.Date;

        return await dbContext.RetentionDeletionWarningLogs
            .AsNoTracking()
            .AnyAsync(log =>
                log.UserId == userId &&
                log.DaysBeforeDeletion == daysBeforeDeletion &&
                log.DeletionDate == deletionDay);
    }

    private async Task<bool> TryLogDeletionWarningAsync(
        ApplicationDbContext dbContext,
        int daysBeforeDeletion,
        string userId,
        DateTime deletionDate)
    {
        var logEntry = new RetentionDeletionWarningLog
        {
            UserId = userId,
            DaysBeforeDeletion = daysBeforeDeletion,
            DeletionDate = deletionDate.Date,
            SentAtUtc = _timeProvider.GetUtcNow().UtcDateTime
        };

        dbContext.RetentionDeletionWarningLogs.Add(logEntry);

        try
        {
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogDebug(
                "Retention deletion warning already logged for user {UserId} on {DeletionDate} ({DaysBeforeDeletion}-day)",
                userId, deletionDate.Date, daysBeforeDeletion);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to record retention deletion warning for user {UserId} on {DeletionDate} ({DaysBeforeDeletion}-day)",
                userId, deletionDate.Date, daysBeforeDeletion);
            return false;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is SqlException sqlException)
        {
            return sqlException.Number is 2601 or 2627;
        }

        return false;
    }
}
