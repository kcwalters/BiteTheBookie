# Minimal PowerShell version using Azure CLI commands

$RG = "BiteTheBookie"
$APP = "bitethebookie-app-20260216193509"
$ACR_NAME = "bitethebookie20260216193956"
$ACR_LOGIN_SERVER = "$ACR_NAME.azurecr.io"

# Optional: target a subscription if set
if ($env:SUBSCRIPTION_ID) { az account set -s $env:SUBSCRIPTION_ID }

# 1) Enable system-assigned identity
az containerapp identity assign -g $RG -n $APP --system-assigned

# 2) Grant AcrPull on ACR to the app’s identity
$APP_MI_PRINCIPAL_ID = az containerapp show -g $RG -n $APP --query identity.principalId -o tsv
$ACR_ID = az acr show -n $ACR_NAME --query id -o tsv
az role assignment create --assignee $APP_MI_PRINCIPAL_ID --role "AcrPull" --scope $ACR_ID

# 3) Configure identity-based registry auth
az containerapp registry set -g $RG -n $APP --server $ACR_LOGIN_SERVER --identity system

# 4) Restart the latest revision to force a re-pull (revision name is required)
$REVISION = az containerapp revision list -g $RG -n $APP --query "[0].name" -o tsv
if ($REVISION) {
  az containerapp revision restart -g $RG -n $APP --revision $REVISION
}