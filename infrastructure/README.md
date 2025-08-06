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

### **simple-deploy.json** ✅ ACTIVE  
Compiled ARM template from Bicep source.

## 🔧 Development Tools

Additional development tools are available in `/scripts/` directory.

---

**Note**: These scripts are maintained for incident response scenarios. Primary deployment uses the automated GitHub Actions workflow (`powershell-deploy.yml`).