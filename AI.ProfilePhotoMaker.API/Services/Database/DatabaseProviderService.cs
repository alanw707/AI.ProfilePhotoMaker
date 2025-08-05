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
        
        if (config.Provider == DatabaseProvider.SqlServer)
        {
            _logger.LogInformation("Configuring SQL Server provider with retry policy");
            options.UseSqlServer(connString, sqlServerOptions =>
            {
                sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: config.MaxRetryCount,
                    maxRetryDelay: config.MaxRetryDelay,
                    errorNumbersToAdd: null);
                sqlServerOptions.CommandTimeout(config.CommandTimeout);
            });
        }
        else
        {
            _logger.LogInformation("Configuring SQLite provider");
            options.UseSqlite(connString);
        }

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

    public bool IsAzureSqlServer(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return false;

        var indicators = new[]
        {
            "azure",
            "database.windows.net",
            "SqlServer",
            "Authentication=Active Directory",
            "Server=tcp:"
        };

        return indicators.Any(indicator => 
            connectionString.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    public string GetConnectionString()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            var fallbackConnection = _environment.IsDevelopment() 
                ? "Data Source=ProfilePhotoMaker.db"
                : throw new InvalidOperationException("No connection string configured for production environment");
                
            _logger.LogWarning("No connection string found, using fallback: {FallbackConnection}", 
                fallbackConnection.Split('=')[0] + "=***");
            return fallbackConnection;
        }

        return connectionString;
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
        var isAzureSql = IsAzureSqlServer(connectionString);
        
        return new DatabaseProviderConfig
        {
            Provider = isAzureSql ? DatabaseProvider.SqlServer : DatabaseProvider.SQLite,
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