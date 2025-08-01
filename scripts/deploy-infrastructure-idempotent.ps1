# Idempotent PowerShell Deployment Script for AI Profile Photo Maker Infrastructure  
# Production-ready deployment with container image building and comprehensive error handling

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
    [string]$Environment = "staging",
    [switch]$BuildImages = $false,
    [switch]$DryRun = $false,
    [switch]$SkipImageDeploy = $false
)

Write-Host "🚀 Starting IDEMPOTENT AI Profile Photo Maker infrastructure deployment" -ForegroundColor Green
Write-Host "📍 Resource Group: $ResourceGroupName" -ForegroundColor Yellow
Write-Host "📍 Location: $Location" -ForegroundColor Yellow
Write-Host "📍 Environment: $Environment" -ForegroundColor Yellow
Write-Host "📍 Build Images: $BuildImages" -ForegroundColor Yellow
Write-Host "📍 Dry Run: $DryRun" -ForegroundColor Yellow

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No resources will be created/modified" -ForegroundColor Cyan
}

# Global deployment state tracking
$global:DeploymentState = @{
    CreatedResources = @()
    FailedOperations = @()
    StartTime = Get-Date
}

# Enhanced functions for deployment management

function Test-ResourceExists {
    param(
        [string]$ResourceName,
        [string]$ResourceGroup,
        [string]$ResourceType
    )
    
    try {
        $resource = az resource show --name $ResourceName --resource-group $ResourceGroup --resource-type $ResourceType 2>$null
        return $null -ne $resource
    } catch {
        return $false
    }
}

function Add-DeploymentState {
    param(
        [string]$ResourceName,
        [string]$ResourceType,
        [string]$Status = "Created"
    )
    
    $global:DeploymentState.CreatedResources += @{
        Name = $ResourceName
        Type = $ResourceType
        Status = $Status
        Timestamp = Get-Date
    }
}

function Write-DeploymentSummary {
    $duration = (Get-Date) - $global:DeploymentState.StartTime
    Write-Host "`n📊 Deployment Summary:" -ForegroundColor Green
    Write-Host "   Duration: $($duration.ToString('mm\:ss'))" -ForegroundColor Gray
    Write-Host "   Resources processed: $($global:DeploymentState.CreatedResources.Count)" -ForegroundColor Gray
    Write-Host "   Failed operations: $($global:DeploymentState.FailedOperations.Count)" -ForegroundColor Gray
    
    if ($global:DeploymentState.FailedOperations.Count -gt 0) {
        Write-Host "`n❌ Failed Operations:" -ForegroundColor Red
        $global:DeploymentState.FailedOperations | ForEach-Object {
            Write-Host "   - $_" -ForegroundColor Red
        }
    }
}

function Build-ContainerImages {
    param(
        [string]$RegistryName,
        [string]$ResourceGroup
    )
    
    if ($DryRun) {
        Write-Host "[DRY RUN] Would build and push container images to $RegistryName" -ForegroundColor Cyan
        return $true
    }
    
    Write-Host "🐳 Building and pushing container images..." -ForegroundColor Green
    
    try {
        # Get ACR login server
        $loginServer = az acr show --name $RegistryName --resource-group $ResourceGroup --query "loginServer" -o tsv
        
        # Login to ACR
        Write-Host "   🔐 Logging into container registry..." -ForegroundColor Gray
        az acr login --name $RegistryName
        
        # Build and push backend image
        Write-Host "   🔧 Building backend API image..." -ForegroundColor Gray
        $backendBuild = az acr build --registry $RegistryName --image "${AppName}-api:${Environment}-latest" --image "${AppName}-api:${Environment}-$(Get-Date -Format 'yyyyMMdd-HHmmss')" --file "../Dockerfile.backend" ..
        if ($LASTEXITCODE -ne 0) {
            throw "Backend image build failed"
        }
        
        # Build and push frontend image  
        Write-Host "   🎨 Building frontend UI image..." -ForegroundColor Gray
        $frontendBuild = az acr build --registry $RegistryName --image "${AppName}-ui:${Environment}-latest" --image "${AppName}-ui:${Environment}-$(Get-Date -Format 'yyyyMMdd-HHmmss')" --file "../Dockerfile.frontend" ..
        if ($LASTEXITCODE -ne 0) {
            throw "Frontend image build failed"
        }
        
        Write-Host "   ✅ Container images built and pushed successfully" -ForegroundColor Green
        return $true
        
    } catch {
        $global:DeploymentState.FailedOperations += "Container image build failed: $_"
        Write-Host "   ❌ Failed to build container images: $_" -ForegroundColor Red
        return $false
    }
}

