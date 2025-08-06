# 🧪 Local Build Workflow Testing Strategy

Complete testing methodology for the new local build deployment workflow.

## 🎯 Testing Philosophy

**Goal**: Validate the local build workflow (build locally → push to ACR → deploy infrastructure) is production-ready with minimal risk to the main deployment.

## 📋 Multi-Phase Testing Approach

### Phase 1: Local Validation (Zero Risk) ⭐ **START HERE**

Test the complete workflow locally without affecting any cloud resources.

```bash
# Run complete local workflow test
./scripts/test-local-workflow.sh

# What it tests:
# ✅ Docker environment
# ✅ Azure CLI authentication  
# ✅ Local image builds
# ✅ ACR push functionality
# ✅ Bicep template compilation
# ✅ Template validation
# ✅ Cleanup procedures
```

**Expected Duration**: 5-10 minutes  
**Risk Level**: Zero - only local testing  
**Prerequisites**: Docker running, Azure CLI authenticated

---

### Phase 2: Branch-Based Testing (Low Risk)

Test the GitHub Actions workflow on a feature branch with isolated resources.

```bash
# Create test branch and configure isolated testing
./scripts/test-branch-workflow.sh

# What it does:
# 🌿 Creates temporary test branch
# 🔨 Builds and pushes test images
# ⚙️ Sets up isolated test workflow
# 🚀 Triggers GitHub Actions test
# 🧹 Auto-cleanup after testing
```

**Expected Duration**: 10-15 minutes  
**Risk Level**: Low - separate resource group  
**Prerequisites**: Phase 1 completed successfully

---

### Phase 3: CLI-Based Workflow Control

Manual workflow triggering and monitoring capabilities.

```bash
# Manually trigger workflows
./scripts/trigger-workflow.sh simple-deploy.yml main

# Available commands:
gh workflow run simple-deploy.yml --ref main    # Trigger deployment
gh run watch <RUN_ID>                          # Watch live logs
gh run view <RUN_ID> --web                     # Open in browser
```

**Use Cases**:
- Test deployments without git push
- Re-run failed deployments
- Deploy specific branches/commits

---

### Phase 4: Performance Comparison

Compare local build vs CI build performance and characteristics.

```bash
# Analyze workflow performance
./scripts/compare-workflows.sh

# Generates report on:
# ⏱️ Build times (local vs CI)
# 📊 Success rates
# 💰 Resource usage (GitHub Actions minutes)
# 🔍 Failure patterns
```

---

### Phase 5: Production Testing & Rollback

Safe production testing with immediate rollback capability.

```bash
# Before production test - ensure rollback ready
./scripts/rollback-deployment.sh aiprofilemaker-v1 auto

# Production test workflow:
# 1. Build and push latest images locally
# 2. Push to main branch (triggers deployment)
# 3. Monitor deployment closely  
# 4. Rollback immediately if issues detected
```

## 🚀 Recommended Testing Sequence

### Option A: Full Validation (Recommended for First Time)

```bash
# Step 1: Local validation
./scripts/test-local-workflow.sh

# Step 2: Branch testing  
./scripts/test-branch-workflow.sh
# (Wait for GitHub Actions to complete)

# Step 3: Performance baseline
./scripts/compare-workflows.sh

# Step 4: Production test
./scripts/build-local.sh
./scripts/push-to-acr.sh
git add . && git commit -m "test: validate local build workflow"
git push origin main

# Step 5: Monitor and rollback if needed
./scripts/rollback-deployment.sh aiprofilemaker-v1 workflow
```

### Option B: Quick Validation (For Experienced Users)

```bash
# Local test only, then direct production
./scripts/test-local-workflow.sh

# If passes, go direct to production
./scripts/build-local.sh && ./scripts/push-to-acr.sh
git push origin main
```

## 📊 Validation Checklist

### ✅ Pre-Production Validation
- [ ] Local build completes successfully
- [ ] Images push to ACR without errors
- [ ] Bicep template compiles and validates
- [ ] Branch-based workflow passes (if using Option A)
- [ ] Rollback script tested and ready

### ✅ Production Deployment Validation
- [ ] GitHub Actions workflow completes successfully
- [ ] Container Apps update with new images
- [ ] Health checks pass for both frontend and backend
- [ ] Application functionality verified
- [ ] Performance meets expectations

### ✅ Post-Deployment Verification
- [ ] Frontend URL accessible and responsive
- [ ] Backend API endpoints functioning
- [ ] Database connectivity working
- [ ] File upload/storage functioning
- [ ] AI photo generation working

## 🚨 Emergency Procedures

### Immediate Rollback Triggers
- Health checks fail after deployment
- Application errors increase significantly
- Performance degrades below acceptable levels
- Critical functionality broken

### Rollback Commands
```bash
# Quick rollback via GitHub Actions
./scripts/rollback-deployment.sh

# Direct Container Apps rollback
./scripts/rollback-deployment.sh aiprofilemaker-v1 direct

# Emergency: Disable broken deployment
az containerapp revision deactivate --resource-group aiprofilemaker-v1 --name <app-name> --revision <revision-name>
```

## 🔧 Troubleshooting Common Issues

### Local Build Failures
```bash
# Check Docker status
docker info

# Verify build context
ls -la Dockerfile.* AI.ProfilePhotoMaker.* nginx.conf docker-entrypoint.sh

# Clean Docker cache
docker system prune -a
```

### ACR Push Failures
```bash
# Re-authenticate with ACR
az acr login --name <acr-name>

# Check ACR permissions
az acr show --name <acr-name> --query "{adminUserEnabled: adminUserEnabled, loginServer: loginServer}"
```

### Template Validation Failures
```bash
# Compile Bicep locally
az bicep build --file infrastructure/simple-deploy.bicep

# Test with minimal parameters
az deployment group validate --resource-group test --template-file infrastructure/simple-deploy.bicep --parameters sqlAdminPassword=Test123! jwtSecret=test replicateApiToken=test
```

## 📈 Success Metrics

### Performance Targets
- **Local build time**: < 5 minutes
- **ACR push time**: < 3 minutes  
- **Deployment time**: < 10 minutes
- **Health check response**: < 5 seconds
- **Total workflow time**: < 15 minutes

### Quality Gates
- **Template validation**: Must pass
- **Image security scan**: No critical vulnerabilities
- **Health checks**: All endpoints responding
- **Rollback capability**: < 2 minutes to working state

## 🎉 Success Criteria

The local build workflow is considered **production-ready** when:

1. ✅ All validation phases pass
2. ✅ Performance meets or exceeds current CI build workflow
3. ✅ Rollback procedures verified and functional
4. ✅ Zero production incidents during testing
5. ✅ Developer workflow improved (faster feedback, better debugging)

---

## 🚀 Quick Start Commands

```bash
# Complete workflow test (recommended first run)
./scripts/test-local-workflow.sh && ./scripts/test-branch-workflow.sh

# Production deployment (after validation)
./scripts/build-local.sh && ./scripts/push-to-acr.sh && git push origin main

# Emergency rollback
./scripts/rollback-deployment.sh
```