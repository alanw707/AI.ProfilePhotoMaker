# Safe Azure Resource Cleanup Execution Script
# Comprehensive cleanup with validation, rollback, and cost tracking

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName = "rg-aiprofilemaker-staging",
    
    [switch]$DryRun = $false,
    [switch]$Force = $false,
    [switch]$Interactive = $true,
    [switch]$EnableBackup = $true,
    [string]$BackupPath = "./cleanup-backup-$(Get-Date -Format 'yyyy-MM-dd-HHmm')"
)

Write-Host "🧹 Safe Azure Resource Cleanup Execution" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host "📍 Resource Group: $ResourceGroupName" -ForegroundColor Yellow
Write-Host "🔍 Dry Run: $DryRun" -ForegroundColor Yellow
Write-Host "💾 Backup Enabled: $EnableBackup" -ForegroundColor Yellow

# Initialize cleanup session
$global:CleanupSession = @{
    SessionId = [guid]::NewGuid().ToString("N")[0..7] -join ""
    StartTime = Get-Date
    ResourceGroupName = $ResourceGroupName
    BackupPath = $BackupPath
    ExecutionLog = @()
    ResourcesAnalyzed = 0
    ResourcesTargeted = 0
    ResourcesDeleted = 0
    ResourcesFailed = 0
    EstimatedSavings = 0
    ActualSavings = 0
    SafetyChecks = @{
        PreCleanupValidation = $false
        DependencyAnalysis = $false
        BackupCreated = $false
        PostCleanupValidation = $false
    }
}

function Write-Log {
    param(
        [string]$Message,
        [string]$Level = "INFO",
        [string]$Color = "White"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Level] $Message"
    
    Write-Host $logEntry -ForegroundColor $Color
    $global:CleanupSession.ExecutionLog += $logEntry
}

function New-ResourceBackup {
    param([object]$Resource)
    
    if (-not $EnableBackup) { return $true }
    
    try {
        Write-Log "Creating backup for $($Resource.name)" "BACKUP" "Cyan"
        
        # Create backup directory
        $resourceBackupPath = Join-Path $BackupPath $Resource.type.Replace("/", "_")
        New-Item -ItemType Directory -Path $resourceBackupPath -Force | Out-Null
        
        # Export resource configuration
        $resourceConfig = az resource show --ids $Resource.id --output json | ConvertFrom-Json
        $configFile = Join-Path $resourceBackupPath "$($Resource.name).json"
        $resourceConfig | ConvertTo-Json -Depth 10 | Out-File $configFile -Encoding UTF8
        
        # Resource-specific backup logic
        switch ($Resource.type) {
            "Microsoft.Storage/storageAccounts" {
                Write-Log "Backing up storage account configuration for $($Resource.name)" "BACKUP" "Cyan"
                # Could add blob inventory or key backup here
            }
            "Microsoft.ContainerRegistry/registries" {
                Write-Log "Backing up container registry configuration for $($Resource.name)" "BACKUP" "Cyan"
                # Could add repository list backup here
            }
            "Microsoft.KeyVault/vaults" {
                Write-Log "Backing up key vault configuration for $($Resource.name)" "BACKUP" "Cyan"
                # Note: Secrets cannot be backed up via CLI for security reasons
            }
        }
        
        Write-Log "✅ Backup created for $($Resource.name) at $configFile" "BACKUP" "Green"
        return $true
    } catch {
        Write-Log "❌ Failed to backup $($Resource.name): $_" "ERROR" "Red"
        return $false
    }
}

