using System.Text.Json.Serialization;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

/// <summary>
/// Performance metrics response DTO
/// </summary>
public class PerformanceMetricsDto
{
    /// <summary>
    /// Timestamp when metrics were collected
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Time range for the metrics
    /// </summary>
    [JsonPropertyName("timeRange")]
    public TimeRangeDto TimeRange { get; set; } = new();

    /// <summary>
    /// API request metrics
    /// </summary>
    [JsonPropertyName("apiMetrics")]
    public ApiMetricsDto ApiMetrics { get; set; } = new();

    /// <summary>
    /// Database performance metrics
    /// </summary>
    [JsonPropertyName("databaseMetrics")]
    public DatabaseMetricsDto DatabaseMetrics { get; set; } = new();

    /// <summary>
    /// External service metrics
    /// </summary>
    [JsonPropertyName("externalServicesMetrics")]
    public ExternalServicesMetricsDto ExternalServicesMetrics { get; set; } = new();

    /// <summary>
    /// System resource metrics
    /// </summary>
    [JsonPropertyName("systemMetrics")]
    public SystemMetricsDto SystemMetrics { get; set; } = new();

    /// <summary>
    /// Custom business metrics
    /// </summary>
    [JsonPropertyName("customMetrics")]
    public Dictionary<string, double> CustomMetrics { get; set; } = new();

    /// <summary>
    /// Performance alerts
    /// </summary>
    [JsonPropertyName("alerts")]
    public List<PerformanceAlertDto> Alerts { get; set; } = new();
}

/// <summary>
/// Time range information
/// </summary>
public class TimeRangeDto
{
    /// <summary>
    /// Start time of the range
    /// </summary>
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    /// <summary>
    /// End time of the range
    /// </summary>
    [JsonPropertyName("end")]
    public DateTime End { get; set; }

    /// <summary>
    /// Duration in seconds
    /// </summary>
    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds => (End - Start).TotalSeconds;
}

/// <summary>
/// API request metrics
/// </summary>
public class ApiMetricsDto
{
    /// <summary>
    /// Total number of requests
    /// </summary>
    [JsonPropertyName("totalRequests")]
    public long TotalRequests { get; set; }

    /// <summary>
    /// Successful requests (2xx status codes)
    /// </summary>
    [JsonPropertyName("successfulRequests")]
    public long SuccessfulRequests { get; set; }

    /// <summary>
    /// Failed requests (4xx, 5xx status codes)
    /// </summary>
    [JsonPropertyName("failedRequests")]
    public long FailedRequests { get; set; }

    /// <summary>
    /// Error rate as percentage (0-100)
    /// </summary>
    [JsonPropertyName("errorRate")]
    public double ErrorRate => TotalRequests > 0 ? (failedRequests * 100.0 / TotalRequests) : 0;

    private long failedRequests => FailedRequests;

    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    [JsonPropertyName("averageResponseTime")]
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// 50th percentile response time
    /// </summary>
    [JsonPropertyName("p50ResponseTime")]
    public double P50ResponseTime { get; set; }

    /// <summary>
    /// 95th percentile response time
    /// </summary>
    [JsonPropertyName("p95ResponseTime")]
    public double P95ResponseTime { get; set; }

    /// <summary>
    /// 99th percentile response time
    /// </summary>
    [JsonPropertyName("p99ResponseTime")]
    public double P99ResponseTime { get; set; }

    /// <summary>
    /// Requests per second
    /// </summary>
    [JsonPropertyName("requestsPerSecond")]
    public double RequestsPerSecond { get; set; }

    /// <summary>
    /// Slowest endpoints
    /// </summary>
    [JsonPropertyName("slowestEndpoints")]
    public List<EndpointMetricDto> SlowestEndpoints { get; set; } = new();

    /// <summary>
    /// Most active endpoints by request count
    /// </summary>
    [JsonPropertyName("mostActiveEndpoints")]
    public List<EndpointMetricDto> MostActiveEndpoints { get; set; } = new();

    /// <summary>
    /// Error breakdown by status code
    /// </summary>
    [JsonPropertyName("errorBreakdown")]
    public Dictionary<int, long> ErrorBreakdown { get; set; } = new();
}

/// <summary>
/// Individual endpoint metrics
/// </summary>
public class EndpointMetricDto
{
    /// <summary>
    /// Endpoint path
    /// </summary>
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Request count
    /// </summary>
    [JsonPropertyName("requestCount")]
    public long RequestCount { get; set; }

    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    [JsonPropertyName("averageResponseTime")]
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Error rate as percentage
    /// </summary>
    [JsonPropertyName("errorRate")]
    public double ErrorRate { get; set; }

