# PowerShell Deployment Script for AI Profile Photo Maker Infrastructure
# Solves ARM template API consumption issues with sequential deployment

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$Location = "eastus2",
    
    [Parameter(Mandatory=$true)]
    [string]$SqlAdminPassword,
    
    [Parameter(Mandatory=$true)]
    [string]$JwtSecret,
    
    [Parameter(Mandatory=$true)]
    [string]$ReplicateApiToken,
    
    [string]$AppName = "aiprofilemaker",
    [string]$Environment = "staging"
)

Write-Host "🚀 Starting AI Profile Photo Maker infrastructure deployment" -ForegroundColor Green
Write-Host "📍 Resource Group: $ResourceGroupName" -ForegroundColor Yellow
Write-Host "📍 Location: $Location" -ForegroundColor Yellow
Write-Host "📍 Environment: $Environment" -ForegroundColor Yellow

try {
    # Generate unique suffix for resources
    $chars = @('0','1','2','3','4','5','6','7','8','9','a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z')
    $uniqueSuffix = -join ((1..8) | ForEach-Object { Get-Random -InputObject $chars })
    
    # Define resource names
    $containerRegistryName = "${AppName}cr${uniqueSuffix}"
    $sqlServerName = "${AppName}-sql-${uniqueSuffix}"
    $storageAccountName = "${AppName}st${uniqueSuffix}"
    $keyVaultName = "apmkv${uniqueSuffix}"
    $containerEnvName = "${AppName}-env-${Environment}"
    $backendAppName = "${AppName}-api-${Environment}"
    $frontendAppName = "${AppName}-web-${Environment}"
    $applicationInsightsName = "${AppName}-ai-${Environment}"
    
    Write-Host "🏗️ Resource Names Generated:" -ForegroundColor Green
    Write-Host "  Container Registry: $containerRegistryName" -ForegroundColor Gray
    Write-Host "  SQL Server: $sqlServerName" -ForegroundColor Gray
    Write-Host "  Storage Account: $storageAccountName" -ForegroundColor Gray
    Write-Host "  Key Vault: $keyVaultName" -ForegroundColor Gray
    
    # Step 1: Create Resource Group
    Write-Host "📦 Creating Resource Group..." -ForegroundColor Green
    az group create --name $ResourceGroupName --location $Location --tags Environment=$Environment Application=AIProfileMaker
    
    # Step 1.5: Register required resource providers
    Write-Host "🔧 Registering Azure resource providers..." -ForegroundColor Green
    az provider register --namespace Microsoft.ContainerRegistry --wait
    az provider register --namespace Microsoft.App --wait
    az provider register --namespace Microsoft.ContainerService --wait
    az provider register --namespace Microsoft.OperationalInsights --wait
    
    # Step 2: Deploy Storage Account (first to avoid API consumption conflicts)
    Write-Host "💾 Creating Storage Account..." -ForegroundColor Green
    $storageResult = az storage account create `
        --name $storageAccountName `
        --resource-group $ResourceGroupName `
        --location $Location `
        --sku Standard_LRS `
        --kind StorageV2 `
        --min-tls-version TLS1_2 `
        --allow-blob-public-access true `
        --output json | ConvertFrom-Json
    
    if (-not $storageResult) {
        throw "Failed to create storage account"
    }
    
    # Get storage connection string once and cache it
    Write-Host "🔑 Retrieving storage account key..." -ForegroundColor Green
    $storageKeys = az storage account keys list --resource-group $ResourceGroupName --account-name $storageAccountName --output json | ConvertFrom-Json
    $storageKey = $storageKeys[0].value
    $storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=$storageAccountName;AccountKey=$storageKey;EndpointSuffix=core.windows.net"
    
    # Create blob container
    Write-Host "📁 Creating blob container..." -ForegroundColor Green
    az storage container create --name "profile-images" --account-name $storageAccountName --account-key $storageKey --public-access blob
    
    # Step 3: Create Container Registry
    Write-Host "🐳 Creating Container Registry..." -ForegroundColor Green
    $registryResult = az acr create `
        --name $containerRegistryName `
        --resource-group $ResourceGroupName `
        --location $Location `
        --sku Basic `
        --admin-enabled true `
        --output json | ConvertFrom-Json
    
    if (-not $registryResult) {
        throw "Failed to create container registry"
    }
    
    # Step 4: Create SQL Server and Database
    Write-Host "🗄️ Creating SQL Server..." -ForegroundColor Green
    $sqlResult = az sql server create `
        --name $sqlServerName `
        --resource-group $ResourceGroupName `
        --location $Location `
        --admin-user "sqladmin" `
        --admin-password $SqlAdminPassword `
        --output json | ConvertFrom-Json
    
    if (-not $sqlResult) {
        throw "Failed to create SQL server"
    }
    
    # Create SQL Database
    Write-Host "🗃️ Creating SQL Database..." -ForegroundColor Green
    az sql db create `
        --resource-group $ResourceGroupName `
        --server $sqlServerName `
        --name "${AppName}db" `
        --service-objective Basic `
        --max-size 2GB
    
    # Add firewall rule for Azure services
    Write-Host "🔥 Adding SQL firewall rule..." -ForegroundColor Green
    az sql server firewall-rule create `
        --resource-group $ResourceGroupName `
        --server $sqlServerName `
        --name "AllowAzureServices" `
        --start-ip-address "0.0.0.0" `
        --end-ip-address "0.0.0.0"
    
    # Step 5: Create Application Insights
    Write-Host "📊 Creating Application Insights..." -ForegroundColor Green
    $appInsightsResult = az monitor app-insights component create `
        --app $applicationInsightsName `
        --location $Location `
        --resource-group $ResourceGroupName `
        --kind web `
        --application-type web `
        --output json | ConvertFrom-Json
    
    if (-not $appInsightsResult) {
        throw "Failed to create Application Insights"
    }
    
    # Step 6: Create Key Vault
    Write-Host "🔐 Creating Key Vault..." -ForegroundColor Green
    $keyVaultResult = az keyvault create `
        --name $keyVaultName `
        --resource-group $ResourceGroupName `
        --location $Location `
        --sku standard `
        --enable-rbac-authorization true `
        --retention-days 7 `
        --output json | ConvertFrom-Json
    
    if (-not $keyVaultResult) {
        throw "Failed to create Key Vault"
    }
    
    # Store secrets in Key Vault
    Write-Host "🔑 Storing secrets in Key Vault..." -ForegroundColor Green
    $sqlConnectionString = "Server=tcp:$($sqlResult.fullyQualifiedDomainName),1433;Initial Catalog=${AppName}db;Authentication=Active Directory Default;Encrypt=True;"
    
    az keyvault secret set --vault-name $keyVaultName --name "JwtSecret" --value $JwtSecret
    az keyvault secret set --vault-name $keyVaultName --name "ReplicateApiToken" --value $ReplicateApiToken
    az keyvault secret set --vault-name $keyVaultName --name "ConnectionString" --value $sqlConnectionString
    az keyvault secret set --vault-name $keyVaultName --name "StorageConnectionString" --value $storageConnectionString
    
    # Step 7: Create Container Apps Environment
    Write-Host "🌐 Creating Container Apps Environment..." -ForegroundColor Green
    $containerEnvResult = az containerapp env create `
        --name $containerEnvName `
        --resource-group $ResourceGroupName `
        --location $Location `
        --output json | ConvertFrom-Json
    
    if (-not $containerEnvResult) {
        throw "Failed to create Container Apps Environment"
    }
    
    # Step 8: Create Backend Container App
    Write-Host "🔧 Creating Backend Container App..." -ForegroundColor Green
    $backendResult = az containerapp create `
        --name $backendAppName `
        --resource-group $ResourceGroupName `
        --environment $containerEnvName `
        --image "mcr.microsoft.com/k8se/quickstart:latest" `
        --target-port 80 `
        --ingress external `
        --cpu 0.5 `
        --memory 1Gi `
        --min-replicas 0 `
        --max-replicas 3 `
        --system-assigned `
        --secrets "jwt-secret=$JwtSecret" "replicate-token=$ReplicateApiToken" "connection-string=$sqlConnectionString" "storage-connection-string=$storageConnectionString" `
        --env-vars "ASPNETCORE_ENVIRONMENT=Staging" "ConnectionStrings__DefaultConnection=secretref:connection-string" "Jwt__Secret=secretref:jwt-secret" "Replicate__ApiToken=secretref:replicate-token" "AzureStorage__ConnectionString=secretref:storage-connection-string" "ApplicationInsights__ConnectionString=$($appInsightsResult.connectionString)" `
        --output json | ConvertFrom-Json
    
    if (-not $backendResult) {
        throw "Failed to create backend container app"
    }
    
    # Step 9: Create Frontend Container App
    Write-Host "🎨 Creating Frontend Container App..." -ForegroundColor Green
    $frontendResult = az containerapp create `
        --name $frontendAppName `
        --resource-group $ResourceGroupName `
        --environment $containerEnvName `
        --image "mcr.microsoft.com/k8se/quickstart:latest" `
        --target-port 80 `
        --ingress external `
        --cpu 0.25 `
        --memory 0.5Gi `
        --min-replicas 0 `
        --max-replicas 2 `
        --env-vars "API_URL=https://$($backendResult.properties.configuration.ingress.fqdn)" `
        --output json | ConvertFrom-Json
    
    if (-not $frontendResult) {
        throw "Failed to create frontend container app"
    }
    
    # Step 10: Configure Container Registry Access
    Write-Host "🔗 Configuring Container Registry Access..." -ForegroundColor Green
    
    # Get principal IDs
    $backendPrincipalId = $backendResult.identity.principalId
    $frontendPrincipalId = $frontendResult.identity.principalId
    
    # Assign AcrPull roles
    if ($backendPrincipalId) {
        az role assignment create --assignee $backendPrincipalId --role "AcrPull" --scope "/subscriptions/$((az account show --query id -o tsv))/resourceGroups/$ResourceGroupName/providers/Microsoft.ContainerRegistry/registries/$containerRegistryName"
        Write-Host "✅ Assigned AcrPull role to backend app" -ForegroundColor Green
    }
    
    if ($frontendPrincipalId) {
        az role assignment create --assignee $frontendPrincipalId --role "AcrPull" --scope "/subscriptions/$((az account show --query id -o tsv))/resourceGroups/$ResourceGroupName/providers/Microsoft.ContainerRegistry/registries/$containerRegistryName"
        Write-Host "✅ Assigned AcrPull role to frontend app" -ForegroundColor Green
    }
    
    # Configure Key Vault access for backend app
    Write-Host "🔐 Configuring Key Vault Access..." -ForegroundColor Green
    if ($backendPrincipalId) {
        az role assignment create --assignee $backendPrincipalId --role "Key Vault Secrets User" --scope "/subscriptions/$((az account show --query id -o tsv))/resourceGroups/$ResourceGroupName/providers/Microsoft.KeyVault/vaults/$keyVaultName"
        Write-Host "✅ Assigned Key Vault Secrets User role to backend app" -ForegroundColor Green
    }
    
    # Output deployment results
    Write-Host "🎉 Deployment completed successfully!" -ForegroundColor Green
    Write-Host "📋 Deployment Summary:" -ForegroundColor Yellow
    Write-Host "  Frontend URL: https://$($frontendResult.properties.configuration.ingress.fqdn)" -ForegroundColor Green
    Write-Host "  Backend URL: https://$($backendResult.properties.configuration.ingress.fqdn)" -ForegroundColor Green
    Write-Host "  Container Registry: $($registryResult.loginServer)" -ForegroundColor Green
    Write-Host "  SQL Server: $($sqlResult.fullyQualifiedDomainName)" -ForegroundColor Green
    Write-Host "  Storage Account: $storageAccountName" -ForegroundColor Green
    Write-Host "  Key Vault: https://$keyVaultName.vault.azure.net/" -ForegroundColor Green
    
    # Set GitHub Actions outputs (using environment file method)
    "registry-name=$containerRegistryName" >> $env:GITHUB_OUTPUT
    "registry-server=$($registryResult.loginServer)" >> $env:GITHUB_OUTPUT
    "frontend-url=https://$($frontendResult.properties.configuration.ingress.fqdn)" >> $env:GITHUB_OUTPUT
    "backend-url=https://$($backendResult.properties.configuration.ingress.fqdn)" >> $env:GITHUB_OUTPUT
    "sql-server=$($sqlResult.fullyQualifiedDomainName)" >> $env:GITHUB_OUTPUT
    "storage-account=$storageAccountName" >> $env:GITHUB_OUTPUT
    "key-vault=$keyVaultName" >> $env:GITHUB_OUTPUT
    
} catch {
    Write-Error "❌ Deployment failed: $_"
    Write-Host "🔍 Check the error details above and retry the deployment" -ForegroundColor Red
    exit 1
}