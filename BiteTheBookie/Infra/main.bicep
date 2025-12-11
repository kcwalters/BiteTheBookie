@description('Deployment location')
param location string = resourceGroup().location

@description('Container App name')
param containerAppName string = 'bitethebookie-container-app'

@description('Container Apps managed environment name')
param environmentName string

@description('ACR name (without .azurecr.io)')
param acrName string = 'bitethebookie'

@description('Image name (repository) in ACR')
param imageName string = 'bitethebookie'

@description('Image tag to deploy')
param imageTag string = 'latest'

// Existing Container Apps Environment
module managedEnv 'modules/containerapp-environment.bicep' = {
  name: 'bitethebookie-env-existing'
  params: {
    environmentName: environmentName
  }
}

// Existing ACR
module acr 'modules/acr-existing.bicep' = {
  name: 'bitethebookie-acr-existing'
  params: {
    acrName: acrName
  }
}

// Container App + identity + ACR binding
module containerApp 'modules/containerapp.bicep' = {
  name: 'bitethebookie-container-app'
  params: {
    location: location
    containerAppName: containerAppName
    environmentId: managedEnv.outputs.environmentId
    acrLoginServer: acr.outputs.loginServer
    acrResourceId: acr.outputs.acrId
    imageName: imageName
    imageTag: imageTag
  }
}