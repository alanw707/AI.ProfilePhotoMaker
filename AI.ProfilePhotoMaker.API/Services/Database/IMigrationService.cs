namespace AI.ProfilePhotoMaker.API.Services.Database;

/// <summary>
/// Service for managing database migrations and schema validation
/// </summary>
public interface IMigrationService
{
    /// <summary>
    /// Apply all pending migrations
    /// </summary>
    Task<MigrationResult> ApplyMigrationsAsync();

    /// <summary>
    /// Check if there are pending migrations
    /// </summary>
    Task<MigrationStatus> GetMigrationStatusAsync();

    /// <summary>
    /// Validate database schema and seed data
    /// </summary>
    Task<ValidationResult> ValidateDatabaseAsync();

    /// <summary>
    /// Get applied migrations
    /// </summary>
    Task<IEnumerable<string>> GetAppliedMigrationsAsync();

    /// <summary>
    /// Get pending migrations
    /// </summary>
    Task<IEnumerable<string>> GetPendingMigrationsAsync();

    /// <summary>
    /// Ensure database is created
    /// </summary>
    Task<bool> EnsureDatabaseCreatedAsync();

    /// <summary>
    /// Get comprehensive database health status
    /// </summary>
    Task<DatabaseHealth> GetDatabaseHealthAsync();
}

/// <summary>
/// Result of migration operation
/// </summary>
public class MigrationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> AppliedMigrations { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Status of database migrations
/// </summary>
public class MigrationStatus
{
    public int PendingCount { get; set; }
    public int AppliedCount { get; set; }
    public List<string> PendingMigrations { get; set; } = new();
    public List<string> AppliedMigrations { get; set; } = new();
    public bool DatabaseExists { get; set; }
    public bool CanConnect { get; set; }
}

/// <summary>
/// Result of database validation
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Issues { get; set; } = new();
    public Dictionary<string, int> TableCounts { get; set; } = new();
    public bool HasRequiredSeedData { get; set; }
    public List<string> MissingSeedData { get; set; } = new();
}

/// <summary>
/// Comprehensive database health information
/// </summary>
public class DatabaseHealth
{
    public bool IsHealthy { get; set; }
    public bool CanConnect { get; set; }
    public MigrationStatus MigrationStatus { get; set; } = new();
    public ValidationResult ValidationResult { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}