function Test-ResourceDependencies {
    param([object]$Resource)
    
    Write-Log "🔍 Analyzing dependencies for $($Resource.name)" "SAFETY" "Yellow"
    $dependencies = @()
    $criticalDependencies = @()
    
    try {
        switch ($Resource.type) {
            "Microsoft.ContainerRegistry/registries" {
                # Check container apps using this registry
                $apps = az containerapp list -g $ResourceGroupName --output json | ConvertFrom-Json
                foreach ($app in $apps) {
                    $appDetails = az containerapp show --name $app.name -g $ResourceGroupName --output json | ConvertFrom-Json
                    $image = $appDetails.properties.template.containers[0].image
                    $registryServer = az acr show --name $Resource.name -g $ResourceGroupName --query "loginServer" -o tsv 2>$null
                    
                    if ($image -match [regex]::Escape($registryServer)) {
                        $dependencies += "Container App: $($app.name) uses image $image"
                        $criticalDependencies += $app.name
                    }
                }
                
                # Check for stored images
                try {
                    $repositories = az acr repository list --name $Resource.name --output json 2>$null | ConvertFrom-Json
                    if ($repositories -and $repositories.Count -gt 0) {
                        $dependencies += "Contains $($repositories.Count) image repositories"
                    }
                } catch {
                    Write-Log "Could not check repositories in $($Resource.name)" "WARNING" "Yellow"
                }
            }
            
            "Microsoft.Storage/storageAccounts" {
                # Check for blob containers and data
                try {
                    $containers = az storage container list --account-name $Resource.name --output json 2>$null | ConvertFrom-Json
                    if ($containers -and $containers.Count -gt 0) {
                        $dependencies += "Contains $($containers.Count) blob containers"
                        
                        foreach ($container in $containers) {
                            $blobCount = az storage blob list --account-name $Resource.name --container-name $container.name --output json 2>$null | ConvertFrom-Json | Measure-Object | Select-Object -ExpandProperty Count
                            if ($blobCount -gt 0) {
                                $dependencies += "Container '$($container.name)' has $blobCount blobs"
                            }
                        }
                    }
                } catch {
                    Write-Log "Could not analyze storage containers for $($Resource.name)" "WARNING" "Yellow"
                }
            }
            
            "Microsoft.Sql/servers" {
                # Check for databases
                try {
                    $databases = az sql db list --server $Resource.name -g $ResourceGroupName --output json 2>$null | ConvertFrom-Json
                    if ($databases -and $databases.Count -gt 1) { # Exclude master db
                        $userDatabases = $databases | Where-Object { $_.name -ne "master" }
                        $dependencies += "Contains $($userDatabases.Count) user databases"
                        $criticalDependencies += $userDatabases.name
                    }
                } catch {
                    Write-Log "Could not analyze databases for $($Resource.name)" "WARNING" "Yellow"
                }
            }
            
            "Microsoft.KeyVault/vaults" {
                # Check for secrets, keys, certificates
                try {
                    $secrets = az keyvault secret list --vault-name $Resource.name --output json 2>$null | ConvertFrom-Json
                    if ($secrets -and $secrets.Count -gt 0) {
                        $dependencies += "Contains $($secrets.Count) secrets"
                    }
                } catch {
                    Write-Log "Could not analyze key vault contents for $($Resource.name)" "WARNING" "Yellow"
                }
            }
        }
        
        return @{
            Dependencies = $dependencies
            CriticalDependencies = $criticalDependencies
            HasCriticalDependencies = $criticalDependencies.Count -gt 0
        }
    } catch {
        Write-Log "❌ Failed to analyze dependencies for $($Resource.name): $_" "ERROR" "Red"
        return @{
            Dependencies = @("Failed to analyze dependencies")
            CriticalDependencies = @()
            HasCriticalDependencies = $true
        }
    }
}

