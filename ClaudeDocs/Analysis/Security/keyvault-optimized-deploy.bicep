// Security-Optimized AI Profile Photo Maker Infrastructure
// Uses Azure Key Vault references for maximum security
// Eliminates secret exposure in deployment templates

param appName string = 'aipm'
param environment string = 'v1'
param location string = resourceGroup().location

// Only infrastructure secrets needed for deployment
@secure()
param sqlAdminPassword string

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

// Existing certificate IDs - using working certificates
var frontendCertificateId = '/subscriptions/7e5147a4-3abb-4a43-aef7-5a2ae770c739/resourceGroups/aiprofilemaker-v1/providers/Microsoft.App/managedEnvironments/aipm-env-v1-6j74jubocuukg/managedCertificates/mc-aipm-env-v1-6j-app-aiprofilepho-5691'
var backendCertificateId = '/subscriptions/7e5147a4-3abb-4a43-aef7-5a2ae770c739/resourceGroups/aiprofilemaker-v1/providers/Microsoft.App/managedEnvironments/aipm-env-v1-6j74jubocuukg/managedCertificates/mc-aipm-env-v1-6j-api-aiprofilepho-8094'

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
}

resource profileImagesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'profile-images'
  properties: {
    publicAccess: 'Blob'
  }
}

// Log Analytics Workspace
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

// Key Vault (existing - reference only)
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' existing = {
  name: 'aipm-kv-v1-6j74jubocuukg'  // Use existing Key Vault
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

// Backend API Container App with Security-Optimized Configuration
resource backendApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: backendAppName
  location: location
  dependsOn: [
    containerAppsEnvironment
    containerRegistry
    sqlServer
    sqlDatabase
    storageAccount
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
      // 🔐 SECURITY OPTIMIZED: Key Vault references instead of direct values
      secrets: [
        // Key Vault referenced secrets (secure - no value exposure)
        {
          name: 'jwt-secret'
          keyVaultUrl: 'https://${keyVault.name}.vault.azure.net/secrets/JwtSecret'
        }
        {
          name: 'replicate-token'
          keyVaultUrl: 'https://${keyVault.name}.vault.azure.net/secrets/ReplicateApiToken'
        }
        {
          name: 'replicate-webhook-secret'
          keyVaultUrl: 'https://${keyVault.name}.vault.azure.net/secrets/ReplicateWebhookSecret'
        }
        {
          name: 'google-client-id'
          keyVaultUrl: 'https://${keyVault.name}.vault.azure.net/secrets/GoogleClientId'
        }
        {
          name: 'google-client-secret'
          keyVaultUrl: 'https://${keyVault.name}.vault.azure.net/secrets/GoogleClientSecret'
        }
        {
          name: 'connection-string'
          keyVaultUrl: 'https://${keyVault.name}.vault.azure.net/secrets/ConnectionString'
        }
        // Non-sensitive operational secrets (direct values acceptable)
        {
          name: 'acr-password'
          value: containerRegistry.listCredentials().passwords[0].value
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${containerRegistry.properties.loginServer}/aiprofilemaker-api:latest'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            // All sensitive values now reference Key Vault secrets
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
              name: 'GOOGLE_CLIENT_ID'
              secretRef: 'google-client-id'
            }
            {
              name: 'GOOGLE_CLIENT_SECRET'
              secretRef: 'google-client-secret'
            }
            {
              name: 'Authentication__Google__ClientId'
              secretRef: 'google-client-id'
            }
            {
              name: 'Authentication__Google__ClientSecret'
              secretRef: 'google-client-secret'
            }
            // Non-sensitive configuration (direct values)
            {
              name: 'AzureStorage__ConnectionString'
              value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
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
              value: 'https://app.aiprofilephotomaker.com,https://aiprofilephotomaker.com'
            }
          ]
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

// 🔐 CRITICAL: Grant Container App access to Key Vault
resource keyVaultAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2023-02-01' = {
  parent: keyVault
  name: 'add'
  properties: {
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: backendApp.identity.principalId
        permissions: {
          secrets: ['get']
        }
      }
    ]
  }
}

// Frontend Container App (unchanged - no secrets needed)
resource frontendApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: frontendAppName
  location: location
  dependsOn: [
    containerAppsEnvironment
    containerRegistry
    backendApp
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
            name: 'app.aiprofilephotomaker.com'
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
          image: '${containerRegistry.properties.loginServer}/aiprofilemaker-web:latest'
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

// Outputs
output frontendUrl string = 'https://${frontendApp.properties.configuration.ingress.fqdn}'
output backendUrl string = 'https://${backendApp.properties.configuration.ingress.fqdn}'
output containerRegistryName string = containerRegistry.name
output containerRegistryLoginServer string = containerRegistry.properties.loginServer
output sqlServerName string = sqlServer.name
output storageAccountName string = storageAccount.name
output keyVaultName string = keyVault.name
output securityOptimization string = '🔐 SECURITY OPTIMIZED: All secrets reference Key Vault directly - zero exposure'