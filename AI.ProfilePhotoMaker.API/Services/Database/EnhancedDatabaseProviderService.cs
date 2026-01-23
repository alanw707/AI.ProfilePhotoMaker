using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace AI.ProfilePhotoMaker.API.Services.Database;

/// <summary>
/// Enhanced database provider service with improved authentication handling and connection resilience
/// </summary>
public class EnhancedDatabaseProviderService : IDatabaseProviderService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<EnhancedDatabaseProviderService> _logger;
    private readonly DatabaseProviderConfig _providerConfig;
    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sl(string? value) => LoggingSanitizer.Sanitize(value, 400);

    public EnhancedDatabaseProviderService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<EnhancedDatabaseProviderService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
        _providerConfig = InitializeProviderConfig();
    }

    public void ConfigureDbContextOptions<TContext>(DbContextOptionsBuilder<TContext> options, string? connectionString = null)
        where TContext : DbContext
    {
        var connString = connectionString ?? GetConnectionString();
        var config = GetProviderConfig();

        _logger.LogInformation("Configuring SQL Server provider with enhanced retry policy and connection pooling");

        options.UseSqlServer(connString, sqlServerOptions =>
        {
            // Enhanced retry policy with additional transient error codes
            sqlServerOptions.EnableRetryOnFailure(
                maxRetryCount: config.MaxRetryCount,
                maxRetryDelay: config.MaxRetryDelay,
                errorNumbersToAdd: new[] {
                    4060,  // Cannot open database
                    40613, // Database not currently available
                    40197, // Service error
                    40501, // Service busy
                    49918, // Cannot process request
                    49919, // Cannot process create/update
                    49920, // Cannot process delete
                    18456  // Login failed
                });

            sqlServerOptions.CommandTimeout(config.CommandTimeout);

            // Enable connection resiliency features
            sqlServerOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });

        // Development-specific configurations
        if (_environment.IsDevelopment())
        {
            if (config.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }

            if (config.EnableDetailedErrors)
            {
                options.EnableDetailedErrors();
            }
        }

        // Performance optimizations
        options.ConfigureWarnings(warnings =>
        {
            // Suppress common EF Core warnings in production
            if (!_environment.IsDevelopment())
            {
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.SensitiveDataLoggingEnabledWarning);
            }
        });
    }

    public string GetConnectionString()
    {
        try
        {
            // Priority 0: CI/CD (GitHub Actions) connection string passed explicitly for EF tooling.
            // This enables `dotnet ef migrations list` and `dotnet ef database update` steps to run
            // without requiring Container Apps environment variables to be set on the runner.
            var workflowConnectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING");
            if (!string.IsNullOrEmpty(workflowConnectionString))
            {
                _logger.LogInformation("Using SQL connection string from CI/CD environment variable");
                LogConnectionStringDetails(workflowConnectionString, "CI/CD");
                return EnhanceConnectionString(workflowConnectionString);
            }

            // Priority 1: Container Apps environment variable (already includes password)
            var containerAppConnString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            if (!string.IsNullOrEmpty(containerAppConnString))
            {
                _logger.LogInformation("Using Container Apps connection string from environment variable");
                LogConnectionStringDetails(containerAppConnString, "Container Apps");
                return EnhanceConnectionString(containerAppConnString);
            }

            // Priority 2: Configuration connection string
            var configConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(configConnectionString) &&
                !configConnectionString.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
            {
                // Check if password needs to be injected
                if (configConnectionString.Contains("{from-environment}", StringComparison.OrdinalIgnoreCase))
                {
                    var password = GetSqlPassword();
                    if (!string.IsNullOrEmpty(password))
                    {
                        configConnectionString = configConnectionString.Replace("{from-environment}", password);
                        _logger.LogInformation("Injected SQL password from environment into connection string");
                    }
                }

                _logger.LogInformation("Using connection string from configuration");
                LogConnectionStringDetails(configConnectionString, "Configuration");
                return EnhanceConnectionString(configConnectionString);
            }

            // Priority 3: Build connection string from individual components
            var builtConnectionString = BuildConnectionStringFromComponents();
            if (!string.IsNullOrEmpty(builtConnectionString))
            {
                _logger.LogInformation("Built connection string from individual components");
                LogConnectionStringDetails(builtConnectionString, "Built");
                return EnhanceConnectionString(builtConnectionString);
            }

            // Priority 4: Local development with environment variable
            var envPassword = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD");
            if (!string.IsNullOrEmpty(envPassword))
            {
                var server = _configuration.GetValue<string>("Database:Server", "localhost,1433");
                var database = _configuration.GetValue<string>("Database:Name", "AIProfileMaker");
                var localConnString = $"Server={server};Database={database};User Id=sa;Password={envPassword};TrustServerCertificate=true;MultipleActiveResultSets=true;";

                _logger.LogInformation("Using local development connection string with SA password");
                LogConnectionStringDetails(localConnString, "Local Development");
                return EnhanceConnectionString(localConnString);
            }

            throw new InvalidOperationException(
                "No valid database connection configured. " +
                "For production: Set ConnectionStrings__DefaultConnection environment variable. " +
                "For local development: Set MSSQL_SA_PASSWORD environment variable.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get database connection string: {Message}", S(ex.Message));
            throw;
        }
    }

    private string? BuildConnectionStringFromComponents()
    {
        var server = _configuration["Database:Server"]
                  ?? Environment.GetEnvironmentVariable("DATABASE_SERVER");
        var database = _configuration["Database:DatabaseName"]
                    ?? Environment.GetEnvironmentVariable("DATABASE_NAME");
        var userId = _configuration["Database:UserId"]
                  ?? Environment.GetEnvironmentVariable("DATABASE_USERID");
        var password = GetSqlPassword();

        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(database) ||
            string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            UserID = userId,
            Password = password,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 30,
            MultipleActiveResultSets = true
        };

        return builder.ConnectionString;
    }

    private string? GetSqlPassword()
    {
        // Try multiple sources for SQL password
        return Environment.GetEnvironmentVariable("SQL_ADMIN_PASSWORD")
            ?? Environment.GetEnvironmentVariable("SQL_PASSWORD")
            ?? Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD")
            ?? _configuration["Database:SqlAdminPassword"]
            ?? _configuration["SqlAdminPassword"];
    }

    private string EnhanceConnectionString(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);

            // Add connection pooling optimization
            if (builder.MinPoolSize == 0)
            {
                builder.MinPoolSize = 5; // Maintain minimum connections
            }

            if (builder.MaxPoolSize == 100) // Default value
            {
                builder.MaxPoolSize = 100; // Explicit maximum
            }

            // Ensure pooling is enabled
            builder.Pooling = true;

            // Connection lifetime is not a property of SqlConnectionStringBuilder
            // This was an error in the enhanced implementation

            // Ensure MultipleActiveResultSets for EF Core
            builder.MultipleActiveResultSets = true;

            // Set appropriate timeout
            if (builder.ConnectTimeout < 30)
            {
                builder.ConnectTimeout = 30;
            }

            return builder.ConnectionString;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enhance connection string, using as-is: {Message}", S(ex.Message));
            return connectionString;
        }
    }

    private void LogConnectionStringDetails(string connectionString, string source)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);

            _logger.LogInformation(
                "Connection string details from {Source}: Server={Server}, Database={Database}, User={User}, " +
                "Pooling={Pooling}, MinPool={MinPool}, MaxPool={MaxPool}, Timeout={Timeout}",
                S(source),
                MaskServerName(builder.DataSource),
                S(builder.InitialCatalog),
                S(builder.UserID),
                builder.Pooling,
                builder.MinPoolSize,
                builder.MaxPoolSize,
                builder.ConnectTimeout);
        }
        catch
        {
            _logger.LogInformation("Connection string from {Source} (unable to parse details)", S(source));
        }
    }

    private string MaskServerName(string server)
    {
        if (string.IsNullOrEmpty(server))
            return "unknown";

        if (server.Contains(".database.windows.net"))
        {
            var parts = server.Split('.');
            if (parts.Length > 0)
            {
                return $"{parts[0].Substring(0, Math.Min(4, parts[0].Length))}***.database.windows.net";
            }
        }

        return server.Length > 4 ? $"{server.Substring(0, 4)}***" : S(server);
    }

    public DatabaseProvider GetDatabaseProvider()
    {
        return _providerConfig.Provider;
    }

    public async Task<bool> CanConnectAsync()
    {
        const int maxAttempts = 3;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation("Testing database connectivity (attempt {Attempt}/{MaxAttempts})",
                    attempt, maxAttempts);

                var connectionString = GetConnectionString();
                using var connection = new SqlConnection(connectionString);

                // Set a reasonable timeout for the connection test
                connection.ConnectionString = new SqlConnectionStringBuilder(connectionString)
                {
                    ConnectTimeout = 15
                }.ConnectionString;

                await connection.OpenAsync();

                // Test with a simple query
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                command.CommandTimeout = 5;

                var result = await command.ExecuteScalarAsync();

                _logger.LogInformation("Database connectivity test successful on attempt {Attempt}", attempt);
                return true;
            }
            catch (SqlException sqlEx)
            {
                _logger.LogWarning(sqlEx,
                    "Database connectivity test failed on attempt {Attempt}/{MaxAttempts}. " +
                    "Error Number: {ErrorNumber}, State: {ErrorState}",
                    attempt, maxAttempts, sqlEx.Number, sqlEx.State);

                if (sqlEx.Number == 4060)
                {
                    if (await CanConnectToServerAsync())
                    {
                        _logger.LogInformation("Database not found yet; server connectivity verified.");
                        return true;
                    }
                }

                if (attempt == maxAttempts)
                {
                    _logger.LogError(sqlEx, "All database connectivity attempts failed: {Message}", S(sqlEx.Message));
                    return false;
                }

                // Wait before retrying
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))); // Exponential backoff
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during database connectivity test: {Message}", S(ex.Message));
                return false;
            }
        }

        return false;
    }

    public DatabaseProviderConfig GetProviderConfig()
    {
        return _providerConfig;
    }

    private async Task<bool> CanConnectToServerAsync()
    {
        try
        {
            var connectionString = GetConnectionString();
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = "master",
                ConnectTimeout = 15
            };

            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 5;

            await command.ExecuteScalarAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Server connectivity check failed: {Message}", S(ex.Message));
            return false;
        }
    }

    private DatabaseProviderConfig InitializeProviderConfig()
    {
        return new DatabaseProviderConfig
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = string.Empty, // Will be set dynamically
            MaxRetryCount = _configuration.GetValue<int>("Database:MaxRetryCount", 5),
            MaxRetryDelay = TimeSpan.FromSeconds(_configuration.GetValue<int>("Database:MaxRetryDelaySeconds", 30)),
            CommandTimeout = _configuration.GetValue<int>("Database:CommandTimeoutSeconds", 30),
            EnableSensitiveDataLogging = _environment.IsDevelopment() &&
                _configuration.GetValue<bool>("Database:EnableSensitiveDataLogging", false),
            EnableDetailedErrors = _environment.IsDevelopment() &&
                _configuration.GetValue<bool>("Database:EnableDetailedErrors", true)
        };
    }
}
