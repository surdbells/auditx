// AuditX — Azure reference topology (Container Apps).
//
// Deploys: Log Analytics, a Container Apps Environment, Azure Container Registry, a user-assigned managed
// identity, Key Vault (RBAC), Azure SQL (server + database), Azure Cache for Redis, a storage account with
// two Azure Files shares (evidence + DataProtection key ring), and two Container Apps (api, web).
//
// See docs/AZURE_DEPLOYMENT_RUNBOOK.md for the end-to-end walkthrough (build/push images, run this template,
// apply migrations, smoke-test). Adjust to the bank's landing-zone standards before production use — this
// reference topology uses public endpoints with firewall allow-lists; private endpoints / VNet integration
// for Container Apps, SQL, Redis, Storage and Key Vault are the recommended hardening for real bank traffic
// (see the runbook's "Landing-zone hardening" section).

@description('Short name prefix for all resources, e.g. auditx. Capped at 7 chars: Key Vault names allow 24 total and this template builds them as "<prefix>-kv-<13-char-uniqueString>".')
@minLength(3)
@maxLength(7)
param namePrefix string = 'auditx'

@description('Environment suffix, e.g. prod, staging, uat.')
param environmentName string = 'prod'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Azure SQL administrator login name.')
param sqlAdminLogin string = 'auditxadmin'

@secure()
@description('Azure SQL administrator password. Supply at deploy time — never commit.')
param sqlAdminPassword string

@secure()
@description('JWT signing key (>= 32 bytes, high-entropy). Supply at deploy time — never commit.')
param jwtSigningKey string

@description('Active Directory identity provider: ActiveDirectory (LDAPS) or ActiveDirectoryApi (AD REST gateway).')
@allowed(['ActiveDirectory', 'ActiveDirectoryApi'])
param identityProvider string = 'ActiveDirectory'

@description('LDAPS host (a domain controller / LDAPS VIP reachable over VPN/ExpressRoute). Required when identityProvider=ActiveDirectory.')
param activeDirectoryHost string = ''

@description('LDAPS base DN. Required when identityProvider=ActiveDirectory.')
param activeDirectoryBaseDn string = ''

@description('LDAPS service-account bind DN. Required when identityProvider=ActiveDirectory.')
param activeDirectoryServiceAccountDn string = ''

@secure()
@description('LDAPS service-account password. Required when identityProvider=ActiveDirectory.')
param activeDirectoryServiceAccountPassword string = ''

@description('UPN suffix for AD accounts, e.g. bank.local. Required when identityProvider=ActiveDirectory.')
param activeDirectoryUpnSuffix string = ''

@description('The bank AD REST gateway base URL. Required when identityProvider=ActiveDirectoryApi.')
param activeDirectoryApiBaseUrl string = ''

@secure()
@description('The bank AD REST gateway API key. Required when identityProvider=ActiveDirectoryApi.')
param activeDirectoryApiKey string = ''

@description('Container image tag to deploy (e.g. the release git SHA). The CD pipeline pushes this tag to ACR before this template runs.')
param imageTag string = 'latest'

@description('SPA origin(s) the API accepts via CORS — normally just the web Container App''s own FQDN once known, since nginx proxies /api same-origin. Leave empty on first deploy, then re-apply with the emitted webFqdn output.')
param corsOrigin string = ''

@description('API container min replicas.')
param apiMinReplicas int = 2
@description('API container max replicas.')
param apiMaxReplicas int = 10

@description('Web (nginx/SPA) container min replicas.')
param webMinReplicas int = 2
@description('Web (nginx/SPA) container max replicas.')
param webMaxReplicas int = 6

var uniqueSuffix = uniqueString(resourceGroup().id)
var apiAppName = '${namePrefix}-api'
var webAppName = '${namePrefix}-web'
var sqlDatabaseName = 'auditx'

// ---------------------------------------------------------------------------
// Observability
// ---------------------------------------------------------------------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-law-${environmentName}'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 90
  }
}

// ---------------------------------------------------------------------------
// Container Apps environment + registry
// ---------------------------------------------------------------------------
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${namePrefix}-cae-${environmentName}'
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

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: '${namePrefix}acr${uniqueSuffix}'
  location: location
  sku: { name: 'Standard' }
  properties: {
    adminUserEnabled: false
  }
}