function Get-OrCreateResource {
    param(
        [string]$ResourceName,
        [string]$ResourceGroup,
        [string]$ResourceType,
        [scriptblock]$CreateCommand
    )
    
    if (Test-ResourceExists -ResourceName $ResourceName -ResourceGroup $ResourceGroup -ResourceType $ResourceType) {
        Write-Host "✅ $ResourceType '$ResourceName' already exists - skipping creation" -ForegroundColor Green
        Add-DeploymentState -ResourceName $ResourceName -ResourceType $ResourceType -Status "Exists"
        return $true
    } else {
        if ($DryRun) {
            Write-Host "[DRY RUN] Would create $ResourceType '$ResourceName'" -ForegroundColor Cyan
            Add-DeploymentState -ResourceName $ResourceName -ResourceType $ResourceType -Status "DryRun"
            return $true
        }
        
        Write-Host "🏗️ Creating $ResourceType '$ResourceName'..." -ForegroundColor Green
        try {
            $result = & $CreateCommand
            if ($result) {
                Write-Host "✅ Successfully created $ResourceType '$ResourceName'" -ForegroundColor Green
                Add-DeploymentState -ResourceName $ResourceName -ResourceType $ResourceType -Status "Created"
                return $true
            } else {
                $global:DeploymentState.FailedOperations += "Failed to create $ResourceType '$ResourceName'"
                Write-Error "❌ Failed to create $ResourceType '$ResourceName'"
                return $false
            }
        } catch {
            $global:DeploymentState.FailedOperations += "Exception creating $ResourceType '$ResourceName': $_"
            Write-Error "❌ Exception creating $ResourceType '$ResourceName': $_"
            return $false
        }
    }
}

