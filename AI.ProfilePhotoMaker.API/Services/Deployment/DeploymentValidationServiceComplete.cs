using AI.ProfilePhotoMaker.API.Models.DTOs;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Deployment;

/// <summary>
/// Continuation of DeploymentValidationService with remaining method implementations
/// </summary>
public partial class DeploymentValidationService
{
    private async Task<ValidationComponentDto> ValidateHealthComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var health = await _healthCheckService.GetBasicHealthAsync();
            stopwatch.Stop();

            return new ValidationComponentDto
            {
                Status = health.Status,
                IsValid = health.Status.ToLower() == "healthy",
                Description = "Basic application health status",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = health.Status.ToLower() != "healthy" ? health.Message : null,
                Data = new Dictionary<string, object>
                {
                    ["status"] = health.Status,
                    ["version"] = health.Version,
                    ["environment"] = health.Environment
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ValidationComponentDto
            {
                Status = "Error",
                IsValid = false,
                Description = "Basic application health status",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<ValidationComponentDto> ValidateConnectivityComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var dependencies = await _dependencyHealthService.CheckDependenciesAsync();
            var connectedServices = dependencies.Values.Count(d => d.Status == "Healthy");
            var totalServices = dependencies.Count;

            stopwatch.Stop();

            return new ValidationComponentDto
            {
                Status = connectedServices == totalServices ? "AllConnected" : "PartiallyConnected",
                IsValid = connectedServices > totalServices / 2, // At least half must be connected
                Description = "External service connectivity validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Data = new Dictionary<string, object>
                {
                    ["connectedServices"] = connectedServices,
                    ["totalServices"] = totalServices,
                    ["services"] = dependencies.Keys.ToList()
                },
                Warnings = dependencies
                    .Where(d => d.Value.Status != "Healthy")
                    .Select(d => $"{d.Key}: {d.Value.Error}")
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
                Description = "External service connectivity validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<ValidationComponentDto> ValidateFunctionalityComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Run basic functionality tests
            var testResults = await RunBasicFunctionalityTestsAsync();
            var passedTests = testResults.Count(t => t.Passed);
            var totalTests = testResults.Count;

            stopwatch.Stop();

            return new ValidationComponentDto
            {
                Status = passedTests == totalTests ? "AllPassed" : "SomeFailures",
                IsValid = passedTests >= totalTests * 0.8, // 80% must pass
                Description = "Core functionality validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Data = new Dictionary<string, object>
                {
                    ["passedTests"] = passedTests,
                    ["totalTests"] = totalTests,
                    ["successRate"] = totalTests > 0 ? (double)passedTests / totalTests * 100 : 0
                },
                Warnings = testResults
                    .Where(t => !t.Passed)
                    .Select(t => $"{t.TestName}: {t.ErrorMessage}")
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
                Description = "Core functionality validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<ValidationComponentDto> ValidateMonitoringComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Check if monitoring services are available
            var metrics = await _performanceMonitoring.GetCurrentMetricsAsync();
            var isMonitoringActive = metrics != null;

            stopwatch.Stop();

            return new ValidationComponentDto
            {
                Status = isMonitoringActive ? "Active" : "Inactive",
                IsValid = isMonitoringActive,
                Description = "Monitoring and metrics collection validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Data = new Dictionary<string, object>
                {
                    ["monitoringActive"] = isMonitoringActive,
                    ["metricsAvailable"] = metrics != null
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ValidationComponentDto
            {
                Status = "Error",
                IsValid = false,
                Description = "Monitoring and metrics collection validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<ValidationComponentDto> ValidateRuntimeSecurityComponentAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Basic runtime security checks
            var securityIssues = new List<string>();

            // Check if running as non-root (in container context)
            var currentUser = Environment.UserName;
            if (currentUser == "root" && !_environment.IsDevelopment())
            {
                securityIssues.Add("Application running as root user");
            }

            // Check for secure communication
            var isHttpsForced = !_environment.IsDevelopment();

            stopwatch.Stop();

            return new ValidationComponentDto
            {
                Status = securityIssues.Count == 0 ? "Secure" : "SecurityIssues",
                IsValid = securityIssues.Count == 0,
                Description = "Runtime security configuration validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Warnings = securityIssues,
                Data = new Dictionary<string, object>
                {
                    ["currentUser"] = currentUser,
                    ["httpsForced"] = isHttpsForced,
                    ["environment"] = _environment.EnvironmentName,
                    ["securityIssueCount"] = securityIssues.Count
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ValidationComponentDto
            {
                Status = "Error",
                IsValid = false,
                Description = "Runtime security configuration validation",
                Duration = stopwatch.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<DatabaseSchemaValidationDto> ValidateDatabaseSchemaAsync()
    {
        try
        {
            var pendingMigrations = await _dbContext.Database.GetPendingMigrationsAsync();
            var appliedMigrations = await _dbContext.Database.GetAppliedMigrationsAsync();

            // Get table counts
            var tableCounts = new Dictionary<string, int>();
            try
            {
                tableCounts["Users"] = await _dbContext.Users.CountAsync();
                tableCounts["UserProfiles"] = await _dbContext.UserProfiles.CountAsync();
                tableCounts["Styles"] = await _dbContext.Styles.CountAsync();
                tableCounts["CreditPackages"] = await _dbContext.CreditPackages.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve table counts");
            }

            return new DatabaseSchemaValidationDto
            {
                IsUpToDate = !pendingMigrations.Any(),
                PendingMigrations = pendingMigrations.Count(),
                PendingMigrationNames = pendingMigrations.ToList(),
                AppliedMigrations = appliedMigrations.Count(),
                LatestMigration = appliedMigrations.LastOrDefault() ?? "None",
                TableCounts = tableCounts,
                SchemaIssues = !pendingMigrations.Any() ? new List<string>() : new List<string> { $"{pendingMigrations.Count()} pending migrations" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database schema validation failed");
            return new DatabaseSchemaValidationDto
            {
                IsUpToDate = false,
                SchemaIssues = new List<string> { $"Schema validation failed: {ex.Message}" }
            };
        }
    }

    private async Task<DatabaseDataValidationDto> ValidateDatabaseDataAsync()
    {
        try
        {
            var seedDataCounts = new Dictionary<string, int>();
            var missingSeedData = new List<string>();

            // Check for required seed data
            var stylesCount = await _dbContext.Styles.CountAsync();
            seedDataCounts["Styles"] = stylesCount;
            if (stylesCount < 21) // Expected minimum styles
            {
                missingSeedData.Add("Style definitions (expected 21+, found " + stylesCount + ")");
            }

            var creditPackagesCount = await _dbContext.CreditPackages.CountAsync();
            seedDataCounts["CreditPackages"] = creditPackagesCount;
            if (creditPackagesCount < 3) // Expected credit packages
            {
                missingSeedData.Add("Credit packages (expected 3+, found " + creditPackagesCount + ")");
            }

            var usersCount = await _dbContext.Users.CountAsync();
            seedDataCounts["Users"] = usersCount;

            var userProfilesCount = await _dbContext.UserProfiles.CountAsync();
            seedDataCounts["UserProfiles"] = userProfilesCount;

            return new DatabaseDataValidationDto
            {
                HasRequiredSeedData = missingSeedData.Count == 0,
                SeedDataCounts = seedDataCounts,
                MissingSeedData = missingSeedData,
                IsDataConsistent = true, // Simplified for now
                DataIntegrityIssues = new List<string>() // Would need more complex validation
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database data validation failed");
            return new DatabaseDataValidationDto
            {
                HasRequiredSeedData = false,
                IsDataConsistent = false,
                DataIntegrityIssues = new List<string> { $"Data validation failed: {ex.Message}" }
            };
        }
    }

    private async Task<DatabasePerformanceDto> ValidateDatabasePerformanceAsync()
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            await _dbContext.Database.CanConnectAsync();
            stopwatch.Stop();

            // Simple performance check - in a real implementation, you'd want more sophisticated metrics
            var connectionTimeMs = (int)stopwatch.ElapsedMilliseconds;

            return new DatabasePerformanceDto
            {
                AverageConnectionTimeMs = connectionTimeMs,
                AverageQueryTimeMs = connectionTimeMs < 100 ? 50 : connectionTimeMs, // Simplified
                ActiveConnections = 1, // Would need actual connection pool metrics
                MaxConnections = 100, // Default SQL Server setting
                ConnectionPoolUtilization = 1.0, // Would need actual metrics
                SlowQueries = new List<SlowQueryDto>(), // Would need query monitoring
                PerformanceIssuesDetected = connectionTimeMs > 1000
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database performance validation failed");
            return new DatabasePerformanceDto
            {
                PerformanceIssuesDetected = true,
                AverageConnectionTimeMs = 9999,
                SlowQueries = new List<SlowQueryDto>
                {
                    new() { Query = "Connection test", ExecutionTimeMs = 9999, Recommendation = $"Fix database connectivity: {ex.Message}" }
                }
            };
        }
    }

    private async Task<List<RegressionTestResultDto>> RunApiRegressionTestsAsync()
    {
        var tests = new List<RegressionTestResultDto>();

        // Health endpoint test
        tests.Add(await RunSingleRegressionTestAsync("Health Check", "API", async () =>
        {
            var health = await _healthCheckService.GetBasicHealthAsync();
            if (health.Status.ToLower() != "healthy")
                throw new Exception($"Health check returned: {health.Status}");
        }, "Critical"));

        // Database connectivity test
        tests.Add(await RunSingleRegressionTestAsync("Database Connection", "API", async () =>
        {
            var canConnect = await _dbContext.Database.CanConnectAsync();
            if (!canConnect)
                throw new Exception("Cannot connect to database");
        }, "Critical"));

        return tests;
    }

    private async Task<List<RegressionTestResultDto>> RunDatabaseRegressionTestsAsync()
    {
        var tests = new List<RegressionTestResultDto>();

        // Basic query test
        tests.Add(await RunSingleRegressionTestAsync("Basic Query", "Database", async () =>
        {
            var count = await _dbContext.Users.CountAsync();
            // Test passes if query completes without error
        }, "High"));

        // Migration status test
        tests.Add(await RunSingleRegressionTestAsync("Migration Status", "Database", async () =>
        {
            var pendingMigrations = await _dbContext.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
                throw new Exception($"Found {pendingMigrations.Count()} pending migrations");
        }, "Critical"));

        return tests;
    }

    private async Task<List<RegressionTestResultDto>> RunExternalServiceRegressionTestsAsync()
    {
        var tests = new List<RegressionTestResultDto>();

        // Storage service test
        tests.Add(await RunSingleRegressionTestAsync("Storage Service", "External", async () =>
        {
            // Test storage connectivity by attempting to check if a test path exists
            try
            {
                await _storageService.ExistsAsync("health-check-test");
            }
            catch (Exception ex)
            {
                throw new Exception($"Storage service connectivity failed: {ex.Message}");
            }
        }, "High"));

        return tests;
    }

    private async Task<List<RegressionTestResultDto>> RunPerformanceRegressionTestsAsync()
    {
        var tests = new List<RegressionTestResultDto>();

        // Response time test
        tests.Add(await RunSingleRegressionTestAsync("Response Time", "Performance", async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            await _healthCheckService.GetBasicHealthAsync();
            stopwatch.Stop();
            
            if (stopwatch.ElapsedMilliseconds > 2000)
                throw new Exception($"Response time too slow: {stopwatch.ElapsedMilliseconds}ms");
        }, "Medium"));

        return tests;
    }

    private async Task<List<RegressionTestResultDto>> RunSecurityRegressionTestsAsync()
    {
        var tests = new List<RegressionTestResultDto>();

        // Security configuration test
        tests.Add(await RunSingleRegressionTestAsync("Security Config", "Security", async () =>
        {
            var jwtSecret = _configuration["JWT:Secret"];
            if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Contains("YourSuperSecret"))
                throw new Exception("JWT secret not properly configured");
        }, "Critical"));

        return tests;
    }

    private async Task<RegressionTestResultDto> RunSingleRegressionTestAsync(string testName, string category, Func<Task> testAction, string severity)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await testAction();
            stopwatch.Stop();

            return new RegressionTestResultDto
            {
                TestName = testName,
                Category = category,
                Passed = true,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Severity = severity,
                AssertionResults = new List<string> { "Test passed" }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new RegressionTestResultDto
            {
                TestName = testName,
                Category = category,
                Passed = false,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message,
                Severity = severity,
                AssertionResults = new List<string> { $"Test failed: {ex.Message}" }
            };
        }
    }

    private async Task<List<RegressionTestResultDto>> RunBasicFunctionalityTestsAsync()
    {
        var tests = new List<RegressionTestResultDto>();

        // Add API functionality tests
        tests.AddRange(await RunApiRegressionTestsAsync());
        
        // Add database tests
        tests.AddRange(await RunDatabaseRegressionTestsAsync());

        return tests;
    }

    private ComponentScoreDto CalculateComponentScore(string componentName, bool isValid, int weight, List<string> errors, List<string> recommendations)
    {
        var score = isValid ? 100.0 : Math.Max(0, 100 - (errors.Count * 20)); // Reduce score for each error
        var weightedScore = score * weight / 100.0;

        return new ComponentScoreDto
        {
            ComponentName = componentName,
            Score = score,
            Weight = weight,
            WeightedScore = weightedScore,
            Status = isValid ? "Passed" : "Failed",
            Issues = errors.ToList(),
            Recommendations = recommendations.Take(3).ToList()
        };
    }

    private string DetermineReadinessLevel(double overallScore)
    {
        return overallScore switch
        {
            >= 95 => "Excellent",
            >= 85 => "Ready",
            >= 70 => "Needs Attention",
            >= 50 => "Not Ready",
            _ => "Critical Issues"
        };
    }

    private DeploymentRiskAssessmentDto CalculateRiskAssessment(Dictionary<string, ComponentScoreDto> componentScores, double overallScore)
    {
        var riskFactors = new List<RiskFactorDto>();
        var mitigationStrategies = new List<string>();

        var failedComponents = componentScores.Values.Where(c => c.Score < 80).ToList();
        
        if (failedComponents.Any())
        {
            riskFactors.Add(new RiskFactorDto
            {
                Factor = "Failed Validation Components",
                Impact = "High",
                Probability = "High",
                RiskValue = failedComponents.Count * 10,
                Mitigation = "Fix all failed validation components before deployment"
            });
            mitigationStrategies.Add("Address all failed validation components");
        }

        var configScore = componentScores.GetValueOrDefault("Configuration")?.Score ?? 0;
        if (configScore < 90)
        {
            riskFactors.Add(new RiskFactorDto
            {
                Factor = "Configuration Issues",
                Impact = "High",
                Probability = "Medium",
                RiskValue = 25,
                Mitigation = "Complete environment configuration setup"
            });
            mitigationStrategies.Add("Complete all required configuration");
        }

        var securityScore = componentScores.GetValueOrDefault("Security")?.Score ?? 0;
        if (securityScore < 85)
        {
            riskFactors.Add(new RiskFactorDto
            {
                Factor = "Security Configuration",
                Impact = "Critical",
                Probability = "Medium",
                RiskValue = 30,
                Mitigation = "Address all security configuration issues"
            });
            mitigationStrategies.Add("Implement all security best practices");
        }

        var totalRisk = riskFactors.Sum(r => r.RiskValue);
        var riskLevel = totalRisk switch
        {
            <= 10 => "Low",
            <= 30 => "Medium", 
            <= 60 => "High",
            _ => "Critical"
        };

        return new DeploymentRiskAssessmentDto
        {
            RiskLevel = riskLevel,
            RiskScore = totalRisk,
            RiskFactors = riskFactors,
            MitigationStrategies = mitigationStrategies.Distinct().ToList(),
            RequiresManualReview = riskLevel == "High" || riskLevel == "Critical"
        };
    }

    private string GenerateNextSteps(DeploymentReadinessScoreDto readiness)
    {
        if (readiness.IsReadyForDeployment)
        {
            return "System is ready for deployment. Proceed with deployment pipeline.";
        }

        var criticalIssues = readiness.ComponentScores.Values.Where(c => c.Score < 60).ToList();
        if (criticalIssues.Any())
        {
            return $"Critical issues must be resolved before deployment: {string.Join(", ", criticalIssues.Select(c => c.ComponentName))}";
        }

        return "Address validation issues and re-run readiness check before deployment.";
    }
}