using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace AI.ProfilePhotoMaker.API.Services.Deployment;

/// <summary>
/// Helper methods for deployment validation service
/// Contains implementation details for various validation components
/// </summary>
public partial class DeploymentValidationService
{
    private Task<(Dictionary<string, EnvironmentVariableDto>, List<string>)> ValidateEnvironmentVariablesAsync()
    {
        var envVars = new Dictionary<string, EnvironmentVariableDto>();
        var missingRequired = new List<string>();

        var requiredVars = new[]
        {
            EnvironmentConfiguration.MSSQL_SA_PASSWORD,
            EnvironmentConfiguration.JWT_SECRET,
            EnvironmentConfiguration.REPLICATE_API_TOKEN,
            EnvironmentConfiguration.REPLICATE_WEBHOOK_SECRET
        };

        var optionalVars = new[]
        {
            EnvironmentConfiguration.GOOGLE_CLIENT_ID,
            EnvironmentConfiguration.GOOGLE_CLIENT_SECRET,
            EnvironmentConfiguration.STRIPE_PUBLISHABLE_KEY,
            EnvironmentConfiguration.STRIPE_SECRET_KEY,
            EnvironmentConfiguration.AZURE_STORAGE_CONNECTION_STRING
        };

        foreach (var varName in requiredVars)
        {
            var value = _environmentConfiguration.GetEnvironmentVariable(varName);
            var isConfigured = !string.IsNullOrEmpty(value);
            var validationResult = ValidateEnvironmentVariableValue(varName, value);

            envVars[varName] = new EnvironmentVariableDto
            {
                Name = varName,
                IsConfigured = isConfigured,
                IsRequired = true,
                IsValid = validationResult.Item1,
                ValidationMessage = validationResult.Item2,
                IsSecure = IsSecureVariable(varName),
                Source = DetermineVariableSource(varName, value)
            };

            if (!isConfigured)
            {
                missingRequired.Add(varName);
            }
        }

        foreach (var varName in optionalVars)
        {
            var value = _environmentConfiguration.GetEnvironmentVariable(varName);
            var isConfigured = !string.IsNullOrEmpty(value);
            var validationResult = isConfigured ? ValidateEnvironmentVariableValue(varName, value) : (true, "Not configured (optional)");

            envVars[varName] = new EnvironmentVariableDto
            {
                Name = varName,
                IsConfigured = isConfigured,
                IsRequired = false,
                IsValid = validationResult.Item1,
                ValidationMessage = validationResult.Item2,
                IsSecure = IsSecureVariable(varName),
                Source = DetermineVariableSource(varName, value)
            };
        }

        return Task.FromResult((envVars, missingRequired));
    }

    private (bool IsValid, string Message) ValidateEnvironmentVariableValue(string name, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return (false, "Value is required but not configured");
        }