// ---------------------------------------------------------------------------
// Managed identity (ACR pull + Key Vault secrets)
// ---------------------------------------------------------------------------
resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-id-api-${environmentName}'
  location: location
}

var acrPullRoleId = 'b24988ac-6180-42a0-ab88-20f7382dd24c' // built-in "AcrPull"
resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, apiIdentity.id, acrPullRoleId)
  scope: acr
  properties: {
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
  }
}

// ---------------------------------------------------------------------------
// Key Vault (RBAC) — secrets referenced directly by the API Container App at runtime
// ---------------------------------------------------------------------------
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${namePrefix}-kv-${uniqueSuffix}'
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
  }
}

var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6' // built-in "Key Vault Secrets User"
resource keyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, apiIdentity.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
  }
}

resource sqlConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'sql-connection-string'
  properties: {
    value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};User Id=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

resource jwtSigningKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'jwt-signing-key'
  properties: {
    value: jwtSigningKey
  }
}

resource redisConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'redis-connection-string'
  properties: {
    value: '${redisCache.properties.hostName}:6380,password=${redisCache.listKeys().primaryKey},ssl=True,abortConnect=False'
  }
}

resource adServiceAccountPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (identityProvider == 'ActiveDirectory') {
  parent: keyVault
  name: 'ad-service-account-password'
  properties: {
    value: activeDirectoryServiceAccountPassword
  }
}

resource adApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (identityProvider == 'ActiveDirectoryApi') {
  parent: keyVault
  name: 'ad-api-key'
  properties: {
    value: activeDirectoryApiKey
  }
}

// ---------------------------------------------------------------------------
// Azure SQL
// ---------------------------------------------------------------------------
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: '${namePrefix}-sql-${uniqueSuffix}'
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled' // tighten to 'Disabled' + a private endpoint per the landing zone
  }
}

resource sqlAllowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'GP_S_Gen5_2' // General Purpose Serverless, 2 vCore ceiling — right-size per the bank's data volume
    tier: 'GeneralPurpose'
  }
  properties: {
    autoPauseDelay: -1 // disable auto-pause in production (cold-start latency is undesirable for a bank workload)
    zoneRedundant: false
  }
}

// ---------------------------------------------------------------------------
// Azure Cache for Redis
// ---------------------------------------------------------------------------
resource redisCache 'Microsoft.Cache/redis@2024-03-01' = {
  name: '${namePrefix}-redis-${uniqueSuffix}'
  location: location
  properties: {
    sku: { name: 'Standard', family: 'C', capacity: 1 }
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled' // tighten with a private endpoint per the landing zone
  }
}

// ---------------------------------------------------------------------------
// Storage: evidence + DataProtection key-ring shares, mounted into the API Container App
// ---------------------------------------------------------------------------
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: '${namePrefix}st${uniqueSuffix}'
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource fileServices 'Microsoft.Storage/storageAccounts/fileServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource evidenceShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = {
  parent: fileServices
  name: 'auditx-evidence'
  properties: { shareQuota: 512 }
}

resource keyRingShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = {
  parent: fileServices
  name: 'auditx-keyring'
  properties: { shareQuota: 1 }
}

resource evidenceStorageDef 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: containerAppsEnvironment
  name: 'evidence'
  properties: {
    azureFile: {
      accountName: storageAccount.name
      accountKey: storageAccount.listKeys().keys[0].value
      shareName: evidenceShare.name
      accessMode: 'ReadWrite'
    }
  }
}

resource keyRingStorageDef 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: containerAppsEnvironment
  name: 'keyring'
  properties: {
    azureFile: {
      accountName: storageAccount.name
      accountKey: storageAccount.listKeys().keys[0].value
      shareName: keyRingShare.name
      accessMode: 'ReadWrite'
    }
  }
}

