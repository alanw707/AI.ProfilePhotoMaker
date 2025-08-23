using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Controller for performance monitoring and metrics endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MonitoringController : ControllerBase
{
    private readonly IPerformanceMonitoringService _performanceMonitoring;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(
        IPerformanceMonitoringService performanceMonitoring,
        ILogger<MonitoringController> logger)
    {
        _performanceMonitoring = performanceMonitoring;
        _logger = logger;
    }

    /// <summary>
    /// Get current performance metrics
    /// </summary>
    /// <returns>Current performance metrics</returns>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(PerformanceMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PerformanceMetricsDto>> GetCurrentMetrics()
    {
        try
        {
            var metrics = await _performanceMonitoring.GetCurrentMetricsAsync();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve current metrics");
            return StatusCode(500, new { error = "Failed to retrieve metrics", details = ex.Message });
        }
    }

    /// <summary>
    /// Get performance metrics for a specific time range
    /// </summary>
    /// <param name="from">Start time (UTC)</param>
    /// <param name="to">End time (UTC)</param>
    /// <returns>Performance metrics for the specified time range</returns>
    [HttpGet("metrics/range")]
    [ProducesResponseType(typeof(PerformanceMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PerformanceMetricsDto>> GetMetricsRange(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        try
        {
            if (from >= to)
            {
                return BadRequest(new { error = "'from' must be earlier than 'to'" });
            }

            if ((to - from).TotalDays > 7)
            {
                return BadRequest(new { error = "Time range cannot exceed 7 days" });
            }

            var metrics = await _performanceMonitoring.GetMetricsAsync(from, to);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve metrics for range {From} to {To}", from, to);
            return StatusCode(500, new { error = "Failed to retrieve metrics", details = ex.Message });
        }
    }

    /// <summary>
    /// Get system resource utilization
    /// </summary>
    /// <returns>Current resource utilization information</returns>
    [HttpGet("resources")]
    [ProducesResponseType(typeof(ResourceUtilizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ResourceUtilizationDto>> GetResourceUtilization()
    {
        try
        {
            var utilization = await _performanceMonitoring.GetResourceUtilizationAsync();
            return Ok(utilization);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve resource utilization");
            return StatusCode(500, new { error = "Failed to retrieve resource utilization", details = ex.Message });
        }
    }

    /// <summary>
    /// Get performance alerts
    /// </summary>
    /// <returns>Current performance alerts</returns>
    [HttpGet("alerts")]
    [ProducesResponseType(typeof(List<PerformanceAlertDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<PerformanceAlertDto>>> GetPerformanceAlerts()
    {
        try
        {
            var alerts = await _performanceMonitoring.CheckPerformanceAlertsAsync();
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve performance alerts");
            return StatusCode(500, new { error = "Failed to retrieve alerts", details = ex.Message });
        }
    }

    /// <summary>
    /// Get error rate for a specific endpoint
    /// </summary>
    /// <param name="endpoint">Endpoint path</param>
    /// <param name="timeWindowMinutes">Time window in minutes (default: 60)</param>
    /// <returns>Error rate as percentage</returns>
    [HttpGet("error-rate")]
    [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<double>> GetErrorRate(
        [FromQuery] string endpoint,
        [FromQuery] int timeWindowMinutes = 60)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return BadRequest(new { error = "Endpoint parameter is required" });
            }

            if (timeWindowMinutes <= 0 || timeWindowMinutes > 1440) // Max 24 hours
            {
                return BadRequest(new { error = "Time window must be between 1 and 1440 minutes" });
            }

            var errorRate = await _performanceMonitoring.GetErrorRateAsync(endpoint, TimeSpan.FromMinutes(timeWindowMinutes));
            return Ok(errorRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve error rate for endpoint {Endpoint}", endpoint);
            return StatusCode(500, new { error = "Failed to retrieve error rate", details = ex.Message });
        }
    }

    /// <summary>
    /// Get average response time for a specific endpoint
    /// </summary>
    /// <param name="endpoint">Endpoint path</param>
    /// <param name="timeWindowMinutes">Time window in minutes (default: 60)</param>
    /// <returns>Average response time in milliseconds</returns>
    [HttpGet("response-time")]
    [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<double>> GetAverageResponseTime(
        [FromQuery] string endpoint,
        [FromQuery] int timeWindowMinutes = 60)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return BadRequest(new { error = "Endpoint parameter is required" });
            }

            if (timeWindowMinutes <= 0 || timeWindowMinutes > 1440)
            {
                return BadRequest(new { error = "Time window must be between 1 and 1440 minutes" });
            }

            var avgResponseTime = await _performanceMonitoring.GetAverageResponseTimeAsync(endpoint, TimeSpan.FromMinutes(timeWindowMinutes));
            return Ok(avgResponseTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve response time for endpoint {Endpoint}", endpoint);
            return StatusCode(500, new { error = "Failed to retrieve response time", details = ex.Message });
        }
    }

    /// <summary>
    /// Get system throughput (requests per second)
    /// </summary>
    /// <param name="timeWindowMinutes">Time window in minutes (default: 60)</param>
    /// <returns>Throughput in requests per second</returns>
    [HttpGet("throughput")]
    [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<double>> GetThroughput(
        [FromQuery] int timeWindowMinutes = 60)
    {
        try
        {
            if (timeWindowMinutes <= 0 || timeWindowMinutes > 1440)
            {
                return BadRequest(new { error = "Time window must be between 1 and 1440 minutes" });
            }

            var throughput = await _performanceMonitoring.GetThroughputAsync(TimeSpan.FromMinutes(timeWindowMinutes));
            return Ok(throughput);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve throughput");
            return StatusCode(500, new { error = "Failed to retrieve throughput", details = ex.Message });
        }
    }

    /// <summary>
    /// Record a custom metric (for testing or manual tracking)
    /// </summary>
    /// <param name="request">Custom metric request</param>
    /// <returns>Success confirmation</returns>
    [HttpPost("custom-metric")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult RecordCustomMetric([FromBody] CustomMetricRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { error = "Metric name is required" });
            }

            _performanceMonitoring.RecordCustomMetric(request.Name, request.Value, request.Tags);
            return Ok(new { message = "Custom metric recorded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record custom metric {MetricName}", request.Name);
            return StatusCode(500, new { error = "Failed to record metric", details = ex.Message });
        }
    }

    /// <summary>
    /// Trigger memory usage recording (for manual monitoring)
    /// </summary>
    /// <returns>Success confirmation</returns>
    [HttpPost("record-memory")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult RecordMemoryUsage()
    {
        try
        {
            _performanceMonitoring.RecordMemoryUsage();
            return Ok(new { message = "Memory usage recorded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record memory usage");
            return StatusCode(500, new { error = "Failed to record memory usage", details = ex.Message });
        }
    }

    /// <summary>
    /// Get monitoring service health status
    /// </summary>
    /// <returns>Health status of monitoring services</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetMonitoringHealth()
    {
        try
        {
            var healthData = new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow,
                services = new
                {
                    performanceMonitoring = "Healthy",
                    applicationInsights = "Healthy"
                },
                version = GetType().Assembly.GetName().Version?.ToString() ?? "Unknown"
            };

            // Basic health check by attempting to get current metrics
            var metrics = await _performanceMonitoring.GetCurrentMetricsAsync();

            return Ok(healthData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Monitoring health check failed");
            return StatusCode(500, new
            {
                status = "Unhealthy",
                timestamp = DateTime.UtcNow,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get monitoring configuration and settings
    /// </summary>
    /// <returns>Current monitoring configuration</returns>
    [HttpGet("config")]
    [Authorize] // Require authentication for configuration endpoints
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult GetMonitoringConfiguration()
    {
        try
        {
            var config = new
            {
                settings = new
                {
                    metricsRetentionHours = 24,
                    slowRequestThresholdMs = 1000,
                    errorRateAlertThreshold = 5.0,
                    memoryUsageAlertThreshold = 85.0,
                    cpuUsageAlertThreshold = 80.0
                },
                features = new
                {
                    applicationInsightsEnabled = true,
                    performanceMonitoringEnabled = true,
                    healthChecksEnabled = true,
                    customMetricsEnabled = true
                },
                endpoints = new
                {
                    currentMetrics = "/api/monitoring/metrics",
                    rangeMetrics = "/api/monitoring/metrics/range",
                    resources = "/api/monitoring/resources",
                    alerts = "/api/monitoring/alerts",
                    health = "/api/monitoring/health"
                }
            };

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve monitoring configuration");
            return StatusCode(500, new { error = "Failed to retrieve configuration", details = ex.Message });
        }
    }
}

/// <summary>
/// Request model for recording custom metrics
/// </summary>
public class CustomMetricRequest
{
    /// <summary>
    /// Metric name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Metric value
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Optional tags for categorization
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }
}