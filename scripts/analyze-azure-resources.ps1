# Enhanced Azure Resource Analysis for AI Profile Photo Maker
# Comprehensive resource discovery, cost analysis, and duplicate detection

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName = "rg-aiprofilemaker-staging",
    
    [switch]$DetailedAnalysis = $false,
    [switch]$ExportResults = $false,
    [string]$OutputFormat = "table"
)

Write-Host "🔍 Enhanced Azure Resource Analysis" -ForegroundColor Green
Write-Host "📍 Resource Group: $ResourceGroupName" -ForegroundColor Yellow

# Initialize analysis results
$global:AnalysisResults = @{
    ResourceGroup = $ResourceGroupName
    ScanTime = Get-Date
    TotalResources = 0
    TargetResources = @{}
    DuplicateResources = @{}
    EstimatedMonthlyCosts = @{}
    SafetyAssessment = @{}
    CleanupRecommendations = @()
}

function Get-ResourceCostEstimate {
    param(
        [string]$ResourceType,
        [string]$ResourceName,
        [string]$SKU = "Basic",
        [string]$Location = "eastus2"
    )
    
    # Enhanced cost estimates based on actual Azure pricing (USD/month)
    $costTable = @{
        "Microsoft.Storage/storageAccounts" = @{
            "Standard_LRS" = 2.00
            "Standard_GRS" = 4.00
            "Premium_LRS" = 15.00
        }
        "Microsoft.ContainerRegistry/registries" = @{
            "Basic" = 5.00
            "Standard" = 20.00
            "Premium" = 500.00
        }
        "Microsoft.Sql/servers" = @{
            "Basic" = 4.99      # DTU-based Basic
            "Standard" = 15.00  # DTU-based Standard S0
            "Premium" = 465.00  # DTU-based Premium P1
        }
        "Microsoft.Sql/servers/databases" = @{
            "Basic" = 4.99
            "Standard" = 15.00
            "Premium" = 465.00
        }
        "Microsoft.KeyVault/vaults" = 0.03  # Per 10K transactions
        "Microsoft.App/containerApps" = @{
            "Consumption" = 0.000016  # Per vCPU second
            "Dedicated" = 73.00       # Per month for dedicated plan
        }
        "Microsoft.App/managedEnvironments" = 0.00
        "Microsoft.Insights/components" = 2.30  # Per GB ingested
        "Microsoft.OperationalInsights/workspaces" = 2.30
    }
    
    $typeTable = $costTable[$ResourceType]
    if ($typeTable -is [hashtable]) {
        return $typeTable[$SKU] ?? $typeTable["Basic"] ?? 0.00
    }
    return $typeTable ?? 0.00
}

function Analyze-ResourceNaming {
    param([object]$Resource)
    
    $name = $Resource.name
    $analysis = @{
        Name = $name
        Type = $Resource.type
        IsDeterministic = $false
        IsTarget = $false
        IsDuplicate = $false
        Pattern = "unknown"
        Confidence = 0
        Reason = ""
    }
    
    # Target pattern analysis (z3bawc74 suffix)
    if ($name -match "z3bawc74$") {
        $analysis.IsDeterministic = $true
        $analysis.IsTarget = $true
        $analysis.Pattern = "deterministic-z3bawc74"
        $analysis.Confidence = 95
        $analysis.Reason = "Matches deterministic naming pattern from uniqueString(resourceGroup().id)"
    }
    # Duplicate pattern analysis (random suffixes)
    elseif ($name -match "[a-z0-9]{8}$" -and $name -notmatch "z3bawc74$") {
        $analysis.IsDuplicate = $true
        $analysis.Pattern = "random-suffix-duplicate"
        $analysis.Confidence = 90
        $analysis.Reason = "Random 8-character suffix suggests duplicate from separate deployment"
    }
    # App names with deterministic environment suffix
    elseif ($name -match "(api|web)-staging$") {
        $analysis.IsDeterministic = $true
        $analysis.IsTarget = $true
        $analysis.Pattern = "app-environment"
        $analysis.Confidence = 100
        $analysis.Reason = "Container app with expected environment naming"
    }
    # Environment naming
    elseif ($name -match "env-staging$") {
        $analysis.IsDeterministic = $true
        $analysis.IsTarget = $true
        $analysis.Pattern = "environment-staging"
        $analysis.Confidence = 100
        $analysis.Reason = "Container environment with expected naming"
    }
    else {
        $analysis.Pattern = "unknown-pattern"
        $analysis.Confidence = 50
        $analysis.Reason = "Unknown naming pattern - manual review required"
    }
    
    return $analysis
}

