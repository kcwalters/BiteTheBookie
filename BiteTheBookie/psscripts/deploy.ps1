$tenantId       = "2e7660ae-d674-4c1b-93fc-d2dceeb36812"
$subscriptionId = "e1d9ef80-dcea-41ea-b053-ea32d3530915"
$rgName         = "BiteTheBookie"
$location       = "eastus"
$envName        = "btb-env"
$appName        = "bitethebookie-app"
$acrName        = "bitethebookieregistry"
$acrLogin       = "$acrName.azurecr.io"
$imageName      = "bitethebookie"
$imageTag       = git rev-parse --short HEAD
$repoRoot       = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$dockerfilePath = Join-Path $repoRoot "BiteTheBookie/Dockerfile"

$requiredSecrets = @{
    CONNECTION_STRING       = $env:CONNECTION_STRING
    AZURE_OPENAI_API_KEY    = $env:AZURE_OPENAI_API_KEY
    AZURE_OPENAI_ENDPOINT   = $env:AZURE_OPENAI_ENDPOINT
    AZURE_OPENAI_DEPLOYMENT = $env:AZURE_OPENAI_DEPLOYMENT
    ODDS_API_KEY            = $env:ODDS_API_KEY
}

$missingSecrets = $requiredSecrets.GetEnumerator() | Where-Object { [string]::IsNullOrWhiteSpace($_.Value) } | ForEach-Object Key
if ($missingSecrets) {
    throw "Missing required environment variables: $($missingSecrets -join ', ')"
}

$envVars = @(
    "ConnectionStrings__DefaultConnection=secretref:connection-string",
    "AzureOpenAI__ApiKey=secretref:azure-openai-api-key",
    "AzureOpenAI__Endpoint=secretref:azure-openai-endpoint",
    "AzureOpenAI__DeploymentName=secretref:azure-openai-deployment",
    "OddsApi__ApiKey=secretref:odds-api-key",
    "ForwardedHeaders__TrustAllProxies=true"
)

$secrets = @(
    "connection-string=$($env:CONNECTION_STRING)",
    "azure-openai-api-key=$($env:AZURE_OPENAI_API_KEY)",
    "azure-openai-endpoint=$($env:AZURE_OPENAI_ENDPOINT)",
    "azure-openai-deployment=$($env:AZURE_OPENAI_DEPLOYMENT)",
    "odds-api-key=$($env:ODDS_API_KEY)"
)

if ($env:PAYPAL_CLIENT_ID -and $env:PAYPAL_CLIENT_SECRET) {
    $secrets += "paypal-client-id=$($env:PAYPAL_CLIENT_ID)"
    $secrets += "paypal-client-secret=$($env:PAYPAL_CLIENT_SECRET)"
    $envVars += "PayPal__ClientId=secretref:paypal-client-id"
    $envVars += "PayPal__ClientSecret=secretref:paypal-client-secret"
}

Write-Host "Logging in to Azure..." -ForegroundColor Cyan
az login --tenant $tenantId | Out-Null
az account set --subscription $subscriptionId

Write-Host "Ensuring Azure Container Apps prerequisites..." -ForegroundColor Cyan
az extension add --name containerapp --upgrade --only-show-errors | Out-Null
az provider register --namespace Microsoft.App --wait | Out-Null
az provider register --namespace Microsoft.OperationalInsights --wait | Out-Null
az group create --name $rgName --location $location --only-show-errors | Out-Null

if (-not (az acr show --name $acrName --resource-group $rgName 2>$null)) {
    az acr create --name $acrName --resource-group $rgName --location $location --sku Basic --admin-enabled false --only-show-errors | Out-Null
}

if (-not (az containerapp env show --name $envName --resource-group $rgName 2>$null)) {
    az containerapp env create --name $envName --resource-group $rgName --location $location --only-show-errors | Out-Null
}

