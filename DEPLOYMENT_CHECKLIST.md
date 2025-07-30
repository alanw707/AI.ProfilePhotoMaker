# Azure Deployment Checklist - AI Profile Photo Maker

**Quick Reference**: Step-by-step deployment validation checklist

## 🚀 Pre-Deployment Checklist

### **Required Tools & Access**
- [ ] Azure CLI installed (`az --version`)
- [ ] Bicep CLI installed (`bicep --version`)
- [ ] Azure subscription access (Contributor role)
- [ ] GitHub repository admin access
- [ ] Domain name ready (if using custom domain)

### **Required Secrets & Credentials**
- [ ] SQL Admin Password (16+ chars, complex)
- [ ] Replicate API Token (from replicate.com account)
- [ ] JWT Secret Key (32+ random characters)
- [ ] Azure Service Principal (for GitHub Actions)

### **Parameter File Updates**
- [ ] Update `infrastructure/parameters.staging.json`
  - [ ] Replace `REPLACE_WITH_STRONG_PASSWORD_STAGING_123!`
  - [ ] Replace `REPLACE_WITH_YOUR_REPLICATE_TOKEN`
  - [ ] Replace `REPLACE_WITH_YOUR_JWT_SECRET_KEY_STAGING_MIN_32_CHARS`
- [ ] Update `infrastructure/parameters.prod.json`  
  - [ ] Replace `REPLACE_WITH_STRONG_PASSWORD_123!`
  - [ ] Replace `REPLACE_WITH_YOUR_REPLICATE_TOKEN`  
  - [ ] Replace `REPLACE_WITH_YOUR_JWT_SECRET_KEY_MIN_32_CHARS`

## 🧪 Staging Deployment Checklist

### **Infrastructure Deployment**
- [ ] Login to Azure CLI (`az login`)
- [ ] Set correct subscription (`az account set --subscription "..."`)
- [ ] Navigate to infrastructure directory (`cd infrastructure`)
- [ ] Execute staging deployment (`./deploy.sh --environment staging`)
- [ ] Verify deployment success (check Azure portal)

### **Resource Validation**
- [ ] Resource Group created: `ai-profile-photo-maker-staging`
- [ ] App Service Plan created (F1 tier)
- [ ] App Service created: `aiprofilephotomakerapi-staging`
- [ ] Static Web App created: `aiprofilephotomaker-swa-staging`
- [ ] SQL Server & Database created
- [ ] Storage Account created
- [ ] Key Vault created with secrets
- [ ] Application Insights created

### **GitHub Actions Configuration**
- [ ] Get Static Web App deployment token:
  ```bash
  STATIC_WEB_APP_NAME=$(az deployment group show \
    --resource-group ai-profile-photo-maker-staging \
    --name main \
    --query properties.outputs.staticWebAppName.value -o tsv)
  
  DEPLOYMENT_TOKEN=$(az staticwebapp secrets list \
    --name $STATIC_WEB_APP_NAME \
    --resource-group ai-profile-photo-maker-staging \
    --query properties.apiKey -o tsv)
  ```
- [ ] Add GitHub repository secret: `AZURE_STATIC_WEB_APPS_API_TOKEN`
- [ ] Configure backend deployment secrets (if needed)

### **Application Deployment Testing**
- [ ] Push code to trigger GitHub Actions
- [ ] Verify frontend deployment success
- [ ] Verify backend deployment success
- [ ] Check deployment logs for errors

## ✅ Staging Validation Checklist

### **Frontend Validation**
- [ ] Static Web App URL accessible
- [ ] Landing page loads correctly
- [ ] Navigation menu functional
- [ ] Theme toggle working
- [ ] No console errors in browser

### **Backend API Validation**
- [ ] API health endpoint responds (`/api/health`)
- [ ] Database connection working
- [ ] Blob storage accessible
- [ ] Key Vault integration working
- [ ] Authentication endpoints functional

### **End-to-End Testing**
- [ ] User registration flow
- [ ] User login flow
- [ ] Image upload functionality
- [ ] AI processing pipeline
- [ ] Payment integration (if applicable)
- [ ] Email notifications (if applicable)

### **Performance & Security**
- [ ] Page load times acceptable (<3s)
- [ ] API response times good (<500ms)
- [ ] HTTPS enforced everywhere
- [ ] CORS configuration working
- [ ] No sensitive data in client-side code

## 🚀 Production Deployment Checklist

