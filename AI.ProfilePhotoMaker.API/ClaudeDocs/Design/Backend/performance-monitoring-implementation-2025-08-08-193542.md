---
title: "AI Profile Photo Maker Performance Monitoring Implementation"
type: "backend-design"
system: "ai-profile-photo-maker"
created: "2025-08-08 19:35:42"
agent: "backend-engineer"
api_version: "v1"
database_type: "sql"
security_level: "high"
performance_targets:
  response_time: "200ms"
  throughput: "200rps"
  availability: "99.9%"
technologies:
  - "ASP.NET Core 8.0"
  - "Azure Application Insights"
  - "Serilog"
  - "Performance Counters"
  - "Health Checks"
compliance:
  - "Azure Production Standards"
  - "Performance Monitoring Best Practices"
---

# AI Profile Photo Maker Performance Monitoring Implementation

## Overview

This document details the comprehensive performance monitoring and health check system implemented for the AI Profile Photo Maker application, designed to ensure Azure deployment readiness with production-grade observability.

## Architecture

### Core Components

1. **Performance Monitoring Service** (`IPerformanceMonitoringService`)
   - Real-time metrics collection
   - API response time tracking
   - Database query performance monitoring
   - External service call tracking
   - Resource utilization monitoring

2. **Application Insights Integration** (`IApplicationInsightsService`)
   - Azure telemetry integration
   - Custom events and metrics
   - Exception tracking
   - Dependency monitoring

3. **Performance Monitoring Middleware**
   - Request/response tracking
   - Correlation ID generation
   - Slow request detection
   - Error rate monitoring

4. **Enhanced Health Checks**
   - System component health
   - Performance-aware health status
   - Resource utilization alerts
   - External dependency monitoring

## Implementation Details

### Performance Metrics Collection

#### API Metrics
```csharp
public void RecordApiRequest(string endpoint, string method, int statusCode, long duration, string? userId = null)
{
    // Track in-memory metrics for immediate access
    // Forward to Application Insights for long-term storage
    // Generate alerts for threshold violations
}
```

**Tracked Metrics:**
- Request count and rate
- Response times (avg, p50, p95, p99)
- Error rates by endpoint
- User activity tracking
- Throughput (requests per second)

#### Database Performance
```csharp
public void RecordDatabaseQuery(string queryType, string tableName, long duration, int recordsAffected = 0)
{
    // Monitor query performance
    // Track by query type and table
    // Identify slow queries
    // Monitor connection pool utilization
}
```

**Tracked Metrics:**
- Query execution times
- Query type breakdown (SELECT, INSERT, UPDATE, DELETE)
- Table-specific performance
- Connection pool metrics
- Database health status

#### External Service Monitoring
```csharp
public void RecordExternalServiceCall(string serviceName, string operation, long duration, bool success, string? errorMessage = null)
{
    // Track service availability
    // Monitor response times
    // Aggregate error patterns
    // Service health assessment
}
```

**Monitored Services:**
- Replicate AI API
- Stripe Payment API
- Azure Storage
- Google OAuth

### Resource Utilization Monitoring

#### System Metrics
- Memory usage (working set, private, managed)
- CPU utilization
- Thread pool metrics
- Garbage collection statistics
- Process uptime

#### Performance Thresholds
```json
{
  "PerformanceThresholds": {
    "ApiResponseTimeWarningMs": 1000,
    "ApiResponseTimeCriticalMs": 5000,
    "ErrorRateWarningPercent": 5.0,
    "ErrorRateCriticalPercent": 15.0,
    "MemoryUsageWarningPercent": 80.0,
    "MemoryUsageCriticalPercent": 90.0
  }
}
```

## API Endpoints

### Monitoring Controller

#### GET /api/monitoring/metrics
Returns current performance metrics including:
- API request statistics
- Database performance data
- System resource utilization
- External service health

#### GET /api/monitoring/resources
Resource utilization information:
- Memory usage breakdown
- CPU utilization
- Thread pool status
- Performance warnings

#### GET /api/monitoring/alerts
Current performance alerts:
- Critical issues requiring immediate attention
- Performance degradation warnings
- Resource utilization alerts
- Recommended actions

### Enhanced Health Controller

#### GET /api/health/performance
Comprehensive health check with performance data:
- System component health
- Performance metrics summary
- Resource utilization status
- Performance indicators
- Actionable recommendations

## Azure Integration

### Application Insights Configuration

```json
{
  "ApplicationInsights": {
    "EnableAdaptiveSampling": true,
    "EnableQuickPulseMetricStream": true,
    "SamplingPercentage": 100.0,
    "MaxTelemetryItemsPerSecond": 1000
  }
}
```

**Telemetry Types:**
- Custom Events (API calls, business events)
- Custom Metrics (performance counters)
- Dependencies (external service calls)
- Exceptions (error tracking)
- Traces (structured logging)

### Structured Logging with Serilog

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.ApplicationInsights(connectionString, TelemetryConverter.Traces)
    .Enrich.WithProperty("Application", "AI.ProfilePhotoMaker.API")
    .CreateLogger();
