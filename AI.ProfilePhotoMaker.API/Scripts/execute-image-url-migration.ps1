#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Production Image URL Migration Script
    
.DESCRIPTION
    Safely migrates image URLs from relative paths to full Azure Blob URLs.
    This script addresses the issue where database stored relative paths (/prod/uploads/...)
    instead of full Azure URLs, causing 404 errors and triggering incorrect auto-repair.
    
.PARAMETER ApiBaseUrl
    Base URL of the API (default: https://localhost:5032)
    
.PARAMETER DryRun
    If specified, only performs analysis and dry-run without making changes
    
.PARAMETER Force
    If specified, skips confirmation prompts (use with caution)
    
.EXAMPLE
    # Analyze current state
    ./execute-image-url-migration.ps1 -DryRun
    
.EXAMPLE
    # Execute migration with confirmation
    ./execute-image-url-migration.ps1 -ApiBaseUrl "https://your-api.azurewebsites.net"
    
.EXAMPLE
    # Execute migration without prompts (automated deployment)
    ./execute-image-url-migration.ps1 -Force
#>

param(
    [string]$ApiBaseUrl = "https://localhost:5032",
    [switch]$DryRun,
    [switch]$Force
)

# Configuration
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
        [string]$Method = "GET",
        [object]$Body = $null
    )
    
    $uri = "$ApiBaseUrl/api/$Endpoint"
    $headers = @{
        "Content-Type" = "application/json"
        "Accept" = "application/json"
    }
    
    try {
        if ($Body) {
            $response = Invoke-RestMethod -Uri $uri -Method $Method -Headers $headers -Body ($Body | ConvertTo-Json)
        } else {
            $response = Invoke-RestMethod -Uri $uri -Method $Method -Headers $headers
        }
        return $response
    }
    catch {
        Write-Status "API request failed: $($_.Exception.Message)" $ColorError
        throw
    }
}

function Show-AnalysisResults {
    param([object]$Analysis)
    
    Write-Status "Database State:" $ColorInfo
    Write-Host "  Total Records: $($Analysis.DatabaseState.TotalRecords)"
    Write-Host "  Records with Relative Original Paths: $($Analysis.DatabaseState.RecordsWithRelativeOriginalPaths)"
    Write-Host "  Records with Relative Processed Paths: $($Analysis.DatabaseState.RecordsWithRelativeProcessedPaths)"
    Write-Host "  Estimated Affected Records: $($Analysis.DatabaseState.EstimatedAffectedRecords)" -ForegroundColor $ColorWarning
    
    Write-Status "Impact Assessment:" $ColorInfo
    Write-Host "  Migration Required: $($Analysis.Impact.MigrationRequired)"
    Write-Host "  Affected Percentage: $($Analysis.Impact.AffectedPercentage)%"
    Write-Host "  Estimated Processing Time: $($Analysis.Impact.EstimatedProcessingTime)"
    
    if ($Analysis.SampleData.SampleRecords.Count -gt 0) {
        Write-Status "Sample Records Requiring Migration:" $ColorInfo
        foreach ($record in $Analysis.SampleData.SampleRecords) {
            Write-Host "  ID $($record.Id): Original=$($record.RequiresOriginalConversion), Processed=$($record.RequiresProcessedConversion)"
        }
    }
}

function Show-MigrationResults {
    param([object]$Results, [bool]$IsDryRun = $false)
    
    $prefix = if ($IsDryRun) { "Dry Run " } else { "" }
    
    Write-Status "${prefix}Migration Results:" $ColorInfo
    Write-Host "  Success: $($Results.Results.Success)" -ForegroundColor $(if($Results.Results.Success) { $ColorSuccess } else { $ColorError })
    Write-Host "  Records Processed: $($Results.Results.TotalRecordsAnalyzed)"
    Write-Host "  Successful: $($Results.Results.WouldBeSuccessful)" -ForegroundColor $ColorSuccess
    Write-Host "  Failed: $($Results.Results.WouldFail)" -ForegroundColor $(if($Results.Results.WouldFail -eq 0) { $ColorSuccess } else { $ColorError })
    Write-Host "  Success Rate: $([math]::Round($Results.Results.SuccessRate, 2))%"
    Write-Host "  Processing Time: $($Results.Results.ProcessingTime)"
    
    if ($Results.ValidationIssues -and $Results.ValidationIssues.Count -gt 0) {
        Write-Status "Validation Issues:" $ColorWarning
        foreach ($issue in $Results.ValidationIssues) {
            Write-Host "  - $issue" -ForegroundColor $ColorWarning
        }
    }
    
    if ($Results.SampleConversions -and $Results.SampleConversions.Count -gt 0) {
        Write-Status "Sample Conversions:" $ColorInfo
        foreach ($conversion in $Results.SampleConversions) {
            Write-Host "  Record $($conversion.Id):"
            if ($conversion.OriginalConversion) {
                Write-Host "    Original: $($conversion.OriginalConversion.From) -> $($conversion.OriginalConversion.To)"
            }
            if ($conversion.ProcessedConversion) {
                Write-Host "    Processed: $($conversion.ProcessedConversion.From) -> $($conversion.ProcessedConversion.To)"
            }
        }
    }
}

function Confirm-Execution {
    param([string]$Message)
    
    if ($Force) {
        Write-Status "Force mode enabled - skipping confirmation" $ColorWarning
        return $true
    }
    
    Write-Host ""
    Write-Host $Message -ForegroundColor $ColorWarning
    $response = Read-Host "Do you want to continue? (y/N)"
    return $response -eq "y" -or $response -eq "Y"
}

