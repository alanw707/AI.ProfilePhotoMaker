using AI.ProfilePhotoMaker.API.Data;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Database;

/// <summary>
/// Command-line service for migration operations
/// </summary>
public static class MigrationCommandService
{
    /// <summary>
    /// Handle migration command-line operations
    /// </summary>
    public static async Task<int> HandleMigrationCommand(string[] args, IServiceProvider serviceProvider)
    {
        if (args.Length == 0)
            return 0; // No migration command

        var command = args[0];
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            return command switch
            {
                "--check-db-connection" => await CheckDatabaseConnection(serviceProvider, logger),
                "--verify-migrations" => await VerifyMigrations(serviceProvider, logger),
                "--apply-migrations" => await ApplyMigrations(serviceProvider, logger),
                "--validate-database" => await ValidateDatabase(serviceProvider, logger),
                "--migration-status" => await MigrationStatus(serviceProvider, logger),
                "--database-health" => await DatabaseHealth(serviceProvider, logger),
                _ => 0 // Not a migration command
            };
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Migration command failed: {Command}", command);
            Console.WriteLine($"ERROR: Migration command '{command}' failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> CheckDatabaseConnection(IServiceProvider serviceProvider, ILogger logger)
    {
        logger.LogInformation("Checking database connection...");
        Console.WriteLine("Checking database connection...");

        var databaseProvider = serviceProvider.GetRequiredService<IDatabaseProviderService>();
        var canConnect = await databaseProvider.CanConnectAsync();
        var config = databaseProvider.GetProviderConfig();

        if (canConnect)
        {
            logger.LogInformation("Database connection successful");
            Console.WriteLine($"SUCCESS: Connected to {config.Provider} database");
            Console.WriteLine($"Connection: {MaskConnectionString(config.ConnectionString)}");
            return 0;
        }
        else
        {
            logger.LogError("Database connection failed");
            Console.WriteLine("ERROR: Cannot connect to database");
            Console.WriteLine($"Provider: {config.Provider}");
            Console.WriteLine($"Connection: {MaskConnectionString(config.ConnectionString)}");
            return 1;
        }
    }

    private static async Task<int> VerifyMigrations(IServiceProvider serviceProvider, ILogger logger)
    {
        logger.LogInformation("Verifying migrations...");
        Console.WriteLine("Verifying migrations...");

        var migrationService = serviceProvider.GetRequiredService<IMigrationService>();
        var status = await migrationService.GetMigrationStatusAsync();

        if (!status.CanConnect)
        {
            Console.WriteLine("ERROR: Cannot connect to database");
            return 1;
        }

        if (status.PendingCount > 0)
        {
            Console.WriteLine($"ERROR: Found {status.PendingCount} pending migrations:");
            foreach (var migration in status.PendingMigrations)
            {
                Console.WriteLine($"  - {migration}");
            }
            return 1;
        }

        Console.WriteLine($"SUCCESS: All migrations applied. Total: {status.AppliedCount}");
        return 0;
    }

    private static async Task<int> ApplyMigrations(IServiceProvider serviceProvider, ILogger logger)
    {
        logger.LogInformation("Applying migrations...");
        Console.WriteLine("Applying migrations...");

        var migrationService = serviceProvider.GetRequiredService<IMigrationService>();
        var result = await migrationService.ApplyMigrationsAsync();

        if (result.Success)
        {
            Console.WriteLine($"SUCCESS: {result.Message}");
            Console.WriteLine($"Duration: {result.Duration:c}");

            if (result.AppliedMigrations.Any())
            {
                Console.WriteLine("Applied migrations:");
                foreach (var migration in result.AppliedMigrations)
                {
                    Console.WriteLine($"  - {migration}");
                }
            }
            return 0;
        }
        else
        {
            Console.WriteLine("ERROR: Migration failed");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
            return 1;
        }
    }

    private static async Task<int> ValidateDatabase(IServiceProvider serviceProvider, ILogger logger)
    {
        logger.LogInformation("Validating database...");
        Console.WriteLine("Validating database...");

        var migrationService = serviceProvider.GetRequiredService<IMigrationService>();
        var validation = await migrationService.ValidateDatabaseAsync();

        if (validation.IsValid)
        {
            Console.WriteLine("SUCCESS: Database validation passed");
            Console.WriteLine($"Required seed data: {(validation.HasRequiredSeedData ? "Present" : "Missing")}");
        }
        else
        {
            Console.WriteLine("ERROR: Database validation failed");
            Console.WriteLine("Issues found:");
            foreach (var issue in validation.Issues)
            {
                Console.WriteLine($"  - {issue}");
            }
        }

        if (validation.TableCounts.Any())
        {
            Console.WriteLine("Table counts:");
            foreach (var tableCount in validation.TableCounts)
            {
                Console.WriteLine($"  - {tableCount.Key}: {tableCount.Value}");
            }
        }

        return validation.IsValid ? 0 : 1;
    }

    private static async Task<int> MigrationStatus(IServiceProvider serviceProvider, ILogger logger)
    {
        logger.LogInformation("Getting migration status...");
        Console.WriteLine("Migration Status:");

        var migrationService = serviceProvider.GetRequiredService<IMigrationService>();
        var status = await migrationService.GetMigrationStatusAsync();

        Console.WriteLine($"Can Connect: {status.CanConnect}");
        Console.WriteLine($"Database Exists: {status.DatabaseExists}");
        Console.WriteLine($"Applied Migrations: {status.AppliedCount}");
        Console.WriteLine($"Pending Migrations: {status.PendingCount}");

        if (status.PendingMigrations.Any())
        {
            Console.WriteLine("Pending migrations:");
            foreach (var migration in status.PendingMigrations)
            {
                Console.WriteLine($"  - {migration}");
            }
        }

        if (status.AppliedMigrations.Any())
        {
            Console.WriteLine("Applied migrations (last 5):");
            foreach (var migration in status.AppliedMigrations.TakeLast(5))
            {
                Console.WriteLine($"  - {migration}");
            }
        }

        return status.CanConnect ? 0 : 1;
    }

    private static async Task<int> DatabaseHealth(IServiceProvider serviceProvider, ILogger logger)
    {
        logger.LogInformation("Checking database health...");
        Console.WriteLine("Database Health Check:");

        var migrationService = serviceProvider.GetRequiredService<IMigrationService>();
        var health = await migrationService.GetDatabaseHealthAsync();

        Console.WriteLine($"Overall Health: {(health.IsHealthy ? "Healthy" : "Unhealthy")}");
        Console.WriteLine($"Can Connect: {health.CanConnect}");
        Console.WriteLine($"Checked At: {health.CheckedAt:yyyy-MM-dd HH:mm:ss} UTC");

        if (health.Metrics.Any())
        {
            Console.WriteLine("Metrics:");
            foreach (var metric in health.Metrics)
            {
                Console.WriteLine($"  - {metric.Key}: {metric.Value}");
            }
        }

        if (health.Warnings.Any())
        {
            Console.WriteLine("Warnings:");
            foreach (var warning in health.Warnings)
            {
                Console.WriteLine($"  - {warning}");
            }
        }

        if (health.Errors.Any())
        {
            Console.WriteLine("Errors:");
            foreach (var error in health.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
        }

        return health.IsHealthy ? 0 : 1;
    }

    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "Not configured";

        var parts = connectionString.Split(';');
        var serverPart = parts.FirstOrDefault(p => p.StartsWith("Server=", StringComparison.OrdinalIgnoreCase))
                        ?? parts.FirstOrDefault(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                        ?? "Unknown";

        return $"{serverPart};...";
    }
}