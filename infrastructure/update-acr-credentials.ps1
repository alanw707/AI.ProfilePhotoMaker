# Update Container Apps with actual ACR credentials
# Run this after successful Bicep deployment to fix placeholder passwords

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$ContainerRegistryName,
    
    [Parameter(Mandatory=$true)]
    [string]$BackendAppName,
    
    [Parameter(Mandatory=$true)]
    [string]$FrontendAppName
)

Write-Host "🔧 Updating ACR credentials for Container Apps..." -ForegroundColor Cyan

try {
    # Get ACR admin credentials
    Write-Host "📋 Getting ACR admin credentials..." -ForegroundColor Yellow
    $acrCredentials = az acr credential show --name $ContainerRegistryName --resource-group $ResourceGroupName | ConvertFrom-Json
    $acrPassword = $acrCredentials.passwords[0].value
    
    if (-not $acrPassword) {
        throw "Failed to retrieve ACR password"
    }
    
    Write-Host "✅ Retrieved ACR credentials successfully" -ForegroundColor Green
    
    # Update Backend App
    Write-Host "🔄 Updating Backend Container App ACR credentials..." -ForegroundColor Yellow
    az containerapp secret set `
        --name $BackendAppName `
        --resource-group $ResourceGroupName `
        --secrets "acr-password=$acrPassword"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Backend App ACR credentials updated successfully" -ForegroundColor Green
    } else {
        throw "Failed to update Backend App ACR credentials"
    }
    
    # Update Frontend App  
    Write-Host "🔄 Updating Frontend Container App ACR credentials..." -ForegroundColor Yellow
    az containerapp secret set `
        --name $FrontendAppName `
        --resource-group $ResourceGroupName `
        --secrets "acr-password=$acrPassword"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Frontend App ACR credentials updated successfully" -ForegroundColor Green
    } else {
        throw "Failed to update Frontend App ACR credentials"
    }
    
    Write-Host "🎉 All ACR credentials updated successfully!" -ForegroundColor Green
    Write-Host "📝 Container Apps can now pull images from the registry" -ForegroundColor Cyan
    
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "💡 Make sure you have Azure CLI installed and are logged in" -ForegroundColor Yellow
    Write-Host "💡 Verify the resource names and that the deployment completed successfully" -ForegroundColor Yellow
    exit 1
}