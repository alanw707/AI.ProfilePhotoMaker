using System.Data.Common;
using System.Diagnostics;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Database;

/// <summary>
/// Service for managing database migrations and schema validation
/// </summary>
public class MigrationService : IMigrationService
{
    private readonly ApplicationDbContext _context;
    private readonly IDatabaseProviderService _databaseProvider;
    private readonly ILogger<MigrationService> _logger;
    private readonly IWebHostEnvironment _environment;
    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sl(string? value) => LoggingSanitizer.Sanitize(value, 400);

    public MigrationService(
        ApplicationDbContext context,
        IDatabaseProviderService databaseProvider,
        ILogger<MigrationService> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _databaseProvider = databaseProvider;
        _logger = logger;
        _environment = environment;
    }

    public async Task<MigrationResult> ApplyMigrationsAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new MigrationResult();

        try
        {
            _logger.LogInformation("Starting database migration process");

            // Check database connectivity
            if (!await _databaseProvider.CanConnectAsync())
            {
                result.Errors.Add("Cannot connect to database");
                return result;
            }

            // Get pending migrations before applying
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
            result.AppliedMigrations.AddRange(pendingMigrations);

            if (!pendingMigrations.Any())
            {
                _logger.LogInformation("No pending migrations found");
                result.Success = true;
                result.Message = "Database is up to date";
                return result;
            }

            _logger.LogInformation("Applying {Count} pending migrations: {Migrations}",
                pendingMigrations.Count(), Sl(string.Join(", ", pendingMigrations)));

            // Apply migrations with conflict resolution
            await ApplyMigrationsWithConflictResolutionAsync(pendingMigrations);

            _logger.LogInformation("Successfully applied all migrations");
            result.Success = true;
            result.Message = $"Applied {pendingMigrations.Count()} migrations successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration failed: {Message}", S(ex.Message));
            result.Errors.Add($"Migration failed: {S(ex.Message)}");

