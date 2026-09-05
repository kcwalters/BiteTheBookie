# =============================================================================
# Azure + GitHub setup for the CI/CD pipeline (.github/workflows/ci-cd.yml)
#
# Handles all 3 required setup steps:
#   1. Discovers/confirms your Azure resource names (updates workflow env values)
#   2. Creates an Azure AD app + OIDC federated credential (step 3)
#   3. Pushes the required GitHub repository secrets (step 2)
#
# Prerequisites:
#   - Azure CLI:  az login
#   - GitHub CLI: gh auth login   (https://cli.github.com/)
#   - Run from the repo root in PowerShell.
# =============================================================================

$ErrorActionPreference = 'Stop'

# Existence checks below run "az ... show" and rely on a non-zero exit code to
# mean "not found". Newer PowerShell turns native non-zero exits into terminating
# errors, which would break those checks, so opt out of that behavior here.
$PSNativeCommandUseErrorActionPreference = $false

# ---- Guard: run az against a clean extension directory --------------------
# Works around a corrupt local 'containerapp' extension (WinError 5 / access
# denied on containerapp-*.dist-info) that otherwise breaks every az command.
# Using a separate dir lets az install a fresh extension without touching the
# locked one. Remove/reset AZURE_EXTENSION_DIR once the corrupt folder is gone.
$env:AZURE_EXTENSION_DIR = "$env:USERPROFILE\.azure\cliextensions_clean"
New-Item -ItemType Directory -Force -Path $env:AZURE_EXTENSION_DIR | Out-Null
# --------------------------------------------------------------------------

# ---- EDIT THESE to match (or create) your Azure resources ------------------
$SubscriptionId    = (az account show --query id -o tsv)
$ResourceGroup     = 'BiteTheBookie-rg'
$Location          = 'eastus'
$AcrName           = 'bitethebookieregistry'  # must be globally unique, alphanumeric
$ContainerAppName  = 'bitethebookieca'
$ContainerEnvName  = 'bitethebookie-env'     # Container Apps managed environment
$GitHubRepo        = 'kcwalters/BiteTheBookie'
$GitHubBranch      = 'master'
$AadAppName        = 'bitethebookie-github-oidc'
# ---------------------------------------------------------------------------

Write-Host "Using subscription: $SubscriptionId" -ForegroundColor Cyan
az account set --subscription $SubscriptionId

# Returns $true only if the given "az ... show" command succeeds (resource exists).
# Fully swallows output/errors and checks the exit code, so a "not found" result
# never terminates the script (regardless of $ErrorActionPreference).
function Test-AzResourceExists {
	param([Parameter(Mandatory)][string[]]$AzArgs)
	$prev = $ErrorActionPreference
	$ErrorActionPreference = 'Continue'
	& az @AzArgs *> $null
	$ok = ($LASTEXITCODE -eq 0)
	$ErrorActionPreference = $prev
	return $ok
}

# --- STEP 1: Ensure the Azure resources exist -------------------------------
Write-Host "`n[1/3] Ensuring Azure resources exist..." -ForegroundColor Green

az group create --name $ResourceGroup --location $Location | Out-Null

# Azure Container Registry
if (-not (Test-AzResourceExists @('acr','show','--name',$AcrName,'--resource-group',$ResourceGroup))) {
	Write-Host "Creating ACR $AcrName..."
	az acr create --name $AcrName --resource-group $ResourceGroup --sku Basic --admin-enabled false | Out-Null
}

# Container Apps environment
az extension add --name containerapp --upgrade --only-show-errors | Out-Null
az provider register --namespace Microsoft.App --wait | Out-Null
az provider register --namespace Microsoft.OperationalInsights --wait | Out-Null

if (-not (Test-AzResourceExists @('containerapp','env','show','--name',$ContainerEnvName,'--resource-group',$ResourceGroup))) {
	Write-Host "Creating Container Apps environment $ContainerEnvName..."
	az containerapp env create --name $ContainerEnvName --resource-group $ResourceGroup --location $Location | Out-Null
}

