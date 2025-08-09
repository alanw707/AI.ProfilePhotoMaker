using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Azure.Identity;
using Serilog;
using Serilog.Events;
using System.Diagnostics;

namespace AI.ProfilePhotoMaker.API.Configuration;

/// <summary>
/// Extension methods for configuring monitoring and logging services
/// </summary>
public static class MonitoringConfiguration
{
    /// <summary>
    /// Configure comprehensive logging with Serilog and Application Insights
    /// </summary>
    public static IServiceCollection AddComprehensiveLogging(
        this IServiceCollection services, 
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Configure Serilog
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "AI.ProfilePhotoMaker.API")
            .Enrich.WithProperty("Environment", environment.EnvironmentName)
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId();

        // Console logging
        loggerConfiguration.WriteTo.Console(
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

        // Application Insights logging (if configured)
        var appInsightsConnectionString = configuration.GetConnectionString("ApplicationInsights");
        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            loggerConfiguration.WriteTo.ApplicationInsights(
                appInsightsConnectionString,
                TelemetryConverter.Traces);
        }

        // File logging for development
        if (environment.IsDevelopment())
        {
            loggerConfiguration.WriteTo.File(
                path: Path.Combine("logs", "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }

        Log.Logger = loggerConfiguration.CreateLogger();

        services.AddSingleton(Log.Logger);
        services.AddLogging(loggingBuilder => loggingBuilder.ClearProviders().AddSerilog());

        return services;
    }

    /// <summary>
    /// Configure Application Insights with custom settings
    /// </summary>
    public static IServiceCollection AddEnhancedApplicationInsights(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var appInsightsConnectionString = configuration.GetConnectionString("ApplicationInsights")
                                        ?? configuration["Azure:ApplicationInsights:ConnectionString"];

        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            services.AddApplicationInsightsTelemetry(options =>
            {
                options.ConnectionString = appInsightsConnectionString;
                options.EnableAdaptiveSampling = true;
                options.EnableQuickPulseMetricStream = true;
                options.EnableAuthenticationTrackingJavaScript = false;
            });

            // Configure telemetry
            services.Configure<TelemetryConfiguration>(telemetryConfiguration =>
            {
                telemetryConfiguration.SetAzureTokenCredential(new DefaultAzureCredential());
            });

            // Add custom telemetry initializers
            services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();

            Console.WriteLine($"✅ Application Insights configured for environment: {environment.EnvironmentName}");
        }
        else
        {
            Console.WriteLine("⚠️  Application Insights connection string not found. Telemetry will be limited to console output.");
        }

        return services;
    }

    /// <summary>
    /// Configure monitoring-specific health checks
    /// </summary>
    public static IServiceCollection AddMonitoringHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var azureStorageConnectionString = configuration.GetConnectionString("AzureStorage");

        var healthChecksBuilder = services.AddHealthChecks();

        // Add database health check
        if (!string.IsNullOrEmpty(connectionString))
        {
            healthChecksBuilder.AddSqlServer(
                connectionString,
                name: "database",
                failureStatus: HealthStatus.Degraded,
                timeout: TimeSpan.FromSeconds(30));
        }

        // Add Azure Storage health check
        if (!string.IsNullOrEmpty(azureStorageConnectionString))
        {
            healthChecksBuilder.AddAzureBlobStorage(
                azureStorageConnectionString,
                name: "azure-storage",
                failureStatus: HealthStatus.Degraded);
        }

        // External service health checks would be added here
        // Currently commented out due to package dependency issues

        // Add memory usage health check
        healthChecksBuilder.AddCheck<MemoryHealthCheck>("memory-usage");

        // Application Insights publisher would be added here if available

        return services;
    }

    /// <summary>
    /// Configure request/response logging
    /// </summary>
    public static IServiceCollection AddRequestResponseLogging(this IServiceCollection services)
    {
        // HTTP logging would be configured here
        // Currently simplified due to namespace issues
        return services;
    }
}

/// <summary>
/// Custom telemetry initializer for Application Insights
/// </summary>
public class CustomTelemetryInitializer : ITelemetryInitializer
{
    private readonly IWebHostEnvironment _environment;

    public CustomTelemetryInitializer(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.Cloud.RoleName = "AI.ProfilePhotoMaker.API";
        telemetry.Context.Cloud.RoleInstance = Environment.MachineName;
        
        // Add custom properties
        telemetry.Context.GlobalProperties["Environment"] = _environment.EnvironmentName;
        telemetry.Context.GlobalProperties["Application"] = "AI.ProfilePhotoMaker.API";
        telemetry.Context.GlobalProperties["Version"] = GetType().Assembly.GetName().Version?.ToString() ?? "Unknown";
        
        if (telemetry is RequestTelemetry requestTelemetry)
        {
            // Custom request telemetry processing
            if (requestTelemetry.Url?.AbsolutePath.StartsWith("/api/") == true)
            {
                requestTelemetry.Context.GlobalProperties["RequestType"] = "API";
            }
        }
    }
}

/// <summary>
/// Memory usage health check
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly long _maxMemoryBytes;

    public MemoryHealthCheck(long maxMemoryBytes = 1024 * 1024 * 1024) // 1GB default
    {
        _maxMemoryBytes = maxMemoryBytes;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var workingSet = process.WorkingSet64;
            var managedMemory = GC.GetTotalMemory(false);

            var data = new Dictionary<string, object>
            {
                ["WorkingSetMB"] = workingSet / (1024 * 1024),
                ["ManagedMemoryMB"] = managedMemory / (1024 * 1024),
                ["MaxMemoryMB"] = _maxMemoryBytes / (1024 * 1024),
                ["MemoryUsagePercentage"] = (workingSet * 100.0) / _maxMemoryBytes
            };

            if (workingSet > _maxMemoryBytes * 0.9) // 90% threshold
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"High memory usage: {workingSet / (1024 * 1024)}MB", null, data));
            }

            if (workingSet > _maxMemoryBytes * 0.7) // 70% threshold
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Elevated memory usage: {workingSet / (1024 * 1024)}MB", null, data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Memory usage normal: {workingSet / (1024 * 1024)}MB", data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Failed to check memory usage", ex));
        }
    }
}