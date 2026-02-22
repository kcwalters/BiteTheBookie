$tenantId = "2e7660ae-d674-4c1b-93fc-d2dceeb36812"
$subscriptionId = "e1d9ef80-dcea-41ea-b053-ea32d3530915"
$rgName = "BiteTheBookie"
$appName = "bitethebookie-app-20260216193509"

Write-Host "Logging in to Azure..." -ForegroundColor Cyan
az login --tenant $tenantId | Out-Null
az account set --subscription $subscriptionId

Write-Host "Configuring OddsApi settings for Container App: $appName" -ForegroundColor Cyan

# For Container App
az containerapp update `
  --resource-group $rgName `
  --name $appName `
  --set-env-vars `
    OddsApi__BaseUrl="https://api.the-odds-api.com/v4/" `
    OddsApi__ApiKey="299c1c2345b1c6cf161598dd4b5aa8be" `
    OddsApi__Regions="us" `
    OddsApi__Markets="h2h,spreads,totals" `
    OddsApi__OddsFormat="american" `
    OddsApi__CacheSeconds="30"

Write-Host "`n✅ SUCCESS! OddsApi configuration has been applied!" -ForegroundColor Green
Write-Host "The Container App will automatically restart with the new settings." -ForegroundColor Green
Write-Host "Wait a few minutes for the deployment to complete, then check /Odds/NFL or /Odds/NBA" -ForegroundColor Yellow