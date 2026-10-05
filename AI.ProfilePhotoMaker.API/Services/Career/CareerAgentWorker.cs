namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>
/// Polls for claimable agent runs and works them, one at a time (ADR 0009). Off when
/// <c>Career:Agent:WorkerEnabled</c> is false. Logs ids and codes only.
/// </summary>
public sealed class CareerAgentWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly Microsoft.Extensions.Options.IOptions<CareerAgentOptions> _options;
    private readonly ILogger<CareerAgentWorker> _logger;
    private readonly string _workerId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public CareerAgentWorker(
        IServiceScopeFactory scopes, Microsoft.Extensions.Options.IOptions<CareerAgentOptions> options, ILogger<CareerAgentWorker> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.WorkerEnabled)
        {
            _logger.LogInformation("Career agent worker is disabled");
            return;
        }

        var poll = TimeSpan.FromSeconds(Math.Max(1, _options.Value.PollSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            var worked = false;
            try
            {
                // A scope per iteration: each pass gets a fresh DbContext, so nothing stale carries over.
                using var scope = _scopes.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<ICareerAgentRunner>();
                worked = await runner.RunOnceAsync(_workerId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // The lease expires and the run is retried; one bad run must not stop the worker.
                _logger.LogError("Career agent worker iteration failed: {ExceptionType}", ex.GetType().Name);
            }

            if (!worked)
            {
                try
                {
                    await Task.Delay(poll, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
