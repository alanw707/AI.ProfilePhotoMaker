using Microsoft.Extensions.DependencyInjection;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>
/// Hourly purge of expired raw resumes (ADR 0007). Registered from Program.cs only,
/// so the test host never runs it.
/// </summary>
public sealed class CareerResumePurgeBackgroundService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<CareerResumePurgeBackgroundService> _logger;

    public CareerResumePurgeBackgroundService(
        IServiceScopeFactory scopes, TimeProvider clock, ILogger<CareerResumePurgeBackgroundService> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IResumeImportService>();
                await service.PurgeExpiredResumesAsync(_clock.GetUtcNow().UtcDateTime, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed run is retried next hour; one bad run must not stop the job.
                _logger.LogError(ex, "Career resume purge failed");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
