#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploy AI Profile Photo Maker infrastructure to Azure using PowerShell
    
.DESCRIPTION  
    Standalone PowerShell script for deploying Azure infrastructure
    Bypasses Azure CLI issues by using direct PowerShell modules
    
.PARAMETER Environment
    Target environment: staging or production
    
.PARAMETER Force
    Skip confirmations and deploy immediately
    
.PARAMETER ValidateOnly
    Only validate template, don't deploy resources
    
.EXAMPLE
    ./Deploy-Infrastructure.ps1 -Environment staging
    ./Deploy-Infrastructure.ps1 -Environment production -Force
    ./Deploy-Infrastructure.ps1 -Environment staging -ValidateOnly
#>

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("staging", "production")]
    [string]$Environment,
    
    [switch]$Force,
    [switch]$ValidateOnly
)

# Configuration
$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"
$resourceGroupName = "ai-profile-photo-maker-$Environment"
$location = "East US"
$templateFile = "infrastructure/main.bicep"
$parameterFile = "infrastructure/parameters.$Environment.json"
$deploymentName = "local-deployment-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

# Colors for output  
$colors = @{
    Info = "Cyan"
    Success = "Green"
    Warning = "Yellow"
    Error = "Red"
}

function Write-Status {
    param($Message, $Type = "Info")
    Write-Host "[$Type] $Message" -ForegroundColor $colors[$Type]
}

function Test-Prerequisites {
    Write-Status "Checking prerequisites..." "Info"
    
    # Check PowerShell version
    if ($PSVersionTable.PSVersion.Major -lt 7) {
        Write-Status "PowerShell 7+ recommended. Current: $($PSVersionTable.PSVersion)" "Warning"
    }
    
    # Check Azure PowerShell modules
    $azModules = @("Az.Accounts", "Az.Resources", "Az.Profile")
    foreach ($module in $azModules) {
        if (-not (Get-Module -ListAvailable -Name $module)) {
            Write-Status "Installing missing module: $module" "Info"
            Install-Module -Name $module -Force -AllowClobber -Scope CurrentUser
        }
    }
    
    # Check files exist
    if (-not (Test-Path $templateFile)) {
        throw "Template file not found: $templateFile"
    }
    
    if (-not (Test-Path $parameterFile)) {
        throw "Parameter file not found: $parameterFile"
    }
    
    Write-Status "Prerequisites check passed" "Success"
}

function Connect-ToAzure {
    Write-Status "Connecting to Azure..." "Info"
    
    # Check if already connected
    $context = Get-AzContext -ErrorAction SilentlyContinue
    if ($context) {
        Write-Status "Already connected as: $($context.Account.Id)" "Info"
        
        if (-not $Force) {
            $continue = Read-Host "Continue with current account? (y/n)"
            if ($continue -ne "y") {
                Disconnect-AzAccount
                $context = $null
            }
        }
    }
    
    # Connect if needed
    if (-not $context) {
        try {
            Connect-AzAccount -UseDeviceAuthentication
            Write-Status "Azure connection successful" "Success"
        } catch {
            throw "Failed to connect to Azure: $($_.Exception.Message)"
        }
    }
    
    # Set subscription context
    $subscription = Get-AzSubscription | Where-Object { $_.Name -like "*profile*" -or $_.Name -like "*ai*" }
    if ($subscription) {
        Set-AzContext -SubscriptionId $subscription.Id
        Write-Status "Using subscription: $($subscription.Name)" "Info"
    }
}

function New-ResourceGroupIfNotExists {
    Write-Status "Ensuring resource group exists: $resourceGroupName" "Info"
    
    $rg = Get-AzResourceGroup -Name $resourceGroupName -ErrorAction SilentlyContinue
    if (-not $rg) {
        Write-Status "Creating resource group: $resourceGroupName" "Info"
        New-AzResourceGroup -Name $resourceGroupName -Location $location
        Write-Status "Resource group created successfully" "Success"
    } else {
        Write-Status "Resource group already exists" "Info"
    }
}

