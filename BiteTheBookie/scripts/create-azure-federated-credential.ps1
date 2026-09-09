<#
.SYNOPSIS
	Creates the GitHub Actions OIDC federated identity credential on the
	Entra ID app registration used by the deploy workflow.

.DESCRIPTION
	Fixes the deploy failure:
		AADSTS70025: The client 'github-bitethebookie-deploy' has no
		configured federated identity credentials.

	The GitHub OIDC token presented by the 'production' environment job has
	subject:  repo:kcwalters/BiteTheBookie:environment:production
	This script registers a federated credential that trusts exactly that
	subject so azure/login@v2 can authenticate.

.PARAMETER AppClientId
	The Application (client) ID of the 'github-bitethebookie-deploy'
	app registration. Find it in Entra ID > App registrations.

.EXAMPLE
	./create-azure-federated-credential.ps1 -AppClientId "00000000-0000-0000-0000-000000000000"

.NOTES
	Requires: Azure CLI (az) and an interactive login (az login) with rights
	to manage the app registration.
#>

param(
	[Parameter(Mandatory = $true)]
	[string]$AppClientId,

	[string]$Organization = "kcwalters",
	[string]$Repository   = "BiteTheBookie",

	# Which OIDC subject to trust:
	#   environment -> repo:<org>/<repo>:environment:<Environment>
	#   branch      -> repo:<org>/<repo>:ref:refs/heads/<Branch>
	[ValidateSet("environment", "branch")]
	[string]$SubjectType = "environment",

	[string]$Environment = "production",
	[string]$Branch      = "master",

	[string]$CredentialName
)

$ErrorActionPreference = "Stop"

switch ($SubjectType) {
	"environment" {
		$subject = "repo:$Organization/$Repository:environment:$Environment"
		if (-not $CredentialName) { $CredentialName = "github-bitethebookie-env-$Environment" }
	}
	"branch" {
		$subject = "repo:$Organization/$Repository:ref:refs/heads/$Branch"
		if (-not $CredentialName) { $CredentialName = "github-bitethebookie-branch-$Branch" }
	}
}

Write-Host "Creating federated credential '$CredentialName'"
Write-Host "  App (client) id : $AppClientId"
Write-Host "  Subject         : $subject"

$parameters = @{
	name      = $CredentialName
	issuer    = "https://token.actions.githubusercontent.com"
	subject   = $subject
	audiences = @("api://AzureADTokenExchange")
} | ConvertTo-Json -Compress

# Write parameters to a temp file to avoid shell-quoting issues on the JSON.
$tempFile = New-TemporaryFile
try {
	Set-Content -Path $tempFile -Value $parameters -Encoding utf8

	az ad app federated-credential create `
		--id $AppClientId `
		--parameters "@$tempFile"

	if ($LASTEXITCODE -ne 0) {
		throw "az ad app federated-credential create failed with exit code $LASTEXITCODE."
	}

	Write-Host "Federated credential created successfully." -ForegroundColor Green
	Write-Host "Re-run the GitHub Actions deploy workflow to verify azure/login succeeds."
}
finally {
	Remove-Item -Path $tempFile -ErrorAction SilentlyContinue
}
