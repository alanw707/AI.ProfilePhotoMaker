using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AI.ProfilePhotoMaker.API.Configuration;

/// <summary>
/// Centralized environment variable configuration with validation
/// Provides strongly-typed access to environment variables with security best practices
/// </summary>
public class EnvironmentConfiguration
{
    // Required environment variables
    public const string MSSQL_SA_PASSWORD = "MSSQL_SA_PASSWORD";
    public const string JWT_SECRET = "JWT_SECRET";
    public const string REPLICATE_API_TOKEN = "REPLICATE_API_TOKEN";
    public const string REPLICATE_WEBHOOK_SECRET = "REPLICATE_WEBHOOK_SECRET";

    // Optional environment variables with defaults
    public const string ASPNETCORE_ENVIRONMENT = "ASPNETCORE_ENVIRONMENT";
    public const string APP_BASE_URL = "APP_BASE_URL";
    public const string JWT_VALID_AUDIENCE = "JWT_VALID_AUDIENCE";
    public const string JWT_VALID_ISSUER = "JWT_VALID_ISSUER";
    
    // Google OAuth (optional)
    public const string GOOGLE_CLIENT_ID = "GOOGLE_CLIENT_ID";
    public const string GOOGLE_CLIENT_SECRET = "GOOGLE_CLIENT_SECRET";
    
    // Stripe Payment (optional)
    public const string STRIPE_PUBLISHABLE_KEY = "STRIPE_PUBLISHABLE_KEY";
    public const string STRIPE_SECRET_KEY = "STRIPE_SECRET_KEY";
    public const string STRIPE_WEBHOOK_SECRET = "STRIPE_WEBHOOK_SECRET";
    
    // Azure Storage (optional)
    public const string AZURE_STORAGE_CONNECTION_STRING = "AZURE_STORAGE_CONNECTION_STRING";
    public const string AZURE_STORAGE_CONTAINER_NAME = "AZURE_STORAGE_CONTAINER_NAME";

    private readonly IConfiguration _configuration;
    private readonly ILogger<EnvironmentConfiguration> _logger;