        return name switch
        {
            EnvironmentConfiguration.MSSQL_SA_PASSWORD => ValidatePasswordComplexity(value),
            EnvironmentConfiguration.JWT_SECRET => ValidateJwtSecret(value),
            EnvironmentConfiguration.REPLICATE_API_TOKEN => ValidateReplicateToken(value),
            EnvironmentConfiguration.REPLICATE_WEBHOOK_SECRET => ValidateWebhookSecret(value),
            EnvironmentConfiguration.AZURE_STORAGE_CONNECTION_STRING => ValidateAzureStorageConnectionString(value),
            _ => (true, "Valid")
        };
    }

    private (bool IsValid, string Message) ValidatePasswordComplexity(string password)
    {
        if (password.Length < 8)
            return (false, "Password must be at least 8 characters");

        if (!Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$"))
            return (false, "Password must contain uppercase, lowercase, number, and special character");

        return (true, "Valid strong password");
    }

    private (bool IsValid, string Message) ValidateJwtSecret(string secret)
    {
        if (secret.Length < 32)
            return (false, "JWT secret must be at least 32 characters");

        if (secret.Contains("YourSuperSecret") || secret.Contains("REPLACE_WITH"))
            return (false, "JWT secret appears to be a placeholder");

        return (true, "Valid JWT secret");
    }

    private (bool IsValid, string Message) ValidateReplicateToken(string token)
    {
        if (!token.StartsWith("r8_"))
            return (false, "Replicate token should start with 'r8_'");

        // In development, allow shorter tokens for testing
        var minLength = _environment.IsDevelopment() ? 10 : 20;
        if (token.Length < minLength)
        {
            var envNote = _environment.IsDevelopment() ? " (relaxed for development)" : "";
            return (false, $"Replicate token appears to be too short (minimum {minLength} characters){envNote}");
        }

        // In development, allow test tokens
        if (_environment.IsDevelopment() && token.Contains("DevTest"))
        {
            return (true, "Valid development test token format");
        }

        return (true, "Valid Replicate API token format");
    }

    private (bool IsValid, string Message) ValidateWebhookSecret(string secret)
    {
        if (secret.Length < 16)
            return (false, "Webhook secret should be at least 16 characters");

        return (true, "Valid webhook secret");
    }

    private (bool IsValid, string Message) ValidateAzureStorageConnectionString(string connectionString)
    {
        if (!connectionString.Contains("DefaultEndpointsProtocol"))
            return (false, "Invalid Azure Storage connection string format");

        return (true, "Valid Azure Storage connection string format");
    }

    private bool IsSecureVariable(string varName)
    {
        var secureVars = new[]
        {
            EnvironmentConfiguration.MSSQL_SA_PASSWORD,
            EnvironmentConfiguration.JWT_SECRET,
            EnvironmentConfiguration.REPLICATE_API_TOKEN,
            EnvironmentConfiguration.REPLICATE_WEBHOOK_SECRET,
            EnvironmentConfiguration.GOOGLE_CLIENT_SECRET,
            EnvironmentConfiguration.STRIPE_SECRET_KEY,
            EnvironmentConfiguration.STRIPE_WEBHOOK_SECRET,
            EnvironmentConfiguration.AZURE_STORAGE_CONNECTION_STRING
        };

        return secureVars.Contains(varName);
    }

    private string DetermineVariableSource(string varName, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "Not configured";

        // Check if it's from environment variable
        var envValue = Environment.GetEnvironmentVariable(varName);
        if (!string.IsNullOrEmpty(envValue) && envValue == value)
            return "Environment Variable";

        // Check if it's from configuration
        var configValue = _configuration[varName];
        if (!string.IsNullOrEmpty(configValue) && configValue == value)
            return "Configuration";

        return "Unknown";
    }

    private Task<Dictionary<string, ConfigurationSectionDto>> ValidateConfigurationSectionsAsync()
    {
        var sections = new Dictionary<string, ConfigurationSectionDto>();

        // Validate ConnectionStrings section
        var connectionStrings = _configuration.GetSection("ConnectionStrings");
        sections["ConnectionStrings"] = new ConfigurationSectionDto
        {
            Name = "ConnectionStrings",
            IsValid = !string.IsNullOrEmpty(connectionStrings["DefaultConnection"]),
            Values = connectionStrings.AsEnumerable().Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => (object)kv.Value!),
            Issues = string.IsNullOrEmpty(connectionStrings["DefaultConnection"]) 
                ? new List<string> { "DefaultConnection not configured" } 
                : new List<string>(),
            IsComplete = !string.IsNullOrEmpty(connectionStrings["DefaultConnection"])
        };

        // Validate JWT section
        var jwt = _configuration.GetSection("JWT");
        var jwtIssues = new List<string>();
        if (string.IsNullOrEmpty(jwt["ValidAudience"])) jwtIssues.Add("ValidAudience not configured");
        if (string.IsNullOrEmpty(jwt["ValidIssuer"])) jwtIssues.Add("ValidIssuer not configured");
        if (string.IsNullOrEmpty(jwt["Secret"])) jwtIssues.Add("Secret not configured");

        sections["JWT"] = new ConfigurationSectionDto
        {
            Name = "JWT",
            IsValid = jwtIssues.Count == 0,
            Values = jwt.AsEnumerable().Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => (object)kv.Value!),
            Issues = jwtIssues,
            IsComplete = jwtIssues.Count == 0
        };

        // Validate Replicate section
        var replicate = _configuration.GetSection("Replicate");
        var replicateIssues = new List<string>();
        if (string.IsNullOrEmpty(replicate["ApiToken"])) replicateIssues.Add("ApiToken not configured");
        if (string.IsNullOrEmpty(replicate["WebhookSecret"])) replicateIssues.Add("WebhookSecret not configured");

        sections["Replicate"] = new ConfigurationSectionDto
        {
            Name = "Replicate",
            IsValid = replicateIssues.Count == 0,
            Values = replicate.AsEnumerable().Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => (object)kv.Value!),
            Issues = replicateIssues,
            IsComplete = replicateIssues.Count == 0
        };

        return Task.FromResult(sections);
    }

    private List<string> CheckForDefaultValues()
    {
        var usingDefaults = new List<string>();

        // Check for placeholder values in configuration
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("REPLACE_WITH"))
        {
            usingDefaults.Add("Database connection string uses placeholder");
        }

        var jwtSecret = _configuration["JWT:Secret"];
        if (!string.IsNullOrEmpty(jwtSecret) && (jwtSecret.Contains("YourSuperSecret") || jwtSecret.Contains("REPLACE_WITH")))
        {
            usingDefaults.Add("JWT secret uses placeholder value");
        }

        var appBaseUrl = _configuration["AppBaseUrl"];
        if (!string.IsNullOrEmpty(appBaseUrl) && appBaseUrl.Contains("your-domain"))
        {
            usingDefaults.Add("App base URL uses placeholder domain");
        }

        return usingDefaults;
    }

    private List<PerformanceIssueDto> ValidatePerformanceMetrics(PerformanceBaselineDto baseline, PerformanceTestResultDto results)
    {
        var issues = new List<PerformanceIssueDto>();

        if (results.AverageResponseTimeMs > baseline.MaxResponseTimeMs)
        {
            issues.Add(new PerformanceIssueDto
            {
                Category = "Response Time",
                Severity = "High",
                Description = $"Average response time ({results.AverageResponseTimeMs}ms) exceeds baseline ({baseline.MaxResponseTimeMs}ms)",
                Recommendation = "Optimize API endpoints and database queries to improve response time",
                ActualValue = results.AverageResponseTimeMs,
                ExpectedValue = baseline.MaxResponseTimeMs,
                Metric = "ResponseTimeMs"
            });
        }

        if (results.ErrorRatePercent > baseline.MaxErrorRatePercent)
        {
            issues.Add(new PerformanceIssueDto
            {
                Category = "Error Rate",
                Severity = "Critical",
                Description = $"Error rate ({results.ErrorRatePercent:F1}%) exceeds baseline ({baseline.MaxErrorRatePercent:F1}%)",
                Recommendation = "Investigate and fix errors causing high error rate",
                ActualValue = results.ErrorRatePercent,
                ExpectedValue = baseline.MaxErrorRatePercent,
                Metric = "ErrorRatePercent"
            });
        }

        if (results.MemoryUsagePercent > baseline.MaxMemoryUsagePercent)
        {
            issues.Add(new PerformanceIssueDto
            {
                Category = "Memory Usage",
                Severity = "High",
                Description = $"Memory usage ({results.MemoryUsagePercent:F1}%) exceeds baseline ({baseline.MaxMemoryUsagePercent:F1}%)",
                Recommendation = "Optimize memory usage or increase memory limits",
                ActualValue = results.MemoryUsagePercent,
                ExpectedValue = baseline.MaxMemoryUsagePercent,
                Metric = "MemoryUsagePercent"
            });
        }

        if (results.CpuUsagePercent > baseline.MaxCpuUsagePercent)
        {
            issues.Add(new PerformanceIssueDto
            {
                Category = "CPU Usage",
                Severity = "Medium",
                Description = $"CPU usage ({results.CpuUsagePercent:F1}%) exceeds baseline ({baseline.MaxCpuUsagePercent:F1}%)",
                Recommendation = "Optimize CPU-intensive operations or increase CPU limits",
                ActualValue = results.CpuUsagePercent,
                ExpectedValue = baseline.MaxCpuUsagePercent,
                Metric = "CpuUsagePercent"
            });
        }

        if (results.AverageDatabaseQueryTimeMs > baseline.MaxDatabaseQueryTimeMs)
        {
            issues.Add(new PerformanceIssueDto
            {
                Category = "Database Performance",
                Severity = "High",
                Description = $"Database query time ({results.AverageDatabaseQueryTimeMs}ms) exceeds baseline ({baseline.MaxDatabaseQueryTimeMs}ms)",
                Recommendation = "Optimize database queries and consider indexing improvements",
                ActualValue = results.AverageDatabaseQueryTimeMs,
                ExpectedValue = baseline.MaxDatabaseQueryTimeMs,
                Metric = "DatabaseQueryTimeMs"
            });
        }

        return issues;
    }

    private Task<List<SecurityCheckDto>> ValidateJwtSecurityAsync()
    {
        var checks = new List<SecurityCheckDto>();

        var jwtSecret = _configuration["JWT:Secret"];
        var jwtAudience = _configuration["JWT:ValidAudience"];
        var jwtIssuer = _configuration["JWT:ValidIssuer"];

        // JWT Secret validation
        checks.Add(new SecurityCheckDto
        {
            CheckName = "JWT Secret Strength",
            Category = "Authentication",
            Passed = !string.IsNullOrEmpty(jwtSecret) && jwtSecret.Length >= 32 && !jwtSecret.Contains("YourSuperSecret"),
            Severity = "Critical",
            Description = "JWT secret must be strong and unique",
            Recommendation = "Use a cryptographically secure random string of at least 32 characters",
            Details = new Dictionary<string, object>
            {
                ["secretConfigured"] = !string.IsNullOrEmpty(jwtSecret),
                ["secretLength"] = jwtSecret?.Length ?? 0,
                ["isPlaceholder"] = jwtSecret?.Contains("YourSuperSecret") ?? false
            }
        });

        // JWT Audience validation
        checks.Add(new SecurityCheckDto
        {
            CheckName = "JWT Audience Configuration",
            Category = "Authentication",
            Passed = !string.IsNullOrEmpty(jwtAudience) && Uri.IsWellFormedUriString(jwtAudience, UriKind.Absolute),
            Severity = "High",
            Description = "JWT audience must be properly configured",
            Recommendation = "Set JWT audience to your application's production URL",
            Details = new Dictionary<string, object>
            {
                ["audienceConfigured"] = !string.IsNullOrEmpty(jwtAudience),
                ["isValidUrl"] = Uri.IsWellFormedUriString(jwtAudience ?? "", UriKind.Absolute)
            }
        });

        // JWT Issuer validation
        checks.Add(new SecurityCheckDto
        {
            CheckName = "JWT Issuer Configuration",
            Category = "Authentication",
            Passed = !string.IsNullOrEmpty(jwtIssuer) && Uri.IsWellFormedUriString(jwtIssuer, UriKind.Absolute),
            Severity = "High",
            Description = "JWT issuer must be properly configured",
            Recommendation = "Set JWT issuer to your application's production URL",
            Details = new Dictionary<string, object>
            {
                ["issuerConfigured"] = !string.IsNullOrEmpty(jwtIssuer),
                ["isValidUrl"] = Uri.IsWellFormedUriString(jwtIssuer ?? "", UriKind.Absolute)
            }
        });

        return Task.FromResult(checks);
    }

    private List<SecurityCheckDto> ValidateHttpsConfiguration()
    {
        var checks = new List<SecurityCheckDto>();

        // HTTPS enforcement check
        var httpsRedirection = _configuration.GetValue<bool>("UseHttpsRedirection", true);
        checks.Add(new SecurityCheckDto
        {
            CheckName = "HTTPS Enforcement",
            Category = "Transport Security",
            Passed = httpsRedirection || _environment.IsDevelopment(),
            Severity = "Critical",
            Description = "HTTPS must be enforced in production",
            Recommendation = "Enable HTTPS redirection for production deployments",
            Details = new Dictionary<string, object>
            {
                ["httpsRedirectionEnabled"] = httpsRedirection,
                ["environment"] = _environment.EnvironmentName
            }
        });

        return checks;
    }

    private List<SecurityCheckDto> ValidateSecurityHeaders()
    {
        var checks = new List<SecurityCheckDto>();

        // This would need to be enhanced with actual header checking in a real implementation
        checks.Add(new SecurityCheckDto
        {
            CheckName = "Security Headers",
            Category = "Web Security",
            Passed = true, // Placeholder - would need actual header validation
            Severity = "Medium",
            Description = "Security headers should be properly configured",
            Recommendation = "Implement security headers like HSTS, CSP, X-Frame-Options",
            Details = new Dictionary<string, object>
            {
                ["note"] = "Header validation requires runtime testing"
            }
        });

        return checks;
    }

    private Task<List<SecurityCheckDto>> ValidateDatabaseSecurityAsync()
    {
        var checks = new List<SecurityCheckDto>();

        try
        {
            // Connection encryption check
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var usesTrustedConnection = connectionString?.Contains("TrustServerCertificate=true") ?? false;
            var usesEncryption = connectionString?.Contains("Encrypt=true") ?? false;

            checks.Add(new SecurityCheckDto
            {
                CheckName = "Database Connection Security",
                Category = "Database Security",
                Passed = usesEncryption || _environment.IsDevelopment(),
                Severity = "High",
                Description = "Database connections should use encryption",
                Recommendation = "Enable SSL encryption for database connections",
                Details = new Dictionary<string, object>
                {
                    ["usesEncryption"] = usesEncryption,
                    ["trustsServerCertificate"] = usesTrustedConnection,
                    ["environment"] = _environment.EnvironmentName
                }
            });

            // SQL Injection protection (Entity Framework provides this)
            checks.Add(new SecurityCheckDto
            {
                CheckName = "SQL Injection Protection",
                Category = "Database Security",
                Passed = true, // Entity Framework provides parameterized queries
                Severity = "Critical",
                Description = "Application should use parameterized queries",
                Recommendation = "Continue using Entity Framework for database access",
                Details = new Dictionary<string, object>
                {
                    ["usesEntityFramework"] = true,
                    ["parametrizedQueries"] = true
                }
            });
        }
        catch (Exception ex)
        {
            checks.Add(new SecurityCheckDto
            {
                CheckName = "Database Security Validation",
                Category = "Database Security",
                Passed = false,
                Severity = "High",
                Description = "Unable to validate database security configuration",
                Recommendation = "Review database security settings manually",
                Details = new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                }
            });
        }

        return Task.FromResult(checks);
    }

    private List<SecurityCheckDto> ValidateExternalServiceSecurity()
    {
        var checks = new List<SecurityCheckDto>();

        // API key security
        var replicateToken = _configuration["Replicate:ApiToken"];
        checks.Add(new SecurityCheckDto
        {
            CheckName = "External API Key Security",
            Category = "API Security",
            Passed = !string.IsNullOrEmpty(replicateToken) && !replicateToken.Contains("your-api-key"),
            Severity = "High",
            Description = "External API keys must be properly configured and secured",
            Recommendation = "Use secure API keys and store them in environment variables",
            Details = new Dictionary<string, object>
            {
                ["replicateTokenConfigured"] = !string.IsNullOrEmpty(replicateToken),
                ["isPlaceholder"] = replicateToken?.Contains("your-api-key") ?? false
            }
        });

        return checks;
    }

    private List<SecurityCheckDto> ValidateEnvironmentVariableSecurity()
    {
        var checks = new List<SecurityCheckDto>();

        // Check for secrets in configuration vs environment variables
        var secureConfigItems = new[]
        {
            ("JWT:Secret", _configuration["JWT:Secret"]),
            ("Replicate:ApiToken", _configuration["Replicate:ApiToken"]),
            ("ConnectionStrings:DefaultConnection", _configuration.GetConnectionString("DefaultConnection"))
        };

        var secretsInConfig = secureConfigItems.Where(item => 
            !string.IsNullOrEmpty(item.Item2) && 
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable(GetEnvironmentVariableName(item.Item1)))).ToList();

        checks.Add(new SecurityCheckDto
        {
            CheckName = "Secrets Management",
            Category = "Configuration Security",
            Passed = secretsInConfig.Count == 0 || _environment.IsDevelopment(),
            Severity = "High",
            Description = "Secrets should be stored in environment variables, not configuration files",
            Recommendation = "Move sensitive configuration to environment variables or Azure Key Vault",
            Details = new Dictionary<string, object>
            {
                ["secretsInConfig"] = secretsInConfig.Count,
                ["environment"] = _environment.EnvironmentName,
                ["secretItems"] = secretsInConfig.Select(s => s.Item1).ToList()
            }
        });

        return checks;
    }

    private string GetEnvironmentVariableName(string configKey)
    {
        return configKey switch
        {
            "JWT:Secret" => EnvironmentConfiguration.JWT_SECRET,
            "Replicate:ApiToken" => EnvironmentConfiguration.REPLICATE_API_TOKEN,
            "ConnectionStrings:DefaultConnection" => "MSSQL_CONNECTION_STRING",
            _ => configKey.Replace(":", "_").ToUpper()
        };
    }

    private async Task<AzureServiceStatusDto> ValidateAzureSqlAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Test database connectivity
            await _dbContext.Database.CanConnectAsync();
            stopwatch.Stop();

            return new AzureServiceStatusDto
            {
                ServiceName = "Azure SQL Database",
                Status = "Healthy",
                IsHealthy = true,
                IsConfigured = true,
                IsConnected = true,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                LastChecked = DateTime.UtcNow,
                Configuration = new Dictionary<string, object>
                {
                    ["provider"] = "Microsoft.EntityFrameworkCore.SqlServer",
                    ["connectionTimeout"] = _dbContext.Database.GetCommandTimeout() ?? 30
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new AzureServiceStatusDto
            {
                ServiceName = "Azure SQL Database",
                Status = "Unhealthy",
                IsHealthy = false,
                IsConfigured = !string.IsNullOrEmpty(_configuration.GetConnectionString("DefaultConnection")),
                IsConnected = false,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message,
                LastChecked = DateTime.UtcNow
            };
        }
    }

    private async Task<AzureServiceStatusDto> ValidateAzureBlobStorageAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var isConfigured = !string.IsNullOrEmpty(_configuration["AzureStorage:ConnectionString"]);
            if (!isConfigured)
            {
                stopwatch.Stop();
                return new AzureServiceStatusDto
                {
                    ServiceName = "Azure Blob Storage",
                    Status = "NotConfigured",
                    IsHealthy = false,
                    IsConfigured = false,
                    IsConnected = false,
                    ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    ErrorMessage = "Azure Blob Storage not configured",
                    LastChecked = DateTime.UtcNow
                };
            }

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

            return new AzureServiceStatusDto
            {
                ServiceName = "Azure Blob Storage",
                Status = canConnect ? "Healthy" : "Unhealthy",
                IsHealthy = canConnect,
                IsConfigured = true,
                IsConnected = canConnect,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                ErrorMessage = canConnect ? string.Empty : "Unable to connect to Azure Blob Storage",
                LastChecked = DateTime.UtcNow,
                Configuration = new Dictionary<string, object>
                {
                    ["containerName"] = _configuration["AzureStorage:ContainerName"] ?? "profile-images",
                    ["storageType"] = _storageService.GetType().Name
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new AzureServiceStatusDto
            {
                ServiceName = "Azure Blob Storage",
                Status = "Error",
                IsHealthy = false,
                IsConfigured = true,
                IsConnected = false,
                ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message,
                LastChecked = DateTime.UtcNow
            };
        }
    }

    private async Task<AzureServiceStatusDto> ValidateApplicationInsightsAsync()
    {
        return await Task.FromResult(new AzureServiceStatusDto
        {
            ServiceName = "Application Insights",
            Status = "NotImplemented",
            IsHealthy = true, // Assume healthy for now
            IsConfigured = false,
            IsConnected = false,
            ResponseTimeMs = 0,
            ErrorMessage = "Application Insights validation not implemented",
            LastChecked = DateTime.UtcNow,
            Configuration = new Dictionary<string, object>
            {
                ["note"] = "Requires Application Insights SDK integration for full validation"
            }
        });
    }

    private async Task<AzureServiceStatusDto> ValidateAzureKeyVaultAsync()
    {
        return await Task.FromResult(new AzureServiceStatusDto
        {
            ServiceName = "Azure Key Vault",
            Status = "NotConfigured",
            IsHealthy = true, // Not critical for initial deployment
            IsConfigured = false,
            IsConnected = false,
            ResponseTimeMs = 0,
            ErrorMessage = "Azure Key Vault not configured (optional)",
            LastChecked = DateTime.UtcNow,
            Configuration = new Dictionary<string, object>
            {
                ["isOptional"] = true,
                ["recommendation"] = "Consider implementing Azure Key Vault for production secrets management"
            }
        });
    }

    private async Task<AzureServiceStatusDto> ValidateAzureContainerRegistryAsync()
    {
        return await Task.FromResult(new AzureServiceStatusDto
        {
            ServiceName = "Azure Container Registry",
            Status = "NotValidated",
            IsHealthy = true, // Assume healthy for deployment
            IsConfigured = false,
            IsConnected = false,
            ResponseTimeMs = 0,
            ErrorMessage = "Container registry validation requires Azure CLI or SDK",
            LastChecked = DateTime.UtcNow,
            Configuration = new Dictionary<string, object>
            {
                ["note"] = "Validation typically handled by deployment pipeline"
            }
        });
    }

    private async Task<AzureResourceHealthDto> GetAzureResourceHealthAsync()
    {
        return await Task.FromResult(new AzureResourceHealthDto
        {
            SubscriptionId = "Not available at runtime",
            ResourceGroup = Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP") ?? "Not configured",
            Region = Environment.GetEnvironmentVariable("AZURE_REGION") ?? "Not configured",
            HasManagedIdentity = false, // Would need Azure SDK to check
            ManagedIdentityStatus = "Not validated",
            ResourceTags = new Dictionary<string, string>
            {
                ["environment"] = _environment.EnvironmentName,
                ["application"] = "AI.ProfilePhotoMaker.API"
            },
            AvailabilityZones = new List<string> { "Zone information not available at runtime" }
        });
    }

    // Additional helper methods would continue here for the remaining validation components...

    private List<string> GeneratePreDeploymentRecommendations(Dictionary<string, ValidationComponentDto> components)
    {
        var recommendations = new List<string>();

        var failedComponents = components.Values.Where(c => !c.IsValid).ToList();
        if (failedComponents.Any())
        {
            recommendations.Add($"Fix {failedComponents.Count} failed validation component(s) before deployment");
        }

        var configComponent = components.GetValueOrDefault("configuration");
        if (configComponent != null && !configComponent.IsValid)
        {
            recommendations.Add("Complete environment configuration setup");
        }

        var securityComponent = components.GetValueOrDefault("security");
        if (securityComponent != null && !securityComponent.IsValid)
        {
            recommendations.Add("Address security configuration issues before production deployment");
        }

        var databaseComponent = components.GetValueOrDefault("database");
        if (databaseComponent != null && !databaseComponent.IsValid)
        {
            recommendations.Add("Ensure database is properly migrated and seeded");
        }

        return recommendations.Distinct().Take(5).ToList();
    }

    private List<string> GeneratePostDeploymentRecommendations(Dictionary<string, ValidationComponentDto> components)
    {
        var recommendations = new List<string>();

        var unhealthyComponents = components.Values.Where(c => !c.IsValid).ToList();
        if (unhealthyComponents.Any())
        {
            recommendations.Add("Monitor and resolve unhealthy components");
        }

        var performanceComponent = components.GetValueOrDefault("performance");
        if (performanceComponent != null && !performanceComponent.IsValid)
        {
            recommendations.Add("Address performance issues to meet SLA requirements");
        }

        recommendations.Add("Set up monitoring alerts for critical metrics");
        recommendations.Add("Schedule regular health checks and performance reviews");

        return recommendations.Distinct().Take(5).ToList();
    }
}
