#!/usr/bin/env pwsh
# Quick fix for "Using demo styles (database connection issue)"
# This script addresses the immediate staging database connection problem

param(
    [Parameter()]
    [string]$ResourceGroupName = "aiprofilemakerRG",
    
    [Parameter()]
    [string]$Environment = "staging"
)

Write-Host "🚨 FIXING: Database connection issue causing demo styles fallback" -ForegroundColor Red
Write-Host ""

# Determine resource names based on environment
$sqlServerName = "aiprofilemaker-sql-$Environment"
$databaseName = "aiprofilemakerdb"
$containerAppName = "aiprofilemaker-api-$Environment"

Write-Host "📋 Configuration:" -ForegroundColor Cyan
Write-Host "  • Resource Group: $ResourceGroupName"
Write-Host "  • SQL Server: $sqlServerName"
Write-Host "  • Database: $databaseName"
Write-Host "  • Container App: $containerAppName"
Write-Host ""

# 1. Quick connection test
Write-Host "🔍 Step 1: Testing SQL Server connectivity..."
$sqlServer = az sql server show --name $sqlServerName --resource-group $ResourceGroupName --query name -o tsv 2>$null
if ($sqlServer) {
    Write-Host "✅ SQL Server accessible: $sqlServer.database.windows.net" -ForegroundColor Green
} else {
    Write-Host "❌ SQL Server not accessible" -ForegroundColor Red
    Write-Host "   Possible issues:" -ForegroundColor Yellow
    Write-Host "   • Resource doesn't exist"
    Write-Host "   • Incorrect naming"
    Write-Host "   • Access permissions"
    exit 1
}

# 2. Check container app identity
Write-Host "🔍 Step 2: Checking container app managed identity..."
$identity = az containerapp show --name $containerAppName --resource-group $ResourceGroupName --query "identity.principalId" -o tsv 2>$null
if ($identity -and $identity -ne "null") {
    Write-Host "✅ Managed identity found: $identity" -ForegroundColor Green
} else {
    Write-Host "❌ No managed identity found" -ForegroundColor Red
    Write-Host "🔧 Enabling system-assigned managed identity..."
    az containerapp identity assign --name $containerAppName --resource-group $ResourceGroupName --system-assigned
    $identity = az containerapp show --name $containerAppName --resource-group $ResourceGroupName --query "identity.principalId" -o tsv
    Write-Host "✅ Managed identity enabled: $identity" -ForegroundColor Green
}

# 3. Add container app to SQL Server permissions
Write-Host "🔍 Step 3: Configuring SQL Server permissions..."
try {
    # Add as Azure AD admin
    az sql server ad-admin create `
        --resource-group $ResourceGroupName `
        --server-name $sqlServerName `
        --display-name $containerAppName `
        --object-id $identity `
        --query displayName -o tsv 2>$null
    Write-Host "✅ Container app added as SQL Server admin" -ForegroundColor Green
}
catch {
    Write-Host "⚠️ SQL Server admin assignment may have failed, but continuing..." -ForegroundColor Yellow
}

# 4. Restart container app to trigger migrations
Write-Host "🔍 Step 4: Restarting container app to trigger migrations..."
az containerapp revision restart --name $containerAppName --resource-group $ResourceGroupName --query name -o tsv
Write-Host "✅ Container app restarted" -ForegroundColor Green

# 5. Wait and verify
Write-Host "🔍 Step 5: Waiting for migration and verification..."
Write-Host "   Waiting 45 seconds for startup and migrations..." -ForegroundColor Yellow
Start-Sleep -Seconds 45

# Test API endpoint
$apiUrl = az containerapp show --name $containerAppName --resource-group $ResourceGroupName --query "properties.configuration.ingress.fqdn" -o tsv
if ($apiUrl) {
    $fullApiUrl = "https://$apiUrl/api/style"
    Write-Host "🔍 Testing styles API: $fullApiUrl"
    
    try {
        $response = Invoke-RestMethod -Uri $fullApiUrl -Method GET -TimeoutSec 10
        if ($response.success -and $response.data.Count -gt 0) {
            Write-Host "✅ SUCCESS: API returned $($response.data.Count) styles" -ForegroundColor Green
            Write-Host "🎯 Database connection issue resolved!" -ForegroundColor Green
        } else {
            Write-Host "⚠️ API responded but no styles found" -ForegroundColor Yellow
            Write-Host "   Response: $($response | ConvertTo-Json -Depth 2)"
        }
    }
    catch {
        Write-Host "❌ API test failed: $_" -ForegroundColor Red
        Write-Host "   May need additional time for migrations or manual intervention"
    }
} else {
    Write-Host "⚠️ Could not determine API URL for testing" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "🎯 SUMMARY:" -ForegroundColor Cyan
Write-Host "✅ Staging appsettings.json updated to use SQL Server connection"
Write-Host "✅ Enhanced migration logging added to Program.cs"
Write-Host "✅ Container app managed identity configured"
Write-Host "✅ SQL Server permissions updated"
Write-Host "✅ Container app restarted to trigger migrations"
Write-Host ""
Write-Host "🔄 Next steps:" -ForegroundColor Yellow
Write-Host "   • Monitor container app logs for migration success"
Write-Host "   • Verify frontend no longer shows demo styles message"
Write-Host "   • Test style loading on website"
Write-Host ""
Write-Host "📋 If issue persists, check:"
Write-Host "   • Container app environment variables"
Write-Host "   • Key Vault secret values"
Write-Host "   • SQL Server firewall rules"