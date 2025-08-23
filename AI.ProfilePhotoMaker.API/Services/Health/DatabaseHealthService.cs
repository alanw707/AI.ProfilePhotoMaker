using System.Diagnostics;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Database;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Health;

/// <summary>
/// Database health check service implementation
/// </summary>
public class DatabaseHealthService : IDatabaseHealthService
{
    private readonly ApplicationDbContext _context;
    private readonly IMigrationService _migrationService;
    private readonly IDatabaseProviderService _databaseProvider;
    private readonly ILogger<DatabaseHealthService> _logger;

    public DatabaseHealthService(
        ApplicationDbContext context,
        IMigrationService migrationService,
        IDatabaseProviderService databaseProvider,
        ILogger<DatabaseHealthService> logger)
    {
        _context = context;
        _migrationService = migrationService;
        _databaseProvider = databaseProvider;
        _logger = logger;
    }

    public async Task<bool> CanConnectAsync()
    {
        try
        {
            // More aggressive timeout and proper cancellation
            using var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            // Use a task with explicit timeout to prevent hanging
            var connectTask = _context.Database.CanConnectAsync(cancellationToken.Token);
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(1500, cancellationToken.Token));

            if (completedTask == connectTask)
            {
                return await connectTask;
            }
            else
            {
                _logger.LogWarning("Database connectivity check timed out after 1.5 seconds");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database connectivity check failed: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<MigrationStatusDto> GetMigrationStatusAsync()
    {
        try
        {
            var migrationStatus = await _migrationService.GetMigrationStatusAsync();

            return new MigrationStatusDto
            {
                AppliedCount = migrationStatus.AppliedCount,
                PendingCount = migrationStatus.PendingCount,
                PendingMigrations = migrationStatus.PendingMigrations,
                LatestMigration = migrationStatus.AppliedMigrations.LastOrDefault()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get migration status");
            return new MigrationStatusDto
            {
                AppliedCount = 0,
                PendingCount = -1, // Indicates error
                PendingMigrations = new List<string> { "Error retrieving migration status" }
            };
        }
    }

    public async Task<DataValidationDto> ValidateDataAsync()
    {
        try
        {
            var validation = await _migrationService.ValidateDatabaseAsync();

            return new DataValidationDto
            {
                IsValid = validation.IsValid,
                TableCounts = validation.TableCounts,
                HasRequiredSeedData = validation.HasRequiredSeedData,
                MissingSeedData = validation.MissingSeedData,
                Issues = validation.Issues
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate database data");
            return new DataValidationDto
            {
                IsValid = false,
                Issues = new List<string> { $"Data validation failed: {ex.Message}" }
            };
        }
    }

    public async Task<Dictionary<string, object>> GetMetricsAsync()
    {
        var metrics = new Dictionary<string, object>();

        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Get provider information
            var providerConfig = _databaseProvider.GetProviderConfig();
            metrics["provider"] = providerConfig.Provider.ToString();
            metrics["commandTimeout"] = providerConfig.CommandTimeout;

            // Test basic query performance
            var queryStopwatch = Stopwatch.StartNew();
            var userCount = await _context.Users.CountAsync();
            queryStopwatch.Stop();
            metrics["userCount"] = userCount;
            metrics["queryResponseTimeMs"] = queryStopwatch.ElapsedMilliseconds;

            // Get table counts for monitoring
            var tableCounts = new Dictionary<string, int>();

            try
            {
                tableCounts["Users"] = await _context.Users.CountAsync();
                tableCounts["Styles"] = await _context.Styles.CountAsync();
                tableCounts["CreditPackages"] = await _context.CreditPackages.CountAsync();
                tableCounts["ProcessedImages"] = await _context.ProcessedImages.CountAsync();
                tableCounts["ModelCreationRequests"] = await _context.ModelCreationRequests.CountAsync();

                metrics["tableCounts"] = tableCounts;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get some table counts");
                metrics["tableCounts"] = tableCounts; // Include partial results
            }

            // Database size information (if available)
            try
            {
                if (providerConfig.Provider == DatabaseProvider.SQLite)
                {
                    var connectionString = providerConfig.ConnectionString;
                    if (connectionString.Contains("Data Source="))
                    {
                        var dbPath = connectionString.Split("Data Source=")[1].Split(';')[0];
                        if (File.Exists(dbPath))
                        {
                            var fileInfo = new FileInfo(dbPath);
                            metrics["databaseSizeMB"] = Math.Round(fileInfo.Length / 1024.0 / 1024.0, 2);
                        }
                    }
                }
                // For SQL Server, we could query sys.database_files, but that requires elevated permissions
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not determine database size");
            }

            stopwatch.Stop();
            metrics["metricsCollectionTimeMs"] = stopwatch.ElapsedMilliseconds;

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect database metrics");
            return new Dictionary<string, object>
            {
                ["error"] = "Failed to collect database metrics",
                ["errorMessage"] = ex.Message
            };
        }
    }
}