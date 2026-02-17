#!/usr/bin/env pwsh
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Config (tailored for BiteTheBookie)
$RG = "BiteTheBookie"
$APP = "bitethebookie-app-20260216193509"
$ACR_NAME = "bitethebookie20260216193956"     # Registry name (no domain)
$ACR_LOGIN_SERVER = "$ACR_NAME.azurecr.io"    # bitethebookie20260216193956.azurecr.io
$SubscriptionId = $env:SUBSCRIPTION_ID        # Optional: set before running

function Write-Log([string]$Message) {
  Write-Host "[$(Get-Date -Format o)] $Message"
}

function Invoke-AzCli {
  param(
    [Parameter(Mandatory)][string]$ArgsLine
  )
  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = "az"
  $psi.Arguments = $ArgsLine
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError  = $true
  $psi.UseShellExecute = $false
  $proc = [System.Diagnostics.Process]::Start($psi)
  $stdout = $proc.StandardOutput.ReadToEnd()
  $stderr = $proc.StandardError.ReadToEnd()
  $proc.WaitForExit()
  if ($proc.ExitCode -ne 0) {
    throw "az $ArgsLine failed ($($proc.ExitCode)): $stderr"
  }
  return $stdout.Trim()
}

# Ensure az is available
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
  throw "Azure CLI (az) is not installed or not in PATH."
}

Write-Log "Ensuring Azure CLI components/providers..."
# Container Apps extension (ignore error if already present)
try { Invoke-AzCli "extension add -n containerapp --upgrade -y" } catch { }
try { Invoke-AzCli "provider register -n Microsoft.App" } catch { }
try { Invoke-AzCli "provider register -n Microsoft.OperationalInsights" } catch { }

if ($SubscriptionId) {
  Write-Log "Setting subscription to $SubscriptionId ..."
  Invoke-AzCli "account set -s $SubscriptionId" | Out-Null
}

Write-Log "Validating resources exist..."
Invoke-AzCli "group show -n $RG" | Out-Null
Invoke-AzCli "containerapp show -g $RG -n $APP" | Out-Null
Invoke-AzCli "acr show -n $ACR_NAME" | Out-Null

Write-Log "Enabling system-assigned managed identity on Container App..."
Invoke-AzCli "containerapp identity assign -g $RG -n $APP --system-assigned" | Out-Null

Write-Log "Fetching app managed identity principalId..."
$AppMiPrincipalId = ""
for ($i = 0; $i -lt 10; $i++) {
  try {
    $AppMiPrincipalId = Invoke-AzCli "containerapp show -g $RG -n $APP --query identity.principalId -o tsv"
  } catch { $AppMiPrincipalId = "" }
  if ($AppMiPrincipalId) { break }
  Start-Sleep -Seconds 3
}
if (-not $AppMiPrincipalId) {
  throw "Could not retrieve principalId for the app's managed identity."
}
Write-Log "App MI principalId: $AppMiPrincipalId"

Write-Log "Granting AcrPull on ACR to the app identity (idempotent)..."
$AcrId = Invoke-AzCli "acr show -n $ACR_NAME --query id -o tsv"
$existing = Invoke-AzCli "role assignment list --assignee $AppMiPrincipalId --role AcrPull --scope $AcrId --query [].id -o tsv"
if ([string]::IsNullOrWhiteSpace($existing)) {
  Invoke-AzCli "role assignment create --assignee $AppMiPrincipalId --role AcrPull --scope $AcrId" | Out-Null
  Write-Log "AcrPull role assignment created."
} else {
  Write-Log "AcrPull role assignment already exists."
}

Write-Log "Waiting briefly for RBAC propagation..."
Start-Sleep -Seconds 20

Write-Log "Configuring registry on the Container App to use system identity..."
Invoke-AzCli "containerapp registry set -g $RG -n $APP --server $ACR_LOGIN_SERVER --identity system" | Out-Null

Write-Log "Restarting the current revision to force re-pull..."
try { Invoke-AzCli "containerapp revision restart -g $RG -n $APP" | Out-Null } catch { Write-Log "Restart warning: $($_.Exception.Message)" }

# Optional verification: confirm the image exists in ACR
Write-Log "Verifying that the configured image exists in ACR..."
$image = Invoke-AzCli "containerapp show -g $RG -n $APP --query properties.template.containers[0].image -o tsv"
$repoPath = $image.Substring($ACR_LOGIN_SERVER.Length + 1)
$repo = $repoPath.Split(':')[0]
$tag = $repoPath.Split(':')[-1]
$existsOutput = ""
$exists = $true
try {
  $existsOutput = Invoke-AzCli "acr repository show -n $ACR_NAME --image $repo`:$tag"
} catch {
  $exists = $false
}
if ($exists) {
  Write-Log "Image $ACR_LOGIN_SERVER/$repo`:$tag found in ACR."
} else {
  Write-Log "WARNING: Image $ACR_LOGIN_SERVER/$repo`:$tag not found in ACR."
}

Write-Log "Done. If UNAUTHORIZED persists, wait up to 5 minutes and check revision status:"
Write-Host "  az containerapp revision list -g `"$RG`" -n `"$APP`" --query `"[].{name:name,active:active,health:properties.healthState,reason:properties.conditions}`" -o table"