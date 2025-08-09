using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Database;

/// <summary>
/// Service for managing database provider configuration and connection logic
/// </summary>
public interface IDatabaseProviderService
{
    /// <summary>
    /// Configure DbContext options based on connection string and environment
    /// </summary>
    void ConfigureDbContextOptions<TContext>(DbContextOptionsBuilder<TContext> options, string? connectionString = null) 
        where TContext : DbContext;
    
    /// <summary>
    /// Get the appropriate connection string for the current environment
    /// </summary>
    string GetConnectionString();
    
    /// <summary>
    /// Get database provider type for current configuration
    /// </summary>
    DatabaseProvider GetDatabaseProvider();
    
    /// <summary>
    /// Test database connectivity
    /// </summary>
    Task<bool> CanConnectAsync();
    
    /// <summary>
    /// Get database provider-specific configuration
    /// </summary>
    DatabaseProviderConfig GetProviderConfig();
}

/// <summary>
/// Database provider types
/// </summary>
public enum DatabaseProvider
{
    SQLite,
    SqlServer
}

/// <summary>
/// Database provider configuration settings
/// </summary>
public class DatabaseProviderConfig
{
    public DatabaseProvider Provider { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public int MaxRetryCount { get; set; } = 5;
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
    public int CommandTimeout { get; set; } = 30;
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public bool EnableDetailedErrors { get; set; } = false;
}