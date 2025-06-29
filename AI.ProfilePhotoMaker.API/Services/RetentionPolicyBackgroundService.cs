using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.EntityFrameworkCore;

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
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredImages();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing expired images");
                }

                // Wait for the next interval
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Retention Policy Background Service cancellation requested.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Retention Policy Background Service");
        }

        _logger.LogInformation("Retention Policy Background Service stopped");
    }

    private async Task ProcessExpiredImages()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            _logger.LogInformation("Starting retention policy cleanup check");

            var now = DateTime.UtcNow;
            
            // Find images that are past their scheduled deletion date and not already marked for deletion
            var expiredImages = await dbContext.ProcessedImages
                .Where(img => !img.IsDeleted && 
                             !img.IsMarkedForDeletion && 
                             img.ScheduledDeletionDate <= now)
                .ToListAsync();

            if (expiredImages.Any())
            {
                _logger.LogInformation("Found {Count} expired images to mark for deletion", expiredImages.Count);

                foreach (var image in expiredImages)
                {
                    // Mark for deletion but don't immediately delete
                    // This allows for a grace period and user recovery
                    image.IsMarkedForDeletion = true;
                    
                    _logger.LogDebug("Marked image {ImageId} for deletion (Created: {CreatedAt}, Scheduled: {ScheduledDate})", 
                        image.Id, image.CreatedAt, image.ScheduledDeletionDate);
                }

                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Successfully marked {Count} images for deletion", expiredImages.Count);
            }

            // Find images marked for deletion for more than 24 hours and perform soft delete
            var imagesToSoftDelete = await dbContext.ProcessedImages
                .Where(img => !img.IsDeleted && 
                             img.IsMarkedForDeletion && 
                             img.ScheduledDeletionDate <= now.AddDays(-1)) // Grace period of 1 day
                .ToListAsync();

            if (imagesToSoftDelete.Any())
            {
                _logger.LogInformation("Found {Count} images to soft delete", imagesToSoftDelete.Count);

                foreach (var image in imagesToSoftDelete)
                {
                    // Perform soft delete
                    image.IsDeleted = true;
                    image.DeletedAt = DateTime.UtcNow;
                    
                    _logger.LogDebug("Soft deleted image {ImageId}", image.Id);
                }

                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Successfully soft deleted {Count} images", imagesToSoftDelete.Count);
            }

            _logger.LogInformation("Completed retention policy cleanup check");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process expired images");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retention Policy Background Service is stopping");
        return base.StopAsync(cancellationToken);
    }
}