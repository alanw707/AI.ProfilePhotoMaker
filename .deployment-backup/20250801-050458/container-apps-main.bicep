// AI Profile Photo Maker - Modern Container Apps Infrastructure
// Production-ready deployment with Container Apps, security, and observability

targetScope = 'subscription'

@description('The name of the resource group')
param resourceGroupName string = 'rg-aiprofilemaker-${environment}'

@description('The location for all resources')
param location string = 'eastus2'

@description('Environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environment string

@description('Application name')
param appName string = 'aiprofilemaker'

@description('Container image tags')
param frontendImageTag string = 'latest'
param backendImageTag string = 'latest'

@description('SQL Database administrator login')
param sqlAdminLogin string

@description('SQL Database administrator password')
@secure()
param sqlAdminPassword string

@description('JWT secret key')
@secure()
param jwtSecret string

@description('Replicate API token')
@secure()
param replicateApiToken string

@description('Replicate webhook secret')
@secure()
param replicateWebhookSecret string

@description('Custom domain name (optional)')
param customDomain string = ''

@description('Enable multi-region deployment')
param enableMultiRegion bool = false

@description('Secondary region for DR')
param secondaryLocation string = 'westus2'

// Create resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: resourceGroupName
  location: location
  tags: {
    Environment: environment
    Application: appName
    ManagedBy: 'Bicep'
    CostCenter: 'Engineering'
    Owner: 'DevOps'
  }
}

// Deploy core networking and container infrastructure
module networking 'modules/networking.bicep' = {
  name: 'networking'
  scope: rg
  params: {
    location: location
    environment: environment
    appName: appName
  }
}

// Deploy Container Registry
module containerRegistry 'modules/container-registry.bicep' = {
  name: 'container-registry'
  scope: rg
  params: {
    location: location
    environment: environment
    appName: appName
    subnetId: networking.outputs.containerRegistrySubnetId
  }
}

// Deploy database with private endpoint
module database 'modules/sql-database.bicep' = {
  name: 'database'
  scope: rg
  params: {
    location: location
    environment: environment
    appName: appName
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    subnetId: networking.outputs.databaseSubnetId
    enableMultiRegion: enableMultiRegion
    secondaryLocation: secondaryLocation
  }
}

// Deploy storage with private endpoint
module storage 'modules/blob-storage.bicep' = {
  name: 'storage'
  scope: rg
  params: {
    location: location
    environment: environment
    appName: appName
    subnetId: networking.outputs.storageSubnetId
    enableMultiRegion: enableMultiRegion
    secondaryLocation: secondaryLocation
  }
}

// Deploy Redis cache
module cache 'modules/redis-cache.bicep' = {
  name: 'cache'
  scope: rg
  params: {
    location: location
    environment: environment
    appName: appName
    subnetId: networking.outputs.cacheSubnetId
  }
}

// Deploy Key Vault with private endpoint
module keyVault 'modules/key-vault.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    location: location
    environment: environment
    appName: appName
    subnetId: networking.outputs.keyVaultSubnetId
    secrets: {
      sqlConnectionString: database.outputs.connectionString
      jwtSecret: jwtSecret
      replicateApiToken: replicateApiToken
      replicateWebhookSecret: replicateWebhookSecret
      storageConnectionString: storage.outputs.connectionString
      redisConnectionString: cache.outputs.connectionString
    }
  }
}

// Deploy monitoring and observability
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  scope: rg
  params: {
    location: location
    environment: environment
    appName: appName
  }
}

// Deploy Container Apps Environment
module containerAppsEnvironment 'modules/container-apps-environment.bicep' = {
  name: 'container-apps-environment'
  scope: rg
  params: {
    location: location
    environment: environment
    appName: appName
    subnetId: networking.outputs.containerAppsSubnetId
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// Deploy backend API container app
module backendApp 'modules/backend-container-app.bicep' = {
  name: 'backend-app'
  scope: rg
  params: {
    location: location
    environment: environment
    appName: appName
    containerAppsEnvironmentId: containerAppsEnvironment.outputs.containerAppsEnvironmentId
    containerRegistryName: containerRegistry.outputs.containerRegistryName
    backendImageTag: backendImageTag
    keyVaultName: keyVault.outputs.keyVaultName
    applicationInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
  }
}

// Deploy frontend container app
module frontendApp 'modules/frontend-container-app.bicep' = {
  name: 'frontend-app'
  scope: rg
  params: {
    location: location
    environment: environment
    appName: appName
    containerAppsEnvironmentId: containerAppsEnvironment.outputs.containerAppsEnvironmentId
    containerRegistryName: containerRegistry.outputs.containerRegistryName
    frontendImageTag: frontendImageTag
    backendUrl: backendApp.outputs.backendUrl
  }
}

// Deploy CDN and custom domain
module cdn 'modules/cdn.bicep' = {
  name: 'cdn'
  scope: rg
  params: {
    environment: environment
    appName: appName
    frontendUrl: frontendApp.outputs.frontendUrl
    storageEndpoint: storage.outputs.blobEndpoint
    customDomain: customDomain
  }
}

// Deploy security center and compliance
module security 'modules/security.bicep' = {
  name: 'security'
  scope: rg
  params: {
    location: location
    environment: environment
    appName: appName
    keyVaultId: keyVault.outputs.keyVaultId
    storageAccountId: storage.outputs.storageAccountId
    sqlServerId: database.outputs.sqlServerId
  }
}

// Deploy alerting and notifications
module alerting 'modules/alerting.bicep' = {
  name: 'alerting'
  scope: rg
  params: {
    location: location
    environment: environment
    appName: appName
    applicationInsightsId: monitoring.outputs.applicationInsightsId
    backendAppId: backendApp.outputs.backendAppId
    frontendAppId: frontendApp.outputs.frontendAppId
    sqlDatabaseId: database.outputs.sqlDatabaseId
    storageAccountId: storage.outputs.storageAccountId
  }
}

// Outputs for CI/CD pipeline
output resourceGroupName string = rg.name
output frontendUrl string = customDomain != '' ? 'https://${customDomain}' : frontendApp.outputs.frontendUrl
output backendUrl string = backendApp.outputs.backendUrl
output containerRegistryLoginServer string = containerRegistry.outputs.containerRegistryLoginServer
output keyVaultName string = keyVault.outputs.keyVaultName
output applicationInsightsConnectionString string = monitoring.outputs.applicationInsightsConnectionString
output cdnEndpoint string = cdn.outputs.cdnEndpoint