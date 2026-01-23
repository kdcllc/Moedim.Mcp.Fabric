# Quickstart

## Installation

1. Install tools

```bash
dotnet tool restore
```

1. Restore dependencies

```bash
dotnet restore
```

## Running the MCP Server

The server supports two transport modes: **stdio** (default) and **HTTP/SSE**.

### Stdio Mode (Default)

Use stdio transport for local development with VS Code MCP extension:

```bash
dotnet run --project src/Moedim.Mcp.Fabric/Moedim.Mcp.Fabric.csproj
```

Or with explicit flag:

```bash
dotnet run --project src/Moedim.Mcp.Fabric/Moedim.Mcp.Fabric.csproj --stdio
```

### HTTP Mode

Use HTTP transport for stateless deployment or container environments:

```bash
# Default port 5000
dotnet run --project src/Moedim.Mcp.Fabric/Moedim.Mcp.Fabric.csproj --http

# Custom port
dotnet run --project src/Moedim.Mcp.Fabric/Moedim.Mcp.Fabric.csproj --http --port 8080

# Disable HTTPS redirection (useful behind reverse proxy)
dotnet run --project src/Moedim.Mcp.Fabric/Moedim.Mcp.Fabric.csproj --http --disable-https-redirect
```

### Environment Variables

Set transport and port via environment variables:

```bash
# Enable HTTP mode
UseHttp=true dotnet run --project src/Moedim.Mcp.Fabric/Moedim.Mcp.Fabric.csproj

# Set custom port
PORT=8080 dotnet run --project src/Moedim.Mcp.Fabric/Moedim.Mcp.Fabric.csproj --http
```

**Note**: Command-line arguments take precedence over environment variables.

### Get Help

```bash
dotnet run --project src/Moedim.Mcp.Fabric/Moedim.Mcp.Fabric.csproj --help
```

## Configuration

### Required Configuration

Set the Fabric workspace ID in `appsettings.json` or via environment variables:

```json
{
  "Fabric": {
    "WorkspaceId": "your-workspace-id",
    "DefaultDatasetId": "optional-default-dataset-id"
  }
}
```

Or using environment variables:

```bash
export Fabric__WorkspaceId="your-workspace-id"
export Fabric__DefaultDatasetId="optional-default-dataset-id"
```

### Authentication

The server uses `DefaultAzureCredential` for authentication. Ensure you're logged in:

```bash
az login
```

### Optional: Exclude IDE Credentials

To exclude Visual Studio and Visual Studio Code from the authentication chain (useful in container or CI/CD environments), set:

```json
{
  "Fabric": {
    "ExcludeIdeCredentials": true
  }
}
```

Or via environment variable:

```bash
export Fabric__ExcludeIdeCredentials="true"
```

## VS Code MCP Client Configuration

### Configure Stdio Transport

Copy the stdio configuration to activate stdio transport:

```bash
cp .vscode/mcp.stdio.local.json .vscode/mcp.json
```

The stdio mode is the default configuration and requires no additional setup.

### Configure HTTP Local Transport

1. Start the server in HTTP mode:

   ```bash
   dotnet run --project src/Moedim.Mcp.Fabric/Moedim.Mcp.Fabric.csproj --http
   ```

1. Copy the HTTP local configuration:

   ```bash
   cp .vscode/mcp.http.local.json .vscode/mcp.json
   ```

### Configure HTTP Container Transport

1. Start the server in a container:

   ```bash
   docker run -p 8080:5000 moedim-mcp-fabric --http
   ```

1. Copy the HTTP container configuration:

   ```bash
   cp .vscode/mcp.http.container.json .vscode/mcp.json
   ```

### Configure VS Code with dnx (packaged server)

Use the published package via `dnx` instead of building locally:

```json
{
    "servers": {
      "Moedim.Mcp.Fabric": {
        "type": "stdio",

        "command": "dnx",
        "args": [
          "Moedim.Mcp.Fabric@1.0.0",
          "--yes"
        ],
        "env": {
          "Fabric__WorkspaceId": "${input:FabricWorkSpaceId}",
          "Fabric__DefaultDatasetId": "${input:FabricDefaultDatasetId}"
        }
      }
    },
    "inputs": [
        {
            "id": "FabricWorkSpaceId",
            "type": "promptString",
            "description": "Microsoft Fabric Workspace Id"
        },
        {
            "id": "FabricDefaultDatasetId",
            "type": "promptString",
            "description": "Microsoft Fabric Default Dataset Id"
        }
    ]
}
```

Ensure your environment variables are set before launching the MCP client. You can either set them in the `env` block above or export them in your shell:

```bash
export Fabric__WorkspaceId="your-workspace-id"
export Fabric__DefaultDatasetId="optional-default-dataset-id"
```

## Available MCP Tools

The server provides the following tools for interacting with Microsoft Fabric Semantic Models:

- `query_semantic_model` - Execute DAX queries against semantic models
- `list_semantic_models` - List all available semantic models in the workspace
- `get_semantic_model_metadata` - Retrieve table/column schema for a semantic model
- `get_dataset_details` - Get detailed dataset information
- `get_dataset_datasources` - List datasources configured for a dataset
- `get_dataset_parameters` - List mashup parameters for a dataset
- `get_dataset_refresh_history` - Retrieve refresh history entries
- `get_dataset_users` - List users with access to a dataset
- `aggregate_data` - Perform aggregation operations (SUM, AVG, COUNT, MIN, MAX)
- `get_distinct_values` - Retrieve distinct values from a column

