using AI.ProfilePhotoMaker.API.Services;

namespace AI.ProfilePhotoMaker.API.Services;

public class ModelExpirationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModelExpirationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(4); // Check every 4 hours

    public ModelExpirationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ModelExpirationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Model Expiration Background Service started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredModels();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing expired models");
                }

                // Wait for the next interval
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Model Expiration Background Service cancellation requested.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Model Expiration Background Service");
        }

        _logger.LogInformation("Model Expiration Background Service stopped");
    }

    private async Task ProcessExpiredModels()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var premiumPackageService = scope.ServiceProvider.GetRequiredService<IPremiumPackageService>();

            _logger.LogInformation("Starting expired model cleanup check");
            await premiumPackageService.CleanupExpiredModelsAsync();
            _logger.LogInformation("Completed expired model cleanup check");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process expired models");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Model Expiration Background Service is stopping");
        return base.StopAsync(cancellationToken);
    }
}