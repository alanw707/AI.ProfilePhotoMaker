using System.Diagnostics;
using System.Text.RegularExpressions;
using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Health;
using AI.ProfilePhotoMaker.API.Services.Monitoring;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;
using AI.ProfilePhotoMaker.API.Data;

namespace AI.ProfilePhotoMaker.API.Services.Deployment;

/// <summary>
/// Comprehensive deployment validation service
/// Provides thorough validation for Azure deployment readiness
/// </summary>
public partial class DeploymentValidationService : IDeploymentValidationService
{
    private readonly IHealthCheckService _healthCheckService;
    private readonly IPerformanceMonitoringService _performanceMonitoring;
    private readonly IDependencyHealthService _dependencyHealthService;
    private readonly IStorageService _storageService;
    private readonly EnvironmentConfiguration _environmentConfiguration;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DeploymentValidationService> _logger;
    private readonly IWebHostEnvironment _environment;

    public DeploymentValidationService(
        IHealthCheckService healthCheckService,
        IPerformanceMonitoringService performanceMonitoring,
        IDependencyHealthService dependencyHealthService,
        IStorageService storageService,
        EnvironmentConfiguration environmentConfiguration,
        IConfiguration configuration,
        ApplicationDbContext dbContext,
        ILogger<DeploymentValidationService> logger,
        IWebHostEnvironment environment)
    {
        _healthCheckService = healthCheckService;
        _performanceMonitoring = performanceMonitoring;
        _dependencyHealthService = dependencyHealthService;
        _storageService = storageService;
        _environmentConfiguration = environmentConfiguration;
        _configuration = configuration;
        _dbContext = dbContext;
        _logger = logger;
        _environment = environment;
    }

    public async Task<DeploymentValidationResponseDto> ValidatePreDeploymentAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new DeploymentValidationResponseDto
        {
            DeploymentTarget = "Azure Container Apps",
            Environment = _environment.EnvironmentName,
            Version = GetVersion()
        };

        try
        {
            _logger.LogInformation("Starting comprehensive pre-deployment validation");

            var validationTasks = new Dictionary<string, Task<ValidationComponentDto>>
            {
                ["configuration"] = ValidateConfigurationComponentAsync(),
                ["security"] = ValidateSecurityComponentAsync(),
                ["database"] = ValidateDatabaseComponentAsync(),
                ["storage"] = ValidateStorageComponentAsync(),
                ["externalServices"] = ValidateExternalServicesComponentAsync(),
                ["performance"] = ValidatePerformanceComponentAsync(),
                ["azure"] = ValidateAzureComponentAsync()
            };

            await Task.WhenAll(validationTasks.Values);

            foreach (var task in validationTasks)
            {
                response.Components[task.Key] = await task.Value;
            }

            // Aggregate results
            var allValid = response.Components.Values.All(c => c.IsValid);
            var anyFailed = response.Components.Values.Any(c => !c.IsValid);

            response.IsValid = allValid;
            response.Status = anyFailed ? "ValidationFailed" : "ValidationPassed";

            // Collect errors and warnings
            foreach (var component in response.Components.Values)
            {
                if (!string.IsNullOrEmpty(component.Error))
                {
                    response.Errors.Add($"{component.Description}: {component.Error}");
                }
                response.Warnings.AddRange(component.Warnings);
            }

            // Generate recommendations
            response.Recommendations = GeneratePreDeploymentRecommendations(response.Components);

            stopwatch.Stop();
            response.Duration = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Pre-deployment validation completed: {Status} in {Duration}ms",
                response.Status, response.Duration);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Pre-deployment validation failed");

            response.IsValid = false;
            response.Status = "ValidationError";
            response.Duration = stopwatch.ElapsedMilliseconds;
            response.Errors.Add($"Validation failed: {ex.Message}");

