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
    
    // Google OAuth (required for authentication)
    public const string GOOGLE_CLIENT_ID = "GOOGLE_CLIENT_ID";
    public const string GOOGLE_CLIENT_SECRET = "GOOGLE_CLIENT_SECRET";
    
    // Stripe Payment (required - referenced in infrastructure)
    public const string STRIPE_PUBLISHABLE_KEY = "STRIPE_PUBLISHABLE_KEY";
    public const string STRIPE_SECRET_KEY = "STRIPE_SECRET_KEY";
    public const string STRIPE_WEBHOOK_SECRET = "STRIPE_WEBHOOK_SECRET";
    
    // Azure Storage (required in Production/Staging)
    public const string AZURE_STORAGE_CONNECTION_STRING = "AZURE_STORAGE_CONNECTION_STRING";
    public const string AZURE_STORAGE_CONTAINER_NAME = "AZURE_STORAGE_CONTAINER_NAME";

    private readonly IConfiguration _configuration;
    private readonly ILogger<EnvironmentConfiguration> _logger;
    private readonly IWebHostEnvironment _environment;

    public EnvironmentConfiguration(IConfiguration configuration, ILogger<EnvironmentConfiguration> logger, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
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

            // Validate required Google OAuth configuration
            await ValidateGoogleOAuthConfigurationAsync(validationResults);

            // Validate required Stripe configuration
            await ValidateStripeConfigurationAsync(validationResults);

            // Validate environment-specific configurations  
            await ValidateEnvironmentSpecificConfigurationsAsync(validationResults);

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
        // Support either a full connection string or a local SA password for development
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var password = GetEnvironmentVariable(MSSQL_SA_PASSWORD);

        // If neither is provided, it's an error
        if (string.IsNullOrEmpty(connectionString) && string.IsNullOrEmpty(password))
        {
            results.Add(new ValidationResult(false, "DATABASE_CONFIG",
                "Database configuration is required. Set either MSSQL_SA_PASSWORD (for local development) or ConnectionStrings__DefaultConnection (for production)"));
            return;
        }

        // If using local development password, validate it
        if (!string.IsNullOrEmpty(password) && string.IsNullOrEmpty(connectionString))
        {
            if (password.Length < 8)
            {
                results.Add(new ValidationResult(false, MSSQL_SA_PASSWORD, "Database password must be at least 8 characters"));
            }

            if (!Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$"))
            {
                results.Add(new ValidationResult(false, MSSQL_SA_PASSWORD,
                    "Database password must contain uppercase, lowercase, number, and special character"));
            }

            _logger.LogInformation("✅ Database configuration validation completed (using local development mode)");
            return;
        }

        // Otherwise, validate the provided connection string minimally
        if (!string.IsNullOrEmpty(connectionString))
        {
            if (!connectionString.Contains("Server=") && !connectionString.Contains("Data Source="))
            {
                results.Add(new ValidationResult(false, "ConnectionStrings__DefaultConnection",
                    "Connection string appears to be invalid (missing Server or Data Source)"));
            }

            _logger.LogInformation("✅ Database configuration validation completed (using configured connection string)");
        }
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

        _logger.LogInformation("✅ JWT configuration validation completed");
    }

    private async Task ValidateReplicateConfigurationAsync(List<ValidationResult> results)
    {
        // Accept either env or config for Replicate credentials
        var envToken = GetEnvironmentVariable(REPLICATE_API_TOKEN);
        var configToken = _configuration["Replicate:ApiToken"];
        var apiToken = envToken ?? configToken;
        var webhookSecret = GetEnvironmentVariable(REPLICATE_WEBHOOK_SECRET) ?? _configuration["Replicate:WebhookSecret"];
        
        // Debug logging for development
        if (_environment.IsDevelopment())
        {
            _logger.LogDebug("Validating Replicate token: '{TokenStart}...' (length: {Length})", 
                string.IsNullOrEmpty(apiToken) ? "NULL" : apiToken.Substring(0, Math.Min(4, apiToken.Length)), 
                apiToken?.Length ?? 0);
        }

        if (string.IsNullOrEmpty(apiToken))
        {
            results.Add(new ValidationResult(false, REPLICATE_API_TOKEN, "Replicate API token is required"));
        }
        else if (_environment.IsDevelopment() && (apiToken.Contains("your-actual-replicate-token-here") || apiToken.Contains("placeholder")))
        {
            // Allow placeholder tokens in development mode
            _logger.LogWarning("⚠️  Using placeholder Replicate token in development mode - this will not work for actual API calls");
        }
        else if (!apiToken.StartsWith("r8_"))
        {
            results.Add(new ValidationResult(false, REPLICATE_API_TOKEN, "Replicate API token should start with 'r8_'"));
        }
        else
        {
            // Additional validation for token length and format
            var minLength = _environment.IsDevelopment() ? 10 : 20;
            if (apiToken.Length < minLength)
            {
                var envNote = _environment.IsDevelopment() ? " (relaxed for development)" : "";
                results.Add(new ValidationResult(false, REPLICATE_API_TOKEN, $"Replicate token appears to be too short (minimum {minLength} characters){envNote}"));
            }
            else if (_environment.IsDevelopment() && apiToken.Contains("DevTest"))
            {
                // Allow development test tokens - no error added
            }
        }

        if (string.IsNullOrEmpty(webhookSecret))
        {
            results.Add(new ValidationResult(false, REPLICATE_WEBHOOK_SECRET, "Replicate webhook secret is required"));
        }

        _logger.LogInformation("✅ Replicate configuration validation completed");
    }

    /// <summary>
    /// Validates required Stripe payment configuration
    /// </summary>
    private async Task ValidateStripeConfigurationAsync(List<ValidationResult> results)
    {
        // All Stripe secrets are REQUIRED - referenced in infrastructure and application code
        var stripeSecretKey = GetEnvironmentVariable(STRIPE_SECRET_KEY);
        var stripePublishableKey = GetEnvironmentVariable(STRIPE_PUBLISHABLE_KEY);
        var stripeWebhookSecret = GetEnvironmentVariable(STRIPE_WEBHOOK_SECRET);
        
        if (string.IsNullOrEmpty(stripeSecretKey))
        {
            results.Add(new ValidationResult(false, STRIPE_SECRET_KEY, "Stripe Secret Key is REQUIRED (referenced in infrastructure and payment processing)"));
        }
        else if (!stripeSecretKey.StartsWith("sk_"))
        {
            results.Add(new ValidationResult(false, STRIPE_SECRET_KEY, "Stripe Secret Key format appears invalid (should start with sk_)"));
        }
        
        if (string.IsNullOrEmpty(stripePublishableKey))
        {
            results.Add(new ValidationResult(false, STRIPE_PUBLISHABLE_KEY, "Stripe Publishable Key is REQUIRED (referenced in infrastructure and frontend payment processing)"));
        }
        else if (!stripePublishableKey.StartsWith("pk_"))
        {
            results.Add(new ValidationResult(false, STRIPE_PUBLISHABLE_KEY, "Stripe Publishable Key format appears invalid (should start with pk_)"));
        }
        
        if (string.IsNullOrEmpty(stripeWebhookSecret))
        {
            results.Add(new ValidationResult(false, STRIPE_WEBHOOK_SECRET, "Stripe Webhook Secret is REQUIRED (referenced in infrastructure and webhook validation)"));
        }
        else if (!stripeWebhookSecret.StartsWith("whsec_"))
        {
            results.Add(new ValidationResult(false, STRIPE_WEBHOOK_SECRET, "Stripe Webhook Secret format appears invalid (should start with whsec_)"));
        }
        
        _logger.LogInformation("✅ Stripe configuration validation completed");
    }

    private async Task ValidateEnvironmentSpecificConfigurationsAsync(List<ValidationResult> results)
    {
        // Check Azure Storage configuration - ENVIRONMENT-AWARE VALIDATION
        await ValidateAzureStorageConfigurationAsync(results);

        _logger.LogInformation("✅ Environment-specific configurations validation completed");
    }

    /// <summary>
    /// Validates required Google OAuth configuration with enhanced format checking
    /// </summary>
    private async Task ValidateGoogleOAuthConfigurationAsync(List<ValidationResult> results)
    {
        var googleClientId = GetEnvironmentVariable(GOOGLE_CLIENT_ID);
        var googleClientSecret = GetEnvironmentVariable(GOOGLE_CLIENT_SECRET);
        
        // Google OAuth is REQUIRED - both secrets must be present
        if (string.IsNullOrEmpty(googleClientId))
        {
            results.Add(new ValidationResult(false, GOOGLE_CLIENT_ID, "Google Client ID is REQUIRED for authentication (referenced in infrastructure deployment)"));
        }
        
        if (string.IsNullOrEmpty(googleClientSecret))
        {
            results.Add(new ValidationResult(false, GOOGLE_CLIENT_SECRET, "Google Client Secret is REQUIRED for authentication (referenced in infrastructure deployment)"));
        }
        
        // Validate Google Client ID format and content
        if (!string.IsNullOrEmpty(googleClientId))
        {
            // Check for common misconfigurations
            if (googleClientId.Contains("Specify --help") || googleClientId.Contains("command") || googleClientId.Contains("options"))
            {
                results.Add(new ValidationResult(false, GOOGLE_CLIENT_ID, 
                    "Google Client ID contains invalid text (appears to be help text or command output). Expected format: 123456789-abc123.apps.googleusercontent.com"));
            }
            else if (googleClientId.StartsWith("YOUR_") || googleClientId.Contains("REPLACE_WITH_") || googleClientId.Contains("placeholder"))
            {
                results.Add(new ValidationResult(false, GOOGLE_CLIENT_ID, 
                    "Google Client ID contains placeholder text. Must be replaced with actual Google OAuth client ID"));
            }
            else if (!googleClientId.Contains(".apps.googleusercontent.com"))
            {
                results.Add(new ValidationResult(false, GOOGLE_CLIENT_ID, 
                    "Google Client ID format appears invalid. Expected format: 123456789-abc123.apps.googleusercontent.com"));
            }
            else
            {
                _logger.LogInformation("✅ Google Client ID format appears valid");
            }
        }
        
        // Validate Google Client Secret format
        if (!string.IsNullOrEmpty(googleClientSecret))
        {
            if (googleClientSecret.Contains("Specify --help") || googleClientSecret.Contains("command") || googleClientSecret.Contains("options"))
            {
                results.Add(new ValidationResult(false, GOOGLE_CLIENT_SECRET, 
                    "Google Client Secret contains invalid text (appears to be help text or command output)"));
            }
            else if (googleClientSecret.StartsWith("YOUR_") || googleClientSecret.Contains("REPLACE_WITH_") || googleClientSecret.Contains("placeholder"))
            {
                results.Add(new ValidationResult(false, GOOGLE_CLIENT_SECRET, 
                    "Google Client Secret contains placeholder text. Must be replaced with actual Google OAuth client secret"));
            }
            else if (!googleClientSecret.StartsWith("GOCSPX-"))
            {
                results.Add(new ValidationResult(false, GOOGLE_CLIENT_SECRET, 
                    "Google Client Secret format appears invalid (should start with GOCSPX-)"));
            }
            else
            {
                _logger.LogInformation("✅ Google Client Secret format appears valid");
            }
        }
        
        _logger.LogInformation("✅ Google OAuth configuration validation completed");
    }

    /// <summary>
    /// Validates Azure Storage configuration with environment-specific requirements
    /// </summary>
    private async Task ValidateAzureStorageConfigurationAsync(List<ValidationResult> results)
    {
        var azureStorage = GetEnvironmentVariable(AZURE_STORAGE_CONNECTION_STRING);
        var containerName = GetEnvironmentVariable(AZURE_STORAGE_CONTAINER_NAME);
        
        // Azure Storage is REQUIRED in production and staging environments
        if (_environment.IsProduction() || _environment.IsStaging())
        {
            if (string.IsNullOrEmpty(azureStorage))
            {
                results.Add(new ValidationResult(false, AZURE_STORAGE_CONNECTION_STRING,
                    $"Azure Storage connection string is REQUIRED in {_environment.EnvironmentName} environment. Local storage is not supported in containerized deployments."));
            }
            else
            {
                // Validate connection string format for production
                if (!azureStorage.Contains("AccountName=") || !azureStorage.Contains("AccountKey="))
                {
                    results.Add(new ValidationResult(false, AZURE_STORAGE_CONNECTION_STRING,
                        "Azure Storage connection string appears to be invalid (missing AccountName or AccountKey)"));
                }
            }

            if (string.IsNullOrEmpty(containerName))
            {
                results.Add(new ValidationResult(false, AZURE_STORAGE_CONTAINER_NAME,
                    $"Azure Storage container name is REQUIRED in {_environment.EnvironmentName} environment"));
            }

            _logger.LogInformation($"✅ Azure Storage is REQUIRED and validated for {_environment.EnvironmentName} environment");
        }
        else
        {
            // Development/local environments - Azure Storage is still required
            if (string.IsNullOrEmpty(azureStorage))
            {
                results.Add(new ValidationResult(false, AZURE_STORAGE_CONNECTION_STRING,
                    $"Azure Storage connection string is REQUIRED in all environments (referenced in infrastructure configuration)"));
            }
            else
            {
                _logger.LogInformation($"✅ Azure Storage configured for {_environment.EnvironmentName} environment");
            }

            if (string.IsNullOrEmpty(containerName))
            {
                results.Add(new ValidationResult(false, AZURE_STORAGE_CONTAINER_NAME,
                    $"Azure Storage container name is REQUIRED in all environments (referenced in infrastructure configuration)"));
            }
        }
    }

    private void LogValidationSummary(List<ValidationResult> results)
    {
        if (results.Count == 0)
        {
            _logger.LogInformation("✅ All environment variables validated successfully");
        }
        else
        {
            _logger.LogError($"❌ Environment validation failed with {results.Count} errors:");
            foreach (var result in results)
            {
                _logger.LogError($"  - {result.Variable}: {result.Message}");
            }
        }
    }

    /// <summary>
    /// Gets an environment variable value with fallback to configuration
    /// </summary>
    public string? GetEnvironmentVariable(string key)
    {
        // First check actual environment variable
        var value = Environment.GetEnvironmentVariable(key);
        
        // If not found, check configuration (handles both appsettings and environment)
        if (string.IsNullOrEmpty(value))
        {
            value = _configuration[key];
        }

        return value;
    }

    /// <summary>
    /// Gets a required environment variable or throws
    /// </summary>
    public string GetRequiredVariable(string key)
    {
        var value = GetEnvironmentVariable(key);
        
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Required environment variable '{key}' is not set");
        }

        return value;
    }

    /// <summary>
    /// Gets an optional environment variable with default value
    /// </summary>
    public string GetOptionalVariable(string key, string defaultValue)
    {
        return GetEnvironmentVariable(key) ?? defaultValue;
    }

    /// <summary>
    /// Checks if an environment variable is set
    /// </summary>
    public bool IsVariableSet(string key)
    {
        return !string.IsNullOrEmpty(GetEnvironmentVariable(key));
    }
}

