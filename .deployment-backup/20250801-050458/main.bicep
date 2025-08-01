// Staging-Only Bicep template for AI Profile Photo Maker Azure infrastructure
@description('The environment is fixed to staging for cost optimization')
param environmentName string = 'staging'

@description('The Azure region for resource deployment')
param location string = resourceGroup().location

@description('The base name prefix for staging resources')
param namePrefix string = 'aiprofilephotomaker-staging'

@description('Generate unique resource names to prevent conflicts')
var uniqueStagingSuffix = uniqueString(resourceGroup().id, 'staging')
var shortStagingSuffix = take(uniqueStagingSuffix, 6)

@description('The database administrator username')
param sqlAdminUsername string = 'aiprofileadmin'

@description('The database administrator password')
@secure()
param sqlAdminPassword string

@description('The Replicate API token')
@secure()
param replicateApiToken string

@description('The JWT secret key')
@secure()
param jwtSecret string

@description('The Replicate webhook secret for signature validation')
@secure()
param replicateWebhookSecret string

@description('Enable Container Registry for staging Docker images (cost-optimized)')
param enableContainerRegistry bool = false

@description('Container Registry SKU - Basic tier for staging cost optimization')
@allowed(['Basic', 'Standard'])
param containerRegistrySku string = 'Basic'

// Staging-Optimized Variables with unique naming
var containerAppEnvironmentName = 'app-env-staging-${shortStagingSuffix}'
var containerAppName = 'api-staging-${shortStagingSuffix}'
var staticWebAppName = 'swa-staging-${shortStagingSuffix}'
var sqlServerName = 'sql-staging-${uniqueStagingSuffix}'
var sqlDatabaseName = 'profilephotomakerdb-staging'
var storageAccountName = 'stgstaging${shortStagingSuffix}'
// Staging Key Vault with unique naming
var keyVaultName = 'kv-stg-${shortStagingSuffix}'
var applicationInsightsName = 'ai-staging-${shortStagingSuffix}'
var logAnalyticsName = 'logs-staging-${shortStagingSuffix}'
var containerRegistryName = 'acrstaging${shortStagingSuffix}'

// Container App Environment (replaces App Service Plan for cost optimization)
resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerAppEnvironmentName
  location: location
  properties: {
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker Staging'
    CostOptimized: 'true'
  }
}

// Container App (Backend API) - Cost-Optimized for Staging
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    environmentId: containerAppEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 80
        allowInsecure: false
        transport: 'auto'
        corsPolicy: {
          allowedOrigins: [
            'https://${staticWebAppName}.azurestaticapps.net'
            'https://${staticWebAppName}.azurestaticapps.net'
          ]
          allowCredentials: false
        }
      }
      secrets: [
        {
          name: 'db-connection-string'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/DatabaseConnectionString'
          identity: 'system'
        }
        {
          name: 'jwt-secret'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/JwtSecret'
          identity: 'system'
        }
        {
          name: 'replicate-token'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/ReplicateApiToken'
          identity: 'system'
        }
        {
          name: 'webhook-secret'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/ReplicateWebhookSecret'
          identity: 'system'
        }
      ]
    }
    template: {
      revisionSuffix: 'staging-v1'
      scale: {
        minReplicas: 0  // Cost optimization: scale to zero when idle
        maxReplicas: 2  // Staging limit: max 2 replicas
      }
      containers: [
        {
          name: 'api-container'
          image: 'mcr.microsoft.com/dotnet/aspnet:8.0' // Placeholder - update with actual image
          resources: {
            cpu: json('0.25')      // Minimal CPU for staging
            memory: '0.5Gi'        // Minimal memory for staging
          }
          env: [
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'db-connection-string'
            }
            {
              name: 'Jwt__Secret'
              secretRef: 'jwt-secret'
            }
            {
              name: 'Jwt__ValidAudience'
              value: 'https://${staticWebAppName}.azurestaticapps.net'
            }
            {
              name: 'Jwt__ValidIssuer'
              value: 'https://${containerAppName}.${containerAppEnvironment.properties.defaultDomain}'
            }
            {
              name: 'Replicate__ApiToken'
              secretRef: 'replicate-token'
            }
            {
              name: 'Replicate__WebhookSecret'
              secretRef: 'webhook-secret'
            }
            {
              name: 'AzureStorage__ConnectionString'
              value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
            }
            {
              name: 'AzureStorage__ContainerName'
              value: 'profile-images'
            }
            {
              name: 'ApplicationInsights__ConnectionString'
              value: applicationInsights.properties.ConnectionString
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Staging'
            }
          ]
        }
      ]
    }
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker Staging'
    CostOptimized: 'true'
  }
}

