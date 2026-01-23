// Azure Container Apps deployment for Moedim.Mcp.Fabric
// Deploys Container App with system-assigned Managed Identity
// Usage: az deployment group create -g <resource-group> -f infra/aca.bicep -p workspaceId=<workspace-id>

@description('Azure region for resources')
param location string = resourceGroup().location

@description('Name prefix for resources')
param namePrefix string = 'moedim-mcp'

@description('Microsoft Fabric workspace ID (required)')
param workspaceId string

@description('Default dataset ID for queries (optional)')
param defaultDatasetId string = ''

@description('Container image to deploy (use with pre-built image)')
param containerImage string = ''

@description('Container App Environment name')
param environmentName string = '${namePrefix}-env'

@description('Container App name')
param containerAppName string = '${namePrefix}-fabric'

// Log Analytics workspace for Container Apps Environment
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${namePrefix}-logs'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Container Apps Environment
resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: environmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// Container App with system-assigned Managed Identity
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 5000
        transport: 'http'
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: 'moedim-mcp-fabric'
          // Use provided image or placeholder for source deployment
          image: empty(containerImage) ? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest' : containerImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'Fabric__WorkspaceId'
              value: workspaceId
            }
            {
              name: 'Fabric__DefaultDatasetId'
              value: defaultDatasetId
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 5000
              }
              initialDelaySeconds: 10
              periodSeconds: 30
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 5000
              }
              initialDelaySeconds: 5
              periodSeconds: 10
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 3
        rules: [
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '100'
              }
            }
          }
        ]
      }
    }
  }
}

// Outputs
output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output mcpEndpoint string = 'https://${containerApp.properties.configuration.ingress.fqdn}/mcp'
output managedIdentityPrincipalId string = containerApp.identity.principalId
output managedIdentityClientId string = containerApp.identity.tenantId
