#!/usr/bin/env pwsh

# Emergency script to populate styles directly via Azure SQL
# This bypasses the API and directly executes SQL against the database

Write-Host "🚨 EMERGENCY STYLES POPULATION" -ForegroundColor Red
Write-Host "===============================" -ForegroundColor Red
Write-Host ""

# Configuration
$resourceGroup = "rg-aiprofilemaker-staging"
$serverName = "aiprofilemaker-sql-staging"
$databaseName = "aiprofilemakerdb"

Write-Host "📋 Database Details:" -ForegroundColor Yellow
Write-Host "  Resource Group: $resourceGroup" -ForegroundColor Gray
Write-Host "  Server: $serverName" -ForegroundColor Gray
Write-Host "  Database: $databaseName" -ForegroundColor Gray
Write-Host ""

# Check if logged into Azure
Write-Host "🔐 Checking Azure authentication..." -ForegroundColor Blue
try {
    $account = az account show --output json | ConvertFrom-Json
    Write-Host "✅ Logged into Azure as: $($account.user.name)" -ForegroundColor Green
} catch {
    Write-Host "❌ Not logged into Azure. Run: az login" -ForegroundColor Red
    exit 1
}

# SQL to populate all 21 styles
$stylesSQL = @"
-- Emergency styles population
-- Clear existing styles and add comprehensive set

DELETE FROM Styles;

INSERT INTO Styles (Name, Category, Description, IsActive) VALUES
('professional', 'Business', 'Professional business headshot style', 1),
('casual', 'Lifestyle', 'Casual everyday portrait style', 1),
('artistic', 'Creative', 'Artistic and creative portrait style', 1),
('corporate', 'Business', 'Corporate executive professional style', 1),
('executive', 'Business', 'Senior executive leadership style', 1),
('consultant', 'Business', 'Professional consultant style', 1),
('linkedin', 'Business', 'LinkedIn profile optimized style', 1),
('legal', 'Business', 'Legal professional style', 1),
('medical', 'Professional', 'Healthcare professional style', 1),
('academic', 'Professional', 'Academic and research style', 1),
('entrepreneur', 'Business', 'Entrepreneur and startup style', 1),
('startup', 'Business', 'Startup founder style', 1),
('tech-professional', 'Technology', 'Technology industry style', 1),
('influencer', 'Social', 'Social media influencer style', 1),
('digital-nomad', 'Lifestyle', 'Remote work professional style', 1),
('creative', 'Creative', 'Creative industry professional style', 1),
('edgy-urban', 'Creative', 'Modern urban creative style', 1),
('glamour', 'Lifestyle', 'Glamorous portrait style', 1),
('fitness', 'Lifestyle', 'Health and fitness professional style', 1),
('spiritual', 'Lifestyle', 'Wellness and spiritual style', 1),
('author', 'Creative', 'Literary author style', 1);

-- Verify population
SELECT COUNT(*) as total_styles FROM Styles WHERE IsActive = 1;
"@

Write-Host "🔄 Executing SQL against Azure database..." -ForegroundColor Blue
Write-Host ""

# Create temp SQL file
$tempSQLFile = [System.IO.Path]::GetTempFileName() + ".sql"
$stylesSQL | Out-File -FilePath $tempSQLFile -Encoding UTF8

try {
    # Execute SQL using Azure CLI
    Write-Host "📊 Running SQL commands..." -ForegroundColor Blue
    
    $result = az sql db query `
        --resource-group $resourceGroup `
        --server $serverName `
        --database $databaseName `
        --queries $tempSQLFile `
        --output table

    Write-Host "✅ SQL Execution Results:" -ForegroundColor Green
    Write-Host $result
    Write-Host ""
    
    # Verify by checking count
    Write-Host "🔍 Verifying styles count..." -ForegroundColor Blue
    $countSQL = "SELECT COUNT(*) as total_styles FROM Styles WHERE IsActive = 1"
    $countResult = az sql db query `
        --resource-group $resourceGroup `
        --server $serverName `
        --database $databaseName `
        --queries $countSQL `
        --output table
    
    Write-Host "📊 Verification Results:" -ForegroundColor Green
    Write-Host $countResult
    Write-Host ""
    
    Write-Host "🎉 SUCCESS: Emergency styles population completed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "🧪 Next Steps:" -ForegroundColor Yellow
    Write-Host "  1. Test API: curl https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io/api/style" -ForegroundColor Gray
    Write-Host "  2. Verify frontend loads 21 styles" -ForegroundColor Gray
    Write-Host "  3. Check console for JSON parsing errors" -ForegroundColor Gray

} catch {
    Write-Host "❌ ERROR: Failed to execute SQL" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    # Clean up temp file
    if (Test-Path $tempSQLFile) {
        Remove-Item $tempSQLFile -Force
    }
}

Write-Host ""
Write-Host "✅ Emergency fix completed!" -ForegroundColor Green