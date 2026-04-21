@description('Environment name (dev, stg, prod)')
param environmentName string = 'dev'

@description('Location for all resources')
param location string = resourceGroup().location

@description('SQL admin login')
param sqlAdminLogin string = 'medflowadmin'

@secure()
@description('SQL admin password')
param sqlAdminPassword string

@secure()
@description('JWT signing key (32+ chars)')
param jwtKey string

var prefix = 'medflow-${environmentName}'
var appServicePlanName = '${prefix}-plan'
var webAppName = '${prefix}-api'
var sqlServerName = '${prefix}-sql'
var dbName = 'MedFlowDb'
var staticWebAppName = '${prefix}-client'

// ── App Service Plan ──────────────────────────────────────────────────────────
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: { name: 'B2', tier: 'Basic' }
  properties: { reserved: false }
}

// ── API Web App ───────────────────────────────────────────────────────────────
resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: webAppName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: environmentName == 'prod' ? 'Production' : 'Development' }
        { name: 'Jwt__Key', value: jwtKey }
        { name: 'Jwt__Issuer', value: 'MedFlowApi' }
        { name: 'Jwt__Audience', value: 'MedFlowClient' }
        { name: 'Jwt__ExpiryMinutes', value: '60' }
        { name: 'AllowedOrigins__0', value: 'https://${staticWebApp.properties.defaultHostname}' }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${dbName};User Id=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;'
          type: 'SQLAzure'
        }
      ]
    }
  }
}

// ── SQL Server ────────────────────────────────────────────────────────────────
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: dbName
  location: location
  sku: { name: 'S1', tier: 'Standard' }
}

resource sqlFirewall 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

// ── Static Web App (React client) ─────────────────────────────────────────────
resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: staticWebAppName
  location: location
  sku: { name: 'Standard', tier: 'Standard' }
  properties: {}
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output apiUrl string = 'https://${webApp.properties.defaultHostName}'
output clientUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
