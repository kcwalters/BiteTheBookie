@description('ACR name (without FQDN)')
param acrName string

resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' existing = {
  name: acrName
}

output acrId string = acr.id
output loginServer string = acr.properties.loginServer