            return response;
        }
    }

    public async Task<DeploymentValidationResponseDto> ValidatePostDeploymentAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new DeploymentValidationResponseDto
        {
            DeploymentTarget = "Azure Container Apps",
            Environment = _environment.EnvironmentName,
            Version = GetVersion()
        };

        try
        {
            _logger.LogInformation("Starting post-deployment validation");

            var validationTasks = new Dictionary<string, Task<ValidationComponentDto>>
            {
                ["health"] = ValidateHealthComponentAsync(),
                ["connectivity"] = ValidateConnectivityComponentAsync(),
                ["functionality"] = ValidateFunctionalityComponentAsync(),
                ["performance"] = ValidatePerformanceComponentAsync(),
                ["monitoring"] = ValidateMonitoringComponentAsync(),
                ["security"] = ValidateRuntimeSecurityComponentAsync()
            };

            await Task.WhenAll(validationTasks.Values);

            foreach (var task in validationTasks)
            {
                response.Components[task.Key] = await task.Value;
            }

            var allValid = response.Components.Values.All(c => c.IsValid);
            response.IsValid = allValid;
            response.Status = allValid ? "DeploymentSuccessful" : "DeploymentIssuesDetected";

            // Collect errors and warnings
            foreach (var component in response.Components.Values)
            {
                if (!string.IsNullOrEmpty(component.Error))
                {
                    response.Errors.Add($"{component.Description}: {component.Error}");
                }
                response.Warnings.AddRange(component.Warnings);
            }

            response.Recommendations = GeneratePostDeploymentRecommendations(response.Components);

            stopwatch.Stop();
            response.Duration = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Post-deployment validation completed: {Status} in {Duration}ms",
                response.Status, response.Duration);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Post-deployment validation failed");

            response.IsValid = false;
            response.Status = "ValidationError";
            response.Duration = stopwatch.ElapsedMilliseconds;
            response.Errors.Add($"Validation failed: {ex.Message}");

            return response;
        }
    }

    public async Task<ConfigurationValidationResponseDto> ValidateConfigurationAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new ConfigurationValidationResponseDto
        {
            Environment = _environment.EnvironmentName,
            Version = GetVersion()
        };

        try
        {
            _logger.LogInformation("Starting configuration validation");

            // Validate environment variables
            var envVars = await ValidateEnvironmentVariablesAsync();
            response.EnvironmentVariables = envVars.Item1;
            response.MissingRequired = envVars.Item2;

            // Validate configuration sections
            response.ConfigurationSections = await ValidateConfigurationSectionsAsync();

            // Check for defaults being used
            response.UsingDefaults = CheckForDefaultValues();

            // Overall validation status
            response.IsValid = response.MissingRequired.Count == 0 &&
                              response.EnvironmentVariables.Values.All(v => v.IsValid);

            response.Status = response.IsValid ? "ConfigurationValid" : "ConfigurationInvalid";

            if (!response.IsValid)
            {
                response.Errors.AddRange(response.MissingRequired.Select(m => $"Missing required configuration: {m}"));
                response.Errors.AddRange(response.EnvironmentVariables.Values
                    .Where(v => !v.IsValid)
                    .Select(v => $"{v.Name}: {v.ValidationMessage}"));
            }

            stopwatch.Stop();
            response.Duration = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Configuration validation completed: {Status} in {Duration}ms",
                response.Status, response.Duration);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Configuration validation failed");

            response.IsValid = false;
            response.Status = "ValidationError";
            response.Duration = stopwatch.ElapsedMilliseconds;
            response.Errors.Add($"Configuration validation failed: {ex.Message}");

            return response;
        }
    }

    public async Task<PerformanceValidationResponseDto> ValidatePerformanceBaselinesAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new PerformanceValidationResponseDto
        {
            Environment = _environment.EnvironmentName,
            Version = GetVersion(),
            Baseline = new PerformanceBaselineDto
            {
                MaxResponseTimeMs = 1000,
                MaxErrorRatePercent = 5.0,
                MinThroughputRps = 10,
                MaxMemoryUsagePercent = 80.0,
                MaxCpuUsagePercent = 70.0,
                MaxDatabaseQueryTimeMs = 500,
                ConcurrentUserTarget = 200
            }
        };

        try
        {
            _logger.LogInformation("Starting performance baseline validation");

            // Get current performance metrics
            var currentMetrics = await _performanceMonitoring.GetCurrentMetricsAsync();

            response.TestResults = new PerformanceTestResultDto
            {
                AverageResponseTimeMs = (int)currentMetrics.ApiMetrics.AverageResponseTime,
                ErrorRatePercent = currentMetrics.ApiMetrics.ErrorRate,
                ThroughputRps = (int)currentMetrics.ApiMetrics.RequestsPerSecond,
                MemoryUsagePercent = currentMetrics.SystemMetrics.Memory.UsagePercentage,
                CpuUsagePercent = currentMetrics.SystemMetrics.Cpu.UsagePercentage,
                AverageDatabaseQueryTimeMs = (int)currentMetrics.DatabaseMetrics.AverageQueryTime,
                TestTimestamp = DateTime.UtcNow
            };

            // Validate against baselines
            response.Issues = ValidatePerformanceMetrics(response.Baseline, response.TestResults);
            response.MeetsRequirements = response.Issues.Count == 0;

            response.IsValid = response.MeetsRequirements;
            response.Status = response.IsValid ? "PerformanceBaselinesValid" : "PerformanceIssuesDetected";

            if (!response.IsValid)
            {
                response.Errors.AddRange(response.Issues.Select(i => $"{i.Category}: {i.Description}"));
                response.Recommendations.AddRange(response.Issues.Select(i => i.Recommendation));
            }

            stopwatch.Stop();
            response.Duration = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Performance validation completed: {Status} in {Duration}ms",
                response.Status, response.Duration);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Performance validation failed");

            response.IsValid = false;
            response.Status = "ValidationError";
            response.Duration = stopwatch.ElapsedMilliseconds;
            response.Errors.Add($"Performance validation failed: {ex.Message}");

            return response;
        }
    }

    public async Task<SecurityValidationResponseDto> ValidateSecurityConfigurationAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new SecurityValidationResponseDto
        {
            Environment = _environment.EnvironmentName,
            Version = GetVersion()
        };

        try
        {
            _logger.LogInformation("Starting security configuration validation");

            var securityChecks = new List<SecurityCheckDto>();

            // Validate JWT configuration
            securityChecks.AddRange(await ValidateJwtSecurityAsync());

            // Validate HTTPS configuration
            securityChecks.AddRange(ValidateHttpsConfiguration());

            // Validate API security headers
            securityChecks.AddRange(ValidateSecurityHeaders());

            // Validate database security
            securityChecks.AddRange(await ValidateDatabaseSecurityAsync());

            // Validate external service security
            securityChecks.AddRange(ValidateExternalServiceSecurity());

            // Validate environment variable security
            securityChecks.AddRange(ValidateEnvironmentVariableSecurity());

            response.SecurityChecks = securityChecks;
            response.HasSecurityIssues = securityChecks.Any(c => !c.Passed && (c.Severity == "High" || c.Severity == "Critical"));

            // Calculate security score
            var totalChecks = securityChecks.Count;
            var passedChecks = securityChecks.Count(c => c.Passed);
            var securityScorePercent = totalChecks > 0 ? (double)passedChecks / totalChecks * 100 : 0;
            response.SecurityScore = $"{securityScorePercent:F1}% ({passedChecks}/{totalChecks} checks passed)";

            response.IsValid = !response.HasSecurityIssues;
            response.Status = response.IsValid ? "SecurityValidationPassed" : "SecurityIssuesDetected";

            var failedChecks = securityChecks.Where(c => !c.Passed).ToList();
            response.Errors.AddRange(failedChecks.Where(c => c.Severity == "Critical" || c.Severity == "High")
                .Select(c => $"{c.Category}: {c.Description}"));
            response.Warnings.AddRange(failedChecks.Where(c => c.Severity == "Medium" || c.Severity == "Low")
                .Select(c => $"{c.Category}: {c.Description}"));
            response.Recommendations.AddRange(failedChecks.Select(c => c.Recommendation).Distinct());

            stopwatch.Stop();
            response.Duration = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Security validation completed: {Status} with {Score} in {Duration}ms",
                response.Status, response.SecurityScore, response.Duration);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Security validation failed");

            response.IsValid = false;
            response.Status = "ValidationError";
            response.Duration = stopwatch.ElapsedMilliseconds;
            response.Errors.Add($"Security validation failed: {ex.Message}");

            return response;
        }
    }

    public async Task<AzureValidationResponseDto> ValidateAzureServicesAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new AzureValidationResponseDto
        {
            Environment = _environment.EnvironmentName,
            Version = GetVersion()
        };

        try
        {
            _logger.LogInformation("Starting Azure services validation");

            var serviceValidations = new Dictionary<string, Task<AzureServiceStatusDto>>
            {
                ["Azure SQL Database"] = ValidateAzureSqlAsync(),
                ["Azure Blob Storage"] = ValidateAzureBlobStorageAsync(),
                ["Application Insights"] = ValidateApplicationInsightsAsync(),
                ["Azure Key Vault"] = ValidateAzureKeyVaultAsync(),
                ["Azure Container Registry"] = ValidateAzureContainerRegistryAsync()
            };

            await Task.WhenAll(serviceValidations.Values);

            foreach (var validation in serviceValidations)
            {
                response.Services[validation.Key] = await validation.Value;
            }

            response.FailedServices = response.Services.Values
                .Where(s => !s.IsHealthy)
                .Select(s => s.ServiceName)
                .ToList();

            response.AllServicesHealthy = response.FailedServices.Count == 0;
            response.IsValid = response.AllServicesHealthy;
            response.Status = response.IsValid ? "AzureServicesHealthy" : "AzureServiceIssuesDetected";

            if (!response.IsValid)
            {
                response.Errors.AddRange(response.Services.Values
                    .Where(s => !s.IsHealthy)
                    .Select(s => $"{s.ServiceName}: {s.ErrorMessage}"));
            }

            // Add resource health information
            response.ResourceHealth = await GetAzureResourceHealthAsync();

            stopwatch.Stop();
            response.Duration = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Azure services validation completed: {Status} in {Duration}ms",
                response.Status, response.Duration);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Azure services validation failed");

            response.IsValid = false;
            response.Status = "ValidationError";
            response.Duration = stopwatch.ElapsedMilliseconds;
            response.Errors.Add($"Azure services validation failed: {ex.Message}");

            return response;
        }
    }

    public async Task<DatabaseValidationResponseDto> ValidateDatabaseReadinessAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new DatabaseValidationResponseDto
        {
            Environment = _environment.EnvironmentName,
            Version = GetVersion()
        };

        try
        {
            _logger.LogInformation("Starting database readiness validation");

            // Validate database schema
            response.Schema = await ValidateDatabaseSchemaAsync();

            // Validate required data
            response.Data = await ValidateDatabaseDataAsync();

            // Validate database performance
            response.Performance = await ValidateDatabasePerformanceAsync();

            // Determine overall readiness
            response.IsProductionReady = response.Schema.IsUpToDate &&
                                       response.Data.HasRequiredSeedData &&
                                       response.Data.IsDataConsistent &&
                                       !response.Performance.PerformanceIssuesDetected;

            response.IsValid = response.IsProductionReady;
            response.Status = response.IsValid ? "DatabaseReady" : "DatabaseNotReady";

            // Collect issues
            if (response.Schema.PendingMigrations > 0)
            {
                response.Errors.Add($"Database has {response.Schema.PendingMigrations} pending migrations");
                response.Recommendations.Add("Apply pending database migrations before deployment");
            }

            if (!response.Data.HasRequiredSeedData)
            {
                response.Errors.Add("Database is missing required seed data");
                response.Recommendations.Add("Run seed data scripts to populate required reference data");
            }

            if (response.Performance.PerformanceIssuesDetected)
            {
                response.Warnings.Add($"Database performance issues detected: {response.Performance.SlowQueries.Count} slow queries");
                response.Recommendations.Add("Review and optimize slow database queries before deployment");
            }

            stopwatch.Stop();
            response.Duration = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Database validation completed: {Status} in {Duration}ms",
                response.Status, response.Duration);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Database validation failed");

            response.IsValid = false;
            response.Status = "ValidationError";
            response.Duration = stopwatch.ElapsedMilliseconds;
            response.Errors.Add($"Database validation failed: {ex.Message}");

            return response;
        }
    }

    public async Task<RegressionValidationResponseDto> ValidateRegressionTestsAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new RegressionValidationResponseDto
        {
            Environment = _environment.EnvironmentName,
            Version = GetVersion()
        };

        try
        {
            _logger.LogInformation("Starting regression validation tests");

            var regressionTests = new List<RegressionTestResultDto>();

            // API functionality tests
            regressionTests.AddRange(await RunApiRegressionTestsAsync());

            // Database functionality tests
            regressionTests.AddRange(await RunDatabaseRegressionTestsAsync());

            // External service integration tests
            regressionTests.AddRange(await RunExternalServiceRegressionTestsAsync());

            // Performance regression tests
            regressionTests.AddRange(await RunPerformanceRegressionTestsAsync());

            // Security regression tests
            regressionTests.AddRange(await RunSecurityRegressionTestsAsync());

            response.TestResults = regressionTests;
            response.PassedTests = regressionTests.Count(t => t.Passed);
            response.FailedTests = regressionTests.Count(t => !t.Passed);
            response.SkippedTests = 0; // No skipped tests in this implementation

            response.TestsByCategory = regressionTests
                .GroupBy(t => t.Category)
                .ToDictionary(g => g.Key, g => g.ToList());

            response.AllTestsPassed = response.FailedTests == 0;
            response.IsValid = response.AllTestsPassed;
            response.Status = response.IsValid ? "RegressionTestsPassed" : "RegressionTestsFailed";

            if (!response.IsValid)
            {
                var criticalFailures = regressionTests.Where(t => !t.Passed && t.Severity == "Critical").ToList();
                response.Errors.AddRange(criticalFailures.Select(t => $"{t.Category}.{t.TestName}: {t.ErrorMessage}"));

                var highFailures = regressionTests.Where(t => !t.Passed && t.Severity == "High").ToList();
                response.Warnings.AddRange(highFailures.Select(t => $"{t.Category}.{t.TestName}: {t.ErrorMessage}"));
            }

            stopwatch.Stop();
            response.Duration = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Regression tests completed: {Status} - {Passed}/{Total} passed in {Duration}ms",
                response.Status, response.PassedTests, regressionTests.Count, response.Duration);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Regression validation failed");

            response.IsValid = false;
            response.Status = "ValidationError";
            response.Duration = stopwatch.ElapsedMilliseconds;
            response.Errors.Add($"Regression validation failed: {ex.Message}");

            return response;
        }
    }

    public async Task<DeploymentReadinessScoreDto> GetDeploymentReadinessScoreAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Calculating deployment readiness score");

            // Run all validations
            var configValidation = await ValidateConfigurationAsync();
            var securityValidation = await ValidateSecurityConfigurationAsync();
            var performanceValidation = await ValidatePerformanceBaselinesAsync();
            var azureValidation = await ValidateAzureServicesAsync();
            var databaseValidation = await ValidateDatabaseReadinessAsync();

            var componentScores = new Dictionary<string, ComponentScoreDto>
            {
                ["Configuration"] = CalculateComponentScore("Configuration", configValidation.IsValid, 25, configValidation.Errors, configValidation.Recommendations),
                ["Security"] = CalculateComponentScore("Security", securityValidation.IsValid, 25, securityValidation.Errors, securityValidation.Recommendations),
                ["Performance"] = CalculateComponentScore("Performance", performanceValidation.IsValid, 20, performanceValidation.Errors, performanceValidation.Recommendations),
                ["Azure Services"] = CalculateComponentScore("Azure Services", azureValidation.IsValid, 20, azureValidation.Errors, azureValidation.Recommendations),
                ["Database"] = CalculateComponentScore("Database", databaseValidation.IsValid, 10, databaseValidation.Errors, databaseValidation.Recommendations)
            };

            var totalWeightedScore = componentScores.Values.Sum(c => c.WeightedScore);
            var totalWeight = componentScores.Values.Sum(c => c.Weight);
            var overallScore = totalWeight > 0 ? totalWeightedScore / totalWeight * 100 : 0;

            var response = new DeploymentReadinessScoreDto
            {
                OverallScore = Math.Round(overallScore, 1),
                ComponentScores = componentScores,
                ReadinessLevel = DetermineReadinessLevel(overallScore),
                IsReadyForDeployment = overallScore >= 80.0,
                CalculatedAt = DateTime.UtcNow
            };

            // Collect blocking issues
            response.BlockingIssues = componentScores.Values
                .Where(c => c.Score < 60)
                .SelectMany(c => c.Issues)
                .ToList();

            // Generate recommendations
            response.CriticalRecommendations = componentScores.Values
                .Where(c => c.Score < 80)
                .SelectMany(c => c.Recommendations)
                .Distinct()
                .Take(5)
                .ToList();

            response.OptionalRecommendations = componentScores.Values
                .Where(c => c.Score >= 80 && c.Score < 95)
                .SelectMany(c => c.Recommendations)
                .Distinct()
                .Take(3)
                .ToList();

            // Risk assessment
            response.RiskAssessment = CalculateRiskAssessment(componentScores, overallScore);

            // Next steps
            response.NextSteps = GenerateNextSteps(response);

            stopwatch.Stop();

            _logger.LogInformation("Deployment readiness calculated: {Score}% ({Level}) in {Duration}ms",
                response.OverallScore, response.ReadinessLevel, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to calculate deployment readiness score");

            return new DeploymentReadinessScoreDto
            {
                OverallScore = 0,
                ReadinessLevel = "Error",
                IsReadyForDeployment = false,
                BlockingIssues = { $"Readiness calculation failed: {ex.Message}" },
                CriticalRecommendations = { "Fix readiness calculation errors before deployment" },
                CalculatedAt = DateTime.UtcNow
            };
        }
    }

    private async Task<ValidationComponentDto> ValidateConfigurationComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var validation = await ValidateConfigurationAsync();
            stopwatch.Stop();

            return new ValidationComponentDto
            {
                Status = validation.IsValid ? "Valid" : "Invalid",
                IsValid = validation.IsValid,
                Description = "Environment and application configuration validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = validation.Errors.FirstOrDefault(),
                Warnings = validation.Warnings,
                Data = new Dictionary<string, object>
                {
                    ["missingRequired"] = validation.MissingRequired.Count,
                    ["usingDefaults"] = validation.UsingDefaults.Count,
                    ["configuredVariables"] = validation.EnvironmentVariables.Count(v => v.Value.IsConfigured)
                },
                RequiredActions = validation.MissingRequired.Select(m => $"Configure {m}").ToList()
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ValidationComponentDto
            {
                Status = "Error",
                IsValid = false,
                Description = "Environment and application configuration validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<ValidationComponentDto> ValidateSecurityComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var validation = await ValidateSecurityConfigurationAsync();
            stopwatch.Stop();

            return new ValidationComponentDto
            {
                Status = validation.IsValid ? "Secure" : "SecurityIssues",
                IsValid = validation.IsValid,
                Description = "Security configuration and compliance validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = validation.Errors.FirstOrDefault(),
                Warnings = validation.Warnings,
                Data = new Dictionary<string, object>
                {
                    ["securityScore"] = validation.SecurityScore,
                    ["checksCount"] = validation.SecurityChecks.Count,
                    ["failedChecks"] = validation.SecurityChecks.Count(c => !c.Passed)
                },
                RequiredActions = validation.Recommendations.Take(3).ToList()
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ValidationComponentDto
            {
                Status = "Error",
                IsValid = false,
                Description = "Security configuration and compliance validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<ValidationComponentDto> ValidateDatabaseComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var validation = await ValidateDatabaseReadinessAsync();
            stopwatch.Stop();

            return new ValidationComponentDto
            {
                Status = validation.IsValid ? "Ready" : "NotReady",
                IsValid = validation.IsValid,
                Description = "Database schema, data, and performance validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = validation.Errors.FirstOrDefault(),
                Warnings = validation.Warnings,
                Data = new Dictionary<string, object>
                {
                    ["pendingMigrations"] = validation.Schema.PendingMigrations,
                    ["hasSeedData"] = validation.Data.HasRequiredSeedData,
                    ["performanceIssues"] = validation.Performance.PerformanceIssuesDetected
                },
                RequiredActions = validation.Recommendations.Take(3).ToList()
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ValidationComponentDto
            {
                Status = "Error",
                IsValid = false,
                Description = "Database schema, data, and performance validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<ValidationComponentDto> ValidateStorageComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Test storage connectivity by attempting to check if a test path exists
            var canConnect = true;
            try
            {
                await _storageService.ExistsAsync("health-check-test");
            }
            catch
            {
                canConnect = false;
            }
            stopwatch.Stop();

            return new ValidationComponentDto
            {
                Status = canConnect ? "Connected" : "Disconnected",
                IsValid = canConnect,
                Description = "Storage service connectivity and configuration validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = canConnect ? null : "Unable to connect to storage service",
                Data = new Dictionary<string, object>
                {
                    ["storageType"] = _storageService.GetType().Name,
                    ["connected"] = canConnect
                },
                RequiredActions = canConnect ? new List<string>() : new List<string> { "Check storage service configuration and connectivity" }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ValidationComponentDto
            {
                Status = "Error",
                IsValid = false,
                Description = "Storage service connectivity and configuration validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<ValidationComponentDto> ValidateExternalServicesComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var dependencies = await _dependencyHealthService.CheckDependenciesAsync();
            var healthyCount = dependencies.Values.Count(d => d.Status == "Healthy");
            var totalCount = dependencies.Count;
            var allHealthy = healthyCount == totalCount;

            stopwatch.Stop();

            return new ValidationComponentDto
            {
                Status = allHealthy ? "Healthy" : "Degraded",
                IsValid = healthyCount > totalCount / 2, // At least half must be healthy
                Description = "External service dependencies validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Warnings = dependencies.Where(d => d.Value.Status != "Healthy").Select(d => $"{d.Key}: {d.Value.Error}").ToList(),
                Data = new Dictionary<string, object>
                {
                    ["totalServices"] = totalCount,
                    ["healthyServices"] = healthyCount,
                    ["services"] = dependencies.Keys.ToList()
                },
                RequiredActions = dependencies
                    .Where(d => d.Value.Status != "Healthy")
                    .Select(d => $"Fix {d.Key} connectivity")
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ValidationComponentDto
            {
                Status = "Error",
                IsValid = false,
                Description = "External service dependencies validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<ValidationComponentDto> ValidatePerformanceComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var validation = await ValidatePerformanceBaselinesAsync();
            stopwatch.Stop();

            return new ValidationComponentDto
            {
                Status = validation.IsValid ? "MeetsBaseline" : "BelowBaseline",
                IsValid = validation.IsValid,
                Description = "Performance baseline and capacity validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = validation.Errors.FirstOrDefault(),
                Warnings = validation.Issues.Where(i => i.Severity != "Critical").Select(i => i.Description).ToList(),
                Data = new Dictionary<string, object>
                {
                    ["responseTime"] = validation.TestResults.AverageResponseTimeMs,
                    ["errorRate"] = validation.TestResults.ErrorRatePercent,
                    ["memoryUsage"] = validation.TestResults.MemoryUsagePercent,
                    ["issuesCount"] = validation.Issues.Count
                },
                RequiredActions = validation.Issues.Where(i => i.Severity == "Critical").Select(i => i.Recommendation).ToList()
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ValidationComponentDto
            {
                Status = "Error",
                IsValid = false,
                Description = "Performance baseline and capacity validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<ValidationComponentDto> ValidateAzureComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var validation = await ValidateAzureServicesAsync();
            stopwatch.Stop();

            return new ValidationComponentDto
            {
                Status = validation.IsValid ? "Connected" : "ConnectionIssues",
                IsValid = validation.IsValid,
                Description = "Azure services connectivity and configuration validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = validation.Errors.FirstOrDefault(),
                Data = new Dictionary<string, object>
                {
                    ["totalServices"] = validation.Services.Count,
                    ["healthyServices"] = validation.Services.Values.Count(s => s.IsHealthy),
                    ["failedServices"] = validation.FailedServices.Count
                },
                RequiredActions = validation.FailedServices.Select(s => $"Fix {s} connectivity").ToList()
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ValidationComponentDto
            {
                Status = "Error",
                IsValid = false,
                Description = "Azure services connectivity and configuration validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    // Additional helper methods would continue here...
    // Due to length constraints, I'll include key remaining methods in the next part

    private static string GetVersion()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    // More implementation methods would continue...
    // This includes the remaining validation methods, helper methods, and the remaining interface implementations
}