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
    # Validate inputs
    Write-Host "🔍 Validating input parameters..." -ForegroundColor Yellow
    
    if (-not $ResourceGroupName -or -not $ContainerRegistryName -or -not $BackendAppName -or -not $FrontendAppName) {
        throw "Missing required parameters"
    }
    
    Write-Host "[INFO] Resource Group: $ResourceGroupName" -ForegroundColor Cyan
    Write-Host "[INFO] Container Registry: $ContainerRegistryName" -ForegroundColor Cyan
    Write-Host "[INFO] Backend App: $BackendAppName" -ForegroundColor Cyan
    Write-Host "[INFO] Frontend App: $FrontendAppName" -ForegroundColor Cyan
    
    # Verify ACR admin user is enabled
    Write-Host "🔐 Checking ACR admin user status..." -ForegroundColor Yellow
    $acrInfo = Get-AzContainerRegistry -ResourceGroupName $ResourceGroupName -Name $ContainerRegistryName
    
    if (-not $acrInfo.AdminUserEnabled) {
        Write-Host "⚠️ ACR admin user is not enabled. Attempting to enable..." -ForegroundColor Yellow
        Update-AzContainerRegistry -ResourceGroupName $ResourceGroupName -Name $ContainerRegistryName -EnableAdminUser
        Write-Host "✅ ACR admin user enabled" -ForegroundColor Green
    } else {
        Write-Host "✅ ACR admin user is already enabled" -ForegroundColor Green
    }
    
    # Get ACR credentials using PowerShell Azure modules
    Write-Host "📋 Retrieving ACR credentials..." -ForegroundColor Yellow
    $acrCredentials = Get-AzContainerRegistryCredential -ResourceGroupName $ResourceGroupName -RegistryName $ContainerRegistryName
    
    if (-not $acrCredentials -or -not $acrCredentials.Username -or -not $acrCredentials.Password) {
        throw "Failed to retrieve ACR credentials. Ensure admin user is enabled on Container Registry."
    }
    
    $acrUsername = $acrCredentials.Username
    $acrPassword = $acrCredentials.Password
    
    Write-Host "✅ ACR credentials retrieved successfully" -ForegroundColor Green
    Write-Host "[INFO] ACR Username: $acrUsername" -ForegroundColor Cyan
    Write-Host "[INFO] ACR Password: [REDACTED - Length: $($acrPassword.Length)]" -ForegroundColor Cyan
    
    # Update Backend App ACR password using PowerShell
    Write-Host "🔄 Updating backend app ACR password..." -ForegroundColor Yellow
    try {
        $backendApp = Get-AzContainerApp -ResourceGroupName $ResourceGroupName -Name $BackendAppName
        $secretName = "acr-password"
        
        # Update secret using PowerShell cmdlet
        Update-AzContainerAppSecret -ResourceGroupName $ResourceGroupName -ContainerAppName $BackendAppName -SecretName $secretName -SecretValue $acrPassword
        Write-Host "✅ Backend app ACR password updated" -ForegroundColor Green
    } catch {
        Write-Host "⚠️ Falling back to Azure CLI for backend app secret update..." -ForegroundColor Yellow
        az containerapp secret set --name $BackendAppName --resource-group $ResourceGroupName --secrets "acr-password=$acrPassword"
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to update backend app ACR password"
        }
        Write-Host "✅ Backend app ACR password updated via CLI fallback" -ForegroundColor Green
    }
    
    # Update Frontend App ACR password using PowerShell
    Write-Host "🔄 Updating frontend app ACR password..." -ForegroundColor Yellow
    try {
        $frontendApp = Get-AzContainerApp -ResourceGroupName $ResourceGroupName -Name $FrontendAppName
        $secretName = "acr-password"
        
        # Update secret using PowerShell cmdlet
        Update-AzContainerAppSecret -ResourceGroupName $ResourceGroupName -ContainerAppName $FrontendAppName -SecretName $secretName -SecretValue $acrPassword
        Write-Host "✅ Frontend app ACR password updated" -ForegroundColor Green
    } catch {
        Write-Host "⚠️ Falling back to Azure CLI for frontend app secret update..." -ForegroundColor Yellow
        az containerapp secret set --name $FrontendAppName --resource-group $ResourceGroupName --secrets "acr-password=$acrPassword"
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to update frontend app ACR password"
        }
        Write-Host "✅ Frontend app ACR password updated via CLI fallback" -ForegroundColor Green
    }
    
    # Restart Container Apps using PowerShell modules
    Write-Host "🔄 Restarting container apps..." -ForegroundColor Yellow
    
    # Restart backend app
    try {
        $backendRevisions = Get-AzContainerAppRevision -ContainerAppName $BackendAppName -ResourceGroupName $ResourceGroupName
        $activeRevision = $backendRevisions | Where-Object { $_.Properties.Active -eq $true } | Select-Object -First 1
        
        if ($activeRevision) {
            Restart-AzContainerAppRevision -Name $activeRevision.Name -ResourceGroupName $ResourceGroupName
            Write-Host "✅ Backend app restarted" -ForegroundColor Green
        } else {
            Write-Host "⚠️ Warning: No active revision found for backend app" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "⚠️ Warning: Failed to restart backend app via PowerShell" -ForegroundColor Yellow
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Gray
    }
    
    # Restart frontend app
    try {
        $frontendRevisions = Get-AzContainerAppRevision -ContainerAppName $FrontendAppName -ResourceGroupName $ResourceGroupName
        $activeRevision = $frontendRevisions | Where-Object { $_.Properties.Active -eq $true } | Select-Object -First 1
        
        if ($activeRevision) {
            Restart-AzContainerAppRevision -Name $activeRevision.Name -ResourceGroupName $ResourceGroupName
            Write-Host "✅ Frontend app restarted" -ForegroundColor Green
        } else {
            Write-Host "⚠️ Warning: No active revision found for frontend app" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "⚠️ Warning: Failed to restart frontend app via PowerShell" -ForegroundColor Yellow
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Gray
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