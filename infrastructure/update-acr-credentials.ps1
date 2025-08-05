# Option A: Post-Deployment ACR Credential Update Script
# This script updates Container Apps with actual ACR credentials after deployment

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

Write-Host "🔐 Updating ACR credentials for Container Apps..." -ForegroundColor Green

try {
    # Get ACR credentials
    Write-Host "📋 Retrieving ACR credentials..." -ForegroundColor Yellow
    $acrCredentials = az acr credential show --name $ContainerRegistryName --resource-group $ResourceGroupName | ConvertFrom-Json
    $acrPassword = $acrCredentials.passwords[0].value
    
    if (-not $acrPassword) {
        throw "Failed to retrieve ACR password"
    }
    
    Write-Host "✅ ACR credentials retrieved successfully" -ForegroundColor Green
    
    # Update Backend App ACR password
    Write-Host "🔄 Updating backend app ACR password..." -ForegroundColor Yellow
    az containerapp secret set `
        --name $BackendAppName `
        --resource-group $ResourceGroupName `
        --secrets "acr-password=$acrPassword"
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to update backend app ACR password"
    }
    
    Write-Host "✅ Backend app ACR password updated" -ForegroundColor Green
    
    # Update Frontend App ACR password
    Write-Host "🔄 Updating frontend app ACR password..." -ForegroundColor Yellow
    az containerapp secret set `
        --name $FrontendAppName `
        --resource-group $ResourceGroupName `
        --secrets "acr-password=$acrPassword"
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to update frontend app ACR password"
    }
    
    Write-Host "✅ Frontend app ACR password updated" -ForegroundColor Green
    
    # Restart Container Apps to use new credentials
    Write-Host "🔄 Restarting container apps..." -ForegroundColor Yellow
    
    # Restart backend app
    az containerapp revision restart --name $BackendAppName --resource-group $ResourceGroupName
    if ($LASTEXITCODE -ne 0) {
        Write-Host "⚠️ Warning: Failed to restart backend app" -ForegroundColor Yellow
    } else {
        Write-Host "✅ Backend app restarted" -ForegroundColor Green
    }
    
    # Restart frontend app
    az containerapp revision restart --name $FrontendAppName --resource-group $ResourceGroupName
    if ($LASTEXITCODE -ne 0) {
        Write-Host "⚠️ Warning: Failed to restart frontend app" -ForegroundColor Yellow
    } else {
        Write-Host "✅ Frontend app restarted" -ForegroundColor Green
    }
    
    Write-Host ""
    Write-Host "🎉 ACR credential update completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Monitor container app status in Azure Portal" -ForegroundColor White
    Write-Host "2. Check container app logs for any authentication issues" -ForegroundColor White
    Write-Host "3. Test application functionality" -ForegroundColor White
    
} catch {
    Write-Host ""
    Write-Host "❌ Error updating ACR credentials: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting steps:" -ForegroundColor Yellow
    Write-Host "1. Verify resource names are correct" -ForegroundColor White
    Write-Host "2. Check Azure CLI authentication" -ForegroundColor White
    Write-Host "3. Ensure you have sufficient permissions" -ForegroundColor White
    Write-Host "4. Check if Container Registry admin is enabled" -ForegroundColor White
    exit 1
}