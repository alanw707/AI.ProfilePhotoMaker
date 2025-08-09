using AI.ProfilePhotoMaker.API.Models.DTOs;
using System.Diagnostics;

namespace AI.ProfilePhotoMaker.API.Services.Monitoring;

/// <summary>
/// Interface for comprehensive performance monitoring
/// </summary>
public interface IPerformanceMonitoringService
{
    /// <summary>
    /// Record API request metrics
    /// </summary>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="method">HTTP method</param>
    /// <param name="statusCode">Response status code</param>
    /// <param name="duration">Request duration in milliseconds</param>
    /// <param name="userId">Optional user ID for tracking</param>
    void RecordApiRequest(string endpoint, string method, int statusCode, long duration, string? userId = null);

    /// <summary>
    /// Record database query performance
    /// </summary>
    /// <param name="queryType">Type of query (Select, Insert, Update, Delete)</param>
    /// <param name="tableName">Target table name</param>
    /// <param name="duration">Query duration in milliseconds</param>
    /// <param name="recordsAffected">Number of records affected</param>
    void RecordDatabaseQuery(string queryType, string tableName, long duration, int recordsAffected = 0);

    /// <summary>
    /// Record external service call performance
    /// </summary>
    /// <param name="serviceName">External service name (Replicate, Stripe, Azure Storage)</param>
    /// <param name="operation">Operation performed</param>
    /// <param name="duration">Call duration in milliseconds</param>
    /// <param name="success">Whether the call was successful</param>
    /// <param name="errorMessage">Error message if unsuccessful</param>
    void RecordExternalServiceCall(string serviceName, string operation, long duration, bool success, string? errorMessage = null);

    /// <summary>
    /// Record memory usage metrics
    /// </summary>
    void RecordMemoryUsage();

    /// <summary>
    /// Get current performance metrics
    /// </summary>
    Task<PerformanceMetricsDto> GetCurrentMetricsAsync();

    /// <summary>
    /// Get performance metrics for a specific time range
    /// </summary>
    Task<PerformanceMetricsDto> GetMetricsAsync(DateTime from, DateTime to);

    /// <summary>
    /// Get system resource utilization
    /// </summary>
    Task<ResourceUtilizationDto> GetResourceUtilizationAsync();

    /// <summary>
    /// Start monitoring a long-running operation
    /// </summary>
    /// <param name="operationName">Name of the operation</param>
    /// <param name="correlationId">Correlation ID for tracking</param>
    /// <returns>Activity for correlation tracking</returns>
    Activity StartOperation(string operationName, string? correlationId = null);

    /// <summary>
    /// Complete monitoring a long-running operation
    /// </summary>
    /// <param name="activity">Activity started with StartOperation</param>
    /// <param name="success">Whether the operation was successful</param>
    /// <param name="errorMessage">Error message if unsuccessful</param>
    void CompleteOperation(Activity activity, bool success, string? errorMessage = null);

    /// <summary>
    /// Record custom metric
    /// </summary>
    /// <param name="name">Metric name</param>
    /// <param name="value">Metric value</param>
    /// <param name="tags">Optional tags for categorization</param>
    void RecordCustomMetric(string name, double value, Dictionary<string, string>? tags = null);

    /// <summary>
    /// Get error rate for specific endpoint
    /// </summary>
    Task<double> GetErrorRateAsync(string endpoint, TimeSpan timeWindow);

    /// <summary>
    /// Get average response time for specific endpoint
    /// </summary>
    Task<double> GetAverageResponseTimeAsync(string endpoint, TimeSpan timeWindow);

    /// <summary>
    /// Get throughput metrics (requests per second)
    /// </summary>
    Task<double> GetThroughputAsync(TimeSpan timeWindow);

    /// <summary>
    /// Check if any performance thresholds are exceeded
    /// </summary>
    Task<List<PerformanceAlertDto>> CheckPerformanceAlertsAsync();
}

/// <summary>
/// Interface for Azure Application Insights integration
/// </summary>
public interface IApplicationInsightsService
{
    /// <summary>
    /// Track custom event
    /// </summary>
    void TrackEvent(string eventName, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null);

    /// <summary>
    /// Track dependency call
    /// </summary>
    void TrackDependency(string type, string target, string name, string data, DateTimeOffset startTime, TimeSpan duration, bool success);

    /// <summary>
    /// Track exception
    /// </summary>
    void TrackException(Exception exception, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null);

    /// <summary>
    /// Track metric
    /// </summary>
    void TrackMetric(string name, double value, Dictionary<string, string>? properties = null);

    /// <summary>
    /// Track request
    /// </summary>
    void TrackRequest(string name, DateTimeOffset startTime, TimeSpan duration, string responseCode, bool success);

    /// <summary>
    /// Track trace/log message
    /// </summary>
    void TrackTrace(string message, Microsoft.ApplicationInsights.DataContracts.SeverityLevel level, Dictionary<string, string>? properties = null);

    /// <summary>
    /// Flush all telemetry data
    /// </summary>
    Task FlushAsync();
}

/// <summary>
/// Interface for monitoring middleware
/// </summary>
public interface IMonitoringMiddleware
{
    /// <summary>
    /// Process request through monitoring pipeline
    /// </summary>
    Task InvokeAsync(HttpContext context, RequestDelegate next);
}