# Secrets Configuration Guide - Azure Deployment

**🔒 SECURITY WARNING**: This guide contains instructions for handling sensitive secrets. Never commit actual secrets to version control.

## 🎯 Overview

Before deploying to Azure, you need to update the parameter files with actual secret values. The current files contain placeholder values that must be replaced.

## 📋 Required Secrets

### **1. SQL Admin Password**
- **Purpose**: Administrator password for Azure SQL Database
- **Requirements**: 
  - Minimum 16 characters
  - Must contain uppercase letters (A-Z)
  - Must contain lowercase letters (a-z)
  - Must contain numbers (0-9)
  - Must contain symbols (!@#$%^&*)
  - Cannot contain username or parts of username

**Example Strong Password Pattern**:
```
MyApp2025!SecureDB#Admin$Pass
```

### **2. Replicate API Token**
- **Purpose**: Authentication for Replicate AI image processing service
- **How to Get**:
  1. Visit https://replicate.com
  2. Sign up/login to your account
  3. Go to Account Settings → API Tokens
  4. Create a new token or copy existing token
  5. Token format: `r8_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`

### **3. JWT Secret Key**
- **Purpose**: Signing key for JSON Web Tokens (user authentication)
- **Requirements**:
  - Minimum 32 characters
  - Should be randomly generated
  - Use different keys for staging vs production
  - Keep highly confidential

**Generate Strong JWT Secret**:
```bash
# Method 1: Using openssl
openssl rand -base64 32

# Method 2: Using Python
python3 -c "import secrets; print(secrets.token_urlsafe(32))"

# Method 3: Using Node.js
node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"
```

## 🔧 Parameter File Updates

### **Step 1: Update Staging Parameters**

**File**: `infrastructure/parameters.staging.json`

```bash
# Navigate to infrastructure directory
cd infrastructure

# Make a backup of original file
cp parameters.staging.json parameters.staging.json.backup

# Edit the staging parameters
# Replace the following placeholders:
```

**Replace These Values**:
```json
{
  "sqlAdminPassword": {
    "value": "REPLACE_WITH_STRONG_PASSWORD_STAGING_123!"
  },
  "replicateApiToken": {
    "value": "REPLACE_WITH_YOUR_REPLICATE_TOKEN"
  },
  "jwtSecret": {
    "value": "REPLACE_WITH_YOUR_JWT_SECRET_KEY_STAGING_MIN_32_CHARS"
  }
}
```

**With Your Actual Values**:
```json
{
  "sqlAdminPassword": {
    "value": "YourStrongStagingPassword2025!#"
  },
  "replicateApiToken": {
    "value": "r8_your_actual_replicate_token_here"
  },
  "jwtSecret": {
    "value": "your-generated-32-char-jwt-secret-for-staging"
  }
}
```

### **Step 2: Update Production Parameters**

**File**: `infrastructure/parameters.prod.json`

⚠️ **CRITICAL**: Use **different, stronger secrets** for production!

**Replace These Values**:
```json
{
  "sqlAdminPassword": {
    "value": "REPLACE_WITH_STRONG_PASSWORD_123!"
  },
  "replicateApiToken": {
    "value": "REPLACE_WITH_YOUR_REPLICATE_TOKEN"
  },
  "jwtSecret": {
    "value": "REPLACE_WITH_YOUR_JWT_SECRET_KEY_MIN_32_CHARS"
  }
}
```

**With Your Production Values**:
```json
{
  "sqlAdminPassword": {
    "value": "ProductionSecurePass2025!@#$"
  },
  "replicateApiToken": {
    "value": "r8_your_production_replicate_token_here"
  },
  "jwtSecret": {
    "value": "your-different-production-jwt-secret-32-chars"
  }
}
```

## 🛡️ Security Best Practices

### **DO:**
✅ Use different secrets for staging vs production  
✅ Generate strong, random passwords  
✅ Store secrets in Azure Key Vault after deployment  
✅ Use environment variables for local development  
✅ Rotate secrets regularly (quarterly)  
✅ Keep backups of parameter files in secure location  

### **DON'T:**
❌ Never commit actual secrets to git  
❌ Don't reuse passwords across environments  
❌ Don't share secrets via email or chat  
❌ Don't use weak or predictable passwords  
❌ Don't store secrets in plain text files  

## 🔍 Verification Steps

### **Before Deployment - Verify Your Updates**

1. **Check Parameter Files**:
   ```bash
   # Verify no placeholder values remain
   grep -r "REPLACE_WITH" infrastructure/parameters.*.json
   
   # Should return no results if all placeholders are replaced
   ```

2. **Validate Secret Strength**:
   ```bash
   # Check password length (should be 16+ characters)
   # Check JWT secret length (should be 32+ characters)
   # Verify Replicate token format (starts with r8_)
   ```

3. **Test Replicate Token**:
   ```bash
   # Test your Replicate token
   curl -H "Authorization: Token r8_your_token_here" \
        https://api.replicate.com/v1/models
   
   # Should return JSON response, not error
   ```

## 🚀 Deployment Process

### **After Updating Secrets**

1. **Commit Parameter Updates** (without exposing secrets):
   ```bash
   # Add deployment documentation
   git add AZURE_DEPLOYMENT_BACKLOG.md DEPLOYMENT_CHECKLIST.md SECRETS_CONFIGURATION_GUIDE.md
   
   # DO NOT add parameter files with real secrets
   git commit -m "docs: add Azure deployment backlog and configuration guides"
   ```

2. **Deploy Staging First**:
   ```bash
   cd infrastructure
   ./deploy.sh --environment staging
   ```

3. **Validate Staging Deployment**:
   - Check all resources created successfully
   - Test application functionality
   - Verify secrets are working correctly

4. **Deploy Production** (after staging validation):
   ```bash
   ./deploy.sh --environment prod
   ```

## 🆘 Troubleshooting

### **Common Issues**

**Issue**: Deployment fails with "Invalid password"
**Solution**: Ensure SQL password meets complexity requirements

**Issue**: Replicate API returns 401 Unauthorized
**Solution**: Verify token is correct and account has credits

**Issue**: JWT validation fails
**Solution**: Ensure JWT secret is at least 32 characters

**Issue**: Azure Key Vault access denied
**Solution**: Check managed identity permissions

### **Recovery Procedures**

**If Secrets Are Compromised**:
1. Immediately rotate all affected secrets
2. Update parameter files with new values
3. Redeploy infrastructure
4. Update any dependent services
5. Monitor for suspicious activity

**If Deployment Fails**:
1. Check deployment logs in Azure portal
2. Verify all parameter values are correct
3. Ensure Azure CLI is authenticated
4. Check subscription permissions
5. Review Bicep template for issues

## 📞 Support

### **Getting Help**
- **Azure Issues**: Check Azure portal deployment logs
- **Replicate Issues**: Visit https://replicate.com/docs
- **JWT Issues**: Verify secret length and encoding
- **General Deployment**: Review DEPLOYMENT_CHECKLIST.md

### **Emergency Contacts**
- Azure Support: Through Azure portal
- Development Team: Internal escalation
- Security Team: For secret compromise incidents

---

**⚠️ REMINDER**: Always keep secrets secure and never commit them to version control!

**Next Steps**: 
1. Update parameter files with your actual secrets
2. Follow DEPLOYMENT_CHECKLIST.md for deployment
3. Monitor deployment success in Azure portal

**Status**: Ready for Implementation  
**Last Updated**: July 30, 2025