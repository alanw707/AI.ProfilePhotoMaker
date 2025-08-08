# Azure DevOps Setup Guide for AI.ProfilePhotoMaker

This guide provides comprehensive instructions for setting up Azure DevOps CI/CD pipelines for the AI.ProfilePhotoMaker project.

## Prerequisites

### Azure Resources
- Azure DevOps organization
- Azure subscription with appropriate permissions
- Existing Azure Container Registry (ACR)
- Resource group for deployment

### Required Tools
- Azure CLI (`az` command)
- Docker Desktop
- Git
- .NET 8 SDK
- Node.js 18+

## 1. Azure DevOps Project Setup

### Create New Project
1. Go to your Azure DevOps organization
2. Create a new project: "AI-ProfilePhotoMaker"
3. Choose Git for version control
4. Set visibility (Private recommended)

### Repository Connection
Choose one of these options:

#### Option A: Azure Repos (Recommended for full Azure DevOps integration)
```bash
# Clone from GitHub and push to Azure Repos
git clone https://github.com/alanw707/AI.ProfilePhotoMaker.git
cd AI.ProfilePhotoMaker
git remote add azure https://dev.azure.com/YOUR-ORG/AI-ProfilePhotoMaker/_git/AI-ProfilePhotoMaker
git push azure main
```

#### Option B: GitHub Integration
1. Go to Project Settings → Service connections
2. Create a new GitHub service connection
3. Authorize Azure DevOps to access your GitHub repository

## 2. Service Connections Setup

### Azure Resource Manager Connection
1. Go to Project Settings → Service connections
2. Create new service connection → Azure Resource Manager
3. Choose "Service principal (automatic)"
4. Select your subscription and resource group
5. Name: `azure-rm-connection`
6. Grant access to all pipelines

### Azure Container Registry Connection
1. Create new service connection → Docker Registry
2. Choose "Azure Container Registry"
3. Select your ACR instance
4. Name: `acr-connection`
5. Grant access to all pipelines

## 3. Variable Groups Setup

### Create Variable Group
1. Go to Pipelines → Library
2. Create variable group: `aiprofilemaker-variables`
3. Add these variables:

```yaml
# Secret variables (click lock icon)
sqlAdminPassword: "YourStrongPassword123!"
jwtSecret: "YourJWTSecretKey-32CharactersMinimum"
replicateApiToken: "r8_your-replicate-api-token"

# Azure subscription details
AZURE_SUBSCRIPTION_ID: "your-subscription-id"
AZURE_TENANT_ID: "your-tenant-id"
AZURE_CLIENT_ID: "your-service-principal-client-id"

# Optional: Resource group override
resourceGroupName: "aiprofilemaker-v1"
```

## 4. Agent Configuration Options

### Option A: Microsoft-hosted Agents (Recommended for getting started)

**Benefits:**
- No infrastructure management
- Always up-to-date
- Immediate availability
- Good for standard builds

**Configuration:** Already configured in `azure-pipelines.yml`

### Option B: Self-hosted Agents (Recommended for production)

**Benefits:**
- Better performance with cached dependencies
- Access to private networks
- Consistent build environment
- More control over build tools

#### Self-hosted Agent Setup

1. **Create Agent Pool**
   - Go to Organization Settings → Agent pools
   - Create new pool: "AI-ProfilePhotoMaker-Pool"
   - Grant permission to your project

2. **Install Agent (Linux/WSL)**
```bash
# Create agent directory
mkdir ~/azagent && cd ~/azagent

# Download agent
wget https://vstsagentpackage.azureedge.net/agent/3.232.0/vsts-agent-linux-x64-3.232.0.tar.gz
tar zxvf vsts-agent-linux-x64-3.232.0.tar.gz

# Configure agent
./config.sh

# When prompted:
# Server URL: https://dev.azure.com/YOUR-ORG
# Authentication type: PAT
# Personal access token: [Create PAT with Agent Pools (read, manage) scope]
# Agent pool: AI-ProfilePhotoMaker-Pool
# Agent name: ai-profilemaker-agent-01
# Work folder: _work
```

