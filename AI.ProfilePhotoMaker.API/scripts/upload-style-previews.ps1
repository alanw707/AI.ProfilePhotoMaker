#!/usr/bin/env pwsh
# Upload Style Preview Images to Azure Blob Storage
# This script uploads all style preview images from the local style-previews directory
# to Azure Blob Storage for the AI ProfilePhotoMaker application

param(
    [string]$ConnectionString = $env:AZURE_STORAGE_CONNECTION_STRING,
    [string]$ContainerName = "profile-images-staging",
    [string]$PreviewsPath = "../style-previews",
    [switch]$Force = $false,
    [switch]$DryRun = $false,
    [switch]$Verbose = $false
)

# Check if Az.Storage module is installed
if (-not (Get-Module -ListAvailable -Name Az.Storage)) {
    Write-Host "Installing Az.Storage module..." -ForegroundColor Yellow
    Install-Module -Name Az.Storage -Force -AllowClobber -Scope CurrentUser
}

Import-Module Az.Storage

# Validate parameters
if ([string]::IsNullOrEmpty($ConnectionString)) {
    Write-Error "Azure Storage connection string is required. Set AZURE_STORAGE_CONNECTION_STRING environment variable or pass -ConnectionString parameter."
    Write-Host "Usage examples:" -ForegroundColor Cyan
    Write-Host "  export AZURE_STORAGE_CONNECTION_STRING='DefaultEndpointsProtocol=https;...'" -ForegroundColor Gray
    Write-Host "  ./upload-style-previews.ps1" -ForegroundColor Gray
    Write-Host "  ./upload-style-previews.ps1 -ConnectionString 'DefaultEndpointsProtocol=https;...'" -ForegroundColor Gray
    exit 1
}

if (-not (Test-Path $PreviewsPath)) {
    Write-Error "Style previews directory not found: $PreviewsPath"
    exit 1
}

# Initialize Azure Storage context
try {
    $ctx = New-AzStorageContext -ConnectionString $ConnectionString -ErrorAction Stop
    Write-Host "✅ Connected to Azure Storage successfully" -ForegroundColor Green
} catch {
    Write-Error "Failed to connect to Azure Storage: $($_.Exception.Message)"
    exit 1
}

# Ensure container exists
try {
    $container = Get-AzStorageContainer -Name $ContainerName -Context $ctx -ErrorAction SilentlyContinue
    if (-not $container) {
        if ($DryRun) {
            Write-Host "🔍 DRY RUN: Would create container '$ContainerName'" -ForegroundColor Yellow
        } else {
            $container = New-AzStorageContainer -Name $ContainerName -Context $ctx -Permission Blob
            Write-Host "✅ Created container '$ContainerName'" -ForegroundColor Green
        }
    } else {
        Write-Host "✅ Container '$ContainerName' already exists" -ForegroundColor Green
    }
} catch {
    Write-Error "Failed to create/verify container: $($_.Exception.Message)"
    exit 1
}

# Get all image files from style-previews directory
$imageFiles = Get-ChildItem -Path $PreviewsPath -Filter "*.jpg" | Where-Object { $_.Length -gt 0 }

if ($imageFiles.Count -eq 0) {
    Write-Warning "No valid .jpg files found in $PreviewsPath"
    exit 0
}

Write-Host "📋 Found $($imageFiles.Count) style preview images to upload" -ForegroundColor Cyan

# Track upload statistics
$uploadStats = @{
    Total = $imageFiles.Count
    Uploaded = 0
    Skipped = 0
    Failed = 0
    TotalSize = 0
}

# Upload each file
foreach ($file in $imageFiles) {
    $fileName = $file.Name
    $blobName = "style-previews/$fileName"
    $fileSizeKB = [math]::Round($file.Length / 1024, 2)
    
    try {
        # Check if blob already exists
        $existingBlob = Get-AzStorageBlob -Container $ContainerName -Blob $blobName -Context $ctx -ErrorAction SilentlyContinue
        
        if ($existingBlob -and -not $Force) {
            if ($Verbose) {
                Write-Host "⏭️  Skipping $fileName (already exists, use -Force to overwrite)" -ForegroundColor Yellow
            }
            $uploadStats.Skipped++
            continue
        }
        
        if ($DryRun) {
            $action = if ($existingBlob) { "overwrite" } else { "upload" }
            Write-Host "🔍 DRY RUN: Would $action $fileName ($fileSizeKB KB) → $blobName" -ForegroundColor Yellow
            $uploadStats.Uploaded++
        } else {
            # Upload the file
            $blob = Set-AzStorageBlobContent -File $file.FullName -Container $ContainerName -Blob $blobName -Context $ctx -Force:$Force -ErrorAction Stop
            
            # Set content type
            $blob | Set-AzStorageBlobContent -Properties @{ContentType="image/jpeg"} -ErrorAction SilentlyContinue
            
            Write-Host "✅ Uploaded $fileName ($fileSizeKB KB) → $blobName" -ForegroundColor Green
            $uploadStats.Uploaded++
            $uploadStats.TotalSize += $file.Length
        }
        
    } catch {
        Write-Error "❌ Failed to upload $fileName : $($_.Exception.Message)"
        $uploadStats.Failed++
    }
}

# Display summary
Write-Host "`n📊 Upload Summary:" -ForegroundColor Cyan
Write-Host "   Total files: $($uploadStats.Total)" -ForegroundColor White
Write-Host "   Uploaded: $($uploadStats.Uploaded)" -ForegroundColor Green
Write-Host "   Skipped: $($uploadStats.Skipped)" -ForegroundColor Yellow
Write-Host "   Failed: $($uploadStats.Failed)" -ForegroundColor Red

if (-not $DryRun -and $uploadStats.TotalSize -gt 0) {
    $totalSizeMB = [math]::Round($uploadStats.TotalSize / 1MB, 2)
    Write-Host "   Total uploaded: $totalSizeMB MB" -ForegroundColor Cyan
}

# Generate public URLs for verification
if ($uploadStats.Uploaded -gt 0 -and -not $DryRun) {
    Write-Host "`n🔗 Sample URLs (for verification):" -ForegroundColor Cyan
    
    # Extract storage account name from connection string
    if ($ConnectionString -match "AccountName=([^;]+)") {
        $accountName = $Matches[1]
        $baseUrl = "https://$accountName.blob.core.windows.net/$ContainerName/style-previews"
        
        # Show first 3 uploaded files as examples
        $sampleFiles = $imageFiles | Select-Object -First 3
        foreach ($file in $sampleFiles) {
            Write-Host "   $baseUrl/$($file.Name)" -ForegroundColor Gray
        }
    }
}

# Test API endpoint if not dry run
if (-not $DryRun -and $uploadStats.Uploaded -gt 0) {
    Write-Host "`n🔄 Testing API endpoint..." -ForegroundColor Cyan
    try {
        # Try to make a request to the style preview list endpoint
        $apiResponse = Invoke-RestMethod -Uri "https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io/api/style-preview/list" -Method GET -ErrorAction SilentlyContinue
        
        if ($apiResponse -and $apiResponse.success) {
            Write-Host "✅ API endpoint working! Found $($apiResponse.count) style previews" -ForegroundColor Green
        } else {
            Write-Host "⚠️  API endpoint returned unexpected response" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "⚠️  Could not test API endpoint (this is normal if API is not running)" -ForegroundColor Yellow
    }
}

if ($uploadStats.Failed -eq 0) {
    Write-Host "`n🎉 Upload completed successfully!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n⚠️  Upload completed with $($uploadStats.Failed) errors" -ForegroundColor Yellow
    exit 1
}