function Test-Template {
    Write-Status "Validating template..." "Info"
    
    try {
        $validation = Test-AzResourceGroupDeployment `
            -ResourceGroupName $resourceGroupName `
            -TemplateFile $templateFile `
            -TemplateParameterFile $parameterFile
            
        if ($validation) {
            Write-Status "Template validation warnings:" "Warning"
            $validation | ForEach-Object { 
                Write-Status "  - $($_.Message)" "Warning"
            }
        } else {
            Write-Status "Template validation passed" "Success"
        }
        
        return $true
    } catch {
        Write-Status "Template validation failed: $($_.Exception.Message)" "Error"
        return $false
    }
}

function Start-Deployment {
    if ($ValidateOnly) {
        Write-Status "Validation-only mode - no resources will be created" "Info"
        return
    }
    
    Write-Status "Starting deployment..." "Info"
    Write-Status "  Environment: $Environment" "Info"
    Write-Status "  Resource Group: $resourceGroupName" "Info"
    Write-Status "  Template: $templateFile" "Info"
    Write-Status "  Parameters: $parameterFile" "Info"
    Write-Status "  Deployment Name: $deploymentName" "Info"
    
    if (-not $Force) {
        $confirm = Read-Host "Proceed with deployment? (y/n)"
        if ($confirm -ne "y") {
            Write-Status "Deployment cancelled by user" "Warning"
            return
        }
    }
    
    try {
        $startTime = Get-Date
        
        $deployment = New-AzResourceGroupDeployment `
            -ResourceGroupName $resourceGroupName `
            -TemplateFile $templateFile `
            -TemplateParameterFile $parameterFile `
            -Name $deploymentName `
            -Mode Incremental `
            -Force `
            -Verbose
            
        $endTime = Get-Date
        $duration = $endTime - $startTime
        
        if ($deployment.ProvisioningState -eq "Succeeded") {
            Write-Status "Deployment completed successfully!" "Success"
            Write-Status "Duration: $($duration.TotalMinutes.ToString('F1')) minutes" "Info"
            
            # Display outputs
            if ($deployment.Outputs) {
                Write-Status "Deployment outputs:" "Info"
                $deployment.Outputs.Keys | ForEach-Object {
                    $value = $deployment.Outputs[$_].Value
                    Write-Status "  $($_): $value" "Info"
                }
            }
            
            return $deployment
        } else {
            throw "Deployment failed with status: $($deployment.ProvisioningState)"
        }
        
    } catch {
        Write-Status "Deployment failed: $($_.Exception.Message)" "Error"
        throw
    }
}

function Test-DeployedResources {
    param($deployment)
    
    Write-Status "Validating deployed resources..." "Info"
    
    $resources = Get-AzResource -ResourceGroupName $resourceGroupName
    $expectedTypes = @(
        "Microsoft.Web/sites",
        "Microsoft.Web/staticSites", 
        "Microsoft.Sql/servers",
        "Microsoft.Storage/storageAccounts",
        "Microsoft.KeyVault/vaults"
    )
    
    $foundResources = @()
    foreach ($expectedType in $expectedTypes) {
        $resource = $resources | Where-Object { $_.ResourceType -eq $expectedType }
        if ($resource) {
            $foundResources += $expectedType
            Write-Status "✓ Found: $($resource.Name) ($expectedType)" "Success"
        } else {
            Write-Status "✗ Missing: $expectedType" "Warning"
        }
    }
    
    $successRate = ($foundResources.Count / $expectedTypes.Count) * 100
    Write-Status "Resource validation: $($foundResources.Count)/$($expectedTypes.Count) ($($successRate.ToString('F0'))%)" "Info"
    
    if ($successRate -ge 80) {
        Write-Status "Resource deployment validation passed" "Success"
    } else {
        Write-Status "Resource deployment validation failed" "Warning"
    }
}

# Main execution
try {
    Write-Status "🚀 Starting Azure Infrastructure Deployment" "Info"
    Write-Status "Environment: $Environment" "Info"
    Write-Status "Timestamp: $(Get-Date)" "Info"
    
    # Execute deployment steps
    Test-Prerequisites
    Connect-ToAzure  
    New-ResourceGroupIfNotExists
    
    $templateValid = Test-Template
    if (-not $templateValid -and -not $Force) {
        throw "Template validation failed. Use -Force to override."
    }
    
    $deployment = Start-Deployment
    
    if ($deployment -and -not $ValidateOnly) {
        Test-DeployedResources -deployment $deployment
    }
    
    Write-Status "🎉 Deployment process completed successfully!" "Success"
    Write-Status "Azure Portal: https://portal.azure.com/#@/resource/subscriptions/$($(Get-AzContext).Subscription.Id)/resourcegroups/$resourceGroupName" "Info"
    
} catch {
    Write-Status "❌ Deployment failed: $($_.Exception.Message)" "Error"
    Write-Status "Check the error details above and retry after fixing issues." "Error"
    exit 1
}