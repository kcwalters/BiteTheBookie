<#
.SYNOPSIS
	Verifies that the PayPal plan IDs in appsettings match the account/environment of the
	configured ClientId. Loads the base appsettings.json and, when an -Environment is given,
	layers the matching appsettings.{Environment}.json on top (just like ASP.NET Core does),
	so environment-specific plan IDs are checked against the base credentials.

.EXAMPLE
	# Check the base appsettings.json
	./verify-paypal-plans.ps1

.EXAMPLE
	# Check Development overrides (plan IDs) merged over base (credentials)
	./verify-paypal-plans.ps1 -Environment Development
#>
[CmdletBinding()]
param(
	[string] $BasePath = "$PSScriptRoot\..\appsettings.json",
	[string] $Environment
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BasePath)) {
	Write-Host "Base settings file not found: $BasePath" -ForegroundColor Red
	exit 1
}

# Load base PayPal settings.
$basePayPal = (Get-Content -Raw -Path $BasePath | ConvertFrom-Json).PayPal
$clientId = $basePayPal.ClientId
$secret = $basePayPal.ClientSecret
$env = $basePayPal.Environment
$proId = $basePayPal.PlanId.Pro
$allId = $basePayPal.PlanId.AllAccess
$sourceFiles = @($BasePath)

# Layer environment-specific overrides on top (mirrors ASP.NET Core config precedence).
if ($Environment) {
	$envPath = Join-Path (Split-Path -Parent $BasePath) "appsettings.$Environment.json"
	if (Test-Path $envPath) {
		$envPayPal = (Get-Content -Raw -Path $envPath | ConvertFrom-Json).PayPal
		if ($envPayPal) {
			if ($envPayPal.ClientId) { $clientId = $envPayPal.ClientId }
			if ($envPayPal.ClientSecret) { $secret = $envPayPal.ClientSecret }
			if ($envPayPal.Environment) { $env = $envPayPal.Environment }
			if ($envPayPal.PlanId.Pro) { $proId = $envPayPal.PlanId.Pro }
			if ($envPayPal.PlanId.AllAccess) { $allId = $envPayPal.PlanId.AllAccess }
			$sourceFiles += $envPath
		}
	}
	else {
		Write-Host "Environment file not found (using base only): $envPath" -ForegroundColor Yellow
	}
}

$plans = [ordered]@{ Pro = $proId; AllAccess = $allId }

$useSandbox = $true
if ($env) {
	$useSandbox = @("sandbox", "development", "test") -contains $env.ToLowerInvariant()
}
$baseUrl = if ($useSandbox) { "https://api-m.sandbox.paypal.com" } else { "https://api-m.paypal.com" }

if ([string]::IsNullOrWhiteSpace($clientId)) {
	Write-Host "No PayPal ClientId found in the merged configuration." -ForegroundColor Red
	exit 1
}

Write-Host "Config sources : $($sourceFiles -join '  +  ')"
Write-Host "Environment    : $env  ->  $baseUrl" -ForegroundColor Cyan
Write-Host "ClientId       : $($clientId.Substring(0,[Math]::Min(8,$clientId.Length)))..." -ForegroundColor Cyan
Write-Host ""

# 1) Authenticate ----------------------------------------------------------
$basic = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${clientId}:${secret}"))
try {
	$token = (Invoke-RestMethod -Method Post -Uri "$baseUrl/v1/oauth2/token" `
			-Headers @{ Authorization = "Basic $basic" } `
			-Body @{ grant_type = "client_credentials" }).access_token
	Write-Host "AUTH OK - credentials are valid for this environment." -ForegroundColor Green
}
catch {
	Write-Host "AUTH FAILED - the ClientId/ClientSecret are not valid for $baseUrl." -ForegroundColor Red
	Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
	Write-Host "  => The credentials likely belong to the OTHER environment (live vs sandbox)." -ForegroundColor Yellow
	exit 2
}

Write-Host ""
$authHeaders = @{ Authorization = "Bearer $token" }

# 2) Check each plan id ----------------------------------------------------
$anyBad = $false
foreach ($name in $plans.Keys) {
	$id = $plans[$name]
	if ([string]::IsNullOrWhiteSpace($id)) {
		Write-Host "$name : (no plan id configured)" -ForegroundColor Yellow
		$anyBad = $true
		continue
	}
	try {
		$plan = Invoke-RestMethod -Method Get -Uri "$baseUrl/v1/billing/plans/$id" -Headers $authHeaders
		$color = if ($plan.status -eq "ACTIVE") { "Green" } else { "Yellow" }
		Write-Host "$name : $id  =>  $($plan.status)" -ForegroundColor $color
		if ($plan.status -ne "ACTIVE") { $anyBad = $true }
	}
	catch {
		Write-Host "$name : $id  =>  NOT FOUND in this account/environment" -ForegroundColor Red
		$anyBad = $true
	}
}

Write-Host ""
if ($anyBad) {
	Write-Host "RESULT: One or more plans are missing/inactive for these credentials." -ForegroundColor Red
	Write-Host "Fix: create the plans in the SAME account+environment as the ClientId, then" -ForegroundColor Yellow
	Write-Host "     paste the new P-... ids (and matching Environment) into $SettingsPath." -ForegroundColor Yellow
	exit 3
}
else {
	Write-Host "RESULT: All configured plans exist and are ACTIVE. Configuration is consistent." -ForegroundColor Green
}
