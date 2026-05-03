$tenantId       = "2e7660ae-d674-4c1b-93fc-d2dceeb36812"
$subscriptionId = "e1d9ef80-dcea-41ea-b053-ea32d3530915"
$rgName         = "BiteTheBookie"
$appName        = "bitethebookie-app-20260216193509"
$acrName        = "registry20260214124314"
$acrLogin       = "$acrName.azurecr.io"
$imageName      = "bitethebookie"
$imageTag       = git rev-parse --short HEAD   # unique per commit

Write-Host "🔐 Logging in..." -ForegroundColor Cyan
az login --tenant $tenantId | Out-Null
az account set --subscription $subscriptionId
az acr login --name $acrName

Write-Host "🐳 Building image..." -ForegroundColor Cyan
docker build -f BiteTheBookie/Dockerfile -t "$acrLogin/${imageName}:$imageTag" .
if ($LASTEXITCODE -ne 0) { throw "Docker build failed." }

Write-Host "📤 Pushing image..." -ForegroundColor Cyan
docker push "$acrLogin/${imageName}:$imageTag"
if ($LASTEXITCODE -ne 0) { throw "Docker push failed." }

Write-Host "🚀 Updating Container App..." -ForegroundColor Cyan
az containerapp update `
  --resource-group $rgName `
  --name $appName `
  --image "$acrLogin/${imageName}:$imageTag"

Write-Host "`n✅ Deployment complete!" -ForegroundColor Green
az containerapp show `
  --resource-group $rgName `
  --name $appName `
  --query "properties.configuration.ingress.fqdn" `
  --output tsv