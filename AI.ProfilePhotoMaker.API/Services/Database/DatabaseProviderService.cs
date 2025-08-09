using AI.ProfilePhotoMaker.API.Data;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Database;

/// <summary>
/// Service for managing database provider configuration and connection logic
/// </summary>
public class DatabaseProviderService : IDatabaseProviderService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DatabaseProviderService> _logger;
    private readonly DatabaseProviderConfig _providerConfig;

    public DatabaseProviderService(
        IConfiguration configuration, 
        IWebHostEnvironment environment, 
        ILogger<DatabaseProviderService> logger)
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
        
        _logger.LogInformation("Configuring SQL Server provider with retry policy");
        options.UseSqlServer(connString, sqlServerOptions =>
        {
            sqlServerOptions.EnableRetryOnFailure(
                maxRetryCount: config.MaxRetryCount,
                maxRetryDelay: config.MaxRetryDelay,
                errorNumbersToAdd: null);
            sqlServerOptions.CommandTimeout(config.CommandTimeout);
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
        // First try to get from environment variables
        var envPassword = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD");
        if (!string.IsNullOrEmpty(envPassword))
        {
            // Build connection string using environment variable
            var server = _configuration.GetValue<string>("Database:Server", "localhost,1433");
            var database = _configuration.GetValue<string>("Database:Name", "AIProfileMaker");
            var connectionString = $"Server={server};Database={database};User Id=sa;Password={envPassword};TrustServerCertificate=true;MultipleActiveResultSets=true;";
            _logger.LogInformation("Using environment variable for database password");
            _logger.LogDebug("Connection string: {ConnectionString}", connectionString.Replace(envPassword, "***"));
            return connectionString;
        }
        
        // Fallback to configuration
        var configConnectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(configConnectionString))
        {
            throw new InvalidOperationException("No SQL Server connection string configured. Please set MSSQL_SA_PASSWORD environment variable or DefaultConnection in appsettings.");
        }

        return configConnectionString;
    }

    public DatabaseProvider GetDatabaseProvider()
    {
        return _providerConfig.Provider;
    }

    public async Task<bool> CanConnectAsync()
    {
        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            ConfigureDbContextOptions(optionsBuilder);
            
            using var context = new ApplicationDbContext(optionsBuilder.Options);
            return await context.Database.CanConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connectivity test failed");
            return false;
        }
    }

    public DatabaseProviderConfig GetProviderConfig()
    {
        return _providerConfig;
    }

    private DatabaseProviderConfig InitializeProviderConfig()
    {
        var connectionString = GetConnectionString();
        
        return new DatabaseProviderConfig
        {
            Provider = DatabaseProvider.SqlServer,
            ConnectionString = connectionString,
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