// Static Web App (Frontend)
resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    repositoryUrl: 'https://github.com/YourUsername/AI.ProfilePhotoMaker'
    branch: 'main'
    buildProperties: {
      appLocation: 'AI.ProfilePhotoMaker.UI'
      outputLocation: 'dist/ai.profile-photo-maker.ui'
    }
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker'
  }
}

// SQL Server
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminUsername
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker'
  }
}

// SQL Database
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2GB
    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
    readScale: 'Disabled'
    requestedBackupStorageRedundancy: 'Local'
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker'
  }
}

// SQL Server Firewall Rule for Azure Services
resource sqlFirewallRule 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
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
    dnsEndpointType: 'Standard'
    defaultToOAuthAuthentication: false
    publicNetworkAccess: 'Enabled'
    allowCrossTenantReplication: false
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: true
    allowSharedKeyAccess: true
    networkAcls: {
      bypass: 'AzureServices'
      virtualNetworkRules: []
      ipRules: []
      defaultAction: 'Allow'
    }
    supportsHttpsTrafficOnly: true
    encryption: {
      requireInfrastructureEncryption: false
      services: {
        file: {
          keyType: 'Account'
          enabled: true
        }
        blob: {
          keyType: 'Account'
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
    accessTier: 'Hot'
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker'
  }
}

// Blob Service
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    cors: {
      corsRules: [
        {
          allowedOrigins: [
            'https://${staticWebAppName}.azurestaticapps.net'
            'https://${namePrefix}.azurestaticapps.net'
          ]
          allowedMethods: [
            'GET'
            'POST'
            'PUT'
            'DELETE'
            'HEAD'
            'OPTIONS'
          ]
          maxAgeInSeconds: 3600
          exposedHeaders: [
            '*'
          ]
          allowedHeaders: [
            '*'
          ]
        }
      ]
    }
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

// Storage Container for images
resource storageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'profile-images'
  properties: {
    publicAccess: 'Blob'
  }
}

// Log Analytics Workspace - Staging Cost Optimized
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 7      // Reduced retention for staging cost optimization
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    workspaceCapping: {
      dailyQuotaGb: json('0.5')  // Reduced daily quota for staging
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker Staging'
    CostOptimized: 'true'
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
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker'
  }
}

// Key Vault - Staging Configuration
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: containerApp.identity.principalId
        permissions: {
          secrets: [
            'get'
            'list'
          ]
        }
      }
    ]
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
    enableSoftDelete: true
    softDeleteRetentionInDays: 30  // Reduced for staging cost optimization
    enableRbacAuthorization: false
    publicNetworkAccess: 'Enabled'
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker Staging'
    CostOptimized: 'true'
  }
}

// Key Vault Secrets
resource jwtSecretKV 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'JwtSecret'
  properties: {
    value: jwtSecret
  }
}

resource replicateTokenKV 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ReplicateApiToken'
  properties: {
    value: replicateApiToken
  }
}

// Replicate Webhook Secret for signature validation
resource replicateWebhookSecretKV 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ReplicateWebhookSecret'
  properties: {
    value: replicateWebhookSecret
  }
}

resource sqlConnectionStringKV 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'DatabaseConnectionString'
  properties: {
    value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabaseName};Persist Security Info=False;User ID=${sqlAdminUsername};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

// Container App configuration is handled directly in the containerApp resource template
// Environment variables and secrets are configured in the Container App definition above

// Redis Cache removed - not needed for current deployment

// Container Registry (optional)
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = if (enableContainerRegistry) {
  name: containerRegistryName
  location: location
  sku: {
    name: containerRegistrySku
  }
  properties: {
    adminUserEnabled: true
    policies: {
      quarantinePolicy: {
        status: 'enabled'
      }
      trustPolicy: {
        type: 'Notary'
        status: 'enabled'
      }
      retentionPolicy: {
        days: 7
        status: 'enabled'
      }
    }
    encryption: {
      status: 'enabled'
    }
    dataEndpointEnabled: false
    publicNetworkAccess: 'Enabled'
    networkRuleBypassOptions: 'AzureServices'
    zoneRedundancy: 'Disabled'
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker'
  }
}

// Redis Cache connection string removed - Redis Cache not deployed

// Container Registry credentials in Key Vault (if enabled)
resource containerRegistryUsernameKV 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (enableContainerRegistry) {
  parent: keyVault
  name: 'ContainerRegistryUsername'
  properties: {
    value: containerRegistry.name
  }
}

resource containerRegistryPasswordKV 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (enableContainerRegistry) {
  parent: keyVault
  name: 'ContainerRegistryPassword'
  properties: {
    value: enableContainerRegistry ? containerRegistry.listCredentials().passwords[0].value : ''
  }
}

