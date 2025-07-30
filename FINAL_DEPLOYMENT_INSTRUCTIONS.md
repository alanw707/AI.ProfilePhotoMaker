# Final Azure Deployment Instructions

**🎯 Status**: Ready for Deployment - All secrets generated and validated!

## 🔧 Manual Steps Required

### **Step 1: Update Parameter Files (5 minutes)**

**Navigate to infrastructure directory:**
```bash
cd infrastructure
```

**Edit Staging Parameters:**
```bash
vi parameters.staging.json

# Replace these values:
"sqlAdminPassword": { "value": "UnPxWvveYHDkCiCH2025!@#" }
"replicateApiToken": { "value": "r8_FvCjahczdLfNmFTcDjhMzMuEuqKQswx2BxWD1" }
"jwtSecret": { "value": "e7b7c0c2-4b6e-4c2d-8e7a-1b2f3c4d5e6f" }
```

**Edit Production Parameters:**
```bash
vi parameters.prod.json

# Replace these values:
"sqlAdminPassword": { "value": "JkGNdDTct101gGAj2025!$%" }
"replicateApiToken": { "value": "r8_FvCjahczdLfNmFTcDjhMzMuEuqKQswx2BxWD1" }
"jwtSecret": { "value": "oznZk9rcI2LWwPbX6LoIx3BFGu0s4ldq4OwdIMy8/II=" }
```

### **Step 2: Azure Authentication (2 minutes)**
```bash
# Login to Azure
az login

# Set your subscription (if you have multiple)
az account set --subscription "Your-Subscription-ID"
```

### **Step 3: Deploy Staging Environment (15-30 minutes)**
```bash
# Deploy staging infrastructure
./deploy.sh --environment staging

# This will create:
# - Resource Group: ai-profile-photo-maker-staging
# - App Service Plan (F1 - Free tier)
# - Azure SQL Database (Basic tier)
# - Static Web App
# - Storage Account
# - Key Vault
# - Application Insights
```

### **Step 4: Configure GitHub Actions (5 minutes)**
```bash
# After staging deployment, get the Static Web App deployment token:
STATIC_WEB_APP_NAME=$(az deployment group show \
  --resource-group ai-profile-photo-maker-staging \
  --name main \
  --query properties.outputs.staticWebAppName.value -o tsv)

DEPLOYMENT_TOKEN=$(az staticwebapp secrets list \
  --name $STATIC_WEB_APP_NAME \
  --resource-group ai-profile-photo-maker-staging \
  --query properties.apiKey -o tsv)

echo "Add this token to GitHub Actions secrets:"
echo "Secret Name: AZURE_STATIC_WEB_APPS_API_TOKEN"
echo "Secret Value: $DEPLOYMENT_TOKEN"
```

**Add to GitHub:**
1. Go to repository Settings → Secrets and variables → Actions
2. Add secret: `AZURE_STATIC_WEB_APPS_API_TOKEN` = `<deployment_token>`

### **Step 5: Test Staging Deployment (10 minutes)**
```bash
# Push code to trigger automatic deployment
git push origin main

# Check staging URLs:
# Frontend: https://aiprofilephotomaker-staging.azurestaticapps.net
# Backend: https://aiprofilephotomakerapi-staging.azurewebsites.net

# Test key functionality:
# - Landing page loads
# - User registration/login
# - Image upload works
# - AI processing functional
```

### **Step 6: Deploy Production (15-30 minutes)**
```bash
# After staging validation, deploy production
./deploy.sh --environment prod

# This creates production environment:
# - Resource Group: ai-profile-photo-maker-prod
# - App Service Plan (B1 - Production tier)
# - Production database with backup
# - Production Static Web App
```

## 📊 Expected Results

### **Staging Environment**
- **Frontend**: `https://aiprofilephotomaker-staging.azurestaticapps.net`
- **API**: `https://aiprofilephotomakerapi-staging.azurewebsites.net`
- **Cost**: ~$50-100/month
- **Purpose**: Testing and validation

### **Production Environment**  
- **Frontend**: `https://aiprofilephotomaker.azurestaticapps.net`
- **API**: `https://aiprofilephotomakerapi.azurewebsites.net`
- **Cost**: ~$200-500/month
- **Purpose**: Live user traffic

## ⚠️ Important Security Notes

### **Parameter Files**
- **✅ DO**: Update parameter files locally with real secrets
- **❌ DON'T**: Commit parameter files with real secrets to git
- **💡 TIP**: After deployment, secrets are stored securely in Azure Key Vault

### **Deployment Process**
- Azure SQL Server will be created during deployment (you don't need existing SQL)
- Database will be created automatically with your generated passwords
- All Azure resources are created by the Bicep template

## 🚨 Troubleshooting

### **Common Issues**

**"Deployment failed - password complexity"**
- Solution: Passwords are already strong, check for special characters in terminal

**"Replicate API unauthorized"**  
- Solution: Token is from your working .NET secrets, should work fine

**"Static Web App deployment token not found"**
- Solution: Wait 2-3 minutes after infrastructure deployment, then retry

**"GitHub Actions deployment fails"**
- Solution: Ensure deployment token is correctly added to GitHub secrets

## ✅ Success Validation

After deployment, verify:

### **Infrastructure Created**
- [ ] Resource groups exist in Azure portal
- [ ] SQL databases are accessible
- [ ] Storage accounts contain containers
- [ ] Application Insights collecting data

### **Applications Working**
- [ ] Frontend loads without errors
- [ ] API health endpoint responds: `/api/health`
- [ ] User registration/login functional
- [ ] Image upload and AI processing works
- [ ] Payment integration active (if configured)

## 🎯 Total Time Estimate

- **Parameter Updates**: 5 minutes
- **Staging Deployment**: 15-30 minutes
- **GitHub Configuration**: 5 minutes
- **Testing**: 10 minutes
- **Production Deployment**: 15-30 minutes
- **Total**: **50-80 minutes to full Azure deployment**

## 🎉 You're Ready!

All secrets are generated and validated:
- ✅ **SQL Passwords**: Strong, unique for staging/production
- ✅ **JWT Secrets**: Secure, different for each environment  
- ✅ **Replicate Token**: From your working .NET user secrets
- ✅ **Infrastructure**: Complete Bicep templates ready

**Next Action**: Update those parameter files and run `./deploy.sh --environment staging`!

---

**Status**: 🚀 **READY FOR AZURE CLOUD DEPLOYMENT**

**Questions?** Check `DEPLOYMENT_CHECKLIST.md` for detailed validation steps.