if (-not (az containerapp show --name $appName --resource-group $rgName 2>$null)) {
    az containerapp create `
        --name $appName `
        --resource-group $rgName `
        --environment $envName `
        --image "mcr.microsoft.com/k8se/quickstart:latest" `
        --target-port 8080 `
        --ingress external `
        --min-replicas 1 `
        --max-replicas 3 `
        --cpu 0.5 `
        --memory 1.0Gi `
        --only-show-errors | Out-Null
}

az containerapp identity assign --name $appName --resource-group $rgName --system-assigned --only-show-errors | Out-Null
$acrId = az acr show --name $acrName --resource-group $rgName --query id -o tsv
$principalId = az containerapp identity show --name $appName --resource-group $rgName --query principalId -o tsv
az role assignment create --assignee-object-id $principalId --assignee-principal-type ServicePrincipal --role AcrPull --scope $acrId --only-show-errors 2>$null | Out-Null
az containerapp registry set --name $appName --resource-group $rgName --server $acrLogin --identity system --only-show-errors | Out-Null

Write-Host "Building image in ACR..." -ForegroundColor Cyan
az acr build `
    --registry $acrName `
    --image "$imageName:$imageTag" `
    --image "$imageName:latest" `
    --file $dockerfilePath `
    $repoRoot

$deploymentImage = "$acrLogin/$imageName:$imageTag"
$previousImage = az containerapp show --resource-group $rgName --name $appName --query "properties.template.containers[0].image" --output tsv

try {
    Write-Host "Updating Container App secrets..." -ForegroundColor Cyan
    az containerapp secret set --name $appName --resource-group $rgName --secrets $secrets --only-show-errors | Out-Null
    az containerapp revision set-mode --name $appName --resource-group $rgName --mode single --only-show-errors | Out-Null

    Write-Host "Deploying image $deploymentImage..." -ForegroundColor Cyan
    az containerapp update `
        --resource-group $rgName `
        --name $appName `
        --image $deploymentImage `
        --revision-suffix "sha-$imageTag" `
        --min-replicas 1 `
        --max-replicas 3 `
        --cpu 0.5 `
        --memory 1.0Gi `
        --set-env-vars $envVars `
        --only-show-errors | Out-Null

    $expectedRevision = az containerapp show --resource-group $rgName --name $appName --query "properties.latestRevisionName" --output tsv
    $readyRevision = ""

    for ($attempt = 1; $attempt -le 30; $attempt++) {
        $readyRevision = az containerapp show --resource-group $rgName --name $appName --query "properties.latestReadyRevisionName" --output tsv 2>$null
        if ($readyRevision -and $readyRevision -eq $expectedRevision) {
            break
        }

        Write-Host "Waiting for revision $expectedRevision to become ready ($attempt/30)..." -ForegroundColor Yellow
        Start-Sleep -Seconds 10
    }

    if (-not $readyRevision -or $readyRevision -ne $expectedRevision) {
        throw "Latest revision did not become ready."
    }

    $fqdn = az containerapp show --resource-group $rgName --name $appName --query "properties.configuration.ingress.fqdn" --output tsv
    if (-not $fqdn) {
        throw "Container App ingress FQDN was not found."
    }

    Invoke-WebRequest -Uri "https://$fqdn/health/live" -UseBasicParsing | Out-Null
    Invoke-WebRequest -Uri "https://$fqdn/health/ready" -UseBasicParsing | Out-Null

    Write-Host "`nDeployment succeeded: https://$fqdn" -ForegroundColor Green
}
catch {
    Write-Warning "Deployment failed: $($_.Exception.Message)"

    if ($previousImage -and $previousImage -ne $deploymentImage) {
        Write-Warning "Rolling back to $previousImage"
        az containerapp update `
            --resource-group $rgName `
            --name $appName `
            --image $previousImage `
            --revision-suffix "rollback-$imageTag" `
            --set-env-vars $envVars `
            --only-show-errors | Out-Null
    }

    az containerapp show --resource-group $rgName --name $appName
    az containerapp revision list --resource-group $rgName --name $appName
    throw
}
