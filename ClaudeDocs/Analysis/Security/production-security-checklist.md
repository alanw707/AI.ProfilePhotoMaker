# Production Security Checklist
**AI Profile Photo Maker Application**

## Pre-Deployment Security Validation

### ✅ Secret Management
- [ ] **Generate Secure Secrets**
  - [ ] Run `/ClaudeDocs/Analysis/Security/secure-secret-generator.sh`
  - [ ] Verify all secrets meet minimum length requirements
  - [ ] SQL Password: 16+ characters with complexity
  - [ ] JWT Secret: 256+ bits (64+ base64 characters)
  - [ ] Webhook Secret: 32+ hex characters

- [ ] **Manual Secret Collection**
  - [ ] Google OAuth Client Secret from [Google Cloud Console](https://console.cloud.google.com/apis/credentials)
  - [ ] Replicate API Token from [Replicate Account](https://replicate.com/account/api-tokens)
  - [ ] Verify Replicate token starts with `r8_`

- [ ] **Azure Key Vault Storage**
  - [ ] Store all secrets in Azure Key Vault
  - [ ] Verify Key Vault RBAC permissions
  - [ ] Test secret retrieval from Container Apps
  - [ ] Enable Key Vault access logging

### ✅ Configuration Security
- [ ] **Remove Placeholder Values**
  - [ ] No "REPLACE_WITH_*" values in any configuration files
  - [ ] Verify `appsettings.json` uses environment variables
  - [ ] Verify `appsettings.Production.json` has no hardcoded secrets
  - [ ] Check deployment parameter templates only

- [ ] **Environment Variables**
  - [ ] Set all required environment variables
  - [ ] Use Key Vault references in Bicep template
  - [ ] Verify no secrets in deployment logs
  - [ ] Test environment variable resolution

### ✅ Authentication Security
- [ ] **Google OAuth Configuration**
  - [ ] Client ID: `116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com`
  - [ ] Client Secret stored securely in Key Vault
  - [ ] OAuth redirect URIs configured correctly
  - [ ] Test OAuth flow end-to-end

- [ ] **JWT Security**
  - [ ] JWT secret is cryptographically secure (256+ bits)
  - [ ] Token expiration configured appropriately
  - [ ] Verify token signing and validation works
  - [ ] Test token refresh mechanism

### ✅ Database Security
- [ ] **SQL Server Configuration**
  - [ ] Strong admin password (16+ chars, complexity)
  - [ ] TLS encryption enabled (`Encrypt=True`)
  - [ ] Firewall rules properly configured
  - [ ] No sensitive data logging enabled in production

- [ ] **Connection Security**
  - [ ] Connection string stored in Key Vault
  - [ ] Connection timeout configured (30 seconds)
  - [ ] Test database connectivity from Container Apps
  - [ ] Verify SQL injection protections

### ✅ Storage Security
- [ ] **Azure Blob Storage**
  - [ ] Consider Managed Identity over connection strings
  - [ ] Verify blob access permissions
  - [ ] Check container public access settings
  - [ ] Test image upload/download functionality

### ✅ API Security
- [ ] **Replicate Integration**
  - [ ] API token stored securely in Key Vault
  - [ ] Webhook secret configured and validated
  - [ ] Test AI model training and generation
  - [ ] Verify webhook signature validation

## Deployment Security

### ✅ Infrastructure Security
- [ ] **Azure Container Apps**
  - [ ] System-assigned Managed Identity enabled
  - [ ] HTTPS-only ingress configuration
  - [ ] Custom domain certificates configured
  - [ ] Health check endpoints working

- [ ] **Container Registry**
  - [ ] Consider Managed Identity over admin credentials
  - [ ] Verify image scanning enabled
  - [ ] Check registry firewall rules
  - [ ] Validate image pull permissions

### ✅ Network Security
- [ ] **HTTPS Configuration**
  - [ ] SSL/TLS certificates valid and current
  - [ ] HTTPS redirect enabled
  - [ ] HSTS headers configured
  - [ ] No mixed content warnings

- [ ] **CORS Configuration**
  - [ ] Allowed origins correctly configured
  - [ ] No wildcard origins in production
  - [ ] Verify preflight request handling
  - [ ] Test cross-origin requests

### ✅ Monitoring and Logging
- [ ] **Application Insights**
  - [ ] Connection string configured
  - [ ] Custom telemetry working
  - [ ] Performance monitoring active
  - [ ] Error tracking configured

- [ ] **Security Monitoring**
  - [ ] Key Vault access logs enabled
  - [ ] Authentication failure monitoring
  - [ ] Suspicious activity alerts configured
  - [ ] Log retention policies set

## Post-Deployment Validation

### ✅ Functional Testing
- [ ] **Authentication Flow**
  - [ ] Google OAuth login works
  - [ ] JWT token generation/validation
  - [ ] Session management working
  - [ ] Logout functionality tested

- [ ] **Core Application Features**
  - [ ] User registration/profile creation
  - [ ] Image upload to Azure Storage
  - [ ] AI model training via Replicate
  - [ ] Image generation working
  - [ ] Webhook callbacks functioning

### ✅ Security Testing
- [ ] **Vulnerability Assessment**
  - [ ] No secrets exposed in responses
  - [ ] Authentication bypasses tested
  - [ ] Input validation working
  - [ ] File upload restrictions enforced

- [ ] **Performance Testing**
  - [ ] Load testing with authentication
  - [ ] Resource usage monitoring
  - [ ] Database connection pooling
  - [ ] Image processing performance

## Ongoing Security Operations

### ✅ Secret Rotation
- [ ] **90-Day Rotation Schedule**
  - [ ] Google OAuth Client Secret
  - [ ] JWT signing key
  - [ ] SQL Server admin password
  - [ ] Replicate API token
  - [ ] Webhook secrets

- [ ] **Rotation Procedures**
  - [ ] Document rotation steps
  - [ ] Test rotation in staging
  - [ ] Zero-downtime rotation plan
  - [ ] Emergency rotation procedures

### ✅ Compliance Monitoring
- [ ] **OWASP Top 10 Review**
  - [ ] Monthly security assessment
  - [ ] Vulnerability scanning
  - [ ] Dependency updates
  - [ ] Security patch management

- [ ] **Access Review**
  - [ ] Key Vault access permissions
  - [ ] Container Registry access
  - [ ] Database access review
  - [ ] Admin account audit

## Emergency Procedures

### ✅ Incident Response
- [ ] **Secret Compromise Response**
  - [ ] Immediate secret rotation
  - [ ] Access log review
  - [ ] User notification if required
  - [ ] Incident documentation

- [ ] **Security Breach Response**
  - [ ] Incident response team contacts
  - [ ] Evidence preservation procedures
  - [ ] Communication plan
  - [ ] Recovery procedures

## Quick Security Commands

### Generate All Secrets
```bash
./ClaudeDocs/Analysis/Security/secure-secret-generator.sh
```

### Validate Current Configuration
```bash
# Check for placeholder values
grep -r "REPLACE_WITH" AI.ProfilePhotoMaker.API/ || echo "✅ No placeholders found"

# Check .gitignore excludes secrets
grep -E "\.env|secrets|params\.json" .gitignore && echo "✅ Secrets excluded from git"

# Test Key Vault access
az keyvault secret list --vault-name YOUR_KEY_VAULT_NAME --query "[].name" -o table
```

### Deploy Securely
```bash
# Set environment variables from Key Vault
export SQL_ADMIN_PASSWORD=$(az keyvault secret show --vault-name YOUR_KV --name SqlAdminPassword --query value -o tsv)
export JWT_SECRET=$(az keyvault secret show --vault-name YOUR_KV --name JwtSecret --query value -o tsv)

# Deploy with secure parameters
./scripts/deploy-with-oauth.sh
```

### Monitor Security
```bash
# Check recent Key Vault access
az monitor activity-log list --resource-group aiprofilemaker-v1 --max-events 50 --query "[?contains(resourceId, 'keyvault')]"

# Check application logs for security events
az containerapp logs show --name aipm-api-v1 --resource-group aiprofilemaker-v1 --type console
```

---

## Security Contact Information
- **Security Issues**: Immediate rotation of all secrets
- **Emergency Contact**: Azure Support for infrastructure issues
- **Documentation**: Keep this checklist updated with any configuration changes

**Last Updated**: 2025-08-13  
**Next Review**: 2025-09-13 (30 days)  
**Version**: 1.0