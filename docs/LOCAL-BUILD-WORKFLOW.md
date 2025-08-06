# Local Build Workflow Documentation

## 🎯 Overview

This simplified deployment approach moves Docker image building from GitHub Actions to your local development environment. This eliminates CI/CD complexity, speeds up development iterations, and provides better debugging capabilities.

## 🏗️ Architecture

### **Before (Complex CI/CD)**
```
┌─────────────────┐    ┌──────────────────┐    ┌────────────────────┐
│   GitHub Push   │ -> │ Build Images in  │ -> │ Update Container   │
│                 │    │ GitHub Actions   │    │ Apps with Images   │
└─────────────────┘    └──────────────────┘    └────────────────────┘
```

### **After (Local Build)**  
```
┌─────────────────┐    ┌──────────────────┐    ┌────────────────────┐
│ Build Images    │ -> │   Push to ACR    │ -> │ Deploy Infra with  │
│ Locally         │    │   Locally        │    │ Real Images        │
└─────────────────┘    └──────────────────┘    └────────────────────┘
```

## 🚀 Quick Start

### **1. Prerequisites**
- Docker Desktop installed and running
- Azure CLI installed (`az --version`)
- Logged in to Azure (`az login`)
- Project cloned locally

### **2. Build & Deploy Workflow**
```bash
# Build images locally
./scripts/build-local.sh

# Push to Azure Container Registry  
./scripts/push-to-acr.sh

# Deploy infrastructure (triggers automatically on git push)
git add . && git commit -m "Deploy latest changes" && git push origin main
```

## 📋 Detailed Steps

### **Step 1: Build Images Locally**

```bash
# Build with default 'latest' tag
./scripts/build-local.sh

# Build with custom tag
./scripts/build-local.sh v1.2.3

# Build with development tag for testing
./scripts/build-local.sh dev
```

**What it does:**
- ✅ Validates Docker is running
- ✅ Checks all required Dockerfile and source files exist
- ✅ Builds both `aiprofilemaker-api:latest` and `aiprofilemaker-web:latest`
- ✅ Provides detailed build logs and error reporting
- ✅ Lists built images for verification

**Expected output:**
```
🏗️ AI Profile Photo Maker - Local Build Script
=================================================

✅ Docker is running
✅ All required files present

🔨 Building backend image...
✅ Backend image built successfully

🔨 Building frontend image...
✅ Frontend image built successfully

🎉 Build completed successfully!
```

### **Step 2: Push to Azure Container Registry**

```bash
# Push with default settings (auto-discovers ACR)
./scripts/push-to-acr.sh

# Push with custom tag
./scripts/push-to-acr.sh v1.2.3

# Push to specific resource group
./scripts/push-to-acr.sh latest aiprofilemaker-dev
```

**What it does:**
- ✅ Validates Azure CLI login and permissions
- ✅ Auto-discovers Container Registry in resource group
- ✅ Validates local images exist before pushing
- ✅ Logs in to ACR and pushes both images
- ✅ Verifies images exist in ACR after push
- ✅ Provides image URLs for Container Apps deployment

**Expected output:**
```
📤 AI Profile Photo Maker - ACR Push Script
=============================================

✅ Azure CLI found
✅ Logged in to Azure
✅ Found Container Registry: aipmcrv1abc123
✅ Local images validated

📤 Pushing backend image...
✅ Backend image pushed successfully

📤 Pushing frontend image...
✅ Frontend image pushed successfully

🎉 Push completed successfully!
```

### **Step 3: Deploy Infrastructure**

The infrastructure deployment is now greatly simplified and triggered automatically:

```bash
# Make any code changes
git add .
git commit -m "Latest updates"
git push origin main  # This triggers the simplified deployment
```

**What the CI/CD does now:**
- ✅ Validates Bicep templates
- ✅ Deploys all Azure resources with real images
- ✅ No Docker builds or image updates needed
- ✅ Container Apps start with correct images immediately
- ✅ Performs health checks on deployed applications

## 🔧 Advanced Usage

### **Custom Tags and Versioning**

```bash
# Build and push with semantic version
./scripts/build-local.sh v1.2.3
./scripts/push-to-acr.sh v1.2.3

# Build development version for testing
./scripts/build-local.sh dev
./scripts/push-to-acr.sh dev
```

### **Multi-Environment Support**

