$scriptPath = Join-Path $PSScriptRoot "psscripts/deploy.ps1"

if (-not (Test-Path $scriptPath)) {
    throw "Deployment script not found: $scriptPath"
}

& $scriptPath @args

if ($LASTEXITCODE) {
    exit $LASTEXITCODE
}