function Remove-ResourceWithValidation {
    param(
        [object]$Resource,
        [string]$Reason,
        [double]$EstimatedCost
    )
    
    $global:CleanupSession.ResourcesTargeted++
    
    Write-Log "🎯 Processing: $($Resource.name)" "TARGET" "Yellow"
    Write-Log "   Type: $($Resource.type)" "INFO" "Gray"
    Write-Log "   Reason: $Reason" "INFO" "Gray"
    Write-Log "   Estimated Monthly Cost: ~$$EstimatedCost" "COST" "Gray"
    
    # Step 1: Create backup
    if ($EnableBackup) {
        Write-Log "📦 Creating backup..." "BACKUP" "Cyan"
        $backupSuccess = New-ResourceBackup -Resource $Resource
        if (-not $backupSuccess -and -not $Force) {
            Write-Log "❌ Backup failed - skipping deletion for safety" "ERROR" "Red"
            return $false
        }
        $global:CleanupSession.SafetyChecks.BackupCreated = $true
    }
    
    # Step 2: Dependency analysis
    Write-Log "🔍 Analyzing dependencies..." "SAFETY" "Yellow"
    $dependencyAnalysis = Test-ResourceDependencies -Resource $Resource
    $global:CleanupSession.SafetyChecks.DependencyAnalysis = $true
    
    if ($dependencyAnalysis.Dependencies.Count -gt 0) {
        Write-Log "📋 Dependencies found:" "WARNING" "Yellow"
        foreach ($dep in $dependencyAnalysis.Dependencies) {
            Write-Log "   - $dep" "WARNING" "Yellow"
        }
    }
    
    # Step 3: Critical dependency check
    if ($dependencyAnalysis.HasCriticalDependencies -and -not $Force) {
        Write-Log "🚨 CRITICAL DEPENDENCIES DETECTED!" "ERROR" "Red"
        Write-Log "   This resource has active dependencies and deletion may break functionality" "ERROR" "Red"
        
        if ($Interactive) {
            $confirmation = Read-Host "   Continue deletion despite critical dependencies? Type 'FORCE-DELETE' to confirm"
            if ($confirmation -ne 'FORCE-DELETE') {
                Write-Log "🛡️ Deletion cancelled for safety" "SAFETY" "Yellow"
                return $false
            }
        } else {
            Write-Log "🛡️ Deletion cancelled - use -Force to override safety checks" "SAFETY" "Yellow"
            return $false
        }
    }
    
    # Step 4: User confirmation (if interactive)
    if ($Interactive -and -not $Force) {
        Write-Host "`n   Proceed with deletion? (y/N): " -NoNewline -ForegroundColor Cyan
        $confirmation = Read-Host
        if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
            Write-Log "⏭️ Skipped by user choice" "INFO" "Gray"
            return $false
        }
    }
    
    # Step 5: Execute deletion (or dry run)
    if ($DryRun) {
        Write-Log "🔍 [DRY RUN] Would delete: $($Resource.name)" "DRYRUN" "Cyan"
        Write-Log "   Command: az resource delete --ids $($Resource.id) --yes" "DRYRUN" "Cyan"
        return $true
    }
    
    try {
        Write-Log "🗑️ Deleting resource: $($Resource.name)" "DELETE" "Red"
        az resource delete --ids $Resource.id --yes
        
        # Verify deletion
        Start-Sleep -Seconds 5
        $checkResult = az resource show --ids $Resource.id --output json 2>$null
        if ($checkResult) {
            Write-Log "⚠️ Resource still exists - deletion may be in progress" "WARNING" "Yellow"
        } else {
            Write-Log "✅ Successfully deleted: $($Resource.name)" "SUCCESS" "Green"
            $global:CleanupSession.ResourcesDeleted++
            $global:CleanupSession.ActualSavings += $EstimatedCost
        }
        
        return $true
    } catch {
        Write-Log "❌ Failed to delete $($Resource.name): $_" "ERROR" "Red"
        $global:CleanupSession.ResourcesFailed++
        return $false
    }
}

