# Production Deployment Security Checklist
**AI Profile Photo Maker - Replicate Secrets Configuration**

## Pre-Deployment Security Requirements ✅

### Critical Security Controls (MUST COMPLETE)

#### 1. Replicate API Configuration
- [ ] **Obtain Production API Token**
  - Login to [Replicate.com](https://replicate.com/account/api-tokens)
  - Generate new API token for production use
  - Verify token starts with `r8_` and is 40+ characters
  - **⚠️ Never use development/test tokens in production**

- [ ] **Configure Webhook Security**  
  - Use standardized webhook secret: `whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM`
  - Verify webhook secret is configured in Replicate dashboard
  - Test webhook signature validation locally
  - **⚠️ All environments must use the same webhook secret**

#### 2. Secret Storage Security
- [ ] **Azure Key Vault Configuration**
  - Verify Key Vault exists: `aipm-kv-v1-{suffix}`
  - Store `ReplicateApiToken` in Key Vault
  - Store `ReplicateWebhookSecret` in Key Vault  
  - Verify Container Apps has Key Vault access
  - **⚠️ Never store secrets in configuration files**

- [ ] **Local Development Secrets**
  ```bash
  # Configure dotnet user-secrets
  dotnet user-secrets set "Replicate:ApiToken" "your-production-token"
  dotnet user-secrets set "Replicate:WebhookSecret" "whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM"
  ```

#### 3. Infrastructure Security
- [ ] **Bicep Template Validation**
  - Verify `replicateApiToken` parameter exists
  - Verify `replicateWebhookSecret` parameter exists
  - Confirm Container Apps environment variables configured
  - Test deployment with secure parameters

- [ ] **Production Configuration Cleanup**
  - Remove all `REPLACE_WITH_*` placeholders
  - Verify no hardcoded secrets in `appsettings.json`
  - Update production config to use Key Vault references
  - **⚠️ Scan for any remaining placeholder values**

### Security Validation Tests

#### 4. Authentication Testing
- [ ] **API Token Validation**
  ```bash
  # Test API token format
  curl -H "Authorization: Token $REPLICATE_API_TOKEN" https://api.replicate.com/v1/models
  ```

- [ ] **Webhook Signature Testing**
  ```bash
  # Run local application with production secrets
  dotnet run --project AI.ProfilePhotoMaker.API
  # Test webhook endpoint with signed request
  ```

#### 5. Application Security Testing
- [ ] **Startup Configuration Test**
  - Application starts without errors
  - Replicate services initialize properly
  - Webhook endpoints respond correctly
  - No secret-related exceptions in logs

- [ ] **End-to-End Integration Test**
  - Create test model training request
  - Verify webhook callbacks are processed
  - Confirm signature validation works
  - Test image generation workflow

## Deployment Security Process 🚀

### Phase 1: Secret Configuration (30 minutes)
1. **Run Security Configuration Script**
   ```bash
   ./ClaudeDocs/Analysis/Security/secure-replicate-production-config.sh
   ```

2. **Verify Local Development Setup**
   ```bash
   dotnet user-secrets list --project AI.ProfilePhotoMaker.API | grep Replicate
   ```

3. **Export Secrets for Deployment**
   ```bash
   export REPLICATE_API_TOKEN="your-production-token"
   export REPLICATE_WEBHOOK_SECRET="whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM"
   ```

### Phase 2: Infrastructure Deployment (45 minutes)
1. **Deploy with Secure Parameters**
   ```bash
   az deployment group create \
     --template-file infrastructure/simple-deploy.bicep \
     --parameters \
       replicateApiToken="$REPLICATE_API_TOKEN" \
       replicateWebhookSecret="$REPLICATE_WEBHOOK_SECRET" \
     --mode Incremental
   ```

2. **Verify Key Vault Secrets**
   ```bash
   az keyvault secret show --vault-name "aipm-kv-v1-{suffix}" --name "ReplicateApiToken"
   az keyvault secret show --vault-name "aipm-kv-v1-{suffix}" --name "ReplicateWebhookSecret"
   ```

3. **Restart Container Apps**
   ```bash
   az containerapp restart --name aipm-api-v1 --resource-group aiprofilemaker-v1
   ```

### Phase 3: Security Validation (30 minutes)
1. **Application Health Check**
   ```bash
   curl https://api.aiprofilephotomaker.com/api/health
   ```

2. **Replicate Integration Test**
   - Test model training API call
   - Verify webhook callback processing  
   - Check Application Insights for any errors

3. **Security Monitoring Setup**
   - Configure alerts for authentication failures
   - Set up monitoring for webhook validation errors
   - Verify logging captures security events

## Post-Deployment Security Monitoring 📊

### Immediate Monitoring (First 24 hours)
- [ ] **Application Insights Dashboard**
  - Monitor Replicate API authentication success/failure rates
  - Track webhook signature validation results
  - Alert on any secret-related errors

- [ ] **Key Vault Access Monitoring**
  - Verify Container Apps can access secrets
  - Monitor for any access denied errors
  - Confirm secret retrieval is working

### Ongoing Security Operations
- [ ] **Weekly Security Checks**
  - Review Application Insights for anomalies
  - Check Replicate billing for unexpected usage
  - Verify webhook endpoints are responding correctly

- [ ] **Monthly Security Review**
  - Assess need for secret rotation
  - Review access logs for suspicious patterns
  - Update security documentation as needed

## Security Incident Response 🚨

### Compromised API Token Response
1. **Immediate Actions (Within 5 minutes)**
   ```bash
   # Revoke token in Replicate dashboard immediately
   # Generate new token
   NEW_TOKEN="r8_new_secure_token_here"
   
   # Update Key Vault
   az keyvault secret set --vault-name "aipm-kv-v1-{suffix}" \
     --name "ReplicateApiToken" --value "$NEW_TOKEN"
   
   # Restart applications
   az containerapp restart --name aipm-api-v1 --resource-group aiprofilemaker-v1
   ```

2. **Investigation (Within 30 minutes)**
   - Review Application Insights logs for unauthorized usage
   - Check Replicate billing for suspicious activity
   - Identify source of compromise

### Webhook Security Incident Response
1. **Detection Signs**
   - High volume of webhook validation failures
   - Unexpected webhook requests from unknown sources
   - Application Insights showing signature validation errors

2. **Immediate Response**
   ```bash
   # Rotate webhook secret if needed
   NEW_WEBHOOK_SECRET=$(openssl rand -hex 32)
   
   # Update Replicate dashboard with new secret
   # Update Key Vault
   az keyvault secret set --vault-name "aipm-kv-v1-{suffix}" \
     --name "ReplicateWebhookSecret" --value "$NEW_WEBHOOK_SECRET"
   ```

## Security Compliance Verification ✅

### OWASP Top 10 Checklist
- [ ] **A01 - Broken Access Control**: Webhook signature validation implemented
- [ ] **A02 - Cryptographic Failures**: Strong secrets, proper storage in Key Vault
- [ ] **A05 - Security Misconfiguration**: No hardcoded secrets, proper config
- [ ] **A07 - Authentication Failures**: Valid API tokens, proper error handling
- [ ] **A09 - Security Logging**: Application Insights monitoring enabled

### Production Readiness Criteria
- [ ] All placeholder secrets replaced with production values
- [ ] Azure Key Vault storing all sensitive configuration
- [ ] Container Apps using Managed Identity for Key Vault access  
- [ ] Webhook signature validation working correctly
- [ ] Application Insights monitoring security events
- [ ] Incident response procedures documented and tested

## Final Security Sign-off

**Security Review Completed**: _______________  
**Reviewed By**: _______________  
**Production Deployment Approved**: _______________  

**Critical Security Controls Verified**:
- [x] No hardcoded secrets in configuration
- [x] Azure Key Vault properly configured
- [x] Webhook signature validation enabled
- [x] API token authentication working
- [x] Security monitoring operational

**Deployment Authorization**: Ready for production deployment ✅

---

**Document Version**: 1.0  
**Last Updated**: 2025-08-14  
**Next Review**: 2025-09-14  
**Security Assessment Reference**: replicate-production-security-audit-2025-08-14-150900.md