using System.Diagnostics;
using System.Text.Json;

namespace AI.ProfilePhotoMaker.API.Middleware;

/// <summary>
/// Middleware for monitoring async I/O performance and identifying blocking operations
/// </summary>
public class AsyncIoPerformanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AsyncIoPerformanceMiddleware> _logger;
    private readonly AsyncIoPerformanceOptions _options;

    public AsyncIoPerformanceMiddleware(
        RequestDelegate next, 
        ILogger<AsyncIoPerformanceMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _options = new AsyncIoPerformanceOptions();
        configuration.GetSection("AsyncIoPerformance").Bind(_options);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only monitor relevant endpoints that handle file operations
        if (!ShouldMonitor(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTimeOffset.UtcNow;
        var threadId = Thread.CurrentThread.ManagedThreadId;
        var requestId = context.TraceIdentifier;

        // Track thread pool stats before request
        var threadPoolStatsBefore = GetThreadPoolStats();

        Exception? caughtException = null;
        
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            caughtException = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var endTime = DateTimeOffset.UtcNow;
            var duration = stopwatch.Elapsed;

            // Track thread pool stats after request
            var threadPoolStatsAfter = GetThreadPoolStats();

            // Log performance metrics
            await LogPerformanceMetricsAsync(
                context, 
                requestId, 
                threadId, 
                startTime, 
                endTime, 
                duration, 
                threadPoolStatsBefore, 
                threadPoolStatsAfter,
                caughtException);

            // Check for potential blocking operations
            if (duration > _options.SlowRequestThreshold)
            {
                await LogSlowRequestWarningAsync(context, requestId, duration, threadId);
            }

            // Check for thread pool exhaustion
            if (threadPoolStatsAfter.AvailableWorkerThreads < _options.LowThreadPoolThreshold)
            {
                await LogThreadPoolExhaustionWarningAsync(requestId, threadPoolStatsBefore, threadPoolStatsAfter);
            }
        }
    }

    private static bool ShouldMonitor(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant();
        if (pathValue == null) return false;

        return pathValue.Contains("/api/image") ||
               pathValue.Contains("/upload") ||
               pathValue.Contains("/download") ||
               pathValue.Contains("/zip") ||
               pathValue.Contains("/file");
    }

    private async Task LogPerformanceMetricsAsync(
        HttpContext context,
        string requestId,
        int threadId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        TimeSpan duration,
        ThreadPoolStats before,
        ThreadPoolStats after,
        Exception? exception)
    {
        var metrics = new
        {
            RequestId = requestId,
            Method = context.Request.Method,
            Path = context.Request.Path.Value,
            StatusCode = context.Response.StatusCode,
            ThreadId = threadId,
            StartTime = startTime,
            EndTime = endTime,
            DurationMs = duration.TotalMilliseconds,
            ThreadPoolBefore = before,
            ThreadPoolAfter = after,
            ThreadPoolDelta = new
            {
                WorkerThreadsUsed = before.AvailableWorkerThreads - after.AvailableWorkerThreads,
                CompletionPortsUsed = before.AvailableCompletionPortThreads - after.AvailableCompletionPortThreads
            },
            HasException = exception != null,
            ExceptionType = exception?.GetType().Name,
            UserAgent = context.Request.Headers.UserAgent.FirstOrDefault(),
            ContentLength = context.Request.ContentLength,
            ResponseSize = context.Response.ContentLength
        };

        if (_options.EnableDetailedLogging)
        {
            _logger.LogInformation("Async I/O Performance Metrics: {Metrics}", 
                JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = false }));
        }

        // Log to Application Insights or other monitoring systems if configured
        if (_options.EnableApplicationInsights)
        {
            // Add custom telemetry here if needed
            context.Items.Add("AsyncIoMetrics", metrics);
        }
    }

    private async Task LogSlowRequestWarningAsync(HttpContext context, string requestId, TimeSpan duration, int threadId)
    {
        _logger.LogWarning(
            "Potential blocking I/O detected - Request {RequestId} on thread {ThreadId} took {DurationMs}ms " +
            "to complete {Method} {Path}. This may indicate synchronous file operations blocking the request pipeline.",
            requestId,
            threadId,
            duration.TotalMilliseconds,
            context.Request.Method,
            context.Request.Path);

        // Emit custom metric for monitoring
        if (_options.EnableMetrics)
        {
            // Add custom metric emission here
        }
    }

    private async Task LogThreadPoolExhaustionWarningAsync(string requestId, ThreadPoolStats before, ThreadPoolStats after)
    {
        _logger.LogWarning(
            "Thread pool exhaustion detected during request {RequestId}. " +
            "Available worker threads: {BeforeWorkers} -> {AfterWorkers}, " +
            "Available completion port threads: {BeforeCompletionPorts} -> {AfterCompletionPorts}. " +
            "This may indicate blocking I/O operations consuming thread pool threads.",
            requestId,
            before.AvailableWorkerThreads,
            after.AvailableWorkerThreads,
            before.AvailableCompletionPortThreads,
            after.AvailableCompletionPortThreads);
    }

    private static ThreadPoolStats GetThreadPoolStats()
    {
        ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int availableCompletionPortThreads);
        ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);

        return new ThreadPoolStats
        {
            AvailableWorkerThreads = availableWorkerThreads,
            AvailableCompletionPortThreads = availableCompletionPortThreads,
            MaxWorkerThreads = maxWorkerThreads,
            MaxCompletionPortThreads = maxCompletionPortThreads,
            BusyWorkerThreads = maxWorkerThreads - availableWorkerThreads,
            BusyCompletionPortThreads = maxCompletionPortThreads - availableCompletionPortThreads
        };
    }
}

/// <summary>
/// Thread pool statistics snapshot
/// </summary>
public class ThreadPoolStats
{
    public int AvailableWorkerThreads { get; set; }
    public int AvailableCompletionPortThreads { get; set; }
    public int MaxWorkerThreads { get; set; }
    public int MaxCompletionPortThreads { get; set; }
    public int BusyWorkerThreads { get; set; }
    public int BusyCompletionPortThreads { get; set; }
}

/// <summary>
/// Configuration options for async I/O performance monitoring
/// </summary>
public class AsyncIoPerformanceOptions
{
    /// <summary>
    /// Threshold for considering a request as slow (potential blocking I/O)
    /// </summary>
    public TimeSpan SlowRequestThreshold { get; set; } = TimeSpan.FromMilliseconds(2000);

    /// <summary>
    /// Threshold for considering thread pool as low
    /// </summary>
    public int LowThreadPoolThreshold { get; set; } = 5;

    /// <summary>
    /// Enable detailed performance logging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Enable Application Insights integration
    /// </summary>
    public bool EnableApplicationInsights { get; set; } = false;

    /// <summary>
    /// Enable custom metrics emission
    /// </summary>
    public bool EnableMetrics { get; set; } = false;
}

/// <summary>
/// Extension methods for async I/O performance monitoring
/// </summary>
public static class AsyncIoPerformanceExtensions
{
    /// <summary>
    /// Adds async I/O performance monitoring middleware
    /// </summary>
    public static IApplicationBuilder UseAsyncIoPerformanceMonitoring(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AsyncIoPerformanceMiddleware>();
    }
}