function Get-ResourceDependencies {
    param([object]$Resource)
    
    $dependencies = @()
    
    try {
        switch ($Resource.type) {
            "Microsoft.ContainerRegistry/registries" {
                # Check if container apps are using this registry
                $apps = az containerapp list -g $ResourceGroupName --output json | ConvertFrom-Json
                foreach ($app in $apps) {
                    $appDetails = az containerapp show --name $app.name -g $ResourceGroupName --output json | ConvertFrom-Json
                    $image = $appDetails.properties.template.containers[0].image
                    $registryServer = az acr show --name $Resource.name -g $ResourceGroupName --query "loginServer" -o tsv 2>$null
                    if ($image -match [regex]::Escape($registryServer)) {
                        $dependencies += "Container App: $($app.name)"
                    }
                }
            }
            "Microsoft.Sql/servers" {
                # Check for databases
                $databases = az sql db list --server $Resource.name -g $ResourceGroupName --output json 2>$null | ConvertFrom-Json
                if ($databases) {
                    $dependencies += "Databases: $($databases.Count)"
                }
            }
            "Microsoft.Storage/storageAccounts" {
                # Check for blob containers
                $containers = az storage container list --account-name $Resource.name --output json 2>$null | ConvertFrom-Json
                if ($containers) {
                    $dependencies += "Blob Containers: $($containers.Count)"
                }
            }
        }
    } catch {
        Write-Warning "Could not analyze dependencies for $($Resource.name): $_"
    }
    
    return $dependencies
}

