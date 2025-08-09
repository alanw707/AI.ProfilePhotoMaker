// =================================================================
// Azure Bicep Template for Environment Variable Configuration
// =================================================================
// This template configures App Service with secure environment variables
// Use with Azure Key Vault for production secrets
// =================================================================

@description('Environment name (dev, test, prod)')
param environment string = 'dev'

@description('Application name')
param appName string = 'aipm'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Key Vault name for storing secrets')
param keyVaultName string

@description('Managed Identity ID for Key Vault access')
param managedIdentityId string

// =================================================================
// VARIABLES
// =================================================================

var environmentConfig = {
  dev: {
    appServicePlanSku: 'F1'
    databaseSku: 'Basic'
    logLevel: 'Information'
    enableDetailedErrors: 'true'
    autoMigrate: 'true'
  }
  test: {
    appServicePlanSku: 'S1'
    databaseSku: 'S0'
    logLevel: 'Information'
    enableDetailedErrors: 'false'
    autoMigrate: 'false'
  }
  prod: {
    appServicePlanSku: 'P1v2'
    databaseSku: 'S2'
    logLevel: 'Warning'
    enableDetailedErrors: 'false'
    autoMigrate: 'false'
  }
}

var config = environmentConfig[environment]

// =================================================================
// RESOURCES
// =================================================================

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${appName}-plan-${environment}'
  location: location
  sku: {
    name: config.appServicePlanSku
  }
  properties: {
    reserved: true
  }
  kind: 'linux'
}

