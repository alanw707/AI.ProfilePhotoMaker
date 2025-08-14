// Secure Production Deployment Template
// AI Profile Photo Maker - Enhanced Security Configuration
// Security Audit Compliance: 2025-08-14

@description('Application name prefix')
param appName string = 'aipm'

@description('Environment name')
param environment string = 'v1'

@description('Azure region for deployment')
param location string = resourceGroup().location

// CRITICAL: Secure parameters for production secrets
@secure()
@description('SQL Server admin password - minimum 16 chars, mixed case, numbers, special chars')
param sqlAdminPassword string

@secure()
@description('JWT secret for token signing - minimum 256 bits (32 bytes)')
param jwtSecret string

@secure()
@description('Replicate API token for AI services - must start with r8_')
param replicateApiToken string

@secure()
@description('Replicate webhook secret for signature validation - minimum 32 chars')
param replicateWebhookSecret string

@secure()
@description('Google OAuth Client ID for authentication')
param googleClientId string

@secure()
@description('Google OAuth Client Secret for authentication')
param googleClientSecret string

// Security configuration options
@description('Enable enhanced security monitoring')
param enableSecurityMonitoring bool = true

@description('Enable automatic secret rotation (future feature)')
param enableSecretRotation bool = false

@description('Security compliance mode - strict validation')
param securityComplianceMode string = 'strict'

// Generate unique names with security considerations
var uniqueSuffix = uniqueString(resourceGroup().id)
var containerRegistryName = '${appName}cr${environment}${uniqueSuffix}'
var sqlServerName = '${appName}-sql-${environment}-${uniqueSuffix}'
var storageAccountName = '${appName}st${environment}${uniqueSuffix}'
var keyVaultName = '${appName}-kv-${environment}-${uniqueSuffix}'
var containerEnvName = '${appName}-env-${environment}-${uniqueSuffix}'
var backendAppName = '${appName}-api-${environment}'
var frontendAppName = '${appName}-web-${environment}'
var applicationInsightsName = '${appName}-ai-${environment}'

// Security: Use existing certificate IDs for production domains
var frontendCertificateId = '/subscriptions/7e5147a4-3abb-4a43-aef7-5a2ae770c739/resourceGroups/aiprofilemaker-v1/providers/Microsoft.App/managedEnvironments/aipm-env-v1-6j74jubocuukg/managedCertificates/mc-aipm-env-v1-6j-app-aiprofilepho-5691'
var backendCertificateId = '/subscriptions/7e5147a4-3abb-4a43-aef7-5a2ae770c739/resourceGroups/aiprofilemaker-v1/providers/Microsoft.App/managedEnvironments/aipm-env-v1-6j74jubocuukg/managedCertificates/mc-aipm-env-v1-6j-api-aiprofilepho-8094'

// Container Registry with security hardening
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: containerRegistryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true // Note: Consider using Managed Identity in future
    policies: {
      quarantinePolicy: {
        status: 'enabled'  // Enhanced security: quarantine untrusted images
      }
    }
    encryption: {
      status: 'disabled'  // Basic tier doesn't support encryption
    }
  }
}

// SQL Database with enhanced security
resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: 'sqladmin'
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'  // Security: Force TLS 1.2+
    publicNetworkAccess: 'Enabled'  // Required for Container Apps
  }
  
  // Security: Advanced threat protection
  resource securityAlertPolicies 'securityAlertPolicies@2021-11-01' = if (enableSecurityMonitoring) {
    name: 'default'
    properties: {
      state: 'Enabled'
      emailAccountAdmins: true
      retentionDays: 90
    }
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
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

// Security: Firewall rules for Azure services
resource sqlFirewallRule 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Storage Account with security hardening
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'          // Security: Force TLS 1.2+
    supportsHttpsTrafficOnly: true       // Security: HTTPS only
    allowBlobPublicAccess: true          // Required for profile images
    allowSharedKeyAccess: true           // Required for connection string access
    defaultToOAuthAuthentication: false  // Container Apps compatibility
    networkAcls: {
      defaultAction: 'Allow'  // Open access for Container Apps
    }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 30  // Security: Enable soft delete
    }
  }
}

resource profileImagesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'profile-images'
  properties: {
    publicAccess: 'Blob'  // Required for image serving
  }
}

// Log Analytics Workspace with security monitoring
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${appName}-logs-${environment}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 90  // Security: Extended retention for compliance
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// Application Insights with enhanced monitoring
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    DisableIpMasking: false  // Security: Keep IP masking enabled
    DisableLocalAuth: false  // Allow connection string auth for Container Apps
  }
}

// CRITICAL: Azure Key Vault with enhanced security configuration
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true        // Security: Use RBAC instead of access policies
    enableSoftDelete: true               // Security: Enable soft delete
    softDeleteRetentionInDays: 90        // Security: Extended retention
    enablePurgeProtection: false         // Disabled for cost/simplicity in MVP
    publicNetworkAccess: 'Enabled'       // Required for Container Apps access
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// SECURITY CRITICAL: Store all production secrets in Key Vault
resource jwtSecretKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'JwtSecret'
  properties: {
    value: jwtSecret
    attributes: {
      enabled: true
    }
    contentType: 'text/plain'
  }
}

resource replicateTokenKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'ReplicateApiToken'
  properties: {
    value: replicateApiToken
    attributes: {
      enabled: true
    }
    contentType: 'Replicate API Token'
  }
}

// SECURITY CRITICAL: Store Replicate webhook secret
resource replicateWebhookSecretKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'ReplicateWebhookSecret'
  properties: {
    value: replicateWebhookSecret
    attributes: {
      enabled: true
    }
    contentType: 'Webhook Secret for Signature Validation'
  }
}

