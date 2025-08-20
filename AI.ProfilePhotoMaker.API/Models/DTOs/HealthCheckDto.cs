using System.Text.Json.Serialization;

namespace AI.ProfilePhotoMaker.API.Models.DTOs;

/// <summary>
/// Health check response DTO for API endpoints
/// </summary>
public class HealthCheckResponseDto
{
    /// <summary>
    /// Overall health status
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Healthy";

    /// <summary>
    /// Timestamp of the health check
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional message with additional information
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Duration of the health check in milliseconds
    /// </summary>
    [JsonPropertyName("duration")]
    public long Duration { get; set; }

    /// <summary>
    /// Version information
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// Environment information
    /// </summary>
    [JsonPropertyName("environment")]
    public string? Environment { get; set; }
}

/// <summary>
/// Comprehensive health check response with detailed information
/// </summary>
public class ComprehensiveHealthResponseDto : HealthCheckResponseDto
{
    /// <summary>
    /// Individual component health checks
    /// </summary>
    [JsonPropertyName("components")]
    public Dictionary<string, ComponentHealthDto> Components { get; set; } = new();

    /// <summary>
    /// System metrics
    /// </summary>
    [JsonPropertyName("metrics")]
    public Dictionary<string, object> Metrics { get; set; } = new();

    /// <summary>
    /// Warnings (non-critical issues)
    /// </summary>
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Errors (if any)
    /// </summary>
    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Individual component health information
/// </summary>
public class ComponentHealthDto
{
    /// <summary>
    /// Component status
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Healthy";

    /// <summary>
    /// Component description
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Duration of the component check in milliseconds
    /// </summary>
    [JsonPropertyName("duration")]
    public long Duration { get; set; }

    /// <summary>
    /// Component-specific data
    /// </summary>
    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Error message if unhealthy
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Timestamp of the component check
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Database-specific health check response
/// </summary>
public class DatabaseHealthResponseDto : HealthCheckResponseDto
{
    /// <summary>
    /// Database provider type
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    /// <summary>
    /// Connection status
    /// </summary>
    [JsonPropertyName("canConnect")]
    public bool CanConnect { get; set; }

    /// <summary>
    /// Database exists
    /// </summary>
    [JsonPropertyName("exists")]
    public bool Exists { get; set; }

    /// <summary>
    /// Migration information
    /// </summary>
    [JsonPropertyName("migrations")]
    public MigrationStatusDto? Migrations { get; set; }

    /// <summary>
    /// Data validation results
    /// </summary>
    [JsonPropertyName("validation")]
    public DataValidationDto? Validation { get; set; }
}

/// <summary>
/// Migration status information
/// </summary>
public class MigrationStatusDto
{
    /// <summary>
    /// Number of applied migrations
    /// </summary>
    [JsonPropertyName("appliedCount")]
    public int AppliedCount { get; set; }

    /// <summary>
    /// Number of pending migrations
    /// </summary>
    [JsonPropertyName("pendingCount")]
    public int PendingCount { get; set; }

    /// <summary>
    /// List of pending migrations
    /// </summary>
    [JsonPropertyName("pendingMigrations")]
    public List<string> PendingMigrations { get; set; } = new();

    /// <summary>
    /// Latest applied migration
    /// </summary>
    [JsonPropertyName("latestMigration")]
    public string? LatestMigration { get; set; }
}

/// <summary>
/// Data validation results
/// </summary>
public class DataValidationDto
{
    /// <summary>
    /// Overall validation status
    /// </summary>
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    /// <summary>
    /// Table counts
    /// </summary>
    [JsonPropertyName("tableCounts")]
    public Dictionary<string, int> TableCounts { get; set; } = new();

    /// <summary>
    /// Required seed data status
    /// </summary>
    [JsonPropertyName("hasRequiredSeedData")]
    public bool HasRequiredSeedData { get; set; }

    /// <summary>
    /// Missing seed data items
    /// </summary>
    [JsonPropertyName("missingSeedData")]
    public List<string> MissingSeedData { get; set; } = new();

    /// <summary>
    /// Validation issues
    /// </summary>
    [JsonPropertyName("issues")]
    public List<string> Issues { get; set; } = new();
}

/// <summary>
/// Storage health check response
/// </summary>
public class StorageHealthResponseDto : HealthCheckResponseDto
{
    /// <summary>
    /// Storage provider type
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    /// <summary>
    /// Indicates whether a storage connection string is configured
    /// </summary>
    [JsonPropertyName("hasConnectionString")]
    public bool HasConnectionString { get; set; }

    /// <summary>
    /// Indicates whether the Azure Storage emulator is in use
    /// </summary>
    [JsonPropertyName("isEmulator")]
    public bool IsEmulator { get; set; }

    /// <summary>
    /// Connection status
    /// </summary>
    [JsonPropertyName("canConnect")]
    public bool CanConnect { get; set; }

    /// <summary>
    /// Test operation results
    /// </summary>
    [JsonPropertyName("testResults")]
    public Dictionary<string, bool> TestResults { get; set; } = new();

    /// <summary>
    /// Configuration details
    /// </summary>
    [JsonPropertyName("configuration")]
    public Dictionary<string, object> Configuration { get; set; } = new();
}

/// <summary>
/// Dependencies health check response
/// </summary>
public class DependenciesHealthResponseDto : HealthCheckResponseDto
{
    /// <summary>
    /// Individual dependency statuses
    /// </summary>
    [JsonPropertyName("dependencies")]
    public Dictionary<string, DependencyStatusDto> Dependencies { get; set; } = new();
}

/// <summary>
/// Individual dependency status
/// </summary>
public class DependencyStatusDto
{
    /// <summary>
    /// Dependency name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Status
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Unknown";

    /// <summary>
    /// Response time in milliseconds
    /// </summary>
    [JsonPropertyName("responseTime")]
    public long ResponseTime { get; set; }

    /// <summary>
    /// Version information
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// Error message if unhealthy
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}