    public EnvironmentConfiguration(IConfiguration configuration, ILogger<EnvironmentConfiguration> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Validates all required environment variables on application startup
    /// </summary>
    public async Task<ValidationResult> ValidateAsync()
    {
        var validationResults = new List<ValidationResult>();

        try
        {
            // Validate required database configuration
            await ValidateDatabaseConfigurationAsync(validationResults);

            // Validate required JWT configuration
            await ValidateJwtConfigurationAsync(validationResults);

            // Validate required AI/ML service configuration
            await ValidateReplicateConfigurationAsync(validationResults);

            // Validate optional configurations
            await ValidateOptionalConfigurationsAsync(validationResults);

            // Log validation summary
            LogValidationSummary(validationResults);

            return new ValidationResult(validationResults.Count == 0, validationResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during environment validation");
            validationResults.Add(new ValidationResult(false, "CRITICAL_ERROR", $"Environment validation failed: {ex.Message}"));
            return new ValidationResult(false, validationResults);
        }
    }

    private async Task ValidateDatabaseConfigurationAsync(List<ValidationResult> results)
    {
        // If a DefaultConnection is configured, prefer it and do not require MSSQL_SA_PASSWORD
        var defaultConnection = _configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(defaultConnection))
        {
            _logger.LogInformation("✅ Using configured DefaultConnection for database; skipping MSSQL_SA_PASSWORD requirement");
            return;
        }

        // Otherwise validate MSSQL_SA_PASSWORD for local/dev SQL authentication
        var password = GetEnvironmentVariable(MSSQL_SA_PASSWORD);
        if (string.IsNullOrEmpty(password))
        {
            results.Add(new ValidationResult(false, MSSQL_SA_PASSWORD, "Database password is required when no ConnectionStrings:DefaultConnection is configured"));
            return;
        }

        // Validate password complexity
        if (password.Length < 8)
        {
            results.Add(new ValidationResult(false, MSSQL_SA_PASSWORD, "Database password must be at least 8 characters"));
        }

        if (!Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$"))
        {
            results.Add(new ValidationResult(false, MSSQL_SA_PASSWORD, 
                "Database password must contain uppercase, lowercase, number, and special character"));
        }

        _logger.LogInformation("✅ Database configuration validation completed");
    }

    private async Task ValidateJwtConfigurationAsync(List<ValidationResult> results)
    {
        // Accept either environment variable JWT_SECRET or configuration key JWT:Secret
        var jwtSecret = GetEnvironmentVariable(JWT_SECRET) ?? _configuration["JWT:Secret"] ?? _configuration["Jwt:Secret"];
        
        if (string.IsNullOrEmpty(jwtSecret))
        {
            results.Add(new ValidationResult(false, JWT_SECRET, "JWT secret is required (set env JWT_SECRET or config JWT:Secret)"));
            return;
        }

        if (jwtSecret.Length < 32)
        {
            results.Add(new ValidationResult(false, JWT_SECRET, "JWT secret must be at least 32 characters"));
        }

        // Check for weak or default secrets
        if (jwtSecret.Contains("YourSuperSecret") || jwtSecret.Contains("REPLACE_WITH"))
        {
            results.Add(new ValidationResult(false, JWT_SECRET, "JWT secret appears to be a placeholder - use a real secret"));
        }

        // Validate JWT audience and issuer URLs
        ValidateUrl(JWT_VALID_AUDIENCE, "JWT audience URL", results);
        ValidateUrl(JWT_VALID_ISSUER, "JWT issuer URL", results);

        _logger.LogInformation("✅ JWT configuration validation completed");
    }

    private async Task ValidateReplicateConfigurationAsync(List<ValidationResult> results)
    {
        // Accept either env REPLICATE_API_TOKEN or config Replicate:ApiToken
        var apiToken = GetEnvironmentVariable(REPLICATE_API_TOKEN) ?? _configuration["Replicate:ApiToken"];
        
        if (string.IsNullOrEmpty(apiToken))
        {
            results.Add(new ValidationResult(false, REPLICATE_API_TOKEN, "Replicate API token is required (set env REPLICATE_API_TOKEN or config Replicate:ApiToken)"));
            return;
        }

        if (!apiToken.StartsWith("r8_"))
        {
            results.Add(new ValidationResult(false, REPLICATE_API_TOKEN, "Replicate API token should start with 'r8_'"));
        }

        // Webhook secret is recommended but optional (signature validator tolerates missing secret in dev)
        var webhookSecret = GetEnvironmentVariable(REPLICATE_WEBHOOK_SECRET) ?? _configuration["Replicate:WebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret))
        {
            _logger.LogWarning("Replicate webhook secret is not configured - signature validation will be skipped");
        }
        else if (webhookSecret.Length < 32)
        {
            results.Add(new ValidationResult(false, REPLICATE_WEBHOOK_SECRET, "Webhook secret should be at least 32 characters"));
        }

        _logger.LogInformation("✅ Replicate API configuration validation completed");
    }

    private async Task ValidateOptionalConfigurationsAsync(List<ValidationResult> results)
    {
        // Validate Google OAuth if configured
        var googleClientId = GetEnvironmentVariable(GOOGLE_CLIENT_ID);
        var googleClientSecret = GetEnvironmentVariable(GOOGLE_CLIENT_SECRET);
        
        if (!string.IsNullOrEmpty(googleClientId) || !string.IsNullOrEmpty(googleClientSecret))
        {
            if (string.IsNullOrEmpty(googleClientId))
                results.Add(new ValidationResult(false, GOOGLE_CLIENT_ID, "Google Client ID required when Google OAuth is configured"));
            if (string.IsNullOrEmpty(googleClientSecret))
                results.Add(new ValidationResult(false, GOOGLE_CLIENT_SECRET, "Google Client Secret required when Google OAuth is configured"));
        }

        // Validate Stripe if configured
        var stripePublishable = GetEnvironmentVariable(STRIPE_PUBLISHABLE_KEY);
        var stripeSecret = GetEnvironmentVariable(STRIPE_SECRET_KEY);
        
        if (!string.IsNullOrEmpty(stripePublishable) || !string.IsNullOrEmpty(stripeSecret))
        {
            if (string.IsNullOrEmpty(stripePublishable))
                results.Add(new ValidationResult(false, STRIPE_PUBLISHABLE_KEY, "Stripe publishable key required when Stripe is configured"));
            if (string.IsNullOrEmpty(stripeSecret))
                results.Add(new ValidationResult(false, STRIPE_SECRET_KEY, "Stripe secret key required when Stripe is configured"));
        }

        // Validate Azure Storage connection string format if provided
        var azureStorage = GetEnvironmentVariable(AZURE_STORAGE_CONNECTION_STRING);
        if (!string.IsNullOrEmpty(azureStorage) && !azureStorage.Contains("DefaultEndpointsProtocol"))
        {
            results.Add(new ValidationResult(false, AZURE_STORAGE_CONNECTION_STRING, "Azure Storage connection string format appears invalid"));
        }

        _logger.LogInformation("✅ Optional configurations validation completed");
    }

    private void ValidateUrl(string envVarName, string description, List<ValidationResult> results)
    {
        var url = GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrEmpty(url) && !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            results.Add(new ValidationResult(false, envVarName, $"{description} is not a valid URL: {url}"));
        }
    }

    private void LogValidationSummary(List<ValidationResult> results)
    {
        if (results.Count == 0)
        {
            _logger.LogInformation("🎉 All environment variables validated successfully!");
        }
        else
        {
            _logger.LogError("❌ Environment validation failed with {ErrorCount} errors:", results.Count);
            foreach (var result in results)
            {
                _logger.LogError("  - {Variable}: {Message}", result.Variable, result.Message);
            }
        }
    }

    /// <summary>
    /// Gets environment variable with fallback to configuration
    /// </summary>
    public string? GetEnvironmentVariable(string name, string? defaultValue = null)
    {
        // Try environment variable first (highest priority)
        var envValue = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrEmpty(envValue))
            return envValue;

        // Fallback to configuration (appsettings.json, user secrets, etc.)
        var configValue = _configuration[name];
        if (!string.IsNullOrEmpty(configValue))
            return configValue;

        return defaultValue;
    }

    /// <summary>
    /// Gets required environment variable or throws exception
    /// </summary>
    public string GetRequiredEnvironmentVariable(string name)
    {
        var value = GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Required environment variable '{name}' is not configured");
        }
        return value;
    }

