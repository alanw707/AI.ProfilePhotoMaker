# Azure Resource Cleanup - Execution Guide

## 🚀 Quick Start

**Ready to execute Azure cleanup for V1 deployment? Run the master script:**

```bash
./execute-azure-cleanup.sh
```

This orchestrates the complete 5-phase cleanup process with safety checks and user confirmations.

---

## 📋 Cleanup Strategy Overview

### Current State Analysis
- **Legacy Environment**: `rg-aiprofilemaker-staging` (complete removal required)
- **Target Environment**: `aiprofilemaker-v1` (selective cleanup needed)
- **Container Registry**: `aiprofilemakercrv16j74jubocuukg` (assess for preservation)

### Safety Protocol
✅ **Conservative Approach**: Preserve valuable resources by default  
✅ **Interactive Confirmations**: User approval required for destructive actions  
✅ **Comprehensive Backups**: Critical data protected before cleanup  
✅ **Validation Gates**: Environment readiness verified before deployment  

---

## 🔧 Execution Methods

### Option 1: Master Script (Recommended)
**Single command orchestration with full safety protocol:**
```bash
./execute-azure-cleanup.sh
```

**Features:**
- Interactive mode with phase-by-phase confirmations
- Comprehensive logging and reporting
- Automatic error handling and recovery guidance
- Pre-execution safety checks

### Option 2: Individual Phase Execution
**Manual control over each cleanup phase:**

```bash
# Phase 1: Remove legacy staging environment
./scripts/01-staging-cleanup.sh

# Phase 2: Assess V1 environment resources  
./scripts/02-v1-assessment.sh

# Phase 3: Backup valuable V1 resources
./scripts/03-backup-valuable-resources.sh

# Phase 4: Selective V1 cleanup
./scripts/04-selective-v1-cleanup.sh

# Phase 5: Pre-deployment validation
./scripts/05-pre-deployment-validation.sh
```

---

## 📊 Cleanup Phases Detail

### Phase 1: Legacy Staging Cleanup
- **Target**: `rg-aiprofilemaker-staging`
- **Action**: Complete resource group deletion
- **Risk**: LOW (isolated staging environment)
- **Duration**: 5-15 minutes (background deletion)

### Phase 2: V1 Environment Assessment  
- **Target**: `aiprofilemaker-v1`
- **Action**: Resource inventory and conflict analysis
- **Output**: Detailed assessment reports and cleanup recommendations
- **Duration**: 2-5 minutes

### Phase 3: Valuable Resources Backup
- **Target**: High-value V1 resources (Container Registry, Key Vault, Storage)
- **Action**: Create backup scripts and export critical data
- **Output**: Recovery scripts and data exports
- **Duration**: 5-15 minutes (depending on data volume)

### Phase 4: Selective V1 Cleanup
- **Target**: Deployment-conflicting resources in V1 environment
- **Action**: Interactive removal with preservation options
- **Strategy**: Remove Container Apps, assess SQL/Storage/KeyVault/Registry
- **Duration**: 10-20 minutes (with user interaction)

### Phase 5: Pre-deployment Validation
- **Target**: Complete environment readiness
- **Action**: Validate Azure CLI, permissions, secrets, and deployment files
- **Output**: GO/NO-GO decision with detailed validation report
- **Duration**: 2-5 minutes

---

## 🛡️ Safety Features

### Backup Protection
- **Container Images**: Export and recovery scripts created
- **SQL Databases**: BACPAC export scripts generated  
- **Key Vault Secrets**: Backup and restore scripts provided
- **Storage Blobs**: Download and restore procedures documented

### Interactive Confirmations
- **High-Risk Actions**: Require explicit `DELETE` confirmation
- **Selective Cleanup**: User choice for each resource type
- **Phase Gates**: Option to skip phases during execution

### Comprehensive Logging
- **Master Log**: Complete execution trace
- **Phase Logs**: Detailed logs for each cleanup phase
- **Error Capture**: Full error context and recovery guidance
- **Audit Trail**: Complete record of all actions taken

