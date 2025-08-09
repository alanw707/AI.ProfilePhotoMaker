using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace AI.ProfilePhotoMaker.API.Services.Monitoring;

/// <summary>
/// Azure Application Insights integration service
/// </summary>
public class ApplicationInsightsService : IApplicationInsightsService
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<ApplicationInsightsService> _logger;

    public ApplicationInsightsService(
        TelemetryClient telemetryClient,
        ILogger<ApplicationInsightsService> logger)
    {
        _telemetryClient = telemetryClient;
        _logger = logger;
    }

    public void TrackEvent(string eventName, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null)
    {
        try
        {
            _telemetryClient.TrackEvent(eventName, properties, metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track event: {EventName}", eventName);
        }
    }

    public void TrackDependency(string type, string target, string name, string data, DateTimeOffset startTime, TimeSpan duration, bool success)
    {
        try
        {
            _telemetryClient.TrackDependency(type, target, name, data, startTime, duration, success ? "200" : "500", success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track dependency: {Type} - {Name}", type, name);
        }
    }

    public void TrackException(Exception exception, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null)
    {
        try
        {
            _telemetryClient.TrackException(exception, properties, metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track exception");
        }
    }

    public void TrackMetric(string name, double value, Dictionary<string, string>? properties = null)
    {
        try
        {
            _telemetryClient.TrackMetric(name, value, properties);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track metric: {MetricName}", name);
        }
    }

    public void TrackRequest(string name, DateTimeOffset startTime, TimeSpan duration, string responseCode, bool success)
    {
        try
        {
            _telemetryClient.TrackRequest(name, startTime, duration, responseCode, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track request: {RequestName}", name);
        }
    }

    public void TrackTrace(string message, SeverityLevel level, Dictionary<string, string>? properties = null)
    {
        try
        {
            _telemetryClient.TrackTrace(message, level, properties);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track trace");
        }
    }

    public async Task FlushAsync()
    {
        try
        {
            _telemetryClient.Flush();
            // Wait a bit for telemetry to be sent
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush telemetry");
        }
    }
}