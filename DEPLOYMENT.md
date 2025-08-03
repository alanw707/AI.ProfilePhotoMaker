# Production Deployment Guide

## Overview

This document provides the complete production-grade CI/CD pipeline implementation that addresses all previous deployment issues and implements Infrastructure as Code best practices.

## Key Issues Resolved

✅ **Migration Container Image**: Fixed migration job to use proper application container instead of `mcr.microsoft.com/k8se/quickstart:latest`  
✅ **Database Connectivity**: Resolved SQLite vs SQL Server connection issues with automatic provider detection  
✅ **Deployment Orchestration**: Implemented proper deployment sequence with validation gates  
✅ **Atomic Deployments**: All-or-nothing deployment strategy with automatic rollback  
✅ **Zero Manual Steps**: Complete automation from infrastructure to application validation  

## Architecture

### Multi-Stage Container Strategy

**Single Dockerfile** (`/home/alanw/projects/AI.ProfilePhotoMaker/Dockerfile`) builds:
- **API Container**: .NET 8 application with health checks
- **Migration Container**: Same source as API with EF Core migration tools
- **Frontend Container**: Angular SPA with nginx and security headers

### Deployment Pipeline Stages

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   STAGE 1       │───▶│   STAGE 2       │───▶│   STAGE 3       │───▶│   STAGE 4       │
│ VALIDATE &      │    │ INFRASTRUCTURE  │    │ BUILD &         │    │ DEPLOY &        │
│ BUILD           │    │ PROVISION       │    │ PUBLISH         │    │ VALIDATE        │
└─────────────────┘    └─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Deployment Files

### 1. CI/CD Pipeline
**File**: `/home/alanw/projects/AI.ProfilePhotoMaker/.github/workflows/deploy-production.yml`

**Features**:
- Multi-stage deployment with proper orchestration
- Comprehensive validation gates
- Automatic rollback on failure
- Security scanning and code quality checks
- Health verification and integration testing

### 2. Unified Dockerfile
**File**: `/home/alanw/projects/AI.ProfilePhotoMaker/Dockerfile`

**Multi-stage build**:
- Backend build with test execution
- Frontend build with Angular optimization
- Three production containers from single source
- Security hardening and health checks

### 3. Validation Scripts
**File**: `/home/alanw/projects/AI.ProfilePhotoMaker/.github/scripts/validate-deployment.sh`

**Validation coverage**:
- Infrastructure resource health
- Application endpoint availability
- Database connectivity
- API integration testing
- Performance baseline checks

### 4. Rollback Procedures
**File**: `/home/alanw/projects/AI.ProfilePhotoMaker/.github/scripts/rollback-deployment.sh`

**Rollback capabilities**:
- Emergency rollback to previous version
- Configuration backup and restore
- Traffic management during rollback
- Verification and reporting

### 5. Health Check Infrastructure
**File**: `/home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.API/Controllers/HealthController.cs`

**Health endpoints**:
- `/health` - Basic health check
- `/api/health/database` - Database connectivity
- `/api/health/storage` - Storage connectivity
- `/api/health/detailed` - Comprehensive health status

## Database Migration Strategy

### SQLite to SQL Server Compatibility

The application automatically detects database provider based on connection string:

```csharp
bool isAzureSqlServer = !string.IsNullOrEmpty(connectionString) && 
                       (connectionString.Contains("azure") || 
                        connectionString.Contains("database.windows.net") || 
                        connectionString.Contains("SqlServer") ||
                        connectionString.Contains("Authentication=Active Directory"));
```

### Migration Container

The migration container uses the same application source with additional command-line support:

- `--check-db-connection`: Validates database connectivity
- `--verify-migrations`: Confirms migration success

## Deployment Process

### Prerequisites

1. **Azure Authentication**: Configure GitHub secrets
   ```
   AZURE_CLIENT_ID
   AZURE_TENANT_ID  
   AZURE_SUBSCRIPTION_ID
   ```

2. **Application Secrets**: Configure in GitHub secrets
   ```
   SQL_ADMIN_PASSWORD
   JWT_SECRET
   REPLICATE_API_TOKEN
   STRIPE_API_KEY
   FACEBOOK_APP_SECRET
   GOOGLE_CLIENT_SECRET
   ```

### Manual Deployment

