using AI.ProfilePhotoMaker.API.Services;

namespace AI.ProfilePhotoMaker.API.Services;

public class BasicTierBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BasicTierBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Check every hour

    public BasicTierBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<BasicTierBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Basic Tier Background Service started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessCreditResets();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing credit resets");
                }

                // Wait for the next interval
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Basic Tier Background Service cancellation requested.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Basic Tier Background Service");
        }

        _logger.LogInformation("Basic Tier Background Service stopped");
    }

    private async Task ProcessCreditResets()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var basicTierService = scope.ServiceProvider.GetRequiredService<IBasicTierService>();

            _logger.LogInformation("Starting weekly credit reset check");
            await basicTierService.ResetAllExpiredCreditsAsync();
            _logger.LogInformation("Completed weekly credit reset check");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process credit resets");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Basic Tier Background Service is stopping");
        return base.StopAsync(cancellationToken);
    }
}