3. **Install Prerequisites on Agent**
```bash
# Install .NET 8
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0

# Install Node.js 18
curl -fsSL https://deb.nodesource.com/setup_18.x | sudo -E bash -
sudo apt-get install -y nodejs

# Install Docker
sudo apt-get update
sudo apt-get install -y docker.io
sudo systemctl start docker
sudo systemctl enable docker
sudo usermod -aG docker $USER

# Install Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
```

4. **Start Agent**
```bash
# Run interactively (for testing)
./run.sh

# Run as service (for production)
sudo ./svc.sh install
sudo ./svc.sh start
```

5. **Update Pipeline for Self-hosted Agent**
Edit `azure-pipelines.yml`:
```yaml
pool:
  name: 'AI-ProfilePhotoMaker-Pool'  # Replace vmImage line
```

## 5. Pipeline Configuration

### Import Pipeline
1. Go to Pipelines → Pipelines
2. Click "New pipeline"
3. Choose your repository source
4. Select "Existing Azure Pipelines YAML file"
5. Select `/azure-pipelines.yml`
6. Review and run

### Environment Setup
1. Go to Pipelines → Environments
2. Create environment: "production"
3. Add approval checks if needed
4. Configure deployment history retention

## 6. Branch Policies (Optional but Recommended)

1. Go to Repos → Branches
2. Click on "main" branch
3. Go to "Branch policies"
4. Configure:
   - Require a minimum number of reviewers: 1
   - Check for linked work items: Optional
   - Build validation: Add build policy using your pipeline

## 7. Monitoring and Notifications

### Setup Notifications
1. Go to Project Settings → Notifications
2. Create subscription for:
   - Build completion
   - Build failure
   - Release deployment completion

### Azure Monitor Integration
The pipeline automatically configures Application Insights alerts. Additional monitoring can be set up through:
- Azure Monitor dashboards
- Log Analytics queries
- Custom alert rules

## 8. Security Best Practices

### Secret Management
- All secrets stored in Azure DevOps variable groups
- Use Azure Key Vault for production secrets
- Enable secret scanning in repositories

### Access Control
- Use least-privilege principle for service connections
- Regular review of permissions
- Enable audit logging

### Container Security
- Enable vulnerability scanning in ACR
- Use base images from trusted sources
- Regular security updates

## 9. Migration from GitHub Actions

### Parallel Operation Period
1. Keep GitHub Actions workflow running
2. Test Azure DevOps pipeline in parallel
3. Compare deployment outcomes
4. Switch traffic once validated

### Complete Migration
1. Disable GitHub Actions workflow
2. Update documentation
3. Train team on Azure DevOps interface

## 10. Troubleshooting

### Common Issues

**Agent Connection Issues:**
```bash
# Check agent status
./run.sh --once

# View agent logs
tail -f _diag/Agent_*.log
```

**Docker Permission Issues:**
```bash
# Add user to docker group
sudo usermod -aG docker $(whoami)
# Log out and back in
```

**Build Failures:**
- Check variable group values
- Verify service connection permissions
- Review build logs in Azure DevOps

**Deployment Issues:**
- Verify Azure subscription permissions
- Check resource group exists
- Validate Bicep template syntax

### Support Resources
- Azure DevOps Documentation
- Azure Container Apps Troubleshooting
- Community forums and Stack Overflow

## 11. Advanced Configuration

### Multi-Environment Setup
Create separate environments for:
- Development
- Staging  
- Production

### Performance Optimization
- Enable parallel job execution
- Use artifact caching
- Implement incremental builds

### Integration Testing
- Add integration test stage
- Use test containers
- Implement smoke tests

---

## Next Steps

1. Complete Azure DevOps project setup
2. Configure service connections and variable groups
3. Choose agent strategy (Microsoft-hosted vs self-hosted)
4. Import and test the pipeline
5. Set up monitoring and notifications
6. Plan migration timeline from GitHub Actions

This setup provides enterprise-grade CI/CD with comprehensive monitoring, security, and scalability for the AI.ProfilePhotoMaker application.