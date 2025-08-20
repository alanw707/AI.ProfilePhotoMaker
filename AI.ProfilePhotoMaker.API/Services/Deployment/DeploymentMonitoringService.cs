using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Health;
using AI.ProfilePhotoMaker.API.Services.Monitoring;
using System.Diagnostics;
using System.Text.Json;

namespace AI.ProfilePhotoMaker.API.Services.Deployment;

/// <summary>
/// Service for continuous deployment monitoring and drift detection
/// Monitors deployment health and detects configuration changes
/// </summary>
public class DeploymentMonitoringService : IDeploymentMonitoringService
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly IPerformanceMonitoringService _performanceMonitoring;
    private readonly IDependencyHealthService _dependencyHealthService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DeploymentMonitoringService> _logger;
    private readonly IWebHostEnvironment _environment;

    // Cache for baseline configuration
    private static Dictionary<string, object>? _configurationBaseline;
    private static DateTime _baselineTimestamp;
    private static readonly object _baselineLock = new object();

    public DeploymentMonitoringService(
        IHealthCheckService healthCheckService,
        IPerformanceMonitoringService performanceMonitoring,
        IDependencyHealthService dependencyHealthService,
        IConfiguration configuration,
        ILogger<DeploymentMonitoringService> logger,
        IWebHostEnvironment environment)
    {
        _healthCheckService = healthCheckService;
        _performanceMonitoring = performanceMonitoring;
        _dependencyHealthService = dependencyHealthService;
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    public async Task<DeploymentHealthDto> GetDeploymentHealthAsync()
    {
        try
        {
            _logger.LogDebug("Starting deployment health check");

            var services = new Dictionary<string, ServiceHealthDto>();
            var issues = new List<HealthIssueDto>();
            var recommendations = new List<string>();

            // Check core application health
            var appHealth = await _healthCheckService.GetBasicHealthAsync();
            services["Application"] = new ServiceHealthDto
            {
                ServiceName = "Application",
                Status = appHealth.Status,
                IsHealthy = appHealth.Status.ToLower() == "healthy",
                ResponseTimeMs = (int)appHealth.Duration,
                UptimePercentage = appHealth.Status.ToLower() == "healthy" ? 100.0 : 0.0,
                LastHealthCheck = DateTime.UtcNow,
                ErrorMessage = appHealth.Status.ToLower() != "healthy" ? (appHealth.Message ?? string.Empty) : string.Empty
            };

            // Check database health
            var dbHealth = await _healthCheckService.GetDatabaseHealthAsync();
            services["Database"] = new ServiceHealthDto
            {
                ServiceName = "Database",
                Status = dbHealth.Status,
                IsHealthy = dbHealth.CanConnect && dbHealth.Status.ToLower() == "healthy",
                ResponseTimeMs = (int)dbHealth.Duration,
                UptimePercentage = dbHealth.CanConnect ? 100.0 : 0.0,
                LastHealthCheck = DateTime.UtcNow,
                ErrorMessage = !dbHealth.CanConnect ? "Database connection failed" : string.Empty
            };

            // Check storage health
            var storageHealth = await _healthCheckService.GetStorageHealthAsync();
            services["Storage"] = new ServiceHealthDto
            {
                ServiceName = "Storage",
                Status = storageHealth.Status,
                IsHealthy = storageHealth.CanConnect && storageHealth.Status.ToLower() == "healthy",
                ResponseTimeMs = (int)storageHealth.Duration,
                UptimePercentage = storageHealth.CanConnect ? 100.0 : 0.0,
                LastHealthCheck = DateTime.UtcNow,
                ErrorMessage = !storageHealth.CanConnect ? "Storage connection failed" : string.Empty
            };

            // Check external dependencies
            var dependencies = await _dependencyHealthService.CheckDependenciesAsync();
            foreach (var dependency in dependencies)
            {
                services[dependency.Key] = new ServiceHealthDto
                {
                    ServiceName = dependency.Key,
                    Status = dependency.Value.Status,
                    IsHealthy = dependency.Value.Status.ToLower() == "healthy",
                    ResponseTimeMs = (int)dependency.Value.ResponseTime,
                    UptimePercentage = dependency.Value.Status.ToLower() == "healthy" ? 100.0 : 0.0,
                    LastHealthCheck = DateTime.UtcNow,
                    ErrorMessage = dependency.Value.Error ?? string.Empty
                };
            }

            // Identify issues
            foreach (var service in services.Values.Where(s => !s.IsHealthy))
            {
                var severity = DetermineIssueSeverity(service.ServiceName);
                issues.Add(new HealthIssueDto
                {
                    Category = "Service Health",
                    Severity = severity,
                    Description = $"{service.ServiceName} is unhealthy: {service.ErrorMessage}",
                    Service = service.ServiceName,
                    DetectedAt = DateTime.UtcNow,
                    RecommendedActions = GetServiceRecommendations(service.ServiceName, service.ErrorMessage)
                });
            }

            // Generate recommendations
            if (issues.Any(i => i.Severity == "Critical"))
            {
                recommendations.Add("Immediate attention required - critical services are down");
            }

            if (services.Values.Count(s => !s.IsHealthy) > services.Count / 2)
            {
                recommendations.Add("Multiple services are unhealthy - check infrastructure");
            }

            var performanceMetrics = await GetPerformanceMetrics();

            // Determine overall health
            var criticalIssues = issues.Count(i => i.Severity == "Critical");
            var overallHealth = criticalIssues == 0 
                ? (issues.Count == 0 ? "Healthy" : "Degraded")
                : "Unhealthy";

            return new DeploymentHealthDto
            {
                HealthStatus = overallHealth,
                IsHealthy = overallHealth == "Healthy",
                LastChecked = DateTime.UtcNow,
                Services = services,
                Issues = issues,
                Recommendations = recommendations.Distinct().ToList(),
                Metrics = performanceMetrics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment health check failed");
            
            return new DeploymentHealthDto
            {
                HealthStatus = "Error",
                IsHealthy = false,
                LastChecked = DateTime.UtcNow,
                Issues = new List<HealthIssueDto>
                {
                    new()
                    {
                        Category = "Monitoring",
                        Severity = "Critical",
                        Description = $"Health monitoring failed: {ex.Message}",
                        Service = "Monitoring Service",
                        DetectedAt = DateTime.UtcNow,
                        RecommendedActions = new List<string> { "Check monitoring service configuration" }
                    }
                }
            };
        }
    }

    public async Task<ConfigurationDriftResponseDto> DetectConfigurationDriftAsync()
    {
        try
        {
            _logger.LogDebug("Starting configuration drift detection");

            // Get current configuration snapshot
            var currentSnapshot = await GetConfigurationSnapshotAsync();
            
            // Initialize baseline if not set
            lock (_baselineLock)
            {
                if (_configurationBaseline == null)
                {
                    _configurationBaseline = currentSnapshot;
                    _baselineTimestamp = DateTime.UtcNow;
                    
                    return new ConfigurationDriftResponseDto
                    {
                        HasDrift = false,
                        DriftSeverity = "None",
                        BaselineTimestamp = _baselineTimestamp,
                        CurrentTimestamp = DateTime.UtcNow,
                        DriftItems = new List<ConfigurationDriftDto>(),
                        BaselineSnapshot = _configurationBaseline,
                        CurrentSnapshot = currentSnapshot
                    };
                }
            }

            // Compare configurations
            var driftItems = CompareConfigurations(_configurationBaseline, currentSnapshot);
            var hasDrift = driftItems.Any();
            var driftSeverity = DetermineDriftSeverity(driftItems);

            return new ConfigurationDriftResponseDto
            {
                HasDrift = hasDrift,
                DriftSeverity = driftSeverity,
                BaselineTimestamp = _baselineTimestamp,
                CurrentTimestamp = DateTime.UtcNow,
                DriftItems = driftItems,
                BaselineSnapshot = _configurationBaseline,
                CurrentSnapshot = currentSnapshot
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration drift detection failed");
            
            return new ConfigurationDriftResponseDto
            {
                HasDrift = true,
                DriftSeverity = "Error",
                BaselineTimestamp = _baselineTimestamp,
                CurrentTimestamp = DateTime.UtcNow,
                DriftItems = new List<ConfigurationDriftDto>
                {
                    new()
                    {
                        ConfigurationKey = "DriftDetection",
                        DriftType = "Error",
                        Impact = "High",
                        Recommendation = $"Fix drift detection error: {ex.Message}"
                    }
                }
            };
        }
    }

    public async Task<ServiceAvailabilityResponseDto> ValidateServiceAvailabilityAsync()
    {
        try
        {
            _logger.LogDebug("Starting service availability validation");

            var services = new Dictionary<string, ExternalServiceStatusDto>();
            var unavailableServices = new List<string>();

            // Check external dependencies
            var dependencies = await _dependencyHealthService.CheckDependenciesAsync();
            
            foreach (var dependency in dependencies)
            {
                var isAvailable = dependency.Value.Status.ToLower() == "healthy";
                var service = new ExternalServiceStatusDto
                {
                    ServiceName = dependency.Key,
                    Status = dependency.Value.Status,
                    IsAvailable = isAvailable,
                    ResponseTimeMs = (int)dependency.Value.ResponseTime,
                    Endpoint = dependency.Key,
                    LastSuccessfulCall = isAvailable ? DateTime.UtcNow : DateTime.MinValue,
                    ErrorMessage = dependency.Value.Error ?? string.Empty,
                    IsCritical = IsCriticalService(dependency.Key)
                };

                services[dependency.Key] = service;

                if (!isAvailable)
                {
                    unavailableServices.Add(dependency.Key);
                }
            }

            // Calculate overall availability
            var totalServices = services.Count;
            var availableServices = services.Values.Count(s => s.IsAvailable);
            var availabilityPercentage = totalServices > 0 ? (double)availableServices / totalServices * 100 : 100;

            return new ServiceAvailabilityResponseDto
            {
                AllServicesAvailable = unavailableServices.Count == 0,
                Services = services,
                UnavailableServices = unavailableServices,
                OverallAvailabilityPercentage = Math.Round(availabilityPercentage, 2),
                LastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service availability validation failed");
            
            return new ServiceAvailabilityResponseDto
            {
                AllServicesAvailable = false,
                Services = new Dictionary<string, ExternalServiceStatusDto>(),
                UnavailableServices = new List<string> { "ValidationError" },
                OverallAvailabilityPercentage = 0,
                LastChecked = DateTime.UtcNow
            };
        }
    }

    public async Task<PerformanceRegressionResponseDto> CheckPerformanceRegressionAsync()
    {
        try
        {
            _logger.LogDebug("Starting performance regression check");

            // This would typically compare against stored baseline metrics
            // For now, we'll use simplified thresholds
            var currentMetrics = await _performanceMonitoring.GetCurrentMetricsAsync();
            
            var comparisons = new Dictionary<string, PerformanceComparisonDto>();
            var issues = new List<PerformanceRegressionIssueDto>();

            // Response time comparison
            var baselineResponseTime = 500.0; // ms - would come from stored baseline
            var currentResponseTime = currentMetrics.ApiMetrics.AverageResponseTime;
            var responseTimeChange = ((currentResponseTime - baselineResponseTime) / baselineResponseTime) * 100;

            comparisons["ResponseTime"] = new PerformanceComparisonDto
            {
                MetricName = "Response Time",
                BaselineValue = baselineResponseTime,
                CurrentValue = currentResponseTime,
                PercentageChange = Math.Round(responseTimeChange, 2),
                Trend = responseTimeChange > 20 ? "Degraded" : responseTimeChange < -10 ? "Improved" : "Stable",
                IsSignificant = Math.Abs(responseTimeChange) > 15,
                Impact = Math.Abs(responseTimeChange) > 50 ? "High" : Math.Abs(responseTimeChange) > 20 ? "Medium" : "Low"
            };

            if (responseTimeChange > 30)
            {
                issues.Add(new PerformanceRegressionIssueDto
                {
                    MetricName = "Response Time",
                    Category = "API Performance",
                    Severity = "High",
                    RegressionPercentage = responseTimeChange,
                    Description = $"Response time increased by {responseTimeChange:F1}%",
                    PossibleCauses = new List<string> 
                    { 
                        "Increased database query time",
                        "External service latency",
                        "Resource constraints"
                    },
                    RecommendedActions = new List<string>
                    {
                        "Review database query performance",
                        "Check external service response times",
                        "Monitor resource utilization"
                    }
                });
            }

            // Error rate comparison
            var baselineErrorRate = 2.0; // % - would come from stored baseline
            var currentErrorRate = currentMetrics.ApiMetrics.ErrorRate;
            var errorRateChange = ((currentErrorRate - baselineErrorRate) / Math.Max(baselineErrorRate, 0.1)) * 100;

            comparisons["ErrorRate"] = new PerformanceComparisonDto
            {
                MetricName = "Error Rate",
                BaselineValue = baselineErrorRate,
                CurrentValue = currentErrorRate,
                PercentageChange = Math.Round(errorRateChange, 2),
                Trend = errorRateChange > 50 ? "Degraded" : errorRateChange < -25 ? "Improved" : "Stable",
                IsSignificant = Math.Abs(errorRateChange) > 25,
                Impact = errorRateChange > 100 ? "High" : errorRateChange > 50 ? "Medium" : "Low"
            };

            if (errorRateChange > 100)
            {
                issues.Add(new PerformanceRegressionIssueDto
                {
                    MetricName = "Error Rate",
                    Category = "API Reliability",
                    Severity = "Critical",
                    RegressionPercentage = errorRateChange,
                    Description = $"Error rate increased by {errorRateChange:F1}%",
                    PossibleCauses = new List<string>
                    {
                        "Application bugs",
                        "External service failures",
                        "Configuration issues"
                    },
                    RecommendedActions = new List<string>
                    {
                        "Review application logs for errors",
                        "Check external service health",
                        "Validate configuration changes"
                    }
                });
            }

            // Memory usage comparison
            var baselineMemoryUsage = 60.0; // % - would come from stored baseline
            var currentMemoryUsage = currentMetrics.SystemMetrics.Memory.UsagePercentage;
            var memoryUsageChange = ((currentMemoryUsage - baselineMemoryUsage) / baselineMemoryUsage) * 100;

            comparisons["MemoryUsage"] = new PerformanceComparisonDto
            {
                MetricName = "Memory Usage",
                BaselineValue = baselineMemoryUsage,
                CurrentValue = currentMemoryUsage,
                PercentageChange = Math.Round(memoryUsageChange, 2),
                Trend = memoryUsageChange > 25 ? "Degraded" : memoryUsageChange < -15 ? "Improved" : "Stable",
                IsSignificant = Math.Abs(memoryUsageChange) > 20,
                Impact = memoryUsageChange > 50 ? "High" : memoryUsageChange > 25 ? "Medium" : "Low"
            };

            if (memoryUsageChange > 40)
            {
                issues.Add(new PerformanceRegressionIssueDto
                {
                    MetricName = "Memory Usage",
                    Category = "Resource Usage",
                    Severity = "Medium",
                    RegressionPercentage = memoryUsageChange,
                    Description = $"Memory usage increased by {memoryUsageChange:F1}%",
                    PossibleCauses = new List<string>
                    {
                        "Memory leaks",
                        "Increased load",
                        "Inefficient algorithms"
                    },
                    RecommendedActions = new List<string>
                    {
                        "Profile memory usage",
                        "Check for memory leaks",
                        "Consider scaling resources"
                    }
                });
            }

            var hasRegression = issues.Any();
            var regressionSeverity = issues.Any(i => i.Severity == "Critical") ? "Critical" :
                                   issues.Any(i => i.Severity == "High") ? "High" :
                                   issues.Any(i => i.Severity == "Medium") ? "Medium" : "Low";

            return new PerformanceRegressionResponseDto
            {
                HasRegression = hasRegression,
                RegressionSeverity = hasRegression ? regressionSeverity : "None",
                Comparisons = comparisons,
                Issues = issues,
                BaselineTimestamp = DateTime.UtcNow.AddHours(-1), // Simulated baseline timestamp
                CurrentTimestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Performance regression check failed");
            
            return new PerformanceRegressionResponseDto
            {
                HasRegression = true,
                RegressionSeverity = "Error",
                Comparisons = new Dictionary<string, PerformanceComparisonDto>(),
                Issues = new List<PerformanceRegressionIssueDto>
                {
                    new()
                    {
                        MetricName = "RegressionCheck",
                        Category = "Monitoring",
                        Severity = "High",
                        Description = $"Performance regression check failed: {ex.Message}",
                        RecommendedActions = new List<string> { "Fix performance monitoring configuration" }
                    }
                },
                BaselineTimestamp = DateTime.MinValue,
                CurrentTimestamp = DateTime.UtcNow
            };
        }
    }

    private string DetermineIssueSeverity(string serviceName)
    {
        var criticalServices = new[] { "Application", "Database" };
        var highPriorityServices = new[] { "Storage", "Replicate" };

        if (criticalServices.Contains(serviceName))
            return "Critical";
        if (highPriorityServices.Contains(serviceName))
            return "High";
        
        return "Medium";
    }

    private List<string> GetServiceRecommendations(string serviceName, string errorMessage)
    {
        return serviceName switch
        {
            "Application" => new List<string> { "Check application logs", "Restart application if necessary" },
            "Database" => new List<string> { "Check database connectivity", "Verify database server status" },
            "Storage" => new List<string> { "Verify storage service configuration", "Check network connectivity" },
            "Replicate" => new List<string> { "Verify API token", "Check Replicate service status" },
            _ => new List<string> { "Check service configuration", "Verify network connectivity" }
        };
    }

    private async Task<Dictionary<string, object>> GetPerformanceMetrics()
    {
        try
        {
            var metrics = await _performanceMonitoring.GetCurrentMetricsAsync();
            
            return new Dictionary<string, object>
            {
                ["responseTime"] = metrics.ApiMetrics.AverageResponseTime,
                ["errorRate"] = metrics.ApiMetrics.ErrorRate,
                ["throughput"] = metrics.ApiMetrics.RequestsPerSecond,
                ["memoryUsage"] = metrics.SystemMetrics.Memory.UsagePercentage,
                ["cpuUsage"] = metrics.SystemMetrics.Cpu.UsagePercentage
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve performance metrics");
            return new Dictionary<string, object> { ["error"] = "Metrics unavailable" };
        }
    }

    private Task<Dictionary<string, object>> GetConfigurationSnapshotAsync()
    {
        var snapshot = new Dictionary<string, object>();

        try
        {
            // Capture key configuration values (without sensitive data)
            snapshot["Environment"] = _environment.EnvironmentName;
            snapshot["ApplicationName"] = _environment.ApplicationName;
            snapshot["ContentRootPath"] = _environment.ContentRootPath;

            // Connection strings (masked)
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            snapshot["HasDatabaseConnection"] = !string.IsNullOrEmpty(connectionString);

            // JWT configuration (masked)
            snapshot["JWTConfigured"] = !string.IsNullOrEmpty(_configuration["JWT:Secret"]);
            snapshot["JWTAudience"] = _configuration["JWT:ValidAudience"] ?? "Not configured";
            snapshot["JWTIssuer"] = _configuration["JWT:ValidIssuer"] ?? "Not configured";

            // External services configuration (masked)
            snapshot["ReplicateConfigured"] = !string.IsNullOrEmpty(_configuration["Replicate:ApiToken"]);
            snapshot["StorageConfigured"] = !string.IsNullOrEmpty(_configuration["AzureStorage:ConnectionString"]);
            snapshot["GoogleAuthConfigured"] = !string.IsNullOrEmpty(_configuration["Authentication:Google:ClientId"]);

            // Application settings
            snapshot["AppBaseUrl"] = _configuration["AppBaseUrl"] ?? "Not configured";
            snapshot["LogLevel"] = _configuration["Logging:LogLevel:Default"] ?? "Information";

            // Database configuration
            var dbConfig = _configuration.GetSection("Database");
            snapshot["DatabaseAutoMigrate"] = dbConfig.GetValue<bool>("AutoMigrateOnStartup");
            snapshot["DatabaseValidateOnStartup"] = dbConfig.GetValue<bool>("ValidateOnStartup");
            snapshot["DatabaseMaxRetryCount"] = dbConfig.GetValue<int>("MaxRetryCount", 5);

            snapshot["Timestamp"] = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error creating configuration snapshot");
            snapshot["Error"] = $"Snapshot creation failed: {ex.Message}";
        }

        return Task.FromResult(snapshot);
    }

    private List<ConfigurationDriftDto> CompareConfigurations(Dictionary<string, object> baseline, Dictionary<string, object> current)
    {
        var driftItems = new List<ConfigurationDriftDto>();

        // Check for added items
        foreach (var currentItem in current)
        {
            if (!baseline.ContainsKey(currentItem.Key))
            {
                driftItems.Add(new ConfigurationDriftDto
                {
                    ConfigurationKey = currentItem.Key,
                    DriftType = "Added",
                    BaselineValue = null,
                    CurrentValue = currentItem.Value,
                    Impact = DetermineConfigImpact(currentItem.Key),
                    Recommendation = $"New configuration '{currentItem.Key}' added - verify if intentional"
                });
            }
        }

        // Check for removed items
        foreach (var baselineItem in baseline)
        {
            if (!current.ContainsKey(baselineItem.Key))
            {
                driftItems.Add(new ConfigurationDriftDto
                {
                    ConfigurationKey = baselineItem.Key,
                    DriftType = "Removed",
                    BaselineValue = baselineItem.Value,
                    CurrentValue = null,
                    Impact = DetermineConfigImpact(baselineItem.Key),
                    Recommendation = $"Configuration '{baselineItem.Key}' removed - verify if intentional"
                });
            }
        }

        // Check for modified items
        foreach (var baselineItem in baseline)
        {
            if (current.ContainsKey(baselineItem.Key))
            {
                var baselineValue = JsonSerializer.Serialize(baselineItem.Value);
                var currentValue = JsonSerializer.Serialize(current[baselineItem.Key]);

                if (baselineValue != currentValue)
                {
                    driftItems.Add(new ConfigurationDriftDto
                    {
                        ConfigurationKey = baselineItem.Key,
                        DriftType = "Modified",
                        BaselineValue = baselineItem.Value,
                        CurrentValue = current[baselineItem.Key],
                        Impact = DetermineConfigImpact(baselineItem.Key),
                        Recommendation = $"Configuration '{baselineItem.Key}' changed - review if change was intended"
                    });
                }
            }
        }

        return driftItems;
    }

    private string DetermineConfigImpact(string configKey)
    {
        var highImpactKeys = new[] { "Environment", "HasDatabaseConnection", "JWTConfigured" };
        var mediumImpactKeys = new[] { "ReplicateConfigured", "StorageConfigured", "AppBaseUrl" };

        if (highImpactKeys.Any(key => configKey.Contains(key)))
            return "High";
        if (mediumImpactKeys.Any(key => configKey.Contains(key)))
            return "Medium";
        
        return "Low";
    }

    private string DetermineDriftSeverity(List<ConfigurationDriftDto> driftItems)
    {
        if (!driftItems.Any())
            return "None";

        var highImpactCount = driftItems.Count(d => d.Impact == "High");
        var mediumImpactCount = driftItems.Count(d => d.Impact == "Medium");

        if (highImpactCount > 0)
            return "High";
        if (mediumImpactCount > 2)
            return "Medium";
        
        return "Low";
    }

    private bool IsCriticalService(string serviceName)
    {
        var criticalServices = new[] { "Replicate", "Database", "Storage" };
        return criticalServices.Contains(serviceName);
    }
}
