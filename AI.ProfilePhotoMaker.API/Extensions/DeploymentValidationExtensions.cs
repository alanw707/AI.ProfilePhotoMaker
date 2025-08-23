using AI.ProfilePhotoMaker.API.Services.Deployment;

namespace AI.ProfilePhotoMaker.API.Extensions;

/// <summary>
/// Extension methods for registering deployment validation services
/// </summary>
public static class DeploymentValidationExtensions
{
    /// <summary>
    /// Add deployment validation and monitoring services to the DI container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddDeploymentValidation(this IServiceCollection services, IConfiguration configuration)
    {
        // Register deployment validation services
        services.AddScoped<IDeploymentValidationService, DeploymentValidationService>();
        services.AddScoped<IDeploymentMonitoringService, DeploymentMonitoringService>();

        return services;
    }

    /// <summary>
    /// Add deployment validation middleware to the application pipeline
    /// </summary>
    /// <param name="app">Web application</param>
    /// <returns>Web application for chaining</returns>
    public static WebApplication UseDeploymentValidation(this WebApplication app)
    {
        // Add deployment validation endpoints are handled by the controller
        // This extension is for future middleware if needed

        return app;
    }

    /// <summary>
    /// Perform startup deployment validation
    /// Validates critical deployment requirements during application startup
    /// </summary>
    /// <param name="app">Web application</param>
    /// <returns>Task for async execution</returns>
    public static async Task ValidateDeploymentOnStartupAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebApplication>>();
        var deploymentValidation = scope.ServiceProvider.GetRequiredService<IDeploymentValidationService>();

        try
        {
            logger.LogInformation("🚀 Starting deployment validation on application startup");

            // Run configuration validation
            var configResult = await deploymentValidation.ValidateConfigurationAsync();
            if (!configResult.IsValid)
            {
                logger.LogWarning("⚠️  Configuration validation issues detected:");
                foreach (var error in configResult.Errors.Take(5))
                {
                    logger.LogWarning("  - {Error}", error);
                }

                if (app.Environment.IsProduction())
                {
                    logger.LogError("🚨 Critical configuration issues in production environment");
                    // In production, you might want to prevent startup for critical issues
                    // throw new InvalidOperationException("Critical configuration issues detected");
                }
            }
            else
            {
                logger.LogInformation("✅ Configuration validation passed");
            }

            // Run security validation
            var securityResult = await deploymentValidation.ValidateSecurityConfigurationAsync();
            if (securityResult.HasSecurityIssues)
            {
                logger.LogWarning("⚠️  Security validation issues detected: {SecurityScore}", securityResult.SecurityScore);
                foreach (var error in securityResult.Errors.Take(3))
                {
                    logger.LogWarning("  - {Error}", error);
                }
            }
            else
            {
                logger.LogInformation("✅ Security validation passed: {SecurityScore}", securityResult.SecurityScore);
            }

            // Run database validation
            var databaseResult = await deploymentValidation.ValidateDatabaseReadinessAsync();
            if (!databaseResult.IsProductionReady)
            {
                logger.LogWarning("⚠️  Database readiness issues detected:");
                foreach (var error in databaseResult.Errors.Take(3))
                {
                    logger.LogWarning("  - {Error}", error);
                }
            }
            else
            {
                logger.LogInformation("✅ Database validation passed - production ready");
            }

            // Get overall readiness score
            var readinessResult = await deploymentValidation.GetDeploymentReadinessScoreAsync();
            logger.LogInformation("📊 Deployment Readiness Score: {Score}% ({Level})",
                readinessResult.OverallScore, readinessResult.ReadinessLevel);

            if (readinessResult.IsReadyForDeployment)
            {
                logger.LogInformation("🎉 Application is ready for deployment!");
            }
            else
            {
                logger.LogWarning("⚠️  Application has deployment readiness issues:");
                foreach (var issue in readinessResult.BlockingIssues.Take(3))
                {
                    logger.LogWarning("  - {Issue}", issue);
                }

                if (readinessResult.CriticalRecommendations.Any())
                {
                    logger.LogInformation("💡 Critical recommendations:");
                    foreach (var recommendation in readinessResult.CriticalRecommendations.Take(3))
                    {
                        logger.LogInformation("  - {Recommendation}", recommendation);
                    }
                }
            }

            logger.LogInformation("🏁 Deployment validation completed on startup");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "🚨 Deployment validation failed during startup");

            if (app.Environment.IsProduction())
            {
                logger.LogCritical("Production environment startup validation failed - this may indicate serious deployment issues");
            }

            // Don't throw in startup validation to avoid preventing application start
            // Instead, log the error and allow the application to start with warnings
        }
    }

    /// <summary>
    /// Configure deployment validation for different environments
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="environment">Hosting environment</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection ConfigureDeploymentValidation(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Add environment-specific configuration
        if (environment.IsProduction())
        {
            // Production-specific validation settings
            services.Configure<DeploymentValidationOptions>(options =>
            {
                options.StrictValidation = true;
                options.RequireAllServicesHealthy = true;
                options.MinimumReadinessScore = 85.0;
                options.FailOnSecurityIssues = true;
            });
        }
        else if (environment.IsStaging())
        {
            // Staging-specific validation settings
            services.Configure<DeploymentValidationOptions>(options =>
            {
                options.StrictValidation = true;
                options.RequireAllServicesHealthy = false;
                options.MinimumReadinessScore = 75.0;
                options.FailOnSecurityIssues = false;
            });
        }
        else
        {
            // Development-specific validation settings
            services.Configure<DeploymentValidationOptions>(options =>
            {
                options.StrictValidation = false;
                options.RequireAllServicesHealthy = false;
                options.MinimumReadinessScore = 60.0;
                options.FailOnSecurityIssues = false;
            });
        }

        return services;
    }
}

/// <summary>
/// Configuration options for deployment validation
/// </summary>
public class DeploymentValidationOptions
{
    /// <summary>
    /// Enable strict validation mode
    /// </summary>
    public bool StrictValidation { get; set; } = false;

    /// <summary>
    /// Require all external services to be healthy
    /// </summary>
    public bool RequireAllServicesHealthy { get; set; } = false;

    /// <summary>
    /// Minimum readiness score required for deployment
    /// </summary>
    public double MinimumReadinessScore { get; set; } = 70.0;

    /// <summary>
    /// Fail validation if security issues are detected
    /// </summary>
    public bool FailOnSecurityIssues { get; set; } = false;

    /// <summary>
    /// Enable continuous monitoring
    /// </summary>
    public bool EnableContinuousMonitoring { get; set; } = true;

    /// <summary>
    /// Configuration drift detection interval in minutes
    /// </summary>
    public int DriftDetectionIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Performance regression check interval in minutes
    /// </summary>
    public int PerformanceCheckIntervalMinutes { get; set; } = 15;
}