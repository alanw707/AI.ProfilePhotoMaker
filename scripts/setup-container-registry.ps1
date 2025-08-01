# PowerShell script to setup Container Registry permissions and authentication
# This addresses the Container Registry permission issues during deployment

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$RegistryName,
    
    [Parameter(Mandatory=$true)]
    [string]$BackendAppName,
    
    [Parameter(Mandatory=$true)]
    [string]$FrontendAppName
)

Write-Host "🔧 Setting up Container Registry permissions for $RegistryName" -ForegroundColor Green

try {
    # Get the Container Registry resource ID
    $registryId = az acr show --name $RegistryName --resource-group $ResourceGroupName --query "id" -o tsv
    if (-not $registryId) {
        throw "Failed to get Container Registry ID"
    }
    Write-Host "✅ Found Container Registry: $registryId" -ForegroundColor Green

    # Get Container Apps managed identities
    $backendPrincipalId = az containerapp show --name $BackendAppName --resource-group $ResourceGroupName --query "identity.principalId" -o tsv
    $frontendPrincipalId = az containerapp show --name $FrontendAppName --resource-group $ResourceGroupName --query "identity.principalId" -o tsv
    
    if ($backendPrincipalId) {
        Write-Host "✅ Found Backend App Principal ID: $backendPrincipalId" -ForegroundColor Green
        
        # Assign AcrPull role to backend app
        az role assignment create `
            --assignee $backendPrincipalId `
            --role "AcrPull" `
            --scope $registryId
        Write-Host "✅ Assigned AcrPull role to backend app" -ForegroundColor Green
    } else {
        Write-Warning "⚠️ Backend app principal ID not found - may not be deployed yet"
    }
    
    if ($frontendPrincipalId) {
        Write-Host "✅ Found Frontend App Principal ID: $frontendPrincipalId" -ForegroundColor Green
        
        # Assign AcrPull role to frontend app
        az role assignment create `
            --assignee $frontendPrincipalId `
            --role "AcrPull" `
            --scope $registryId
        Write-Host "✅ Assigned AcrPull role to frontend app" -ForegroundColor Green
    } else {
        Write-Warning "⚠️ Frontend app principal ID not found - may not be deployed yet"
    }

    # Enable admin user on Container Registry for deployment
    az acr update --name $RegistryName --admin-enabled true
    Write-Host "✅ Enabled admin user on Container Registry" -ForegroundColor Green

    # Test registry access
    az acr login --name $RegistryName
    Write-Host "✅ Successfully logged into Container Registry" -ForegroundColor Green

    Write-Host "🎉 Container Registry setup completed successfully!" -ForegroundColor Green

} catch {
    Write-Error "❌ Failed to setup Container Registry: $_"
    exit 1
}