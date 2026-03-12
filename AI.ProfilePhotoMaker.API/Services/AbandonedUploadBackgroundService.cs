using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Services;

public class AbandonedUploadBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AbandonedUploadBackgroundService> _logger;
    private readonly AbandonedUploadOptions _options;
    private readonly TimeProvider _timeProvider;

    public AbandonedUploadBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AbandonedUploadBackgroundService> logger,
        IOptions<AbandonedUploadOptions> options,
        TimeProvider timeProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Abandoned Upload Background Service started (Enabled: {Enabled})", _options.Enabled);

        try
        {
            await Task.Delay(_options.InitialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformAbandonedUploadCheck(stoppingToken);
                    await Task.Delay(_options.CheckInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during abandoned upload check");

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(_options.ErrorRetryDelay, stoppingToken);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Abandoned Upload Background Service cancellation requested");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Abandoned Upload Background Service cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Abandoned Upload Background Service");
            throw;
        }

        _logger.LogInformation("Abandoned Upload Background Service stopped");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Abandoned Upload Background Service is stopping...");
        await base.StopAsync(cancellationToken);
    }

    internal async Task PerformAbandonedUploadCheck(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Abandoned upload check is disabled via configuration");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailNotificationService>();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var graceCutoff = now.Subtract(_options.GracePeriod);
        var maxAgeCutoff = now.Subtract(_options.MaxAge);

        var candidates = await dbContext.UserProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.CreatedAt >= maxAgeCutoff &&
                profile.CreatedAt < graceCutoff &&
                !dbContext.ProcessedImages.Any(img => img.UserProfileId == profile.Id && img.IsOriginalUpload) &&
                !dbContext.AbandonedUploadNudgeLogs.Any(log => log.UserId == profile.UserId))
            .Join(
                dbContext.Users.AsNoTracking(),
                profile => profile.UserId,
                user => user.Id,
                (profile, user) => new
                {
                    profile.UserId,
                    user.Email,
                    user.FirstName,
                })
            .Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            _logger.LogDebug("No abandoned uploads found to nudge");
            return;
        }

        _logger.LogInformation("Found {UserCount} user(s) with abandoned uploads to nudge", candidates.Count);

        var sentCount = 0;
        var skippedCount = 0;
        var errorCount = 0;

        foreach (var candidate in candidates)
        {
            try
            {
                var alreadySent = await dbContext.AbandonedUploadNudgeLogs
                    .AsNoTracking()
                    .AnyAsync(log => log.UserId == candidate.UserId, cancellationToken);

                if (alreadySent)
                {
                    skippedCount++;
                    continue;
                }

                await emailService.SendAbandonedUploadNudgeAsync(candidate.UserId, candidate.Email, candidate.FirstName);

                dbContext.AbandonedUploadNudgeLogs.Add(new AbandonedUploadNudgeLog
                {
                    UserId = candidate.UserId,
                    SentAtUtc = now,
                });

                await dbContext.SaveChangesAsync(cancellationToken);
                sentCount++;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                dbContext.ChangeTracker.Clear();
                skippedCount++;
            }
            catch (Exception ex)
            {
                dbContext.ChangeTracker.Clear();
                errorCount++;
                _logger.LogWarning(ex, "Failed to send abandoned upload nudge to user {UserId}", candidate.UserId);
            }
        }

        _logger.LogInformation(
            "Abandoned upload nudges completed: {SentCount} sent, {SkippedCount} skipped, {ErrorCount} errors",
            sentCount,
            skippedCount,
            errorCount);
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
