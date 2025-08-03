targetScope = 'subscription'

@description('Environment name (dev, staging, prod)')
param environmentName string

@description('Location for all resources')
param location string = 'eastus2'

@description('Application name prefix')
param appName string = 'aiprofilemaker'

@description('SQL Administrator password')
@secure()
param sqlAdminPassword string

@description('JWT Secret key')
@secure()
param jwtSecret string

@description('Replicate API token')
@secure()
param replicateApiToken string

@description('Stripe API key')
@secure()
param stripeApiKey string

@description('Facebook App ID for OAuth')
param facebookAppId string = ''

@description('Facebook App Secret for OAuth')
@secure()
param facebookAppSecret string = ''

@description('Google Client ID for OAuth')
param googleClientId string = ''

@description('Google Client Secret for OAuth')
@secure()
param googleClientSecret string = ''

@description('Container image tag to deploy')
param imageTag string = 'latest'

// Resource group
resource resourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: 'rg-${appName}-${environmentName}'
  location: location
  tags: {
    Environment: environmentName
    Application: appName
    IaC: 'Bicep'
    CreatedBy: 'GitHub-Actions'
    DeployedAt: utcNow()
  }
}

// Networking - Virtual Network, Subnets, NSGs
module networking 'modules/networking.bicep' = {
  scope: resourceGroup
  name: 'networking-deployment'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
  }
}

// Managed Identity for secure authentication
module identity 'modules/identity.bicep' = {
  scope: resourceGroup
  name: 'identity-deployment'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
  }
}

// Key Vault for secrets management
module keyVault 'modules/keyvault.bicep' = {
  scope: resourceGroup
  name: 'keyvault-deployment'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
    managedIdentityPrincipalId: identity.outputs.managedIdentityPrincipalId
    vnetId: networking.outputs.vnetId
    privateEndpointSubnetId: networking.outputs.privateEndpointSubnetId
    sqlAdminPassword: sqlAdminPassword
    jwtSecret: jwtSecret
    replicateApiToken: replicateApiToken
    stripeApiKey: stripeApiKey
    facebookAppSecret: facebookAppSecret
    googleClientSecret: googleClientSecret
  }
}

// Container Registry for images
module registry 'modules/registry.bicep' = {
  scope: resourceGroup
  name: 'registry-deployment'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
    managedIdentityPrincipalId: identity.outputs.managedIdentityPrincipalId
    vnetId: networking.outputs.vnetId
    privateEndpointSubnetId: networking.outputs.privateEndpointSubnetId
  }
}

// SQL Database (Flexible Server)
module database 'modules/database.bicep' = {
  scope: resourceGroup
  name: 'database-deployment'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
    managedIdentityPrincipalId: identity.outputs.managedIdentityPrincipalId
    managedIdentityName: identity.outputs.managedIdentityName
    vnetId: networking.outputs.vnetId
    privateEndpointSubnetId: networking.outputs.privateEndpointSubnetId
    sqlAdminPasswordSecretUri: keyVault.outputs.sqlPasswordSecretUri
  }
}

// Storage Account for blob storage
module storage 'modules/storage.bicep' = {
  scope: resourceGroup
  name: 'storage-deployment'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
    managedIdentityPrincipalId: identity.outputs.managedIdentityPrincipalId
    vnetId: networking.outputs.vnetId
    privateEndpointSubnetId: networking.outputs.privateEndpointSubnetId
  }
}

// Container Apps Environment
module containerEnvironment 'modules/containerenv.bicep' = {
  scope: resourceGroup
  name: 'containerenv-deployment'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
    vnetId: networking.outputs.vnetId
    containerAppsSubnetId: networking.outputs.containerAppsSubnetId
  }
}

// Container Apps (API & UI)
module containerApps 'modules/containerapps.bicep' = {
  scope: resourceGroup
  name: 'containerapps-deployment'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
    containerAppsEnvironmentId: containerEnvironment.outputs.containerAppsEnvironmentId
    managedIdentityId: identity.outputs.managedIdentityId
    containerRegistryServer: registry.outputs.registryLoginServer
    sqlServerFqdn: database.outputs.sqlServerFqdn
    databaseName: database.outputs.databaseName
    storageAccountName: storage.outputs.storageAccountName
    keyVaultUri: keyVault.outputs.keyVaultUri
    imageTag: imageTag
    facebookAppId: facebookAppId
    googleClientId: googleClientId
  }
  dependsOn: [
    database
    storage
    keyVault
    registry
  ]
}

// Migration Job for database setup
module migrationJob 'modules/migrationjob.bicep' = {
  scope: resourceGroup
  name: 'migrationjob-deployment'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
    containerAppsEnvironmentId: containerEnvironment.outputs.containerAppsEnvironmentId
    managedIdentityId: identity.outputs.managedIdentityId
    containerRegistryServer: registry.outputs.registryLoginServer
    sqlServerFqdn: database.outputs.sqlServerFqdn
    databaseName: database.outputs.databaseName
    keyVaultUri: keyVault.outputs.keyVaultUri
    imageTag: imageTag
  }
  dependsOn: [
    containerApps
  ]
}

// Application Insights for monitoring
module monitoring 'modules/monitoring.bicep' = {
  scope: resourceGroup
  name: 'monitoring-deployment'
  params: {
    location: location
    environmentName: environmentName
    appName: appName
  }
}

// Outputs for pipeline consumption
output resourceGroupName string = resourceGroup.name
output location string = location
output environmentName string = environmentName

// Application URLs
output frontendUrl string = containerApps.outputs.frontendUrl
output backendUrl string = containerApps.outputs.backendUrl

// Infrastructure details
output registryLoginServer string = registry.outputs.registryLoginServer
output registryName string = registry.outputs.registryName
output sqlServerFqdn string = database.outputs.sqlServerFqdn
output databaseName string = database.outputs.databaseName
output storageAccountName string = storage.outputs.storageAccountName
output keyVaultName string = keyVault.outputs.keyVaultName
output keyVaultUri string = keyVault.outputs.keyVaultUri

// Identity information
output managedIdentityClientId string = identity.outputs.managedIdentityClientId
output managedIdentityName string = identity.outputs.managedIdentityName

// Migration job name for triggering
output migrationJobName string = migrationJob.outputs.migrationJobName

// Monitoring
output applicationInsightsName string = monitoring.outputs.applicationInsightsName
output applicationInsightsConnectionString string = monitoring.outputs.applicationInsightsConnectionString