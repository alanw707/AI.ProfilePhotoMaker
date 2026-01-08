# Infrastructure Scripts

This directory contains essential infrastructure management and incident response tools.

## 🚨 Incident Response Scripts

These scripts are kept for manual intervention during production emergencies when automated deployment fails:

### **deploy-fixed.ps1**
- **Purpose**: Emergency deployment when GitHub Actions is unavailable
- **Use Case**: Service outages requiring immediate redeploy
- **Capabilities**: Manual secret handling, local PowerShell execution

### **update-acr-credentials.ps1** 
- **Purpose**: Fix ACR authentication issues
- **Use Case**: Credential rotation, authentication failures, container pull errors
- **Capabilities**: PowerShell + Azure CLI fallback, container app restart

### **validate-docker-deployment.ps1**
- **Purpose**: Comprehensive deployment environment diagnostics
- **Use Case**: Build failures, environment issues, ACR connectivity problems  
- **Capabilities**: Complete environment validation, Docker build testing

## 📁 Production Infrastructure

### **simple-deploy.bicep** ✅ ACTIVE
Primary infrastructure template used by production deployment pipeline.

**⚠️ Custom Domain Requirements:**
- DNS CNAME records must be configured **before** deployment:
  - `aiprofilephotomaker.com` → {containerAppsEnvironment}.region.azurecontainerapps.io
  - `api.aiprofilephotomaker.com` → {containerAppsEnvironment}.region.azurecontainerapps.io
- Template includes managed certificates and persistent custom domain configuration
- Domain bindings persist across deployment revisions (no manual reconfiguration needed)

### **simple-deploy.json** 🗑️ REMOVED  
Auto-generated file - can be recreated with `az bicep build --file simple-deploy.bicep`

## 🔧 Deployment Validation

### **validate-deployment.js** ✅ ACTIVE (in `/scripts/`)
Playwright-based validation for custom domain deployment verification.

**Usage:**
```bash
# Basic validation
./scripts/validate-deployment.sh

# With retry configuration
./scripts/validate-deployment.sh --wait 60 --retries 5

# Run with visible browser (debugging)
./scripts/validate-deployment.sh --headed
```

**Validates:**
- Frontend domain accessibility (https://aiprofilephotomaker.com)
- Backend health check (https://api.aiprofilephotomaker.com/api/health) 
- CORS functionality between domains
- SSL certificate validity

**Output:** Generates `validation-report.json` with detailed results.

## 🔧 Development Tools

Additional development tools are available in `/scripts/` directory.

---

**Note**: These scripts are maintained for incident response scenarios. Primary deployment uses the automated GitHub Actions workflow (`powershell-deploy.yml`).