#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Emergency script to create CreditPackages table in Azure SQL Database
    
.DESCRIPTION
    Executes the emergency SQL migration script against the staging database
    to resolve the missing CreditPackages table issue.
    
.PARAMETER SqlServerName
    The Azure SQL Server name (default: aiprofilemaker-sql-staging)
    
.PARAMETER DatabaseName
    The database name (default: aiprofilemakerdb)
    
.PARAMETER DryRun
    If specified, only shows what would be executed without making changes
#>

param(
    [string]$SqlServerName = "aiprofilemaker-sql-staging",
    [string]$DatabaseName = "aiprofilemakerdb", 
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "🚨 EMERGENCY DATABASE MIGRATION SCRIPT" -ForegroundColor Red
Write-Host "=====================================" -ForegroundColor Red
Write-Host ""

# Validate Azure CLI is installed and user is logged in
try {
    $account = az account show --query "user.name" -o tsv 2>$null
    if (-not $account) {
        throw "Not logged in"
    }
    Write-Host "✅ Azure CLI authenticated as: $account" -ForegroundColor Green
} catch {
    Write-Host "❌ Azure CLI not installed or not logged in" -ForegroundColor Red
    Write-Host "Please run: az login" -ForegroundColor Yellow
    exit 1
}

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sqlScript = Join-Path $scriptDir "emergency-create-creditpackages.sql"

# Validate SQL script exists
if (-not (Test-Path $sqlScript)) {
    Write-Host "❌ SQL script not found: $sqlScript" -ForegroundColor Red
    exit 1
}

Write-Host "📋 Configuration:" -ForegroundColor Cyan
Write-Host "  SQL Server: $SqlServerName.database.windows.net" -ForegroundColor White
Write-Host "  Database: $DatabaseName" -ForegroundColor White
Write-Host "  SQL Script: $sqlScript" -ForegroundColor White
Write-Host ""

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "SQL Script Content:" -ForegroundColor Cyan
    Get-Content $sqlScript
    Write-Host ""
    Write-Host "To execute for real, run without -DryRun flag" -ForegroundColor Yellow
    exit 0
}

# Confirm execution
$confirmation = Read-Host "⚠️  This will execute emergency migration against STAGING database. Continue? (y/N)"
if ($confirmation -ne "y" -and $confirmation -ne "Y") {
    Write-Host "❌ Operation cancelled by user" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "🔄 Executing emergency migration..." -ForegroundColor Cyan

try {
    # Execute the SQL script using Azure CLI
    Write-Host "Running SQL script against Azure SQL Database..."
    
    $result = az sql db query `
        --server $SqlServerName `
        --database $DatabaseName `
        --auth-type ADIntegrated `
        --query-file $sqlScript `
        --output table
        
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Emergency migration completed successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Cyan
        Write-Host "1. Restart the Container App to pick up the new table" -ForegroundColor White
        Write-Host "2. Test the /api/credit-packages endpoint" -ForegroundColor White
        Write-Host "3. Verify API responds with HTTP 200 instead of 500" -ForegroundColor White
        
        # Try to verify the result
        Write-Host ""
        Write-Host "🔍 Verifying table creation..." -ForegroundColor Cyan
        
        $verifyScript = @"
SELECT 'CreditPackages' as TableName, COUNT(*) as RecordCount 
FROM CreditPackages;
"@
        
        $verifyResult = az sql db query `
            --server $SqlServerName `
            --database $DatabaseName `
            --auth-type ADIntegrated `
            --query $verifyScript `
            --output table
            
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Verification successful:" -ForegroundColor Green
            Write-Host $verifyResult
        }
        
    } else {
        Write-Host "❌ Migration failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
    
} catch {
    Write-Host "❌ Error executing migration: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "1. Verify you have contributor access to the SQL Server" -ForegroundColor White
    Write-Host "2. Check if Azure SQL firewall allows your IP" -ForegroundColor White
    Write-Host "3. Ensure the server and database names are correct" -ForegroundColor White
    Write-Host "4. Try: az login --scope https://database.windows.net/.default" -ForegroundColor White
    exit 1
}

Write-Host ""
Write-Host "🎯 Emergency migration process completed!" -ForegroundColor Green