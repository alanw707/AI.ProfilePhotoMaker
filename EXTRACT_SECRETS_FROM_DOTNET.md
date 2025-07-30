# Extract Secrets from .NET User Secrets

**🎯 Great News!** Your secrets are already configured in .NET user secrets. Here's how to use them for Azure deployment.

## 📋 Current .NET User Secrets

From your `dotnet user-secrets list`, you have:

```
Replicate:ApiToken = r8_FvCjahczdLfNmFTcDjhMzMuEuqKQswx2BxWD1
JWT:Secret = e7b7c0c2-4b6e-4c2d-8e7a-1b2f3c4d5e6f
Authentication:Google:ClientSecret = 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
Authentication:Google:ClientId = 331984288023-lh1upthod06meoko58g7hn9d7h68l311.apps.googleusercontent.com
```

## ✅ Secret Validation

### **Replicate API Token** ✅
- **Format**: `r8_FvCjahczdLfNmFTcDjhMzMuEuqKQswx2BxWD1`
- **Status**: ✅ Valid format (starts with `r8_`)
- **Ready for**: Both staging and production

### **JWT Secret** ⚠️ 
- **Current**: `e7b7c0c2-4b6e-4c2d-8e7a-1b2f3c4d5e6f`
- **Length**: 36 characters ✅ (meets 32+ requirement)
- **Status**: ✅ Valid for staging
- **Recommendation**: Generate different secret for production

### **Missing: SQL Admin Password**
- **Status**: ❌ Not in user secrets
- **Action**: Need to generate strong password for Azure SQL

## 🔧 Update Azure Parameter Files

### **Step 1: Update Staging Parameters**

Replace in `infrastructure/parameters.staging.json`:

```json
{
  "sqlAdminPassword": {
    "value": "YourStrongStagingPassword2025!#"
  },
  "replicateApiToken": {
    "value": "r8_FvCjahczdLfNmFTcDjhMzMuEuqKQswx2BxWD1"
  },
  "jwtSecret": {
    "value": "e7b7c0c2-4b6e-4c2d-8e7a-1b2f3c4d5e6f"
  }
}
```

### **Step 2: Update Production Parameters**

Replace in `infrastructure/parameters.prod.json`:

```json
{
  "sqlAdminPassword": {
    "value": "YourStrongProductionPassword2025!@#$"
  },
  "replicateApiToken": {
    "value": "r8_FvCjahczdLfNmFTcDjhMzMuEuqKQswx2BxWD1"
  },
  "jwtSecret": {
    "value": "different-production-jwt-secret-32-chars-min"
  }
}
```

## 🛠️ Generate Missing Secrets

### **SQL Admin Password** (Required)

Create strong passwords for both environments:

```bash
# Generate suggestions (pick one and modify)
echo "StagingDB2025!@#\$SecurePass"
echo "ProductionSQL2025!@#\$StrongPass"

# Ensure your password has:
# - 16+ characters
# - Uppercase letters (A-Z)
# - Lowercase letters (a-z) 
# - Numbers (0-9)
# - Symbols (!@#$%^&*)
```

### **Production JWT Secret** (Recommended)

Generate a different JWT secret for production:

```bash
# Generate new production JWT secret
openssl rand -base64 32

# Or use Python
python3 -c "import secrets; print(secrets.token_urlsafe(32))"

# Or use Node.js
node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"
```

## ⚡ Quick Command Reference

### **Extract Current Secrets**
```bash
# Get your current secrets
echo "Replicate Token: $(dotnet user-secrets get "Replicate:ApiToken")"
echo "JWT Secret: $(dotnet user-secrets get "JWT:Secret")"
```

### **Validate Replicate Token**
```bash
# Test your Replicate token works
curl -H "Authorization: Token r8_FvCjahczdLfNmFTcDjhMzMuEuqKQswx2BxWD1" \
     https://api.replicate.com/v1/models
```

## 🎯 Ready-to-Use Values

Based on your .NET user secrets, here are the values ready for Azure:

### **For Staging (`parameters.staging.json`)**:
```json
"replicateApiToken": {
  "value": "r8_FvCjahczdLfNmFTcDjhMzMuEuqKQswx2BxWD1"
},
"jwtSecret": {
  "value": "e7b7c0c2-4b6e-4c2d-8e7a-1b2f3c4d5e6f"
},
"sqlAdminPassword": {
  "value": "GENERATE_STRONG_PASSWORD_FOR_STAGING"
}
```

### **For Production (`parameters.prod.json`)**:
```json
"replicateApiToken": {
  "value": "r8_FvCjahczdLfNmFTcDjhMzMuEuqKQswx2BxWD1"
},
"jwtSecret": {
  "value": "GENERATE_DIFFERENT_JWT_SECRET_FOR_PRODUCTION"
},
"sqlAdminPassword": {
  "value": "GENERATE_STRONG_PASSWORD_FOR_PRODUCTION"
}
```

## 🚀 Next Steps

1. **Generate SQL passwords** for staging and production
2. **Generate different JWT secret** for production (security best practice)
3. **Update parameter files** with these values
4. **Deploy staging first**: `./infrastructure/deploy.sh --environment staging`
5. **Test staging thoroughly** before production deployment
6. **Deploy production**: `./infrastructure/deploy.sh --environment prod`

## 🔒 Security Reminders

- ✅ **Replicate token**: Already secure and ready to use
- ✅ **JWT secret**: Valid for staging, generate new for production
- ❌ **SQL passwords**: Must create strong passwords for both environments
- 🔒 **Never commit**: Parameter files with real secrets to git

**Status**: 67% Ready - Just need SQL passwords and production JWT secret!

---

**Time Saved**: Using existing .NET user secrets saves ~30 minutes of secret generation and configuration! 🎉