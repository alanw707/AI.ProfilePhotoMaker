# Container Image Update Script for AI Profile Photo Maker
# Updates existing container apps to use actual application images instead of placeholder images

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [string]$AppName = "aiprofilemaker",
    [string]$Environment = "staging",
    [switch]$BuildImages = $true,
    [switch]$DryRun = $false,
    [switch]$Force = $false
)

Write-Host "🐳 Container Image Update Tool for AI Profile Photo Maker" -ForegroundColor Green
Write-Host "📍 Resource Group: $ResourceGroupName" -ForegroundColor Yellow
Write-Host "📍 Environment: $Environment" -ForegroundColor Yellow
Write-Host "📍 Build Images: $BuildImages" -ForegroundColor Yellow

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No changes will be made" -ForegroundColor Cyan
}

# Resource names (deterministic)
$containerRegistryName = "${AppName}cr${Environment}"
$backendAppName = "${AppName}-api-${Environment}"
$frontendAppName = "${AppName}-web-${Environment}"

# Initialize update tracking
$global:UpdateStats = @{
    ImagesBuilt = 0
    AppsUpdated = 0
    FailedOperations = @()
    StartTime = Get-Date
}

function Build-ContainerImages {
    param(
        [string]$RegistryName,
        [string]$ResourceGroup
    )
    
    if ($DryRun) {
        Write-Host "[DRY RUN] Would build and push container images to $RegistryName" -ForegroundColor Cyan
        return $true
    }
    
    Write-Host "🏗️ Building and pushing container images..." -ForegroundColor Green
    
    try {
        # Get ACR login server
        $loginServer = az acr show --name $RegistryName --resource-group $ResourceGroup --query "loginServer" -o tsv
        
        if (-not $loginServer) {
            throw "Container registry '$RegistryName' not found"
        }
        
        # Login to ACR
        Write-Host "   🔐 Logging into container registry..." -ForegroundColor Gray
        az acr login --name $RegistryName
        
        # Build and push backend image
        Write-Host "   🔧 Building backend API image..." -ForegroundColor Gray
        $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        
        az acr build --registry $RegistryName `
            --image "${AppName}-api:${Environment}-latest" `
            --image "${AppName}-api:${Environment}-${timestamp}" `
            --file "Dockerfile.backend" .
        
        # Build and push frontend image  
        Write-Host "   🎨 Building frontend UI image..." -ForegroundColor Gray
        az acr build --registry $RegistryName `
            --image "${AppName}-ui:${Environment}-latest" `
            --image "${AppName}-ui:${Environment}-${timestamp}" `
            --file "Dockerfile.frontend" .
        
        $global:UpdateStats.ImagesBuilt = 2
        Write-Host "   ✅ Container images built and pushed successfully" -ForegroundColor Green
        return $true
        
    } catch {
        $global:UpdateStats.FailedOperations += "Container image build failed: $_"
        Write-Host "   ❌ Failed to build container images: $_" -ForegroundColor Red
        return $false
    }
}

