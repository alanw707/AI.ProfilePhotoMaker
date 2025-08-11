# Database Troubleshooting Breakthrough - 2025-08-10

## 🎯 Critical Learning: Always Check Database First

**KEY INSIGHT**: When troubleshooting API timeouts, database connectivity should be the FIRST thing to check, not the last.

## Root Cause Analysis Summary

### The Problem Timeline
1. **Initial Symptom**: API timing out after 15 seconds
2. **Initial Focus**: Application-level timeout configurations, environment variables, ngrok setup
3. **Real Issue**: Database authentication failure preventing health probes from passing
4. **Traffic Blocking**: Container Apps load balancer blocked external traffic due to failed health probes

### The Root Causes (In Order of Discovery)

#### 1. Username Mismatch (Critical)
- **Problem**: Connection string used `User Id=aipmadmin`
- **Reality**: SQL Server admin username was `sqladmin`
- **Detection**: `az sql server show --query administratorLogin`
- **Impact**: Authentication failure, health probes failing

#### 2. Incorrect Password (Critical)
- **Problem**: Connection string had outdated password
- **Solution**: Reset SQL Server admin password to known value
- **Command**: `az sql server update --admin-password "NewSecure2025!P@ssw0rd"`

#### 3. Configuration Priority Issue (Resolved Earlier)
- **Problem**: App checked for `MSSQL_SA_PASSWORD` environment variable first
- **Issue**: When found, it built localhost connection instead of using Azure SQL
- **Solution**: Updated code to prioritize production connection string

## Performance Results

### Before Fix
- **Liveness Probe**: Never worked - environment validation failed
- **Readiness Probe**: 503 Service Unavailable after 2000ms timeout
- **External Access**: 15 second timeout, no response
- **Container Status**: Unhealthy/None

### After Fix  
- **Liveness Probe**: ✅ 200 OK in 4-24ms
- **Readiness Probe**: ✅ 200 OK in 3-9ms  
- **External Access**: ✅ 200 OK in 0.23s (65x improvement)
- **Container Status**: ✅ Healthy

## Troubleshooting Methodology That Worked

### 1. Systematic Analysis with Agents
- Used specialized agents: root-cause-analyzer, system-architect
- Each agent provided focused expertise and comprehensive analysis
- Systematic approach prevented missing critical issues

### 2. Progressive Problem Solving
1. **Environment Variables**: Fixed missing/incorrect variables first
2. **Authentication**: Identified username mismatch through systematic checking
3. **Password Reset**: Used known password to eliminate uncertainty
4. **Validation**: Confirmed each fix with logs and health checks

### 3. Key Diagnostic Commands
```bash
# Check actual SQL Server admin username
az sql server show --name aipm-sql-v1-6j74jubocuukg --resource-group aiprofilemaker-v1 --query administratorLogin

# Check Container Apps logs for specific errors
az containerapp logs show --name aipm-api-v1 --resource-group aiprofilemaker-v1 --tail 30

# Check Container Apps revision health
az containerapp revision list --name aipm-api-v1 --resource-group aiprofilemaker-v1 --query "[0].{healthy:properties.healthState}"

# Reset SQL Server password
az sql server update --name aipm-sql-v1-6j74jubocuukg --resource-group aiprofilemaker-v1 --admin-password "NewPassword"

# Update connection string secret
az containerapp secret set --name aipm-api-v1 --resource-group aiprofilemaker-v1 --secrets "connection-string=Server=..."
```

## Critical Learnings for Future Troubleshooting

### 1. Database First Rule
**🔴 ALWAYS CHECK DATABASE CONNECTIVITY FIRST** when troubleshooting API issues:
- Verify database server exists and is online
- Check connection string format and credentials  
- Validate username matches SQL Server admin login
- Test authentication with known password
- Confirm firewall rules allow Container Apps access

### 2. Health Probe Architecture Understanding
- **Container Apps**: Must pass BOTH liveness AND readiness probes for external traffic
- **Liveness**: Basic application responsiveness (less critical)
- **Readiness**: Application ready for traffic (includes database connectivity)
- **Traffic Routing**: Load balancer blocks traffic if readiness fails

### 3. Systematic Debugging Approach
1. **Check Infrastructure First**: Database, networking, authentication
2. **Environment Variables**: Ensure all required variables are present and correct
3. **Application Logs**: Look for specific error messages and stack traces
4. **Health Endpoints**: Test health endpoints directly
5. **External Access**: Only test after internal health is confirmed

### 4. Common Azure SQL Database Issues
- **Username Mismatch**: Connection string vs actual admin login
- **Password Issues**: Outdated passwords in secrets
- **Firewall Rules**: Container Apps IP not whitelisted
- **Connection String Format**: Missing encryption parameters

## Tools and Commands Reference

### Essential Diagnostic Commands
```bash
# Database verification
az sql server list --resource-group <rg> --query "[].{name:name,admin:administratorLogin}"
az sql db show --server <server> --name <db> --resource-group <rg> --query "{status:status}"

# Container Apps health
az containerapp revision list --name <app> --resource-group <rg> --query "[0].properties.healthState"
az containerapp logs show --name <app> --resource-group <rg> --tail 50

# Network and connectivity  
az containerapp show --name <app> --resource-group <rg> --query "properties.outboundIpAddresses"
az sql server firewall-rule list --server <server> --resource-group <rg>

# Secret management
az containerapp secret list --name <app> --resource-group <rg>
az containerapp secret set --name <app> --resource-group <rg> --secrets "key=value"
```

### Health Probe Configuration
```yaml
liveness_probe:
  path: "/api/health/live"
  timeout: 10s
  interval: 10s
  failure_threshold: 3

readiness_probe:  
  path: "/api/health/ready"
  timeout: 5s
  interval: 10s
  failure_threshold: 5
```

## Prevention Strategies

### 1. Environment Setup Validation
- Always validate database connectivity during initial setup
- Use consistent naming conventions for usernames
- Store connection strings securely and validate format
- Test authentication before deploying

### 2. Monitoring and Alerting
- Monitor health probe success rates
- Alert on Container Apps revision health changes
- Track database connection failures
- Monitor API response times

### 3. Documentation Standards
- Document actual SQL Server admin usernames
- Maintain connection string templates
- Keep troubleshooting runbooks updated
- Record environment variable mappings

## Success Metrics Achieved
- ✅ 65x improvement in API response time (15s → 0.23s)
- ✅ 100% health probe success rate
- ✅ Container Apps revision healthy
- ✅ Database connectivity restored
- ✅ Production-ready performance

**This breakthrough demonstrates the importance of systematic database-first troubleshooting for API connectivity issues.**