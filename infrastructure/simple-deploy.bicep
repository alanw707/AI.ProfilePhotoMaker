// Simple AI Profile Photo Maker Infrastructure
// Perfect for single developer deployment

param appName string = 'aipm'
param environment string = 'v1'
param location string = resourceGroup().location

// Image tag to deploy (e.g., timestamp). Defaults to 'latest' for local/dev.
param imageTag string = 'latest'

@description('Managed certificate resource ID for the frontend custom domain')
param frontendCertificateId string = '/subscriptions/7e5147a4-3abb-4a43-aef7-5a2ae770c739/resourceGroups/aiprofilemaker-v1/providers/Microsoft.App/managedEnvironments/aipm-env-v1-6j74jubocuukg/managedCertificates/aiprofilephotomaker.com-aipm-env-260106133524'

@description('Managed certificate resource ID for the backend custom domain')
param backendCertificateId string = '/subscriptions/7e5147a4-3abb-4a43-aef7-5a2ae770c739/resourceGroups/aiprofilemaker-v1/providers/Microsoft.App/managedEnvironments/aipm-env-v1-6j74jubocuukg/managedCertificates/mc-aiprofilemaker-api-aiprofilepho-8101'

// Core secrets
@secure()
param sqlAdminPassword string
@secure()
param jwtSecret string
@secure()
param replicateApiToken string

// OAuth Configuration
@secure()
@description('Google OAuth Client ID for authentication')
param googleClientId string

@secure()
@description('Google OAuth Client Secret for authentication')
param googleClientSecret string

@secure()
@description('Replicate webhook secret for signature validation')
param replicateWebhookSecret string

// Stripe Payment Configuration
@secure()
@description('Stripe secret key for payment processing')
param stripeSecretKey string

@secure()
@description('Stripe publishable key for frontend payment processing')
param stripePublishableKey string

@secure()
@description('Stripe webhook secret for payment event validation')
param stripeWebhookSecret string

@secure()
@description('OpenAI API key for DALL-E 3 image generation')
param openAiApiKey string

@secure()
@description('Postmark server token for transactional email')
param postmarkServerToken string

@secure()
@description('Cloudflare Turnstile secret key for bot verification')
param turnstileSecretKey string

// Generate unique names
var uniqueSuffix = uniqueString(resourceGroup().id)

var containerRegistryName = '${appName}cr${environment}${uniqueSuffix}'
var sqlServerName = '${appName}-sql-${environment}-${uniqueSuffix}'
var storageAccountName = '${appName}st${environment}${uniqueSuffix}'
var keyVaultName = '${appName}-kv-${environment}-${uniqueSuffix}'
var containerEnvName = '${appName}-env-${environment}-${uniqueSuffix}'
var backendAppName = '${appName}-api-${environment}'
var frontendAppName = '${appName}-web-${environment}'
var applicationInsightsName = '${appName}-ai-${environment}'

// Container Registry
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: containerRegistryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

// SQL Database
resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: 'sqladmin'
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: '${appName}db'
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    maxSizeBytes: 2147483648 // 2GB
  }
}

resource sqlFirewallRule 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    // Configure CORS so the frontend can fetch images directly from Blob Storage
    cors: {
      corsRules: [
        {
          // Production and app domains
          allowedOrigins: [
            'https://aiprofilephotomaker.com'
            'https://aiprofilephotomaker.com'
            // Container Apps default FQDNs (resolved at deploy time)
            'https://${frontendApp.properties.configuration.ingress.fqdn}'
            'https://${backendApp.properties.configuration.ingress.fqdn}'
            // Local development convenience
            'http://localhost:4200'
            'http://localhost:5173'
          ]
          // Allow typical read methods from the browser
          allowedMethods: [ 'GET', 'HEAD', 'OPTIONS' ]
          // Permit all request headers and expose common response headers
          allowedHeaders: [ '*' ]
          exposedHeaders: [ 'Content-Length', 'Content-Type', 'Last-Modified', 'ETag', 'x-ms-*' ]
          maxAgeInSeconds: 3600
        }
      ]
    }
  }
}

resource profileImagesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'profile-images'
  properties: {
    publicAccess: 'Blob'
  }
}

// Log Analytics Workspace (required for Container Apps)
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${appName}-logs-${environment}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Application Insights
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

// Store secrets in Key Vault
resource jwtSecretKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'JwtSecret'
  properties: {
    value: jwtSecret
  }
}

resource replicateTokenKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'ReplicateApiToken'
  properties: {
    value: replicateApiToken
  }
}

resource replicateWebhookSecretKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'ReplicateWebhookSecret'
  properties: {
    value: replicateWebhookSecret
  }
}

resource connectionStringKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'ConnectionString'
  properties: {
    value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};User ID=sqladmin;Password=${sqlAdminPassword};Encrypt=True;'
  }
}