```bash
# Deploy to staging
gh workflow run deploy-production.yml -f environment=staging

# Deploy to production
gh workflow run deploy-production.yml -f environment=production

# Force deployment (skip validation gates)
gh workflow run deploy-production.yml -f environment=staging -f force_deploy=true
```

### Automatic Deployment

- **Main branch push**: Triggers staging deployment
- **Production deployment**: Manual workflow dispatch

## Validation Gates

### Stage 1: Code Quality
- Backend linting and build
- Frontend linting and build  
- Security dependency scanning
- Unit and integration tests
- Infrastructure template validation

### Stage 2: Infrastructure Health
- Resource provisioning verification
- Network connectivity validation
- Service health confirmation

### Stage 3: Build Verification
- Container build and test
- Image push to registry
- Configuration updates

### Stage 4: Application Validation
- Database migration execution
- Application health checks
- API integration testing
- Performance baseline verification

## Monitoring and Alerting

### Health Check Endpoints

```bash
# API Health
curl https://api-staging.aiprofilephotomaker.com/health

# Database Health  
curl https://api-staging.aiprofilephotomaker.com/api/health/database

# Detailed Health
curl https://api-staging.aiprofilephotomaker.com/api/health/detailed
```

### Application Insights Integration

Monitor key metrics:
- Response times
- Error rates  
- Database performance
- User activity

## Rollback Procedures

### Automatic Rollback

Triggers automatically on:
- Health check failures >5 minutes
- Error rate >5% for 10 consecutive minutes
- Database migration failures
- Critical security validation failures

### Manual Rollback

```bash
# Emergency rollback to previous version
./rollback-deployment.sh production

# Rollback to specific version
./rollback-deployment.sh production v20240103-a1b2c3d4

# Force rollback (skip confirmations)
./rollback-deployment.sh production latest-stable true
```

## Security Features

### Container Security
- Non-root user execution
- Minimal base images
- Security header configuration
- Dependency vulnerability scanning

### Network Security
- Private endpoints for all PaaS services
- Network Security Groups
- TLS encryption for all traffic
- Zero-trust architecture

### Secret Management  
- Azure Key Vault integration
- Managed Identity authentication
- No secrets in configuration files
- Automatic secret rotation support

## Performance Optimization

### Build Optimization
- Multi-stage builds for minimal image size
- Parallel build execution
- Build caching strategies
- Optimized layer ordering

### Runtime Optimization
- Gzip compression
- Static asset caching
- Database connection pooling
- Auto-scaling configuration

## Troubleshooting

### Common Issues

1. **Migration Failures**
   ```bash
   # Check migration logs
   az containerapp job execution logs show --name [migration-job] --execution-name [execution]
   
   # Verify database connectivity
   ./validate-deployment.sh staging
   ```

2. **Health Check Failures**
   ```bash
   # Test individual endpoints
   curl -v https://api-staging.aiprofilephotomaker.com/health
   
   # Check detailed health status
   curl https://api-staging.aiprofilephotomaker.com/api/health/detailed
   ```

3. **Container Startup Issues**
   ```bash
   # Check container logs
   az containerapp logs show --name [app-name] --resource-group [rg-name]
   
   # Verify container registry access
   az acr repository list --name [registry-name]
   ```

### Emergency Contacts

- **DevOps Team**: [Contact Information]
- **Database Team**: [Contact Information]  
- **Security Team**: [Contact Information]

## Next Steps

1. **Monitor deployment**: Verify all health checks pass
2. **Performance validation**: Confirm response times meet SLA
3. **User acceptance testing**: Validate critical user journeys
4. **Documentation updates**: Update operational runbooks

---

## File Summary

| File | Purpose |
|------|---------|
| `Dockerfile` | Unified multi-stage container build |
| `.github/workflows/deploy-production.yml` | Complete CI/CD pipeline |
| `.github/scripts/validate-deployment.sh` | Deployment validation |
| `.github/scripts/rollback-deployment.sh` | Emergency rollback procedures |
| `HealthController.cs` | Application health check endpoints |
| `nginx.conf` | Frontend nginx configuration |
| `docker-entrypoint.sh` | Frontend environment injection |

This deployment solution eliminates the entire class of issues experienced previously and provides a robust, secure, and fully automated deployment pipeline.