## Troubleshooting

### Authentication Issues

If you encounter authentication errors, verify you're logged into Azure CLI:

```bash
az login
az account show
```

### Configuration Errors

Ensure `Fabric:WorkspaceId` is configured:

```bash
dotnet run --project src/Moedim.Mcp.Fabric/Moedim.Mcp.Fabric.csproj
# Check startup logs for configuration validation errors
```

### HTTP Port Already in Use

If port 5000 is in use, specify a different port:

```bash
dotnet run --project src/Moedim.Mcp.Fabric/Moedim.Mcp.Fabric.csproj --http --port 8080
```

## Docker Container Deployment

The MCP server can be deployed as a Linux Docker container for local development or cloud deployment.

### Build Docker Image

Build the container image from the solution root:

```bash
docker build -t moedim-mcp-fabric:latest .
```

### Run Locally with Docker

Run the container with environment variables:

```bash
docker run -d \
  -p 5000:5000 \
  -e Fabric__WorkspaceId="your-workspace-id" \
  -e Fabric__DefaultDatasetId="your-dataset-id" \
  --name moedim-mcp-fabric \
  moedim-mcp-fabric:latest
```

### Run with Docker Compose

For local development with Azure CLI credentials:

```bash
# Set environment variables
export FABRIC_WORKSPACE_ID="your-workspace-id"
export FABRIC_DEFAULT_DATASET_ID="your-dataset-id"

# Run with docker-compose
docker-compose up -d

# Or use the dev profile to mount Azure CLI credentials
docker-compose --profile dev up -d moedim-mcp-fabric-dev
```

### Health Check

The container exposes a health endpoint at `/health`:

```bash
curl http://localhost:5000/health
```

## Azure Deployment

Deploy the MCP server to Azure using system-assigned Managed Identity for Fabric API authentication.

### Deploy to Azure Container Apps (Recommended)

Azure Container Apps provides serverless container hosting with automatic scaling.

#### Option 1: Source-to-Cloud Deployment (No Registry Setup)

Deploy directly from source code - Azure creates a managed container registry automatically:

```bash
# Login to Azure
az login

# Create resource group
az group create --name moedim-mcp-rg --location eastus

# Deploy from source (builds and deploys in one command)
az containerapp up \
  --name moedim-mcp-fabric \
  --resource-group moedim-mcp-rg \
  --source . \
  --ingress external \
  --target-port 5000 \
  --env-vars \
    Fabric__WorkspaceId="your-workspace-id" \
    Fabric__DefaultDatasetId="your-dataset-id"
```

#### Option 2: Deploy with Bicep Template

Use the provided Bicep template for more control:

```bash
# Deploy infrastructure
az deployment group create \
  --resource-group moedim-mcp-rg \
  --template-file infra/aca.bicep \
  --parameters \
    workspaceId="your-workspace-id" \
    defaultDatasetId="your-dataset-id"

# Get the MCP endpoint URL
az deployment group show \
  --resource-group moedim-mcp-rg \
  --name aca \
  --query properties.outputs.mcpEndpoint.value -o tsv
```

#### Get Managed Identity Principal ID (Optional)

After deployment, get the Managed Identity principal ID for Fabric permissions:

```bash
az containerapp show \
  --name moedim-mcp-fabric \
  --resource-group moedim-mcp-rg \
  --query identity.principalId -o tsv
```

Or pass OAuth 2 token when calling the MCP services to use on-behalf-of passthrough

### Deploy to Azure Container Instances

Azure Container Instances provides simpler container hosting with fixed resources.

#### Step 1: Create Azure Container Registry

```bash
# Create ACR
az acr create \
  --resource-group moedim-mcp-rg \
  --name moedimmcpacr \
  --sku Basic

# Enable admin access (for initial push)
az acr update --name moedimmcpacr --admin-enabled true
```

#### Step 2: Build and Push Image

```bash
# Build image in ACR (no local Docker required)
az acr build \
  --registry moedimmcpacr \
  --image moedim-mcp-fabric:latest \
  .
```

#### Step 3: Deploy with Bicep Template

```bash
# Deploy container instance
az deployment group create \
  --resource-group moedim-mcp-rg \
  --template-file infra/aci.bicep \
  --parameters \
    workspaceId="your-workspace-id" \
    defaultDatasetId="your-dataset-id" \
    acrName="moedimmcpacr"

# Get the MCP endpoint URL
az deployment group show \
  --resource-group moedim-mcp-rg \
  --name aci \
  --query properties.outputs.mcpEndpoint.value -o tsv
```

### Configure Fabric Permissions (Option)

After deployment, grant the Managed Identity access to your Fabric workspace. Use the principal ID from the deployment output.

Or pass OAuth 2 token when calling the MCP services to use on-behalf-of passthrough

**Note**: Fabric permission configuration is outside the scope of this quickstart. Refer to Microsoft Fabric documentation for granting workspace access to service principals.

### VS Code MCP Configuration for Azure

Create `.vscode/mcp.json` to connect to your deployed container:

```json
{
  "mcpServers": {
    "fabric": {
      "type": "http",
      "url": "https://your-container-app.azurecontainerapps.io/mcp"
    }
  }
}
```
