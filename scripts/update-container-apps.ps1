# PowerShell script to update Container Apps with new images
# Avoids ARM API consumption issues by using sequential operations

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$RegistryServer,
    
    [string]$BackendImageTag = "latest",
    [string]$FrontendImageTag = "latest"
)

Write-Host "🔄 Starting Container Apps update process" -ForegroundColor Green

try {
    # Find container apps
    Write-Host "🔍 Finding Container Apps..." -ForegroundColor Green
    $backendApp = az containerapp list -g $ResourceGroupName --query "[?contains(name, 'api')].name" -o tsv
    $frontendApp = az containerapp list -g $ResourceGroupName --query "[?contains(name, 'web')].name" -o tsv
    
    if (-not $backendApp) {
        Write-Warning "⚠️ Backend app not found"
    } else {
        Write-Host "✅ Found backend app: $backendApp" -ForegroundColor Green
    }
    
    if (-not $frontendApp) {
        Write-Warning "⚠️ Frontend app not found"
    } else {
        Write-Host "✅ Found frontend app: $frontendApp" -ForegroundColor Green
    }
    
    # Update backend container app
    if ($backendApp) {
        Write-Host "🔄 Updating backend container app..." -ForegroundColor Green
        
        $backendImage = "$RegistryServer/aiprofilemaker-api:$BackendImageTag"
        Write-Host "📦 Backend image: $backendImage" -ForegroundColor Gray
        
        az containerapp update `
            --name $backendApp `
            --resource-group $ResourceGroupName `
            --image $backendImage `
            --output table
            
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Backend app updated successfully" -ForegroundColor Green
        } else {
            Write-Error "❌ Failed to update backend app"
        }
    }
    
    # Update frontend container app  
    if ($frontendApp) {
        Write-Host "🔄 Updating frontend container app..." -ForegroundColor Green
        
        $frontendImage = "$RegistryServer/aiprofilemaker-ui:$FrontendImageTag"
        Write-Host "📦 Frontend image: $frontendImage" -ForegroundColor Gray
        
        az containerapp update `
            --name $frontendApp `
            --resource-group $ResourceGroupName `
            --image $frontendImage `
            --output table
            
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Frontend app updated successfully" -ForegroundColor Green
        } else {
            Write-Error "❌ Failed to update frontend app"
        }
    }
    
    # Wait for deployments to stabilize
    Write-Host "⏳ Waiting for deployments to stabilize..." -ForegroundColor Green
    Start-Sleep -Seconds 30
    
    # Get app URLs for health checks
    if ($backendApp) {
        $backendUrl = az containerapp show --name $backendApp --resource-group $ResourceGroupName --query 'properties.configuration.ingress.fqdn' -o tsv
        if ($backendUrl) {
            Write-Host "🔍 Backend URL: https://$backendUrl" -ForegroundColor Green
            
            # Test backend health
            try {
                $response = Invoke-RestMethod -Uri "https://$backendUrl/health" -Method Get -TimeoutSec 30 -ErrorAction Stop
                Write-Host "✅ Backend health check passed" -ForegroundColor Green
            } catch {
                Write-Warning "⚠️ Backend health check failed (may still be starting): $_"
            }
        }
    }
    
    if ($frontendApp) {
        $frontendUrl = az containerapp show --name $frontendApp --resource-group $ResourceGroupName --query 'properties.configuration.ingress.fqdn' -o tsv
        if ($frontendUrl) {
            Write-Host "🔍 Frontend URL: https://$frontendUrl" -ForegroundColor Green
            
            # Test frontend health
            try {
                $response = Invoke-RestMethod -Uri "https://$frontendUrl" -Method Get -TimeoutSec 30 -ErrorAction Stop
                Write-Host "✅ Frontend health check passed" -ForegroundColor Green
            } catch {
                Write-Warning "⚠️ Frontend health check failed (may still be starting): $_"
            }
        }
    }
    
    # Run EF Core migrations on the updated backend
    if ($backendApp) {
        Write-Host "🔄 Running EF Core migrations..." -ForegroundColor Green
        
        try {
            # Wait a bit more for the backend to be fully ready
            Start-Sleep -Seconds 15
            
            # Run migration command in the container
            $migrationResult = az containerapp exec --name $backendApp --resource-group $ResourceGroupName --command "dotnet ef database update --no-build" 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ EF Core migrations completed successfully!" -ForegroundColor Green
            } else {
                Write-Warning "⚠️ EF Core migrations may have failed. Result: $migrationResult"
                Write-Host "💡 You may need to run migrations manually using the run-staging-migrations.ps1 script" -ForegroundColor Yellow
            }
            
            # Test the problematic API endpoint
            if ($backendUrl) {
                Write-Host "🧪 Testing credit packages endpoint..." -ForegroundColor Green
                try {
                    $testResult = Invoke-RestMethod -Uri "https://$backendUrl/api/credit/packages" -Method Get -Headers @{ "Accept" = "application/json" } -TimeoutSec 30 -ErrorAction Stop
                    
                    if ($testResult -and $testResult.success) {
                        Write-Host "✅ Credit packages API is working correctly!" -ForegroundColor Green
                        Write-Host "📊 Found $($testResult.data.Count) credit packages" -ForegroundColor Green
                    } else {
                        Write-Warning "⚠️ Credit packages API returned unexpected response"
                    }
                } catch {
                    Write-Warning "⚠️ Credit packages API test failed: $_"
                    Write-Host "💡 This may indicate database schema issues - check migration logs" -ForegroundColor Yellow
                }
            }
            
        } catch {
            Write-Warning "⚠️ Failed to run EF Core migrations: $_"
            Write-Host "💡 Manual migration may be required using the run-staging-migrations.ps1 script" -ForegroundColor Yellow
        }
    }
    
    Write-Host "🎉 Container Apps update completed!" -ForegroundColor Green
    
} catch {
    Write-Error "❌ Container Apps update failed: $_"
    exit 1
}