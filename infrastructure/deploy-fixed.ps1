# AI Profile Photo Maker - Fixed Deployment Script
# Deploys infrastructure and updates ACR credentials

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$AppName = "aiprofilemaker",
    
    [Parameter(Mandatory=$true)]
    [string]$Environment = "v1",
    
    [Parameter(Mandatory=$true)]
    [SecureString]$SqlAdminPassword,
    
    [Parameter(Mandatory=$true)]
    [SecureString]$JwtSecret,
    
    [Parameter(Mandatory=$true)]
    [SecureString]$ReplicateApiToken,
    
    [string]$Location = "East US 2"
)

Write-Host "🚀 Starting AI Profile Photo Maker deployment..." -ForegroundColor Cyan
Write-Host "📍 Resource Group: $ResourceGroupName" -ForegroundColor White
Write-Host "📍 Location: $Location" -ForegroundColor White
Write-Host "📍 App Name: $AppName" -ForegroundColor White
Write-Host "📍 Environment: $Environment" -ForegroundColor White

try {
    # Convert SecureStrings to plain text for deployment
    $sqlPasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlAdminPassword))
    $jwtSecretPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($JwtSecret))
    $replicateTokenPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($ReplicateApiToken))
    
    # Step 1: Deploy Bicep template
    Write-Host "🏗️  Deploying infrastructure with Bicep..." -ForegroundColor Yellow
    
    $deploymentResult = az deployment group create `
        --resource-group $ResourceGroupName `
        --template-file "simple-deploy.bicep" `
        --parameters "appName=$AppName" `
        --parameters "environment=$Environment" `
        --parameters "location=$Location" `
        --parameters "sqlAdminPassword=$sqlPasswordPlain" `
        --parameters "jwtSecret=$jwtSecretPlain" `
        --parameters "replicateApiToken=$replicateTokenPlain" `
        --output json
    
    if ($LASTEXITCODE -ne 0) {
        throw "Bicep deployment failed"
    }
    
    $deployment = $deploymentResult | ConvertFrom-Json
    Write-Host "✅ Infrastructure deployment completed successfully" -ForegroundColor Green
    
    # Extract resource names from deployment output
    $uniqueSuffix = az group show --name $ResourceGroupName --query "id" --output tsv | ForEach-Object { 
        [System.Web.Security.Membership]::GeneratePassword(13, 0).ToLower() 
    }
    
    $containerRegistryName = "$AppName" + "cr$Environment" + $uniqueSuffix
    $backendAppName = "$AppName-api-$Environment"
    $frontendAppName = "$AppName-web-$Environment"
    
    Write-Host "📋 Resource Names:" -ForegroundColor Cyan
    Write-Host "   • Container Registry: $containerRegistryName" -ForegroundColor White
    Write-Host "   • Backend App: $backendAppName" -ForegroundColor White  
    Write-Host "   • Frontend App: $frontendAppName" -ForegroundColor White
    
    # Step 2: Update ACR credentials
    Write-Host "🔧 Updating ACR credentials..." -ForegroundColor Yellow
    
    & "./update-acr-credentials.ps1" `
        -ResourceGroupName $ResourceGroupName `
        -ContainerRegistryName $containerRegistryName `
        -BackendAppName $backendAppName `
        -FrontendAppName $frontendAppName
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "⚠️  ACR credential update failed, but infrastructure is deployed" -ForegroundColor Yellow
        Write-Host "💡 You can run update-acr-credentials.ps1 manually later" -ForegroundColor Yellow
    }
    
    # Display outputs
    Write-Host "🎉 Deployment completed successfully!" -ForegroundColor Green
    Write-Host "📝 Deployment Outputs:" -ForegroundColor Cyan
    
    if ($deployment.properties.outputs) {
        $deployment.properties.outputs | ConvertTo-Json -Depth 3 | Write-Host
    }
    
    Write-Host "🔗 Next Steps:" -ForegroundColor Cyan
    Write-Host "   1. Build and push container images to ACR" -ForegroundColor White
    Write-Host "   2. Update Container Apps to use your images" -ForegroundColor White
    Write-Host "   3. Configure database schema" -ForegroundColor White
    
} catch {
    Write-Host "❌ Deployment failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "💡 Check the error details above and retry" -ForegroundColor Yellow
    exit 1
} finally {
    # Clear sensitive variables
    if ($sqlPasswordPlain) { Clear-Variable -Name "sqlPasswordPlain" -ErrorAction SilentlyContinue }
    if ($jwtSecretPlain) { Clear-Variable -Name "jwtSecretPlain" -ErrorAction SilentlyContinue }
    if ($replicateTokenPlain) { Clear-Variable -Name "replicateTokenPlain" -ErrorAction SilentlyContinue }
}