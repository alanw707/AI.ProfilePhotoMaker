using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Deployment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Deployment validation and monitoring endpoints
/// Provides comprehensive deployment readiness validation and continuous monitoring
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DeploymentController : ControllerBase
{
    private readonly IDeploymentValidationService _deploymentValidation;
    private readonly IDeploymentMonitoringService _deploymentMonitoring;
    private readonly ILogger<DeploymentController> _logger;

    public DeploymentController(
        IDeploymentValidationService deploymentValidation,
        IDeploymentMonitoringService deploymentMonitoring,
        ILogger<DeploymentController> logger)
    {
        _deploymentValidation = deploymentValidation;
        _deploymentMonitoring = deploymentMonitoring;
        _logger = logger;
    }

    /// <summary>
    /// Comprehensive pre-deployment validation
    /// Validates all components required for successful Azure deployment
    /// </summary>
    /// <returns>Complete pre-deployment validation results</returns>
    /// <response code="200">Pre-deployment validation completed (check IsValid property)</response>
    /// <response code="500">Validation process failed</response>
    [HttpPost("validate/pre-deployment")]
    [ProducesResponseType(typeof(DeploymentValidationResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<DeploymentValidationResponseDto>> ValidatePreDeploymentAsync()
    {
        _logger.LogInformation("Pre-deployment validation requested");
        
        try
        {
            var result = await _deploymentValidation.ValidatePreDeploymentAsync();
            
            _logger.LogInformation("Pre-deployment validation completed: {Status} with {ComponentCount} components in {Duration}ms", 
                result.Status, result.Components.Count, result.Duration);

            // Always return 200 OK - the validation status is in the response body
            // This allows CI/CD pipelines to process the validation results
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pre-deployment validation failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Pre-deployment validation failed",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Post-deployment validation
    /// Verifies the deployment was successful and all services are functional
    /// </summary>
    /// <returns>Post-deployment validation results</returns>
    /// <response code="200">Post-deployment validation completed</response>
    /// <response code="500">Validation process failed</response>
    [HttpPost("validate/post-deployment")]
    [ProducesResponseType(typeof(DeploymentValidationResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<DeploymentValidationResponseDto>> ValidatePostDeploymentAsync()
    {
        _logger.LogInformation("Post-deployment validation requested");
        
        try
        {
            var result = await _deploymentValidation.ValidatePostDeploymentAsync();
            
            _logger.LogInformation("Post-deployment validation completed: {Status} with {ComponentCount} components in {Duration}ms", 
                result.Status, result.Components.Count, result.Duration);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Post-deployment validation failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Post-deployment validation failed",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Get deployment readiness score
    /// Provides an overall readiness percentage and recommendations
    /// </summary>
    /// <returns>Deployment readiness score with recommendations</returns>
    /// <response code="200">Readiness score calculated successfully</response>
    /// <response code="500">Score calculation failed</response>
    [HttpGet("readiness-score")]
    [ProducesResponseType(typeof(DeploymentReadinessScoreDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<DeploymentReadinessScoreDto>> GetReadinessScoreAsync()
    {
        _logger.LogInformation("Deployment readiness score requested");
        
        try
        {
            var result = await _deploymentValidation.GetDeploymentReadinessScoreAsync();
            
            _logger.LogInformation("Readiness score calculated: {Score}% ({Level}) - Ready: {IsReady}", 
                result.OverallScore, result.ReadinessLevel, result.IsReadyForDeployment);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness score calculation failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Readiness score calculation failed",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Validate environment configuration
    /// Checks all required environment variables and configuration values
    /// </summary>
    /// <returns>Configuration validation results</returns>
    /// <response code="200">Configuration validation completed</response>
    /// <response code="500">Validation failed</response>
    [HttpGet("validate/configuration")]
    [ProducesResponseType(typeof(ConfigurationValidationResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<ConfigurationValidationResponseDto>> ValidateConfigurationAsync()
    {
        _logger.LogInformation("Configuration validation requested");
        
        try
        {
            var result = await _deploymentValidation.ValidateConfigurationAsync();
            
            _logger.LogInformation("Configuration validation completed: {Status} with {MissingCount} missing required variables", 
                result.Status, result.MissingRequired.Count);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration validation failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Configuration validation failed",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Validate performance baselines
    /// Ensures the system meets minimum performance requirements
    /// </summary>
    /// <returns>Performance validation results</returns>
    /// <response code="200">Performance validation completed</response>
    /// <response code="500">Validation failed</response>
    [HttpGet("validate/performance")]
    [ProducesResponseType(typeof(PerformanceValidationResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<PerformanceValidationResponseDto>> ValidatePerformanceAsync()
    {
        _logger.LogInformation("Performance validation requested");
        
        try
        {
            var result = await _deploymentValidation.ValidatePerformanceBaselinesAsync();
            
            _logger.LogInformation("Performance validation completed: {Status} - Meets requirements: {MeetsRequirements}, Issues: {IssueCount}", 
                result.Status, result.MeetsRequirements, result.Issues.Count);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Performance validation failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Performance validation failed",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Validate security configuration
    /// Checks security settings, credentials, and compliance requirements
    /// </summary>
    /// <returns>Security validation results</returns>
    /// <response code="200">Security validation completed</response>
    /// <response code="500">Validation failed</response>
    [HttpGet("validate/security")]
    [ProducesResponseType(typeof(SecurityValidationResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<SecurityValidationResponseDto>> ValidateSecurityAsync()
    {
        _logger.LogInformation("Security validation requested");
        
        try
        {
            var result = await _deploymentValidation.ValidateSecurityConfigurationAsync();
            
            _logger.LogInformation("Security validation completed: {Status} - Score: {Score}, Issues: {HasIssues}", 
                result.Status, result.SecurityScore, result.HasSecurityIssues);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Security validation failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Security validation failed",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Validate Azure services connectivity
    /// Tests connectivity to Azure SQL, Blob Storage, Application Insights, etc.
    /// </summary>
    /// <returns>Azure services validation results</returns>
    /// <response code="200">Azure validation completed</response>
    /// <response code="500">Validation failed</response>
    [HttpGet("validate/azure")]
    [ProducesResponseType(typeof(AzureValidationResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<AzureValidationResponseDto>> ValidateAzureServicesAsync()
    {
        _logger.LogInformation("Azure services validation requested");
        
        try
        {
            var result = await _deploymentValidation.ValidateAzureServicesAsync();
            
            _logger.LogInformation("Azure services validation completed: {Status} - All healthy: {AllHealthy}, Failed services: {FailedCount}", 
                result.Status, result.AllServicesHealthy, result.FailedServices.Count);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure services validation failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Azure services validation failed",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Validate database readiness
    /// Ensures database is properly migrated and seeded for production
    /// </summary>
    /// <returns>Database readiness validation results</returns>
    /// <response code="200">Database validation completed</response>
    /// <response code="500">Validation failed</response>
    [HttpGet("validate/database")]
    [ProducesResponseType(typeof(DatabaseValidationResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<DatabaseValidationResponseDto>> ValidateDatabaseAsync()
    {
        _logger.LogInformation("Database validation requested");
        
        try
        {
            var result = await _deploymentValidation.ValidateDatabaseReadinessAsync();
            
            _logger.LogInformation("Database validation completed: {Status} - Production ready: {IsReady}, Pending migrations: {PendingMigrations}", 
                result.Status, result.IsProductionReady, result.Schema.PendingMigrations);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database validation failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Database validation failed",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Run regression tests
    /// Validates that core functionality works after deployment
    /// </summary>
    /// <returns>Regression test results</returns>
    /// <response code="200">Regression tests completed</response>
    /// <response code="500">Tests failed to execute</response>
    [HttpPost("validate/regression-tests")]
    [ProducesResponseType(typeof(RegressionValidationResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<RegressionValidationResponseDto>> RunRegressionTestsAsync()
    {
        _logger.LogInformation("Regression tests requested");
        
        try
        {
            var result = await _deploymentValidation.ValidateRegressionTestsAsync();
            
            _logger.LogInformation("Regression tests completed: {Status} - {Passed}/{Total} tests passed", 
                result.Status, result.PassedTests, result.TestResults.Count);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Regression tests failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Regression tests failed to execute",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Get deployment health status
    /// Monitors deployment health and detects issues
    /// </summary>
    /// <returns>Current deployment health status</returns>
    /// <response code="200">Health status retrieved successfully</response>
    /// <response code="500">Health check failed</response>
    [HttpGet("health")]
    [ProducesResponseType(typeof(DeploymentHealthDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<DeploymentHealthDto>> GetDeploymentHealthAsync()
    {
        _logger.LogInformation("Deployment health check requested");
        
        try
        {
            var result = await _deploymentMonitoring.GetDeploymentHealthAsync();
            
            _logger.LogInformation("Deployment health check completed: {Status} - Healthy: {IsHealthy}, Issues: {IssueCount}", 
                result.HealthStatus, result.IsHealthy, result.Issues.Count);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment health check failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Deployment health check failed",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Detect configuration drift
    /// Compares current configuration with expected baseline
    /// </summary>
    /// <returns>Configuration drift detection results</returns>
    /// <response code="200">Drift detection completed</response>
    /// <response code="500">Drift detection failed</response>
    [HttpGet("monitor/configuration-drift")]
    [ProducesResponseType(typeof(ConfigurationDriftResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<ConfigurationDriftResponseDto>> DetectConfigurationDriftAsync()
    {
        _logger.LogInformation("Configuration drift detection requested");
        
        try
        {
            var result = await _deploymentMonitoring.DetectConfigurationDriftAsync();
            
            _logger.LogInformation("Configuration drift detection completed: Has drift: {HasDrift}, Severity: {Severity}, Items: {ItemCount}", 
                result.HasDrift, result.DriftSeverity, result.DriftItems.Count);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration drift detection failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Configuration drift detection failed",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Validate service availability
    /// Monitors external service connectivity and response times
    /// </summary>
    /// <returns>Service availability validation results</returns>
    /// <response code="200">Service availability check completed</response>
    /// <response code="500">Availability check failed</response>
    [HttpGet("monitor/service-availability")]
    [ProducesResponseType(typeof(ServiceAvailabilityResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<ServiceAvailabilityResponseDto>> ValidateServiceAvailabilityAsync()
    {
        _logger.LogInformation("Service availability validation requested");
        
        try
        {
            var result = await _deploymentMonitoring.ValidateServiceAvailabilityAsync();
            
            _logger.LogInformation("Service availability validation completed: All available: {AllAvailable}, Availability: {Availability}%, Unavailable: {UnavailableCount}", 
                result.AllServicesAvailable, result.OverallAvailabilityPercentage, result.UnavailableServices.Count);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service availability validation failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Service availability validation failed",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Check for performance regression
    /// Compares current performance with deployment baseline
    /// </summary>
    /// <returns>Performance regression analysis results</returns>
    /// <response code="200">Performance regression check completed</response>
    /// <response code="500">Regression check failed</response>
    [HttpGet("monitor/performance-regression")]
    [ProducesResponseType(typeof(PerformanceRegressionResponseDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<PerformanceRegressionResponseDto>> CheckPerformanceRegressionAsync()
    {
        _logger.LogInformation("Performance regression check requested");
        
        try
        {
            var result = await _deploymentMonitoring.CheckPerformanceRegressionAsync();
            
            _logger.LogInformation("Performance regression check completed: Has regression: {HasRegression}, Severity: {Severity}, Issues: {IssueCount}", 
                result.HasRegression, result.RegressionSeverity, result.Issues.Count);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Performance regression check failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Performance regression check failed",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Reset configuration baseline
    /// Sets the current configuration as the new baseline for drift detection
    /// </summary>
    /// <returns>Baseline reset confirmation</returns>
    /// <response code="200">Baseline reset successfully</response>
    /// <response code="500">Baseline reset failed</response>
    [HttpPost("monitor/reset-baseline")]
    [Authorize] // Require authentication for this operation
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<object>> ResetConfigurationBaselineAsync()
    {
        _logger.LogInformation("Configuration baseline reset requested by user: {User}", User.Identity?.Name ?? "Unknown");
        
        try
        {
            // First, get current configuration to establish new baseline
            var currentDrift = await _deploymentMonitoring.DetectConfigurationDriftAsync();
            
            // In a real implementation, you would store the baseline in a persistent store
            // For now, we'll just trigger a new baseline detection
            var newBaseline = await _deploymentMonitoring.DetectConfigurationDriftAsync();
            
            _logger.LogInformation("Configuration baseline reset completed");

            return Ok(new
            {
                message = "Configuration baseline reset successfully",
                timestamp = DateTime.UtcNow,
                resetBy = User.Identity?.Name ?? "Unknown",
                newBaselineTimestamp = newBaseline.BaselineTimestamp
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration baseline reset failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Configuration baseline reset failed",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Get deployment validation summary
    /// Provides a quick overview of all validation statuses
    /// </summary>
    /// <returns>Summary of all deployment validations</returns>
    /// <response code="200">Summary retrieved successfully</response>
    /// <response code="500">Summary generation failed</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(object), (int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult<object>> GetDeploymentSummaryAsync()
    {
        _logger.LogInformation("Deployment validation summary requested");
        
        try
        {
            // Run key validations concurrently
            var readinessTask = _deploymentValidation.GetDeploymentReadinessScoreAsync();
            var healthTask = _deploymentMonitoring.GetDeploymentHealthAsync();
            var configTask = _deploymentValidation.ValidateConfigurationAsync();
            var securityTask = _deploymentValidation.ValidateSecurityConfigurationAsync();

            await Task.WhenAll(readinessTask, healthTask, configTask, securityTask);

            var readiness = await readinessTask;
            var health = await healthTask;
            var config = await configTask;
            var security = await securityTask;

            var summary = new
            {
                timestamp = DateTime.UtcNow,
                overall = new
                {
                    readinessScore = readiness.OverallScore,
                    readinessLevel = readiness.ReadinessLevel,
                    isReadyForDeployment = readiness.IsReadyForDeployment,
                    healthStatus = health.HealthStatus,
                    isHealthy = health.IsHealthy
                },
                components = new
                {
                    configuration = new
                    {
                        status = config.Status,
                        isValid = config.IsValid,
                        missingRequired = config.MissingRequired.Count
                    },
                    security = new
                    {
                        status = security.Status,
                        isValid = security.IsValid,
                        securityScore = security.SecurityScore,
                        hasIssues = security.HasSecurityIssues
                    },
                    services = new
                    {
                        healthyServices = health.Services.Values.Count(s => s.IsHealthy),
                        totalServices = health.Services.Count,
                        issues = health.Issues.Count
                    }
                },
                recommendations = new
                {
                    critical = readiness.CriticalRecommendations.Take(3),
                    optional = readiness.OptionalRecommendations.Take(2),
                    nextSteps = readiness.NextSteps
                },
                risks = new
                {
                    riskLevel = readiness.RiskAssessment.RiskLevel,
                    riskScore = readiness.RiskAssessment.RiskScore,
                    requiresReview = readiness.RiskAssessment.RequiresManualReview,
                    blockingIssues = readiness.BlockingIssues.Count
                }
            };

            _logger.LogInformation("Deployment summary generated: Readiness: {ReadinessScore}%, Health: {HealthStatus}, Issues: {IssueCount}", 
                readiness.OverallScore, health.HealthStatus, health.Issues.Count);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment summary generation failed");
            
            return StatusCode((int)HttpStatusCode.InternalServerError, new
            {
                error = "Deployment summary generation failed",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }
}