
# BiteTheBookie

Sports betting analysis and picks platform built with **ASP.NET Core 10.0**.

---

## CI/CD Pipeline

The repository ships with two GitHub Actions workflows:

| Workflow | File | Trigger |
|----------|------|---------|
| **CI** – build & test | `.github/workflows/ci.yml` | Push or PR → `master` |
| **Deploy to Azure Container Apps** | `.github/workflows/deploy-aca.yml` | Push → `master` |

### Azure target

| Setting | Value |
|---------|-------|
| Subscription ID | `e1d9ef80-dcea-41ea-b053-ea32d3530915` |
| Resource Group | `BiteTheBookie` |
| Location | `centralus` |
| Container App | `BiteTheBookie` |
| Container Apps Environment | `bitethebookiecontainerapp` |
| Azure Container Registry | `bitethebookieregistry` (`bitethebookieregistry.azurecr.io`) |

---

## Required GitHub Secrets

Go to **Settings → Secrets and variables → Actions → New repository secret** and add each of the following:

| Secret name | Description |
|-------------|-------------|
| `AZURE_CLIENT_ID` | Service principal Application (client) ID |
| `AZURE_TENANT_ID` | Azure Active Directory tenant (directory) ID |
| `AZURE_CLIENT_SECRET` | Service principal client secret value |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID (`e1d9ef80-dcea-41ea-b053-ea32d3530915`) |

---

## One-time Azure Setup

### 1. Create a Service Principal

```bash
# Replace <YOUR_SP_NAME> with a descriptive name (e.g. "btb-github-deploy")
az ad sp create-for-rbac \
  --name "<YOUR_SP_NAME>" \
  --sdk-auth \
  --output json
```

Record the `clientId`, `clientSecret`, and `tenantId` from the output and add them as GitHub secrets (see above).

### 2. Assign least-privilege roles

```bash
SUBSCRIPTION_ID="e1d9ef80-dcea-41ea-b053-ea32d3530915"
RESOURCE_GROUP="BiteTheBookie"
ACR_NAME="bitethebookieregistry"
SP_CLIENT_ID="<paste-clientId-from-step-1>"

# Allow the SP to push images to ACR
az role assignment create \
  --assignee "$SP_CLIENT_ID" \
  --role "AcrPush" \
  --scope "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.ContainerRegistry/registries/${ACR_NAME}"

# Allow the SP to create/update resources in the resource group
# (Contributor is needed to create Container Apps and environments)
az role assignment create \
  --assignee "$SP_CLIENT_ID" \
  --role "Contributor" \
  --scope "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}"
```

> **Least-privilege note:** If you want finer-grained control, replace `Contributor` on the resource group with:
> - `Azure Container Apps Contributor` on the Container Apps resource, and  
> - `Managed Identity Operator` if using managed identities.
>
> For initial setup (creating the environment and app for the first time), `Contributor` on the resource group is the simplest option.

### 3. Ensure ACR exists

```bash
az acr create \
  --name bitethebookieregistry \
  --resource-group BiteTheBookie \
  --sku Basic \
  --location centralus
```

The deploy workflow creates the resource group and Container Apps environment automatically if they don't already exist.

---
