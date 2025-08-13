# OAuth Deployment Checklist

## Immediate Action Required

### Step 1: Fix Production OAuth (CRITICAL - Do this NOW)
```bash
./scripts/fix-oauth-production.sh
```
This will:
- Prompt for Google OAuth credentials
- Update the production Container App
- Validate the OAuth endpoint

### Step 2: Verify OAuth is Working
```bash
# Test OAuth endpoint (should return 302 redirect)
curl -I https://api.aiprofilephotomaker.com/api/auth/external-login/google

# Check application logs for errors
az monitor log-analytics query \
  --workspace aipm-logs-v1 \
  --analytics-query "ContainerAppConsoleLogs_CL | where ContainerAppName_s == 'aipm-api-v1' | where Message contains 'OAuth' or Message contains 'Google' | top 20 by TimeGenerated desc"
```

## Long-term Solution (Complete after immediate fix)

### Step 3: Create Deployment Parameters
```bash
# Copy template
cp deployment-params.template.json deployment-params.json

# Edit with your actual values
nano deployment-params.json
```

Required values:
- `sqlAdminPassword`: Your SQL admin password
- `jwtSecret`: Your JWT secret (32+ characters)
- `replicateApiToken`: Your Replicate API token
- `googleClientId`: Your Google OAuth Client ID
- `googleClientSecret`: Your Google OAuth Client Secret

### Step 4: Deploy with OAuth Configuration
```bash
# Use the new deployment script
./scripts/deploy-with-oauth.sh
```

Or manually:
```bash
# Deploy infrastructure
az deployment group create \
  --resource-group aiprofilemaker-v1 \
  --template-file infrastructure/simple-deploy.bicep \
  --parameters @deployment-params.json
```

## Validation Checklist

### ✅ OAuth Configuration
- [ ] `GOOGLE_CLIENT_ID` environment variable set
- [ ] `GOOGLE_CLIENT_SECRET` environment variable set
- [ ] OAuth endpoint returns 302 redirect (not 500)
- [ ] Google login flow completes successfully

### ✅ Infrastructure as Code
- [ ] Bicep template includes OAuth parameters
- [ ] OAuth secrets configured in Container App
- [ ] Key Vault stores OAuth credentials
- [ ] No manual configuration required

### ✅ Security
- [ ] deployment-params.json is in .gitignore
- [ ] No secrets committed to repository
- [ ] OAuth secrets stored as Container App secrets
- [ ] HTTPS-only for all OAuth redirects

### ✅ Monitoring
- [ ] No OAuth errors in application logs
- [ ] Authentication success rate > 95%
- [ ] OAuth endpoint response time < 500ms

## Quick Commands Reference

```bash
# Fix OAuth immediately
./scripts/fix-oauth-production.sh

# Deploy with OAuth
./scripts/deploy-with-oauth.sh

# Test OAuth endpoint
curl -I https://api.aiprofilephotomaker.com/api/auth/external-login/google

# View OAuth logs
az monitor log-analytics query \
  --workspace aipm-logs-v1 \
  --analytics-query "ContainerAppConsoleLogs_CL | where ContainerAppName_s == 'aipm-api-v1' | where Message contains 'OAuth'"

# Update Container App with OAuth (manual)
az containerapp update \
  --name aipm-api-v1 \
  --resource-group aiprofilemaker-v1 \
  --set-env-vars \
  GOOGLE_CLIENT_ID="your-client-id" \
  GOOGLE_CLIENT_SECRET="your-client-secret"
```

## Files Modified

### Infrastructure
- ✅ `/infrastructure/simple-deploy.bicep` - Added OAuth parameters and configuration
- ✅ `/deployment-params.template.json` - Template for deployment parameters
- ❌ `/infrastructure/simple-deploy-oauth-fix.bicep` - Removed (temporary file)

### Scripts
- ✅ `/scripts/fix-oauth-production.sh` - Immediate OAuth fix script
- ✅ `/scripts/deploy-with-oauth.sh` - Complete deployment with OAuth

### Documentation
- ✅ `/ClaudeDocs/Design/Architecture/oauth-deployment-architecture-2025-08-12-145500.md` - Architecture analysis
- ✅ `/DEPLOYMENT_CHECKLIST.md` - This checklist

### Configuration
- ✅ `.gitignore` - Added deployment-params.json patterns

## Next Steps

1. **Immediate**: Run `./scripts/fix-oauth-production.sh` to fix production
2. **Today**: Create `deployment-params.json` with actual values
3. **This Week**: Test full deployment with `./scripts/deploy-with-oauth.sh`
4. **Future**: Consider Azure Key Vault integration for enterprise-grade secret management

## Success Criteria

The deployment is successful when:
- ✅ OAuth login works without 500 errors
- ✅ All future deployments include OAuth configuration automatically
- ✅ No manual Azure Portal/CLI configuration required
- ✅ Infrastructure is fully reproducible from code