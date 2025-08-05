param appName string = 'aiprofilemaker'
param environment string = 'v1'
param location string = resourceGroup().location

// Generate unique names - test just basics
var uniqueSuffix = uniqueString(resourceGroup().id)
var containerRegistryName = '${appName}cr${environment}${uniqueSuffix}'

// Container Registry only - most basic resource
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: containerRegistryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

output containerRegistryName string = containerRegistry.name
output containerRegistryLoginServer string = containerRegistry.properties.loginServer