```bash
# Push to development environment
./scripts/push-to-acr.sh latest aiprofilemaker-dev

# Push to staging environment  
./scripts/push-to-acr.sh latest aiprofilemaker-staging

# Push to production environment
./scripts/push-to-acr.sh latest aiprofilemaker-v1
```

### **Local Testing**

```bash
# Test backend locally after building
docker run -p 8080:8080 aiprofilemaker-api:latest

# Test frontend locally after building
docker run -p 80:80 aiprofilemaker-web:latest

# View built images
docker images | grep aiprofilemaker
```

### **Debugging & Troubleshooting**

```bash
# Check if images were built successfully
docker images | grep aiprofilemaker

# Inspect image details
docker image inspect aiprofilemaker-api:latest

# Check ACR contents
az acr repository list --name <acr-name>

# View image tags in ACR
az acr repository show-tags --name <acr-name> --repository aiprofilemaker-api
```

## 🆚 Comparison: Old vs New Workflow

| Aspect | **Old CI/CD Build** | **New Local Build** |
|--------|---------------------|---------------------|
| **Build Location** | GitHub Actions runners | Local development machine |
| **Build Time** | 3-5 minutes | 30-60 seconds |
| **Debugging** | Limited CI logs | Full local control |
| **Iteration Speed** | Wait for CI pipeline | Immediate feedback |
| **Resource Usage** | GitHub Actions compute | Local Docker |
| **Complexity** | High (3-step process) | Low (2-step process) |
| **Error Recovery** | Re-run entire pipeline | Fix and rebuild locally |
| **Testing** | Limited in CI | Full local testing |

## 🎯 Benefits

### **For Developers**
- ⚡ **Faster Iteration**: Build locally in 30-60 seconds vs 3-5 minutes in CI
- 🐛 **Better Debugging**: Full control over build process and dependencies  
- 🧪 **Local Testing**: Test images locally before deployment
- 💾 **Reduced Resources**: No GitHub Actions compute usage for builds
- 🔧 **Flexibility**: Easy to modify build process and dependencies

### **For Deployment**
- 🚀 **Simplified CI/CD**: Just infrastructure deployment, no complex build steps
- 🛡️ **More Reliable**: Eliminates CI/CD Docker build failures
- 📦 **No Placeholder Images**: Container Apps start with real images immediately
- ⚙️ **No Update Steps**: No Container App image updates needed
- 🔄 **No Circular Dependencies**: Eliminates complex deployment ordering issues

### **For Troubleshooting**
- 📊 **Clear Error Messages**: Local build errors are immediately visible
- 🔍 **Step-by-Step Control**: Can debug each step independently
- 🧹 **Clean State**: Can easily rebuild and test locally
- 📝 **Better Logging**: Full visibility into build process

## 🚨 Important Notes

### **Prerequisites**
- Ensure Docker Desktop is running before building
- Login to Azure CLI before pushing (`az login`)
- ACR must exist in resource group (created by infrastructure deployment)

### **Image Dependencies**
- Infrastructure deployment now expects images to exist in ACR
- If images are missing, deployment will fail with clear error messages
- Always build and push before deploying infrastructure changes

### **Version Management**
- Use consistent tags between build and push scripts
- Default tag is `latest` for simplicity
- Consider semantic versioning for production releases

### **Security**
- ACR admin credentials are used for simplicity
- Consider using managed identity for production environments
- Local build requires Docker access to build context

## 🔄 Migration from Old Workflow

If you're migrating from the complex CI/CD workflow:

1. **Build images locally first:**
   ```bash
   ./scripts/build-local.sh
   ./scripts/push-to-acr.sh
   ```

2. **Update workflow file:** Replace `powershell-deploy.yml` with `simple-deploy.yml`

3. **Remove old infrastructure:** The new workflow is incompatible with placeholder image approach

4. **Test deployment:** Push to trigger new simplified workflow

## 📞 Support

If you encounter issues:

1. **Build failures**: Check Docker Desktop is running and all source files exist
2. **Push failures**: Verify Azure CLI login and ACR permissions  
3. **Deployment failures**: Ensure images exist in ACR before deployment
4. **Health check failures**: Check Container Apps logs for startup issues

**Common Solutions:**
```bash
# Rebuild images if build corrupted
docker system prune -f
./scripts/build-local.sh

# Re-login to Azure if auth expired
az login
./scripts/push-to-acr.sh

# Check ACR contents if push seems to fail
az acr repository list --name <acr-name>
```