### **Pre-Production Validation**
- [ ] All staging tests passed
- [ ] Performance testing completed
- [ ] Security review completed
- [ ] Business stakeholder approval

### **Production Infrastructure Deployment**
- [ ] Update production parameter file with unique secrets
- [ ] Execute production deployment (`./deploy.sh --environment prod`)
- [ ] Verify all resources created successfully
- [ ] Resource Group: `ai-profile-photo-maker-prod`

### **Production Resource Validation**
- [ ] App Service Plan created (B1 tier)
- [ ] App Service created: `aiprofilephotomakerapi`
- [ ] Static Web App created: `aiprofilephotomaker`
- [ ] Production database with backup enabled
- [ ] Production storage with appropriate tier
- [ ] Key Vault with production secrets
- [ ] Application Insights configured

### **Custom Domain Setup (Optional)**
- [ ] Domain DNS configured
- [ ] SSL certificate provisioned
- [ ] HTTPS redirect working
- [ ] Custom domain accessible

## 🔍 Production Validation Checklist

### **Functional Testing**
- [ ] All staging tests repeated on production
- [ ] User registration & login
- [ ] Image upload & processing
- [ ] Payment processing (with test transactions)
- [ ] Email notifications working
- [ ] Admin functionality accessible

### **Performance Testing**
- [ ] Load testing completed
- [ ] Database performance acceptable
- [ ] CDN delivering static content
- [ ] Image processing within SLA
- [ ] API response times optimal

### **Security Validation**
- [ ] HTTPS enforced across all endpoints
- [ ] Security headers present
- [ ] SQL injection protection active
- [ ] XSS protection enabled
- [ ] CSRF protection implemented
- [ ] Rate limiting configured

### **Monitoring Setup**
- [ ] Application Insights collecting data
- [ ] Custom dashboards configured
- [ ] Alert rules active:
  - [ ] Error rate > 1% (critical)
  - [ ] Response time > 5s (critical)
  - [ ] CPU usage > 80% (warning)
  - [ ] Memory usage > 85% (warning)
- [ ] Log Analytics workspace collecting logs
- [ ] Cost monitoring alerts configured

## 📊 Post-Deployment Checklist

### **Documentation Updates**
- [ ] Update README with production URLs
- [ ] Document environment-specific configurations
- [ ] Update API documentation with production endpoints
- [ ] Create runbook for common operations

### **Team Communication**
- [ ] Notify team of production deployment
- [ ] Share monitoring dashboard links
- [ ] Document troubleshooting procedures
- [ ] Set up on-call rotation (if applicable)

### **Business Validation**
- [ ] Product owner testing completed
- [ ] Marketing team notified of go-live
- [ ] Customer support team briefed
- [ ] Analytics tracking verified

### **Backup & Recovery**
- [ ] Database backup policy verified
- [ ] Recovery procedures tested
- [ ] Disaster recovery plan documented
- [ ] Data retention policies implemented

## 🚨 Rollback Checklist

### **If Issues Occur**
- [ ] Stop new user traffic (if possible)
- [ ] Identify root cause
- [ ] Check Application Insights for errors
- [ ] Review recent deployment logs

### **Rollback Procedures**
- [ ] GitHub: Revert to previous working commit
- [ ] Infrastructure: Redeploy previous template version
- [ ] Database: Restore from backup (if needed)
- [ ] DNS: Switch back to staging (if domain was switched)

### **Post-Rollback**
- [ ] Verify system stability
- [ ] Communicate status to team
- [ ] Document incident for learning
- [ ] Plan fix for next deployment

## 🎯 Success Criteria

### **Technical Success**
- [ ] All services deployed and functional
- [ ] 99.9% uptime target achieved
- [ ] Performance SLAs met
- [ ] Security requirements satisfied

### **Business Success**
- [ ] User registration working
- [ ] Payment processing functional
- [ ] Core user journey complete
- [ ] Analytics tracking active

---

**Quick Commands Reference:**
```bash
# Check deployment status
az deployment group show --resource-group ai-profile-photo-maker-staging --name main

# Get Static Web App deployment token
az staticwebapp secrets list --name <app-name> --resource-group <rg-name> --query properties.apiKey -o tsv

# Restart App Service
az webapp restart --name <app-name> --resource-group <rg-name>

# View application logs
az webapp log tail --name <app-name> --resource-group <rg-name>
```

**Status**: Ready for Use  
**Last Updated**: July 30, 2025