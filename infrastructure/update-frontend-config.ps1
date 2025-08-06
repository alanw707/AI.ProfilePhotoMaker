# Update Frontend API URL Configuration
# This script updates the frontend Container App with the actual backend URL
# after both apps are successfully deployed

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$BackendAppName,
    
    [Parameter(Mandatory=$true)]
    [string]$FrontendAppName
)

Write-Host "🔄 [INFO] Updating frontend API configuration..." -ForegroundColor Cyan

try {
    # Get backend URL
    Write-Host "🔍 [INFO] Getting backend URL from Container App: $BackendAppName" -ForegroundColor Yellow
    $backendUrl = az containerapp show --name $BackendAppName --resource-group $ResourceGroupName --query "properties.configuration.ingress.fqdn" --output tsv
    
    if ([string]::IsNullOrEmpty($backendUrl)) {
        Write-Host "❌ [ERROR] Could not retrieve backend URL" -ForegroundColor Red
        exit 1
    }
    
    $fullBackendUrl = "https://$backendUrl"
    Write-Host "✅ [SUCCESS] Backend URL found: $fullBackendUrl" -ForegroundColor Green
    
    # Update frontend app with actual backend URL
    Write-Host "🔄 [INFO] Updating frontend Container App: $FrontendAppName" -ForegroundColor Yellow
    az containerapp update `
        --name $FrontendAppName `
        --resource-group $ResourceGroupName `
        --set-env-vars "API_URL=$fullBackendUrl"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ [SUCCESS] Frontend API configuration updated successfully!" -ForegroundColor Green
        Write-Host "🔗 [INFO] Frontend now configured to use: $fullBackendUrl" -ForegroundColor Cyan
    } else {
        Write-Host "❌ [ERROR] Failed to update frontend configuration" -ForegroundColor Red
        exit 1
    }
    
} catch {
    Write-Host "❌ [ERROR] Script execution failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}