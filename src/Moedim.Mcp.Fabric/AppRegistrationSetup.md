# App Registration Setup Guide

This guide walks you through setting up Azure App Registrations required for authenticating requests to the Moedim.Mcp.Fabric MCP server and accessing Microsoft Fabric/Power BI APIs using the On-Behalf-Of (OBO) flow.

## Overview

The authentication architecture requires **two App Registrations**:

| App Registration | Purpose |
|------------------|---------|
| **fabric-mcp-server** | Exposes this MCP application as an API. Validates incoming JWT tokens and exchanges them for Fabric API tokens via OBO flow. |
| **fabric-mcp-client** | Used by client applications (e.g., AI agents, custom apps) to obtain tokens for calling the MCP server. |

## On-Behalf-Of (OBO) Token Flow

The following diagram illustrates how tokens flow through the system:

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              OBO Token Exchange Flow                            │
└─────────────────────────────────────────────────────────────────────────────────┘

┌──────────────┐         ┌──────────────────┐         ┌──────────────────┐          ┌──────────────────┐
│              │         │                  │         │                  │          │                  │
│  Client App  │         │  Azure AD        │         │  MCP Server      │          │  Microsoft       │
│  (AI Agent)  │         │  (Entra ID)      │         │  (fabric-mcp-    │          │  Fabric API      │
│              │         │                  │         │   server)        │          │                  │
└──────┬───────┘         └────────┬─────────┘         └────────┬─────────┘          └────────┬─────────┘
       │                          │                            │                             │
       │  1. Request token for    │                            │                             │
       │     fabric-mcp-server    │                            │                             │
       │     scope (access_as_user)                            │                             │
       │─────────────────────────>│                            │                             │
       │                          │                            │                             │
       │  2. Return access token  │                            │                             │
       │     (Token A)            │                            │                             │
       │<─────────────────────────│                            │                             │
       │                          │                            │                             │
       │  3. Call MCP endpoint with Bearer Token A             │                             │
       │ ─────────────────────────────────────────────────────>│                             │
       │                          │                            │                             │
       │                          │  4. Exchange Token A for   │                             │
       │                          │     Fabric API token       │                             │
       │                          │     (OBO flow)             │                             │
       │                          │<───────────────────────────│                             │
       │                          │                            │                             │
       │                          │  5. Return Fabric token    │                             │
       │                          │     (Token B)              │                             │
       │                          │───────────────────────────>│                             │
       │                          │                            │                             │
       │                          │                            │  6. Call Fabric API with    │
       │                          │                            │     Bearer Token B          │
       │                          │                            │────────────────────────────>│
       │                          │                            │                             │
       │                          │                            │                             │
       │  7. Return query results │                            │                             │
       │ <─────────────────────────────────────────────────────│<────────────────────────────│
       │                          │                            │                             │
       │                          │                            │                             │
```

**Key Points:**
- **Token A**: Issued by Azure AD for the `fabric-mcp-server` audience with the `access_as_user` scope
- **Token B**: Obtained via OBO flow, scoped for `https://analysis.windows.net/powerbi/api/.default`
- The MCP server acts as a **confidential client** using its client secret to perform the OBO exchange

## Prerequisites

- Azure subscription with permissions to create App Registrations
- Microsoft Fabric or Power BI workspace with at least Viewer access
- Global Administrator or Application Administrator role (for granting admin consent)

---

## Step 1: Create the fabric-mcp-server App Registration

This app registration represents the MCP server API and is used for both validating incoming requests and obtaining Fabric API tokens.

### 1.1 Create the App Registration

