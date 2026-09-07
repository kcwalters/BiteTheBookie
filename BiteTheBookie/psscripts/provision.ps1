<#
	One-time provisioning for BiteTheBookie in Azure.

	Creates ALL resources in the WaltersSoftwareEngineering subscription under a
	new resource group, builds the container image, and creates the Container App.

	Run once from an account with Owner (or Contributor + User Access
	Administrator) on the subscription. Pass the app secrets as parameters:

		./provision.ps1 `
		  -ConnectionString "Server=...;Database=...;" `
		  -OpenAiApiKey "..." `
		  -OddsApiKey "..." `
		  -SeedEmail "admin@example.com" `
		  -SeedPassword "..."

	After it finishes, the app is live and future pushes to master will deploy
	new revisions via the GitHub Actions workflow.
#>

param(
	[Parameter(Mandatory = $true)][string]$ConnectionString,
	[Parameter(Mandatory = $true)][string]$OpenAiApiKey,
	[Parameter(Mandatory = $true)][string]$OddsApiKey,
	[Parameter(Mandatory = $true)][string]$SeedEmail,
	[Parameter(Mandatory = $true)][string]$SeedPassword
)

$ErrorActionPreference = "Stop"

# ─── Configuration ────────────────────────────────────────────────────────────
$SubscriptionId = "e1d9ef80-dcea-41ea-b053-ea32d3530915"   # WaltersSoftwareEngineering
$Location       = "centralus"
$ResourceGroup  = "bitethebookierg"
$AcrName        = "bitethebookieregistry"                   # login server: bitethebookieregistry.azurecr.io
$IdentityName   = "btb-identity"
$EnvName        = "btb-env"
$AppName        = "bitethebookie-app"
$ImageName      = "bitethebookie"
$ImageTag       = "initial"
$TargetPort     = 8080

# Object id of the GitHub Actions OIDC service principal (AZURE_CLIENT_ID's SP).
$CiPrincipalObjectId = "f193dfb7-8ccd-46e0-a999-860e761e2f6b"

# Path to the repo root (this script lives in BiteTheBookie/psscripts).
$RepoRoot   = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$Dockerfile = "BiteTheBookie/Dockerfile"
# ──────────────────────────────────────────────────────────────────────────────

Write-Host "Selecting subscription $SubscriptionId..." -ForegroundColor Cyan
az account set --subscription $SubscriptionId

Write-Host "Creating resource group $ResourceGroup in $Location..." -ForegroundColor Cyan
az group create --name $ResourceGroup --location $Location | Out-Null

Write-Host "Creating Azure Container Registry $AcrName..." -ForegroundColor Cyan
az acr create --resource-group $ResourceGroup --name $AcrName --sku Basic --location $Location | Out-Null

Write-Host "Creating user-assigned managed identity $IdentityName..." -ForegroundColor Cyan
az identity create --resource-group $ResourceGroup --name $IdentityName --location $Location | Out-Null

$IdentityId          = az identity show --resource-group $ResourceGroup --name $IdentityName --query "id" -o tsv
$IdentityPrincipalId = az identity show --resource-group $ResourceGroup --name $IdentityName --query "principalId" -o tsv
$AcrId               = az acr show --name $AcrName --resource-group $ResourceGroup --query "id" -o tsv

Write-Host "Granting AcrPull to the managed identity (for image pulls)..." -ForegroundColor Cyan
az role assignment create --assignee-object-id $IdentityPrincipalId --assignee-principal-type ServicePrincipal --scope $AcrId --role AcrPull | Out-Null

Write-Host "Granting AcrPush to the CI OIDC principal (for az acr build)..." -ForegroundColor Cyan
az role assignment create --assignee-object-id $CiPrincipalObjectId --assignee-principal-type ServicePrincipal --scope $AcrId --role AcrPush | Out-Null

Write-Host "Creating Container Apps environment $EnvName..." -ForegroundColor Cyan
az containerapp env create --name $EnvName --resource-group $ResourceGroup --location $Location | Out-Null

Write-Host "Building image via ACR (server-side)..." -ForegroundColor Cyan
Push-Location $RepoRoot
try {
	az acr build `
	  --registry $AcrName `
	  --subscription $SubscriptionId `
	  --image "$($ImageName):$ImageTag" `
	  --image "$($ImageName):latest" `
	  --file $Dockerfile `
	  .
}
finally {
	Pop-Location
}

$Image = "$AcrName.azurecr.io/$($ImageName):$ImageTag"

Write-Host "Creating Container App $AppName..." -ForegroundColor Cyan
az containerapp create `
  --name $AppName `
  --resource-group $ResourceGroup `
  --environment $EnvName `
  --image $Image `
  --target-port $TargetPort `
  --ingress external `
  --user-assigned $IdentityId `
  --registry-server "$AcrName.azurecr.io" `
  --registry-identity $IdentityId `
  --secrets `
	"connection-string=$ConnectionString" `
	"openai-api-key=$OpenAiApiKey" `
	"odds-api-key=$OddsApiKey" `
	"seed-email=$SeedEmail" `
	"seed-password=$SeedPassword" `
  --env-vars `
	"ConnectionStrings__DefaultConnection=secretref:connection-string" `
	"AzureOpenAI__ApiKey=secretref:openai-api-key" `
	"OddsApi__ApiKey=secretref:odds-api-key" `
	"SeedAdmin__Email=secretref:seed-email" `
	"SeedAdmin__Password=secretref:seed-password" | Out-Null

az containerapp revision set-mode --name $AppName --resource-group $ResourceGroup --mode single | Out-Null

$Fqdn = az containerapp show --name $AppName --resource-group $ResourceGroup --query "properties.configuration.ingress.fqdn" -o tsv

Write-Host "`n✅ Provisioning complete." -ForegroundColor Green
Write-Host "Resource group : $ResourceGroup"
Write-Host "Registry       : $AcrName.azurecr.io"
Write-Host "Identity id    : $IdentityId"
Write-Host "Environment    : $EnvName"
Write-Host "App URL        : https://$Fqdn"
Write-Host "`nFuture pushes to master will deploy new revisions via the workflow."