    /// <summary>
    /// Last request timestamp
    /// </summary>
    [JsonPropertyName("lastRequest")]
    public DateTime LastRequest { get; set; }
}

/// <summary>
/// Database performance metrics
/// </summary>
public class DatabaseMetricsDto
{
    /// <summary>
    /// Total number of queries
    /// </summary>
    [JsonPropertyName("totalQueries")]
    public long TotalQueries { get; set; }

    /// <summary>
    /// Average query time in milliseconds
    /// </summary>
    [JsonPropertyName("averageQueryTime")]
    public double AverageQueryTime { get; set; }

    /// <summary>
    /// Slowest queries
    /// </summary>
    [JsonPropertyName("slowestQueries")]
    public List<DatabaseQueryMetricDto> SlowestQueries { get; set; } = new();

    /// <summary>
    /// Query breakdown by type (SELECT, INSERT, UPDATE, DELETE)
    /// </summary>
    [JsonPropertyName("queryBreakdown")]
    public Dictionary<string, QueryTypeMetricDto> QueryBreakdown { get; set; } = new();

    /// <summary>
    /// Connection pool metrics
    /// </summary>
    [JsonPropertyName("connectionPool")]
    public ConnectionPoolMetricsDto ConnectionPool { get; set; } = new();

    /// <summary>
    /// Database health status
    /// </summary>
    [JsonPropertyName("healthStatus")]
    public string HealthStatus { get; set; } = "Healthy";
}

/// <summary>
/// Database query metrics
/// </summary>
public class DatabaseQueryMetricDto
{
    /// <summary>
    /// Query type (SELECT, INSERT, UPDATE, DELETE)
    /// </summary>
    [JsonPropertyName("queryType")]
    public string QueryType { get; set; } = string.Empty;

    /// <summary>
    /// Table name
    /// </summary>
    [JsonPropertyName("tableName")]
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Average execution time in milliseconds
    /// </summary>
    [JsonPropertyName("averageExecutionTime")]
    public double AverageExecutionTime { get; set; }

    /// <summary>
    /// Maximum execution time in milliseconds
    /// </summary>
    [JsonPropertyName("maxExecutionTime")]
    public double MaxExecutionTime { get; set; }

    /// <summary>
    /// Number of executions
    /// </summary>
    [JsonPropertyName("executionCount")]
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Last execution timestamp
    /// </summary>
    [JsonPropertyName("lastExecution")]
    public DateTime LastExecution { get; set; }
}

/// <summary>
/// Query type metrics
/// </summary>
public class QueryTypeMetricDto
{
    /// <summary>
    /// Query count
    /// </summary>
    [JsonPropertyName("count")]
    public long Count { get; set; }

    /// <summary>
    /// Average execution time
    /// </summary>
    [JsonPropertyName("averageTime")]
    public double AverageTime { get; set; }

    /// <summary>
    /// Total execution time
    /// </summary>
    [JsonPropertyName("totalTime")]
    public double TotalTime { get; set; }
}

/// <summary>
/// Connection pool metrics
/// </summary>
public class ConnectionPoolMetricsDto
{
    /// <summary>
    /// Active connections
    /// </summary>
    [JsonPropertyName("activeConnections")]
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Idle connections
    /// </summary>
    [JsonPropertyName("idleConnections")]
    public int IdleConnections { get; set; }

    /// <summary>
    /// Maximum pool size
    /// </summary>
    [JsonPropertyName("maxPoolSize")]
    public int MaxPoolSize { get; set; }

    /// <summary>
    /// Pool utilization percentage
    /// </summary>
    [JsonPropertyName("poolUtilization")]
    public double PoolUtilization => MaxPoolSize > 0 ? (ActiveConnections * 100.0 / MaxPoolSize) : 0;
}

/// <summary>
/// External services metrics
/// </summary>
public class ExternalServicesMetricsDto
{
    /// <summary>
    /// Individual service metrics
    /// </summary>
    [JsonPropertyName("services")]
    public Dictionary<string, ExternalServiceMetricDto> Services { get; set; } = new();

    /// <summary>
    /// Overall external services health
    /// </summary>
    [JsonPropertyName("overallHealth")]
    public string OverallHealth { get; set; } = "Healthy";
}

/// <summary>
/// External service metrics
/// </summary>
public class ExternalServiceMetricDto
{
    /// <summary>
    /// Service name
    /// </summary>
    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Total calls made
    /// </summary>
    [JsonPropertyName("totalCalls")]
    public long TotalCalls { get; set; }

