# Microsoft Foundry Setup Guide

This guide walks you through configuring a deployed Moedim.Mcp.Fabric MCP server as a tool in Azure AI Foundry using OAuth Identity Passthrough with the `fabric-mcp-client` App Registration.

## Overview

Azure AI Foundry agents can connect to external MCP servers using OAuth authentication. This setup uses the `fabric-mcp-client` App Registration as a confidential client to obtain tokens for calling the MCP server, which then uses the On-Behalf-Of (OBO) flow to access Microsoft Fabric APIs.

| Component | Purpose |
|-----------|---------|
| **Azure AI Foundry Agent** | AI agent that invokes MCP tools to query Fabric semantic models |
| **fabric-mcp-client** | App Registration used by Foundry to authenticate with the MCP server |
| **Moedim.Mcp.Fabric** | Deployed MCP server that exposes Fabric query tools |
| **fabric-mcp-server** | App Registration that validates tokens and performs OBO exchange |

## Authentication Flow

The following diagram illustrates how Azure AI Foundry authenticates with the MCP server:

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                     Azure AI Foundry → MCP Server Authentication Flow              │
└─────────────────────────────────────────────────────────────────────────────────────┘

┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐
│                  │     │                  │     │                  │     │                  │
│  Azure AI        │     │  Azure AD        │     │  MCP Server      │     │  Microsoft       │
│  Foundry Agent   │     │  (Entra ID)      │     │  (Moedim.Mcp.    │     │  Fabric API      │
│                  │     │                  │     │   Fabric)        │     │                  │
└────────┬─────────┘     └────────┬─────────┘     └────────┬─────────┘     └────────┬─────────┘
         │                        │                        │                        │
         │  1. Agent invokes MCP  │                        │                        │
         │     tool in Foundry    │                        │                        │
         │                        │                        │                        │
         │  2. Request token using│                        │                        │
         │     fabric-mcp-client  │                        │                        │
         │     credentials        │                        │                        │
         │───────────────────────>│                        │                        │
         │                        │                        │                        │
         │  3. Return access token│                        │                        │
         │     (for MCP server)   │                        │                        │
         │<───────────────────────│                        │                        │
         │                        │                        │                        │
         │  4. Call MCP endpoint with Bearer token         │                        │
         │ ───────────────────────────────────────────────>│                        │
         │                        │                        │                        │
         │                        │  5. Validate token &   │                        │
         │                        │     exchange via OBO   │                        │
         │                        │<───────────────────────│                        │
         │                        │                        │                        │
         │                        │  6. Return Fabric token│                        │
         │                        │───────────────────────>│                        │
         │                        │                        │                        │
         │                        │                        │  7. Query Fabric API   │
         │                        │                        │───────────────────────>│
         │                        │                        │                        │
         │  8. Return query results                        │                        │
         │ <───────────────────────────────────────────────│<───────────────────────│
         │                        │                        │                        │
```

**Key Points:**
- Foundry uses `fabric-mcp-client` credentials (Client ID + Client Secret) stored in its configuration
- The token is scoped to `api://<server-client-id>/access_as_user`
- The MCP server validates the token and exchanges it for a Fabric API token via OBO flow
- User identity flows through the entire chain, ensuring Fabric permissions are enforced

## Prerequisites

Before proceeding, ensure you have completed the following:

- **App Registration Setup**: Complete Steps 1 and 2 in [AppRegistrationSetup.md](AppRegistrationSetup.md)
  - `fabric-mcp-server` App Registration created and configured
  - `fabric-mcp-client` App Registration created with `access_as_user` permission
- **Deployed MCP Server**: Moedim.Mcp.Fabric deployed in HTTP mode with:
  - Publicly accessible `/mcp` endpoint (e.g., `https://your-mcp-server.azurecontainerapps.io/mcp`)
  - Authentication enabled (`Authentication:EnableValidation = true`)
  - Health endpoint accessible at `/health`
- **Azure AI Foundry Access**: Access to an Azure AI Foundry workspace with permissions to configure agents
- **Fabric Permissions**: Users who will invoke MCP tools must have at least Viewer access to the target Fabric workspace

---

## Step 1: Configure fabric-mcp-client for Azure AI Foundry

Update the `fabric-mcp-client` App Registration to work with Azure AI Foundry.

### 1.1 Verify or Create Client Secret

If you haven't already created a client secret for `fabric-mcp-client`:

1. Go to **Certificates & secrets** in the left menu
2. Click **New client secret**
3. Configure the secret:
   - **Description**: `Azure AI Foundry MCP Client Secret`
   - **Expires**: Select an appropriate expiration (e.g., 24 months)
4. Click **Add**
5. **Immediately copy the secret value** - it won't be shown again
6. Note this value: `<client-secret>`

> **Important**: Store this secret securely. You will enter it in Azure AI Foundry's configuration in the next step.

### 1.2 Verify API Permissions

Confirm that `fabric-mcp-client` has the required permissions:

1. Go to **API permissions** in the left menu
2. Verify the following permission is listed and has admin consent:
   - **fabric-mcp-server** → `access_as_user` (Delegated)
