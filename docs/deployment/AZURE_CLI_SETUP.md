# Azure CLI Installation Guide

## Quick Installation Options

### Option 1: Using curl (Ubuntu/Debian) - **RECOMMENDED**
```bash
# Single command installation
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
```

### Option 2: Manual Package Installation
```bash
# Update package index
sudo apt-get update

# Install required packages
sudo apt-get install ca-certificates curl apt-transport-https lsb-release gnupg

# Download and install Microsoft signing key
curl -sL https://packages.microsoft.com/keys/microsoft.asc | \
    gpg --dearmor | \
    sudo tee /etc/apt/trusted.gpg.d/microsoft.gpg > /dev/null

# Add Azure CLI repository
AZ_REPO=$(lsb_release -cs)
echo "deb [arch=amd64] https://packages.microsoft.com/repos/azure-cli/ $AZ_REPO main" | \
    sudo tee /etc/apt/sources.list.d/azure-cli.list

# Update package index and install
sudo apt-get update
sudo apt-get install azure-cli
```

### Option 3: Using Snap
```bash
sudo snap install azure-cli --classic
```

### Option 4: Using pip (if you have Python)
```bash
pip install azure-cli --user
# Add ~/.local/bin to PATH if needed
export PATH=$PATH:~/.local/bin
```

## Verify Installation
```bash
# Check version
az --version

# Check if it works
az help
```

## Login to Azure
```bash
# Interactive login
az login

# Or login with specific tenant
az login --tenant YOUR_TENANT_ID
```

## Grant Service Principal Permissions

Once Azure CLI is installed and you're logged in, run these commands:

```bash
# Get your subscription ID
SUBSCRIPTION_ID=$(az account show --query id --output tsv)
echo "Subscription ID: $SUBSCRIPTION_ID"

# Grant Contributor role to service principal
az role assignment create \
  --assignee b19f1dae-b21a-4a63-b56d-085bad6b23b2 \
  --role "Contributor" \
  --scope "/subscriptions/$SUBSCRIPTION_ID"

# Verify the assignment
az role assignment list \
  --assignee b19f1dae-b21a-4a63-b56d-085bad6b23b2 \
  --output table
```

## Test Azure Deployment

After granting permissions, test the deployment:

```bash
# Trigger staging deployment
gh workflow run "Deploy Infrastructure to Azure" --field environment=staging

# Monitor the deployment
gh run list --workflow="Deploy Infrastructure to Azure" --limit 1

# View logs if needed
gh run view [RUN_ID] --log
```

## Alternative: Manual Resource Group Creation

If you prefer not to grant full Contributor permissions, create resource groups manually:

```bash
# Create staging resource group
az group create --name ai-profile-photo-maker-staging --location "East US"

# Create production resource group  
az group create --name ai-profile-photo-maker-prod --location "East US"

# Verify creation
az group list --output table
```

## Troubleshooting

### Common Issues:
1. **Permission denied**: Make sure you have sudo access
2. **Package not found**: Update package repositories first
3. **Login issues**: Check network connectivity and tenant ID

### Alternative Solutions:
- Use Azure Cloud Shell in browser (shell.azure.com)
- Install on Windows Subsystem for Linux (WSL)
- Use Azure Portal for manual resource creation

## Next Steps After Installation

1. ✅ Install Azure CLI using one of the methods above
2. ✅ Login with `az login`
3. ✅ Grant permissions to service principal
4. ✅ Test deployment workflow
5. ✅ Create Pull Request to merge branch to main

---

**Current Service Principal**: `b19f1dae-b21a-4a63-b56d-085bad6b23b2`
**Required Role**: Contributor
**Target Resource Groups**: `ai-profile-photo-maker-staging`, `ai-profile-photo-maker-prod`