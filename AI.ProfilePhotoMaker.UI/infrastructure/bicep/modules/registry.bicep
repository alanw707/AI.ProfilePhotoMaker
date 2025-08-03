@description('Location for all resources')
param location string

@description('Environment name')
param environmentName string

@description('Application name prefix')
param appName string

@description('Managed Identity Principal ID')
param managedIdentityPrincipalId string

@description('Virtual Network ID')
param vnetId string

@description('Private Endpoint Subnet ID')
param privateEndpointSubnetId string

// Container Registry
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: 'cr${appName}${environmentName}'
  location: location
  tags: {
    Environment: environmentName
    Application: appName
  }
  sku: {
    name: 'Premium' // Required for private endpoints
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Disabled'
    networkRuleBypassOptions: 'AzureServices'
    policies: {
      trustPolicy: {
        type: 'Notary'
        status: 'enabled'
      }
      retentionPolicy: {
        days: 7
        status: 'enabled'
      }
      quarantinePolicy: {
        status: 'enabled'
      }
    }
    encryption: {
      status: 'enabled'
    }
    dataEndpointEnabled: true
    anonymousPullEnabled: false
  }
}

// Role assignment for managed identity to pull images
resource acrPullRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '7f951dda-4ed3-4680-a7ca-43fe172d538d' // AcrPull role
}

resource managedIdentityAcrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: containerRegistry
  name: guid(containerRegistry.id, managedIdentityPrincipalId, acrPullRoleDefinition.id)
  properties: {
    roleDefinitionId: acrPullRoleDefinition.id
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Private endpoint for Container Registry
resource registryPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: 'pe-${containerRegistry.name}'
  location: location
  tags: {
    Environment: environmentName
    Application: appName
  }
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'pl-${containerRegistry.name}'
        properties: {
          privateLinkServiceId: containerRegistry.id
          groupIds: [
            'registry'
          ]
        }
      }
    ]
  }
}

// Private DNS zone for Container Registry
resource registryPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.azurecr.io'
  location: 'global'
  tags: {
    Environment: environmentName
    Application: appName
  }
}

// Link private DNS zone to VNet
resource registryPrivateDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: registryPrivateDnsZone
  name: 'link-${appName}-${environmentName}'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetId
    }
  }
}

// DNS record for private endpoint
resource registryPrivateDnsRecord 'Microsoft.Network/privateDnsZones/A@2020-06-01' = {
  parent: registryPrivateDnsZone
  name: containerRegistry.name
  properties: {
    ttl: 300
    aRecords: [
      {
        ipv4Address: registryPrivateEndpoint.properties.customDnsConfigs[0].ipAddresses[0]
      }
    ]
  }
}

// DNS record for data endpoint
resource registryDataPrivateDnsRecord 'Microsoft.Network/privateDnsZones/A@2020-06-01' = {
  parent: registryPrivateDnsZone
  name: '${containerRegistry.name}.${location}.data'
  properties: {
    ttl: 300
    aRecords: [
      {
        ipv4Address: registryPrivateEndpoint.properties.customDnsConfigs[1].ipAddresses[0]
      }
    ]
  }
}

// Outputs
output registryId string = containerRegistry.id
output registryName string = containerRegistry.name
output registryLoginServer string = containerRegistry.properties.loginServer