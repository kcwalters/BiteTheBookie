@description('Existing Container Apps managed environment name')
param environmentName string

resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-05-01' existing = {
  name: environmentName
}

output environmentId string = containerAppEnv.id