    /// <summary>
    /// Builds database connection string using environment variables
    /// </summary>
    public string BuildDatabaseConnectionString(string? server = null, string? database = null)
    {
        var password = GetRequiredEnvironmentVariable(MSSQL_SA_PASSWORD);
        
        // Allow override via full connection string
        var customConnection = GetEnvironmentVariable("MSSQL_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(customConnection))
        {
            return customConnection.Replace("${MSSQL_SA_PASSWORD}", password);
        }

        // Build standard connection string
        var serverName = server ?? "localhost,1433";
        var databaseName = database ?? "AIProfileMaker";
        
        return $"Server={serverName};Database={databaseName};User Id=sa;Password={password};TrustServerCertificate=true;MultipleActiveResultSets=true;";
    }
}

/// <summary>
/// Represents the result of environment variable validation
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; }
    public string Variable { get; }
    public string Message { get; }

    public ValidationResult(bool isValid, string variable, string message)
    {
        IsValid = isValid;
        Variable = variable;
        Message = message;
    }

    public ValidationResult(bool isValid, List<ValidationResult> results)
    {
        IsValid = isValid;
        Variable = "SUMMARY";
        Message = isValid ? "All validations passed" : $"{results.Count} validation errors found";
    }
}

/// <summary>
/// Extension methods for environment configuration
/// </summary>
public static class EnvironmentConfigurationExtensions
{
    /// <summary>
    /// Adds environment configuration services to DI container
    /// </summary>
    public static IServiceCollection AddEnvironmentConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<EnvironmentConfiguration>();
        return services;
    }

    /// <summary>
    /// Validates environment configuration during application startup
    /// </summary>
    public static async Task<WebApplication> UseEnvironmentValidationAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var envConfig = scope.ServiceProvider.GetRequiredService<EnvironmentConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<EnvironmentConfiguration>>();

        var result = await envConfig.ValidateAsync();
        
        if (!result.IsValid)
        {
            logger.LogCritical("🚨 Application startup failed due to environment validation errors");
            logger.LogCritical("Please check your .env file or environment variable configuration");
            logger.LogCritical("See .env.example for required variables and correct format");
            
            // In development, we can continue with warnings
            // In production, we should terminate the application
            if (app.Environment.IsProduction())
            {
                throw new InvalidOperationException("Environment validation failed. Application cannot start safely.");
            }
            else
            {
                logger.LogWarning("⚠️  Continuing in development mode despite validation errors");
            }
        }

        return app;
    }
}