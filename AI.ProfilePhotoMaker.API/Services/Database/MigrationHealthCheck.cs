using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AI.ProfilePhotoMaker.API.Services.Database;

/// <summary>
/// Health check for database migration status
/// </summary>
public class MigrationHealthCheck : IHealthCheck
{
    private readonly IMigrationService _migrationService;
    private readonly ILogger<MigrationHealthCheck> _logger;
    private static string S(string? value) => LoggingSanitizer.Sanitize(value);

    public MigrationHealthCheck(IMigrationService migrationService, ILogger<MigrationHealthCheck> logger)
    {
        _migrationService = migrationService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var migrationStatus = await _migrationService.GetMigrationStatusAsync();

            var data = new Dictionary<string, object>
            {
                ["can_connect"] = migrationStatus.CanConnect,
                ["database_exists"] = migrationStatus.DatabaseExists,
                ["applied_count"] = migrationStatus.AppliedCount,
                ["pending_count"] = migrationStatus.PendingCount
            };

            if (!migrationStatus.CanConnect)
            {
                return HealthCheckResult.Unhealthy("Cannot connect to database for migration check", data: data);
            }

            if (migrationStatus.PendingCount > 0)
            {
                data["pending_migrations"] = migrationStatus.PendingMigrations;
                return HealthCheckResult.Degraded($"{migrationStatus.PendingCount} pending migrations found", data: data);
            }

            return HealthCheckResult.Healthy("All migrations applied", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration health check failed: {Message}", S(ex.Message));
            return HealthCheckResult.Unhealthy($"Migration health check failed: {S(ex.Message)}");
        }
    }
}
