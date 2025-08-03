using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AI.ProfilePhotoMaker.API.Services.Database;

/// <summary>
/// Health check for database connectivity
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDatabaseProviderService _databaseProvider;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(IDatabaseProviderService databaseProvider, ILogger<DatabaseHealthCheck> logger)
    {
        _databaseProvider = databaseProvider;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _databaseProvider.CanConnectAsync();
            var providerConfig = _databaseProvider.GetProviderConfig();
            
            var data = new Dictionary<string, object>
            {
                ["provider"] = providerConfig.Provider.ToString(),
                ["connection_masked"] = MaskConnectionString(providerConfig.ConnectionString),
                ["can_connect"] = canConnect
            };

            if (canConnect)
            {
                return HealthCheckResult.Healthy("Database connection is healthy", data);
            }
            else
            {
                return HealthCheckResult.Unhealthy("Cannot connect to database", data: data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy($"Database health check failed: {ex.Message}");
        }
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