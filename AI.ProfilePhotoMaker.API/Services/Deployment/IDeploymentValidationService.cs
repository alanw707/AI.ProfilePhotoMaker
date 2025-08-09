using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services.Deployment;

/// <summary>
/// Service for comprehensive deployment validation and readiness checks
/// Ensures the application is ready for Azure deployment with all prerequisites met
/// </summary>
public interface IDeploymentValidationService
{
    /// <summary>
    /// Perform comprehensive pre-deployment validation
    /// Checks all components required for successful Azure deployment
    /// </summary>
    Task<DeploymentValidationResponseDto> ValidatePreDeploymentAsync();

    /// <summary>
    /// Perform post-deployment validation
    /// Verifies the deployment was successful and all services are functional
    /// </summary>
    Task<DeploymentValidationResponseDto> ValidatePostDeploymentAsync();

    /// <summary>
    /// Validate environment configuration for deployment
    /// Checks all required environment variables and configuration values
    /// </summary>
    Task<ConfigurationValidationResponseDto> ValidateConfigurationAsync();

    /// <summary>
    /// Validate performance baselines meet deployment criteria
    /// Ensures the system meets minimum performance requirements
    /// </summary>
    Task<PerformanceValidationResponseDto> ValidatePerformanceBaselinesAsync();

    /// <summary>
    /// Validate security configuration for production deployment
    /// Checks security settings, credentials, and compliance requirements
    /// </summary>
    Task<SecurityValidationResponseDto> ValidateSecurityConfigurationAsync();

    /// <summary>
    /// Validate Azure service connectivity and configuration
    /// Tests connectivity to Azure SQL, Blob Storage, Application Insights, etc.
    /// </summary>
    Task<AzureValidationResponseDto> ValidateAzureServicesAsync();

    /// <summary>
    /// Validate database schema and data readiness
    /// Ensures database is properly migrated and seeded for production
    /// </summary>
    Task<DatabaseValidationResponseDto> ValidateDatabaseReadinessAsync();

    /// <summary>
    /// Perform deployment health regression testing
    /// Validates that core functionality works after deployment
    /// </summary>
    Task<RegressionValidationResponseDto> ValidateRegressionTestsAsync();

    /// <summary>
    /// Get deployment readiness score
    /// Provides an overall readiness percentage and recommendations
    /// </summary>
    Task<DeploymentReadinessScoreDto> GetDeploymentReadinessScoreAsync();
}

/// <summary>
/// Service for continuous deployment monitoring
/// Monitors deployment health and detects configuration drift
/// </summary>
public interface IDeploymentMonitoringService
{
    /// <summary>
    /// Monitor deployment health continuously
    /// Tracks key metrics and alerts on issues
    /// </summary>
    Task<DeploymentHealthDto> GetDeploymentHealthAsync();

    /// <summary>
    /// Detect configuration drift from baseline
    /// Compares current configuration with expected baseline
    /// </summary>
    Task<ConfigurationDriftResponseDto> DetectConfigurationDriftAsync();

    /// <summary>
    /// Validate service availability continuously
    /// Monitors external service connectivity and response times
    /// </summary>
    Task<ServiceAvailabilityResponseDto> ValidateServiceAvailabilityAsync();

    /// <summary>
    /// Check for performance regression
    /// Compares current performance with deployment baseline
    /// </summary>
    Task<PerformanceRegressionResponseDto> CheckPerformanceRegressionAsync();
}