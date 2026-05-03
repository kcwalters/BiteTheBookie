$tenantId       = "2e7660ae-d674-4c1b-93fc-d2dceeb36812"
$subscriptionId = "e1d9ef80-dcea-41ea-b053-ea32d3530915"
$rgName         = "BiteTheBookie"
$location       = "eastus"
$acrName        = "btbregistry2026"         # globally unique, alphanumeric only
$envName        = "btb-env"
$appName        = "bitethebookie-app"
$imageName      = "bitethebookie"
$imageTag       = "latest"
$acrLogin       = "$acrName.azurecr.io"

# Ensure script always runs from solution root
$solutionRoot = Resolve-Path "$PSScriptRoot\.."
Set-Location $solutionRoot
Write-Host "📁 Working directory: $solutionRoot" -ForegroundColor Gray

Write-Host "🔐 Logging in..." -ForegroundColor Cyan
az login --tenant $tenantId | Out-Null
az account set --subscription $subscriptionId

# ── 1. Resource Group ────────────────────────────────────────────────────────
Write-Host "📦 Ensuring resource group '$rgName'..." -ForegroundColor Cyan
az group create --name $rgName --location $location | Out-Null

# ── 2. Azure Container Registry ─────────────────────────────────────────────
Write-Host "🏗️  Creating ACR '$acrName'..." -ForegroundColor Cyan
az acr create `
  --resource-group $rgName `
  --name $acrName `
  --sku Basic `
  --admin-enabled true | Out-Null

# ── 3. Build & push image ────────────────────────────────────────────────────
Write-Host "🔑 Logging in to ACR..." -ForegroundColor Cyan
az acr login --name $acrName

Write-Host "🐳 Building Docker image..." -ForegroundColor Cyan
docker build -f BiteTheBookie/Dockerfile -t "$acrLogin/${imageName}:$imageTag" .
if ($LASTEXITCODE -ne 0) { throw "Docker build failed." }

Write-Host "📤 Pushing image to ACR..." -ForegroundColor Cyan
docker push "$acrLogin/${imageName}:$imageTag"
if ($LASTEXITCODE -ne 0) { throw "Docker push failed." }

# ── 4. Container Apps Environment ───────────────────────────────────────────
Write-Host "🌍 Creating Container Apps environment '$envName'..." -ForegroundColor Cyan
az containerapp env create `
  --name $envName `
  --resource-group $rgName `
  --location $location | Out-Null

# ── 5. Get ACR credentials ───────────────────────────────────────────────────
$acrPassword = az acr credential show --name $acrName --query "passwords[0].value" --output tsv

# ── 6. Create Container App ──────────────────────────────────────────────────
Write-Host "🚀 Creating Container App '$appName'..." -ForegroundColor Cyan
az containerapp create `
  --name $appName `
  --resource-group $rgName `
  --environment $envName `
  --image "$acrLogin/${imageName}:$imageTag" `
  --registry-server $acrLogin `
  --registry-username $acrName `
  --registry-password $acrPassword `
  --target-port 8080 `
  --ingress external `
  --cpu 0.5 `
  --memory 1.0Gi `
  --min-replicas 0 `
  --max-replicas 3 `
  --env-vars `
    OddsApi__BaseUrl="https://api.the-odds-api.com/v4/" `
    OddsApi__ApiKey="299c1c2345b1c6cf161598dd4b5aa8be" `
    OddsApi__Regions="us" `
    OddsApi__Markets="h2h,spreads,totals" `
    OddsApi__OddsFormat="american" `
    OddsApi__CacheSeconds="30"

# ── 7. Print public URL ───────────────────────────────────────────────────────
Write-Host "`n✅ Deployment complete!" -ForegroundColor Green
$fqdn = az containerapp show `
  --resource-group $rgName `
  --name $appName `
  --query "properties.configuration.ingress.fqdn" `
  --output tsv

Write-Host "🌐 App URL: https://$fqdn" -ForegroundColor Yellow