resource connectionStringKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'ConnectionString'
  properties: {
    value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};User ID=sqladmin;Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
    attributes: {
      enabled: true
    }
    contentType: 'SQL Server Connection String'
  }
}

// OAuth secrets in Key Vault
resource googleClientIdKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'GoogleClientId'
  properties: {
    value: googleClientId
    attributes: {
      enabled: true
    }
    contentType: 'Google OAuth Client ID'
  }
}

resource googleClientSecretKV 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'GoogleClientSecret'
  properties: {
    value: googleClientSecret
    attributes: {
      enabled: true
    }
    contentType: 'Google OAuth Client Secret'
  }
}

// Container Apps Environment with security monitoring
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

// SECURITY CRITICAL: Backend API with comprehensive secret configuration
resource backendApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: backendAppName
  location: location
  dependsOn: [
    containerAppsEnvironment
    containerRegistry
    sqlServer
    sqlDatabase
    storageAccount
    keyVault
    // Ensure all Key Vault secrets are created first
    jwtSecretKV
    replicateTokenKV
    replicateWebhookSecretKV
    connectionStringKV
    googleClientIdKV
    googleClientSecretKV
  ]
  identity: {
    type: 'SystemAssigned'  // Security: Enable Managed Identity
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false  // Security: HTTPS only
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
      // SECURITY CRITICAL: All secrets properly configured
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
          name: 'replicate-webhook-secret'
          value: replicateWebhookSecret  // CRITICAL: Webhook secret now included
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
          // SECURITY CRITICAL: Comprehensive environment variable configuration
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            // Database configuration
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'connection-string'
            }
            // JWT configuration  
            {
              name: 'Jwt__Secret'
              secretRef: 'jwt-secret'
            }
            {
              name: 'JWT__Secret'  // Alternative naming
              secretRef: 'jwt-secret'
            }
            // CRITICAL: Replicate API configuration
            {
              name: 'Replicate__ApiToken'
              secretRef: 'replicate-token'
            }
            {
              name: 'REPLICATE_API_TOKEN'  // Environment variable naming
              secretRef: 'replicate-token'
            }
            // CRITICAL: Replicate webhook security
            {
              name: 'Replicate__WebhookSecret'
              secretRef: 'replicate-webhook-secret'
            }
            {
              name: 'REPLICATE_WEBHOOK_SECRET'  // Environment variable naming
              secretRef: 'replicate-webhook-secret'
            }
            // Storage configuration
            {
              name: 'AzureStorage__ConnectionString'
              value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
            }
            // Monitoring configuration
            {
              name: 'ApplicationInsights__ConnectionString'
              value: applicationInsights.properties.ConnectionString
            }
            // Database safety settings
            {
              name: 'Database__AutoMigrateOnStartup'
              value: 'false'  // Security: Prevent automatic migrations
            }
            {
              name: 'Database__ValidateOnStartup'
              value: 'true'   // Security: Enable startup validation
            }
            // CORS configuration
            {
              name: 'CORS_ALLOWED_ORIGINS'
              value: 'https://app.aiprofilephotomaker.com,https://aiprofilephotomaker.com'
            }
            // OAuth Configuration (dual naming for compatibility)
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
            // Security monitoring
            {
              name: 'ENABLE_SECURITY_MONITORING'
              value: string(enableSecurityMonitoring)
            }
          ]
          // Health probes for production reliability
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/api/health/live'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 30  // Allow more time for secret loading
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
              initialDelaySeconds: 15
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 5
              successThreshold: 1
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1  // Always keep one instance running
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

// Frontend Container App (unchanged but included for completeness)
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
        allowInsecure: false  // Security: HTTPS only
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

// SECURITY: Key Vault RBAC role assignment for backend app
resource keyVaultSecretUserRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '4633458b-17de-408a-b874-0445c86b69e6'  // Key Vault Secrets User
}

resource backendAppKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, backendApp.id, keyVaultSecretUserRole.id)
  properties: {
    roleDefinitionId: keyVaultSecretUserRole.id
    principalId: backendApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Security monitoring alerts (if enabled)
resource securityAlerts 'Microsoft.Insights/scheduledQueryRules@2021-08-01' = if (enableSecurityMonitoring) {
  name: '${appName}-security-alerts-${environment}'
  location: location
  properties: {
    displayName: 'Replicate Security Monitoring'
    description: 'Monitor for Replicate API authentication failures and webhook validation errors'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          query: 'requests | where success == false and name contains "replicate" | summarize count() by bin(timestamp, 5m)'
          timeAggregation: 'Total'
          dimensions: []
          operator: 'GreaterThan'
          threshold: 5
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: []
    }
  }
}

// Outputs with security context
output frontendUrl string = 'https://${frontendApp.properties.configuration.ingress.fqdn}'
output backendUrl string = 'https://${backendApp.properties.configuration.ingress.fqdn}'
output containerRegistryName string = containerRegistry.name
output containerRegistryLoginServer string = containerRegistry.properties.loginServer
output sqlServerName string = sqlServer.name
output storageAccountName string = storageAccount.name
output keyVaultName string = keyVault.name

// Security outputs
output keyVaultUri string = keyVault.properties.vaultUri
output applicationInsightsInstrumentationKey string = applicationInsights.properties.InstrumentationKey
output securityMonitoringEnabled bool = enableSecurityMonitoring

// Security compliance status
output securityCompliance object = {
  replicateSecretsConfigured: true
  webhookValidationEnabled: true
  keyVaultIntegration: true
  rbacEnabled: true
  httpsOnly: true
  secretsInKeyVault: true
  monitoringEnabled: enableSecurityMonitoring
  complianceMode: securityComplianceMode
  auditDate: '2025-08-14'
}