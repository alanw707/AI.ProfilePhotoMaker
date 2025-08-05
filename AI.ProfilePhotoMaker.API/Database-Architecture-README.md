# Database Architecture Documentation

## Overview

This document describes the comprehensive EF Core migration architecture designed for the AI ProfilePhotoMaker application. The architecture provides robust database management with cross-platform support, comprehensive health checks, and seamless CI/CD integration.

## Architecture Components

### 1. Database Provider Service (`IDatabaseProviderService`)

**Purpose**: Centralized database configuration and provider management

**Features**:
- Automatic database provider detection (SQLite for development, SQL Server for production)
- Environment-specific connection string handling
- Retry policies and connection resilience
- Performance optimization settings

**Location**: `Services/Database/DatabaseProviderService.cs`

### 2. Migration Management Service (`IMigrationService`)

**Purpose**: Comprehensive migration operations and database validation

**Features**:
- Apply and verify migrations
- Database health monitoring
- Schema validation
- Seed data verification
- Performance metrics collection

**Location**: `Services/Database/MigrationService.cs`

### 3. Health Check System

**Components**:
- `DatabaseHealthCheck`: Database connectivity validation
- `MigrationHealthCheck`: Migration status verification
- `HealthController`: REST API endpoints for health monitoring

**Endpoints**:
- `GET /api/health` - Basic health check
- `GET /api/health/database` - Database connectivity
- `GET /api/health/migrations` - Migration status
- `GET /api/health/comprehensive` - Full database health
- `GET /api/health/validation` - Database validation
- `GET /api/health/ready` - Kubernetes readiness probe
- `GET /api/health/live` - Kubernetes liveness probe

### 4. Enhanced DbContext

**Improvements**:
- Organized configuration methods
- Performance indexes for all major queries
- Comprehensive relationship configuration
- Proper decimal precision settings
- Named indexes for better maintenance

**Location**: `Data/ApplicationDbContext.cs`

### 5. Command-Line Interface

**Purpose**: CI/CD integration and manual database operations

**Commands**:
- `--check-db-connection` - Test database connectivity
- `--verify-migrations` - Check migration status
- `--apply-migrations` - Apply pending migrations
- `--validate-database` - Validate database structure and data
- `--migration-status` - Display detailed migration information
- `--database-health` - Comprehensive health assessment

**Location**: `Services/Database/MigrationCommandService.cs`

## Database Configuration

### Connection Strings