1. Navigate to the [Azure Portal](https://portal.azure.com)
2. Go to **Microsoft Entra ID** → **App registrations** → **New registration**
3. Configure the registration:
   - **Name**: `fabric-mcp-server`
   - **Supported account types**: Select **Accounts in this organizational directory only (Single tenant)**
   - **Redirect URI**: Leave blank (not needed for API)
4. Click **Register**
5. Note the following values from the **Overview** page:
   - **Application (client) ID**: `<server-client-id>`
   - **Directory (tenant) ID**: `<tenant-id>`

### 1.2 Set the Application ID URI

1. Go to **Expose an API** in the left menu
2. Click **Set** next to **Application ID URI**
3. Accept the default value `api://<server-client-id>` or customize it
4. Click **Save**
5. Note this value: `<application-id-uri>` (e.g., `api://12345678-1234-1234-1234-123456789abc`)

### 1.3 Add an API Scope

1. In **Expose an API**, click **Add a scope**
2. Configure the scope:
   - **Scope name**: `access_as_user`
   - **Who can consent?**: **Admins and users**
   - **Admin consent display name**: `Access Fabric MCP Server as user`
   - **Admin consent description**: `Allows the application to access the Fabric MCP Server on behalf of the signed-in user.`
   - **User consent display name**: `Access Fabric MCP Server`
   - **User consent description**: `Allows the application to access the Fabric MCP Server on your behalf.`
   - **State**: **Enabled**
3. Click **Add scope**

### 1.4 Add Power BI API Permissions

1. Go to **API permissions** in the left menu
2. Click **Add a permission** → **APIs my organization uses**
3. Search for and select **Power BI Service**
4. Select **Delegated permissions**
5. Check the following permissions:
   - `Dataset.Read.All` - Read all datasets
   - `Workspace.Read.All` - Read all workspaces
6. Click **Add permissions**
7. Click **Grant admin consent for [Your Organization]** (requires admin role)
8. Confirm by clicking **Yes**

### 1.5 Create a Client Secret

1. Go to **Certificates & secrets** in the left menu
2. Click **New client secret**
3. Configure the secret:
   - **Description**: `MCP Server OBO Secret`
   - **Expires**: Select an appropriate expiration (e.g., 24 months)
4. Click **Add**
5. **Immediately copy the secret value** - it won't be shown again
6. Note this value: `<client-secret>`

### 1.6 Summary - fabric-mcp-server Values

Record these values for configuration:

| Property | Value | Used In |
|----------|-------|---------|
| Application (client) ID | `<server-client-id>` | `AzureAd:ClientId` |
| Directory (tenant) ID | `<tenant-id>` | `AzureAd:TenantId`, `Authentication:AllowedTenants` |
| Client Secret | `<client-secret>` | `AzureAd:ClientSecret` |
| Application ID URI | `api://<server-client-id>` | `Authentication:ClientId` |

---

## Step 2: Create the fabric-mcp-client App Registration

This app registration is used by client applications to obtain tokens for calling the MCP server.

### 2.1 Create the App Registration

1. Navigate to **Microsoft Entra ID** → **App registrations** → **New registration**
2. Configure the registration:
   - **Name**: `fabric-mcp-client`
   - **Supported account types**: Select **Accounts in this organizational directory only (Single tenant)**
   - **Redirect URI**: Configure based on your client type:
     - **Desktop/Native app**: `http://localhost` (for development)
     - **Web app**: Your application's callback URL
     - **SPA**: Your SPA's redirect URI
3. Click **Register**
4. Note the **Application (client) ID**: `<client-client-id>`

### 2.2 Add Permission to Call fabric-mcp-server

1. Go to **API permissions** in the left menu
2. Click **Add a permission** → **My APIs**
3. Select **fabric-mcp-server**
4. Select **Delegated permissions**
5. Check `access_as_user`
6. Click **Add permissions**
7. Click **Grant admin consent for [Your Organization]**
8. Confirm by clicking **Yes**

### 2.3 Configure Authentication (for Public Clients)

If your client is a desktop app, CLI tool, or native application:

1. Go to **Authentication** in the left menu
2. Under **Advanced settings**, set **Allow public client flows** to **Yes**
3. Click **Save**

### 2.4 Create a Client Secret (for Confidential Clients)

If your client is a web application or service:

1. Go to **Certificates & secrets**
2. Click **New client secret**
3. Add a description and expiration
4. Click **Add** and copy the secret value

### 2.5 Authorize the Client in fabric-mcp-server

1. Go back to **fabric-mcp-server** App Registration
2. Navigate to **Expose an API**
3. Under **Authorized client applications**, click **Add a client application**
4. Enter the `<client-client-id>` from fabric-mcp-client
5. Check the `access_as_user` scope
6. Click **Add application**

### 2.6 Summary - fabric-mcp-client Values

Provide these values to client application developers:

| Property | Value | Purpose |
|----------|-------|---------|
| Client ID | `<client-client-id>` | Client app's identity |
| Tenant ID | `<tenant-id>` | Azure AD tenant |
| Scope | `api://<server-client-id>/access_as_user` | Request this scope to call MCP server |
| Authority | `https://login.microsoftonline.com/<tenant-id>` | Token endpoint |

---

## Step 3: Configure appsettings.json

Update the `appsettings.json` file with values from both App Registrations:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AzureAd": {
    "TenantId": "<tenant-id>",
    "ClientId": "<server-client-id>",
    "ClientSecret": "<client-secret>"
  },
  "Authentication": {
    "EnableValidation": true,
    "ClientId": "api://<server-client-id>",
    "AllowedTenants": [
      "<tenant-id>"
    ]
  },
  "Fabric": {
    "WorkspaceId": "<your-fabric-workspace-id>",
    "DefaultDatasetId": "<your-default-dataset-id>",
    "AuthenticationScopes": [
      "https://analysis.windows.net/powerbi/api/.default"
    ]
  }
}
```

### Configuration Section Reference

#### AzureAd Section

Used by the OBO flow to exchange tokens for Fabric API access.

| Property | Description | Example |
|----------|-------------|---------|
| `TenantId` | Your Azure AD tenant ID | `12345678-1234-1234-1234-123456789abc` |
| `ClientId` | fabric-mcp-server Application (client) ID | `87654321-4321-4321-4321-cba987654321` |
| `ClientSecret` | Client secret created in Step 1.5 | `abc123~secret...` |

#### Authentication Section

Used for validating incoming JWT bearer tokens.

| Property | Description | Example |
|----------|-------------|---------|
| `EnableValidation` | Set to `true` to enable JWT validation | `true` |
| `ClientId` | Application ID URI (the expected audience) | `api://87654321-4321-4321-4321-cba987654321` |
| `AllowedTenants` | Array of tenant IDs allowed to access the API | `["12345678-1234-1234-1234-123456789abc"]` |