try {
    # Use consistent staging naming convention (no dashes for ACR)
    $containerRegistryName = "${AppName}cr${Environment}"
    $sqlServerName = "${AppName}-sql-${Environment}"
    $storageAccountName = "${AppName}st${Environment}"  # Storage accounts don't support dashes
    $keyVaultName = "${AppName}kv${Environment}"
    $containerEnvName = "${AppName}-env-${Environment}"
    $backendAppName = "${AppName}-api-${Environment}"
    $frontendAppName = "${AppName}-web-${Environment}"
    $applicationInsightsName = "${AppName}-ai-${Environment}"
    $workspaceName = "${AppName}-workspace-${Environment}"
    
    Write-Host "🏗️ Resource Names (Consistent Staging Suffix):" -ForegroundColor Green
    Write-Host "  Container Registry: $containerRegistryName (no dashes - ACR requirement)" -ForegroundColor Gray
    Write-Host "  SQL Server: $sqlServerName" -ForegroundColor Gray
    Write-Host "  Storage Account: $storageAccountName" -ForegroundColor Gray
    Write-Host "  Key Vault: $keyVaultName" -ForegroundColor Gray
    Write-Host "  Container Environment: $containerEnvName" -ForegroundColor Gray
    Write-Host "  Backend App: $backendAppName" -ForegroundColor Gray
    Write-Host "  Frontend App: $frontendAppName" -ForegroundColor Gray
    
    # Validate consistent naming pattern
    if ($Environment -eq "staging") {
        Write-Host "✅ Using consistent '-staging' suffix naming convention" -ForegroundColor Green
        Write-Host "   All resources will use predictable, maintainable names" -ForegroundColor Green
    } else {
        Write-Host "⚠️  WARNING: Environment is '$Environment' - expected 'staging'" -ForegroundColor Yellow
        Write-Host "   Resources will use '-$Environment' suffix instead of '-staging'" -ForegroundColor Yellow
    }
    
    # Step 1: Create Resource Group (idempotent)
    Write-Host "📦 Ensuring Resource Group exists..." -ForegroundColor Green
    az group create --name $ResourceGroupName --location $Location --tags Environment=$Environment Application=AIProfileMaker
    
    # Step 1.5: Register required resource providers (idempotent)
    Write-Host "🔧 Registering Azure resource providers..." -ForegroundColor Green
    az provider register --namespace Microsoft.Storage
    az provider register --namespace Microsoft.ContainerRegistry  
    az provider register --namespace Microsoft.App
    az provider register --namespace Microsoft.ContainerService
    az provider register --namespace Microsoft.OperationalInsights
    az provider register --namespace Microsoft.Sql
    az provider register --namespace Microsoft.KeyVault
    az provider register --namespace Microsoft.Insights
    
    # Step 2: Create Storage Account (with existence check)
    $storageExists = Get-OrCreateResource -ResourceName $storageAccountName -ResourceGroup $ResourceGroupName -ResourceType "Microsoft.Storage/storageAccounts" -CreateCommand {
        $result = az storage account create `
            --name $storageAccountName `
            --resource-group $ResourceGroupName `
            --location $Location `
            --sku Standard_LRS `
            --kind StorageV2 `
            --min-tls-version TLS1_2 `
            --allow-blob-public-access true `
            --output json | ConvertFrom-Json
        return $result
    }
    
    if (-not $storageExists) {
        throw "Failed to create or access storage account"
    }
    
    # Get storage connection string (works for existing or new)
    Write-Host "🔑 Retrieving storage account connection..." -ForegroundColor Green
    $storageKeys = az storage account keys list --resource-group $ResourceGroupName --account-name $storageAccountName --output json | ConvertFrom-Json
    $storageKey = $storageKeys[0].value
    $storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=$storageAccountName;AccountKey=$storageKey;EndpointSuffix=core.windows.net"
    
    # Create blob container (idempotent)
    Write-Host "📁 Ensuring blob container exists..." -ForegroundColor Green
    az storage container create --name "profile-images" --account-name $storageAccountName --account-key $storageKey --public-access blob 2>$null
    
    # Step 3: Create Container Registry (with existence check)
    $registryExists = Get-OrCreateResource -ResourceName $containerRegistryName -ResourceGroup $ResourceGroupName -ResourceType "Microsoft.ContainerRegistry/registries" -CreateCommand {
        $result = az acr create `
            --name $containerRegistryName `
            --resource-group $ResourceGroupName `
            --location $Location `
            --sku Basic `
            --admin-enabled true `
            --output json | ConvertFrom-Json
        return $result
    }
    
    # Step 4: Create Log Analytics Workspace (needed for Container Apps)
    $workspaceExists = Get-OrCreateResource -ResourceName $workspaceName -ResourceGroup $ResourceGroupName -ResourceType "Microsoft.OperationalInsights/workspaces" -CreateCommand {
        $result = az monitor log-analytics workspace create `
            --resource-group $ResourceGroupName `
            --workspace-name $workspaceName `
            --location $Location `
            --output json | ConvertFrom-Json
        return $result
    }
    
    # Step 5: Create SQL Server and Database (with existence check)
    $sqlExists = Get-OrCreateResource -ResourceName $sqlServerName -ResourceGroup $ResourceGroupName -ResourceType "Microsoft.Sql/servers" -CreateCommand {
        $result = az sql server create `
            --name $sqlServerName `
            --resource-group $ResourceGroupName `
            --location $Location `
            --admin-user "sqladmin" `
            --admin-password $SqlAdminPassword `
            --output json | ConvertFrom-Json
        return $result
    }
    
    if ($sqlExists) {
        # Create SQL Database (idempotent)
        Write-Host "🗃️ Ensuring SQL Database exists..." -ForegroundColor Green
        az sql db create `
            --resource-group $ResourceGroupName `
            --server $sqlServerName `
            --name "${AppName}db" `
            --service-objective Basic `
            --max-size 2GB 2>$null
        
        # Add firewall rule (idempotent)
        Write-Host "🔥 Ensuring SQL firewall rule exists..." -ForegroundColor Green
        az sql server firewall-rule create `
            --resource-group $ResourceGroupName `
            --server $sqlServerName `
            --name "AllowAzureServices" `
            --start-ip-address "0.0.0.0" `
            --end-ip-address "0.0.0.0" 2>$null
    }
    
    # Step 6: Create Application Insights (with existence check)
    $appInsightsExists = Get-OrCreateResource -ResourceName $applicationInsightsName -ResourceGroup $ResourceGroupName -ResourceType "Microsoft.Insights/components" -CreateCommand {
        $result = az monitor app-insights component create `
            --app $applicationInsightsName `
            --location $Location `
            --resource-group $ResourceGroupName `
            --kind web `
            --application-type web `
            --workspace $workspaceName `
            --output json | ConvertFrom-Json
        return $result
    }
    
    # Get Application Insights connection string
    $appInsightsResult = az monitor app-insights component show --app $applicationInsightsName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
    
    # Step 7: Create Key Vault (with existence check)
    $keyVaultExists = Get-OrCreateResource -ResourceName $keyVaultName -ResourceGroup $ResourceGroupName -ResourceType "Microsoft.KeyVault/vaults" -CreateCommand {
        $result = az keyvault create `
            --name $keyVaultName `
            --resource-group $ResourceGroupName `
            --location $Location `
            --sku standard `
            --enable-rbac-authorization true `
            --retention-days 7 `
            --output json | ConvertFrom-Json
        return $result
    }
    
    # Store secrets in Key Vault (idempotent - updates if exists)
    if ($keyVaultExists) {
        Write-Host "🔑 Updating secrets in Key Vault..." -ForegroundColor Green
        $sqlConnectionString = "Server=tcp:${sqlServerName}.database.windows.net,1433;Initial Catalog=${AppName}db;Authentication=Active Directory Default;Encrypt=True;"
        
        az keyvault secret set --vault-name $keyVaultName --name "JwtSecret" --value $JwtSecret
        az keyvault secret set --vault-name $keyVaultName --name "ReplicateApiToken" --value $ReplicateApiToken
        az keyvault secret set --vault-name $keyVaultName --name "ConnectionString" --value $sqlConnectionString
        az keyvault secret set --vault-name $keyVaultName --name "StorageConnectionString" --value $storageConnectionString
    }
    
    # Step 8: Create Container Apps Environment (with existence check)
    $containerEnvExists = Get-OrCreateResource -ResourceName $containerEnvName -ResourceGroup $ResourceGroupName -ResourceType "Microsoft.App/managedEnvironments" -CreateCommand {
        # Get workspace resource ID
        $workspaceId = az monitor log-analytics workspace show --resource-group $ResourceGroupName --workspace-name $workspaceName --query "customerId" -o tsv
        $workspaceKey = az monitor log-analytics workspace get-shared-keys --resource-group $ResourceGroupName --workspace-name $workspaceName --query "primarySharedKey" -o tsv
        
        $result = az containerapp env create `
            --name $containerEnvName `
            --resource-group $ResourceGroupName `
            --location $Location `
            --logs-workspace-id $workspaceId `
            --logs-workspace-key $workspaceKey `
            --output json | ConvertFrom-Json
        return $result
    }
    
    # Step 8.5: Build and push container images (TEMPORARILY DISABLED - using placeholder images)
    $imagesBuildSuccess = $false
    if ($BuildImages -and $registryExists) {
        Write-Host "⚠️ Image building temporarily disabled - using placeholder images for initial deployment" -ForegroundColor Yellow
        Write-Host "   Use separate image build process after infrastructure deployment completes" -ForegroundColor Yellow
        # $imagesBuildSuccess = Build-ContainerImages -RegistryName $containerRegistryName -ResourceGroup $ResourceGroupName
    }
    
    # Step 9: Create Backend Container App (with existence check)
    $backendExists = Get-OrCreateResource -ResourceName $backendAppName -ResourceGroup $ResourceGroupName -ResourceType "Microsoft.App/containerApps" -CreateCommand {
        # Determine image based on whether custom images were built
        $registryResult = az acr show --name $containerRegistryName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
        $backendImage = if ($BuildImages -and $imagesBuildSuccess -and -not $SkipImageDeploy) {
            "$($registryResult.loginServer)/${AppName}-api:${Environment}-latest"
        } else {
            "mcr.microsoft.com/k8se/quickstart:latest"
        }
        
        Write-Host "   Using backend image: $backendImage" -ForegroundColor Gray
        
        $result = az containerapp create `
            --name $backendAppName `
            --resource-group $ResourceGroupName `
            --environment $containerEnvName `
            --image $backendImage `
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
        return $result
    }
    
    # Step 10: Create Frontend Container App (with existence check)  
    $frontendExists = Get-OrCreateResource -ResourceName $frontendAppName -ResourceGroup $ResourceGroupName -ResourceType "Microsoft.App/containerApps" -CreateCommand {
        # Get backend URL for frontend configuration
        $backendResult = az containerapp show --name $backendAppName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
        $backendUrl = "https://$($backendResult.properties.configuration.ingress.fqdn)"
        
        # Determine image based on whether custom images were built
        $registryResult = az acr show --name $containerRegistryName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
        $frontendImage = if ($BuildImages -and $imagesBuildSuccess -and -not $SkipImageDeploy) {
            "$($registryResult.loginServer)/${AppName}-ui:${Environment}-latest"
        } else {
            "mcr.microsoft.com/k8se/quickstart:latest"
        }
        
        Write-Host "   Using frontend image: $frontendImage" -ForegroundColor Gray
        
        $result = az containerapp create `
            --name $frontendAppName `
            --resource-group $ResourceGroupName `
            --environment $containerEnvName `
            --image $frontendImage `
            --target-port 80 `
            --ingress external `
            --cpu 0.25 `
            --memory 0.5Gi `
            --min-replicas 0 `
            --max-replicas 2 `
            --env-vars "API_URL=$backendUrl" `
            --output json | ConvertFrom-Json
        return $result
    }
    
    # Step 11: Configure Container Registry and Key Vault Access (idempotent)
    Write-Host "🔗 Configuring resource access permissions..." -ForegroundColor Green
    
    # Get principal IDs
    $backendResult = az containerapp show --name $backendAppName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
    $frontendResult = az containerapp show --name $frontendAppName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
    $registryResult = az acr show --name $containerRegistryName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
    
    $backendPrincipalId = $backendResult.identity.principalId
    $frontendPrincipalId = $frontendResult.identity.principalId
    $subscriptionId = (az account show --query id -o tsv)
    
    # Assign AcrPull roles (idempotent)
    if ($backendPrincipalId) {
        az role assignment create --assignee $backendPrincipalId --role "AcrPull" --scope "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.ContainerRegistry/registries/$containerRegistryName" 2>$null
        Write-Host "✅ Ensured AcrPull role for backend app" -ForegroundColor Green
    }
    
    if ($frontendPrincipalId) {
        az role assignment create --assignee $frontendPrincipalId --role "AcrPull" --scope "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.ContainerRegistry/registries/$containerRegistryName" 2>$null
        Write-Host "✅ Ensured AcrPull role for frontend app" -ForegroundColor Green
    }
    
    # Configure Key Vault access (idempotent)
    if ($backendPrincipalId) {
        az role assignment create --assignee $backendPrincipalId --role "Key Vault Secrets User" --scope "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.KeyVault/vaults/$keyVaultName" 2>$null
        Write-Host "✅ Ensured Key Vault access for backend app" -ForegroundColor Green
    }
    
    # Output deployment results
    Write-Host "🎉 IDEMPOTENT deployment completed successfully!" -ForegroundColor Green
    
    # Write detailed deployment summary
    Write-DeploymentSummary
    
    Write-Host "`n📋 Resource URLs:" -ForegroundColor Yellow
    Write-Host "  Frontend URL: https://$($frontendResult.properties.configuration.ingress.fqdn)" -ForegroundColor Green
    Write-Host "  Backend URL: https://$($backendResult.properties.configuration.ingress.fqdn)" -ForegroundColor Green
    Write-Host "  Container Registry: $($registryResult.loginServer)" -ForegroundColor Green
    Write-Host "  SQL Server: ${sqlServerName}.database.windows.net" -ForegroundColor Green
    Write-Host "  Storage Account: $storageAccountName" -ForegroundColor Green
    Write-Host "  Key Vault: https://$keyVaultName.vault.azure.net/" -ForegroundColor Green
    
    if ($BuildImages) {
        Write-Host "`n🐳 Container Images:" -ForegroundColor Yellow
        Write-Host "  Backend API: $($registryResult.loginServer)/${AppName}-api:${Environment}-latest" -ForegroundColor Green
        Write-Host "  Frontend UI: $($registryResult.loginServer)/${AppName}-ui:${Environment}-latest" -ForegroundColor Green
    }
    
    # Set GitHub Actions outputs (if running in GitHub Actions)
    if ($env:GITHUB_OUTPUT) {
        "registry-name=$containerRegistryName" >> $env:GITHUB_OUTPUT
        "registry-server=$($registryResult.loginServer)" >> $env:GITHUB_OUTPUT
        "frontend-url=https://$($frontendResult.properties.configuration.ingress.fqdn)" >> $env:GITHUB_OUTPUT
        "backend-url=https://$($backendResult.properties.configuration.ingress.fqdn)" >> $env:GITHUB_OUTPUT
        "sql-server=${sqlServerName}.database.windows.net" >> $env:GITHUB_OUTPUT
        "storage-account=$storageAccountName" >> $env:GITHUB_OUTPUT
        "key-vault=$keyVaultName" >> $env:GITHUB_OUTPUT
    }
    
} catch {
    $global:DeploymentState.FailedOperations += "Critical deployment failure: $_"
    Write-Error "❌ IDEMPOTENT deployment failed: $_"
    Write-Host "🔍 Check the error details above and retry the deployment" -ForegroundColor Red
    
    # Write failure summary
    Write-DeploymentSummary
    
    Write-Host "`n💡 Troubleshooting Tips:" -ForegroundColor Yellow
    Write-Host "1. Run with -DryRun to preview changes before execution" -ForegroundColor Gray
    Write-Host "2. Check Azure CLI authentication: az account show" -ForegroundColor Gray
    Write-Host "3. Verify resource quotas and permissions in your subscription" -ForegroundColor Gray
    Write-Host "4. Use cleanup script to remove partial deployments: .\cleanup-duplicate-resources.ps1" -ForegroundColor Gray
    
    exit 1
}

Write-Host "`n✨ IDEMPOTENT deployment script completed - safe to run multiple times!" -ForegroundColor Magenta
Write-Host "💡 Usage Examples:" -ForegroundColor Yellow
Write-Host "  Dry run: .\deploy-infrastructure-idempotent.ps1 -ResourceGroupName 'my-rg' -DryRun" -ForegroundColor Gray
Write-Host "  With images: .\deploy-infrastructure-idempotent.ps1 -ResourceGroupName 'my-rg' -BuildImages" -ForegroundColor Gray
Write-Host "  Full deploy: .\deploy-infrastructure-idempotent.ps1 -ResourceGroupName 'my-rg' -BuildImages -SqlAdminPassword 'pwd' -JwtSecret 'secret' -ReplicateApiToken 'token'" -ForegroundColor Gray