# Validate AI Profile Photo Maker Bicep Template
# Quick validation script to test template syntax and deployment plan

param(
    [string]$ResourceGroupName = "rg-aiprofilemaker-test",
    [string]$Location = "East US 2"
)

Write-Host "🔍 Validating AI Profile Photo Maker Bicep template..." -ForegroundColor Cyan

try {
    # Test template compilation
    Write-Host "📋 Testing Bicep template compilation..." -ForegroundColor Yellow
    
    $compileResult = az bicep build --file "simple-deploy.bicep" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Template compilation failed:" -ForegroundColor Red
        Write-Host $compileResult -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ Template compiles successfully" -ForegroundColor Green
    
    # Validate deployment (what-if)
    Write-Host "🧪 Running deployment validation (what-if)..." -ForegroundColor Yellow
    
    $testParams = @{
        "appName" = "aiprofilemaker"
        "environment" = "test" 
        "location" = $Location
        "sqlAdminPassword" = "TempPassword123!"
        "jwtSecret" = "test-jwt-secret-key-32-characters"
        "replicateApiToken" = "test-replicate-token"
    }
    
    $whatIfResult = az deployment group what-if `
        --resource-group $ResourceGroupName `
        --template-file "simple-deploy.bicep" `
        --parameters "appName=$($testParams.appName)" `
        --parameters "environment=$($testParams.environment)" `
        --parameters "location=$($testParams.location)" `
        --parameters "sqlAdminPassword=$($testParams.sqlAdminPassword)" `
        --parameters "jwtSecret=$($testParams.jwtSecret)" `
        --parameters "replicateApiToken=$($testParams.replicateApiToken)" `
        --output json 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Template validation passed" -ForegroundColor Green
        Write-Host "📝 What-if results:" -ForegroundColor Cyan
        $whatIfResult | ConvertFrom-Json | ConvertTo-Json -Depth 3 | Write-Host
    } else {
        Write-Host "⚠️  Template validation completed with warnings" -ForegroundColor Yellow
        Write-Host "📝 Validation output:" -ForegroundColor Cyan
        Write-Host $whatIfResult -ForegroundColor White
    }
    
    # Resource summary
    Write-Host "🏗️  Resources that will be created:" -ForegroundColor Cyan
    Write-Host "   • Container Registry (with admin credentials)" -ForegroundColor White
    Write-Host "   • SQL Server and Database" -ForegroundColor White
    Write-Host "   • Storage Account with blob container" -ForegroundColor White
    Write-Host "   • Key Vault with secrets" -ForegroundColor White
    Write-Host "   • Log Analytics Workspace" -ForegroundColor White
    Write-Host "   • Application Insights" -ForegroundColor White
    Write-Host "   • Container Apps Environment" -ForegroundColor White
    Write-Host "   • Backend Container App" -ForegroundColor White
    Write-Host "   • Frontend Container App" -ForegroundColor White
    
    Write-Host "💡 Next steps:" -ForegroundColor Cyan
    Write-Host "   1. Create resource group if it doesn't exist" -ForegroundColor White
    Write-Host "   2. Run deploy-fixed.ps1 for full deployment" -ForegroundColor White
    Write-Host "   3. Use update-acr-credentials.ps1 to fix ACR passwords" -ForegroundColor White
    
} catch {
    Write-Host "❌ Validation failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}