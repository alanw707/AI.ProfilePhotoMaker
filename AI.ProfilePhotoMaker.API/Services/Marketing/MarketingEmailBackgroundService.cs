using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Services.Marketing;

public class MarketingEmailBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MarketingEmailBackgroundService> _logger;
    private readonly MarketingEmailOptions _options;

    public MarketingEmailBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<MarketingEmailBackgroundService> logger,
        IOptions<MarketingEmailOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Marketing Email Background Service started");

        try
        {
            await Task.Delay(_options.InitialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessScheduledCampaigns(stoppingToken);
                    await Task.Delay(_options.CheckInterval, stoppingToken);
                }
                catch (TaskCanceledException) { break; }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Marketing email background service encountered an error");
                    try { await Task.Delay(_options.ErrorRetryDelay, stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }
        catch (TaskCanceledException) { }
        catch (OperationCanceledException) { }

        _logger.LogInformation("Marketing Email Background Service stopped");
    }

    internal async Task ProcessScheduledCampaigns(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marketingService = scope.ServiceProvider.GetRequiredService<IMarketingEmailService>();

        var now = DateTime.UtcNow;
        var campaignsDue = await db.MarketingCampaigns
            .Where(c => c.Status == CampaignStatus.Scheduled && c.ScheduledAt <= now)
            .OrderBy(c => c.ScheduledAt)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        if (campaignsDue.Count == 0) return;

        _logger.LogInformation("Found {Count} campaign(s) due for sending", campaignsDue.Count);

        foreach (var campaignId in campaignsDue)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                await marketingService.ExecuteCampaignAsync(campaignId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute campaign {CampaignId}", campaignId);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Marketing Email Background Service is stopping");
        await base.StopAsync(cancellationToken);
    }
}
