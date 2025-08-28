using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.Database;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Extensions;

/// <summary>
/// Extension methods for database service configuration
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    /// Add database services to the service collection
    /// </summary>
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // Register enhanced database provider service with improved authentication handling
        services.AddSingleton<IDatabaseProviderService, EnhancedDatabaseProviderService>();

        // Register DbContext with provider service
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var databaseProvider = serviceProvider.GetRequiredService<IDatabaseProviderService>();
            var connectionString = databaseProvider.GetConnectionString();

            // Use SQL Server for all environments
            options.UseSqlServer(connectionString);
        });

        // Register migration service
        services.AddScoped<IMigrationService, MigrationService>();

        // Add health checks
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<MigrationHealthCheck>("migrations");

        return services;
    }

    /// <summary>
    /// Apply database migrations and validate
    /// </summary>
    public static async Task<IApplicationBuilder> UseDatabaseMigrationAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Starting database migration process");

            var result = await migrationService.ApplyMigrationsAsync();

            if (result.Success)
            {
                logger.LogInformation("Database migrations completed successfully: {Message}", result.Message);

                // Validate database after migration
                var validation = await migrationService.ValidateDatabaseAsync();
                if (!validation.IsValid)
                {
                    logger.LogWarning("Database validation found issues: {Issues}", string.Join(", ", validation.Issues));
                }
                else
                {
                    logger.LogInformation("Database validation passed");
                }
            }
            else
            {
                logger.LogError("Database migration failed: {Errors}", string.Join(", ", result.Errors));
                throw new InvalidOperationException($"Database migration failed: {string.Join(", ", result.Errors)}");
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Critical error during database migration");
            throw;
        }

        return app;
    }

    /// <summary>
    /// Configure health check endpoints
    /// </summary>
    public static IApplicationBuilder UseHealthCheckEndpoints(this IApplicationBuilder app)
    {
        // Expose health endpoints under /api prefix to match infra
        app.UseHealthChecks("/api/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(x => new
                    {
                        name = x.Key,
                        status = x.Value.Status.ToString(),
                        description = x.Value.Description,
                        data = x.Value.Data
                    }),
                    timestamp = DateTime.UtcNow
                };

                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            }
        });

        app.UseHealthChecks("/api/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    status = report.Status.ToString(),
                    timestamp = DateTime.UtcNow
                };

                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            }
        });

        app.UseHealthChecks("/api/health/live", new HealthCheckOptions
        {
            Predicate = _ => false, // Exclude all checks for liveness
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
                {
                    status = "Healthy",
                    timestamp = DateTime.UtcNow
                }));
            }
        });

        return app;
    }

    /// <summary>
    /// Add database configuration options
    /// </summary>
    public static IServiceCollection AddDatabaseConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        return services;
    }
}

/// <summary>
/// Database configuration options
/// </summary>
public class DatabaseOptions
{
    public int MaxRetryCount { get; set; } = 5;
    public int MaxRetryDelaySeconds { get; set; } = 30;
    public int CommandTimeoutSeconds { get; set; } = 30;
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public bool EnableDetailedErrors { get; set; } = false;
    public bool AutoMigrateOnStartup { get; set; } = true;
    public bool ValidateOnStartup { get; set; } = true;
}