```

## Health Check Strategy

### Multi-Level Health Checks

1. **Basic Health** (`/api/health`)
   - Simple alive/dead status
   - Fast response for load balancers

2. **Comprehensive Health** (`/api/health/comprehensive`)
   - All system components
   - Detailed component status
   - Performance metrics

3. **Performance Health** (`/api/health/performance`)
   - Health + performance data
   - Resource utilization
   - Performance alerts
   - Recommendations

### Component Health Checks

#### Database Health
- Connection testing
- Migration status validation
- Data integrity checks
- Query performance assessment

#### Storage Health
- Azure Blob Storage connectivity
- Local storage availability
- Operation testing (read/write)
- Configuration validation

#### External Dependencies
- API endpoint availability
- Authentication status
- Response time monitoring
- Error rate tracking

## Performance Alerting

### Alert Categories

1. **Critical Alerts**
   - System unresponsive
   - Memory exhaustion
   - Database connectivity failure
   - High error rates (>15%)

2. **High Priority Alerts**
   - Slow response times (>5s)
   - Resource utilization >90%
   - External service failures
   - Performance degradation

3. **Medium Priority Alerts**
   - Elevated response times (>1s)
   - Resource utilization 70-90%
   - Occasional service issues
   - Warning thresholds exceeded

### Alert Response

```csharp
public class PerformanceAlertDto
{
    public string Severity { get; set; } // Critical, High, Medium, Low
    public string Category { get; set; } // Performance, Resources, Dependencies
    public string Message { get; set; }
    public double CurrentValue { get; set; }
    public double Threshold { get; set; }
    public List<string> RecommendedActions { get; set; }
}
```

## Middleware Implementation

### Performance Monitoring Middleware

```csharp
public class PerformanceMonitoringMiddleware
{
    // Request correlation
    // Response time tracking
    // Error rate monitoring
    // Resource utilization
    // Custom metrics
}
```

**Features:**
- Automatic request tracking
- Correlation ID injection
- Slow request detection
- Memory usage monitoring
- Custom event tracking

### Middleware Pipeline Order

1. CORS configuration
2. **Performance Monitoring Middleware**
3. Authentication/Authorization
4. Static file serving
5. Route handling

## Configuration

### Monitoring Settings

```json
{
  "Monitoring": {
    "EnableDetailedLogging": true,
    "SlowRequestThresholdMs": 1000,
    "ExcludedPaths": ["/health", "/metrics", "/swagger"],
    "MetricsRetention": {
      "ApiMetricsMaxEntries": 1000,
      "MetricsRetentionHours": 24
    }
  }
}
```

### Health Check Configuration

```json
{
  "HealthChecks": {
    "DatabaseTimeoutSeconds": 30,
    "StorageTimeoutSeconds": 15,
    "ExternalServiceTimeoutSeconds": 10,
    "EnableDetailedHealthChecks": true
  }
}
```

## Deployment Considerations

### Azure Container Apps

1. **Environment Variables**
   ```
   APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=xxx
   Monitoring__EnableDetailedLogging=true
   ```

2. **Resource Limits**
   - Memory: 2GB recommended
   - CPU: 1 core minimum
   - Auto-scaling based on metrics

3. **Health Probes**
   - Liveness: `/api/health/live`
   - Readiness: `/api/health/ready`

### Production Optimizations

1. **Sampling Configuration**
   - Adaptive sampling for high traffic
   - Custom sampling rules for critical endpoints
   - Cost optimization

2. **Metric Aggregation**
   - In-memory caching for immediate access
   - Periodic flush to Application Insights
   - Retention policies for local metrics

## Usage Examples

### Recording Custom Metrics

```csharp
_performanceMonitoring.RecordCustomMetric("ImageProcessingDuration", duration, new Dictionary<string, string>
{
    ["operation"] = "enhance",
    ["userId"] = userId
});
```

### Tracking Long-Running Operations

```csharp
using var activity = _performanceMonitoring.StartOperation("ModelTraining", correlationId);
try
{
    await TrainModelAsync();
    _performanceMonitoring.CompleteOperation(activity, true);
}
catch (Exception ex)
{
    _performanceMonitoring.CompleteOperation(activity, false, ex.Message);
    throw;
}
```

### Health Check Integration

```csharp
[HttpGet("status")]
public async Task<IActionResult> GetStatus()
{
    var health = await _healthCheckService.GetComprehensiveHealthAsync();
    var metrics = await _performanceMonitoring.GetCurrentMetricsAsync();
    
    return Ok(new { health, metrics });
}
```

## Benefits

### For Operations Teams
- Real-time performance visibility
- Proactive issue detection
- Automated alerting
- Performance trend analysis

### For Development Teams
- Performance regression detection
- API usage analytics
- Error pattern identification
- Resource optimization guidance

### for Business Stakeholders
- Uptime monitoring
- User experience metrics
- Service level adherence
- Cost optimization insights

## Security Considerations

### Data Privacy
- No sensitive data in telemetry
- User ID anonymization options
- GDPR-compliant logging

### Access Control
- Monitoring endpoints require authentication
- Role-based access to detailed metrics
- Secure telemetry transmission

## Future Enhancements

1. **Advanced Analytics**
   - Machine learning for anomaly detection
   - Predictive scaling recommendations
   - Performance trend analysis

2. **Dashboard Integration**
   - Grafana dashboard templates
   - Azure Monitor integration
   - Custom business metrics

3. **Automated Responses**
   - Auto-scaling triggers
   - Circuit breaker patterns
   - Self-healing capabilities

## Conclusion

This comprehensive performance monitoring implementation provides:
- Production-ready observability
- Azure-native integration
- Automated health checking
- Performance optimization insights
- Proactive issue detection

The system is designed to support the AI Profile Photo Maker's requirement for high availability, optimal performance, and seamless Azure deployment while providing the visibility needed for effective operations and continuous improvement.