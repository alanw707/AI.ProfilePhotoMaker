# Testing Scripts for Local Build Workflow

This directory contains comprehensive testing scripts for validating the new local build workflow.

## 🧪 Testing Strategy

### Multi-Phase Approach

**Don't choose between creating PR vs CLI testing - do both in sequence for comprehensive validation:**

## 📋 Available Scripts

### 1. `./test-local-workflow.sh` - **START HERE**
**Local environment validation (5 minutes, zero risk)**

```bash
./scripts/test-local-workflow.sh
```

**What it tests:**
- ✅ Docker and Azure CLI prerequisites
- ✅ Project structure and file validation
- ✅ Local image building process
- ✅ ACR discovery and authentication
- ✅ Infrastructure template compilation
- ✅ GitHub workflow validation
- ✅ Container runtime testing

**Risk Level:** 🟢 **ZERO** - No production impact, only local validation

### 2. `./test-branch-workflow.sh` - Branch Testing
**Isolated GitHub Actions workflow testing (10 minutes)**

```bash
./scripts/test-branch-workflow.sh
```

**What it does:**
- Creates temporary test branch with unique resource group
- Builds and pushes test images to separate ACR
- Tests GitHub Actions workflow in complete isolation
- Monitors workflow execution with real-time feedback
- Provides cleanup options after testing

**Risk Level:** 🟡 **LOW** - Uses separate Azure resources, no production impact

### 3. `./trigger-workflow.sh` - Manual Workflow Control
**CLI-based workflow triggering and monitoring**

```bash
# Trigger and monitor workflow
./scripts/trigger-workflow.sh simple-deploy.yml main trigger

# Just monitor existing workflow
./scripts/trigger-workflow.sh simple-deploy.yml main monitor

# Check workflow status
./scripts/trigger-workflow.sh simple-deploy.yml main status

# List recent runs
./scripts/trigger-workflow.sh simple-deploy.yml main list

# Cancel running workflows
./scripts/trigger-workflow.sh simple-deploy.yml main cancel
```

**Risk Level:** 🟠 **MEDIUM** - Can trigger production workflows

## 🚀 Recommended Testing Flow

### Phase 1: Local Validation (REQUIRED)
```bash
# Test everything locally first
./scripts/test-local-workflow.sh
```
**If this fails, fix issues before proceeding to other phases**

### Phase 2: Branch Testing (RECOMMENDED)
```bash
# Test workflow in isolation
./scripts/test-branch-workflow.sh
```
**Tests complete GitHub Actions workflow with separate resources**

### Phase 3: Production Testing (OPTIONAL)
```bash
# Only if Phases 1-2 pass
./scripts/build-local.sh && ./scripts/push-to-acr.sh
git add . && git commit -m "deploy: test local build workflow"
git push origin main
```

### Alternative: CLI-Based Testing
```bash
# Manual workflow control (if you have GitHub CLI setup)
./scripts/trigger-workflow.sh simple-deploy.yml main trigger
```

## 🛡️ Safety Features

### Risk Mitigation
- **Phase 1**: Zero risk - only local validation
- **Phase 2**: Separate test resources - no production impact
- **Phase 3**: Rollback capabilities available
- **All phases**: Comprehensive error handling and recovery options

### Cleanup Options
Each script provides multiple cleanup strategies:
1. **Full cleanup** - Delete all test resources and branches
2. **Partial cleanup** - Keep resources for investigation
3. **Manual cleanup** - Instructions for later cleanup
4. **Investigation mode** - Keep everything for debugging

## 📊 What Gets Tested

### Local Build System
- Docker image building and validation
- Local script execution and error handling
- Container runtime testing
- File structure and dependency validation

### Azure Integration
- ACR discovery and authentication
- Resource group management
- Infrastructure template validation
- Azure CLI integration

### GitHub Actions Workflow
- Workflow file validation and syntax checking
- Branch-based testing with isolation
- Real-time monitoring and feedback
- Manual triggering and control

### End-to-End Flow
- Complete build → push → deploy cycle
- Health check validation
- Performance comparison
- Error handling and recovery

## 🎯 Success Criteria

### Local Workflow Test
- ✅ All prerequisite checks pass
- ✅ Images build successfully
- ✅ ACR authentication works
- ✅ Templates compile without errors
- ✅ Containers start correctly

### Branch Workflow Test
- ✅ Test branch deployment succeeds
- ✅ GitHub Actions workflow completes
- ✅ Health checks pass
- ✅ Test resources created correctly

### Production Deployment
- ✅ Images pushed to production ACR
- ✅ Infrastructure deployment succeeds
- ✅ Container Apps start with real images
- ✅ Health endpoints respond correctly

## 🔧 Troubleshooting

### Common Issues

**Local Build Failures**
```bash
# Check Docker status
docker info

# Rebuild clean
docker system prune -f
./scripts/build-local.sh
```

**ACR Authentication Issues**
```bash
# Re-login to Azure
az login
az account set --subscription <subscription-id>

# Test ACR access
az acr list --query "[].{Name:name, ResourceGroup:resourceGroup}"
```

**GitHub Workflow Issues**
```bash
# Check workflow status
./scripts/trigger-workflow.sh simple-deploy.yml main status

# View recent runs
gh run list --limit 5

# Get detailed logs
gh run view <RUN_ID> --log
```

### Getting Help

1. **Check script output** - All scripts provide detailed error messages
2. **Review logs** - GitHub Actions logs available via CLI or web interface
3. **Test incrementally** - Start with Phase 1, then Phase 2, then Phase 3
4. **Use investigation mode** - Keep test resources for debugging

## 📈 Performance Comparison

The testing scripts also help validate the performance improvements:

| **Metric** | **Old CI/CD** | **New Local Build** | **Improvement** |
|------------|---------------|---------------------|-----------------|
| Build Time | 3-5 minutes | 30-60 seconds | **10x faster** |
| Feedback | End of pipeline | Immediate | **Real-time** |
| Debugging | CI logs only | Full local control | **Complete** |
| Iteration | Full pipeline | Build locally | **Instant** |

## 🎉 Ready to Test!

Start with the local workflow test to validate everything works:

```bash
./scripts/test-local-workflow.sh
```

This provides a comprehensive validation of the new workflow with zero risk to production systems.