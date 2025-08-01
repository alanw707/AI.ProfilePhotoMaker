# 🚀 Simple Deployment Guide

**Perfect for Solo Developers** - Get your AI Profile Photo Maker running in Azure with minimal complexity and cost.

## 📋 Prerequisites

1. **Azure Account** with an active subscription
2. **GitHub Account** for CI/CD
3. **Azure CLI** installed locally (optional for manual deployments)

## 🛠️ One-Time Setup

### 1. Create Azure Service Principal

```bash
# Login to Azure
az login

# Create service principal
az ad sp create-for-rbac --name "ai-profile-maker-deploy" --role contributor --scopes /subscriptions/YOUR_SUBSCRIPTION_ID --sdk-auth
```

Save the JSON output - you'll need it for GitHub secrets.

### 2. Configure GitHub Secrets

Go to your GitHub repository → Settings → Secrets and variables → Actions

Add these secrets:

| Secret Name | Description | Example |
|-------------|-------------|---------|
| `AZURE_CLIENT_ID` | From service principal JSON | `12345678-1234-1234-1234-123456789012` |
| `AZURE_TENANT_ID` | From service principal JSON | `12345678-1234-1234-1234-123456789012` |
| `AZURE_SUBSCRIPTION_ID` | Your Azure subscription ID | `12345678-1234-1234-1234-123456789012` |
| `SQL_ADMIN_PASSWORD` | Strong password for SQL Server | `MyStr0ng!P@ssw0rd` |
| `JWT_SECRET` | Random string for JWT tokens | `super-secret-jwt-key-change-me` |
| `REPLICATE_API_TOKEN` | Your Replicate API token | `r8_abc123...` |

## 🚀 Deploy

### Option 1: Automatic (Recommended)
Just push to the `main` branch! The GitHub Action will:
1. Test your code
2. Deploy infrastructure
3. Build and deploy containers
4. Run health checks

### Option 2: Manual Trigger
1. Go to Actions tab in your GitHub repo
2. Select "🚀 Simple Deploy"
3. Click "Run workflow"
4. Choose options and run

## 📊 What Gets Deployed

Your deployment creates these Azure resources:

```
Resource Group: rg-aiprofilemaker
├── Container Registry (for your Docker images)
├── SQL Database (Basic tier, 2GB)
├── Storage Account (for profile images)
├── Key Vault (for secrets)
├── Application Insights (monitoring)
├── Container Apps Environment
├── Backend Container App (your API)
└── Frontend Container App (your Angular app)
```

**Estimated monthly cost: $50-150 USD** (scales to zero when not used)

## 🔍 Monitoring

- **Application Insights**: Monitor performance and errors
- **Container Apps Metrics**: View scaling and resource usage
- **SQL Database Metrics**: Monitor database performance

Access via Azure Portal → Your Resource Group → Application Insights

## 🛡️ Security Features

- ✅ All secrets stored in Azure Key Vault
- ✅ HTTPS enforced on all endpoints
- ✅ Container images stored in private registry
- ✅ SQL Database with firewall rules
- ✅ Managed identities (no stored passwords)

## 🔧 Troubleshooting

### Deployment Failed?
1. Check GitHub Actions logs
2. Verify all secrets are set correctly
3. Ensure your Azure account has sufficient permissions

### App Not Loading?
1. Check Container Apps logs in Azure Portal
2. Verify images built successfully
3. Check Application Insights for errors

### Database Connection Issues?
1. Verify SQL Admin password is correct
2. Check firewall rules allow Azure services
3. Confirm connection string in Key Vault

## 💡 Development Workflow

1. **Make changes** to your code locally
2. **Push to main branch** - triggers automatic deployment
3. **Check GitHub Actions** for deployment status
4. **Test your app** at the provided URLs

## 🔄 Rolling Back

If something goes wrong:
1. Check previous successful deployment in Azure Portal
2. Roll back to previous container images via Azure Portal
3. Or revert your Git commit and push again

## 💰 Cost Optimization Tips

1. **Container Apps scale to zero** when not used
2. **SQL Database Basic tier** is perfect for development
3. **Storage costs** are minimal for image files
4. **Monitor spending** via Azure Cost Management

## 📈 Scaling Up Later

When your app grows, you can easily:
- Upgrade SQL Database tier
- Increase Container Apps resources
- Add Azure CDN for global performance
- Enable multi-region deployment
- Add more sophisticated monitoring

## 🆘 Need Help?

- Check Azure Portal for resource health
- Review GitHub Actions logs for deployment issues
- Use Application Insights to debug runtime issues
- Azure support is available 24/7

---

**That's it!** Your AI Profile Photo Maker is now running in the cloud with professional-grade infrastructure. Focus on building features, not managing servers! 🎉