3. If not present, add it:
   - Click **Add a permission** → **My APIs** → **fabric-mcp-server**
   - Select **Delegated permissions** → **access_as_user**
   - Click **Add permissions**
   - Click **Grant admin consent for [Your Organization]**

### 1.3 Summary - Values for Azure AI Foundry

Gather these values for configuring Azure AI Foundry:

| Property | Value | Description |
|----------|-------|-------------|
| Client ID | `<client-client-id>` | fabric-mcp-client Application (client) ID |
| Client Secret | `<client-secret>` | Secret created in Step 1.2 |
| Authority | `https://login.microsoftonline.com/<tenant-id>` | Token endpoint |
| Scope | `api://<server-client-id>/access_as_user` | Permission scope for MCP server |
| MCP Endpoint | `https://<your-mcp-server>/mcp` | Your deployed MCP server URL |
| Authorization endpoint | `https://login.microsoftonline.com/<your-tenant-id>/oauth2/v2.0/authorize` | OAuth 2.0 authorization endpoint (v2) from App Registration Endpoints |
| Token endpoint | `https://login.microsoftonline.com/<your-tenant-id>/oauth2/v2.0/token` | OAuth 2.0 token endpoint (v2) from App Registration Endpoints |

---

## Step 2: Add MCP Server in Azure AI Foundry

Configure the Moedim.Mcp.Fabric MCP server as a tool in Azure AI Foundry.

### 2.1 Navigate to Azure AI Foundry

1. Go to [Azure AI Foundry](https://ai.azure.com)
2. Sign in with your Azure credentials
3. Select your AI Foundry workspace (or create one if needed)

### 2.3 Create an MCP Server Tool

1. Go to **Tools** in the navigation menu on the left
2. Click **Connect a tool** → **Custom** → **Model Context Protocol (MCP)** → **Create**
3. Configure the MCP server connection:

   | Field | Value |
   |-------|-------|
   | **Name** | `Fabric Data` |
   | **Remote MCP Server Endpoint** | `https://<your-mcp-server>/mcp` |
   | **Authentication** | `OAuth Identity Passthrough` |
   | **Client ID** | `<client-client-id>` |
   | **Client Secret** | `<client-secret>` |
   | **Token URL** | `https://login.microsoftonline.com/<tenant-id>/oauth2/v2.0/token` |
   | **Auth URL** | `https://login.microsoftonline.com/<tenant-id>/oauth2/v2.0/authorize` |
   | **Refresh URL** | `https://login.microsoftonline.com/<tenant-id>/oauth2/v2.0/token` |
   | **Scope** | `api://<server-client-id>/access_as_user` |

4. Click **Connect**

**Note:** The MCP tool may take a few minutes to show up in the Foundry tools list

### 2.4 Configure Redirect Settings

1. Get Redirect URL
   - If configured correctly, the tool will have generated a **Redirect URL** between the **Remote MCP server endpoint** and **Authentication** fields on the Tool details tab
   - If it is not present, there is an error in the MCP server connection configuration from **2.3**
2. Navigate to the [Azure Portal](https://portal.azure.com)
3. Go to **Microsoft Entra ID** → **App registrations**
4. Select **fabric-mcp-client**
5. Go to **Authentication** in the left menu
6. Under **Redirect URI Confirguration**, click **Add a Redirect URI** → **Web**
7. Add the following redirect URI you got from the Fountry Tool
   ```
   example: https://global.consent.azure-apim.net/redirect/{foundry-project-id}-{your-tool-name}
   ```
8. Click **Configure**

### 2.5 Verify Tool Registration

After adding the MCP server:

1. The tool should appear in the agent's **Tools** list
2. You should see the available MCP tools listed:
   - `query_semantic_model` - Execute DAX queries
   - `list_semantic_models` - List available datasets
   - `get_semantic_model_metadata` - Get table/column schema
   - `aggregate_data` - Perform aggregations
   - `get_distinct_values` - Get unique column values
   - And additional dataset management tools

---

## Step 3: Test the Integration

Verify that the Azure AI Foundry agent can successfully call MCP tools.

### 3.1 Verify MCP Server Health

Before testing from Foundry, confirm the MCP server is accessible:

```bash
# Check health endpoint (no authentication required)
curl https://<your-mcp-server>/health
```

Expected response: `Healthy`

### 3.2 Test from Azure AI Foundry

1. Open your agent in Azure AI Foundry
2. Start a chat session with the agent
3. Ask the agent to list available semantic models:

   ```
   List the available semantic models in the Fabric workspace.
   ```

4. The agent should invoke the `list_semantic_models` tool and return a list of datasets

### 3.3 Test a DAX Query

Ask the agent to execute a simple DAX query:

```
Query the Sales semantic model and show me the total revenue.
```

The agent should:
1. Use `list_semantic_models` to find the dataset (if needed)
2. Use `query_semantic_model` with an appropriate DAX query
3. Return the results in a readable format

### 3.4 Verify User Permissions

The MCP server uses the signed-in user's identity for Fabric API calls. To verify permissions are working correctly:

1. Sign in to Azure AI Foundry as a user with Fabric workspace access
2. Execute a query - it should succeed
3. Sign in as a user without Fabric access
4. Execute a query - it should fail with a permissions error