#### Fabric Section

Used for connecting to Microsoft Fabric/Power BI.

| Property | Description | Example |
|----------|-------------|---------|
| `WorkspaceId` | Your Fabric workspace ID | `aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee` |
| `DefaultDatasetId` | (Optional) Default semantic model ID | `ffffffff-gggg-hhhh-iiii-jjjjjjjjjjjj` |
| `AuthenticationScopes` | Scopes for OBO token exchange | `["https://analysis.windows.net/powerbi/api/.default"]` |

> **Security Note**: For production deployments, store sensitive values like `ClientSecret` in Azure Key Vault or use managed identity instead of storing secrets in configuration files.

---

## Troubleshooting

### AADSTS65001: The user or administrator has not consented to use the application

**Cause**: Admin consent has not been granted for the required permissions.

**Solution**:
1. Go to **API permissions** in the fabric-mcp-server App Registration
2. Click **Grant admin consent for [Your Organization]**
3. Ensure both Power BI permissions and the client's `access_as_user` permission are consented

### AADSTS700024: Client assertion is not within its valid time range

**Cause**: The client secret has expired.

**Solution**:
1. Go to **Certificates & secrets** in fabric-mcp-server
2. Create a new client secret
3. Update `AzureAd:ClientSecret` in your configuration

### AADSTS50013: Assertion failed signature validation

**Cause**: The incoming token's audience doesn't match the configured `Authentication:ClientId`.

**Solution**:
1. Verify `Authentication:ClientId` matches the Application ID URI exactly
2. Ensure clients are requesting the correct scope: `api://<server-client-id>/access_as_user`

### AADSTS700016: Application with identifier was not found

**Cause**: The client ID or tenant ID is incorrect.

**Solution**:
1. Verify `AzureAd:TenantId` and `AzureAd:ClientId` match the values in Azure Portal
2. Ensure the App Registration exists and is not deleted

### AADSTS50105: The signed in user is not assigned to a role for the application

**Cause**: User assignment is required but the user is not assigned.

**Solution**:
1. Go to **Enterprise applications** → **fabric-mcp-server**
2. Under **Users and groups**, add the required users or groups
3. Or disable user assignment requirement in **Properties** → **Assignment required?** = **No**

### Power BI API returns 401 Unauthorized

**Cause**: The OBO token doesn't have the required permissions or the user doesn't have access to the Fabric workspace.

**Solution**:
1. Verify the user has at least Viewer access to the Fabric workspace
2. Ensure Power BI API permissions are granted admin consent
3. Check that `Fabric:AuthenticationScopes` includes `https://analysis.windows.net/powerbi/api/.default`

---

## Testing the Configuration

### Test Token Acquisition (Client Side)

Use the Azure CLI to test token acquisition:

```bash
# Login as the user
az login

# Get a token for the MCP server
az account get-access-token --resource api://<server-client-id>
```

### Test MCP Server Health

```bash
# Check the health endpoint (no authentication required)
curl https://<your-mcp-server>/health
```

### Test Authenticated MCP Call

```bash
# Get a token
TOKEN=$(az account get-access-token --resource api://<server-client-id> --query accessToken -o tsv)

# Call the MCP endpoint
curl -X POST https://<your-mcp-server>/mcp \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/list","id":1}'
```

---

## Next Steps

- Review [QUICKSTART.md](QUICKSTART.md) for running the MCP server locally
- Configure your AI agent or MCP client with the fabric-mcp-client credentials
- Set up Azure Key Vault for production secret management
