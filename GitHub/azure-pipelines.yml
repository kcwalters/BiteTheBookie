trigger:
  branches:
    include:
      - master

pool:
  vmImage: ubuntu-latest

variables:
  rgName: "BiteTheBookie"
  location: "eastus"
  acrName: "bitethebookieregistry"
  acrLogin: "bitethebookieregistry.azurecr.io"
  envName: "btb-env"
  appName: "bitethebookie-app"
  imageName: "bitethebookie"
  imageTag: "$(Build.SourceVersion)"

stages:
  - stage: Build
    displayName: "Build validation"
    jobs:
      - job: Validate
        displayName: "Build app and assets"
        steps:
          - task: UseDotNet@2
            inputs:
              packageType: sdk
              version: 10.0.x

          - task: NodeTool@0
            inputs:
              versionSpec: 22.x

          - script: npm ci
            workingDirectory: BiteTheBookie
            displayName: "Install frontend dependencies"

          - script: npm run sass:build
            workingDirectory: BiteTheBookie
            displayName: "Build frontend assets"

          - script: dotnet restore "$(Build.SourcesDirectory)/BiteTheBookie/BiteTheBookie.csproj"
            displayName: "Restore .NET dependencies"

          - script: dotnet build "$(Build.SourcesDirectory)/BiteTheBookie/BiteTheBookie.csproj" --configuration Release --no-restore
            displayName: "Build application"

  - stage: Deploy
    displayName: "Deploy to Azure Container Apps"
    dependsOn: Build
    jobs:
      - deployment: DeployToACA
        displayName: "Deploy Container App"
        environment: production
        strategy:
          runOnce:
            deploy:
              steps:
                - task: AzureCLI@2
                  displayName: "Ensure Azure prerequisites"
                  inputs:
                    azureSubscription: "BiteTheBookie-ServiceConnection"
                    scriptType: bash
                    scriptLocation: inlineScript
                    inlineScript: |
                      set -euo pipefail

                      az extension add --name containerapp --upgrade --only-show-errors
                      az provider register --namespace Microsoft.App --wait
                      az provider register --namespace Microsoft.OperationalInsights --wait

                      az group create --name $(rgName) --location $(location) --only-show-errors

                      if ! az acr show --name $(acrName) --resource-group $(rgName) --only-show-errors >/dev/null 2>&1; then
                        az acr create \
                          --name $(acrName) \
                          --resource-group $(rgName) \
                          --location $(location) \
                          --sku Basic \
                          --admin-enabled false \
                          --only-show-errors
                      fi

                      if ! az containerapp env show --name $(envName) --resource-group $(rgName) --only-show-errors >/dev/null 2>&1; then
                        az containerapp env create \
                          --name $(envName) \
                          --resource-group $(rgName) \
                          --location $(location) \
                          --only-show-errors
                      fi

                      if ! az containerapp show --name $(appName) --resource-group $(rgName) --only-show-errors >/dev/null 2>&1; then
                        az containerapp create \
                          --name $(appName) \
                          --resource-group $(rgName) \
                          --environment $(envName) \
                          --image mcr.microsoft.com/k8se/quickstart:latest \
                          --target-port 8080 \
                          --ingress external \
                          --min-replicas 1 \
                          --max-replicas 3 \
                          --cpu 0.5 \
                          --memory 1.0Gi \
                          --only-show-errors
                      fi

                      az containerapp identity assign --name $(appName) --resource-group $(rgName) --system-assigned --only-show-errors >/dev/null
                      ACR_ID=$(az acr show --name $(acrName) --resource-group $(rgName) --query id -o tsv)
                      CA_PRINCIPAL_ID=$(az containerapp identity show --name $(appName) --resource-group $(rgName) --query principalId -o tsv)
                      az role assignment create --assignee-object-id "$CA_PRINCIPAL_ID" --assignee-principal-type ServicePrincipal --role AcrPull --scope "$ACR_ID" --only-show-errors >/dev/null 2>&1 || true
                      az containerapp registry set --name $(appName) --resource-group $(rgName) --server $(acrLogin) --identity system --only-show-errors >/dev/null

                - task: AzureCLI@2
                  displayName: "Build image in ACR"
                  inputs:
                    azureSubscription: "BiteTheBookie-ServiceConnection"
                    scriptType: bash
                    scriptLocation: inlineScript
                    inlineScript: |
                      set -euo pipefail
                      az acr build \
                        --registry $(acrName) \
                        --image $(imageName):$(imageTag) \
                        --image $(imageName):latest \
                        --file BiteTheBookie/Dockerfile \
                        "$(Build.SourcesDirectory)"

                - task: AzureCLI@2
                  displayName: "Deploy and validate"
                  env:
                    CONNECTION_STRING: $(CONNECTION_STRING)
                    AZURE_OPENAI_API_KEY: $(AZURE_OPENAI_API_KEY)
                    AZURE_OPENAI_ENDPOINT: $(AZURE_OPENAI_ENDPOINT)
                    AZURE_OPENAI_DEPLOYMENT: $(AZURE_OPENAI_DEPLOYMENT)
                    ODDS_API_KEY: $(ODDS_API_KEY)
                    PAYPAL_CLIENT_ID: $(PAYPAL_CLIENT_ID)
                    PAYPAL_CLIENT_SECRET: $(PAYPAL_CLIENT_SECRET)
                  inputs:
                    azureSubscription: "BiteTheBookie-ServiceConnection"
                    scriptType: bash
                    scriptLocation: inlineScript
                    inlineScript: |
                      set -euo pipefail

                      required_vars=(
                        CONNECTION_STRING
                        AZURE_OPENAI_API_KEY
                        AZURE_OPENAI_ENDPOINT
                        AZURE_OPENAI_DEPLOYMENT
                        ODDS_API_KEY
                      )

                      for var_name in "${required_vars[@]}"; do
                        if [ -z "${!var_name:-}" ]; then
                          echo "Missing required pipeline variable: $var_name" >&2
                          exit 1
                        fi
                      done

                      DEPLOYMENT_IMAGE="$(acrLogin)/$(imageName):$(imageTag)"
                      PREVIOUS_IMAGE="$(az containerapp show --name $(appName) --resource-group $(rgName) --query 'properties.template.containers[0].image' -o tsv 2>/dev/null || true)"

                      env_vars=(
                        ConnectionStrings__DefaultConnection=secretref:connection-string
                        AzureOpenAI__ApiKey=secretref:azure-openai-api-key
                        AzureOpenAI__Endpoint=secretref:azure-openai-endpoint
                        AzureOpenAI__DeploymentName=secretref:azure-openai-deployment
                        OddsApi__ApiKey=secretref:odds-api-key
                      )

                      secrets=(
                        "connection-string=$CONNECTION_STRING"
                        "azure-openai-api-key=$AZURE_OPENAI_API_KEY"
                        "azure-openai-endpoint=$AZURE_OPENAI_ENDPOINT"
                        "azure-openai-deployment=$AZURE_OPENAI_DEPLOYMENT"
                        "odds-api-key=$ODDS_API_KEY"
                      )

                      if [ -n "${PAYPAL_CLIENT_ID:-}" ] && [ -n "${PAYPAL_CLIENT_SECRET:-}" ]; then
                        secrets+=("paypal-client-id=$PAYPAL_CLIENT_ID" "paypal-client-secret=$PAYPAL_CLIENT_SECRET")
                        env_vars+=(PayPal__ClientId=secretref:paypal-client-id PayPal__ClientSecret=secretref:paypal-client-secret)
                      fi

                      cleanup() {
                        local exit_code=$?
                        trap - EXIT
                        if [ "$exit_code" -ne 0 ] && [ -n "$PREVIOUS_IMAGE" ] && [ "$PREVIOUS_IMAGE" != "$DEPLOYMENT_IMAGE" ]; then
                          az containerapp update \
                            --name $(appName) \
                            --resource-group $(rgName) \
                            --image "$PREVIOUS_IMAGE" \
                            --revision-suffix "rollback-$(Build.BuildId)" \
                            --set-env-vars "${env_vars[@]}" \
                            --only-show-errors || true
                        fi
                        exit "$exit_code"
                      }
                      trap cleanup EXIT

                      az containerapp secret set --name $(appName) --resource-group $(rgName) --secrets "${secrets[@]}" --only-show-errors >/dev/null
                      az containerapp revision set-mode --name $(appName) --resource-group $(rgName) --mode single --only-show-errors
                      az containerapp update \
                        --name $(appName) \
                        --resource-group $(rgName) \
                        --image "$DEPLOYMENT_IMAGE" \
                        --revision-suffix "sha-$(Build.SourceVersion)" \
                        --min-replicas 1 \
                        --max-replicas 3 \
                        --cpu 0.5 \
                        --memory 1.0Gi \
                        --set-env-vars "${env_vars[@]}" \
                        --only-show-errors

                      EXPECTED_REVISION=$(az containerapp show --name $(appName) --resource-group $(rgName) --query properties.latestRevisionName -o tsv)
                      for attempt in {1..30}; do
                        READY_REVISION=$(az containerapp show --name $(appName) --resource-group $(rgName) --query properties.latestReadyRevisionName -o tsv 2>/dev/null || true)
                        if [ -n "$READY_REVISION" ] && [ "$READY_REVISION" = "$EXPECTED_REVISION" ]; then
                          break
                        fi
                        sleep 10
                      done

                      READY_REVISION=$(az containerapp show --name $(appName) --resource-group $(rgName) --query properties.latestReadyRevisionName -o tsv 2>/dev/null || true)
                      if [ -z "$READY_REVISION" ] || [ "$READY_REVISION" != "$EXPECTED_REVISION" ]; then
                        echo "Latest revision did not become ready." >&2
                        exit 1
                      fi

                      FQDN=$(az containerapp show --resource-group $(rgName) --name $(appName) --query properties.configuration.ingress.fqdn --output tsv)
                      curl --fail --silent --show-error --retry 12 --retry-all-errors --retry-delay 10 "https://$FQDN/health/live" >/dev/null
                      curl --fail --silent --show-error --retry 12 --retry-all-errors --retry-delay 10 "https://$FQDN/health/ready" >/dev/null
                      echo "Deployment succeeded: https://$FQDN"
