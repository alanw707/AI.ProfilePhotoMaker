using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services.Health;

/// <summary>
/// Service for comprehensive health checks
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Perform basic application health check
    /// </summary>
    Task<HealthCheckResponseDto> GetBasicHealthAsync();

    /// <summary>
    /// Perform comprehensive system health check
    /// </summary>
    Task<ComprehensiveHealthResponseDto> GetComprehensiveHealthAsync();

    /// <summary>
    /// Check database health
    /// </summary>
    Task<DatabaseHealthResponseDto> GetDatabaseHealthAsync();

    /// <summary>
    /// Check storage health
    /// </summary>
    Task<StorageHealthResponseDto> GetStorageHealthAsync();

    /// <summary>
    /// Check external dependencies health
    /// </summary>
    Task<DependenciesHealthResponseDto> GetDependenciesHealthAsync();

    /// <summary>
    /// Get readiness probe status (for Kubernetes)
    /// </summary>
    Task<HealthCheckResponseDto> GetReadinessAsync();

    /// <summary>
    /// Get liveness probe status (for Kubernetes)
    /// </summary>
    Task<HealthCheckResponseDto> GetLivenessAsync();
}

/// <summary>
/// Service for database-specific health checks
/// </summary>
public interface IDatabaseHealthService
{
    /// <summary>
    /// Check database connectivity
    /// </summary>
    Task<bool> CanConnectAsync();

    /// <summary>
    /// Check migration status
    /// </summary>
    Task<MigrationStatusDto> GetMigrationStatusAsync();

    /// <summary>
    /// Validate required data exists
    /// </summary>
    Task<DataValidationDto> ValidateDataAsync();

    /// <summary>
    /// Get database metrics
    /// </summary>
    Task<Dictionary<string, object>> GetMetricsAsync();
}

/// <summary>
/// Service for storage-specific health checks
/// </summary>
public interface IStorageHealthService
{
    /// <summary>
    /// Test storage connectivity
    /// </summary>
    Task<bool> CanConnectAsync();

    /// <summary>
    /// Perform storage operations test
    /// </summary>
    Task<Dictionary<string, bool>> PerformOperationTestsAsync();

    /// <summary>
    /// Get storage configuration info
    /// </summary>
    Task<Dictionary<string, object>> GetConfigurationAsync();
}

/// <summary>
/// Service for external dependencies health checks
/// </summary>
public interface IDependencyHealthService
{
    /// <summary>
    /// Check all external dependencies
    /// </summary>
    Task<Dictionary<string, DependencyStatusDto>> CheckDependenciesAsync();

    /// <summary>
    /// Check specific dependency
    /// </summary>
    Task<DependencyStatusDto> CheckDependencyAsync(string dependencyName);
}