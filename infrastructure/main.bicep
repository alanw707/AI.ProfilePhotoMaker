// Main Bicep template for AI Profile Photo Maker Azure infrastructure
@description('The name of the environment (e.g., dev, staging, prod)')
param environmentName string = 'prod'

@description('The Azure region for resource deployment')
param location string = resourceGroup().location

@description('The name prefix for all resources')
param namePrefix string = 'aiprofilephotomaker'

@description('The SKU for the App Service Plan')
@allowed(['F1', 'D1', 'B1', 'B2', 'B3', 'S1', 'S2', 'S3', 'P1', 'P2', 'P3'])
param appServicePlanSku string = 'B1'

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

// Variables
var uniqueSuffix = uniqueString(resourceGroup().id)
var appServicePlanName = '${namePrefix}-asp-${environmentName}'
var webAppName = '${namePrefix}api-${environmentName}'
var staticWebAppName = '${namePrefix}-swa-${environmentName}'
var sqlServerName = '${namePrefix}-sql-${environmentName}-${uniqueSuffix}'
var sqlDatabaseName = '${namePrefix}db'
var storageAccountName = '${take(namePrefix, 14)}st${take(uniqueSuffix, 8)}'
var keyVaultName = '${namePrefix}-kv-${environmentName}-${uniqueSuffix}'
var applicationInsightsName = '${namePrefix}-ai-${environmentName}'
var logAnalyticsName = '${namePrefix}-la-${environmentName}'

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: appServicePlanSku
  }
  kind: 'app'
  properties: {
    reserved: false
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker'
  }
}

// Web App (Backend API)
resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: webAppName
  location: location
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      defaultDocuments: []
      httpLoggingEnabled: true
      logsDirectorySizeLimit: 35
      detailedErrorLoggingEnabled: true
      requestTracingEnabled: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      cors: {
        allowedOrigins: [
          'https://${staticWebAppName}.azurestaticapps.net'
          'https://${namePrefix}.azurestaticapps.net'
        ]
        supportCredentials: false
      }
    }
    httpsOnly: true
    clientAffinityEnabled: false
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker'
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

// Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    workspaceCapping: {
      dailyQuotaGb: 1
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker'
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

// Key Vault
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
        objectId: webApp.identity.principalId
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
    softDeleteRetentionInDays: 90
    enableRbacAuthorization: false
    vaultUri: 'https://${keyVaultName}.${environment().suffixes.keyvaultDns}/'
    provisioningState: 'Succeeded'
    publicNetworkAccess: 'Enabled'
  }
  tags: {
    Environment: environmentName
    Application: 'AI Profile Photo Maker'
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

// Web App has managed identity enabled by default when accessing Key Vault

// Web App Configuration
resource webAppConfig 'Microsoft.Web/sites/config@2023-01-01' = {
  parent: webApp
  name: 'appsettings'
  properties: {
    ConnectionStrings__DefaultConnection: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/DatabaseConnectionString/)'
    Jwt__Secret: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/JwtSecret/)'
    Jwt__ValidAudience: 'https://${staticWebAppName}.azurestaticapps.net'
    Jwt__ValidIssuer: 'https://${webAppName}.azurewebsites.net'
    Replicate__ApiToken: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/ReplicateApiToken/)'
    Replicate__WebhookSecret: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/ReplicateWebhookSecret/)'
    AzureStorage__ConnectionString: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
    AzureStorage__ContainerName: 'profile-images'
    ApplicationInsights__InstrumentationKey: applicationInsights.properties.InstrumentationKey
    ApplicationInsights__ConnectionString: applicationInsights.properties.ConnectionString
    ASPNETCORE_ENVIRONMENT: environmentName == 'prod' ? 'Production' : 'Development'
  }
  dependsOn: [
    jwtSecretKV
    replicateTokenKV
    sqlConnectionStringKV
  ]
}

// Outputs
output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output staticWebAppName string = staticWebApp.name
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output storageAccountName string = storageAccount.name
output keyVaultName string = keyVault.name
output applicationInsightsName string = applicationInsights.name