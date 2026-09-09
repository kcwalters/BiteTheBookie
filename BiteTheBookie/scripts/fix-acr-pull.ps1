<#
.SYNOPSIS
	Fixes the Azure Container App -> ACR image pull failure:
		ImagePullUnauthorized / UNAUTHORIZED: authentication required
	for image bitethebookieregistry.azurecr.io/bitethebookie:latest

.DESCRIPTION
	Grants the Container App's system-assigned managed identity the AcrPull
	role on the container registry and points the Container App registry
	configuration at that identity (no stored passwords). Then restarts a
	revision so the new pull credentials take effect.

.PARAMETER ResourceGroup
	Resource group containing the Container App and (optionally) the registry.

.PARAMETER ContainerAppName
	Name of the Azure Container App. Default: bitethebookiecontainerapp

.PARAMETER RegistryName
	ACR resource name (not the login server). Default: bitethebookieregistry

.EXAMPLE
	./fix-acr-pull.ps1 -ResourceGroup "my-rg"

.NOTES
	Requires: Azure CLI (az) and 'az login' with rights to assign roles and
	manage the Container App. Role assignment requires Owner or User Access
	Administrator on the registry scope.
#>

param(
	[Parameter(Mandatory = $true)]
	[string]$ResourceGroup,

	[string]$ContainerAppName = "bitethebookiecontainerapp",
	[string]$RegistryName     = "bitethebookieregistry"
)

$ErrorActionPreference = "Stop"
$loginServer = "$RegistryName.azurecr.io"

function Invoke-Az {
	param([string[]]$Args)
	Write-Host "az $($Args -join ' ')" -ForegroundColor DarkGray
	$result = az @Args
	if ($LASTEXITCODE -ne 0) {
		throw "az command failed (exit $LASTEXITCODE): az $($Args -join ' ')"
	}
	return $result
}

Write-Host "== Step 1: Ensure system-assigned managed identity ==" -ForegroundColor Cyan
Invoke-Az @("containerapp","identity","assign",
	"--name",$ContainerAppName,
	"--resource-group",$ResourceGroup,
	"--system-assigned") | Out-Null

Write-Host "== Step 2: Resolve identity principalId and registry id ==" -ForegroundColor Cyan
$principalId = Invoke-Az @("containerapp","identity","show",
	"--name",$ContainerAppName,
	"--resource-group",$ResourceGroup,
	"--query","principalId","-o","tsv")

$acrId = Invoke-Az @("acr","show",
	"--name",$RegistryName,
	"--query","id","-o","tsv")

Write-Host "  principalId : $principalId"
Write-Host "  registryId  : $acrId"

Write-Host "== Step 3: Grant AcrPull to the identity ==" -ForegroundColor Cyan
# Idempotent: ignore error if the assignment already exists.
try {
	Invoke-Az @("role","assignment","create",
		"--assignee",$principalId,
		"--role","AcrPull",
		"--scope",$acrId) | Out-Null
}
catch {
	Write-Warning "Role assignment may already exist; continuing. ($_ )"
}

Write-Host "== Step 4: Point Container App registry at the identity ==" -ForegroundColor Cyan
Invoke-Az @("containerapp","registry","set",
	"--name",$ContainerAppName,
	"--resource-group",$ResourceGroup,
	"--server",$loginServer,
	"--identity","system") | Out-Null

Write-Host "== Step 5: Verify the :latest tag exists ==" -ForegroundColor Cyan
$tags = Invoke-Az @("acr","repository","show-tags",
	"--name",$RegistryName,
	"--repository","bitethebookie",
	"-o","tsv")
if (($tags -split "`n") -notcontains "latest") {
	Write-Warning "Tag 'latest' was not found in repository 'bitethebookie'. Ensure the image was pushed."
} else {
	Write-Host "  Found 'latest' tag." -ForegroundColor Green
}

Write-Host "== Step 6: Restart a revision to apply new pull credentials ==" -ForegroundColor Cyan
$revision = Invoke-Az @("containerapp","revision","list",
	"--name",$ContainerAppName,
	"--resource-group",$ResourceGroup,
	"--query","[0].name","-o","tsv")
if ($revision) {
	Invoke-Az @("containerapp","revision","restart",
		"--name",$ContainerAppName,
		"--resource-group",$ResourceGroup,
		"--revision",$revision) | Out-Null
}

Write-Host "Done. The Container App should now pull $loginServer/bitethebookie:latest successfully." -ForegroundColor Green
