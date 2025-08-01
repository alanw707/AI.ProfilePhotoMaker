# Operational Runbook - AI Profile Photo Maker

## Overview

This runbook provides comprehensive operational procedures for maintaining, monitoring, and troubleshooting the AI Profile Photo Maker deployment in production and staging environments.

## Table of Contents

1. [Daily Operations](#daily-operations)
2. [Monitoring & Alerting](#monitoring--alerting)
3. [Incident Response](#incident-response)
4. [Maintenance Procedures](#maintenance-procedures)
5. [Disaster Recovery](#disaster-recovery)
6. [Performance Optimization](#performance-optimization)
7. [Security Operations](#security-operations)
8. [Troubleshooting Guide](#troubleshooting-guide)

---

## Daily Operations

### Morning Health Check (5 minutes)

```bash
# Quick health validation
./validate-deployment-comprehensive.sh staging
./validate-deployment-comprehensive.sh production

# Check recent deployments
gh run list --limit 5

# Monitor Azure resources
az resource list --resource-group "ai-profile-photo-maker-production" \
  --query "[?properties.provisioningState != 'Succeeded'].{Name:name,Status:properties.provisioningState}" \
  -o table
```

### Weekly Health Report (15 minutes)

```bash
# Generate comprehensive reports
./validate-deployment-comprehensive.sh staging --verbose > weekly-health-staging.txt
./validate-deployment-comprehensive.sh production --verbose > weekly-health-production.txt

# Check resource utilization
az monitor metrics list --resource "/subscriptions/{subscription-id}/resourceGroups/ai-profile-photo-maker-production/providers/Microsoft.Web/sites/{app-name}" \
  --metric "CpuPercentage,MemoryPercentage,Http5xx" \
  --interval PT1H \
  --start-time $(date -d '7 days ago' -u +%Y-%m-%dT%H:%M:%SZ) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%SZ)
```

---

## Monitoring & Alerting

### Key Performance Indicators (KPIs)

| Metric | Target | Warning | Critical |
|--------|---------|---------|----------|
| API Response Time | < 500ms | > 1s | > 3s |
| API Availability | > 99.9% | < 99.5% | < 99% |
| Error Rate | < 0.1% | > 0.5% | > 1% |
| Database Connection Time | < 100ms | > 500ms | > 1s |
| Memory Utilization | < 70% | > 85% | > 95% |
| CPU Utilization | < 60% | > 80% | > 90% |

### Azure Monitor Queries

```kql
// API Response Time Trend
requests
| where timestamp >= ago(24h)
| summarize avg(duration) by bin(timestamp, 1h)
| render timechart

// Error Rate Analysis
requests
| where timestamp >= ago(24h)
| summarize total_requests = count(), failed_requests = countif(success == false) by bin(timestamp, 1h)
| extend error_rate = (failed_requests * 100.0) / total_requests
| render timechart

// Database Performance
dependencies
| where type == "SQL"
| where timestamp >= ago(24h)
| summarize avg(duration) by bin(timestamp, 1h)
| render timechart
```

### Alert Rules

#### High Priority Alerts
- API down for > 5 minutes
- Error rate > 1% for > 10 minutes
- Database connection failures > 5 in 5 minutes
- Memory usage > 95% for > 15 minutes

#### Medium Priority Alerts
- API response time > 1s for > 15 minutes
- CPU usage > 80% for > 30 minutes
- Disk space > 85%

#### Low Priority Alerts
- Warning logs increase > 50% from baseline
- SSL certificate expiring in < 30 days

---

## Incident Response

### Severity Levels

**P0 - Critical (0-1 hour response)**
- Complete service outage
- Data loss or corruption
- Security breach

**P1 - High (1-4 hour response)**
- Major feature unavailable
- Performance severely degraded
- Authentication issues

**P2 - Medium (4-24 hour response)**
- Minor feature issues
- Performance moderately impacted
- Non-critical bugs

**P3 - Low (24-72 hour response)**
- Cosmetic issues
- Documentation problems
- Enhancement requests

### Incident Response Procedures

#### Immediate Response (First 15 minutes)
1. **Acknowledge** the incident in monitoring system
2. **Assess** impact and severity level
3. **Communicate** to stakeholders if P0/P1
4. **Begin** initial investigation

#### Investigation Phase (15-60 minutes)
1. **Check** Azure Status: https://status.azure.com/
2. **Review** recent deployments and changes
3. **Analyze** logs and metrics
4. **Identify** root cause

#### Resolution Phase
1. **Implement** fix or workaround
2. **Test** resolution in staging if possible
3. **Deploy** fix to production
4. **Verify** issue resolution

#### Post-Incident (Within 48 hours)
1. **Document** incident details
2. **Conduct** post-mortem if P0/P1
3. **Update** runbooks and procedures
4. **Implement** preventive measures

### Emergency Contacts

- **On-Call Engineer**: [Primary contact]
- **Azure Support**: Azure Portal → Help + Support
- **GitHub Support**: https://support.github.com/
- **Escalation Manager**: [Management contact]

---

## Maintenance Procedures

### Planned Maintenance Windows

**Preferred Times**:
- **Staging**: Anytime (development environment)
- **Production**: Sunday 2-6 AM EST (lowest traffic)

### Deployment Procedures

#### Standard Deployment
```bash
# 1. Deploy to staging first
gh workflow run "Deploy Infrastructure" --field environment=staging

# 2. Validate staging deployment
./validate-deployment-comprehensive.sh staging --verbose

# 3. Deploy to production (if staging passes)
gh workflow run "Deploy Infrastructure" --field environment=production

# 4. Validate production deployment
./validate-deployment-comprehensive.sh production --verbose
```

#### Emergency Deployment
```bash
# Use local script for faster deployment
./deploy-local-reliable.sh production

# Immediate validation
curl -f https://aiprofilephotomakerapi-production.azurewebsites.net/health
```

### Database Maintenance

#### Weekly Tasks
- Check database performance metrics
- Review query performance insights
- Validate backup completion
- Check index fragmentation

#### Monthly Tasks
- Update database statistics
- Review and optimize slow queries
- Check database growth patterns
- Test backup restoration

```sql
-- Check database performance
SELECT 
    DB_NAME() AS DatabaseName,
    (SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id > 50) AS ActiveSessions,
    (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE is_user_process = 1) AS UserSessions;

-- Check index fragmentation
SELECT 
    OBJECT_NAME(ips.object_id) AS TableName,
    i.name AS IndexName,
    ips.avg_fragmentation_in_percent
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
INNER JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
WHERE ips.avg_fragmentation_in_percent > 10
ORDER BY ips.avg_fragmentation_in_percent DESC;
```

---

## Disaster Recovery

### Backup Strategy

#### Automated Backups
- **Database**: Point-in-time restore (35 days retention)
- **Application Code**: Git repository (permanent)
- **Configuration**: Azure Resource Manager templates
- **Secrets**: Azure Key Vault (soft delete enabled)

#### Manual Backups
- Export critical data before major deployments
- Document configuration changes
- Save deployment artifacts

### Recovery Procedures

#### Database Recovery
```bash
# List available restore points
az sql db list-deleted --resource-group "ai-profile-photo-maker-production" --server "your-sql-server"

# Restore database to point in time
az sql db restore --dest-name "aiprofilephotomakerdb-restored" \
  --resource-group "ai-profile-photo-maker-production" \
  --server "your-sql-server" \
  --source-database "aiprofilephotomakerdb" \
  --time "2024-01-01T12:00:00"
```

#### Complete Environment Recovery
```bash
# 1. Clean up failed resources
az group delete --name "ai-profile-photo-maker-production" --yes

# 2. Redeploy infrastructure
./deploy-local-reliable.sh production

# 3. Restore database (if needed)
# [Database restore commands]

# 4. Validate recovery
./validate-deployment-comprehensive.sh production --verbose
```

### Recovery Time Objectives (RTO)

- **Infrastructure**: 30 minutes
- **Application**: 15 minutes
- **Database**: 60 minutes (with point-in-time restore)
- **Complete System**: 90 minutes

### Recovery Point Objectives (RPO)

- **Database**: 5 minutes (transaction log backups)
- **Application Code**: 0 (Git commits)
- **Configuration**: 0 (Infrastructure as Code)

---

## Performance Optimization

### Monitoring Performance Trends

```bash
# Check API performance over time
az monitor metrics list --resource "{app-service-resource-id}" \
  --metric "AverageResponseTime,Http5xx,CpuPercentage" \
  --interval PT1H \
  --start-time "$(date -d '24 hours ago' -u +%Y-%m-%dT%H:%M:%SZ)"
```

### Optimization Procedures

#### Database Optimization
1. **Query Performance**: Review slow query log weekly
2. **Index Management**: Monitor index usage and fragmentation
3. **Statistics Update**: Ensure statistics are current
4. **Connection Pooling**: Monitor connection pool metrics

#### Application Optimization
1. **Memory Usage**: Monitor for memory leaks
2. **CPU Utilization**: Identify CPU-intensive operations
3. **Caching**: Implement Redis caching for frequently accessed data
4. **CDN**: Ensure static assets are properly cached

#### Infrastructure Scaling

```bash
# Scale App Service Plan
az appservice plan update --name "your-app-service-plan" \
  --resource-group "ai-profile-photo-maker-production" \
  --sku P1V2

# Scale database (if needed)
az sql db update --resource-group "ai-profile-photo-maker-production" \
  --server "your-sql-server" \
  --name "aiprofilephotomakerdb" \
  --service-objective S2
```

---

## Security Operations

### Daily Security Checks

```bash
# Check for security updates
az vm image list --publisher Microsoft --offer SQL2019 --sku Enterprise --all --query "[?version == 'latest']"

# Review access logs
az monitor activity-log list --resource-group "ai-profile-photo-maker-production" \
  --start-time "$(date -d '24 hours ago' -u +%Y-%m-%dT%H:%M:%SZ)" \
  --query "[?authorization.action contains 'write' or authorization.action contains 'delete']"
```

### Security Incident Response

1. **Immediate Actions**:
   - Change all API keys and secrets
   - Review access logs for unauthorized access
   - Check for data exfiltration

2. **Investigation**:
   - Analyze authentication logs
   - Review firewall rules and network access
   - Check for malicious requests

3. **Remediation**:
   - Apply security patches
   - Update access controls
   - Implement additional monitoring

### Compliance Checks

- **SSL Certificates**: Verify expiration dates monthly
- **Access Reviews**: Quarterly review of user access
- **Vulnerability Scanning**: Monthly security scans
- **Backup Testing**: Quarterly backup restoration tests

---

## Troubleshooting Guide

### Common Issues & Solutions

#### API Not Responding
**Symptoms**: 502/503 errors, timeouts
**Causes**: Application crash, high CPU/memory, database connection issues

**Troubleshooting Steps**:
1. Check application logs in Azure Portal
2. Verify database connectivity
3. Check resource utilization metrics
4. Restart application if necessary

```bash
# Restart Web App
az webapp restart --name "your-webapp" --resource-group "ai-profile-photo-maker-production"

# Check application logs
az webapp log tail --name "your-webapp" --resource-group "ai-profile-photo-maker-production"
```

#### Database Connection Issues
**Symptoms**: Connection timeouts, authentication failures
**Causes**: Connection string issues, firewall rules, service limits

**Troubleshooting Steps**:
1. Verify connection string configuration
2. Check SQL Server firewall rules
3. Test database connectivity from App Service
4. Review SQL Server metrics

```bash
# Test database connection
az sql db show-connection-string --server "your-sql-server" \
  --name "aiprofilephotomakerdb" --client ado.net
```

#### High Memory Usage
**Symptoms**: OutOfMemoryException, slow performance
**Causes**: Memory leaks, large object processing, insufficient resources

**Troubleshooting Steps**:
1. Check memory usage metrics
2. Review application logs for memory errors
3. Scale up App Service Plan if needed
4. Analyze memory dumps if available

#### SSL Certificate Issues
**Symptoms**: Certificate warnings, HTTPS errors
**Causes**: Expired certificates, misconfigured bindings

**Troubleshooting Steps**:
1. Check certificate expiration dates
2. Verify SSL bindings in App Service
3. Test SSL configuration with online tools
4. Renew certificates if necessary

### Diagnostic Commands

```bash
# Quick system health check
./validate-deployment-comprehensive.sh production

# Detailed resource analysis
az resource list --resource-group "ai-profile-photo-maker-production" \
  --query "[].{Name:name,Type:type,Status:properties.provisioningState,Health:properties.statusDetails}" \
  -o table

# Application performance metrics
az monitor metrics list --resource "{app-service-resource-id}" \
  --metric "CpuPercentage,MemoryPercentage,AverageResponseTime" \
  --interval PT5M --start-time "$(date -d '1 hour ago' -u +%Y-%m-%dT%H:%M:%SZ)"
```

---

## Best Practices

### Deployment Best Practices
1. **Always deploy to staging first**
2. **Validate deployments thoroughly**
3. **Use infrastructure as code**
4. **Maintain deployment documentation**
5. **Test rollback procedures regularly**

### Monitoring Best Practices
1. **Set up comprehensive alerting**
2. **Monitor business metrics, not just technical metrics**
3. **Use dashboards for quick status overview**
4. **Regular review and tune alert thresholds**
5. **Document escalation procedures**

### Security Best Practices
1. **Regular security updates**
2. **Principle of least privilege**
3. **Regular access reviews**
4. **Encrypt data in transit and at rest**
5. **Monitor for suspicious activities**

---

## Contact Information

### Internal Contacts
- **Primary On-Call**: [Name] - [Phone] - [Email]
- **Secondary On-Call**: [Name] - [Phone] - [Email]
- **Manager**: [Name] - [Email]

### External Contacts
- **Azure Support**: Azure Portal → Help + Support
- **GitHub Support**: https://support.github.com/
- **Domain Registrar**: [Contact Info]
- **SSL Certificate Provider**: [Contact Info]

---

## Revision History

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-08-01 | 1.0 | Initial version | Deployment Engineer |

---

*This runbook should be reviewed and updated quarterly or after any major system changes.*