    /// <summary>
    /// Successful calls
    /// </summary>
    [JsonPropertyName("successfulCalls")]
    public long SuccessfulCalls { get; set; }

    /// <summary>
    /// Failed calls
    /// </summary>
    [JsonPropertyName("failedCalls")]
    public long FailedCalls { get; set; }

    /// <summary>
    /// Success rate as percentage
    /// </summary>
    [JsonPropertyName("successRate")]
    public double SuccessRate => TotalCalls > 0 ? (SuccessfulCalls * 100.0 / TotalCalls) : 0;

    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    [JsonPropertyName("averageResponseTime")]
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Service availability status
    /// </summary>
    [JsonPropertyName("availability")]
    public string Availability { get; set; } = "Available";

    /// <summary>
    /// Last successful call timestamp
    /// </summary>
    [JsonPropertyName("lastSuccessfulCall")]
    public DateTime? LastSuccessfulCall { get; set; }

    /// <summary>
    /// Common error messages
    /// </summary>
    [JsonPropertyName("commonErrors")]
    public List<string> CommonErrors { get; set; } = new();
}

/// <summary>
/// System resource metrics
/// </summary>
public class SystemMetricsDto
{
    /// <summary>
    /// Memory usage metrics
    /// </summary>
    [JsonPropertyName("memory")]
    public MemoryMetricsDto Memory { get; set; } = new();

    /// <summary>
    /// CPU usage metrics
    /// </summary>
    [JsonPropertyName("cpu")]
    public CpuMetricsDto Cpu { get; set; } = new();

    /// <summary>
    /// Garbage collection metrics
    /// </summary>
    [JsonPropertyName("garbageCollection")]
    public GarbageCollectionMetricsDto GarbageCollection { get; set; } = new();

    /// <summary>
    /// Thread pool metrics
    /// </summary>
    [JsonPropertyName("threadPool")]
    public ThreadPoolMetricsDto ThreadPool { get; set; } = new();

    /// <summary>
    /// Application uptime in seconds
    /// </summary>
    [JsonPropertyName("uptimeSeconds")]
    public double UptimeSeconds { get; set; }

    /// <summary>
    /// Process start time
    /// </summary>
    [JsonPropertyName("processStartTime")]
    public DateTime ProcessStartTime { get; set; }
}

/// <summary>
/// Memory usage metrics
/// </summary>
public class MemoryMetricsDto
{
    /// <summary>
    /// Working set in bytes
    /// </summary>
    [JsonPropertyName("workingSet")]
    public long WorkingSet { get; set; }

    /// <summary>
    /// Private memory in bytes
    /// </summary>
    [JsonPropertyName("privateMemory")]
    public long PrivateMemory { get; set; }

    /// <summary>
    /// Managed memory in bytes
    /// </summary>
    [JsonPropertyName("managedMemory")]
    public long ManagedMemory { get; set; }

    /// <summary>
    /// Peak working set in bytes
    /// </summary>
    [JsonPropertyName("peakWorkingSet")]
    public long PeakWorkingSet { get; set; }

    /// <summary>
    /// Memory usage percentage (0-100)
    /// </summary>
    [JsonPropertyName("usagePercentage")]
    public double UsagePercentage { get; set; }

    /// <summary>
    /// Available memory in bytes
    /// </summary>
    [JsonPropertyName("availableMemory")]
    public long AvailableMemory { get; set; }
}

/// <summary>
/// CPU usage metrics
/// </summary>
public class CpuMetricsDto
{
    /// <summary>
    /// CPU usage percentage (0-100)
    /// </summary>
    [JsonPropertyName("usagePercentage")]
    public double UsagePercentage { get; set; }

    /// <summary>
    /// Total processor time in milliseconds
    /// </summary>
    [JsonPropertyName("totalProcessorTime")]
    public double TotalProcessorTime { get; set; }

    /// <summary>
    /// User processor time in milliseconds
    /// </summary>
    [JsonPropertyName("userProcessorTime")]
    public double UserProcessorTime { get; set; }

    /// <summary>
    /// Privileged processor time in milliseconds
    /// </summary>
    [JsonPropertyName("privilegedProcessorTime")]
    public double PrivilegedProcessorTime { get; set; }
}

/// <summary>
/// Garbage collection metrics
/// </summary>
public class GarbageCollectionMetricsDto
{
    /// <summary>
    /// Generation 0 collections
    /// </summary>
    [JsonPropertyName("gen0Collections")]
    public long Gen0Collections { get; set; }

