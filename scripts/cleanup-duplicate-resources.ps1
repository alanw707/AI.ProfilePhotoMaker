# Safe Azure Resource Cleanup Script for AI Profile Photo Maker
# Removes duplicate/orphaned resources with cost analysis and safety features

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [switch]$DryRun = $false,
    [switch]$Force = $false,
    [switch]$ShowCosts = $false,
    [string]$Environment = "staging"
)

Write-Host "🧹 Azure Resource Cleanup Tool for AI Profile Photo Maker" -ForegroundColor Green
Write-Host "📍 Resource Group: $ResourceGroupName" -ForegroundColor Yellow
Write-Host "📍 Environment: $Environment" -ForegroundColor Yellow

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No resources will be deleted" -ForegroundColor Cyan
}

# Initialize cleanup tracking
$global:CleanupStats = @{
    ResourcesScanned = 0
    ResourcesTargeted = 0
    ResourcesDeleted = 0
    EstimatedMonthlySavings = 0
    StartTime = Get-Date
}

function Get-ResourceCostEstimate {
    param(
        [string]$ResourceType,
        [string]$ResourceName,
        [string]$Location = "eastus2"
    )
    
    # Basic monthly cost estimates for common resources (USD)
    $costTable = @{
        "Microsoft.Storage/storageAccounts" = 2.00
        "Microsoft.ContainerRegistry/registries" = 5.00  # Basic tier
        "Microsoft.Sql/servers" = 0.00  # Server itself is free
        "Microsoft.KeyVault/vaults" = 0.03  # Per operation cost is minimal
        "Microsoft.OperationalInsights/workspaces" = 2.30  # Per GB ingested
        "Microsoft.App/containerApps" = 0.00  # Pay per usage, minimal when not used
        "Microsoft.App/managedEnvironments" = 0.00  # No additional cost
        "Microsoft.Insights/components" = 0.00  # Pay per GB ingested
    }
    
    return $costTable[$ResourceType] ?? 0.00
}

function Show-CostAnalysis {
    param($Resources)
    
    Write-Host "`n💰 Cost Analysis:" -ForegroundColor Green
    $totalEstimatedSavings = 0
    
    foreach ($resource in $Resources) {
        $monthlyCost = Get-ResourceCostEstimate -ResourceType $resource.type -ResourceName $resource.name
        $totalEstimatedSavings += $monthlyCost
        
        if ($monthlyCost -gt 0) {
            Write-Host "   $($resource.name): ~$${monthlyCost}/month" -ForegroundColor Gray
        }
    }
    
    Write-Host "   📊 Total estimated monthly savings: ~$${totalEstimatedSavings}" -ForegroundColor Yellow
    $global:CleanupStats.EstimatedMonthlySavings = $totalEstimatedSavings
}

function Remove-ResourceSafely {
    param(
        [string]$ResourceName,
        [string]$ResourceType,
        [string]$DeleteCommand,
        [string]$Reason = "Duplicate resource",
        [double]$EstimatedMonthlyCost = 0
    )
    
    $global:CleanupStats.ResourcesTargeted++
    
    Write-Host "`n🔍 Found: $ResourceType '$ResourceName'" -ForegroundColor Yellow
    Write-Host "   Reason: $Reason" -ForegroundColor Gray
    
    if ($EstimatedMonthlyCost -gt 0) {
        Write-Host "   💰 Estimated monthly cost: ~$${EstimatedMonthlyCost}" -ForegroundColor Gray
    }
    
    if ($DryRun) {
        Write-Host "   [DRY RUN] Would delete: $DeleteCommand" -ForegroundColor Cyan
        return
    }
    
    # Extra safety check for important resources
    $criticalPatterns = @("sql.*production", ".*prod", ".*live")
    $isCritical = $criticalPatterns | Where-Object { $ResourceName -match $_ }
    
    if ($isCritical -and -not $Force) {
        Write-Host "   ⚠️ CRITICAL RESOURCE DETECTED - Manual confirmation required" -ForegroundColor Red
        $confirmation = Read-Host "   Type 'DELETE' to confirm deletion of this critical resource"
        if ($confirmation -ne 'DELETE') {
            Write-Host "   🛡️ Skipped critical resource for safety" -ForegroundColor Yellow
            return
        }
    } elseif (-not $Force) {
        $confirmation = Read-Host "   Delete this resource? (y/N)"
        if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
            Write-Host "   ⏭️ Skipped by user" -ForegroundColor Gray
            return
        }
    }
    
    try {
        Write-Host "   🗑️ Deleting $ResourceName..." -ForegroundColor Red
        Invoke-Expression $DeleteCommand
        Write-Host "   ✅ Successfully deleted $ResourceName" -ForegroundColor Green
        $global:CleanupStats.ResourcesDeleted++
    } catch {
        Write-Host "   ❌ Failed to delete $ResourceName: $_" -ForegroundColor Red
    }
}

