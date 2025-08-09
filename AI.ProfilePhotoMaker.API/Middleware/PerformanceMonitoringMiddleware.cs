using AI.ProfilePhotoMaker.API.Services.Monitoring;
using System.Diagnostics;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Middleware;

/// <summary>
/// Middleware for comprehensive performance monitoring and request tracking
/// </summary>
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IPerformanceMonitoringService _performanceMonitoring;
    private readonly IApplicationInsightsService _applicationInsights;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;

    // Configuration
    private readonly HashSet<string> _excludedPaths;
    private readonly int _slowRequestThresholdMs;
    private readonly bool _enableDetailedLogging;

    public PerformanceMonitoringMiddleware(
        RequestDelegate next,
        IPerformanceMonitoringService performanceMonitoring,
        IApplicationInsightsService applicationInsights,
        ILogger<PerformanceMonitoringMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _performanceMonitoring = performanceMonitoring;
        _applicationInsights = applicationInsights;
        _logger = logger;

        // Load configuration
        _excludedPaths = configuration.GetSection("Monitoring:ExcludedPaths").Get<HashSet<string>>() ?? new HashSet<string>
        {
            "/health",
            "/metrics",
            "/swagger",
            "/favicon.ico"
        };

        _slowRequestThresholdMs = configuration.GetValue<int>("Monitoring:SlowRequestThresholdMs", 1000);
        _enableDetailedLogging = configuration.GetValue<bool>("Monitoring:EnableDetailedLogging", false);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip monitoring for excluded paths
        if (ShouldSkipMonitoring(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString();
        var endpoint = GetEndpointPath(context);
        var method = context.Request.Method;
        var userId = GetUserId(context);

        // Add correlation ID to response headers
        context.Response.Headers.Append("X-Correlation-ID", requestId);
        context.Items["CorrelationId"] = requestId;

        // Track request start
        var activity = _performanceMonitoring.StartOperation($"HTTP_{method}_{endpoint}", requestId);

        try
        {
            // Record memory usage before request
            if (_enableDetailedLogging)
            {
                _performanceMonitoring.RecordMemoryUsage();
            }

            await _next(context);

            stopwatch.Stop();
            var duration = stopwatch.ElapsedMilliseconds;
            var statusCode = context.Response.StatusCode;

            // Record API request metrics
            _performanceMonitoring.RecordApiRequest(endpoint, method, statusCode, duration, userId);

            // Complete operation tracking
            var success = statusCode < 400;
            _performanceMonitoring.CompleteOperation(activity, success, success ? null : $"HTTP {statusCode}");

            // Log slow requests
            if (duration > _slowRequestThresholdMs)
            {
                _logger.LogWarning("Slow request detected: {Method} {Endpoint} took {Duration}ms (User: {UserId})", 
                    method, endpoint, duration, userId ?? "anonymous");

                // Track as custom event
                _applicationInsights.TrackEvent("SlowRequest", new Dictionary<string, string>
                {
                    ["endpoint"] = endpoint,
                    ["method"] = method,
                    ["userId"] = userId ?? "anonymous",
                    ["correlationId"] = requestId
                }, new Dictionary<string, double>
                {
                    ["duration"] = duration,
                    ["threshold"] = _slowRequestThresholdMs
                });
            }

            // Record custom metrics for specific endpoints
            RecordEndpointSpecificMetrics(endpoint, method, duration, statusCode);

            if (_enableDetailedLogging)
            {
                _logger.LogInformation("Request completed: {Method} {Endpoint} -> {StatusCode} in {Duration}ms", 
                    method, endpoint, statusCode, duration);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var duration = stopwatch.ElapsedMilliseconds;

            // Record failed request
            _performanceMonitoring.RecordApiRequest(endpoint, method, 500, duration, userId);
            _performanceMonitoring.CompleteOperation(activity, false, ex.Message);

            // Track exception
            _applicationInsights.TrackException(ex, new Dictionary<string, string>
            {
                ["endpoint"] = endpoint,
                ["method"] = method,
                ["userId"] = userId ?? "anonymous",
                ["correlationId"] = requestId
            });

            _logger.LogError(ex, "Request failed: {Method} {Endpoint} after {Duration}ms", 
                method, endpoint, duration);

            throw;
        }
        finally
        {
            // Record memory usage after request for high-memory operations
            if (_enableDetailedLogging && (endpoint.Contains("upload") || endpoint.Contains("generate")))
            {
                _performanceMonitoring.RecordMemoryUsage();
            }
        }
    }

    private bool ShouldSkipMonitoring(PathString path)
    {
        var pathStr = path.Value?.ToLower() ?? "";
        return _excludedPaths.Any(excluded => pathStr.StartsWith(excluded.ToLower()));
    }

    private static string GetEndpointPath(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint != null)
        {
            var routeAttribute = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.RouteAttribute>();
            if (routeAttribute != null)
            {
                return routeAttribute.Template ?? context.Request.Path;
            }
        }

        return context.Request.Path;
    }

    private static string? GetUserId(HttpContext context)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            return user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? user.FindFirst(ClaimTypes.Name)?.Value
                ?? user.FindFirst("sub")?.Value;
        }

        return null;
    }

    private void RecordEndpointSpecificMetrics(string endpoint, string method, long duration, int statusCode)
    {
        var endpointLower = endpoint.ToLower();

        // Track image processing operations
        if (endpointLower.Contains("upload") || endpointLower.Contains("generate"))
        {
            _performanceMonitoring.RecordCustomMetric("ImageProcessingDuration", duration, new Dictionary<string, string>
            {
                ["endpoint"] = endpoint,
                ["method"] = method,
                ["success"] = (statusCode < 400).ToString()
            });
        }

        // Track authentication operations
        if (endpointLower.Contains("auth") || endpointLower.Contains("login") || endpointLower.Contains("token"))
        {
            _performanceMonitoring.RecordCustomMetric("AuthenticationDuration", duration, new Dictionary<string, string>
            {
                ["endpoint"] = endpoint,
                ["method"] = method,
                ["success"] = (statusCode < 400).ToString()
            });
        }

        // Track payment operations
        if (endpointLower.Contains("payment") || endpointLower.Contains("credit") || endpointLower.Contains("stripe"))
        {
            _performanceMonitoring.RecordCustomMetric("PaymentOperationDuration", duration, new Dictionary<string, string>
            {
                ["endpoint"] = endpoint,
                ["method"] = method,
                ["success"] = (statusCode < 400).ToString()
            });
        }

        // Track API health operations
        if (endpointLower.Contains("health") || endpointLower.Contains("status"))
        {
            _performanceMonitoring.RecordCustomMetric("HealthCheckDuration", duration, new Dictionary<string, string>
            {
                ["endpoint"] = endpoint,
                ["method"] = method
            });
        }
    }
}