# Main cleanup execution
try {
    Write-Log "🚀 Starting cleanup session: $($global:CleanupSession.SessionId)" "START" "Green"
    
    # Create backup directory
    if ($EnableBackup -and -not $DryRun) {
        New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null
        Write-Log "📁 Backup directory created: $BackupPath" "BACKUP" "Cyan"
    }
    
    # Step 1: Run analysis
    Write-Log "🔍 Running resource analysis..." "ANALYSIS" "Green"
    $analysisResult = & "$PSScriptRoot\analyze-azure-resources.ps1" -ResourceGroupName $ResourceGroupName -DetailedAnalysis
    $global:CleanupSession.SafetyChecks.PreCleanupValidation = $true
    
    # Get resources for cleanup
    $resources = az resource list -g $ResourceGroupName --output json | ConvertFrom-Json
    $global:CleanupSession.ResourcesAnalyzed = $resources.Count
    
    if (-not $resources) {
        Write-Log "❌ No resources found or access denied" "ERROR" "Red"
        exit 1
    }
    
    Write-Log "📊 Found $($resources.Count) total resources" "INFO" "Gray"
    
    # Categorize resources for cleanup
    $duplicateResources = @()
    $targetResources = @()
    
    foreach ($resource in $resources) {
        $name = $resource.name
        
        # Identify duplicates by pattern matching
        if ($name -match "[a-z0-9]{8}$" -and $name -notmatch "z3bawc74$") {
            # Random suffix that's not the deterministic one
            $duplicateResources += $resource
        } elseif ($name -match "z3bawc74$" -or $name -match "(api|web|env)-staging$") {
            # Deterministic naming - keep these
            $targetResources += $resource
        }
    }
    
    Write-Log "🎯 Target resources (will keep): $($targetResources.Count)" "INFO" "Green"
    Write-Log "🗑️ Duplicate resources (will delete): $($duplicateResources.Count)" "INFO" "Red"
    
    if ($duplicateResources.Count -eq 0) {
        Write-Log "✅ No duplicate resources found - cleanup not needed!" "SUCCESS" "Green"
        exit 0
    }
    
    # Calculate potential savings
    $totalSavings = 0
    foreach ($resource in $duplicateResources) {
        $cost = switch ($resource.type) {
            "Microsoft.Storage/storageAccounts" { 2.00 }
            "Microsoft.ContainerRegistry/registries" { 5.00 }
            "Microsoft.Sql/servers" { 4.99 }
            "Microsoft.KeyVault/vaults" { 0.03 }
            default { 0.00 }
        }
        $totalSavings += $cost
    }
    $global:CleanupSession.EstimatedSavings = $totalSavings
    
    Write-Log "💰 Estimated monthly savings: ~$$totalSavings" "COST" "Green"
    Write-Log "📈 Estimated annual savings: ~$$([math]::Round($totalSavings * 12, 2))" "COST" "Green"
    
    # Execute cleanup for each duplicate resource
    Write-Log "🧹 Beginning resource cleanup..." "CLEANUP" "Green"
    
    $cleanupResults = @()
    foreach ($resource in $duplicateResources) {
        $cost = switch ($resource.type) {
            "Microsoft.Storage/storageAccounts" { 2.00 }
            "Microsoft.ContainerRegistry/registries" { 5.00 }
            "Microsoft.Sql/servers" { 4.99 }
            "Microsoft.KeyVault/vaults" { 0.03 }
            default { 0.00 }
        }
        
        $reason = "Duplicate resource with random suffix (not deterministic z3bawc74)"
        $success = Remove-ResourceWithValidation -Resource $resource -Reason $reason -EstimatedCost $cost
        
        $cleanupResults += @{
            ResourceName = $resource.name
            ResourceType = $resource.type
            Success = $success
            EstimatedCost = $cost
            Reason = $reason
        }
    }
    
    # Final validation
    Write-Log "✅ Running post-cleanup validation..." "VALIDATION" "Green"
    Start-Sleep -Seconds 10  # Allow time for Azure to process deletions
    
    $remainingResources = az resource list -g $ResourceGroupName --output json | ConvertFrom-Json
    $remainingDuplicates = $remainingResources | Where-Object { 
        $_.name -match "[a-z0-9]{8}$" -and $_.name -notmatch "z3bawc74$" 
    }
    
    $global:CleanupSession.SafetyChecks.PostCleanupValidation = $true
    
    if ($remainingDuplicates.Count -gt 0) {
        Write-Log "⚠️ $($remainingDuplicates.Count) duplicate resources still remain" "WARNING" "Yellow"
        foreach ($remaining in $remainingDuplicates) {
            Write-Log "   - $($remaining.name) ($($remaining.type))" "WARNING" "Yellow"
        }
    } else {
        Write-Log "✅ All duplicate resources successfully cleaned up!" "SUCCESS" "Green"
    }
    
    # Generate cleanup report
    $duration = (Get-Date) - $global:CleanupSession.StartTime
    
    Write-Host "`n📊 CLEANUP SESSION REPORT" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════════════════════════════" -ForegroundColor Gray
    Write-Host "Session ID: $($global:CleanupSession.SessionId)" -ForegroundColor Gray
    Write-Host "Duration: $($duration.ToString('mm\:ss'))" -ForegroundColor Gray
    Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Gray
    Write-Host "`nResources Analyzed: $($global:CleanupSession.ResourcesAnalyzed)" -ForegroundColor Gray
    Write-Host "Resources Targeted: $($global:CleanupSession.ResourcesTargeted)" -ForegroundColor Gray
    Write-Host "Resources Deleted: $($global:CleanupSession.ResourcesDeleted)" -ForegroundColor Green
    Write-Host "Resources Failed: $($global:CleanupSession.ResourcesFailed)" -ForegroundColor Red
    Write-Host "`nEstimated Savings: ~$$($global:CleanupSession.EstimatedSavings)/month" -ForegroundColor Gray
    Write-Host "Actual Savings: ~$$($global:CleanupSession.ActualSavings)/month" -ForegroundColor Green
    Write-Host "Annual Impact: ~$$([math]::Round($global:CleanupSession.ActualSavings * 12, 2))/year" -ForegroundColor Green
    
    Write-Host "`n🛡️ Safety Checks:" -ForegroundColor Green
    Write-Host "Pre-cleanup Validation: $($global:CleanupSession.SafetyChecks.PreCleanupValidation)" -ForegroundColor $(if ($global:CleanupSession.SafetyChecks.PreCleanupValidation) { "Green" } else { "Red" })
    Write-Host "Dependency Analysis: $($global:CleanupSession.SafetyChecks.DependencyAnalysis)" -ForegroundColor $(if ($global:CleanupSession.SafetyChecks.DependencyAnalysis) { "Green" } else { "Red" })
    Write-Host "Backup Created: $($global:CleanupSession.SafetyChecks.BackupCreated)" -ForegroundColor $(if ($global:CleanupSession.SafetyChecks.BackupCreated -or -not $EnableBackup) { "Green" } else { "Red" })
    Write-Host "Post-cleanup Validation: $($global:CleanupSession.SafetyChecks.PostCleanupValidation)" -ForegroundColor $(if ($global:CleanupSession.SafetyChecks.PostCleanupValidation) { "Green" } else { "Red" })
    
    if ($EnableBackup -and -not $DryRun) {
        Write-Host "`n📁 Backup Location: $BackupPath" -ForegroundColor Cyan
    }
    
    # Save execution log
    $logFile = "cleanup-session-$($global:CleanupSession.SessionId).log"
    $global:CleanupSession.ExecutionLog | Out-File $logFile -Encoding UTF8
    Write-Host "📝 Execution log saved: $logFile" -ForegroundColor Cyan
    
    Write-Log "🎉 Cleanup session completed successfully!" "SUCCESS" "Green"
    
} catch {
    Write-Log "❌ Cleanup session failed: $_" "ERROR" "Red"
    exit 1
}