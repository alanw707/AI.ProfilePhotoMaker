#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Post-Migration Validation Script
    
.DESCRIPTION
    Validates that the image URL migration completed successfully and provides
    recommendations for re-enabling auto-repair functionality.
    
.PARAMETER ApiBaseUrl
    Base URL of the API (default: https://localhost:5032)
    
.PARAMETER SampleCount
    Number of sample images to test (default: 10)
    
.EXAMPLE
    ./validate-migration-success.ps1
    
.EXAMPLE
    ./validate-migration-success.ps1 -ApiBaseUrl "https://your-api.azurewebsites.net" -SampleCount 20
#>

param(
    [string]$ApiBaseUrl = "https://localhost:5032",
    [int]$SampleCount = 10
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Colors for output
$ColorSuccess = "Green"
$ColorWarning = "Yellow" 
$ColorError = "Red"
$ColorInfo = "Cyan"

function Write-Status {
    param([string]$Message, [string]$Color = "White")
    Write-Host "$(Get-Date -Format 'HH:mm:ss') - $Message" -ForegroundColor $Color
}

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "=" * 60 -ForegroundColor $ColorInfo
    Write-Host $Title -ForegroundColor $ColorInfo
    Write-Host "=" * 60 -ForegroundColor $ColorInfo
}

function Invoke-ApiRequest {
    param(
        [string]$Endpoint,
        [string]$Method = "GET"
    )
    
    $uri = "$ApiBaseUrl/api/$Endpoint"
    $headers = @{
        "Content-Type" = "application/json"
        "Accept" = "application/json"
    }
    
    try {
        $response = Invoke-RestMethod -Uri $uri -Method $Method -Headers $headers
        return $response
    }
    catch {
        Write-Status "API request failed: $($_.Exception.Message)" $ColorError
        throw
    }
}

function Test-ImageUrls {
    param([array]$ImageUrls)
    
    $results = @{
        Total = $ImageUrls.Count
        Accessible = 0
        NotFound = 0
        Errors = 0
        Details = @()
    }
    
    foreach ($url in $ImageUrls) {
        if (-not $url) { continue }
        
        try {
            $response = Invoke-WebRequest -Uri $url -Method Head -UseBasicParsing -TimeoutSec 10
            if ($response.StatusCode -eq 200) {
                $results.Accessible++
                $results.Details += @{
                    Url = $url
                    Status = "✅ Accessible"
                    StatusCode = $response.StatusCode
                }
            } else {
                $results.NotFound++
                $results.Details += @{
                    Url = $url
                    Status = "❌ HTTP $($response.StatusCode)"
                    StatusCode = $response.StatusCode
                }
            }
        }
        catch {
            $results.Errors++
            $results.Details += @{
                Url = $url
                Status = "❌ Error: $($_.Exception.Message)"
                StatusCode = "Error"
            }
        }
    }
    
    return $results
}

function Show-ValidationResults {
    param([object]$Validation)
    
    Write-Status "Migration Validation Results:" $ColorInfo
    Write-Host "  Migration Successful: $($Validation.ValidationResults.MigrationSuccessful)" -ForegroundColor $(if($Validation.ValidationResults.MigrationSuccessful) { $ColorSuccess } else { $ColorError })
    Write-Host "  Total Records: $($Validation.ValidationResults.TotalRecords)"
    Write-Host "  Remaining Relative Paths: $($Validation.ValidationResults.RemainingRelativePaths)" -ForegroundColor $(if($Validation.ValidationResults.RemainingRelativePaths -eq 0) { $ColorSuccess } else { $ColorWarning })
    Write-Host "  All URLs Correct: $($Validation.ValidationResults.AllUrlsCorrect)" -ForegroundColor $(if($Validation.ValidationResults.AllUrlsCorrect) { $ColorSuccess } else { $ColorError })
    
    Write-Host ""
    Write-Host "  Status: $($Validation.Status)" -ForegroundColor $(
        switch ($Validation.Status) {
            "MIGRATION_SUCCESSFUL" { $ColorSuccess }
            "MIGRATION_INCOMPLETE" { $ColorWarning }
            default { $ColorError }
        }
    )
    
    Write-Host "  Message: $($Validation.Message)"
}

function Show-UrlTestResults {
    param([object]$Results)
    
    Write-Status "URL Accessibility Test Results:" $ColorInfo
    Write-Host "  Total URLs Tested: $($Results.Total)"
    Write-Host "  Accessible: $($Results.Accessible)" -ForegroundColor $ColorSuccess
    Write-Host "  Not Found (404): $($Results.NotFound)" -ForegroundColor $(if($Results.NotFound -eq 0) { $ColorSuccess } else { $ColorWarning })
    Write-Host "  Errors: $($Results.Errors)" -ForegroundColor $(if($Results.Errors -eq 0) { $ColorSuccess } else { $ColorError })
    
    if ($Results.Total -gt 0) {
        $successRate = [math]::Round($Results.Accessible / $Results.Total * 100, 2)
        Write-Host "  Success Rate: $successRate%" -ForegroundColor $(if($successRate -ge 95) { $ColorSuccess } elseif($successRate -ge 80) { $ColorWarning } else { $ColorError })
    }
    
    # Show failed URLs for troubleshooting
    $failedTests = $Results.Details | Where-Object { $_.Status -notlike "*Accessible*" }
    if ($failedTests.Count -gt 0 -and $failedTests.Count -le 5) {
        Write-Host ""
        Write-Status "Failed URL Details:" $ColorWarning
        foreach ($failed in $failedTests) {
            Write-Host "  $($failed.Status): $($failed.Url)" -ForegroundColor $ColorWarning
        }
    }
}

# Main execution
try {
    Write-Section "Post-Migration Validation"
    Write-Status "API Base URL: $ApiBaseUrl" $ColorInfo
    Write-Status "Sample Count: $SampleCount" $ColorInfo
    
    # Step 1: Validate migration completion
    Write-Section "Step 1: Migration Completion Validation"
    Write-Status "Checking migration completion status..."
    
    $validation = Invoke-ApiRequest "migration/validate-migration" "POST"
    Show-ValidationResults $validation.data
    
    # Step 2: Test sample image URLs
    Write-Section "Step 2: Image URL Accessibility Testing"
    Write-Status "Testing sample image URL accessibility..."
    
    try {
        # Get sample images from the API
        $images = Invoke-ApiRequest "image/images"
        
        if ($images.data.images -and $images.data.images.Count -gt 0) {
            # Extract URLs from sample images
            $sampleUrls = @()
            $imagesToTest = $images.data.images | Select-Object -First $SampleCount
            
            foreach ($img in $imagesToTest) {
                if ($img.originalImageUrl) { $sampleUrls += $img.originalImageUrl }
                if ($img.processedImageUrl -and $img.processedImageUrl -ne $img.originalImageUrl) { 
                    $sampleUrls += $img.processedImageUrl 
                }
            }
            
            $sampleUrls = $sampleUrls | Select-Object -Unique
            
            Write-Status "Testing $($sampleUrls.Count) unique image URLs..."
            $urlTestResults = Test-ImageUrls $sampleUrls
            Show-UrlTestResults $urlTestResults
        } else {
            Write-Status "No images found for URL testing" $ColorWarning
        }
    }
    catch {
        Write-Status "Could not test image URLs: $($_.Exception.Message)" $ColorWarning
        Write-Status "This may be due to authentication requirements" $ColorWarning
    }
    
    # Step 3: Provide recommendations
    Write-Section "Step 3: Recommendations"
    
    $migrationSuccessful = $validation.data.ValidationResults.MigrationSuccessful
    $canReEnableAutoRepair = $validation.data.ReEnablementRecommendation.CanReEnableAutoRepair
    
    if ($migrationSuccessful -and $canReEnableAutoRepair) {
        Write-Status "✅ MIGRATION VALIDATION PASSED" $ColorSuccess
        Write-Host ""
        Write-Status "Recommendations:" $ColorInfo
        foreach ($step in $validation.data.ReEnablementRecommendation.NextSteps) {
            Write-Host "  $step"
        }
        
        Write-Host ""
        Write-Status "Auto-Repair Re-enablement:" $ColorSuccess
        Write-Host "  1. Update dashboard-state.service.ts to re-enable auto-repair"
        Write-Host "  2. Update image-state.service.ts to re-enable auto-repair"
        Write-Host "  3. Deploy the changes to production"
        Write-Host "  4. Monitor for 24-48 hours to ensure no false positives"
        
        Write-Host ""
        Write-Status "Monitoring Points:" $ColorInfo
        Write-Host "  - Image loading success rate"
        Write-Host "  - 404 error reduction"
        Write-Host "  - Auto-repair deletion accuracy"
        Write-Host "  - User dashboard functionality"
    } else {
        Write-Status "❌ MIGRATION VALIDATION FAILED" $ColorError
        Write-Host ""
        Write-Status "Issues Found:" $ColorError
        if ($validation.data.RemainingIssues) {
            foreach ($issue in $validation.data.RemainingIssues) {
                Write-Host "  - Record ID $($issue.Id): $($issue.OriginalImageUrl) / $($issue.ProcessedImageUrl)"
            }
        }
        
        Write-Host ""
        Write-Status "Required Actions:" $ColorWarning
        foreach ($step in $validation.data.ReEnablementRecommendation.NextSteps) {
            Write-Host "  $step"
        }
        
        Write-Host ""
        Write-Status "⚠️ DO NOT RE-ENABLE AUTO-REPAIR UNTIL ALL ISSUES ARE RESOLVED" $ColorError
    }
    
    # Step 4: Generate summary report
    Write-Section "Step 4: Validation Summary"
    
    $summary = @{
        Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
        MigrationSuccessful = $migrationSuccessful
        CanReEnableAutoRepair = $canReEnableAutoRepair
        TotalRecords = $validation.data.ValidationResults.TotalRecords
        RemainingIssues = $validation.data.ValidationResults.RemainingRelativePaths
        Status = $validation.data.Status
    }
    
    # Add URL test results if available
    if ($urlTestResults) {
        $summary.UrlTests = @{
            Total = $urlTestResults.Total
            Accessible = $urlTestResults.Accessible
            SuccessRate = if ($urlTestResults.Total -gt 0) { [math]::Round($urlTestResults.Accessible / $urlTestResults.Total * 100, 2) } else { 0 }
        }
    }
    
    $reportPath = "migration-validation-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    $summary | ConvertTo-Json -Depth 3 | Out-File -FilePath $reportPath -Encoding UTF8
    
    Write-Status "📊 Validation report saved to: $reportPath" $ColorInfo
    
    # Final status
    if ($migrationSuccessful) {
        Write-Status "🎉 Migration validation completed successfully!" $ColorSuccess
        Write-Status "Ready to re-enable auto-repair functionality." $ColorSuccess
    } else {
        Write-Status "⚠️ Migration validation found issues that need attention." $ColorWarning
        Write-Status "Please resolve issues before re-enabling auto-repair." $ColorWarning
    }
}
catch {
    Write-Status "❌ Validation failed: $($_.Exception.Message)" $ColorError
    exit 1
}