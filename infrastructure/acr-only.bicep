// ACR-Only Infrastructure Template  
// Creates just the Container Registry for local build workflow
// Run this first, then build and push images locally, then run main infrastructure

param appName string = 'aipm'
param environment string = 'v1'
param location string = resourceGroup().location

// Generate unique names
var uniqueSuffix = uniqueString(resourceGroup().id)
var containerRegistryName = '${appName}cr${environment}${uniqueSuffix}'

// Container Registry (only resource in this template)
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-05-01' = {
  name: containerRegistryName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

// Outputs for local build scripts
output containerRegistryName string = containerRegistry.name
output containerRegistryLoginServer string = containerRegistry.properties.loginServer
output uniqueSuffix string = uniqueSuffix