// OAuth secrets in Key Vault
resource googleClientIdKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'GoogleClientId'
  properties: {
    value: googleClientId
  }
}

resource googleClientSecretKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'GoogleClientSecret'
  properties: {
    value: googleClientSecret
  }
}

resource stripeSecretKeyKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'StripeSecretKey'
  properties: {
    value: stripeSecretKey
  }
}

resource stripePublishableKeyKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'StripePublishableKey'
  properties: {
    value: stripePublishableKey
  }
}

resource stripeWebhookSecretKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'StripeWebhookSecret'
  properties: {
    value: stripeWebhookSecret
  }
}

resource openAiApiKeyKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'OpenAiApiKey'
  properties: {
    value: openAiApiKey
  }
}

// Container Apps Environment
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'  
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

// Backend API Container App
resource backendUserIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${appName}-api-identity-${environment}-${uniqueSuffix}'
  location: location
}

resource backendApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: backendAppName
  location: location
  identity: {
    type: 'SystemAssigned,UserAssigned'
    userAssignedIdentities: {
      '${backendUserIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
        customDomains: [
          {
            name: 'api.aiprofilephotomaker.com'
            certificateId: backendCertificateId
            bindingType: 'SniEnabled'
          }
        ]
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          username: containerRegistry.name
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name: 'jwt-secret'
          value: jwtSecret
        }
        {
          name: 'replicate-token'
          value: replicateApiToken
        }
        {
          name: 'connection-string'
          value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};User ID=sqladmin;Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
        {
          name: 'acr-password'
          value: containerRegistry.listCredentials().passwords[0].value
        }
        {
          name: 'google-client-id'
          value: googleClientId
        }
        {
          name: 'google-client-secret'
          value: googleClientSecret
        }
        {
          name: 'replicate-webhook-secret'
          value: replicateWebhookSecret
        }
        {
          name: 'stripe-secret-key'
          value: stripeSecretKey
        }
        {
          name: 'stripe-publishable-key'
          value: stripePublishableKey
        }
        {
          name: 'stripe-webhook-secret'
          value: stripeWebhookSecret
        }
        {
          name: 'openai-api-key'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/OpenAiApiKey'
          identity: backendUserIdentity.id
        }
        {
          name: 'postmark-server-token'
          value: postmarkServerToken
        }
        {
          name: 'turnstile-secret-key'
          value: turnstileSecretKey
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          // References locally built and pushed image with parameterized tag
          image: '${containerRegistry.properties.loginServer}/aiprofilemaker-api:${imageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            // Replicate owner is constant across environments to prevent drift
            {
              name: 'Replicate__Owner'
              value: 'alanw707'
            }
            // Map both ASP.NET Core config pattern and plain env var to the same secret
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'connection-string'
            }
            {
              name: 'Jwt__Secret'
              secretRef: 'jwt-secret'
            }
            {
              name: 'Replicate__ApiToken'
              secretRef: 'replicate-token'
            }
            {
              name: 'Replicate__WebhookSecret'
              secretRef: 'replicate-webhook-secret'
            }
            {
              name: 'REPLICATE_API_TOKEN'
              secretRef: 'replicate-token'
            }
            {
              name: 'REPLICATE_WEBHOOK_SECRET'
              secretRef: 'replicate-webhook-secret'
            }
            {
              name: 'Replicate__FluxTrainingModelId'
              value: 'replicate/fast-flux-trainer:f463fbfc97389e10a2f443a8a84b6953b1058eafbf0c9af4d84457ff07cb04db'
            }
            {
              name: 'Replicate__FluxGenerationModelId'
              value: 'black-forest-labs/flux-dev'
            }
            {
              name: 'Replicate__FluxKontextProModelId'
              value: 'black-forest-labs/flux-kontext-pro'
            }
            {
              name: 'ConnectionStrings__AzureStorage'
              value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
            }
            {
              name: 'AzureStorage__ConnectionString'
              value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
            }
            {
              name: 'AzureStorage__ContainerName'
              value: 'profile-images'
            }
            // Plain env vars for EnvironmentConfiguration production checks
            {
              name: 'AZURE_STORAGE_CONNECTION_STRING'
              value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
            }
            {
              name: 'AZURE_STORAGE_CONTAINER_NAME'
              value: 'profile-images'
            }
            {
              name: 'ApplicationInsights__ConnectionString'
              value: applicationInsights.properties.ConnectionString
            }
            {
              name: 'Database__AutoMigrateOnStartup'
              value: 'false'
            }
            {
              name: 'Database__ValidateOnStartup'
              value: 'false'
            }
            {
              name: 'CORS_ALLOWED_ORIGINS'
              value: 'https://aiprofilephotomaker.com,https://aiprofilephotomaker.com'
            }
            // Application URLs for webhook resolution
            {
              name: 'ExternalApiBaseUrl'
              value: 'https://api.aiprofilephotomaker.com'
            }
            {
              name: 'AppBaseUrl'
              value: 'https://aiprofilephotomaker.com'
            }
            // OAuth Configuration
            {
              name: 'GOOGLE_CLIENT_ID'
              secretRef: 'google-client-id'
            }
            {
              name: 'GOOGLE_CLIENT_SECRET'
              secretRef: 'google-client-secret'
            }
            // Alternative naming for OAuth (for compatibility)
            {
              name: 'Authentication__Google__ClientId'
              secretRef: 'google-client-id'
            }
            {
              name: 'Authentication__Google__ClientSecret'
              secretRef: 'google-client-secret'
            }
            // Stripe Payment Configuration
            {
              name: 'STRIPE_SECRET_KEY'
              secretRef: 'stripe-secret-key'
            }
            {
              name: 'STRIPE_PUBLISHABLE_KEY'
              secretRef: 'stripe-publishable-key'
            }
            {
              name: 'STRIPE_WEBHOOK_SECRET'
              secretRef: 'stripe-webhook-secret'
            }
            // Email / notifications (Postmark API)
            {
              name: 'Email__Username'
              value: ''
            }
            {
              name: 'Email__Password'
              value: ''
            }
            {
              name: 'Email__FromEmail'
              value: 'no-reply@aiprofilephotomaker.com'
            }
            {
              name: 'Email__FromName'
              value: 'AI Profile Photo Maker'
            }
            {
              name: 'Email__SandboxMode'
              value: 'false'
            }
            {
              name: 'Email__FrontendBaseUrl'
              value: 'https://aiprofilephotomaker.com'
            }
            {
              name: 'Email__UseApi'
              value: 'true'
            }
            {
              name: 'Email__PostmarkServerToken'
              secretRef: 'postmark-server-token'
            }

            // Cloudflare Turnstile bot verification (optional, but recommended for production).
            // Provide both env var names for compatibility with current API configuration.
            {
              name: 'TURNSTILE_SECRET_KEY'
              secretRef: 'turnstile-secret-key'
            }
            {
              name: 'Turnstile__SecretKey'
              secretRef: 'turnstile-secret-key'
            }
            
            // OpenAI Configuration
            {
              name: 'OPENAI_API_KEY'
              secretRef: 'openai-api-key'
            }
            {
              name: 'OpenAI__ApiKey'
              secretRef: 'openai-api-key'
            }
            // Alternative naming for Stripe (for compatibility)
            {
              name: 'Stripe__SecretKey'
              secretRef: 'stripe-secret-key'
            }
            {
              name: 'Stripe__PublishableKey'
              secretRef: 'stripe-publishable-key'
            }
            {
              name: 'Stripe__WebhookSecret'
              secretRef: 'stripe-webhook-secret'
            }
          ]
          // Health probes re-enabled since we're using real application images
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/api/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 15
              periodSeconds: 10
              timeoutSeconds: 10
              failureThreshold: 3
              successThreshold: 1
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/api/health/ready'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 10
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 5
              successThreshold: 1
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}