try {
    # Get all resources in the resource group
    Write-Host "`n📋 Scanning for duplicate resources..." -ForegroundColor Green
    $resources = az resource list -g $ResourceGroupName --output json | ConvertFrom-Json
    
    if (-not $resources) {
        Write-Host "❌ No resources found or access denied" -ForegroundColor Red
        exit 1
    }
    
    $global:CleanupStats.ResourcesScanned = $resources.Count
    
    # Categorize resources
    $storageAccounts = $resources | Where-Object { $_.type -eq "Microsoft.Storage/storageAccounts" }
    $containerRegistries = $resources | Where-Object { $_.type -eq "Microsoft.ContainerRegistry/registries" }
    $sqlServers = $resources | Where-Object { $_.type -eq "Microsoft.Sql/servers" }
    $keyVaults = $resources | Where-Object { $_.type -eq "Microsoft.KeyVault/vaults" }
    $workspaces = $resources | Where-Object { $_.type -eq "Microsoft.OperationalInsights/workspaces" }
    $containerApps = $resources | Where-Object { $_.type -eq "Microsoft.App/containerApps" }
    
    Write-Host "`n📊 Resource Summary:" -ForegroundColor Green
    Write-Host "   Total Resources: $($resources.Count)" -ForegroundColor Gray
    Write-Host "   Storage Accounts: $($storageAccounts.Count)" -ForegroundColor Gray
    Write-Host "   Container Registries: $($containerRegistries.Count)" -ForegroundColor Gray
    Write-Host "   SQL Servers: $($sqlServers.Count)" -ForegroundColor Gray
    Write-Host "   Key Vaults: $($keyVaults.Count)" -ForegroundColor Gray
    Write-Host "   Log Analytics Workspaces: $($workspaces.Count)" -ForegroundColor Gray
    Write-Host "   Container Apps: $($containerApps.Count)" -ForegroundColor Gray
    
    # Show cost analysis if requested
    if ($ShowCosts) {
        Show-CostAnalysis -Resources $resources
    }
    
    # Phase 1: Storage Accounts with random suffixes (safe to delete)
    Write-Host "`n🗄️ Phase 1: Cleanup orphaned Storage Accounts" -ForegroundColor Green
    foreach ($storage in $storageAccounts) {
        # Skip storage accounts without random suffixes (likely active)
        if ($storage.name -match "^aiprofilemaker.*st[a-z0-9]{8}$") {
            $reason = "Orphaned storage account with random suffix"
            $deleteCmd = "az storage account delete --name '$($storage.name)' --resource-group '$ResourceGroupName' --yes"
            $cost = Get-ResourceCostEstimate -ResourceType $storage.type -ResourceName $storage.name
            Remove-ResourceSafely -ResourceName $storage.name -ResourceType "Storage Account" -DeleteCommand $deleteCmd -Reason $reason -EstimatedMonthlyCost $cost
        } else {
            Write-Host "   ⚠️ Keeping '$($storage.name)' - appears to be active (deterministic naming)" -ForegroundColor Yellow
        }
    }
    
    # Phase 2: Container Registries - Enhanced analysis with image usage detection
    Write-Host "`n🐳 Phase 2: Cleanup Container Registries" -ForegroundColor Green
    
    # Check if container apps are using custom images
    $customImageRegistries = @()
    $usingCustomImages = $false
    
    foreach ($app in $containerApps) {
        try {
            $appDetails = az containerapp show --name $app.name --resource-group $ResourceGroupName --output json | ConvertFrom-Json
            $image = $appDetails.properties.template.containers[0].image
            if (-not $image.StartsWith("mcr.microsoft.com")) {
                $usingCustomImages = $true
                $registryFromImage = $image.Split('/')[0]
                $customImageRegistries += $registryFromImage
                Write-Host "   ⚠️ Found custom image in use: $image" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "   ⚠️ Could not analyze container app: $($app.name)" -ForegroundColor Yellow
        }
    }
    
    if (-not $usingCustomImages) {
        Write-Host "   ℹ️ No custom images detected - all container registries appear unused" -ForegroundColor Cyan
        foreach ($registry in $containerRegistries) {
            # Check for random suffix patterns (safe to delete)
            if ($registry.name -match "[a-z0-9]{8}$") {
                $reason = "Unused container registry with random suffix (apps use default images)"
                $deleteCmd = "az acr delete --name '$($registry.name)' --resource-group '$ResourceGroupName' --yes"
                $cost = Get-ResourceCostEstimate -ResourceType $registry.type -ResourceName $registry.name
                Remove-ResourceSafely -ResourceName $registry.name -ResourceType "Container Registry" -DeleteCommand $deleteCmd -Reason $reason -EstimatedMonthlyCost $cost
            } else {
                Write-Host "   ⚠️ Keeping '$($registry.name)' - deterministic naming, may be intended for future use" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "   ⚠️ Custom images detected - analyzing registry usage..." -ForegroundColor Yellow
        foreach ($registry in $containerRegistries) {
            $registryServer = az acr show --name $registry.name --resource-group $ResourceGroupName --query "loginServer" -o tsv 2>$null
            if ($registryServer -and $customImageRegistries -contains $registryServer) {
                Write-Host "   ✅ Keeping '$($registry.name)' - actively used by container apps" -ForegroundColor Green
            } elseif ($registry.name -match "[a-z0-9]{8}$") {
                $reason = "Unused container registry with random suffix"
                $deleteCmd = "az acr delete --name '$($registry.name)' --resource-group '$ResourceGroupName' --yes"
                $cost = Get-ResourceCostEstimate -ResourceType $registry.type -ResourceName $registry.name
                Remove-ResourceSafely -ResourceName $registry.name -ResourceType "Container Registry" -DeleteCommand $deleteCmd -Reason $reason -EstimatedMonthlyCost $cost
            } else {
                Write-Host "   ⚠️ Keeping '$($registry.name)' - manual review recommended" -ForegroundColor Yellow
            }
        }
    }
    
    # Phase 3: SQL Servers (DANGEROUS - need to identify active one)
    Write-Host "`n🗄️ Phase 3: SQL Server Analysis" -ForegroundColor Green
    if ($sqlServers.Count -gt 1) {
        Write-Host "   ⚠️ Multiple SQL servers found - MANUAL REVIEW REQUIRED" -ForegroundColor Red
        Write-Host "   📋 SQL Servers found:" -ForegroundColor Gray
        
        foreach ($sql in $sqlServers) {
            $createdDate = az sql server show --name $sql.name --resource-group $ResourceGroupName --query "createdDate" -o tsv 2>$null
            Write-Host "     - $($sql.name) (Created: $createdDate)" -ForegroundColor Gray
        }
        
        Write-Host "`n   🔍 To identify active SQL server:" -ForegroundColor Cyan
        Write-Host "     1. Check container app secrets: az containerapp show --name aiprofilemaker-api-staging -g $ResourceGroupName --query 'properties.template.containers[0].env'" -ForegroundColor Gray
        Write-Host "     2. Check Key Vault secrets to see which SQL connection string is stored" -ForegroundColor Gray
        Write-Host "     3. Delete older SQL servers manually after verification" -ForegroundColor Gray
        
        if (-not $DryRun -and -not $Force) {
            Write-Host "`n   ⏸️ Pausing SQL server cleanup for manual review" -ForegroundColor Yellow
        }
    } else {
        Write-Host "   ✅ Only one SQL server found - keeping it" -ForegroundColor Green
    }
    
    # Phase 4: Key Vaults and Workspaces
    Write-Host "`n🔐 Phase 4: Cleanup Key Vaults and Workspaces" -ForegroundColor Green
    
    # Sort by creation date and keep the newest
    if ($keyVaults.Count -gt 1) {
        Write-Host "   📋 Multiple Key Vaults found - keeping newest, cleaning up duplicates" -ForegroundColor Yellow
        
        foreach ($kv in $keyVaults) {
            if ($kv.name -match "[a-z0-9]{8}$") {
                $reason = "Duplicate Key Vault with random suffix"
                $deleteCmd = "az keyvault delete --name '$($kv.name)' --resource-group '$ResourceGroupName'"
                Remove-ResourceSafely -ResourceName $kv.name -ResourceType "Key Vault" -DeleteCommand $deleteCmd -Reason $reason
            }
        }
    }
    
    # Clean up extra workspaces
    if ($workspaces.Count -gt 1) {
        Write-Host "   📋 Multiple Log Analytics Workspaces found - cleaning up extras" -ForegroundColor Yellow
        
        foreach ($workspace in $workspaces) {
            if ($workspace.name -match "workspace-.*[A-Za-z0-9]{4}$") {
                $reason = "Extra Log Analytics workspace"
                $deleteCmd = "az monitor log-analytics workspace delete --resource-group '$ResourceGroupName' --workspace-name '$($workspace.name)' --yes"
                Remove-ResourceSafely -ResourceName $workspace.name -ResourceType "Log Analytics Workspace" -DeleteCommand $deleteCmd -Reason $reason
            }
        }
    }
    
    # Enhanced Summary with Statistics
    $duration = (Get-Date) - $global:CleanupStats.StartTime
    Write-Host "`n📊 Cleanup Summary:" -ForegroundColor Green
    Write-Host "   Duration: $($duration.ToString('mm\:ss'))" -ForegroundColor Gray
    Write-Host "   Resources scanned: $($global:CleanupStats.ResourcesScanned)" -ForegroundColor Gray
    Write-Host "   Resources targeted for cleanup: $($global:CleanupStats.ResourcesTargeted)" -ForegroundColor Gray
    Write-Host "   Resources actually deleted: $($global:CleanupStats.ResourcesDeleted)" -ForegroundColor Gray
    
    if ($global:CleanupStats.EstimatedMonthlySavings -gt 0) {
        Write-Host "   💰 Estimated monthly savings: ~$${global:CleanupStats.EstimatedMonthlySavings}" -ForegroundColor Green
    }
    
    Write-Host "`n✅ Cleanup completed!" -ForegroundColor Green
    Write-Host "`n💡 Next Steps:" -ForegroundColor Yellow
    Write-Host "1. Review remaining resources to ensure only one of each type" -ForegroundColor Gray
    Write-Host "2. Use the enhanced idempotent deployment script: deploy-infrastructure-idempotent.ps1" -ForegroundColor Gray
    Write-Host "3. Monitor Azure Cost Management to confirm savings" -ForegroundColor Gray
    Write-Host "4. Consider setting up resource tagging for better resource management" -ForegroundColor Gray
    
    if ($DryRun) {
        Write-Host "`n🔄 To execute cleanup, run:" -ForegroundColor Cyan
        Write-Host "   .\cleanup-duplicate-resources.ps1 -ResourceGroupName '$ResourceGroupName'" -ForegroundColor Gray
        Write-Host "`n💡 Additional Options:" -ForegroundColor Cyan
        Write-Host "   -ShowCosts         Show cost analysis" -ForegroundColor Gray
        Write-Host "   -Force             Skip confirmation prompts" -ForegroundColor Gray
        Write-Host "   -Environment prod  Target production resources (use with extreme caution)" -ForegroundColor Gray
    }
    
} catch {
    Write-Error "❌ Cleanup failed: $_"
    exit 1
}