// App Service
resource appService 'Microsoft.Web/sites@2022-03-01' = {
  name: '${appName}-api-${environment}'
  location: location
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: environment != 'dev'
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      http20Enabled: true
      
      // Health check configuration
      healthCheckPath: '/api/health'
      
      // Application settings (environment variables)
      appSettings: [
        // =================================================================
        // REQUIRED ENVIRONMENT VARIABLES
        // =================================================================
        
        // Environment Configuration
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environment == 'prod' ? 'Production' : (environment == 'test' ? 'Test' : 'Development')
        }
        
        // Database Configuration (from Key Vault)
        {
          name: 'MSSQL_SA_PASSWORD'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/mssql-sa-password/)'
        }
        
        // JWT Configuration (from Key Vault)
        {
          name: 'JWT_SECRET'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/jwt-secret/)'
        }
        {
          name: 'JWT_VALID_AUDIENCE'
          value: environment == 'prod' ? 'https://aiprofilephotomaker.com' : 
                 environment == 'test' ? 'https://test.profilephotomaker.com' :
                 'http://localhost:4200'
        }
        {
          name: 'JWT_VALID_ISSUER'
          value: environment == 'prod' ? 'https://api.aiprofilephotomaker.com' : 
                 environment == 'test' ? 'https://test-api.profilephotomaker.com' :
                 'http://localhost:5032'
        }
        
        // AI/ML Services (from Key Vault)
        {
          name: 'REPLICATE_API_TOKEN'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/replicate-api-token/)'
        }
        {
          name: 'REPLICATE_WEBHOOK_SECRET'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/replicate-webhook-secret/)'
        }
        
        // =================================================================
        // OPTIONAL THIRD-PARTY INTEGRATIONS
        // =================================================================
        
        // Google OAuth (from Key Vault)
        {
          name: 'GOOGLE_CLIENT_ID'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/google-client-id/)'
        }
        {
          name: 'GOOGLE_CLIENT_SECRET'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/google-client-secret/)'
        }
        
        // Stripe Payment (from Key Vault)
        {
          name: 'STRIPE_PUBLISHABLE_KEY'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/stripe-publishable-key/)'
        }
        {
          name: 'STRIPE_SECRET_KEY'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/stripe-secret-key/)'
        }
        {
          name: 'STRIPE_WEBHOOK_SECRET'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/stripe-webhook-secret/)'
        }
        
        // Azure Storage (from Key Vault)
        {
          name: 'AZURE_STORAGE_CONNECTION_STRING'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/azure-storage-connection/)'
        }
        {
          name: 'AZURE_STORAGE_CONTAINER_NAME'
          value: 'profile-images'
        }
        
        // =================================================================
        // APPLICATION CONFIGURATION
        // =================================================================
        
        // Application URLs
        {
          name: 'APP_BASE_URL'
          value: environment == 'prod' ? 'https://aiprofilephotomaker.com' : 
                 environment == 'test' ? 'https://test.profilephotomaker.com' :
                 'http://localhost:4200'
        }
        
        // Payment Configuration
        {
          name: 'PAYMENT_SIMULATION_ENABLED'
          value: environment == 'prod' ? 'false' : 'true'
        }
        {
          name: 'PAYMENT_SKIP_STRIPE_INTEGRATION'
          value: environment == 'dev' ? 'true' : 'false'
        }
        
        // Database Settings
        {
          name: 'DB_MAX_RETRY_COUNT'
          value: environment == 'prod' ? '5' : '3'
        }
        {
          name: 'DB_MAX_RETRY_DELAY_SECONDS'
          value: environment == 'prod' ? '30' : '10'
        }
        {
          name: 'DB_COMMAND_TIMEOUT_SECONDS'
          value: environment == 'prod' ? '60' : '30'
        }
        {
          name: 'DB_ENABLE_SENSITIVE_LOGGING'
          value: 'false'
        }
        {
          name: 'DB_ENABLE_DETAILED_ERRORS'
          value: config.enableDetailedErrors
        }
        {
          name: 'DB_AUTO_MIGRATE_ON_STARTUP'
          value: config.autoMigrate
        }
        {
          name: 'DB_VALIDATE_ON_STARTUP'
          value: 'true'
        }
        
        // Security Settings
        {
          name: 'ENABLE_HTTPS_REDIRECT'
          value: environment != 'dev' ? 'true' : 'false'
        }
        {
          name: 'REQUIRE_HTTPS_METADATA'
          value: environment != 'dev' ? 'true' : 'false'
        }
        {
          name: 'CORS_ALLOWED_ORIGINS'
          value: environment == 'prod' ? 'https://aiprofilephotomaker.com' : 
                 environment == 'test' ? 'https://test.profilephotomaker.com' :
                 'http://localhost:4200,https://localhost:4200'
        }
        
        // Performance Settings
        {
          name: 'ENABLE_RESPONSE_COMPRESSION'
          value: 'true'
        }
        {
          name: 'ENABLE_REQUEST_LOGGING'
          value: environment == 'prod' ? 'false' : 'true'
        }
        
        // Logging Configuration
        {
          name: 'LOG_LEVEL_DEFAULT'
          value: config.logLevel
        }
        {
          name: 'LOG_LEVEL_ASPNETCORE'
          value: environment == 'prod' ? 'Error' : 'Warning'
        }
        {
          name: 'LOG_LEVEL_DATABASE'
          value: environment == 'dev' ? 'Debug' : 'Information'
        }
        
        // Health Check Configuration
        {
          name: 'HEALTH_CHECK_TIMEOUT_SECONDS'
          value: environment == 'prod' ? '60' : '30'
        }
        {
          name: 'HEALTH_CHECK_INTERVAL_SECONDS'
          value: environment == 'prod' ? '30' : '60'
        }
        
        // Background Service Intervals
        {
          name: 'RETENTION_POLICY_INTERVAL_HOURS'
          value: '24'
        }
        {
          name: 'MODEL_EXPIRATION_CHECK_INTERVAL_HOURS'
          value: environment == 'prod' ? '4' : '6'
        }
        {
          name: 'MODEL_POLLING_INTERVAL_SECONDS'
          value: '30'
        }
        
        // =================================================================
        // AZURE APP SERVICE SPECIFIC
        // =================================================================
        
        // App Service Settings
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'WEBSITE_HEALTHCHECK_MAXPINGFAILURES'
          value: '3'
        }
        {
          name: 'WEBSITE_HEALTHCHECK_MAXUNHEALTHYWORKERPERCENT'
          value: '50'
        }
        
        // Application Insights (if enabled)
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/app-insights-connection/)'
        }
      ]
      
      // Connection strings
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}.vault.azure.net/secrets/database-connection-string/)'
          type: 'SQLServer'
        }
      ]
    }
    
    httpsOnly: environment != 'dev'
  }
}

// =================================================================
// KEY VAULT ACCESS POLICY
// =================================================================

resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: keyVaultName
}

// Grant App Service access to Key Vault
resource keyVaultAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2022-07-01' = {
  name: 'add'
  parent: keyVault
  properties: {
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: appService.identity.principalId
        permissions: {
          secrets: [
            'get'
          ]
        }
      }
    ]
  }
}

// =================================================================
// OUTPUTS
// =================================================================

output appServiceName string = appService.name
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output appServicePrincipalId string = appService.identity.principalId

// =================================================================
// DEPLOYMENT NOTES
// =================================================================

/*
DEPLOYMENT INSTRUCTIONS:

1. Create Key Vault and store secrets:
   az keyvault secret set --vault-name mykeyvault --name "mssql-sa-password" --value "YourSecurePassword"
   az keyvault secret set --vault-name mykeyvault --name "jwt-secret" --value "YourJWTSecret"
   [... other secrets ...]

2. Deploy this template:
   az deployment group create \
     --resource-group myresourcegroup \
     --template-file azure-env-config.bicep \
     --parameters environment=prod keyVaultName=mykeyvault

3. Configure CI/CD to deploy application code to the App Service

4. Monitor application logs for environment validation results
*/