---

## 📈 Expected Outcomes

### Successful Completion
- **Legacy Staging**: Complete removal of `rg-aiprofilemaker-staging`
- **V1 Environment**: Clean foundation with preserved valuable resources
- **Deployment Ready**: All conflicts resolved, validation passed
- **Documentation**: Complete audit trail and recovery procedures

### Resource Preservation Strategy
- **PRESERVE**: Container Registry (reuse images), Key Vault (keep secrets), Storage Account (retain data)
- **REMOVE**: Container Apps (redeploy fresh), Application Insights (restart monitoring)
- **ASSESS**: SQL Database (backup first, then decide), other resources (case-by-case)

---

## 🚀 Post-Cleanup Deployment

### GitHub Actions Deployment
Once cleanup completes successfully:

```bash
# Via GitHub CLI (if available)
gh workflow run "🚀 V1 Deploy" --ref main

# Or via GitHub web interface:
# https://github.com/YOUR_USERNAME/YOUR_REPO/actions/workflows/simple-deploy.yml
```

### Required GitHub Secrets
Ensure these secrets are configured in your GitHub repository:
- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID` 
- `AZURE_SUBSCRIPTION_ID`
- `SQL_ADMIN_PASSWORD`
- `JWT_SECRET`
- `REPLICATE_API_TOKEN`

### Deployment Monitoring
- **GitHub Actions**: Monitor workflow progress in Actions tab
- **Azure Portal**: Watch resource creation in `aiprofilemaker-v1` resource group
- **Application Health**: Test endpoints post-deployment

---

## 🔧 Prerequisites

### Required Tools
- **Azure CLI**: Latest version, authenticated (`az login`)
- **Docker**: For container image backup (optional but recommended)
- **Git**: For GitHub integration validation
- **Bash**: Linux/WSL/macOS shell environment

### Azure Permissions
- **Resource Group**: Create, delete, and manage permissions
- **Resources**: Full control over Container Registry, Container Apps, SQL, Storage, Key Vault
- **Subscription**: Resource provider registration permissions

### Pre-execution Checklist
- [ ] Azure CLI installed and authenticated
- [ ] Sufficient Azure permissions confirmed
- [ ] GitHub repository secrets configured
- [ ] Local backup storage available
- [ ] Network connectivity to Azure services verified

---

## 📞 Support & Troubleshooting

### Common Issues
- **Authentication**: Ensure `az login` completed successfully
- **Permissions**: Verify Contributor/Owner role on subscription
- **Connectivity**: Check network access to Azure services
- **Resource Locks**: Remove any resource locks before cleanup

### Getting Help
- **Execution Logs**: Check generated log files for detailed error information
- **Azure Portal**: Monitor resource states during cleanup
- **Phase-by-Phase**: Run individual phase scripts to isolate issues
- **Validation**: Use pre-deployment validation to identify configuration problems

### Recovery Procedures
- **Backup Scripts**: Use generated backup scripts to recover critical data
- **Resource Recreation**: Deploy fresh resources if cleanup was too aggressive
- **State Rollback**: Use Azure Portal to manually restore critical resources

---

## ✅ Success Criteria

### Cleanup Success Indicators
- [ ] Legacy staging environment completely removed
- [ ] V1 environment conflicts resolved
- [ ] Valuable resources preserved with backups
- [ ] Pre-deployment validation passes
- [ ] Complete audit trail generated

### Deployment Readiness Checklist
- [ ] Resource group clean or optimally configured
- [ ] No naming conflicts with deployment template
- [ ] Container Registry available (preserved or clean slate)
- [ ] GitHub secrets properly configured
- [ ] Azure permissions verified
- [ ] Deployment files validated

---

*Generated by Claude Code Deployment Agent - Azure Resource Cleanup Strategy*

**Next Step**: Execute `./execute-azure-cleanup.sh` to begin the cleanup process.