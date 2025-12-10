@description('Deployment location (defaults to resource group).')
param location string = resourceGroup().location

// Fixed names for Bitethebookie
var baseName            = 'bitethebookie'
var appServicePlanName  = '${baseName}-asp'
var appServiceName      = '${baseName}-app'
var keyVaultName        = toLower('${baseName}-kv')
var storageName         = toLower(replace('${baseName}st', '-', '')) // must be globally unique, alphanum only
var sbNamespaceName     = '${baseName}-sb'
var acrName             = toLower(replace('${baseName}acr', '-', ''))

// =======================
// App Service + Identity
// =======================
resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'P1v3'
    capacity: 1
  }
}

resource appService 'Microsoft.Web/sites@2022-09-01' = {
  name: appServiceName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      ftpsState: 'FtpsOnly'
      alwaysOn: true
    }
  }
}

// ============
// Key Vault
// ============
resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { name: 'standard', family: 'A' }
    enableRbacAuthorization: true
    softDeleteRetentionInDays: 7
  }
}

// App MI -> Key Vault Secrets User
resource kvSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, appService.identity.principalId, 'kv-secrets-user')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
    )
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ============
// Storage
// ============
resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
  }
}

// App MI -> Storage Blob Data Contributor
resource storageBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, appService.identity.principalId, 'st-blob-contrib')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      'ba92f5b4-2d11-453d-a403-e96b0029c9fe' // Storage Blob Data Contributor
    )
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ============
// Service Bus
// ============
resource sbNamespace 'Microsoft.ServiceBus/namespaces@2023-01-01-preview' = {
  name: sbNamespaceName
  location: location
  sku: { name: 'Standard', tier: 'Standard' }
  properties: { zoneRedundant: false }
}

// App MI -> SB Sender & Receiver
resource sbSenderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(sbNamespace.id, appService.identity.principalId, 'sb-sender')
  scope: sbNamespace
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '69a216fc-b8fb-41d1-bb31-e0829db1b086' // Azure Service Bus Data Sender
    )
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource sbReceiverRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(sbNamespace.id, appService.identity.principalId, 'sb-receiver')
  scope: sbNamespace
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '5c4f164a-e8e0-42c6-87a8-f9c5d4c3b2f9' // Azure Service Bus Data Receiver
    )
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ==========================
// Azure Container Registry
// ==========================
resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: acrName
  location: location
  sku: { name: 'Standard' }
  properties: {
    adminUserEnabled: false
    networkRuleBypassOptions: 'AzureServices'
  }
}

// App MI -> AcrPull
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, appService.identity.principalId, 'acr-pull')
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d' // AcrPull
    )
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ============
// Outputs
// ============
output appServicePrincipalId string = appService.identity.principalId
output keyVaultUri string = 'https://${keyVaultName}.vault.azure.net/'
output storageBlobEndpoint string = storage.properties.primaryEndpoints.blob
output sbNamespaceFqdn string = '${sbNamespaceName}.servicebus.windows.net'
output acrLoginServer string = '${acrName}.azurecr.io'