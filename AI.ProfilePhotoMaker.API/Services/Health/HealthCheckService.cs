using System.Diagnostics;
using System.Reflection;
using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services.Health;

/// <summary>
/// Comprehensive health check service implementation
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly IDatabaseHealthService _databaseHealthService;
    private readonly IStorageHealthService _storageHealthService;
    private readonly IDependencyHealthService _dependencyHealthService;
    private readonly ILogger<HealthCheckService> _logger;
    private readonly IWebHostEnvironment _environment;

    public HealthCheckService(
        IDatabaseHealthService databaseHealthService,
        IStorageHealthService storageHealthService,
        IDependencyHealthService dependencyHealthService,
        ILogger<HealthCheckService> logger,
        IWebHostEnvironment environment)
    {
        _databaseHealthService = databaseHealthService;
        _storageHealthService = storageHealthService;
        _dependencyHealthService = dependencyHealthService;
        _logger = logger;
        _environment = environment;
    }

    public async Task<HealthCheckResponseDto> GetBasicHealthAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Basic checks - just ensure the application is running
            var canConnectToDb = await _databaseHealthService.CanConnectAsync();

            stopwatch.Stop();

            var status = canConnectToDb ? "Healthy" : "Unhealthy";

            return new HealthCheckResponseDto
            {
                Status = status,
                Duration = stopwatch.ElapsedMilliseconds,
                Version = GetVersion(),
                Environment = _environment.EnvironmentName,
                Message = canConnectToDb ? "Application is running normally" : "Database connectivity issues detected"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during basic health check");

            return new HealthCheckResponseDto
            {
                Status = "Unhealthy",
                Duration = stopwatch.ElapsedMilliseconds,
                Version = GetVersion(),
                Environment = _environment.EnvironmentName,
                Message = $"Health check failed: {ex.Message}"
            };
        }
    }

    public async Task<ComprehensiveHealthResponseDto> GetComprehensiveHealthAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = new ComprehensiveHealthResponseDto
            {
                Version = GetVersion(),
                Environment = _environment.EnvironmentName
            };

            // Collect all component health checks in parallel
            var healthTasks = new Dictionary<string, Task<ComponentHealthDto>>
            {
                ["database"] = CheckDatabaseComponentAsync(),
                ["storage"] = CheckStorageComponentAsync(),
                ["dependencies"] = CheckDependenciesComponentAsync()
            };

            await Task.WhenAll(healthTasks.Values);

            // Collect results
            foreach (var healthTask in healthTasks)
            {
                response.Components[healthTask.Key] = await healthTask.Value;
            }

            // Determine overall status
            var allHealthy = response.Components.Values.All(c => c.Status == "Healthy");
            var anyUnhealthy = response.Components.Values.Any(c => c.Status == "Unhealthy");

            response.Status = anyUnhealthy ? "Unhealthy" : allHealthy ? "Healthy" : "Degraded";

            // Collect warnings and errors
            foreach (var component in response.Components.Values)
            {
                if (!string.IsNullOrEmpty(component.Error))
                {
                    response.Errors.Add(component.Error);
                }
            }

            // Add system metrics
            response.Metrics = await GetSystemMetricsAsync();

            stopwatch.Stop();
            response.Duration = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Comprehensive health check completed: {Status} in {Duration}ms",
                response.Status, response.Duration);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during comprehensive health check");

            return new ComprehensiveHealthResponseDto
            {
                Status = "Unhealthy",
                Duration = stopwatch.ElapsedMilliseconds,
                Version = GetVersion(),
                Environment = _environment.EnvironmentName,
                Errors = { $"Health check failed: {ex.Message}" }
            };
        }
    }

    public async Task<DatabaseHealthResponseDto> GetDatabaseHealthAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var canConnect = await _databaseHealthService.CanConnectAsync();
            var migrations = await _databaseHealthService.GetMigrationStatusAsync();
            var validation = await _databaseHealthService.ValidateDataAsync();

            stopwatch.Stop();

            var status = canConnect && migrations.PendingCount == 0 && validation.IsValid
                ? "Healthy"
                : "Unhealthy";

            return new DatabaseHealthResponseDto
            {
                Status = status,
                Duration = stopwatch.ElapsedMilliseconds,
                Version = GetVersion(),
                Environment = _environment.EnvironmentName,
                CanConnect = canConnect,
                Exists = canConnect,
                Migrations = migrations,
                Validation = validation,
                Message = status == "Healthy"
                    ? "Database is healthy and up to date"
                    : "Database has issues that need attention"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during database health check");

            return new DatabaseHealthResponseDto
            {
                Status = "Unhealthy",
                Duration = stopwatch.ElapsedMilliseconds,
                Version = GetVersion(),
                Environment = _environment.EnvironmentName,
                CanConnect = false,
                Exists = false,
                Message = $"Database health check failed: {ex.Message}"
            };
        }
    }

    public async Task<StorageHealthResponseDto> GetStorageHealthAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var canConnect = await _storageHealthService.CanConnectAsync();
            var testResults = await _storageHealthService.PerformOperationTestsAsync();
            var configuration = await _storageHealthService.GetConfigurationAsync();

            stopwatch.Stop();

            var status = canConnect && testResults.Values.All(r => r) ? "Healthy" : "Unhealthy";

            // Extract convenient top-level flags from configuration for easier health parsing
            var provider = configuration.TryGetValue("provider", out var pVal) ? pVal?.ToString() : null;
            var hasConn = configuration.TryGetValue("hasConnectionString", out var hcVal) &&
                          bool.TryParse(hcVal?.ToString(), out var hcBool) && hcBool;
            var isEmu = configuration.TryGetValue("isEmulator", out var emVal) &&
                        bool.TryParse(emVal?.ToString(), out var emBool) && emBool;

            return new StorageHealthResponseDto
            {
                Status = status,
                Duration = stopwatch.ElapsedMilliseconds,
                Version = GetVersion(),
                Environment = _environment.EnvironmentName,
                Provider = provider,
                HasConnectionString = hasConn,
                IsEmulator = isEmu,
                CanConnect = canConnect,
                TestResults = testResults,
                Configuration = configuration,
                Message = status == "Healthy"
                    ? "Storage is accessible and functioning properly"
                    : "Storage has connectivity or operational issues"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during storage health check");

            return new StorageHealthResponseDto
            {
                Status = "Unhealthy",
                Duration = stopwatch.ElapsedMilliseconds,
                Version = GetVersion(),
                Environment = _environment.EnvironmentName,
                CanConnect = false,
                Message = $"Storage health check failed: {ex.Message}"
            };
        }
    }

    public async Task<DependenciesHealthResponseDto> GetDependenciesHealthAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var dependencies = await _dependencyHealthService.CheckDependenciesAsync();

            stopwatch.Stop();

            var allHealthy = dependencies.Values.All(d => d.Status == "Healthy");
            var anyUnhealthy = dependencies.Values.Any(d => d.Status == "Unhealthy");

            var status = anyUnhealthy ? "Unhealthy" : allHealthy ? "Healthy" : "Degraded";

            return new DependenciesHealthResponseDto
            {
                Status = status,
                Duration = stopwatch.ElapsedMilliseconds,
                Version = GetVersion(),
                Environment = _environment.EnvironmentName,
                Dependencies = dependencies,
                Message = status == "Healthy"
                    ? "All external dependencies are accessible"
                    : "Some external dependencies are experiencing issues"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during dependencies health check");

            return new DependenciesHealthResponseDto
            {
                Status = "Unhealthy",
                Duration = stopwatch.ElapsedMilliseconds,
                Version = GetVersion(),
                Environment = _environment.EnvironmentName,
                Message = $"Dependencies health check failed: {ex.Message}"
            };
        }
    }

    public Task<HealthCheckResponseDto> GetReadinessAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // SIMPLIFIED readiness check - avoid database calls that can hang
            // Just verify the application is running and basic services are available

            stopwatch.Stop();

            return Task.FromResult(new HealthCheckResponseDto
            {
                Status = "Ready",
                Duration = stopwatch.ElapsedMilliseconds,
                Version = GetVersion(),
                Environment = _environment.EnvironmentName,
                Message = "Application is ready (database check bypassed for faster response)"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during readiness check");

            return Task.FromResult(new HealthCheckResponseDto
            {
                Status = "NotReady",
                Duration = stopwatch.ElapsedMilliseconds,
                Version = GetVersion(),
                Environment = _environment.EnvironmentName,
                Message = $"Readiness check failed: {ex.Message}"
            });
        }
    }

    public async Task<HealthCheckResponseDto> GetLivenessAsync()
    {
        // Liveness check: just verify the application is running
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Simple check - if we can execute this code, we're alive
            await Task.Delay(1); // Minimal async operation

            stopwatch.Stop();

            return new HealthCheckResponseDto
            {
                Status = "Alive",
                Duration = stopwatch.ElapsedMilliseconds,
                Version = GetVersion(),
                Environment = _environment.EnvironmentName,
                Message = "Application is alive and responding"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during liveness check");

            return new HealthCheckResponseDto
            {
                Status = "Dead",
                Duration = stopwatch.ElapsedMilliseconds,
                Version = GetVersion(),
                Environment = _environment.EnvironmentName,
                Message = $"Liveness check failed: {ex.Message}"
            };
        }
    }

    private async Task<ComponentHealthDto> CheckDatabaseComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var canConnect = await _databaseHealthService.CanConnectAsync();
            var migrations = await _databaseHealthService.GetMigrationStatusAsync();
            var metrics = await _databaseHealthService.GetMetricsAsync();

            stopwatch.Stop();

            var status = canConnect && migrations.PendingCount == 0 ? "Healthy" : "Unhealthy";

            return new ComponentHealthDto
            {
                Status = status,
                Description = "Database connectivity and migration status",
                Duration = stopwatch.ElapsedMilliseconds,
                Data = new Dictionary<string, object>
                {
                    ["canConnect"] = canConnect,
                    ["pendingMigrations"] = migrations.PendingCount,
                    ["appliedMigrations"] = migrations.AppliedCount,
                    ["metrics"] = metrics
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ComponentHealthDto
            {
                Status = "Unhealthy",
                Description = "Database connectivity and migration status",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<ComponentHealthDto> CheckStorageComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var canConnect = await _storageHealthService.CanConnectAsync();
            var testResults = await _storageHealthService.PerformOperationTestsAsync();

            stopwatch.Stop();

            var status = canConnect && testResults.Values.All(r => r) ? "Healthy" : "Unhealthy";

            return new ComponentHealthDto
            {
                Status = status,
                Description = "Storage connectivity and operations",
                Duration = stopwatch.ElapsedMilliseconds,
                Data = new Dictionary<string, object>
                {
                    ["canConnect"] = canConnect,
                    ["testResults"] = testResults
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ComponentHealthDto
            {
                Status = "Unhealthy",
                Description = "Storage connectivity and operations",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<ComponentHealthDto> CheckDependenciesComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var dependencies = await _dependencyHealthService.CheckDependenciesAsync();

            stopwatch.Stop();

            var healthyCount = dependencies.Values.Count(d => d.Status == "Healthy");
            var totalCount = dependencies.Count;
            var status = healthyCount == totalCount ? "Healthy" : healthyCount > 0 ? "Degraded" : "Unhealthy";

            return new ComponentHealthDto
            {
                Status = status,
                Description = "External dependencies status",
                Duration = stopwatch.ElapsedMilliseconds,
                Data = new Dictionary<string, object>
                {
                    ["totalDependencies"] = totalCount,
                    ["healthyDependencies"] = healthyCount,
                    ["dependencies"] = dependencies
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ComponentHealthDto
            {
                Status = "Unhealthy",
                Description = "External dependencies status",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<Dictionary<string, object>> GetSystemMetricsAsync()
    {
        try
        {
            var process = Process.GetCurrentProcess();

            return await Task.FromResult(new Dictionary<string, object>
            {
                ["memoryUsageMB"] = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 2),
                ["privateMemoryMB"] = Math.Round(process.PrivateMemorySize64 / 1024.0 / 1024.0, 2),
                ["processorTime"] = process.TotalProcessorTime.TotalMilliseconds,
                ["threadCount"] = process.Threads.Count,
                ["handleCount"] = process.HandleCount,
                ["startTime"] = process.StartTime,
                ["uptime"] = DateTime.Now - process.StartTime,
                ["gcMemoryMB"] = Math.Round(GC.GetTotalMemory(false) / 1024.0 / 1024.0, 2)
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect system metrics");
            return new Dictionary<string, object>
            {
                ["error"] = "Failed to collect system metrics"
            };
        }
    }

    private static string GetVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}