    /// <summary>
    /// Generation 1 collections
    /// </summary>
    [JsonPropertyName("gen1Collections")]
    public long Gen1Collections { get; set; }

    /// <summary>
    /// Generation 2 collections
    /// </summary>
    [JsonPropertyName("gen2Collections")]
    public long Gen2Collections { get; set; }

    /// <summary>
    /// Total memory allocated in bytes
    /// </summary>
    [JsonPropertyName("totalMemoryAllocated")]
    public long TotalMemoryAllocated { get; set; }

    /// <summary>
    /// Time spent in GC as percentage
    /// </summary>
    [JsonPropertyName("timeInGcPercentage")]
    public double TimeInGcPercentage { get; set; }
}

/// <summary>
/// Thread pool metrics
/// </summary>
public class ThreadPoolMetricsDto
{
    /// <summary>
    /// Worker threads count
    /// </summary>
    [JsonPropertyName("workerThreads")]
    public int WorkerThreads { get; set; }

    /// <summary>
    /// Completion port threads count
    /// </summary>
    [JsonPropertyName("completionPortThreads")]
    public int CompletionPortThreads { get; set; }

    /// <summary>
    /// Maximum worker threads
    /// </summary>
    [JsonPropertyName("maxWorkerThreads")]
    public int MaxWorkerThreads { get; set; }

    /// <summary>
    /// Maximum completion port threads
    /// </summary>
    [JsonPropertyName("maxCompletionPortThreads")]
    public int MaxCompletionPortThreads { get; set; }

    /// <summary>
    /// Thread pool utilization percentage
    /// </summary>
    [JsonPropertyName("utilizationPercentage")]
    public double UtilizationPercentage => MaxWorkerThreads > 0 ? (WorkerThreads * 100.0 / MaxWorkerThreads) : 0;
}

/// <summary>
/// Resource utilization information
/// </summary>
public class ResourceUtilizationDto
{
    /// <summary>
    /// Timestamp when utilization was measured
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Overall system health status
    /// </summary>
    [JsonPropertyName("healthStatus")]
    public string HealthStatus { get; set; } = "Healthy";

    /// <summary>
    /// Resource utilization metrics
    /// </summary>
    [JsonPropertyName("resources")]
    public Dictionary<string, ResourceMetricDto> Resources { get; set; } = new();

    /// <summary>
    /// Performance warnings
    /// </summary>
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Critical issues
    /// </summary>
    [JsonPropertyName("criticalIssues")]
    public List<string> CriticalIssues { get; set; } = new();
}

/// <summary>
/// Individual resource metric
/// </summary>
public class ResourceMetricDto
{
    /// <summary>
    /// Resource name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current value
    /// </summary>
    [JsonPropertyName("currentValue")]
    public double CurrentValue { get; set; }

    /// <summary>
    /// Maximum value
    /// </summary>
    [JsonPropertyName("maxValue")]
    public double MaxValue { get; set; }

    /// <summary>
    /// Utilization percentage
    /// </summary>
    [JsonPropertyName("utilizationPercentage")]
    public double UtilizationPercentage => MaxValue > 0 ? (CurrentValue * 100.0 / MaxValue) : 0;

    /// <summary>
    /// Status (Normal, Warning, Critical)
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Normal";

    /// <summary>
    /// Unit of measurement
    /// </summary>
    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;
}

/// <summary>
/// Performance alert information
/// </summary>
public class PerformanceAlertDto
{
    /// <summary>
    /// Alert severity (Low, Medium, High, Critical)
    /// </summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Low";

    /// <summary>
    /// Alert category (Performance, Resources, Dependencies, Custom)
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = "Performance";

    /// <summary>
    /// Alert message
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Metric name that triggered the alert
    /// </summary>
    [JsonPropertyName("metricName")]
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// Current value of the metric
    /// </summary>
    [JsonPropertyName("currentValue")]
    public double CurrentValue { get; set; }

    /// <summary>
    /// Threshold value that was exceeded
    /// </summary>
    [JsonPropertyName("threshold")]
    public double Threshold { get; set; }

    /// <summary>
    /// Timestamp when alert was triggered
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Recommended actions
    /// </summary>
    [JsonPropertyName("recommendedActions")]
    public List<string> RecommendedActions { get; set; } = new();

    /// <summary>
    /// Additional context information
    /// </summary>
    [JsonPropertyName("context")]
    public Dictionary<string, object> Context { get; set; } = new();
}