try {
    # Get all resources
    Write-Host "`n📋 Discovering resources..." -ForegroundColor Green
    $resources = az resource list -g $ResourceGroupName --output json | ConvertFrom-Json
    
    if (-not $resources) {
        Write-Host "❌ No resources found or access denied" -ForegroundColor Red
        exit 1
    }
    
    $global:AnalysisResults.TotalResources = $resources.Count
    
    Write-Host "   Found $($resources.Count) resources" -ForegroundColor Gray
    
    # Analyze each resource
    Write-Host "`n🔍 Analyzing resource patterns..." -ForegroundColor Green
    
    $targetResources = @()
    $duplicateResources = @()
    $unknownResources = @()
    $totalEstimatedCost = 0
    $potentialSavings = 0
    
    foreach ($resource in $resources) {
        $analysis = Analyze-ResourceNaming -Resource $resource
        $cost = Get-ResourceCostEstimate -ResourceType $resource.type -ResourceName $resource.name
        $dependencies = @()
        
        if ($DetailedAnalysis) {
            $dependencies = Get-ResourceDependencies -Resource $resource
        }
        
        $resourceInfo = @{
            Name = $resource.name
            Type = $resource.type
            Location = $resource.location
            Analysis = $analysis
            EstimatedMonthlyCost = $cost
            Dependencies = $dependencies
            CreatedTime = $resource.createdTime ?? "Unknown"
        }
        
        $totalEstimatedCost += $cost
        
        if ($analysis.IsTarget) {
            $targetResources += $resourceInfo
        } elseif ($analysis.IsDuplicate) {
            $duplicateResources += $resourceInfo
            $potentialSavings += $cost
        } else {
            $unknownResources += $resourceInfo
        }
    }
    
    # Store results
    $global:AnalysisResults.TargetResources = $targetResources
    $global:AnalysisResults.DuplicateResources = $duplicateResources
    $global:AnalysisResults.EstimatedMonthlyCosts = @{
        Total = $totalEstimatedCost
        Target = ($targetResources | Measure-Object EstimatedMonthlyCost -Sum).Sum
        Duplicates = $potentialSavings
        Unknown = ($unknownResources | Measure-Object EstimatedMonthlyCost -Sum).Sum
    }
    
    # Generate safety assessment
    $global:AnalysisResults.SafetyAssessment = @{
        SafeToDelete = $duplicateResources.Count
        RequiresReview = $unknownResources.Count
        HasDependencies = ($duplicateResources | Where-Object { $_.Dependencies.Count -gt 0 }).Count
        RiskLevel = if ($potentialSavings -gt 100) { "HIGH" } elseif ($potentialSavings -gt 50) { "MEDIUM" } else { "LOW" }
    }
    
    # Display results
    Write-Host "`n📊 Analysis Results Summary:" -ForegroundColor Green
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
    
    Write-Host "`n🎯 TARGET RESOURCES (Keep - Deterministic Naming):" -ForegroundColor Green
    foreach ($resource in $targetResources) {
        $costDisplay = if ($resource.EstimatedMonthlyCost -gt 0) { " (~$$$($resource.EstimatedMonthlyCost)/mo)" } else { "" }
        Write-Host "   ✅ $($resource.Name)$costDisplay" -ForegroundColor Green
        Write-Host "      Type: $($resource.Type)" -ForegroundColor Gray
        Write-Host "      Reason: $($resource.Analysis.Reason)" -ForegroundColor Gray
        
        if ($DetailedAnalysis -and $resource.Dependencies.Count -gt 0) {
            Write-Host "      Dependencies: $($resource.Dependencies -join ', ')" -ForegroundColor Gray
        }
    }
    
    Write-Host "`n🗑️ DUPLICATE RESOURCES (Safe to Delete):" -ForegroundColor Red
    foreach ($resource in $duplicateResources) {
        $costDisplay = if ($resource.EstimatedMonthlyCost -gt 0) { " (~$$$($resource.EstimatedMonthlyCost)/mo)" } else { "" }
        Write-Host "   ❌ $($resource.Name)$costDisplay" -ForegroundColor Red
        Write-Host "      Type: $($resource.Type)" -ForegroundColor Gray
        Write-Host "      Reason: $($resource.Analysis.Reason)" -ForegroundColor Gray
        Write-Host "      Confidence: $($resource.Analysis.Confidence)%" -ForegroundColor Gray
        
        if ($DetailedAnalysis -and $resource.Dependencies.Count -gt 0) {
            Write-Host "      ⚠️ Dependencies: $($resource.Dependencies -join ', ')" -ForegroundColor Yellow
        }
    }
    
    if ($unknownResources.Count -gt 0) {
        Write-Host "`n❓ UNKNOWN RESOURCES (Manual Review Required):" -ForegroundColor Yellow
        foreach ($resource in $unknownResources) {
            $costDisplay = if ($resource.EstimatedMonthlyCost -gt 0) { " (~$$$($resource.EstimatedMonthlyCost)/mo)" } else { "" }
            Write-Host "   ❓ $($resource.Name)$costDisplay" -ForegroundColor Yellow
            Write-Host "      Type: $($resource.Type)" -ForegroundColor Gray
            Write-Host "      Reason: $($resource.Analysis.Reason)" -ForegroundColor Gray
        }
    }
    
    Write-Host "`n💰 COST ANALYSIS:" -ForegroundColor Green
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
    Write-Host "   Total Current Cost: ~$$$($global:AnalysisResults.EstimatedMonthlyCosts.Total)/month" -ForegroundColor Gray
    Write-Host "   Target Resources: ~$$$($global:AnalysisResults.EstimatedMonthlyCosts.Target)/month" -ForegroundColor Green
    Write-Host "   Duplicate Resources: ~$$$($global:AnalysisResults.EstimatedMonthlyCosts.Duplicates)/month" -ForegroundColor Red
    Write-Host "   💰 Potential Monthly Savings: ~$$$($potentialSavings)" -ForegroundColor Green
    Write-Host "   📈 Annual Savings Potential: ~$$$([math]::Round($potentialSavings * 12, 2))" -ForegroundColor Green
    
    Write-Host "`n🛡️ SAFETY ASSESSMENT:" -ForegroundColor Green
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
    Write-Host "   Risk Level: $($global:AnalysisResults.SafetyAssessment.RiskLevel)" -ForegroundColor $(if ($global:AnalysisResults.SafetyAssessment.RiskLevel -eq "HIGH") { "Red" } elseif ($global:AnalysisResults.SafetyAssessment.RiskLevel -eq "MEDIUM") { "Yellow" } else { "Green" })
    Write-Host "   Safe to Delete: $($global:AnalysisResults.SafetyAssessment.SafeToDelete) resources" -ForegroundColor Green
    Write-Host "   Requires Review: $($global:AnalysisResults.SafetyAssessment.RequiresReview) resources" -ForegroundColor Yellow
    Write-Host "   Has Dependencies: $($global:AnalysisResults.SafetyAssessment.HasDependencies) resources" -ForegroundColor Yellow
    
    # Generate cleanup recommendations
    $global:AnalysisResults.CleanupRecommendations = @(
        "1. 🧹 Run cleanup script with dry-run: .\cleanup-duplicate-resources.ps1 -ResourceGroupName '$ResourceGroupName' -DryRun"
        "2. 🔍 Review dependency warnings for any critical resources"
        "3. 💰 Potential monthly savings: ~$$$potentialSavings"
        "4. 🚀 Execute cleanup: .\cleanup-duplicate-resources.ps1 -ResourceGroupName '$ResourceGroupName'"
        "5. 🏗️ Migrate to Terraform infrastructure for future deployments"
    )
    
    Write-Host "`n💡 CLEANUP RECOMMENDATIONS:" -ForegroundColor Green
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
    foreach ($recommendation in $global:AnalysisResults.CleanupRecommendations) {
        Write-Host "   $recommendation" -ForegroundColor Gray
    }
    
    # Export results if requested
    if ($ExportResults) {
        $exportFile = "azure-resource-analysis-$(Get-Date -Format 'yyyy-MM-dd-HHmm').json"
        $global:AnalysisResults | ConvertTo-Json -Depth 10 | Out-File $exportFile
        Write-Host "`n📁 Results exported to: $exportFile" -ForegroundColor Cyan
    }
    
    Write-Host "`n✅ Analysis completed successfully!" -ForegroundColor Green
    
} catch {
    Write-Error "❌ Analysis failed: $_"
    exit 1
}