/// <summary>
/// Extension methods for registering performance monitoring middleware
/// </summary>
public static class PerformanceMonitoringMiddlewareExtensions
{
    /// <summary>
    /// Add performance monitoring middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UsePerformanceMonitoring(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PerformanceMonitoringMiddleware>();
    }

    /// <summary>
    /// Add performance monitoring services to dependency injection
    /// </summary>
    public static IServiceCollection AddPerformanceMonitoring(this IServiceCollection services, IConfiguration configuration)
    {
        // Register monitoring services
        services.AddSingleton<IPerformanceMonitoringService, PerformanceMonitoringService>();
        services.AddSingleton<IApplicationInsightsService, ApplicationInsightsService>();

        // Add Application Insights
        services.AddApplicationInsightsTelemetry(configuration);

        // Configure Application Insights sampling (for high-traffic applications)
        services.Configure<Microsoft.ApplicationInsights.AspNetCore.Extensions.ApplicationInsightsServiceOptions>(options =>
        {
            options.EnableAdaptiveSampling = true;
            options.EnableQuickPulseMetricStream = true;
            options.EnableAuthenticationTrackingJavaScript = false;
            options.ConnectionString = configuration.GetConnectionString("ApplicationInsights");
        });

        // Add health checks integration
        services.AddHealthChecks()
            .AddCheck<MonitoringHealthCheck>("monitoring");

        return services;
    }
}

/// <summary>
/// Health check for monitoring services
/// </summary>
public class MonitoringHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly IPerformanceMonitoringService _performanceMonitoring;
    private readonly ILogger<MonitoringHealthCheck> _logger;

    public MonitoringHealthCheck(
        IPerformanceMonitoringService performanceMonitoring,
        ILogger<MonitoringHealthCheck> logger)
    {
        _performanceMonitoring = performanceMonitoring;
        _logger = logger;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if monitoring service can collect metrics
            var metrics = await _performanceMonitoring.GetCurrentMetricsAsync();
            
            // Check for critical alerts
            var alerts = await _performanceMonitoring.CheckPerformanceAlertsAsync();
            var criticalAlerts = alerts.Where(a => a.Severity == "Critical").ToList();

            if (criticalAlerts.Any())
            {
                var alertMessages = string.Join("; ", criticalAlerts.Select(a => a.Message));
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                    $"Critical performance alerts detected: {alertMessages}");
            }

            var data = new Dictionary<string, object>
            {
                ["totalRequests"] = metrics.ApiMetrics.TotalRequests,
                ["errorRate"] = metrics.ApiMetrics.ErrorRate,
                ["averageResponseTime"] = metrics.ApiMetrics.AverageResponseTime,
                ["memoryUsage"] = metrics.SystemMetrics.Memory.UsagePercentage,
                ["uptime"] = metrics.SystemMetrics.UptimeSeconds
            };

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                "Monitoring services are operational", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Monitoring health check failed");
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                "Monitoring services are not responding", ex);
        }
    }
}