// Action Group for Alerts
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${namePrefix}-alerts-${environmentName}'
  location: 'Global'
  properties: {
    groupShortName: 'AIProfile'
    enabled: true
    emailReceivers: [
      {
        name: 'AdminEmail'
        emailAddress: 'admin@example.com' // Replace with actual email
        useCommonAlertSchema: true
      }
    ]
    smsReceivers: []
    webhookReceivers: []
    azureAppPushReceivers: []
    itsmReceivers: []
    azureAutomationRunbookReceivers: []
    voiceReceivers: []
    armRoleReceivers: []
    azureFunctionReceivers: []
    logicAppReceivers: []
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker'
  }
}

// Application Insights Availability Test
resource availabilityTest 'Microsoft.Insights/webtests@2022-06-15' = {
  name: '${namePrefix}-availability-${environmentName}'
  location: location
  kind: 'ping'
  properties: {
    SyntheticMonitorId: '${namePrefix}-availability-${environmentName}'
    Name: 'AI Profile Photo Maker Availability Test'
    Description: 'Availability test for the AI Profile Photo Maker application'
    Enabled: true
    Frequency: 300 // 5 minutes
    Timeout: 120 // 2 minutes
    Kind: 'ping'
    RetryEnabled: true
    Locations: [
      {
        Id: 'us-ca-sjc-azr'
      }
      {
        Id: 'us-tx-sn1-azr'
      }
      {
        Id: 'us-il-ch1-azr'
      }
    ]
    Configuration: {
      WebTest: '<WebTest Name="AI Profile Photo Maker Test" Id="${guid(resourceGroup().id, 'availability')}" Enabled="True" CssProjectStructure="" CssIteration="" Timeout="120" WorkItemIds="" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010" Description="" CredentialUserName="" CredentialPassword="" PreAuthenticate="True" Proxy="default" StopOnError="False" RecordedResultFile="" ResultsLocale=""><Items><Request Method="GET" Guid="${guid(resourceGroup().id, 'request')}" Version="1.1" Url="https://${webAppName}.azurewebsites.net/health" ThinkTime="0" Timeout="120" ParseDependentRequests="False" FollowRedirects="True" RecordResult="True" Cache="False" ResponseTimeGoal="0" Encoding="utf-8" ExpectedHttpStatusCode="200" ExpectedResponseUrl="" ReportingName="" IgnoreHttpStatusCode="False" /></Items></WebTest>'
    }
  }
  tags: {
    'hidden-link:${applicationInsights.id}': 'Resource'
    Environment: environmentName
    Application: 'AI Profile Photo Maker'
  }
}

// Staging-Optimized Metric Alerts for Container Apps
resource containerAppResponseTimeAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'containerapp-response-time-staging-${shortStagingSuffix}'
  location: 'Global'
  properties: {
    description: 'Alert when container app response time is high (staging threshold)'
    severity: 3  // Lower severity for staging
    enabled: true
    scopes: [
      containerApp.id
    ]
    evaluationFrequency: 'PT15M'  // Less frequent evaluation for staging
    windowSize: 'PT30M'           // Longer window for staging
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'RequestsPerSecond'
          metricName: 'RequestsPerSecond'
          operator: 'GreaterThan'
          threshold: 10 // Relaxed threshold for staging
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker Staging'
    CostOptimized: 'true'
  }
}

resource sqlDtuAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${namePrefix}-sql-dtu-${environmentName}'
  location: 'Global'
  properties: {
    description: 'Alert when SQL Database DTU usage is high'
    severity: 1
    enabled: true
    scopes: [
      sqlDatabase.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'DTUPercentage'
          metricName: 'dtu_consumption_percent'
          operator: 'GreaterThan'
          threshold: 80
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker'
  }
}

// Redis memory alert removed - Redis Cache not deployed

// Diagnostic Settings for Container App
resource containerAppDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'containerApp-diagnostics-staging'
  scope: containerApp
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 7  // Staging retention optimization
        }
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 7  // Staging retention optimization
        }
      }
    ]
  }
}

// Diagnostic Settings for SQL Database
resource sqlDatabaseDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'sqlDatabase-diagnostics'
  scope: sqlDatabase
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

// Redis Cache diagnostics removed - Redis Cache not deployed

// Staging-Optimized Outputs
output containerAppName string = containerApp.name
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output containerAppEnvironmentName string = containerAppEnvironment.name
output staticWebAppName string = staticWebApp.name
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output storageAccountName string = storageAccount.name
output keyVaultName string = keyVault.name
output applicationInsightsName string = applicationInsights.name
output logAnalyticsName string = logAnalyticsWorkspace.name
output containerRegistryName string = enableContainerRegistry ? containerRegistry.name : ''
output containerRegistryLoginServer string = enableContainerRegistry ? containerRegistry.properties.loginServer : ''

// Staging Environment Info
output environmentName string = environmentName
output resourceNamingSuffix string = shortStagingSuffix
output costOptimized bool = true