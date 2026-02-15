$tenantId       = "2e7660ae-d674-4c1b-93fc-d2dceeb36812"
$subscriptionId = "e1d9ef80-dcea-41ea-b053-ea32d3530915"
$clientId       = "149500c4-5869-4e13-821c-296b5f6e1c2a"

$rgName  = "BiteTheBookie"
$acrRg   = "bitethebookie"   # resource group where the ACR lives
$acrName = "registry20260214124314"

az login --tenant $tenantId | Out-Null
az account set --subscription $subscriptionId

$spObjectId = az ad sp show --id $clientId --query id -o tsv
if (-not $spObjectId) { throw "Failed to resolve service principal object id for clientId $clientId" }

az role assignment create `
  --assignee-object-id $spObjectId `
  --assignee-principal-type ServicePrincipal `
  --role Contributor `
  --scope "/subscriptions/$subscriptionId/resourceGroups/$rgName"

az role assignment create `
  --assignee-object-id $spObjectId `
  --assignee-principal-type ServicePrincipal `
  --role AcrPush `
  --scope "/subscriptions/$subscriptionId/resourceGroups/$acrRg/providers/Microsoft.ContainerRegistry/registries/$acrName"