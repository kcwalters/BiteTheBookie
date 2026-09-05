# =============================================================================
# Azure + GitHub setup for the hardened Azure Container Apps workflow
# (.github/workflows/deploy-bitethebookie.yml)
#
# Handles:
#   1. Ensuring the Azure resource group, ACR, Container Apps environment, and app exist
#   2. Creating the Azure AD app + OIDC federated credential for GitHub Actions
#   3. Pushing the required GitHub repository secrets
#
# Prerequisites:
#   - Azure CLI: az login
#   - GitHub CLI: gh auth login
#   - Run from the repo root in PowerShell.
# =============================================================================

$ErrorActionPreference = 'Stop'

$SubscriptionId   = az account show --query id -o tsv
$ResourceGroup    = 'BiteTheBookie'
$Location         = 'eastus'
$AcrName          = 'bitethebookieregistry'
$ContainerAppName = 'bitethebookie-app'
$ContainerEnvName = 'btb-env'
$GitHubRepo       = 'kcwalters/BiteTheBookie'
$GitHubBranch     = 'master'
$AadAppName       = 'bitethebookie-github-oidc'

Write-Host "Using subscription: $SubscriptionId" -ForegroundColor Cyan
az account set --subscription $SubscriptionId

Write-Host "`n[1/3] Ensuring Azure resources exist..." -ForegroundColor Green

az group create --name $ResourceGroup --location $Location | Out-Null

if (-not (az acr show --name $AcrName --resource-group $ResourceGroup 2>$null)) {
    Write-Host "Creating ACR $AcrName..."
    az acr create --name $AcrName --resource-group $ResourceGroup --location $Location --sku Basic --admin-enabled false | Out-Null
}

az extension add --name containerapp --upgrade --only-show-errors | Out-Null
az provider register --namespace Microsoft.App --wait | Out-Null
az provider register --namespace Microsoft.OperationalInsights --wait | Out-Null

if (-not (az containerapp env show --name $ContainerEnvName --resource-group $ResourceGroup 2>$null)) {
    Write-Host "Creating Container Apps environment $ContainerEnvName..."
    az containerapp env create --name $ContainerEnvName --resource-group $ResourceGroup --location $Location | Out-Null
}

if (-not (az containerapp show --name $ContainerAppName --resource-group $ResourceGroup 2>$null)) {
    Write-Host "Creating Container App $ContainerAppName..."
    az containerapp create `
        --name $ContainerAppName `
        --resource-group $ResourceGroup `
        --environment $ContainerEnvName `
        --image "mcr.microsoft.com/k8se/quickstart:latest" `
        --target-port 8080 `
        --ingress external `
        --min-replicas 1 `
        --max-replicas 3 `
        --cpu 0.5 `
        --memory 1.0Gi | Out-Null
}

Write-Host "`n[2/3] Configuring Azure AD app + OIDC federated credential..." -ForegroundColor Green

$appId = az ad app list --display-name $AadAppName --query "[0].appId" -o tsv
if (-not $appId) {
    $appId = az ad app create --display-name $AadAppName --query appId -o tsv
    Write-Host "Created AAD app $AadAppName ($appId)"
}

$spId = az ad sp list --filter "appId eq '$appId'" --query "[0].id" -o tsv
if (-not $spId) {
    az ad sp create --id $appId | Out-Null
    Write-Host "Created service principal for $appId"
}

$acrId = az acr show --name $AcrName --resource-group $ResourceGroup --query id -o tsv
az role assignment create --assignee $appId --role "AcrPush" --scope $acrId --only-show-errors 2>$null | Out-Null
az role assignment create --assignee $appId --role "Contributor" `
    --scope "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup" --only-show-errors 2>$null | Out-Null

az containerapp identity assign --name $ContainerAppName --resource-group $ResourceGroup --system-assigned | Out-Null
$caPrincipalId = az containerapp identity show --name $ContainerAppName --resource-group $ResourceGroup --query principalId -o tsv
az role assignment create --assignee-object-id $caPrincipalId --assignee-principal-type ServicePrincipal --role "AcrPull" --scope $acrId --only-show-errors 2>$null | Out-Null
az containerapp registry set --name $ContainerAppName --resource-group $ResourceGroup `
    --server "$AcrName.azurecr.io" --identity system | Out-Null

$subject = "repo:${GitHubRepo}:ref:refs/heads/$GitHubBranch"
$existing = az ad app federated-credential list --id $appId --query "[?subject=='$subject'] | [0].id" -o tsv
if (-not $existing) {
    $fic = @{
        name      = "github-$GitHubBranch"
        issuer    = "https://token.actions.githubusercontent.com"
        subject   = $subject
        audiences = @("api://AzureADTokenExchange")
    } | ConvertTo-Json -Compress
    $fic | az ad app federated-credential create --id $appId --parameters "@-" | Out-Null
    Write-Host "Created federated credential for $subject"
}

$tenantId = az account show --query tenantId -o tsv

Write-Host "`n[3/3] Setting GitHub repository secrets..." -ForegroundColor Green

gh secret set AZURE_CLIENT_ID       --repo $GitHubRepo --body $appId
gh secret set AZURE_TENANT_ID       --repo $GitHubRepo --body $tenantId
gh secret set AZURE_SUBSCRIPTION_ID --repo $GitHubRepo --body $SubscriptionId

$sqlConn      = Read-Host "SQL connection string (CONNECTION_STRING)"
$openAiKey    = Read-Host "Azure OpenAI API key (AZURE_OPENAI_API_KEY)"
$openAiEndpt  = Read-Host "Azure OpenAI endpoint (AZURE_OPENAI_ENDPOINT)"
$openAiDeploy = Read-Host "Azure OpenAI deployment name (AZURE_OPENAI_DEPLOYMENT)"
$oddsApiKey   = Read-Host "The Odds API key (ODDS_API_KEY)"
$paypalClientId = Read-Host "PayPal client id (optional: PAYPAL_CLIENT_ID)"
$paypalClientSecret = Read-Host "PayPal client secret (optional: PAYPAL_CLIENT_SECRET)"

gh secret set CONNECTION_STRING       --repo $GitHubRepo --body $sqlConn
gh secret set AZURE_OPENAI_API_KEY    --repo $GitHubRepo --body $openAiKey
gh secret set AZURE_OPENAI_ENDPOINT   --repo $GitHubRepo --body $openAiEndpt
gh secret set AZURE_OPENAI_DEPLOYMENT --repo $GitHubRepo --body $openAiDeploy
gh secret set ODDS_API_KEY            --repo $GitHubRepo --body $oddsApiKey

if ($paypalClientId) {
    gh secret set PAYPAL_CLIENT_ID --repo $GitHubRepo --body $paypalClientId
}

if ($paypalClientSecret) {
    gh secret set PAYPAL_CLIENT_SECRET --repo $GitHubRepo --body $paypalClientSecret
}

Write-Host "`nDone. GitHub Actions is configured to deploy using:" -ForegroundColor Yellow
Write-Host "  Resource group:      $ResourceGroup"
Write-Host "  Location:            $Location"
Write-Host "  Registry:            $AcrName"
Write-Host "  Container env:       $ContainerEnvName"
Write-Host "  Container app:       $ContainerAppName"
