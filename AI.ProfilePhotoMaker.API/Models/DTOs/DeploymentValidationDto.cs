namespace AI.ProfilePhotoMaker.API.Models.DTOs;

/// <summary>
/// Response DTO for deployment validation results
/// </summary>
public class DeploymentValidationResponseDto
{
    public string Status { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public long Duration { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string DeploymentTarget { get; set; } = string.Empty;
    
    public Dictionary<string, ValidationComponentDto> Components { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    
    public DeploymentMetricsDto Metrics { get; set; } = new();
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Component validation result
/// </summary>
public class ValidationComponentDto
{
    public string Status { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string Description { get; set; } = string.Empty;
    public long Duration { get; set; }
    public string? Error { get; set; }
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
    public List<string> RequiredActions { get; set; } = new();
}

/// <summary>
/// Configuration validation response
/// </summary>
public class ConfigurationValidationResponseDto : DeploymentValidationResponseDto
{
    public Dictionary<string, EnvironmentVariableDto> EnvironmentVariables { get; set; } = new();
    public Dictionary<string, ConfigurationSectionDto> ConfigurationSections { get; set; } = new();
    public List<string> MissingRequired { get; set; } = new();
    public List<string> UsingDefaults { get; set; } = new();
}

/// <summary>
/// Environment variable validation status
/// </summary>
public class EnvironmentVariableDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
    public bool IsRequired { get; set; }
    public bool IsValid { get; set; }
    public string ValidationMessage { get; set; } = string.Empty;
    public bool IsSecure { get; set; }
    public string Source { get; set; } = string.Empty; // Environment, AppSettings, KeyVault, etc.
}

/// <summary>
/// Configuration section validation
/// </summary>
public class ConfigurationSectionDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public Dictionary<string, object> Values { get; set; } = new();
    public List<string> Issues { get; set; } = new();
    public bool IsComplete { get; set; }
}

/// <summary>
/// Performance validation response
/// </summary>
public class PerformanceValidationResponseDto : DeploymentValidationResponseDto
{
    public PerformanceBaselineDto Baseline { get; set; } = new();
    public PerformanceTestResultDto TestResults { get; set; } = new();
    public List<PerformanceIssueDto> Issues { get; set; } = new();
    public bool MeetsRequirements { get; set; }
}

/// <summary>
/// Performance baseline requirements
/// </summary>
public class PerformanceBaselineDto
{
    public int MaxResponseTimeMs { get; set; } = 1000;
    public double MaxErrorRatePercent { get; set; } = 5.0;
    public int MinThroughputRps { get; set; } = 10;
    public double MaxMemoryUsagePercent { get; set; } = 80.0;
    public double MaxCpuUsagePercent { get; set; } = 70.0;
    public int MaxDatabaseQueryTimeMs { get; set; } = 500;
    public int ConcurrentUserTarget { get; set; } = 200;
}

/// <summary>
/// Performance test results
/// </summary>
public class PerformanceTestResultDto
{
    public int AverageResponseTimeMs { get; set; }
    public double ErrorRatePercent { get; set; }
    public int ThroughputRps { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double CpuUsagePercent { get; set; }
    public int AverageDatabaseQueryTimeMs { get; set; }
    public int ConcurrentUsersSupported { get; set; }
    public DateTime TestTimestamp { get; set; }
    public Dictionary<string, object> DetailedMetrics { get; set; } = new();
}

/// <summary>
/// Performance issue
/// </summary>
public class PerformanceIssueDto
{
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public double ActualValue { get; set; }
    public double ExpectedValue { get; set; }
    public string Metric { get; set; } = string.Empty;
}

/// <summary>
/// Security validation response
/// </summary>
public class SecurityValidationResponseDto : DeploymentValidationResponseDto
{
    public List<SecurityCheckDto> SecurityChecks { get; set; } = new();
    public bool HasSecurityIssues { get; set; }
    public string SecurityScore { get; set; } = string.Empty;
    public Dictionary<string, List<string>> ComplianceStatus { get; set; } = new();
}

/// <summary>
/// Security check result
/// </summary>
public class SecurityCheckDto
{
    public string CheckName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Azure services validation response
/// </summary>
public class AzureValidationResponseDto : DeploymentValidationResponseDto
{
    public Dictionary<string, AzureServiceStatusDto> Services { get; set; } = new();
    public bool AllServicesHealthy { get; set; }
    public List<string> FailedServices { get; set; } = new();
    public AzureResourceHealthDto ResourceHealth { get; set; } = new();
}

/// <summary>
/// Azure service status
/// </summary>
public class AzureServiceStatusDto
{
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public bool IsConfigured { get; set; }
    public bool IsConnected { get; set; }
    public int ResponseTimeMs { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public Dictionary<string, object> Configuration { get; set; } = new();
    public DateTime LastChecked { get; set; }
}

/// <summary>
/// Azure resource health information
/// </summary>
public class AzureResourceHealthDto
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public Dictionary<string, string> ResourceTags { get; set; } = new();
    public List<string> AvailabilityZones { get; set; } = new();
    public bool HasManagedIdentity { get; set; }
    public string ManagedIdentityStatus { get; set; } = string.Empty;
}

/// <summary>
/// Database validation response
/// </summary>
public class DatabaseValidationResponseDto : DeploymentValidationResponseDto
{
    public DatabaseSchemaValidationDto Schema { get; set; } = new();
    public DatabaseDataValidationDto Data { get; set; } = new();
    public DatabasePerformanceDto Performance { get; set; } = new();
    public bool IsProductionReady { get; set; }
}

/// <summary>
/// Database schema validation
/// </summary>
public class DatabaseSchemaValidationDto
{
    public bool IsUpToDate { get; set; }
    public int PendingMigrations { get; set; }
    public List<string> PendingMigrationNames { get; set; } = new();
    public int AppliedMigrations { get; set; }
    public string LatestMigration { get; set; } = string.Empty;
    public List<string> SchemaIssues { get; set; } = new();
    public Dictionary<string, int> TableCounts { get; set; } = new();
}

/// <summary>
/// Database data validation
/// </summary>
public class DatabaseDataValidationDto
{
    public bool HasRequiredSeedData { get; set; }
    public Dictionary<string, int> SeedDataCounts { get; set; } = new();
    public List<string> MissingSeedData { get; set; } = new();
    public List<string> DataIntegrityIssues { get; set; } = new();
    public bool IsDataConsistent { get; set; }
}

/// <summary>
/// Database performance metrics
/// </summary>
public class DatabasePerformanceDto
{
    public int AverageConnectionTimeMs { get; set; }
    public int AverageQueryTimeMs { get; set; }
    public int ActiveConnections { get; set; }
    public int MaxConnections { get; set; }
    public double ConnectionPoolUtilization { get; set; }
    public List<SlowQueryDto> SlowQueries { get; set; } = new();
    public bool PerformanceIssuesDetected { get; set; }
}

/// <summary>
/// Slow query information
/// </summary>
public class SlowQueryDto
{
    public string Query { get; set; } = string.Empty;
    public int ExecutionTimeMs { get; set; }
    public int ExecutionCount { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

/// <summary>
/// Regression validation response
/// </summary>
public class RegressionValidationResponseDto : DeploymentValidationResponseDto
{
    public List<RegressionTestResultDto> TestResults { get; set; } = new();
    public bool AllTestsPassed { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public int SkippedTests { get; set; }
    public Dictionary<string, List<RegressionTestResultDto>> TestsByCategory { get; set; } = new();
}

/// <summary>
/// Regression test result
/// </summary>
public class RegressionTestResultDto
{
    public string TestName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public Dictionary<string, object> TestData { get; set; } = new();
    public string Severity { get; set; } = string.Empty;
    public List<string> AssertionResults { get; set; } = new();
}

/// <summary>
/// Deployment readiness score
/// </summary>
public class DeploymentReadinessScoreDto
{
    public double OverallScore { get; set; }
    public string ReadinessLevel { get; set; } = string.Empty;
    public bool IsReadyForDeployment { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    
    public Dictionary<string, ComponentScoreDto> ComponentScores { get; set; } = new();
    public List<string> BlockingIssues { get; set; } = new();
    public List<string> CriticalRecommendations { get; set; } = new();
    public List<string> OptionalRecommendations { get; set; } = new();
    
    public DeploymentRiskAssessmentDto RiskAssessment { get; set; } = new();
    public string NextSteps { get; set; } = string.Empty;
}

/// <summary>
/// Component readiness score
/// </summary>
public class ComponentScoreDto
{
    public string ComponentName { get; set; } = string.Empty;
    public double Score { get; set; }
    public int Weight { get; set; }
    public double WeightedScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Deployment risk assessment
/// </summary>
public class DeploymentRiskAssessmentDto
{
    public string RiskLevel { get; set; } = string.Empty;
    public double RiskScore { get; set; }
    public List<RiskFactorDto> RiskFactors { get; set; } = new();
    public List<string> MitigationStrategies { get; set; } = new();
    public bool RequiresManualReview { get; set; }
}

/// <summary>
/// Risk factor
/// </summary>
public class RiskFactorDto
{
    public string Factor { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Probability { get; set; } = string.Empty;
    public double RiskValue { get; set; }
    public string Mitigation { get; set; } = string.Empty;
}

/// <summary>
/// Deployment metrics
/// </summary>
public class DeploymentMetricsDto
{
    public DateTime DeploymentStartTime { get; set; }
    public DateTime? DeploymentEndTime { get; set; }
    public long TotalValidationTimeMs { get; set; }
    public int ComponentsValidated { get; set; }
    public int TestsExecuted { get; set; }
    public Dictionary<string, long> ComponentTimings { get; set; } = new();
    public Dictionary<string, object> CustomMetrics { get; set; } = new();
}

/// <summary>
/// Deployment health status
/// </summary>
public class DeploymentHealthDto
{
    public string HealthStatus { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
    public Dictionary<string, ServiceHealthDto> Services { get; set; } = new();
    public List<HealthIssueDto> Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();
}

/// <summary>
/// Service health information
/// </summary>
public class ServiceHealthDto
{
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public int ResponseTimeMs { get; set; }
    public double UptimePercentage { get; set; }
    public DateTime LastHealthCheck { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Health issue
/// </summary>
public class HealthIssueDto
{
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// Configuration drift response
/// </summary>
public class ConfigurationDriftResponseDto
{
    public bool HasDrift { get; set; }
    public string DriftSeverity { get; set; } = string.Empty;
    public DateTime BaselineTimestamp { get; set; }
    public DateTime CurrentTimestamp { get; set; }
    public List<ConfigurationDriftDto> DriftItems { get; set; } = new();
    public Dictionary<string, object> BaselineSnapshot { get; set; } = new();
    public Dictionary<string, object> CurrentSnapshot { get; set; } = new();
}

/// <summary>
/// Configuration drift item
/// </summary>
public class ConfigurationDriftDto
{
    public string ConfigurationKey { get; set; } = string.Empty;
    public string DriftType { get; set; } = string.Empty; // Added, Modified, Removed
    public object? BaselineValue { get; set; }
    public object? CurrentValue { get; set; }
    public string Impact { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

/// <summary>
/// Service availability response
/// </summary>
public class ServiceAvailabilityResponseDto
{
    public bool AllServicesAvailable { get; set; }
    public Dictionary<string, ExternalServiceStatusDto> Services { get; set; } = new();
    public List<string> UnavailableServices { get; set; } = new();
    public double OverallAvailabilityPercentage { get; set; }
    public DateTime LastChecked { get; set; }
}

/// <summary>
/// External service status
/// </summary>
public class ExternalServiceStatusDto
{
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public int ResponseTimeMs { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public DateTime LastSuccessfulCall { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
}

/// <summary>
/// Performance regression response
/// </summary>
public class PerformanceRegressionResponseDto
{
    public bool HasRegression { get; set; }
    public string RegressionSeverity { get; set; } = string.Empty;
    public Dictionary<string, PerformanceComparisonDto> Comparisons { get; set; } = new();
    public List<PerformanceRegressionIssueDto> Issues { get; set; } = new();
    public DateTime BaselineTimestamp { get; set; }
    public DateTime CurrentTimestamp { get; set; }
}

/// <summary>
/// Performance comparison data
/// </summary>
public class PerformanceComparisonDto
{
    public string MetricName { get; set; } = string.Empty;
    public double BaselineValue { get; set; }
    public double CurrentValue { get; set; }
    public double PercentageChange { get; set; }
    public string Trend { get; set; } = string.Empty; // Improved, Degraded, Stable
    public bool IsSignificant { get; set; }
    public string Impact { get; set; } = string.Empty;
}

/// <summary>
/// Performance regression issue
/// </summary>
public class PerformanceRegressionIssueDto
{
    public string MetricName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public double RegressionPercentage { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> PossibleCauses { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
}