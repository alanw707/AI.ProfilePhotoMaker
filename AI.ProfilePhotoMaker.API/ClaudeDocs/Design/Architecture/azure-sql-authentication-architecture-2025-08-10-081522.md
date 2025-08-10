---
title: "System Architecture: Azure SQL Database Authentication for Container Apps"
system_id: "AIPM-AUTH-001"
complexity: "medium"
status: "approved"
architectural_patterns:
  - "managed-identity"
  - "key-vault-integration"
  - "connection-pooling"
  - "retry-resilience"
scalability_metrics:
  current_capacity: "1K users"
  target_capacity: "10K users"
  scaling_approach: "horizontal"
technology_stack:
  - backend: "ASP.NET Core 8.0"
  - database: "Azure SQL Database"
  - secrets: "Azure Key Vault"
  - container: "Azure Container Apps"
design_timeline:
  start: "2025-08-10T08:15:22Z"
  review: "2025-08-10T09:00:00Z"
  completion: "2025-08-10T10:00:00Z"
linked_documents:
  - path: "infrastructure/simple-deploy.bicep"
  - path: "Services/Database/DatabaseProviderService.cs"
dependencies:
  - system: "azure-sql-server"
    type: "external"
  - system: "azure-key-vault"
    type: "external"
quality_attributes:
  - attribute: "security"
    priority: "critical"
  - attribute: "reliability"
    priority: "high"
  - attribute: "performance"
    priority: "high"
---

# Azure SQL Database Authentication Architecture

## Executive Summary

This document outlines the authentication architecture for connecting Azure Container Apps to Azure SQL Database in production. The solution prioritizes security, reliability, and maintainability while addressing the current authentication failures.

## Current State Analysis

### Problem Statement
- **Error**: Login failed for user 'aipmadmin' (Error 18456, State: 1)
- **Root Cause**: Username mismatch between configuration and actual SQL Server user
- **Impact**: Container Apps cannot connect to database, causing health check failures

### Configuration Analysis
```
Bicep Template: User ID=sqladmin
appsettings.Production.json: User ID=sqladmin  
Actual Error: Login failed for user 'aipmadmin'
Container App Environment Variable: ConnectionStrings__DefaultConnection (secretRef)
```

## Architecture Decision

### Option 1: SQL Authentication (Current - IMMEDIATE FIX)
**Decision**: Fix the immediate issue with SQL authentication
**Rationale**: Minimal changes required, fastest resolution

### Option 2: Managed Identity (RECOMMENDED - PHASE 2)
**Decision**: Migrate to Managed Identity after stabilization
**Rationale**: Enhanced security, no password management

## Implementation Strategy

### Phase 1: Immediate SQL Authentication Fix

#### 1.1 Connection String Correction
```csharp
// Updated appsettings.Production.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:aipm-sql-v1-6j74jubocuukg.database.windows.net,1433;Initial Catalog=aipmdb;User ID=sqladmin;Password={from-environment};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;MultipleActiveResultSets=true;Max Pool Size=100;Min Pool Size=5;"
  }
}
```

#### 1.2 Enhanced DatabaseProviderService
```csharp
public class DatabaseProviderService : IDatabaseProviderService
{
    public string GetConnectionString()
    {
        // Priority 1: Container Apps environment variable
        var containerAppConnString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrEmpty(containerAppConnString))
        {
            _logger.LogInformation("Using Container Apps connection string");
            return containerAppConnString;
        }
        
        // Priority 2: Configuration with password replacement
        var configConnString = _configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(configConnString))
        {
            // Replace password placeholder if exists
            var password = Environment.GetEnvironmentVariable("SQL_ADMIN_PASSWORD") 
                        ?? _configuration["Database:SqlAdminPassword"];
            
            if (!string.IsNullOrEmpty(password))
            {
                configConnString = configConnString.Replace("{from-environment}", password);
            }
            
            return configConnString;
        }
        
        throw new InvalidOperationException("No valid connection string found");
    }
}
```

#### 1.3 Connection Resilience Configuration
```csharp
options.UseSqlServer(connectionString, sqlOptions =>
{
    sqlOptions.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(30),
        errorNumbersToAdd: new[] { 
            4060, // Cannot open database
            40613, // Database not currently available
            40197, // Service error
            40501, // Service busy
            49918, // Cannot process request
            49919, // Cannot process create/update
            49920  // Cannot process delete
        });
    
    // Connection pooling optimization
    sqlOptions.CommandTimeout(30);
});
```

### Phase 2: Managed Identity Migration (Future)

#### 2.1 Architecture Overview
```
Container App → Managed Identity → Azure SQL Database
                                ↓
                        Azure Key Vault (backup secrets)
```

#### 2.2 Implementation Steps
1. Enable System-Assigned Managed Identity on Container App
2. Create database user for Managed Identity
3. Grant appropriate database permissions
4. Update connection string to use Authentication=Active Directory Managed Identity
5. Remove password from configuration

