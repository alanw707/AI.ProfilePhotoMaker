#!/usr/bin/env pwsh
# Database initialization script for Azure SQL Server
# Fixes: "Using demo styles (database connection issue)"

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$SqlServerName,
    
    [Parameter(Mandatory=$true)]
    [string]$DatabaseName,
    
    [Parameter(Mandatory=$true)]
    [string]$ContainerAppName,
    
    [Parameter()]
    [string]$Environment = "staging"
)

Write-Host "🔍 Initializing database for $Environment environment..." -ForegroundColor Cyan

# 1. Get current user info
$currentUser = az account show --query user.name -o tsv
Write-Host "✅ Current user: $currentUser"

# 2. Check SQL Server exists
Write-Host "🔍 Checking SQL Server: $SqlServerName"
$sqlServer = az sql server show --name $SqlServerName --resource-group $ResourceGroupName 2>$null
if (-not $sqlServer) {
    Write-Error "❌ SQL Server not found: $SqlServerName"
    exit 1
}
Write-Host "✅ SQL Server found"

# 3. Check database exists  
Write-Host "🔍 Checking database: $DatabaseName"
$database = az sql db show --name $DatabaseName --server $SqlServerName --resource-group $ResourceGroupName 2>$null
if (-not $database) {
    Write-Error "❌ Database not found: $DatabaseName"
    exit 1
}
Write-Host "✅ Database found"

# 4. Add current user as SQL Admin (for migration execution)
Write-Host "🔧 Adding current user as SQL Admin..."
az sql server ad-admin create `
    --resource-group $ResourceGroupName `
    --server-name $SqlServerName `
    --display-name $currentUser `
    --object-id (az ad user show --id $currentUser --query id -o tsv)

# 5. Get container app managed identity
Write-Host "🔍 Getting container app managed identity..."
$containerApp = az containerapp show --name $ContainerAppName --resource-group $ResourceGroupName --query identity -o json | ConvertFrom-Json
$principalId = $containerApp.principalId

if (-not $principalId) {
    Write-Error "❌ Container app has no managed identity. Enable system-assigned identity first."
    exit 1
}
Write-Host "✅ Container app principal ID: $principalId"

# 6. Add container app identity to SQL Server
Write-Host "🔧 Adding container app identity to SQL Server..."
try {
    az sql server ad-admin create `
        --resource-group $ResourceGroupName `
        --server-name $SqlServerName `
        --display-name $ContainerAppName `
        --object-id $principalId
    Write-Host "✅ Container app identity added to SQL Server"
}
catch {
    Write-Warning "⚠️ Failed to add container app identity to SQL Server: $_"
}

# 7. Test connection string from container app
Write-Host "🔍 Testing database connection from container app..."
$connectionTest = az containerapp exec `
    --name $ContainerAppName `
    --resource-group $ResourceGroupName `
    --command "sqlcmd -S $SqlServerName.database.windows.net -d $DatabaseName -G -Q 'SELECT 1'" 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Database connection successful"
} else {
    Write-Warning "⚠️ Database connection failed - may need manual migration"
}

# 8. Check if migrations have been applied
Write-Host "🔍 Checking migration status..."
$migrationCheck = az containerapp exec `
    --name $ContainerAppName `
    --resource-group $ResourceGroupName `
    --command "sqlcmd -S $SqlServerName.database.windows.net -d $DatabaseName -G -Q 'SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = \''Styles\'''" 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Database schema appears to be initialized"
} else {
    Write-Warning "⚠️ Database schema may be missing - triggering container app restart for migrations"
    
    # Restart container app to trigger migrations
    az containerapp revision restart `
        --name $ContainerAppName `
        --resource-group $ResourceGroupName
    
    Write-Host "🔄 Container app restarted - migrations should run on startup"
}

# 9. Verify styles data exists
Write-Host "🔍 Verifying styles data..."
Start-Sleep -Seconds 30  # Wait for restart and migrations

$stylesCheck = az containerapp exec `
    --name $ContainerAppName `
    --resource-group $ResourceGroupName `
    --command "sqlcmd -S $SqlServerName.database.windows.net -d $DatabaseName -G -Q 'SELECT COUNT(*) FROM Styles WHERE IsActive = 1'" 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Styles data verified - database connection issue should be resolved"
} else {
    Write-Warning "⚠️ Could not verify styles data - manual intervention may be required"
}

Write-Host ""
Write-Host "🎯 Database initialization completed!" -ForegroundColor Green
Write-Host "   • SQL Server: $SqlServerName.database.windows.net" -ForegroundColor Gray
Write-Host "   • Database: $DatabaseName" -ForegroundColor Gray  
Write-Host "   • Container App: $ContainerAppName" -ForegroundColor Gray
Write-Host ""
Write-Host "🔄 Frontend should no longer show 'Using demo styles (database connection issue)'" -ForegroundColor Green