            // Add inner exception details
            if (ex.InnerException != null)
            {
                result.Errors.Add($"Inner exception: {S(ex.InnerException.Message)}");
            }
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            _logger.LogInformation("Migration process completed in {Duration:c}", result.Duration);
        }

        return result;
    }

    public async Task<MigrationStatus> GetMigrationStatusAsync()
    {
        var status = new MigrationStatus();

        try
        {
            status.CanConnect = await _databaseProvider.CanConnectAsync();

            if (!status.CanConnect)
            {
                return status;
            }

            status.DatabaseExists = await _context.Database.CanConnectAsync();

            if (status.DatabaseExists)
            {
                var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync();
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();

                status.AppliedMigrations.AddRange(appliedMigrations);
                status.PendingMigrations.AddRange(pendingMigrations);
                status.AppliedCount = appliedMigrations.Count();
                status.PendingCount = pendingMigrations.Count();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get migration status: {Message}", S(ex.Message));
        }

        return status;
    }

    public async Task<ValidationResult> ValidateDatabaseAsync()
    {
        var result = new ValidationResult();

        try
        {
            if (!await _databaseProvider.CanConnectAsync())
            {
                result.Issues.Add("Cannot connect to database");
                return result;
            }

            // Validate core tables exist and have data
            await ValidateTable("Styles", result, minExpectedCount: 20);
            await ValidateTable("CreditPackages", result, minExpectedCount: 3);
            await ValidateTable("AspNetRoles", result, minExpectedCount: 0);
            await ValidateTable("AspNetUsers", result, minExpectedCount: 0);

            // Check for required seed data
            var activeStylesCount = await _context.Styles.CountAsync(s => s.IsActive);
            if (activeStylesCount < 20)
            {
                result.Issues.Add($"Expected at least 20 active styles, found {activeStylesCount}");
                result.MissingSeedData.Add("Styles");
            }

            var creditPackagesCount = await _context.CreditPackages.CountAsync(cp => cp.IsActive);
            if (creditPackagesCount < 3)
            {
                result.Issues.Add($"Expected at least 3 active credit packages, found {creditPackagesCount}");
                result.MissingSeedData.Add("CreditPackages");
            }

            result.HasRequiredSeedData = result.MissingSeedData.Count == 0;
            result.IsValid = result.Issues.Count == 0;

            _logger.LogInformation("Database validation completed. Valid: {IsValid}, Issues: {IssueCount}",
                result.IsValid, result.Issues.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database validation failed: {Message}", S(ex.Message));
            result.Issues.Add($"Validation error: {S(ex.Message)}");
        }

        return result;
    }

    public async Task<IEnumerable<string>> GetAppliedMigrationsAsync()
    {
        try
        {
            return await _context.Database.GetAppliedMigrationsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get applied migrations: {Message}", S(ex.Message));
            return Enumerable.Empty<string>();
        }
    }

    public async Task<IEnumerable<string>> GetPendingMigrationsAsync()
    {
        try
        {
            return await _context.Database.GetPendingMigrationsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending migrations: {Message}", S(ex.Message));
            return Enumerable.Empty<string>();
        }
    }

    public async Task<bool> EnsureDatabaseCreatedAsync()
    {
        try
        {
            _logger.LogInformation("Ensuring database is created");
            return await _context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure database creation: {Message}", S(ex.Message));
            return false;
        }
    }

    public async Task<DatabaseHealth> GetDatabaseHealthAsync()
    {
        var health = new DatabaseHealth();
        var issues = new List<string>();
        var warnings = new List<string>();

        try
        {
            // Test connectivity
            health.CanConnect = await _databaseProvider.CanConnectAsync();
            if (!health.CanConnect)
            {
                issues.Add("Cannot connect to database");
                health.Errors.AddRange(issues);
                return health;
            }

            // Get migration status
            health.MigrationStatus = await GetMigrationStatusAsync();
            if (health.MigrationStatus.PendingCount > 0)
            {
                warnings.Add($"{health.MigrationStatus.PendingCount} pending migrations found");
            }

            // Validate database
            health.ValidationResult = await ValidateDatabaseAsync();
            if (!health.ValidationResult.IsValid)
            {
                issues.AddRange(health.ValidationResult.Issues);
            }

            // Collect metrics
            health.Metrics["DatabaseProvider"] = _databaseProvider.GetDatabaseProvider().ToString();
            health.Metrics["ConnectionString"] = MaskConnectionString(_databaseProvider.GetConnectionString());
            health.Metrics["Environment"] = _environment.EnvironmentName;

            if (health.ValidationResult.TableCounts.Any())
            {
                foreach (var tableCount in health.ValidationResult.TableCounts)
                {
                    health.Metrics[$"Table_{tableCount.Key}_Count"] = tableCount.Value;
                }
            }

            health.Warnings.AddRange(warnings);
            health.Errors.AddRange(issues);
            health.IsHealthy = issues.Count == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assess database health: {Message}", S(ex.Message));
            health.Errors.Add($"Health check failed: {S(ex.Message)}");
        }

        return health;
    }

    private async Task ValidateTable(string tableName, ValidationResult result, int minExpectedCount = 0)
    {
        var cleanTableName = S(tableName);
        try
        {
            var count = tableName switch
            {
                "Styles" => await _context.Styles.CountAsync(),
                "CreditPackages" => await _context.CreditPackages.CountAsync(),
                "AspNetRoles" => await _context.Roles.CountAsync(),
                "AspNetUsers" => await _context.Users.CountAsync(),
                "UserProfiles" => await _context.UserProfiles.CountAsync(),
                "ProcessedImages" => await _context.ProcessedImages.CountAsync(),
                _ => -1
            };

            if (count >= 0)
            {
                result.TableCounts[tableName] = count;

                if (count < minExpectedCount)
                {
                    result.Issues.Add($"Table {cleanTableName} has {count} records, expected at least {minExpectedCount}");
                }
            }
            else
            {
                result.Issues.Add($"Could not validate table {cleanTableName}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate table {TableName}", cleanTableName);
            result.Issues.Add($"Failed to access table {cleanTableName}: {S(ex.Message)}");
        }
    }

    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "Not configured";

        // Mask sensitive parts of connection string
        var parts = connectionString.Split(';');
        var maskedParts = parts.Select(part =>
        {
            if (part.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                part.Contains("Pwd", StringComparison.OrdinalIgnoreCase))
            {
                var keyValue = part.Split('=');
                return keyValue.Length == 2 ? $"{keyValue[0]}=***" : part;
            }
            return part;
        });

        return string.Join(";", maskedParts);
    }

    /// <summary>
    /// Apply migrations with intelligent conflict resolution for production databases
    /// </summary>
    private async Task ApplyMigrationsWithConflictResolutionAsync(IEnumerable<string> pendingMigrations)
    {
        var pendingList = pendingMigrations.ToList();

        // Check if this looks like a fresh deployment to existing database
        if (pendingList.Contains("20250808170932_InitialSQLServerFixed") &&
            pendingList.Contains("20250821035813_AddPredictionsTable"))
        {
            _logger.LogInformation("Detected existing database scenario - checking if schema already exists");

            // Check if the AspNetRoles table already exists
            var tableExistsQuery = @"
                SELECT CASE 
                    WHEN EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetRoles') 
                    THEN 1 ELSE 0 
                END";

            await using var connection = CreateStandaloneConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = tableExistsQuery;
            var tableExists = Convert.ToBoolean(await command.ExecuteScalarAsync());

            if (tableExists)
            {
                _logger.LogInformation("AspNetRoles table exists - marking initial migration as applied and applying only new tables");

                // Mark the initial migration as applied without running it
                await MarkMigrationAsAppliedAsync("20250808170932_InitialSQLServerFixed");

                // Now create only the Predictions table if it doesn't exist
                var createPredictionsTableQuery = @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Predictions')
                    BEGIN
                        CREATE TABLE [Predictions] (
                            [Id] nvarchar(450) NOT NULL,
                            [UserId] nvarchar(450) NOT NULL,
                            [Style] nvarchar(max) NULL,
                            [CreatedAt] datetime2 NOT NULL,
                            CONSTRAINT [PK_Predictions] PRIMARY KEY ([Id])
                        );
                        
                        CREATE NONCLUSTERED INDEX [IX_Predictions_UserId] ON [Predictions] ([UserId]);
                    END";

                command.CommandText = createPredictionsTableQuery;
                await command.ExecuteNonQueryAsync();

                // Mark the Predictions table migration as applied
                await MarkMigrationAsAppliedAsync("20250821035813_AddPredictionsTable");

                _logger.LogInformation("Successfully applied schema-safe migrations");
                return;
            }
        }

        // Default migration behavior for normal scenarios
        _logger.LogInformation("Applying migrations using standard Entity Framework approach");
        await _context.Database.MigrateAsync();
    }

    /// <summary>
    /// Mark a migration as applied in the migration history without running it
    /// </summary>
    private async Task MarkMigrationAsAppliedAsync(string migrationId)
    {
        var insertMigrationQuery = @"
            IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = @migrationId)
            BEGIN
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (@migrationId, '8.0.7');
            END";

        await using var connection = CreateStandaloneConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = insertMigrationQuery;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@migrationId";
        parameter.Value = migrationId;
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync();

        _logger.LogInformation("Marked migration {MigrationId} as applied", migrationId);
    }

    private DbConnection CreateStandaloneConnection()
    {
        var connectionString = _databaseProvider.GetConnectionString();
        return new SqlConnection(connectionString);
    }
}
