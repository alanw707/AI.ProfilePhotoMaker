// Infrastructure Security Patch for Replicate Webhook Secret
// This patch addresses VULN-001 and VULN-002 identified in security audit
// 
// Security Improvements:
// 1. Add replicateWebhookSecret parameter
// 2. Store webhook secret in Key Vault
// 3. Configure Container Apps environment variable
// 4. Update GitHub Actions workflow reference

// PATCH INSTRUCTIONS:
// Apply these changes to infrastructure/simple-deploy.bicep

// ====================
// 1. ADD TO PARAMETERS SECTION (after line 23)
// ====================

@secure()
@description('Replicate webhook secret for signature validation')
param replicateWebhookSecret string

// ====================
// 2. ADD TO KEY VAULT SECRETS SECTION (after line 194)
// ====================

resource replicateWebhookSecretKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'ReplicateWebhookSecret'
  properties: {
    value: replicateWebhookSecret
  }
}

// ====================
// 3. UPDATE CONTAINER APPS SECRETS SECTION
// Add to secrets array in backend container app (around line 280)
// ====================

{
  name: 'replicate-webhook-secret'
  keyVaultUrl: '${keyVault.properties.vaultUri}secrets/ReplicateWebhookSecret'
  identity: containerAppsManagedIdentity.id
}

// ====================
// 4. UPDATE CONTAINER APPS ENVIRONMENT VARIABLES
// Add to env array in backend container app (after line 301)
// ====================

{
  name: 'Replicate__WebhookSecret'
  secretRef: 'replicate-webhook-secret'
}

// ====================
// 5. UPDATE GITHUB ACTIONS WORKFLOW
// Add to .github/workflows/simple-deploy.yml parameters (around line 191)
// ====================

/*
In .github/workflows/simple-deploy.yml, update the deployment parameters to include:

--parameters sqlAdminPassword="${{ secrets.SQL_ADMIN_PASSWORD }}" \
            jwtSecret="${{ secrets.JWT_SECRET }}" \
            replicateApiToken="${{ secrets.REPLICATE_API_TOKEN }}" \
            replicateWebhookSecret="${{ secrets.REPLICATE_WEBHOOK_SECRET }}" \
            googleClientId="${{ secrets.GOOGLE_CLIENT_ID }}" \
            googleClientSecret="${{ secrets.GOOGLE_CLIENT_SECRET }}"

Also update the validation section (around line 138) with the same parameters.
*/

// ====================
// SECURITY VERIFICATION CHECKLIST
// ====================

/*
After applying this patch:

1. ✅ Verify parameter is marked @secure()
2. ✅ Confirm Key Vault secret is created
3. ✅ Check Container Apps secret reference
4. ✅ Validate environment variable configuration
5. ✅ Update GitHub Actions workflow
6. ✅ Test deployment with new parameter
7. ✅ Verify webhook signature validation works

Security Benefits:
- Webhook endpoints now have proper signature validation
- Secrets are properly stored in Key Vault
- Environment variables are configured securely
- Deployment process includes all required secrets
- Consistent security posture across environments
*/

// ====================
// TESTING COMMANDS
// ====================

/*
# Test Bicep compilation
az bicep build --file infrastructure/simple-deploy.bicep

# Validate deployment (dry run)
az deployment group validate \
  --resource-group "aiprofilemaker-v1" \
  --template-file infrastructure/simple-deploy.bicep \
  --parameters sqlAdminPassword="test" \
              jwtSecret="test" \
              replicateApiToken="test" \
              replicateWebhookSecret="test" \
              googleClientId="test" \
              googleClientSecret="test"

# Deploy with real secrets (when ready)
az deployment group create \
  --resource-group "aiprofilemaker-v1" \
  --template-file infrastructure/simple-deploy.bicep \
  --parameters sqlAdminPassword="${SQL_ADMIN_PASSWORD}" \
              jwtSecret="${JWT_SECRET}" \
              replicateApiToken="${REPLICATE_API_TOKEN}" \
              replicateWebhookSecret="${REPLICATE_WEBHOOK_SECRET}" \
              googleClientId="${GOOGLE_CLIENT_ID}" \
              googleClientSecret="${GOOGLE_CLIENT_SECRET}"
*/