// ---------------------------------------------------------------------------
// API Container App
// ---------------------------------------------------------------------------
var identityConfig = identityProvider == 'ActiveDirectory' ? [
  { name: 'Identity__Provider', value: 'ActiveDirectory' }
  { name: 'ActiveDirectory__Host', value: activeDirectoryHost }
  { name: 'ActiveDirectory__Port', value: '636' }
  { name: 'ActiveDirectory__UseLdaps', value: 'true' }
  { name: 'ActiveDirectory__BaseDn', value: activeDirectoryBaseDn }
  { name: 'ActiveDirectory__ServiceAccountDn', value: activeDirectoryServiceAccountDn }
  { name: 'ActiveDirectory__ServiceAccountPassword', secretRef: 'ad-service-account-password' }
  { name: 'ActiveDirectory__UpnSuffix', value: activeDirectoryUpnSuffix }
] : [
  { name: 'Identity__Provider', value: 'ActiveDirectoryApi' }
  { name: 'ActiveDirectoryApi__BaseUrl', value: activeDirectoryApiBaseUrl }
  { name: 'ActiveDirectoryApi__ApiKey', secretRef: 'ad-api-key' }
]

resource apiContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false // internal-only: the web Container App reaches it same-environment by app name
        targetPort: 8080
        transport: 'http'
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: apiIdentity.id
        }
      ]
      secrets: [
        { name: 'sql-connection-string', keyVaultUrl: sqlConnectionSecret.properties.secretUri, identity: apiIdentity.id }
        { name: 'jwt-signing-key', keyVaultUrl: jwtSigningKeySecret.properties.secretUri, identity: apiIdentity.id }
        { name: 'redis-connection-string', keyVaultUrl: redisConnectionSecret.properties.secretUri, identity: apiIdentity.id }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${acr.properties.loginServer}/auditx-api:${imageTag}'
          resources: { cpu: json('1.0'), memory: '2Gi' }
          env: concat([
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_HTTP_PORTS', value: '8080' }
            { name: 'Database__MigrateOnStartup', value: 'false' }
            { name: 'ConnectionStrings__Default', secretRef: 'sql-connection-string' }
            { name: 'Jwt__SigningKey', secretRef: 'jwt-signing-key' }
            { name: 'Redis__ConnectionString', secretRef: 'redis-connection-string' }
            { name: 'Cors__Origins__0', value: corsOrigin }
            { name: 'Storage__EvidenceRoot', value: '/mnt/evidence' }
            { name: 'DataProtection__KeyRingPath', value: '/mnt/keyring' }
            { name: 'AllowedHosts', value: '*' }
          ], identityConfig)
          volumeMounts: [
            { volumeName: 'evidence', mountPath: '/mnt/evidence' }
            { volumeName: 'keyring', mountPath: '/mnt/keyring' }
          ]
          probes: [
            {
              type: 'Readiness'
              httpGet: { path: '/admin/health', port: 8080 }
              initialDelaySeconds: 10
              periodSeconds: 15
            }
            {
              type: 'Liveness'
              httpGet: { path: '/admin/health', port: 8080 }
              initialDelaySeconds: 20
              periodSeconds: 30
            }
          ]
        }
      ]
      volumes: [
        { name: 'evidence', storageType: 'AzureFile', storageName: evidenceStorageDef.name }
        { name: 'keyring', storageType: 'AzureFile', storageName: keyRingStorageDef.name }
      ]
      scale: {
        minReplicas: apiMinReplicas
        maxReplicas: apiMaxReplicas
        rules: [
          {
            name: 'http-concurrency'
            http: { metadata: { concurrentRequests: '50' } }
          }
        ]
      }
    }
  }
  dependsOn: [
    acrPullAssignment
    keyVaultRoleAssignment
  ]
}

// ---------------------------------------------------------------------------
// Web (SPA + nginx reverse proxy) Container App — same-origin proxy to the API keeps the
// HttpOnly session cookie same-site (see docker/nginx.conf.template).
// ---------------------------------------------------------------------------
resource webContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 80
        transport: 'auto'
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: apiIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: '${acr.properties.loginServer}/auditx-web:${imageTag}'
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'API_UPSTREAM', value: apiAppName }
          ]
        }
      ]
      scale: {
        minReplicas: webMinReplicas
        maxReplicas: webMaxReplicas
        rules: [
          {
            name: 'http-concurrency'
            http: { metadata: { concurrentRequests: '100' } }
          }
        ]
      }
    }
  }
  dependsOn: [
    acrPullAssignment
    apiContainerApp
  ]
}

output acrLoginServer string = acr.properties.loginServer
output apiAppName string = apiContainerApp.name
output webFqdn string = webContainerApp.properties.configuration.ingress.fqdn
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output keyVaultName string = keyVault.name
output apiIdentityPrincipalId string = apiIdentity.properties.principalId