resource backendKeyVaultSecretsAccess 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(keyVault.id, backendApp.name, 'kv-secrets-user')
  scope: keyVault
  properties: {
    principalId: backendUserIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
  }
}

// Frontend Container App
resource frontendApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: frontendAppName
  location: location
  dependsOn: [
    backendApp  // Deploy after backend to avoid circular dependency during updates
  ]
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 80
        allowInsecure: false
        customDomains: [
          {
            name: 'aiprofilephotomaker.com'
            certificateId: frontendCertificateId
            bindingType: 'SniEnabled'
          }
        ]
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          username: containerRegistry.name
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name: 'acr-password'
          value: containerRegistry.listCredentials().passwords[0].value
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          // References locally built and pushed image with parameterized tag
          image: '${containerRegistry.properties.loginServer}/aiprofilemaker-web:${imageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'API_URL'
              value: 'https://api.aiprofilephotomaker.com'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 2
      }
    }
  }
}

// Note: Using Container Registry admin credentials for simplicity
// In production, consider using managed identity with proper role assignments

// Outputs
output frontendUrl string = 'https://${frontendApp.properties.configuration.ingress.fqdn}'
output backendUrl string = 'https://${backendApp.properties.configuration.ingress.fqdn}'
output containerRegistryName string = containerRegistry.name
output containerRegistryLoginServer string = containerRegistry.properties.loginServer
output sqlServerName string = sqlServer.name
output storageAccountName string = storageAccount.name
output keyVaultName string = keyVault.name
output oauthConfigured string = 'OAuth configured with Google Client ID and Secret'