/// <summary>
/// Validation result for environment variables
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; }
    public List<ValidationResult> Errors { get; }
    public string Variable { get; }
    public string Message { get; }

    public ValidationResult(bool isValid, List<ValidationResult> errors)
    {
        IsValid = isValid;
        Errors = errors;
        Variable = string.Empty;
        Message = string.Empty;
    }

    public ValidationResult(bool isValid, string variable, string message)
    {
        IsValid = isValid;
        Variable = variable;
        Message = message;
        Errors = new List<ValidationResult>();
    }
}

/// <summary>
/// Extension methods for environment configuration
/// </summary>
public static class EnvironmentConfigurationExtensions
{
    /// <summary>
    /// Adds environment configuration services
    /// </summary>
    public static IServiceCollection AddEnvironmentConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<EnvironmentConfiguration>();
        return services;
    }

    /// <summary>
    /// Extension method to check if environment is Staging
    /// </summary>
    public static bool IsStaging(this IWebHostEnvironment environment)
    {
        return environment.IsEnvironment("Staging");
    }

    /// <summary>
    /// Validates environment configuration on startup
    /// </summary>
    public static async Task<IApplicationBuilder> UseEnvironmentValidationAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var environmentConfig = scope.ServiceProvider.GetRequiredService<EnvironmentConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<EnvironmentConfiguration>>();

        logger.LogInformation("🔍 Starting environment variable validation...");

        var result = await environmentConfig.ValidateAsync();

        if (!result.IsValid)
        {
            logger.LogCritical("❌ Application startup failed due to environment validation errors");
            logger.LogCritical("Please check your .env file or environment variable configuration");
            logger.LogCritical("See .env.example for required variables and correct format");
            
            throw new InvalidOperationException("Environment validation failed. Application cannot start safely.");
        }

        logger.LogInformation("✅ Environment validation completed successfully");
        
        return app;
    }
}