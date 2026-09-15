param location string = resourceGroup().location

param storageAccountName string
param containerRegistryName string
param containerAppsEnvironmentName string
param containerAppName string
param containerImage string

// Managed Identity
resource scanlyIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${containerAppName}-identity'
  location: location
}

// Storage
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location

  sku: {
    name: 'Standard_LRS'
  }

  kind: 'StorageV2'

  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource invoicesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'invoices'
}

// ACR
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: containerRegistryName
  location: location

  sku: {
    name: 'Basic'
  }

  properties: {
    adminUserEnabled: false
  }
}

// RBAC
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, scanlyIdentity.id, 'acr-pull')
  scope: containerRegistry

  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    )

    principalId: scanlyIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource blobContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, scanlyIdentity.id, 'blob-contributor')
  scope: storageAccount

  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
    )

    principalId: scanlyIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Container Apps
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppsEnvironmentName
  location: location
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location

  identity: {
    type: 'UserAssigned'

    userAssignedIdentities: {
      '${scanlyIdentity.id}': {}
    }
  }

  properties: {
    managedEnvironmentId: containerAppsEnvironment.id

    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }

      registries: [
        {
          server: containerRegistry.properties.loginServer
          identity: scanlyIdentity.id
        }
      ]
    }

    template: {
      containers: [
        {
          name: 'scanly'
          image: containerImage

          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: scanlyIdentity.properties.clientId
            }
            {
              name: 'Storage__BlobServiceUri'
              value: storageAccount.properties.primaryEndpoints.blob
            }
            {
              name: 'Storage__ContainerName'
              value: invoicesContainer.name
            }
          ]

          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]

      scale: {
        minReplicas: 2
        maxReplicas: 3
      }
    }
  }

  dependsOn: [
    acrPullRole
    blobContributorRole
  ]
}

output registryLoginServer string = containerRegistry.properties.loginServer
output storageBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
