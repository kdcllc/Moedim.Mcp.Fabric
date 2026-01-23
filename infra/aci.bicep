// Azure Container Instances deployment for Moedim.Mcp.Fabric
// Deploys Container Instance with system-assigned Managed Identity
// Requires pre-built image in Azure Container Registry
// Usage: az deployment group create -g <resource-group> -f infra/aci.bicep -p workspaceId=<workspace-id> acrName=<acr-name>

@description('Azure region for resources')
param location string = resourceGroup().location

@description('Container instance name')
param containerName string = 'moedim-mcp-fabric'

@description('Microsoft Fabric workspace ID (required)')
param workspaceId string

@description('Default dataset ID for queries (optional)')
param defaultDatasetId string = ''

@description('Azure Container Registry name (without .azurecr.io)')
param acrName string

@description('Container image name and tag')
param imageName string = 'moedim-mcp-fabric:latest'

@description('CPU cores for the container')
param cpuCores int = 1

@description('Memory in GB for the container')
param memoryInGb int = 1

@description('DNS name label for public IP')
param dnsNameLabel string = containerName

// Reference existing ACR
resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' existing = {
  name: acrName
}

// Container Instance with system-assigned Managed Identity
resource containerGroup 'Microsoft.ContainerInstance/containerGroups@2023-05-01' = {
  name: containerName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    containers: [
      {
        name: containerName
        properties: {
          image: '${acr.properties.loginServer}/${imageName}'
          resources: {
            requests: {
              cpu: cpuCores
              memoryInGB: memoryInGb
            }
          }
          ports: [
            {
              port: 5000
              protocol: 'TCP'
            }
          ]
          environmentVariables: [
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
          livenessProbe: {
            httpGet: {
              path: '/health'
              port: 5000
            }
            initialDelaySeconds: 10
            periodSeconds: 30
            failureThreshold: 3
          }
          readinessProbe: {
            httpGet: {
              path: '/health'
              port: 5000
            }
            initialDelaySeconds: 5
            periodSeconds: 10
            failureThreshold: 3
          }
        }
      }
    ]
    osType: 'Linux'
    restartPolicy: 'Always'
    ipAddress: {
      type: 'Public'
      ports: [
        {
          port: 5000
          protocol: 'TCP'
        }
      ]
      dnsNameLabel: dnsNameLabel
    }
    imageRegistryCredentials: [
      {
        server: acr.properties.loginServer
        identity: 'system'
      }
    ]
  }
}

// Role assignment for ACI to pull from ACR
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerGroup.id, acr.id, 'acrpull')
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d') // AcrPull
    principalId: containerGroup.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Outputs
output containerFqdn string = containerGroup.properties.ipAddress.fqdn
output containerIpAddress string = containerGroup.properties.ipAddress.ip
output mcpEndpoint string = 'http://${containerGroup.properties.ipAddress.fqdn}:5000/mcp'
output managedIdentityPrincipalId string = containerGroup.identity.principalId
