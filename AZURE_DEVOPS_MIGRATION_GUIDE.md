# Azure DevOps Migration Guide for AI.ProfilePhotoMaker

## Overview

This document provides a comprehensive guide for migrating from GitHub Actions to Azure DevOps, or running both systems in parallel. The migration includes advanced DevOps practices, comprehensive monitoring, and enterprise-grade CI/CD capabilities.

## 📋 Quick Start Checklist

### Prerequisites Setup
- [ ] Azure DevOps organization created
- [ ] Project created in Azure DevOps
- [ ] Azure subscription access configured
- [ ] Container Registry permissions verified

### Service Connections
- [ ] Azure Resource Manager connection: `azure-rm-connection`
- [ ] Container Registry connection: `acr-connection`
- [ ] Optional: SonarCloud connection for code analysis
- [ ] Optional: Aqua Security connection for container scanning

### Variable Groups
- [ ] `aiprofilemaker-common` - Shared variables
- [ ] `aiprofilemaker-dev` - Development environment
- [ ] `aiprofilemaker-staging` - Staging environment  
- [ ] `aiprofilemaker-production` - Production environment

### Agent Configuration
- [ ] Choose agent strategy (Microsoft-hosted vs Self-hosted)
- [ ] If self-hosted: Agent pool created and configured
- [ ] Agent capabilities verified (Docker, .NET 8, Node.js 18)

## 🔄 Migration Strategies

### Strategy 1: Parallel Operation (Recommended)
**Timeline: 2-4 weeks**

1. **Week 1-2**: Set up Azure DevOps infrastructure
2. **Week 2-3**: Test deployments in parallel with GitHub Actions
3. **Week 3-4**: Validate performance and switch over

### Strategy 2: Complete Migration
**Timeline: 1-2 weeks**

Direct migration with temporary downtime during cutover.

### Strategy 3: Hybrid Approach
**Long-term**: Keep GitHub Actions for development, Azure DevOps for production

## 📊 Feature Comparison

| Feature | GitHub Actions | Azure DevOps | Migration Notes |
|---------|---------------|--------------|-----------------|
| **Basic CI/CD** | ✅ | ✅ | Direct mapping available |
| **Container Building** | ✅ | ✅ | Same Docker tasks |
| **Azure Integration** | Good | Excellent | Better service connections |
| **Test Reporting** | Basic | Advanced | Rich test analytics |
| **Code Coverage** | Basic | Advanced | Integrated reporting |
| **Security Scanning** | 3rd party | Built-in + 3rd party | More comprehensive |
| **Work Item Tracking** | Issues | Boards | Full ALM integration |
| **Release Management** | Basic | Advanced | Multi-stage deployments |
| **Approval Workflows** | Basic | Advanced | Rich approval gates |
| **Analytics** | Basic | Advanced | Comprehensive dashboards |

## 🏗️ Architecture Comparison

### Current GitHub Actions Architecture
```
GitHub Repository → GitHub Actions → Azure Container Registry → Azure Container Apps
```

### New Azure DevOps Architecture
```
Azure Repos/GitHub → Azure Pipelines → Azure Container Registry → Azure Container Apps
                                  ↓
                            Application Insights → Azure Monitor → Alerts
```

## 📁 File Structure

```
AI.ProfilePhotoMaker/
├── azure-pipelines.yml                 # Standard pipeline (Microsoft-hosted)
├── azure-pipelines-enterprise.yml      # Enterprise pipeline (Self-hosted)
├── azure-devops-setup.md              # Setup documentation
├── AZURE_DEVOPS_MIGRATION_GUIDE.md    # This migration guide
└── scripts/
    └── setup-azure-devops-agent.sh    # Agent setup automation
```

## ⚙️ Pipeline Configurations

### 1. Standard Pipeline (`azure-pipelines.yml`)
**Best for**: Small teams, getting started, standard workloads

**Features**:
- Microsoft-hosted agents
- Basic CI/CD with testing
- Container building and deployment
- Health checks and validation
- Simple security scanning

### 2. Enterprise Pipeline (`azure-pipelines-enterprise.yml`)
**Best for**: Production environments, large teams, compliance requirements

**Features**:
- Self-hosted agents with caching
- Comprehensive testing (Unit, Integration, Smoke)
- Advanced security scanning (OWASP, container security)
- Multi-environment deployment with approvals
- SonarCloud integration for code quality
- Detailed documentation and reporting
- Performance optimization

## 🔧 Agent Strategy Decision Matrix

| Criteria | Microsoft-hosted | Self-hosted | Recommendation |
|----------|------------------|-------------|----------------|
| **Team Size** | < 10 developers | > 10 developers | Self-hosted for scale |
| **Build Frequency** | < 10/day | > 10/day | Self-hosted for performance |
| **Security Requirements** | Standard | High | Self-hosted for control |
| **Maintenance Overhead** | None | Medium | Microsoft-hosted for simplicity |
| **Cost** | Pay per minute | Infrastructure cost | Depends on usage |
| **Customization** | Limited | Full | Self-hosted for specific needs |

## 📋 Migration Steps

### Phase 1: Infrastructure Setup (Week 1)

#### Day 1-2: Azure DevOps Project Setup
```bash
# 1. Create Azure DevOps project
# 2. Configure repository connection
# 3. Set up service connections
# 4. Create variable groups
```

#### Day 3-5: Agent Configuration
```bash
# For self-hosted agents:
sudo ./scripts/setup-azure-devops-agent.sh

# Environment variables needed:
export AZURE_DEVOPS_URL="https://dev.azure.com/your-org"
export AZURE_DEVOPS_TOKEN="your-pat-token"
```