function Update-ContainerApp {
    param(
        [string]$AppName,
        [string]$ImageName,
        [string]$ResourceGroup
    )
    
    if ($DryRun) {
        Write-Host "[DRY RUN] Would update $AppName to use image: $ImageName" -ForegroundColor Cyan
        return $true
    }
    
    Write-Host "   🔄 Updating container app '$AppName'..." -ForegroundColor Gray
    
    try {
        # Check if app exists
        $appExists = az containerapp show --name $AppName --resource-group $ResourceGroup --query "name" -o tsv 2>$null
        
        if (-not $appExists) {
            Write-Host "   ⚠️ Container app '$AppName' not found - skipping" -ForegroundColor Yellow
            return $false
        }
        
        # Update the container app image
        az containerapp update `
            --name $AppName `
            --resource-group $ResourceGroup `
            --image $ImageName
        
        # Verify the update
        $updatedImage = az containerapp show --name $AppName --resource-group $ResourceGroup --query "properties.template.containers[0].image" -o tsv
        
        if ($updatedImage -eq $ImageName) {
            Write-Host "   ✅ Successfully updated '$AppName' to use image: $ImageName" -ForegroundColor Green
            $global:UpdateStats.AppsUpdated++
            return $true
        } else {
            Write-Host "   ❌ Image update verification failed for '$AppName'" -ForegroundColor Red
            $global:UpdateStats.FailedOperations += "Image update verification failed for '$AppName'"
            return $false
        }
        
    } catch {
        $global:UpdateStats.FailedOperations += "Failed to update container app '$AppName': $_"
        Write-Host "   ❌ Failed to update container app '$AppName': $_" -ForegroundColor Red
        return $false
    }
}

function Show-CurrentImages {
    param(
        [string]$ResourceGroup
    )
    
    Write-Host "`n📋 Current Container App Images:" -ForegroundColor Green
    
    try {
        # Check backend app
        $backendImage = az containerapp show --name $backendAppName --resource-group $ResourceGroup --query "properties.template.containers[0].image" -o tsv 2>$null
        if ($backendImage) {
            $isPlaceholder = $backendImage.StartsWith("mcr.microsoft.com")
            $status = if ($isPlaceholder) { "🔴 Placeholder" } else { "✅ Custom" }
            Write-Host "   Backend API ($backendAppName): $status" -ForegroundColor Gray
            Write-Host "     Image: $backendImage" -ForegroundColor Gray
        } else {
            Write-Host "   Backend API ($backendAppName): ❌ Not found" -ForegroundColor Red
        }
        
        # Check frontend app
        $frontendImage = az containerapp show --name $frontendAppName --resource-group $ResourceGroup --query "properties.template.containers[0].image" -o tsv 2>$null
        if ($frontendImage) {
            $isPlaceholder = $frontendImage.StartsWith("mcr.microsoft.com")
            $status = if ($isPlaceholder) { "🔴 Placeholder" } else { "✅ Custom" }
            Write-Host "   Frontend UI ($frontendAppName): $status" -ForegroundColor Gray
            Write-Host "     Image: $frontendImage" -ForegroundColor Gray
        } else {
            Write-Host "   Frontend UI ($frontendAppName): ❌ Not found" -ForegroundColor Red
        }
        
    } catch {
        Write-Host "   ❌ Failed to retrieve current images: $_" -ForegroundColor Red
    }
}

try {
    # Show current state
    Show-CurrentImages -ResourceGroup $ResourceGroupName
    
    # Check if container registry exists
    Write-Host "`n🔍 Checking container registry..." -ForegroundColor Green
    $registryExists = az acr show --name $containerRegistryName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null
    
    if (-not $registryExists) {
        Write-Host "❌ Container registry '$containerRegistryName' not found" -ForegroundColor Red
        Write-Host "💡 Run the deployment script first: deploy-infrastructure-idempotent.ps1" -ForegroundColor Yellow
        exit 1
    }
    
    $registryServer = az acr show --name $containerRegistryName --resource-group $ResourceGroupName --query "loginServer" -o tsv
    Write-Host "✅ Found container registry: $registryServer" -ForegroundColor Green
    
    # Build images if requested
    $imagesBuildSuccess = $true
    if ($BuildImages) {
        $imagesBuildSuccess = Build-ContainerImages -RegistryName $containerRegistryName -ResourceGroup $ResourceGroupName
        
        if (-not $imagesBuildSuccess -and -not $Force) {
            Write-Host "❌ Image build failed. Use -Force to continue with existing images." -ForegroundColor Red
            exit 1
        }
    }
    
    # Update container apps
    Write-Host "`n🚀 Updating container apps..." -ForegroundColor Green
    
    # Update backend app
    $backendImage = "${registryServer}/${AppName}-api:${Environment}-latest"
    Update-ContainerApp -AppName $backendAppName -ImageName $backendImage -ResourceGroup $ResourceGroupName
    
    # Update frontend app
    $frontendImage = "${registryServer}/${AppName}-ui:${Environment}-latest"
    Update-ContainerApp -AppName $frontendAppName -ImageName $frontendImage -ResourceGroup $ResourceGroupName
    
    # Show final results
    Write-Host "`n📊 Update Summary:" -ForegroundColor Green
    $duration = (Get-Date) - $global:UpdateStats.StartTime
    Write-Host "   Duration: $($duration.ToString('mm\:ss'))" -ForegroundColor Gray
    Write-Host "   Images built: $($global:UpdateStats.ImagesBuilt)" -ForegroundColor Gray
    Write-Host "   Apps updated: $($global:UpdateStats.AppsUpdated)" -ForegroundColor Gray
    Write-Host "   Failed operations: $($global:UpdateStats.FailedOperations.Count)" -ForegroundColor Gray
    
    if ($global:UpdateStats.FailedOperations.Count -gt 0) {
        Write-Host "`n❌ Failed Operations:" -ForegroundColor Red
        $global:UpdateStats.FailedOperations | ForEach-Object {
            Write-Host "   - $_" -ForegroundColor Red
        }
    }
    
    # Show updated state
    Show-CurrentImages -ResourceGroup $ResourceGroupName
    
    Write-Host "`n🎉 Container image update completed!" -ForegroundColor Green
    
    if ($global:UpdateStats.AppsUpdated -gt 0) {
        Write-Host "`n💡 Next Steps:" -ForegroundColor Yellow
        Write-Host "1. Test the updated applications to ensure they work correctly" -ForegroundColor Gray
        Write-Host "2. Monitor application logs for any issues: az containerapp logs show" -ForegroundColor Gray
        Write-Host "3. If issues occur, you can rollback using the previous image tags" -ForegroundColor Gray
    }
    
} catch {
    Write-Error "❌ Container image update failed: $_"
    Write-Host "`n💡 Troubleshooting Tips:" -ForegroundColor Yellow
    Write-Host "1. Ensure you're in the project root directory with Dockerfile.backend and Dockerfile.frontend" -ForegroundColor Gray
    Write-Host "2. Check Azure CLI authentication: az account show" -ForegroundColor Gray
    Write-Host "3. Verify the container registry and apps exist in the resource group" -ForegroundColor Gray
    Write-Host "4. Use -DryRun to preview changes before execution" -ForegroundColor Gray
    exit 1
}

Write-Host "`n✨ Container image update script completed!" -ForegroundColor Magenta
Write-Host "💡 Usage Examples:" -ForegroundColor Yellow
Write-Host "  Dry run: .\update-container-images.ps1 -ResourceGroupName 'my-rg' -DryRun" -ForegroundColor Gray
Write-Host "  Build & update: .\update-container-images.ps1 -ResourceGroupName 'my-rg' -BuildImages" -ForegroundColor Gray
Write-Host "  Update only: .\update-container-images.ps1 -ResourceGroupName 'my-rg' -BuildImages:$false" -ForegroundColor Gray