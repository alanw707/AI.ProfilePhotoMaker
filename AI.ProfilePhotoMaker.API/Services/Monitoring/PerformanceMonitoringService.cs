using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Monitoring;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AI.ProfilePhotoMaker.API.Services.Monitoring;

/// <summary>
/// Comprehensive performance monitoring service with Azure Application Insights integration
/// </summary>
public class PerformanceMonitoringService : IPerformanceMonitoringService
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<PerformanceMonitoringService> _logger;
    private readonly Process _currentProcess;
    private readonly DateTime _processStartTime;

    // In-memory storage for metrics (for immediate access)
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ApiRequestMetric>> _apiMetrics = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DatabaseQueryMetric>> _databaseMetrics = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ExternalServiceMetric>> _externalServiceMetrics = new();
    private readonly ConcurrentQueue<CustomMetric> _customMetrics = new();

    // Performance counters
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;

    public PerformanceMonitoringService(
        TelemetryClient telemetryClient,
        ILogger<PerformanceMonitoringService> logger)
    {
        _telemetryClient = telemetryClient;
        _logger = logger;
        _currentProcess = Process.GetCurrentProcess();
        _processStartTime = _currentProcess.StartTime;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not initialize performance counters");
        }
    }

    public void RecordApiRequest(string endpoint, string method, int statusCode, long duration, string? userId = null)
    {
        var metric = new ApiRequestMetric
        {
            Endpoint = endpoint,
            Method = method,
            StatusCode = statusCode,
            Duration = duration,
            UserId = userId,
            Timestamp = DateTime.UtcNow
        };

        var key = $"{method}:{endpoint}";
        _apiMetrics.AddOrUpdate(key, new ConcurrentQueue<ApiRequestMetric>([metric]),
            (_, queue) =>
            {
                queue.Enqueue(metric);
                // Keep only last 1000 entries per endpoint
                while (queue.Count > 1000)
                {
                    ApiRequestMetric? _discard;
                    queue.TryDequeue(out _discard);
                }
                return queue;
            });

        // Track in Application Insights
        var properties = new Dictionary<string, string>
        {
            ["endpoint"] = endpoint,
            ["method"] = method,
            ["statusCode"] = statusCode.ToString(),
            ["userId"] = userId ?? "anonymous"
        };

        var telemetryMetrics = new Dictionary<string, double>
        {
            ["duration"] = duration
        };

        _telemetryClient.TrackEvent("ApiRequest", properties, telemetryMetrics);
        _telemetryClient.TrackMetric("ApiRequestDuration", duration, properties);

        // Track successful/failed requests
        if (statusCode >= 200 && statusCode < 400)
        {
            _telemetryClient.TrackMetric("ApiRequestSuccess", 1, properties);
        }
        else
        {
            _telemetryClient.TrackMetric("ApiRequestFailure", 1, properties);
        }
    }

    public void RecordDatabaseQuery(string queryType, string tableName, long duration, int recordsAffected = 0)
    {
        var metric = new DatabaseQueryMetric
        {
            QueryType = queryType,
            TableName = tableName,
            Duration = duration,
            RecordsAffected = recordsAffected,
            Timestamp = DateTime.UtcNow
        };

        var key = $"{queryType}:{tableName}";
        _databaseMetrics.AddOrUpdate(key, new ConcurrentQueue<DatabaseQueryMetric>([metric]),
            (_, queue) =>
            {
                queue.Enqueue(metric);
                // Keep only last 500 entries per query type/table
                while (queue.Count > 500)
                {
                    DatabaseQueryMetric? _discard;
                    queue.TryDequeue(out _discard);
                }
                return queue;
            });

        // Track in Application Insights
        var properties = new Dictionary<string, string>
        {
            ["queryType"] = queryType,
            ["tableName"] = tableName,
            ["recordsAffected"] = recordsAffected.ToString()
        };

        _telemetryClient.TrackDependency("Database", tableName, queryType,
            $"{queryType} {tableName}", DateTime.UtcNow.AddMilliseconds(-duration),
            TimeSpan.FromMilliseconds(duration), "200", true);

        _telemetryClient.TrackMetric("DatabaseQueryDuration", duration, properties);
    }

    public void RecordExternalServiceCall(string serviceName, string operation, long duration, bool success, string? errorMessage = null)
    {
        var metric = new ExternalServiceMetric
        {
            ServiceName = serviceName,
            Operation = operation,
            Duration = duration,
            Success = success,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        };

        _externalServiceMetrics.AddOrUpdate(serviceName, new ConcurrentQueue<ExternalServiceMetric>([metric]),
            (_, queue) =>
            {
                queue.Enqueue(metric);
                // Keep only last 200 entries per service
                while (queue.Count > 200)
                {
                    ExternalServiceMetric? _discard;
                    queue.TryDequeue(out _discard);
                }
                return queue;
            });

        // Track in Application Insights
        var properties = new Dictionary<string, string>
        {
            ["serviceName"] = serviceName,
            ["operation"] = operation,
            ["success"] = success.ToString(),
            ["errorMessage"] = errorMessage ?? ""
        };

        _telemetryClient.TrackDependency(serviceName, serviceName, operation,
            operation, DateTime.UtcNow.AddMilliseconds(-duration),
            TimeSpan.FromMilliseconds(duration), success ? "200" : "500", success);

        _telemetryClient.TrackMetric("ExternalServiceDuration", duration, properties);

        if (!success && !string.IsNullOrEmpty(errorMessage))
        {
            _telemetryClient.TrackException(new Exception($"{serviceName} error: {errorMessage}"), properties);
        }
    }

    public void RecordMemoryUsage()
    {
        try
        {
            _currentProcess.Refresh();
            var workingSet = _currentProcess.WorkingSet64;
            var privateMemory = _currentProcess.PrivateMemorySize64;
            var managedMemory = GC.GetTotalMemory(false);

            var properties = new Dictionary<string, string>
            {
                ["memoryType"] = "system"
            };

            _telemetryClient.TrackMetric("WorkingSetMemory", workingSet, properties);
            _telemetryClient.TrackMetric("PrivateMemory", privateMemory, properties);
            _telemetryClient.TrackMetric("ManagedMemory", managedMemory, properties);

            // Record GC information
            for (int i = 0; i <= 2; i++)
            {
                var collections = GC.CollectionCount(i);
                _telemetryClient.TrackMetric($"Gen{i}Collections", collections, properties);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record memory usage");
        }
    }

    public async Task<PerformanceMetricsDto> GetCurrentMetricsAsync()
    {
        var now = DateTime.UtcNow;
        var oneHourAgo = now.AddHours(-1);
        return await GetMetricsAsync(oneHourAgo, now);
    }

    public async Task<PerformanceMetricsDto> GetMetricsAsync(DateTime from, DateTime to)
    {
        var metrics = new PerformanceMetricsDto
        {
            Timestamp = DateTime.UtcNow,
            TimeRange = new TimeRangeDto { Start = from, End = to }
        };

        // Collect API metrics
        await PopulateApiMetricsAsync(metrics.ApiMetrics, from, to);

        // Collect database metrics
        await PopulateDatabaseMetricsAsync(metrics.DatabaseMetrics, from, to);

        // Collect external service metrics
        await PopulateExternalServiceMetricsAsync(metrics.ExternalServicesMetrics, from, to);

        // Collect system metrics
        await PopulateSystemMetricsAsync(metrics.SystemMetrics);

        // Collect custom metrics
        PopulateCustomMetrics(metrics.CustomMetrics, from, to);

        // Check for performance alerts
        metrics.Alerts = await CheckPerformanceAlertsAsync();

        return metrics;
    }

    public Task<ResourceUtilizationDto> GetResourceUtilizationAsync()
    {
        var utilization = new ResourceUtilizationDto
        {
            Timestamp = DateTime.UtcNow
        };

        try
        {
            _currentProcess.Refresh();

            // Memory metrics
            var workingSet = _currentProcess.WorkingSet64;
            var privateMemory = _currentProcess.PrivateMemorySize64;
            var managedMemory = GC.GetTotalMemory(false);

            utilization.Resources["WorkingSetMemory"] = new ResourceMetricDto
            {
                Name = "Working Set Memory",
                CurrentValue = workingSet,
                MaxValue = GetTotalPhysicalMemory(),
                Status = GetResourceStatus(workingSet, GetTotalPhysicalMemory(), 0.8, 0.9),
                Unit = "bytes"
            };

            utilization.Resources["PrivateMemory"] = new ResourceMetricDto
            {
                Name = "Private Memory",
                CurrentValue = privateMemory,
                MaxValue = GetTotalPhysicalMemory(),
                Status = GetResourceStatus(privateMemory, GetTotalPhysicalMemory(), 0.7, 0.85),
                Unit = "bytes"
            };

            utilization.Resources["ManagedMemory"] = new ResourceMetricDto
            {
                Name = "Managed Memory",
                CurrentValue = managedMemory,
                MaxValue = workingSet, // Managed memory is subset of working set
                Status = GetResourceStatus(managedMemory, workingSet, 0.6, 0.8),
                Unit = "bytes"
            };

            // CPU metrics
            if (_cpuCounter != null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var cpuUsage = _cpuCounter.NextValue();
                utilization.Resources["CpuUsage"] = new ResourceMetricDto
                {
                    Name = "CPU Usage",
                    CurrentValue = cpuUsage,
                    MaxValue = 100,
                    Status = GetResourceStatus(cpuUsage, 100, 70, 85),
                    Unit = "percentage"
                };
            }

            // Thread pool metrics
            ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);

            var activeWorkerThreads = maxWorkerThreads - workerThreads;
            utilization.Resources["ThreadPoolWorkers"] = new ResourceMetricDto
            {
                Name = "Thread Pool Workers",
                CurrentValue = activeWorkerThreads,
                MaxValue = maxWorkerThreads,
                Status = GetResourceStatus(activeWorkerThreads, maxWorkerThreads, 0.7, 0.9),
                Unit = "threads"
            };

            // Determine overall health status
            var criticalCount = utilization.Resources.Values.Count(r => r.Status == "Critical");
            var warningCount = utilization.Resources.Values.Count(r => r.Status == "Warning");

            if (criticalCount > 0)
            {
                utilization.HealthStatus = "Critical";
                utilization.CriticalIssues.Add($"{criticalCount} critical resource(s) detected");
            }
            else if (warningCount > 0)
            {
                utilization.HealthStatus = "Warning";
                utilization.Warnings.Add($"{warningCount} resource(s) showing high utilization");
            }
            else
            {
                utilization.HealthStatus = "Healthy";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resource utilization");
            utilization.HealthStatus = "Unknown";
            utilization.CriticalIssues.Add($"Failed to collect resource metrics: {ex.Message}");
        }

        return Task.FromResult(utilization);
    }

    public Activity StartOperation(string operationName, string? correlationId = null)
    {
        var activity = new Activity(operationName);
        if (!string.IsNullOrEmpty(correlationId))
        {
            activity.SetTag("correlationId", correlationId);
        }

        activity.Start();

        // Track operation start in Application Insights
        _telemetryClient.TrackEvent($"{operationName}Started", new Dictionary<string, string>
        {
            ["operationId"] = activity.Id ?? Guid.NewGuid().ToString(),
            ["correlationId"] = correlationId ?? ""
        });

        return activity;
    }

    public void CompleteOperation(Activity activity, bool success, string? errorMessage = null)
    {
        if (activity == null) return;

        var duration = activity.Duration;
        var properties = new Dictionary<string, string>
        {
            ["operationId"] = activity.Id ?? "",
            ["operationName"] = activity.OperationName ?? "",
            ["success"] = success.ToString(),
            ["correlationId"] = activity.GetTagItem("correlationId")?.ToString() ?? ""
        };

        if (!success && !string.IsNullOrEmpty(errorMessage))
        {
            properties["errorMessage"] = errorMessage;
        }

        var telemetryMetrics = new Dictionary<string, double>
        {
            ["duration"] = duration.TotalMilliseconds
        };

        _telemetryClient.TrackEvent($"{activity.OperationName}Completed", properties, telemetryMetrics);
        _telemetryClient.TrackMetric($"{activity.OperationName}Duration", duration.TotalMilliseconds, properties);

        if (!success && !string.IsNullOrEmpty(errorMessage))
        {
            _telemetryClient.TrackException(new Exception($"{activity.OperationName} failed: {errorMessage}"), properties);
        }

        activity.Stop();
    }

    public void RecordCustomMetric(string name, double value, Dictionary<string, string>? tags = null)
    {
        var metric = new CustomMetric
        {
            Name = name,
            Value = value,
            Tags = tags ?? new Dictionary<string, string>(),
            Timestamp = DateTime.UtcNow
        };

        _customMetrics.Enqueue(metric);

        // Keep only last 1000 custom metrics
        while (_customMetrics.Count > 1000)
        {
            CustomMetric? _discard;
            _customMetrics.TryDequeue(out _discard);
        }

        // Track in Application Insights
        _telemetryClient.TrackMetric(name, value, tags);
    }

    public Task<double> GetErrorRateAsync(string endpoint, TimeSpan timeWindow)
    {
        var cutoff = DateTime.UtcNow - timeWindow;
        var totalRequests = 0;
        var failedRequests = 0;

        foreach (var kvp in _apiMetrics)
        {
            if (!kvp.Key.Contains(endpoint)) continue;

            var recentMetrics = kvp.Value.Where(m => m.Timestamp > cutoff).ToList();
            totalRequests += recentMetrics.Count;
            failedRequests += recentMetrics.Count(m => m.StatusCode >= 400);
        }

        return Task.FromResult(totalRequests > 0 ? (failedRequests * 100.0 / totalRequests) : 0);
    }

    public Task<double> GetAverageResponseTimeAsync(string endpoint, TimeSpan timeWindow)
    {
        var cutoff = DateTime.UtcNow - timeWindow;
        var responseTimes = new List<long>();

        foreach (var kvp in _apiMetrics)
        {
            if (!kvp.Key.Contains(endpoint)) continue;

            var recentMetrics = kvp.Value.Where(m => m.Timestamp > cutoff);
            responseTimes.AddRange(recentMetrics.Select(m => m.Duration));
        }

        return Task.FromResult(responseTimes.Count > 0 ? responseTimes.Average() : 0);
    }

    public Task<double> GetThroughputAsync(TimeSpan timeWindow)
    {
        var cutoff = DateTime.UtcNow - timeWindow;
        var totalRequests = 0;

        foreach (var queue in _apiMetrics.Values)
        {
            totalRequests += queue.Count(m => m.Timestamp > cutoff);
        }

        return Task.FromResult(totalRequests / timeWindow.TotalSeconds);
    }

    public async Task<List<PerformanceAlertDto>> CheckPerformanceAlertsAsync()
    {
        var alerts = new List<PerformanceAlertDto>();

        try
        {
            // Check API response time alerts
            var avgResponseTime = await GetAverageResponseTimeAsync("", TimeSpan.FromMinutes(5));
            if (avgResponseTime > 1000) // 1 second threshold
            {
                alerts.Add(new PerformanceAlertDto
                {
                    Severity = avgResponseTime > 5000 ? "Critical" : "High",
                    Category = "Performance",
                    Message = $"Average API response time is {avgResponseTime:F0}ms (threshold: 1000ms)",
                    MetricName = "AverageResponseTime",
                    CurrentValue = avgResponseTime,
                    Threshold = 1000,
                    RecommendedActions = new List<string>
                    {
                        "Check database query performance",
                        "Review external service response times",
                        "Consider scaling up resources",
                        "Analyze slow endpoints"
                    }
                });
            }

            // Check error rate alerts
            var errorRate = await GetErrorRateAsync("", TimeSpan.FromMinutes(10));
            if (errorRate > 5) // 5% error rate threshold
            {
                alerts.Add(new PerformanceAlertDto
                {
                    Severity = errorRate > 25 ? "Critical" : errorRate > 15 ? "High" : "Medium",
                    Category = "Performance",
                    Message = $"Error rate is {errorRate:F1}% (threshold: 5%)",
                    MetricName = "ErrorRate",
                    CurrentValue = errorRate,
                    Threshold = 5,
                    RecommendedActions = new List<string>
                    {
                        "Review application logs for error patterns",
                        "Check external service health",
                        "Validate input validation logic",
                        "Monitor database connection health"
                    }
                });
            }

            // Check memory usage alerts
            var utilization = await GetResourceUtilizationAsync();
            foreach (var resource in utilization.Resources)
            {
                if (resource.Value.Status == "Critical" || resource.Value.Status == "Warning")
                {
                    alerts.Add(new PerformanceAlertDto
                    {
                        Severity = resource.Value.Status == "Critical" ? "Critical" : "Medium",
                        Category = "Resources",
                        Message = $"{resource.Value.Name} utilization is {resource.Value.UtilizationPercentage:F1}%",
                        MetricName = resource.Key,
                        CurrentValue = resource.Value.UtilizationPercentage,
                        Threshold = resource.Value.Status == "Critical" ? 90 : 80,
                        RecommendedActions = GetResourceRecommendations(resource.Key)
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check performance alerts");
            alerts.Add(new PerformanceAlertDto
            {
                Severity = "Medium",
                Category = "Monitoring",
                Message = "Failed to check performance alerts",
                MetricName = "MonitoringHealth",
                RecommendedActions = new List<string> { "Check monitoring service health", "Review application logs" }
            });
        }

        return alerts;
    }

    #region Private Helper Methods

    private Task PopulateApiMetricsAsync(ApiMetricsDto apiMetrics, DateTime from, DateTime to)
    {
        var allMetrics = new List<ApiRequestMetric>();
        foreach (var queue in _apiMetrics.Values)
        {
            allMetrics.AddRange(queue.Where(m => m.Timestamp >= from && m.Timestamp <= to));
        }

        apiMetrics.TotalRequests = allMetrics.Count;
        apiMetrics.SuccessfulRequests = allMetrics.Count(m => m.StatusCode >= 200 && m.StatusCode < 400);
        apiMetrics.FailedRequests = allMetrics.Count(m => m.StatusCode >= 400);

        if (allMetrics.Any())
        {
            apiMetrics.AverageResponseTime = allMetrics.Average(m => m.Duration);

            var sortedDurations = allMetrics.Select(m => m.Duration).OrderBy(d => d).ToList();
            apiMetrics.P50ResponseTime = GetPercentile(sortedDurations, 50);
            apiMetrics.P95ResponseTime = GetPercentile(sortedDurations, 95);
            apiMetrics.P99ResponseTime = GetPercentile(sortedDurations, 99);

            var timeSpan = to - from;
            apiMetrics.RequestsPerSecond = timeSpan.TotalSeconds > 0 ? allMetrics.Count / timeSpan.TotalSeconds : 0;

            // Get slowest endpoints
            apiMetrics.SlowestEndpoints = allMetrics
                .GroupBy(m => $"{m.Method}:{m.Endpoint}")
                .Select(g => new EndpointMetricDto
                {
                    Endpoint = g.Key.Split(':')[1],
                    Method = g.Key.Split(':')[0],
                    RequestCount = g.Count(),
                    AverageResponseTime = g.Average(m => m.Duration),
                    ErrorRate = g.Count(m => m.StatusCode >= 400) * 100.0 / g.Count(),
                    LastRequest = g.Max(m => m.Timestamp)
                })
                .OrderByDescending(e => e.AverageResponseTime)
                .Take(5)
                .ToList();

            // Get most active endpoints
            apiMetrics.MostActiveEndpoints = allMetrics
                .GroupBy(m => $"{m.Method}:{m.Endpoint}")
                .Select(g => new EndpointMetricDto
                {
                    Endpoint = g.Key.Split(':')[1],
                    Method = g.Key.Split(':')[0],
                    RequestCount = g.Count(),
                    AverageResponseTime = g.Average(m => m.Duration),
                    ErrorRate = g.Count(m => m.StatusCode >= 400) * 100.0 / g.Count(),
                    LastRequest = g.Max(m => m.Timestamp)
                })
                .OrderByDescending(e => e.RequestCount)
                .Take(5)
                .ToList();

            // Get error breakdown
            apiMetrics.ErrorBreakdown = allMetrics
                .Where(m => m.StatusCode >= 400)
                .GroupBy(m => m.StatusCode)
                .ToDictionary(g => g.Key, g => (long)g.Count());
        }
        return Task.CompletedTask;
    }

    private Task PopulateDatabaseMetricsAsync(DatabaseMetricsDto databaseMetrics, DateTime from, DateTime to)
    {
        var allMetrics = new List<DatabaseQueryMetric>();
        foreach (var queue in _databaseMetrics.Values)
        {
            allMetrics.AddRange(queue.Where(m => m.Timestamp >= from && m.Timestamp <= to));
        }

        databaseMetrics.TotalQueries = allMetrics.Count;

        if (allMetrics.Any())
        {
            databaseMetrics.AverageQueryTime = allMetrics.Average(m => m.Duration);

            // Get slowest queries
            databaseMetrics.SlowestQueries = allMetrics
                .GroupBy(m => $"{m.QueryType}:{m.TableName}")
                .Select(g => new DatabaseQueryMetricDto
                {
                    QueryType = g.Key.Split(':')[0],
                    TableName = g.Key.Split(':')[1],
                    AverageExecutionTime = g.Average(m => m.Duration),
                    MaxExecutionTime = g.Max(m => m.Duration),
                    ExecutionCount = g.Count(),
                    LastExecution = g.Max(m => m.Timestamp)
                })
                .OrderByDescending(q => q.AverageExecutionTime)
                .Take(10)
                .ToList();

            // Get query breakdown by type
            databaseMetrics.QueryBreakdown = allMetrics
                .GroupBy(m => m.QueryType)
                .ToDictionary(g => g.Key, g => new QueryTypeMetricDto
                {
                    Count = g.Count(),
                    AverageTime = g.Average(m => m.Duration),
                    TotalTime = g.Sum(m => m.Duration)
                });
        }

        databaseMetrics.HealthStatus = "Healthy"; // This could be enhanced with actual health checks
        return Task.CompletedTask;
    }

    private Task PopulateExternalServiceMetricsAsync(ExternalServicesMetricsDto externalServicesMetrics, DateTime from, DateTime to)
    {
        foreach (var kvp in _externalServiceMetrics)
        {
            var serviceName = kvp.Key;
            var metrics = kvp.Value.Where(m => m.Timestamp >= from && m.Timestamp <= to).ToList();

            if (metrics.Any())
            {
                var serviceMetric = new ExternalServiceMetricDto
                {
                    ServiceName = serviceName,
                    TotalCalls = metrics.Count,
                    SuccessfulCalls = metrics.Count(m => m.Success),
                    FailedCalls = metrics.Count(m => !m.Success),
                    AverageResponseTime = metrics.Average(m => m.Duration),
                    LastSuccessfulCall = metrics.Where(m => m.Success).Max(m => (DateTime?)m.Timestamp),
                    CommonErrors = metrics
                        .Where(m => !m.Success && !string.IsNullOrEmpty(m.ErrorMessage))
                        .GroupBy(m => m.ErrorMessage)
                        .OrderByDescending(g => g.Count())
                        .Take(3)
                        .Select(g => $"{g.Key} ({g.Count()} times)")
                        .ToList()
                };

                serviceMetric.Availability = serviceMetric.SuccessRate > 95 ? "Available" :
                                           serviceMetric.SuccessRate > 80 ? "Degraded" : "Unavailable";

                externalServicesMetrics.Services[serviceName] = serviceMetric;
            }
        }

        // Determine overall health
        if (!externalServicesMetrics.Services.Any())
        {
            externalServicesMetrics.OverallHealth = "Unknown";
        }
        else if (externalServicesMetrics.Services.Values.Any(s => s.Availability == "Unavailable"))
        {
            externalServicesMetrics.OverallHealth = "Critical";
        }
        else if (externalServicesMetrics.Services.Values.Any(s => s.Availability == "Degraded"))
        {
            externalServicesMetrics.OverallHealth = "Degraded";
        }
        else
        {
            externalServicesMetrics.OverallHealth = "Healthy";
        }
        return Task.CompletedTask;
    }

    private Task PopulateSystemMetricsAsync(SystemMetricsDto systemMetrics)
    {
        try
        {
            _currentProcess.Refresh();

            // Memory metrics
            systemMetrics.Memory = new MemoryMetricsDto
            {
                WorkingSet = _currentProcess.WorkingSet64,
                PrivateMemory = _currentProcess.PrivateMemorySize64,
                ManagedMemory = GC.GetTotalMemory(false),
                PeakWorkingSet = _currentProcess.PeakWorkingSet64,
                AvailableMemory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? (long)((_memoryCounter?.NextValue() ?? 0) * 1024 * 1024)
                    : 0
            };

            var totalMemory = GetTotalPhysicalMemory();
            if (totalMemory > 0)
            {
                systemMetrics.Memory.UsagePercentage = (systemMetrics.Memory.WorkingSet * 100.0) / totalMemory;
            }

            // CPU metrics
            systemMetrics.Cpu = new CpuMetricsDto
            {
                TotalProcessorTime = _currentProcess.TotalProcessorTime.TotalMilliseconds,
                UserProcessorTime = _currentProcess.UserProcessorTime.TotalMilliseconds,
                PrivilegedProcessorTime = _currentProcess.PrivilegedProcessorTime.TotalMilliseconds
            };

            if (_cpuCounter != null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                systemMetrics.Cpu.UsagePercentage = _cpuCounter.NextValue();
            }

            // GC metrics
            systemMetrics.GarbageCollection = new GarbageCollectionMetricsDto
            {
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                TotalMemoryAllocated = GC.GetTotalAllocatedBytes()
            };

            // Thread pool metrics
            ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);

            systemMetrics.ThreadPool = new ThreadPoolMetricsDto
            {
                WorkerThreads = maxWorkerThreads - workerThreads,
                CompletionPortThreads = maxCompletionPortThreads - completionPortThreads,
                MaxWorkerThreads = maxWorkerThreads,
                MaxCompletionPortThreads = maxCompletionPortThreads
            };

            // Uptime
            systemMetrics.UptimeSeconds = (DateTime.UtcNow - _processStartTime).TotalSeconds;
            systemMetrics.ProcessStartTime = _processStartTime;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect system metrics");
        }
        return Task.CompletedTask;
    }

    private void PopulateCustomMetrics(Dictionary<string, double> customMetrics, DateTime from, DateTime to)
    {
        var recentMetrics = _customMetrics.Where(m => m.Timestamp >= from && m.Timestamp <= to).ToList();

        foreach (var group in recentMetrics.GroupBy(m => m.Name))
        {
            customMetrics[group.Key] = group.Average(m => m.Value);
        }
    }

    private static double GetPercentile(List<long> sortedList, int percentile)
    {
        if (!sortedList.Any()) return 0;

        var index = (percentile / 100.0) * (sortedList.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);

        if (lower == upper) return sortedList[lower];

        var weight = index - lower;
        return sortedList[lower] * (1 - weight) + sortedList[upper] * weight;
    }

    private static long GetTotalPhysicalMemory()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var memoryStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memoryStatus))
                {
                    return (long)memoryStatus.ullTotalPhys;
                }
            }
            else
            {
                // For Linux, read from /proc/meminfo
                var memInfo = File.ReadAllText("/proc/meminfo");
                var totalLine = memInfo.Split('\n').FirstOrDefault(line => line.StartsWith("MemTotal:"));
                if (totalLine != null)
                {
                    var parts = totalLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                    {
                        return kb * 1024; // Convert from KB to bytes
                    }
                }
            }
        }
        catch (Exception)
        {
            // Ignore errors and return 0
        }

        return 0;
    }

    private static string GetResourceStatus(double current, double max, double warningThreshold, double criticalThreshold)
    {
        if (max <= 0) return "Unknown";

        var utilization = current / max;

        if (utilization >= criticalThreshold) return "Critical";
        if (utilization >= warningThreshold) return "Warning";
        return "Normal";
    }

    private static List<string> GetResourceRecommendations(string resourceName)
    {
        return resourceName.ToLower() switch
        {
            "workingsetmemory" or "privatememory" or "managedmemory" => new List<string>
            {
                "Consider increasing application memory limits",
                "Review memory leaks in application code",
                "Optimize data caching strategies",
                "Implement memory profiling"
            },
            "cpuusage" => new List<string>
            {
                "Scale up CPU resources",
                "Optimize computationally intensive operations",
                "Consider async/await patterns",
                "Review database query performance"
            },
            "threadpoolworkers" => new List<string>
            {
                "Review async/await usage patterns",
                "Optimize I/O operations",
                "Consider custom task schedulers",
                "Analyze blocking operations"
            },
            _ => new List<string> { "Monitor resource usage trends", "Consider scaling resources" }
        };
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    public void Dispose()
    {
        _cpuCounter?.Dispose();
        _memoryCounter?.Dispose();
        _currentProcess?.Dispose();
    }

    #endregion
}

#region Metric Data Classes

internal class ApiRequestMetric
{
    public string Endpoint { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long Duration { get; set; }
    public string? UserId { get; set; }
    public DateTime Timestamp { get; set; }
}

internal class DatabaseQueryMetric
{
    public string QueryType { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public long Duration { get; set; }
    public int RecordsAffected { get; set; }
    public DateTime Timestamp { get; set; }
}

internal class ExternalServiceMetric
{
    public string ServiceName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public long Duration { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
}

internal class CustomMetric
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

#endregion