#### Day 6-7: Pipeline Testing
```bash
# Import pipeline
# Run first test deployment
# Validate against existing GitHub Actions
```

### Phase 2: Parallel Testing (Week 2-3)

#### Testing Checklist
- [ ] Build process validates correctly
- [ ] Tests run and report properly
- [ ] Container images build and push successfully  
- [ ] Infrastructure deployment works
- [ ] Health checks pass
- [ ] Security scans complete
- [ ] Documentation generates correctly

#### Validation Scripts
```bash
# Compare deployment outputs
# Validate application functionality
# Check performance metrics
# Verify monitoring and alerting
```

### Phase 3: Cutover (Week 3-4)

#### Go-Live Checklist
- [ ] All stakeholders trained on Azure DevOps
- [ ] Monitoring and alerting configured
- [ ] Rollback procedures documented
- [ ] GitHub Actions workflow disabled
- [ ] Team notifications updated

## 🔐 Security Enhancements

### Azure DevOps Security Features
1. **Service Connections**: Secure credential management
2. **Variable Groups**: Encrypted secret storage  
3. **Branch Policies**: Required reviews and validations
4. **Environment Approvals**: Deployment gates and approvals
5. **Audit Logging**: Comprehensive activity tracking

### Security Scanning Integration
```yaml
# Container Security (Aqua Security/Trivy)
- task: AquaSec@4
  inputs:
    scanner: 'trivy'
    image: '$(projectName)-api:$(imageTag)'

# Static Code Analysis (SonarCloud)
- task: SonarCloudPrepare@1
  inputs:
    SonarCloud: 'sonarcloud-connection'
    
# Dependency Scanning (OWASP)
- task: dependency-check-build-task@6
```

## 📊 Monitoring and Observability

### Enhanced Monitoring with Azure DevOps
1. **Pipeline Analytics**: Built-in performance metrics
2. **Test Analytics**: Comprehensive test reporting
3. **Deployment Analytics**: Success rates and duration tracking
4. **Work Item Integration**: Link deployments to features

### Application Monitoring Integration
```yaml
# Application Insights Setup
- task: AzureCLI@2
  inputs:
    inlineScript: |
      # Configure Application Insights alerts
      # Set up availability tests
      # Create performance dashboards
```

## 🎯 Performance Optimizations

### Build Performance
- **Parallel Jobs**: Multiple jobs running simultaneously
- **Caching**: NuGet packages, npm modules, Docker layers
- **Incremental Builds**: Only build changed components
- **Agent Pools**: Dedicated agents for faster builds

### Deployment Performance
- **Blue-Green Deployments**: Zero-downtime deployments
- **Health Check Automation**: Faster validation
- **Rollback Automation**: Quick recovery capabilities

## 📈 Success Metrics

### Migration Success Criteria
- [ ] Build time improved by 20% or maintained
- [ ] Deployment success rate > 95%
- [ ] Test coverage reporting enhanced
- [ ] Security scanning integrated
- [ ] Team productivity maintained or improved

### Ongoing Metrics to Track
- Build duration and success rates
- Deployment frequency and success rates  
- Test coverage and quality metrics
- Security scan results and remediation time
- Team velocity and cycle time

## 🚨 Troubleshooting Guide

### Common Issues and Solutions

#### Agent Connection Issues
```bash
# Check agent status
systemctl status vsts-agent-*.service

# View agent logs
journalctl -u vsts-agent-*.service -f

# Restart agent
sudo systemctl restart vsts-agent-*.service
```

#### Service Connection Problems
1. Verify Azure subscription permissions
2. Check service principal expiration
3. Validate resource group access
4. Test connection manually

#### Pipeline Failures
1. Check variable group values
2. Verify template syntax
3. Review build logs
4. Test locally with same steps

#### Deployment Issues
1. Verify container images exist in ACR
2. Check Azure resource quotas
3. Validate Bicep template
4. Review Container Apps logs

### Support Resources
- [Azure DevOps Documentation](https://docs.microsoft.com/azure/devops/)
- [Azure Container Apps Troubleshooting](https://docs.microsoft.com/azure/container-apps/troubleshooting)
- Internal team knowledge base
- Microsoft support (if needed)

## 📝 Documentation and Training

### Team Training Materials
1. **Azure DevOps Overview**: Introduction for the team
2. **Pipeline Usage**: How to run and monitor builds
3. **Deployment Process**: Release management procedures
4. **Troubleshooting**: Common issues and solutions

### Operational Documentation
1. **Runbooks**: Step-by-step operational procedures
2. **Architecture Diagrams**: System overview and data flow
3. **Security Procedures**: Access management and incident response
4. **Maintenance Schedules**: Regular updates and maintenance

## 🎯 Next Steps After Migration

### Immediate (Week 1 post-migration)
- [ ] Monitor system performance
- [ ] Address any issues quickly
- [ ] Gather team feedback
- [ ] Document lessons learned

### Short-term (Month 1)
- [ ] Optimize pipeline performance
- [ ] Enhance monitoring and alerting
- [ ] Implement additional security measures
- [ ] Expand test coverage

### Long-term (Months 2-6)
- [ ] Implement advanced DevOps practices
- [ ] Set up disaster recovery procedures
- [ ] Explore additional Azure DevOps features
- [ ] Continuous improvement based on metrics

---

## 📞 Support and Contacts

For migration support, contact:
- **DevOps Team**: [Team contact information]
- **Azure Support**: [Support ticket process]
- **Internal Documentation**: [Wiki/knowledge base links]

This migration guide provides a comprehensive path from GitHub Actions to enterprise-grade Azure DevOps CI/CD with enhanced monitoring, security, and operational excellence.