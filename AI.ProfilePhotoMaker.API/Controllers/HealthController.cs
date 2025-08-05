using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Health;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Health check endpoints for monitoring and validation
/// Provides comprehensive system health information for CI/CD, monitoring, and troubleshooting
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IHealthCheckService healthCheckService,
        ILogger<HealthController> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    /// <summary>
    /// Basic application health check
    /// Simple alive/dead status for basic monitoring
    /// </summary>
    /// <returns>Basic health status</returns>
    /// <response code="200">Application is healthy</response>
    /// <response code="503">Application is unhealthy</response>
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(HealthCheckResponseDto), (int)HttpStatusCode.ServiceUnavailable)]
    public async Task<ActionResult<HealthCheckResponseDto>> GetHealthAsync()
    {
        _logger.LogDebug("Basic health check requested");
        
        var health = await _healthCheckService.GetBasicHealthAsync();
        
        var httpStatusCode = health.Status.ToLower() switch
        {
            "healthy" => HttpStatusCode.OK,
            "degraded" => HttpStatusCode.OK, // Still consider degraded as OK for basic health
            _ => HttpStatusCode.ServiceUnavailable
        };

        _logger.LogDebug("Basic health check completed: {Status} in {Duration}ms", 
            health.Status, health.Duration);

        return StatusCode((int)httpStatusCode, health);
    }

    /// <summary>
    /// Comprehensive system health check
    /// Detailed health information including all components and metrics
    /// </summary>
    /// <returns>Comprehensive health status with component details</returns>
    /// <response code="200">System is healthy or degraded but functional</response>
    /// <response code="503">System is unhealthy</response>
    [HttpGet("comprehensive")]
    [ProducesResponseType(typeof(ComprehensiveHealthResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ComprehensiveHealthResponseDto), (int)HttpStatusCode.ServiceUnavailable)]
    public async Task<ActionResult<ComprehensiveHealthResponseDto>> GetComprehensiveHealthAsync()
    {
        _logger.LogDebug("Comprehensive health check requested");
        
        var health = await _healthCheckService.GetComprehensiveHealthAsync();
        
        var httpStatusCode = health.Status.ToLower() switch
        {
            "healthy" or "degraded" => HttpStatusCode.OK,
            _ => HttpStatusCode.ServiceUnavailable
        };

        _logger.LogInformation("Comprehensive health check completed: {Status} in {Duration}ms with {ComponentCount} components", 
            health.Status, health.Duration, health.Components.Count);

        return StatusCode((int)httpStatusCode, health);
    }

    /// <summary>
    /// Database health check
    /// Database connectivity, migration status, and data validation
    /// </summary>
    /// <returns>Database health status</returns>
    /// <response code="200">Database is healthy</response>
    /// <response code="503">Database is unhealthy</response>
    [HttpGet("database")]
    [ProducesResponseType(typeof(DatabaseHealthResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(DatabaseHealthResponseDto), (int)HttpStatusCode.ServiceUnavailable)]
    public async Task<ActionResult<DatabaseHealthResponseDto>> GetDatabaseHealthAsync()
    {
        _logger.LogDebug("Database health check requested");
        
        var health = await _healthCheckService.GetDatabaseHealthAsync();
        
        var httpStatusCode = health.Status.ToLower() switch
        {
            "healthy" => HttpStatusCode.OK,
            _ => HttpStatusCode.ServiceUnavailable
        };

        _logger.LogDebug("Database health check completed: {Status}, CanConnect: {CanConnect}, PendingMigrations: {PendingMigrations}", 
            health.Status, health.CanConnect, health.Migrations?.PendingCount ?? 0);

        return StatusCode((int)httpStatusCode, health);
    }

    /// <summary>
    /// Storage health check
    /// Azure Blob Storage or Local Storage connectivity and operations
    /// </summary>
    /// <returns>Storage health status</returns>
    /// <response code="200">Storage is healthy</response>
    /// <response code="503">Storage is unhealthy</response>
    [HttpGet("storage")]
    [ProducesResponseType(typeof(StorageHealthResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(StorageHealthResponseDto), (int)HttpStatusCode.ServiceUnavailable)]
    public async Task<ActionResult<StorageHealthResponseDto>> GetStorageHealthAsync()
    {
        _logger.LogDebug("Storage health check requested");
        
        var health = await _healthCheckService.GetStorageHealthAsync();
        
        var httpStatusCode = health.Status.ToLower() switch
        {
            "healthy" => HttpStatusCode.OK,
            _ => HttpStatusCode.ServiceUnavailable
        };

        _logger.LogDebug("Storage health check completed: {Status}, CanConnect: {CanConnect}, Provider: {Provider}", 
            health.Status, health.CanConnect, health.Provider);

        return StatusCode((int)httpStatusCode, health);
    }

    /// <summary>
    /// External dependencies health check
    /// Replicate API, Stripe, Google OAuth, and other external services
    /// </summary>
    /// <returns>External dependencies health status</returns>
    /// <response code="200">Dependencies are healthy or degraded but functional</response>
    /// <response code="503">Critical dependencies are unhealthy</response>
    [HttpGet("dependencies")]
    [ProducesResponseType(typeof(DependenciesHealthResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(DependenciesHealthResponseDto), (int)HttpStatusCode.ServiceUnavailable)]
    public async Task<ActionResult<DependenciesHealthResponseDto>> GetDependenciesHealthAsync()
    {
        _logger.LogDebug("Dependencies health check requested");
        
        var health = await _healthCheckService.GetDependenciesHealthAsync();
        
        // For dependencies, we're more lenient - only fail if ALL critical dependencies are down
        var httpStatusCode = health.Status.ToLower() switch
        {
            "healthy" or "degraded" => HttpStatusCode.OK,
            _ => HttpStatusCode.ServiceUnavailable
        };

        _logger.LogDebug("Dependencies health check completed: {Status} with {DependencyCount} dependencies", 
            health.Status, health.Dependencies.Count);

        return StatusCode((int)httpStatusCode, health);
    }

    /// <summary>
    /// Readiness probe for Kubernetes
    /// Determines if the application is ready to accept traffic
    /// </summary>
    /// <returns>Readiness status</returns>
    /// <response code="200">Application is ready to accept traffic</response>
    /// <response code="503">Application is not ready</response>
    [HttpGet("ready")]
    [ProducesResponseType(typeof(HealthCheckResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(HealthCheckResponseDto), (int)HttpStatusCode.ServiceUnavailable)]
    public async Task<ActionResult<HealthCheckResponseDto>> GetReadinessAsync()
    {
        _logger.LogDebug("Readiness probe requested");
        
        var health = await _healthCheckService.GetReadinessAsync();
        
        var httpStatusCode = health.Status.ToLower() switch
        {
            "ready" => HttpStatusCode.OK,
            _ => HttpStatusCode.ServiceUnavailable
        };

        _logger.LogDebug("Readiness probe completed: {Status}", health.Status);

        return StatusCode((int)httpStatusCode, health);
    }

    /// <summary>
    /// Liveness probe for Kubernetes
    /// Determines if the application is alive and should be restarted if not
    /// </summary>
    /// <returns>Liveness status</returns>
    /// <response code="200">Application is alive</response>
    /// <response code="503">Application should be restarted</response>
    [HttpGet("live")]
    [ProducesResponseType(typeof(HealthCheckResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(HealthCheckResponseDto), (int)HttpStatusCode.ServiceUnavailable)]
    public async Task<ActionResult<HealthCheckResponseDto>> GetLivenessAsync()
    {
        _logger.LogDebug("Liveness probe requested");
        
        var health = await _healthCheckService.GetLivenessAsync();
        
        var httpStatusCode = health.Status.ToLower() switch
        {
            "alive" => HttpStatusCode.OK,
            _ => HttpStatusCode.ServiceUnavailable
        };

        _logger.LogDebug("Liveness probe completed: {Status}", health.Status);

        return StatusCode((int)httpStatusCode, health);
    }

    /// <summary>
    /// Migration status check
    /// Specific endpoint for checking database migration status
    /// </summary>
    /// <returns>Migration status information</returns>
    /// <response code="200">Migration status retrieved successfully</response>
    /// <response code="503">Cannot retrieve migration status</response>
    [HttpGet("migration")]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.ServiceUnavailable)]
    public async Task<ActionResult<object>> GetMigrationStatusAsync()
    {
        _logger.LogDebug("Migration status check requested");
        
        try
        {
            var health = await _healthCheckService.GetDatabaseHealthAsync();
            
            if (health.Migrations == null)
            {
                return StatusCode((int)HttpStatusCode.ServiceUnavailable, new
                {
                    status = "Unavailable",
                    message = "Cannot retrieve migration status",
                    timestamp = DateTime.UtcNow
                });
            }

            var status = health.Migrations.PendingCount == 0 ? "UpToDate" : "PendingMigrations";
            var httpStatusCode = status == "UpToDate" ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;

            var response = new
            {
                status,
                appliedCount = health.Migrations.AppliedCount,
                pendingCount = health.Migrations.PendingCount,
                pendingMigrations = health.Migrations.PendingMigrations,
                latestMigration = health.Migrations.LatestMigration,
                timestamp = DateTime.UtcNow
            };

            _logger.LogDebug("Migration status check completed: {Status}, Applied: {Applied}, Pending: {Pending}", 
                status, health.Migrations.AppliedCount, health.Migrations.PendingCount);

            return StatusCode((int)httpStatusCode, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration status check failed");
            
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new
            {
                status = "Error",
                message = "Migration status check failed",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Data validation check
    /// Validates that required seed data exists in the database
    /// </summary>
    /// <returns>Data validation results</returns>
    /// <response code="200">Data validation completed successfully</response>
    /// <response code="503">Data validation failed or found issues</response>
    [HttpGet("data")]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.ServiceUnavailable)]
    public async Task<ActionResult<object>> GetDataValidationAsync()
    {
        _logger.LogDebug("Data validation check requested");
        
        try
        {
            var health = await _healthCheckService.GetDatabaseHealthAsync();
            
            if (health.Validation == null)
            {
                return StatusCode((int)HttpStatusCode.ServiceUnavailable, new
                {
                    status = "Unavailable",
                    message = "Cannot retrieve data validation status",
                    timestamp = DateTime.UtcNow
                });
            }

            var status = health.Validation.IsValid ? "Valid" : "Invalid";
            var httpStatusCode = health.Validation.IsValid ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;

            var response = new
            {
                status,
                isValid = health.Validation.IsValid,
                hasRequiredSeedData = health.Validation.HasRequiredSeedData,
                tableCounts = health.Validation.TableCounts,
                missingSeedData = health.Validation.MissingSeedData,
                issues = health.Validation.Issues,
                expectedCounts = new
                {
                    styles = "21+",
                    creditPackages = "3+",
                    users = "0+"
                },
                timestamp = DateTime.UtcNow
            };

            _logger.LogDebug("Data validation check completed: {Status}, HasRequiredSeedData: {HasSeedData}, Issues: {IssueCount}", 
                status, health.Validation.HasRequiredSeedData, health.Validation.Issues.Count);

            return StatusCode((int)httpStatusCode, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data validation check failed");
            
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new
            {
                status = "Error",
                message = "Data validation check failed",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }
}

/// <summary>
/// Simple health check endpoint (legacy compatibility)
/// Provides a basic health check at /health for simple monitoring tools
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthSimpleController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly ILogger<HealthSimpleController> _logger;

    public HealthSimpleController(
        IHealthCheckService healthCheckService,
        ILogger<HealthSimpleController> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    /// <summary>
    /// Simple health check endpoint
    /// Basic alive/dead status for simple monitoring tools
    /// </summary>
    /// <returns>Simple health status</returns>
    /// <response code="200">Application is healthy</response>
    /// <response code="503">Application is unhealthy</response>
    [HttpGet]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.ServiceUnavailable)]
    public async Task<ActionResult<object>> GetAsync()
    {
        try
        {
            var health = await _healthCheckService.GetBasicHealthAsync();
            
            var httpStatusCode = health.Status.ToLower() switch
            {
                "healthy" => HttpStatusCode.OK,
                _ => HttpStatusCode.ServiceUnavailable
            };

            var response = new
            {
                status = health.Status,
                timestamp = health.Timestamp
            };

            return StatusCode((int)httpStatusCode, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Simple health check failed");
            
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new
            {
                status = "Error",
                timestamp = DateTime.UtcNow
            });
        }
    }
}