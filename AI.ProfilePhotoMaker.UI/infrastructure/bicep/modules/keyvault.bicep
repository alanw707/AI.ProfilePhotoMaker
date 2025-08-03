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

@description('Facebook App Secret')
@secure()
param facebookAppSecret string

@description('Google Client Secret')
@secure()
param googleClientSecret string

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-${appName}-${environmentName}'
  location: location
  tags: {
    Environment: environmentName
    Application: appName
  }
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenant().tenantId
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      virtualNetworkRules: []
      ipRules: []
    }
    accessPolicies: [
      {
        tenantId: tenant().tenantId
        objectId: managedIdentityPrincipalId
        permissions: {
          secrets: [
            'get'
            'list'
          ]
          certificates: [
            'get'
            'list'
          ]
        }
      }
    ]
    enabledForDeployment: false
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: false
    enableRbacAuthorization: false
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    purgeProtectionEnabled: false
  }
}

// Private endpoint for Key Vault
resource keyVaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: 'pe-${keyVault.name}'
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
        name: 'pl-${keyVault.name}'
        properties: {
          privateLinkServiceId: keyVault.id
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

// Private DNS zone for Key Vault
resource keyVaultPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.vaultcore.azure.net'
  location: 'global'
  tags: {
    Environment: environmentName
    Application: appName
  }
}

// Link private DNS zone to VNet
resource keyVaultPrivateDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: keyVaultPrivateDnsZone
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
resource keyVaultPrivateDnsRecord 'Microsoft.Network/privateDnsZones/A@2020-06-01' = {
  parent: keyVaultPrivateDnsZone
  name: keyVault.name
  properties: {
    ttl: 300
    aRecords: [
      {
        ipv4Address: keyVaultPrivateEndpoint.properties.customDnsConfigs[0].ipAddresses[0]
      }
    ]
  }
}

// Secrets
resource sqlPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'sql-admin-password'
  properties: {
    value: sqlAdminPassword
    attributes: {
      enabled: true
    }
  }
}

resource jwtSecretSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'jwt-secret'
  properties: {
    value: jwtSecret
    attributes: {
      enabled: true
    }
  }
}

resource replicateApiTokenSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'replicate-api-token'
  properties: {
    value: replicateApiToken
    attributes: {
      enabled: true
    }
  }
}

resource stripeApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'stripe-api-key'
  properties: {
    value: stripeApiKey
    attributes: {
      enabled: true
    }
  }
}

resource facebookAppSecretSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'facebook-app-secret'
  properties: {
    value: facebookAppSecret
    attributes: {
      enabled: true
    }
  }
}

resource googleClientSecretSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'google-client-secret'
  properties: {
    value: googleClientSecret
    attributes: {
      enabled: true
    }
  }
}

// Outputs
output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output sqlPasswordSecretUri string = sqlPasswordSecret.properties.secretUri
output jwtSecretSecretUri string = jwtSecretSecret.properties.secretUri
output replicateApiTokenSecretUri string = replicateApiTokenSecret.properties.secretUri
output stripeApiKeySecretUri string = stripeApiKeySecret.properties.secretUri
output facebookAppSecretSecretUri string = facebookAppSecretSecret.properties.secretUri
output googleClientSecretSecretUri string = googleClientSecretSecret.properties.secretUri