#### Development (SQLite)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=aiprofilemaker.db"
  }
}
```

#### Production (SQL Server)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:your-server.database.windows.net,1433;Database=YourDatabase;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

### Database Options

```json
{
  "Database": {
    "MaxRetryCount": 5,
    "MaxRetryDelaySeconds": 30,
    "CommandTimeoutSeconds": 30,
    "EnableSensitiveDataLogging": false,
    "EnableDetailedErrors": false,
    "AutoMigrateOnStartup": true,
    "ValidateOnStartup": true
  }
}
```

## Performance Optimizations

### Indexes Created

#### User-Related Performance
- `IX_UserProfiles_UserId` - User profile lookups
- `IX_UsageLogs_UserId` - User activity queries
- `IX_UsageLogs_Timestamp` - Time-based queries

#### Content Performance
- `IX_ProcessedImages_UserProfileId` - User image queries
- `IX_ProcessedImages_CreatedAt` - Time-based image queries
- `IX_Styles_IsActive` - Active style filtering
- `IX_Styles_IsActive_Name` - Combined style queries

#### Business Logic Performance
- `IX_CreditPackages_IsActive_DisplayOrder` - Package display queries
- `IX_CreditPurchases_UserId` - User purchase history
- `IX_Subscriptions_DateRange` - Subscription period queries
- `IX_ModelCreationRequests_Status` - Background service queries

### Query Optimizations

1. **Composite Indexes**: Multi-column indexes for complex queries
2. **Covering Indexes**: Include commonly accessed columns
3. **Filtered Indexes**: Partial indexes for active records only
4. **Named Indexes**: Consistent naming for maintenance

## CI/CD Integration

### Docker/Container Support

#### Migration Script
```bash
./Scripts/migrate-database.sh migrate
```

#### Available Commands
- `migrate` - Complete migration process (default)
- `check` - Test database connection
- `status` - Show migration status
- `validate` - Validate database
- `health` - Comprehensive health check

#### Exit Codes
- `0` - Success
- `1` - Migration/validation failed

### Container Health Checks

#### Dockerfile Integration
```dockerfile
# Add health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:8080/api/health/ready || exit 1
```

#### Kubernetes Integration
```yaml
livenessProbe:
  httpGet:
    path: /api/health/live
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /api/health/ready
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 5
```

## Data Seeding

### Credit Packages (Automatic)
The system automatically seeds three credit packages:
1. **Starter Pack** - 50 credits, $9.99
2. **Professional Pack** - 120 credits + 30 bonus, $19.99
3. **Studio Pack** - 300 credits + 100 bonus, $39.99

### Styles (From Migration)
The system includes 20 professional styles from the existing migration:
- Professional & Career styles (corporate, executive, consultant, linkedin, legal, medical, author)
- Modern Entrepreneur & Tech styles (entrepreneur, startup, tech professional, influencer, digital nomad)
- Creative & Expressive styles (creative, casual, artistic, edgy/urban, glamour)
- Lifestyle & Identity styles (academic, fitness, spiritual)

## Security Considerations

### Connection String Security
- Production connection strings use environment variables
- Managed Identity for Azure SQL Server
- No passwords in configuration files
- Connection string masking in logs

### Migration Security
- Read-only operations for validation
- Proper error handling without sensitive data exposure
- Audit logging for all migration operations
- Rollback capabilities for failed migrations

## Monitoring and Alerting

### Health Check Endpoints
All health checks return structured JSON with:
- Status indicator
- Detailed metrics
- Timestamp information
- Error details when applicable

### Logging
- Structured logging with correlation IDs
- Performance metrics collection
- Error tracking with context
- Migration audit trail

## Troubleshooting

### Common Issues

#### Connection Failures
1. Check connection string configuration
2. Verify network connectivity
3. Validate authentication/permissions
4. Check firewall settings

#### Migration Failures
1. Review migration logs
2. Check database permissions
3. Verify schema compatibility
4. Validate seed data integrity

#### Performance Issues
1. Monitor index usage with `--database-health`
2. Check query execution plans
3. Validate connection pool settings
4. Review timeout configurations

### Diagnostic Commands

```bash
# Test connectivity
dotnet AI.ProfilePhotoMaker.API.dll --check-db-connection

# Check migration status
dotnet AI.ProfilePhotoMaker.API.dll --migration-status

# Validate database
dotnet AI.ProfilePhotoMaker.API.dll --validate-database

# Full health check
dotnet AI.ProfilePhotoMaker.API.dll --database-health
```

## Best Practices

### Development
1. Always run migrations in development first
2. Use SQLite for local development
3. Enable detailed error logging in development
4. Test migration rollback procedures

### Production
1. Use managed identity for authentication
2. Enable retry policies for resilience
3. Monitor health check endpoints
4. Implement proper backup strategies
5. Use Blue-Green deployments for schema changes

### Performance
1. Monitor query performance regularly
2. Review index usage statistics
3. Optimize connection pool settings
4. Implement proper caching strategies

## Migration History

The database maintains a complete migration history through EF Core's `__EFMigrationsHistory` table. This provides:
- Version tracking
- Rollback capabilities
- Audit trail
- Schema evolution history

## Future Enhancements

1. **Database Sharding**: For horizontal scaling
2. **Read Replicas**: For read-heavy workloads
3. **Advanced Monitoring**: Custom metrics and alerting
4. **Automated Performance Tuning**: Query optimization suggestions
5. **Multi-Region Support**: Geographic distribution

---

## Quick Start

1. **Development Setup**:
   ```bash
   # Set development environment
   export ASPNETCORE_ENVIRONMENT=Development
   
   # Run migrations
   dotnet run --project AI.ProfilePhotoMaker.API
   ```

2. **Production Deployment**:
   ```bash
   # Run migration script
   ./Scripts/migrate-database.sh migrate
   
   # Start application
   dotnet AI.ProfilePhotoMaker.API.dll
   ```

3. **Health Monitoring**:
   ```bash
   # Check health
   curl http://localhost:8080/api/health/comprehensive
   ```

This architecture provides a robust, scalable, and maintainable database foundation for the AI ProfilePhotoMaker application.