# Main execution
try {
    Write-Section "Production Image URL Migration"
    Write-Status "API Base URL: $ApiBaseUrl" $ColorInfo
    Write-Status "Mode: $(if($DryRun) { 'Analysis and Dry Run Only' } else { 'Full Migration Execution' })" $ColorInfo
    
    # Step 1: Analyze current database state
    Write-Section "Step 1: Database Analysis"
    Write-Status "Analyzing current database state..."
    
    $analysis = Invoke-ApiRequest "migration/analyze-image-urls"
    Show-AnalysisResults $analysis.data
    
    if (-not $analysis.data.Impact.MigrationRequired) {
        Write-Status "✅ No migration required - all URLs are already in correct format!" $ColorSuccess
        exit 0
    }
    
    # Step 2: Dry run migration
    Write-Section "Step 2: Dry Run Validation"
    Write-Status "Executing dry run to validate migration logic..."
    
    $dryRunResult = Invoke-ApiRequest "migration/dry-run-image-migration" "POST"
    Show-MigrationResults $dryRunResult.data $true
    
    if (-not $dryRunResult.data.Results.Success) {
        Write-Status "❌ Dry run failed - migration cannot proceed safely!" $ColorError
        Write-Status "Please fix the validation issues shown above before retrying." $ColorError
        exit 1
    }
    
    Write-Status "✅ Dry run completed successfully - migration is safe to execute" $ColorSuccess
    
    # Exit here if this is only a dry run
    if ($DryRun) {
        Write-Status "Dry run completed. Use without -DryRun flag to execute actual migration." $ColorInfo
        exit 0
    }
    
    # Step 3: Confirmation and execution
    Write-Section "Step 3: Migration Execution"
    
    $confirmMessage = @"
⚠️  IMPORTANT: You are about to execute a production database migration!

This will modify $($analysis.data.DatabaseState.EstimatedAffectedRecords) image URL records in your database.

Before proceeding, ensure you have:
1. ✅ Created a database backup
2. ✅ Reviewed the dry run results above
3. ✅ Scheduled this during low-traffic period
4. ✅ Notified your team about the maintenance

The migration will convert relative paths like '/prod/uploads/image.jpg' 
to full Azure URLs like 'https://storage.blob.core.windows.net/images/prod/uploads/image.jpg'
"@

    if (-not (Confirm-Execution $confirmMessage)) {
        Write-Status "Migration cancelled by user." $ColorWarning
        exit 0
    }
    
    Write-Status "🚀 Executing production migration..." $ColorWarning
    
    $migrationResult = Invoke-ApiRequest "migration/execute-image-migration?confirmed=true" "POST"
    
    # Step 4: Show results
    Write-Section "Step 4: Migration Results"
    
    if ($migrationResult.data.Migration.Success) {
        Write-Status "✅ Migration completed successfully!" $ColorSuccess
        Write-Host "  Records Migrated: $($migrationResult.data.Migration.SuccessfullyMigrated)" -ForegroundColor $ColorSuccess
        Write-Host "  Processing Time: $($migrationResult.data.Migration.ProcessingTime)" -ForegroundColor $ColorSuccess
        Write-Host "  Success Rate: $([math]::Round($migrationResult.data.Migration.SuccessRate, 2))%" -ForegroundColor $ColorSuccess
    } else {
        Write-Status "⚠️ Migration completed with some errors" $ColorWarning
        Write-Host "  Successful: $($migrationResult.data.Migration.SuccessfullyMigrated)" -ForegroundColor $ColorSuccess
        Write-Host "  Failed: $($migrationResult.data.Migration.Failed)" -ForegroundColor $ColorError
        Write-Host "  Success Rate: $([math]::Round($migrationResult.data.Migration.SuccessRate, 2))%"
    }
    
    # Step 5: Validation
    Write-Section "Step 5: Migration Validation"
    Write-Status "Validating migration success..."
    
    $validation = Invoke-ApiRequest "migration/validate-migration" "POST"
    
    if ($validation.data.ValidationResults.MigrationSuccessful) {
        Write-Status "✅ Migration validation passed!" $ColorSuccess
        Write-Status "🔄 Auto-repair functionality can now be safely re-enabled" $ColorSuccess
    } else {
        Write-Status "⚠️ Migration validation found remaining issues" $ColorWarning
        Write-Status "Remaining relative paths: $($validation.data.ValidationResults.RemainingRelativePaths)" $ColorWarning
        Write-Status "🚫 Do NOT re-enable auto-repair until all issues are resolved" $ColorError
    }
    
    # Show next steps
    Write-Section "Next Steps"
    foreach ($step in $migrationResult.data.NextSteps) {
        Write-Host "  $step"
    }
    
    # Save rollback script
    if ($migrationResult.data.RollbackScript) {
        $rollbackPath = "rollback-migration-$(Get-Date -Format 'yyyyMMdd-HHmmss').sql"
        $migrationResult.data.RollbackScript | Out-File -FilePath $rollbackPath -Encoding UTF8
        Write-Status "💾 Rollback script saved to: $rollbackPath" $ColorInfo
    }
    
    Write-Status "Migration process completed!" $ColorSuccess
}
catch {
    Write-Status "❌ Migration failed: $($_.Exception.Message)" $ColorError
    Write-Status "Check API logs for detailed error information." $ColorError
    exit 1
}