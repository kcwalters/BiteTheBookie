# =============================================================================
# create-sandbox-plans.ps1
#
# Creates PayPal SANDBOX subscription products + plans (Pro and AllAccess) and
# prints the resulting Plan IDs (P-xxxx) to paste into appsettings.Development.json.
#
# HOW TO USE:
#   1. Get your SANDBOX client id/secret from:
#      https://developer.paypal.com/dashboard/applications/sandbox
#      (My Apps & Credentials -> Sandbox tab -> your app)
#   2. Fill in $ClientId and $ClientSecret below (or pass as parameters).
#   3. Run:  ./create-sandbox-plans.ps1
#   4. Copy the two P-... plan ids into appsettings.Development.json under PayPal:PlanId.
# =============================================================================

param(
	[string]$ClientId = "YOUR-SANDBOX-CLIENT-ID",
	[string]$ClientSecret = "YOUR-SANDBOX-CLIENT-SECRET",
	[string]$ProProce = "9.99",
	[string]$AllAccessPrice = "19.99",
	[string]$Currency = "USD"
)

$ErrorActionPreference = "Stop"
$BaseUrl = "https://api-m.sandbox.paypal.com"

if ($ClientId -like "YOUR-*" -or $ClientSecret -like "YOUR-*") {
	Write-Error "Please set your sandbox ClientId and ClientSecret (edit the script or pass -ClientId/-ClientSecret)."
	return
}

Write-Host "Requesting sandbox access token..." -ForegroundColor Cyan
$pair = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("$($ClientId):$($ClientSecret)"))
$token = (Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/oauth2/token" `
	-Headers @{ Authorization = "Basic $pair" } `
	-Body "grant_type=client_credentials" `
	-ContentType "application/x-www-form-urlencoded").access_token
Write-Host "  OK" -ForegroundColor Green

$authHeader = @{ Authorization = "Bearer $token" }

Write-Host "Creating product..." -ForegroundColor Cyan
$productBody = @{
	name        = "BiteTheBookie Membership"
	description = "BiteTheBookie subscription membership"
	type        = "SERVICE"
	category    = "SOFTWARE"
} | ConvertTo-Json
$product = Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/catalogs/products" `
	-Headers $authHeader -ContentType "application/json" -Body $productBody
Write-Host "  Product ID: $($product.id)" -ForegroundColor Green

function New-Plan {
	param([string]$ProductId, [string]$Name, [string]$Price)

	$planObj = @{
		product_id          = $ProductId
		name                = $Name
		status              = "ACTIVE"
		billing_cycles      = @(
			@{
				frequency      = @{ interval_unit = "MONTH"; interval_count = 1 }
				tenure_type    = "REGULAR"
				sequence       = 1
				total_cycles   = 0
				pricing_scheme = @{ fixed_price = @{ value = $Price; currency_code = $Currency } }
			}
		)
		payment_preferences = @{
			auto_bill_outstanding     = $true
			setup_fee_failure_action  = "CONTINUE"
			payment_failure_threshold = 3
		}
	}
	$planBody = $planObj | ConvertTo-Json -Depth 10
	$plan = Invoke-RestMethod -Method Post -Uri "$BaseUrl/v1/billing/plans" `
		-Headers $authHeader -ContentType "application/json" -Body $planBody
	return $plan.id
}

Write-Host "Creating Pro plan..." -ForegroundColor Cyan
$proPlanId = New-Plan -ProductId $product.id -Name "Pro Monthly" -Price $ProProce
Write-Host "  Pro Plan ID: $proPlanId" -ForegroundColor Green

Write-Host "Creating AllAccess plan..." -ForegroundColor Cyan
$allAccessPlanId = New-Plan -ProductId $product.id -Name "AllAccess Monthly" -Price $AllAccessPrice
Write-Host "  AllAccess Plan ID: $allAccessPlanId" -ForegroundColor Green

Write-Host ""
Write-Host "=============================================================" -ForegroundColor Yellow
Write-Host " Paste this into appsettings.Development.json under PayPal:" -ForegroundColor Yellow
Write-Host "=============================================================" -ForegroundColor Yellow
Write-Host @"
	"PlanId": {
	  "Pro": "$proPlanId",
	  "AllAccess": "$allAccessPlanId"
	}
"@ -ForegroundColor White
