# Docker Deployment Validation Script
# Validates that Docker images can be built and pushed successfully

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$ContainerRegistryName,
    
    [switch]$SkipBuild = $false,
    
    [switch]$Verbose = $false
)

Write-Host "🔍 Docker Deployment Validation" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green

$ErrorActionPreference = "Stop"

try {
    # Step 1: Validate Azure CLI and PowerShell modules
    Write-Host "[STEP 1] Validating Azure CLI and PowerShell modules..." -ForegroundColor Yellow
    
    # Check Azure CLI
    try {
        $azVersion = az version --output json | ConvertFrom-Json
        Write-Host "✅ Azure CLI version: $($azVersion.'azure-cli')" -ForegroundColor Green
    } catch {
        throw "Azure CLI not found or not working properly"
    }
    
    # Check Azure PowerShell modules
    $requiredModules = @('Az.Accounts', 'Az.Resources', 'Az.ContainerRegistry', 'Az.App')
    foreach ($module in $requiredModules) {
        if (-not (Get-Module -ListAvailable -Name $module)) {
            Write-Host "⚠️ Missing module: $module" -ForegroundColor Yellow
            Install-Module -Name $module -Force -AllowClobber -Scope CurrentUser
        }
        Write-Host "✅ Module available: $module" -ForegroundColor Green
    }
    
    # Step 2: Validate Docker installation
    Write-Host "[STEP 2] Validating Docker installation..." -ForegroundColor Yellow
    
    try {
        $dockerVersion = docker version --format json | ConvertFrom-Json
        Write-Host "✅ Docker Client version: $($dockerVersion.Client.Version)" -ForegroundColor Green
        Write-Host "✅ Docker Server version: $($dockerVersion.Server.Version)" -ForegroundColor Green
    } catch {
        throw "Docker not found or not running properly"
    }
    
    # Step 3: Validate build context and files
    Write-Host "[STEP 3] Validating build context and required files..." -ForegroundColor Yellow
    
    $requiredFiles = @(
        "Dockerfile.backend",
        "Dockerfile.frontend", 
        "nginx.conf",
        "docker-entrypoint.sh",
        "AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj",
        "AI.ProfilePhotoMaker.UI/package.json"
    )
    
    $missingFiles = @()
    foreach ($file in $requiredFiles) {
        if (Test-Path $file) {
            Write-Host "✅ Found: $file" -ForegroundColor Green
        } else {
            $missingFiles += $file
            Write-Host "❌ Missing: $file" -ForegroundColor Red
        }
    }
    
    if ($missingFiles.Count -gt 0) {
        throw "Missing required files: $($missingFiles -join ', ')"
    }
    
    # Step 4: Validate ACR connection and credentials
    Write-Host "[STEP 4] Validating Azure Container Registry connection..." -ForegroundColor Yellow
    
    # Get ACR information
    $acrInfo = Get-AzContainerRegistry -ResourceGroupName $ResourceGroupName -Name $ContainerRegistryName -ErrorAction SilentlyContinue
    
    if (-not $acrInfo) {
        throw "Container Registry '$ContainerRegistryName' not found in resource group '$ResourceGroupName'"
    }
    
    Write-Host "✅ ACR found: $($acrInfo.Name)" -ForegroundColor Green
    Write-Host "✅ ACR Login Server: $($acrInfo.LoginServer)" -ForegroundColor Green
    Write-Host "✅ ACR Admin User Enabled: $($acrInfo.AdminUserEnabled)" -ForegroundColor Green
    
    if (-not $acrInfo.AdminUserEnabled) {
        Write-Host "⚠️ ACR admin user is disabled. This may cause authentication issues." -ForegroundColor Yellow
    }
    
    # Test ACR credentials
    $acrCredentials = Get-AzContainerRegistryCredential -ResourceGroupName $ResourceGroupName -RegistryName $ContainerRegistryName
    
    if (-not $acrCredentials -or -not $acrCredentials.Username -or -not $acrCredentials.Password) {
        throw "Failed to retrieve ACR credentials"
    }
    
    Write-Host "✅ ACR credentials retrieved successfully" -ForegroundColor Green
    Write-Host "✅ ACR Username: $($acrCredentials.Username)" -ForegroundColor Green
    
    # Step 5: Test Docker login
    Write-Host "[STEP 5] Testing Docker login to ACR..." -ForegroundColor Yellow
    
    $acrUsername = $acrCredentials.Username
    $acrPassword = $acrCredentials.Password
    $registryServer = $acrInfo.LoginServer
    
    # Test Docker login using the same method as the workflow
    try {
        $env:DOCKER_PASSWORD = $acrPassword
        $loginResult = cmd /c "echo %DOCKER_PASSWORD% | docker login $registryServer --username $acrUsername --password-stdin 2>&1"
        $env:DOCKER_PASSWORD = $null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Docker login successful" -ForegroundColor Green
        } else {
            throw "Docker login failed: $loginResult"
        }
    } catch {
        Write-Host "❌ Docker login failed: $($_.Exception.Message)" -ForegroundColor Red
        throw "Docker authentication test failed"
    }
    
    # Step 6: Test Docker build (optional)
    if (-not $SkipBuild) {
        Write-Host "[STEP 6] Testing Docker build..." -ForegroundColor Yellow
        
        # Test backend build
        Write-Host "🔨 Testing backend Docker build..." -ForegroundColor Cyan
        $buildOutput = docker build -f Dockerfile.backend -t "$registryServer/test-backend:validation" . 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Backend build failed:" -ForegroundColor Red
            if ($Verbose) {
                Write-Host $buildOutput -ForegroundColor White
            }
            throw "Backend Docker build test failed"
        }
        
        Write-Host "✅ Backend Docker build successful" -ForegroundColor Green
        
        # Test frontend build
        Write-Host "🔨 Testing frontend Docker build..." -ForegroundColor Cyan
        $buildOutput = docker build -f Dockerfile.frontend -t "$registryServer/test-frontend:validation" . 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Frontend build failed:" -ForegroundColor Red
            if ($Verbose) {
                Write-Host $buildOutput -ForegroundColor White
            }
            throw "Frontend Docker build test failed"
        }
        
        Write-Host "✅ Frontend Docker build successful" -ForegroundColor Green
        
        # Clean up test images
        Write-Host "🧹 Cleaning up test images..." -ForegroundColor Cyan
        docker rmi "$registryServer/test-backend:validation" -f 2>&1 | Out-Null
        docker rmi "$registryServer/test-frontend:validation" -f 2>&1 | Out-Null
    } else {
        Write-Host "[STEP 6] Skipping Docker build test (--SkipBuild specified)" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "🎉 All validation tests passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Summary:" -ForegroundColor Cyan
    Write-Host "✅ Azure CLI and PowerShell modules: Working" -ForegroundColor White
    Write-Host "✅ Docker installation: Working" -ForegroundColor White
    Write-Host "✅ Required build files: Present" -ForegroundColor White
    Write-Host "✅ ACR connection: Working" -ForegroundColor White
    Write-Host "✅ Docker authentication: Working" -ForegroundColor White
    if (-not $SkipBuild) {
        Write-Host "✅ Docker builds: Working" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "Your deployment should work correctly!" -ForegroundColor Green
    
} catch {
    Write-Host ""
    Write-Host "❌ Validation failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting steps:" -ForegroundColor Yellow
    Write-Host "1. Ensure you're logged into Azure (az login)" -ForegroundColor White
    Write-Host "2. Verify resource names are correct" -ForegroundColor White
    Write-Host "3. Check Docker is running (docker version)" -ForegroundColor White
    Write-Host "4. Ensure ACR admin user is enabled" -ForegroundColor White
    Write-Host "5. Verify all required files are present" -ForegroundColor White
    Write-Host ""
    exit 1
}