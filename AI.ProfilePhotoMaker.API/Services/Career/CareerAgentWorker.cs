namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>
/// Polls for claimable agent runs and works them, one at a time (ADR 0009). Off when
/// <c>Career:Agent:WorkerEnabled</c> is false, and idle while <c>Features:CareerWorkspace</c> is off
/// (checked every pass, so a config reload applies without a restart). Logs ids and codes only.
/// </summary>
public sealed class CareerAgentWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly Microsoft.Extensions.Options.IOptions<CareerAgentOptions> _options;
    private readonly ILogger<CareerAgentWorker> _logger;
    private readonly ICareerFeatureGate _gate;
    private readonly string _workerId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public CareerAgentWorker(
        IServiceScopeFactory scopes, Microsoft.Extensions.Options.IOptions<CareerAgentOptions> options, ILogger<CareerAgentWorker> logger,
        ICareerFeatureGate gate)
    {
        _gate = gate;
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// One pass: claims and works at most one run. Does nothing (and resolves no runner or reference data)
    /// while the career flag is off, so queued runs are never processed or billed for a disabled feature.
    /// </summary>
    public async Task<bool> WorkOnceAsync(CancellationToken ct)
    {
        if (!_gate.IsEnabled)
        {
            return false;
        }
        // A scope per pass: each gets a fresh DbContext, so nothing stale carries over.
        using var scope = _scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICareerAgentRunner>().RunOnceAsync(_workerId, ct);
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
                worked = await WorkOnceAsync(stoppingToken);
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
