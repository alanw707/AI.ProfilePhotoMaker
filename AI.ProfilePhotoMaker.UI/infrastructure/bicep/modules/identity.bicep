@description('Location for all resources')
param location string

@description('Environment name')
param environmentName string

@description('Application name prefix')
param appName string

// User-assigned managed identity for application authentication
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${appName}-${environmentName}'
  location: location
  tags: {
    Environment: environmentName
    Application: appName
  }
}

// Outputs
output managedIdentityId string = managedIdentity.id
output managedIdentityName string = managedIdentity.name
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
output managedIdentityClientId string = managedIdentity.properties.clientId