#### 2.3 Connection String for Managed Identity
```
Server=tcp:aipm-sql-v1.database.windows.net,1433;
Initial Catalog=aipmdb;
Authentication=Active Directory Managed Identity;
Encrypt=True;
TrustServerCertificate=False;
Connection Timeout=30;
```

## Security Considerations

### Current State (SQL Authentication)
- **Secrets Management**: Passwords stored in Container App secrets
- **Rotation**: Manual password rotation required
- **Audit**: SQL authentication logs available

### Target State (Managed Identity)
- **Zero Secrets**: No passwords in configuration
- **Automatic Rotation**: Azure handles token refresh
- **Enhanced Audit**: Azure AD authentication provides better audit trail

## Performance Optimization

### Connection Pooling Strategy
```csharp
public class OptimizedDatabaseConfiguration
{
    public void ConfigureConnectionPool(SqlConnectionStringBuilder builder)
    {
        builder.MinPoolSize = 5;        // Maintain minimum connections
        builder.MaxPoolSize = 100;      // Limit maximum connections
        builder.Pooling = true;         // Enable connection pooling
        builder.ConnectionLifetime = 300; // Recycle connections after 5 minutes
        builder.LoadBalanceTimeout = 60; // Load balance timeout
    }
}
```

### Query Performance
- Enable query result caching where appropriate
- Use async operations for all database calls
- Implement circuit breaker pattern for database failures

## Monitoring and Observability

### Health Checks
```csharp
public class DatabaseHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
            
            return HealthCheckResult.Healthy("Database connection successful");
        }
        catch (SqlException ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Database connection failed: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    ["error_number"] = ex.Number,
                    ["error_state"] = ex.State,
                    ["server"] = ExtractServerName(_connectionString)
                });
        }
    }
}
```

### Metrics Collection
- Connection pool statistics
- Query execution times
- Failed authentication attempts
- Connection retry counts

## Deployment Plan

### Immediate Actions (Phase 1)
1. **Update Container App secrets** with correct SQL username
2. **Verify firewall rules** allow Container App IP ranges
3. **Test connection** from Azure Cloud Shell
4. **Deploy updated configuration** with enhanced retry logic
5. **Monitor health endpoints** for successful connections

### Future Migration (Phase 2)
1. **Plan maintenance window** for Managed Identity migration
2. **Test in staging environment** first
3. **Implement gradual rollout** with traffic splitting
4. **Monitor authentication metrics** during transition
5. **Document new authentication flow** for operations team

## Risk Mitigation

### Immediate Risks
- **Risk**: Incorrect username in secrets
  - **Mitigation**: Verify exact username from Azure Portal
  - **Validation**: Test with sqlcmd from Cloud Shell

- **Risk**: Firewall blocking connections
  - **Mitigation**: Enable "Allow Azure Services" rule
  - **Validation**: Check Container App outbound IPs

### Long-term Risks
- **Risk**: Password exposure in logs
  - **Mitigation**: Implement log sanitization
  - **Validation**: Regular security audits

- **Risk**: Connection pool exhaustion
  - **Mitigation**: Proper pool configuration and monitoring
  - **Validation**: Load testing with expected traffic

## Success Criteria

### Phase 1 (Immediate)
- [ ] Health endpoints respond within 5 seconds
- [ ] No authentication errors in logs
- [ ] Database queries execute successfully
- [ ] Container Apps maintain "Healthy" status

### Phase 2 (Future)
- [ ] Zero passwords in configuration
- [ ] Managed Identity authentication working
- [ ] Automated token refresh functioning
- [ ] Enhanced security audit trail available

## Architecture Diagrams

### Current State (SQL Authentication)
```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│ Container App   │────▶│ SQL Connection   │────▶│ Azure SQL DB    │
│                 │     │ (User/Password)  │     │                 │
└─────────────────┘     └──────────────────┘     └─────────────────┘
         │                                                 │
         └──────────────── Secrets ────────────────────────┘
                    (Container App Secrets)
```

### Target State (Managed Identity)
```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│ Container App   │────▶│ Managed Identity │────▶│ Azure SQL DB    │
│ (System MSI)    │     │ (AAD Token)      │     │                 │
└─────────────────┘     └──────────────────┘     └─────────────────┘
         │                                                 │
         └──────────────── No Secrets ────────────────────┘
                      (Automatic Token Management)
```

## Conclusion

This architecture provides a clear path to resolve the immediate authentication issues while establishing a roadmap for enhanced security through Managed Identity. The phased approach ensures minimal disruption while improving the overall security posture of the application.

## References

- [Azure SQL Database Connection Troubleshooting](https://docs.microsoft.com/azure/sql-database/troubleshoot-connectivity)
- [Container Apps Managed Identity](https://docs.microsoft.com/azure/container-apps/managed-identity)
- [SQL Connection Pooling Best Practices](https://docs.microsoft.com/sql/connect/pooling)