# Container App (initial placeholder image; the pipeline updates it on each deploy)
if (-not (Test-AzResourceExists @('containerapp','show','--name',$ContainerAppName,'--resource-group',$ResourceGroup))) {
	Write-Host "Creating Container App $ContainerAppName..."
	az containerapp create `
		--name $ContainerAppName `
		--resource-group $ResourceGroup `
		--environment $ContainerEnvName `
		--image "mcr.microsoft.com/k8se/quickstart:latest" `
		--target-port 8080 `
		--ingress external `
		--query properties.configuration.ingress.fqdn | Out-Null
}

# --- STEP 3: Azure AD app + OIDC federated credential -----------------------
Write-Host "`n[2/3] Configuring Azure AD app + OIDC federated credential..." -ForegroundColor Green

$appId = az ad app list --display-name $AadAppName --query "[0].appId" -o tsv
if (-not $appId) {
	$appId = az ad app create --display-name $AadAppName --query appId -o tsv
	Write-Host "Created AAD app $AadAppName ($appId)"
}

# Service principal
$spId = az ad sp list --filter "appId eq '$appId'" --query "[0].id" -o tsv
if (-not $spId) {
	az ad sp create --id $appId | Out-Null
	Write-Host "Created service principal for $appId"
}

# Role assignments: push images to ACR + manage the Container App
$acrId = az acr show --name $AcrName --resource-group $ResourceGroup --query id -o tsv
az role assignment create --assignee $appId --role "AcrPush" --scope $acrId --only-show-errors | Out-Null
az role assignment create --assignee $appId --role "Contributor" `
	--scope "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup" --only-show-errors | Out-Null

# User Access Administrator lets the pipeline's OIDC principal grant AcrPull to the
# Container App identity (the "Ensure ACR pull access" workflow step) on its own.
az role assignment create --assignee $appId --role "User Access Administrator" `
	--scope "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup" --only-show-errors | Out-Null

# Let the Container App pull from ACR using its system-assigned managed identity.
az containerapp identity assign --name $ContainerAppName --resource-group $ResourceGroup --system-assigned | Out-Null
$caPrincipalId = az containerapp identity show --name $ContainerAppName --resource-group $ResourceGroup --query principalId -o tsv
az role assignment create --assignee $caPrincipalId --role "AcrPull" --scope $acrId --only-show-errors | Out-Null
az containerapp registry set --name $ContainerAppName --resource-group $ResourceGroup `
	--server "$AcrName.azurecr.io" --identity system | Out-Null

# Federated credential for the master branch
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

# --- STEP 2: Push GitHub repository secrets ---------------------------------
Write-Host "`n[3/3] Setting GitHub repository secrets..." -ForegroundColor Green

gh secret set AZURE_CLIENT_ID       --repo $GitHubRepo --body $appId
gh secret set AZURE_TENANT_ID       --repo $GitHubRepo --body $tenantId
gh secret set AZURE_SUBSCRIPTION_ID --repo $GitHubRepo --body $SubscriptionId

# App runtime secrets (used by the "Configure app secrets..." workflow step).
# These prompt so values are never hard-coded into the script.
$sqlConn      = Read-Host "SQL connection string (ConnectionStrings:DefaultConnection)"
$openAiKey    = Read-Host "Azure OpenAI API key (AzureOpenAI:ApiKey)"
$openAiEndpt  = Read-Host "Azure OpenAI endpoint (AzureOpenAI:Endpoint)"
$openAiDeploy = Read-Host "Azure OpenAI deployment name (AzureOpenAI:DeploymentName)"

gh secret set SQL_CONNECTION_STRING   --repo $GitHubRepo --body $sqlConn
gh secret set AZURE_OPENAI_API_KEY    --repo $GitHubRepo --body $openAiKey
gh secret set AZURE_OPENAI_ENDPOINT   --repo $GitHubRepo --body $openAiEndpt
gh secret set AZURE_OPENAI_DEPLOYMENT  --repo $GitHubRepo --body $openAiDeploy

# --- Summary ----------------------------------------------------------------
Write-Host "`nDone. Update .github/workflows/ci-cd.yml env: values to match:" -ForegroundColor Yellow
Write-Host "  ACR_NAME:            $AcrName"
Write-Host "  IMAGE_NAME:          bitethebookie"
Write-Host "  CONTAINER_APP_NAME:  $ContainerAppName"
Write-Host "  RESOURCE_GROUP:      $ResourceGroup"
