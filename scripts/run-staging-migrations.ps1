# PowerShell script to run EF Core migrations on staging database
# This script should be run from the backend container or a container with the app deployed

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName = "rg-aiprofilemaker-staging",
    
    [string]$BackendAppName = "aiprofilemaker-api-staging"
)

Write-Host "🔄 Running EF Core migrations on staging database..." -ForegroundColor Green
Write-Host "📍 Resource Group: $ResourceGroupName" -ForegroundColor Yellow
Write-Host "📍 Backend App: $BackendAppName" -ForegroundColor Yellow

try {
    # Get the container app details
    $containerApp = az containerapp show --name $BackendAppName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
    
    if (-not $containerApp) {
        throw "Container app '$BackendAppName' not found in resource group '$ResourceGroupName'"
    }
    
    Write-Host "✅ Found container app: $($containerApp.name)" -ForegroundColor Green
    
    # Run the migration command in the container
    Write-Host "🏃 Executing EF Core database update command..." -ForegroundColor Green
    
    $migrationCommand = "dotnet ef database update --no-build --verbose"
    
    # Execute the command in the container app
    $result = az containerapp exec --name $BackendAppName --resource-group $ResourceGroupName --command $migrationCommand
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ EF Core migrations completed successfully!" -ForegroundColor Green
        
        # Test the API endpoint to verify the fix
        $backendUrl = "https://$($containerApp.properties.configuration.ingress.fqdn)"
        Write-Host "🧪 Testing credit packages endpoint..." -ForegroundColor Green
        
        $testResult = Invoke-RestMethod -Uri "$backendUrl/api/credit/packages" -Method Get -Headers @{ "Accept" = "application/json" } -ErrorAction SilentlyContinue
        
        if ($testResult -and $testResult.success) {
            Write-Host "✅ API endpoint is now working correctly!" -ForegroundColor Green
            Write-Host "📊 Found $($testResult.data.Count) credit packages" -ForegroundColor Green
        } else {
            Write-Host "⚠️ API endpoint test failed - manual verification needed" -ForegroundColor Yellow
        }
    } else {
        throw "EF Core migration command failed with exit code $LASTEXITCODE"
    }
    
} catch {
    Write-Host "❌ Failed to run EF Core migrations: $_" -ForegroundColor Red
    Write-Host "🔍 Troubleshooting tips:" -ForegroundColor Yellow
    Write-Host "1. Ensure the container app is running and healthy" -ForegroundColor Gray
    Write-Host "2. Check that the EF Core tools are included in the container image" -ForegroundColor Gray
    Write-Host "3. Verify the connection string and database permissions" -ForegroundColor Gray
    Write-Host "4. Check Azure CLI authentication: az account show" -ForegroundColor Gray
    exit 1
}

Write-Host "🎉 Staging database migration script completed!" -ForegroundColor Green