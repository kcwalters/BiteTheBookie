<#
.SYNOPSIS
	Creates the PayPal billing product and subscription plans (Pro + AllAccess) for BiteTheBookie
	and activates them, then prints the resulting plan IDs to paste into appsettings.json.

.DESCRIPTION
	Uses the PayPal REST API directly. Targets either the sandbox or live environment via the
	-Environment switch. IMPORTANT: the ClientId/ClientSecret must belong to the SAME environment
	you target here, and the resulting P-... plan IDs only work with that same account+environment.
	Paste both the plan IDs AND the matching "Environment" value into appsettings so they stay consistent.

.EXAMPLE
	# Sandbox (development) plans - use your sandbox REST app credentials
	./create-paypal-plans.ps1 -Environment sandbox -ClientId "Axxxx" -ClientSecret "Exxxx"

.EXAMPLE
	# Live (production) plans - use your live REST app credentials
	./create-paypal-plans.ps1 -Environment live -ClientId "Axxxx" -ClientSecret "Exxxx" -ProPrice 9.99 -AllAccessPrice 19.99
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)] [string] $ClientId,
	[Parameter(Mandatory = $true)] [string] $ClientSecret,
	[ValidateSet("sandbox", "live")]
	[string] $Environment = "sandbox",
	[string] $CurrencyCode = "USD",
	[decimal] $ProPrice = 9.99,
	[decimal] $AllAccessPrice = 19.99
)

$ErrorActionPreference = "Stop"

$baseUrl = if ($Environment -eq "live") { "https://api-m.paypal.com" } else { "https://api-m.sandbox.paypal.com" }
Write-Host "Targeting PayPal environment ($Environment): $baseUrl" -ForegroundColor Cyan
Write-Host "NOTE: the ClientId/ClientSecret must be your $Environment credentials." -ForegroundColor Yellow

# 1) OAuth token ------------------------------------------------------------
$basic = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${ClientId}:${ClientSecret}"))
$token = (Invoke-RestMethod -Method Post -Uri "$baseUrl/v1/oauth2/token" `
		-Headers @{ Authorization = "Basic $basic" } `
		-Body @{ grant_type = "client_credentials" }).access_token
Write-Host "Authenticated with PayPal." -ForegroundColor Green

$authHeaders = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }

# 2) Product ----------------------------------------------------------------
$productBody = @{
	name        = "BiteTheBookie Membership"
	description = "BiteTheBookie subscription memberships"
	type        = "SERVICE"
	category    = "SOFTWARE"
} | ConvertTo-Json

$product = Invoke-RestMethod -Method Post -Uri "$baseUrl/v1/catalogs/products" `
	-Headers $authHeaders -Body $productBody
Write-Host "Created product: $($product.id)" -ForegroundColor Green

# Helper to create + activate a plan ---------------------------------------
function New-Plan {
	param([string] $Name, [decimal] $Price)

	$planBody = @{
		product_id           = $product.id
		name                 = $Name
		status               = "ACTIVE"
		billing_cycles       = @(
			@{
				frequency      = @{ interval_unit = "MONTH"; interval_count = 1 }
				tenure_type    = "REGULAR"
				sequence       = 1
				total_cycles   = 0
				pricing_scheme = @{
					fixed_price = @{ value = ("{0:N2}" -f $Price); currency_code = $CurrencyCode }
				}
			}
		)
		payment_preferences  = @{
			auto_bill_outstanding     = $true
			setup_fee_failure_action  = "CONTINUE"
			payment_failure_threshold = 1
		}
	} | ConvertTo-Json -Depth 10

	$plan = Invoke-RestMethod -Method Post -Uri "$baseUrl/v1/billing/plans" `
		-Headers $authHeaders -Body $planBody
	Write-Host "Created plan '$Name': $($plan.id) (status $($plan.status))" -ForegroundColor Green
	return $plan.id
}

# 3) Plans ------------------------------------------------------------------
$proPlanId = New-Plan -Name "Pro" -Price $ProPrice
$allAccessPlanId = New-Plan -Name "AllAccess" -Price $AllAccessPrice

# 4) Output -----------------------------------------------------------------
Write-Host ""
Write-Host "==================================================================" -ForegroundColor Yellow
Write-Host " Paste these into appsettings.json / appsettings.Development.json:" -ForegroundColor Yellow
Write-Host "==================================================================" -ForegroundColor Yellow
$snippet = @{
	PayPal = @{
		Environment = $Environment
		PlanId      = @{
			Pro       = $proPlanId
			AllAccess = $allAccessPlanId
		}
	}
} | ConvertTo-Json -Depth 5
Write-Host $snippet
