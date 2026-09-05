# BiteTheBookie

Sports betting analysis and picks platform built with **ASP.NET Core 10.0**.

---

## CI/CD Pipeline

The repository ships with two GitHub Actions workflows:

| Workflow | File | Trigger |
|----------|------|---------|
| **CI** | `.github/workflows/ci.yml` | Push or PR → `master` |
| **Deploy to Azure Container Apps** | `.github/workflows/deploy-bitethebookie.yml` | Push or manual dispatch → `master` |

### Deployment flow

1. CI restores dependencies, builds Sass assets, builds the ASP.NET app, and runs tests when test projects exist.
2. The deploy workflow logs into Azure with GitHub OIDC.
3. Azure resources are bootstrapped if missing: resource group, ACR, Container Apps environment, and the Container App.
4. The app image is built in ACR and tagged with the commit SHA and `latest`.
5. Runtime secrets are written into the Azure Container App secret store and consumed through `secretref:` environment variables.
6. The deployment waits for the latest revision to become ready, then validates `/health/live` and `/health/ready`.
7. If deployment validation fails, the workflow emits Azure diagnostics and attempts to roll back to the previous image.

### Azure target

| Setting | Value |
|---------|-------|
| Subscription ID | `e1d9ef80-dcea-41ea-b053-ea32d3530915` |
| Resource Group | `BiteTheBookie` |
| Location | `eastus` |
| Container App | `bitethebookie-app` |
| Container Apps Environment | `btb-env` |
| Azure Container Registry | `bitethebookieregistry` (`bitethebookieregistry.azurecr.io`) |

---

## Required GitHub Secrets

Go to **Settings → Secrets and variables → Actions** and add:

| Secret name | Required | Purpose |
|-------------|----------|---------|
| `AZURE_CLIENT_ID` | Yes | Azure AD application (client) ID for GitHub OIDC |
| `AZURE_TENANT_ID` | Yes | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Yes | Azure subscription ID |
| `CONNECTION_STRING` | Yes | SQL Server connection string |
| `AZURE_OPENAI_API_KEY` | Yes | Azure OpenAI API key |
| `AZURE_OPENAI_ENDPOINT` | Yes | Azure OpenAI endpoint |
| `AZURE_OPENAI_DEPLOYMENT` | Yes | Azure OpenAI deployment name |
| `ODDS_API_KEY` | Yes | The Odds API key |
| `PAYPAL_CLIENT_ID` | No | PayPal client ID |
| `PAYPAL_CLIENT_SECRET` | No | PayPal client secret |

> The workflow no longer uses an `AZURE_CLIENT_SECRET`; Azure access is OIDC-based.

---

## One-time Azure Setup

Use `.github/setup-azure-oidc.ps1` to:

- create or confirm the Azure resource group, ACR, Container Apps environment, and app
- create the Azure AD app and federated credential for `master`
- assign `AcrPush`, `Contributor`, and `AcrPull` roles
- populate the required GitHub Actions secrets

Run it from the repository root in PowerShell after authenticating with `az login` and `gh auth login`.

---

## Runtime validation

- App liveness: `https://<container-app-fqdn>/health/live`
- App readiness: `https://<container-app-fqdn>/health/ready`

The readiness endpoint validates required configuration and database connectivity before the deployment workflow declares success.

---

## Security follow-up

Committed runtime secrets have been removed from tracked `appsettings` files. Rotate any previously committed credentials before relying on this pipeline.
