#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploy AI Profile Photo Maker infrastructure to Azure using PowerShell

.DESCRIPTION
    This script deploys the complete Azure infrastructure for the AI Profile Photo Maker application.
    It supports both staging and production environments with comprehensive validation and error handling.
    
    Features:
    - Environment-specific parameter processing
    - Pre-deployment validation
    - Resource group management
    - Deployment monitoring with progress updates
    - Post-deployment validation
    - Rollback capabilities
    - Detailed logging and error reporting

.PARAMETER Environment
    Target environment: 'staging' or 'production'

.PARAMETER ResourceGroupName
    Override default resource group name

.PARAMETER Location
    Azure region for deployment (default: East US)

.PARAMETER ValidateOnly
    Only validate the template without deploying

.PARAMETER Force
    Skip confirmations and force deployment

.PARAMETER WhatIf
    Show what would be deployed without actually deploying

.PARAMETER Rollback
    Rollback to previous successful deployment

.PARAMETER SubscriptionId
    Azure subscription ID (if not using current context)

.EXAMPLE
    .\Deploy-Infrastructure.ps1 -Environment staging
    
.EXAMPLE
    .\Deploy-Infrastructure.ps1 -Environment production -ValidateOnly
    
.EXAMPLE
    .\Deploy-Infrastructure.ps1 -Environment staging -WhatIf

.EXAMPLE
    .\Deploy-Infrastructure.ps1 -Environment production -Force

.NOTES
    Author: AI Profile Photo Maker Team
    Version: 1.0.0
    Requires: Azure PowerShell module (Az)
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('staging', 'production')]
    [string]$Environment,
    
    [string]$ResourceGroupName,
    
    [string]$Location = 'East US',
    
    [switch]$ValidateOnly,
    
    [switch]$Force,
    
    [switch]$Rollback,
    
    [string]$SubscriptionId,
    
    [string]$TenantId,
    
    [int]$TimeoutMinutes = 30
)

# Script configuration
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'Continue'
$VerbosePreference = if ($VerbosePreference -eq 'SilentlyContinue') { 'Continue' } else { $VerbosePreference }

# Script variables
$ScriptPath = $PSScriptRoot
$TemplateFile = Join-Path $ScriptPath 'main.json'
$BicepFile = Join-Path $ScriptPath 'main.bicep'
$ParametersFile = Join-Path $ScriptPath "parameters.$Environment.json"
$DeploymentName = "infra-deploy-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

# Default resource group name if not provided
if (-not $ResourceGroupName) {
    $ResourceGroupName = "ai-profile-photo-maker-$Environment"
}

# Color output functions
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = 'White'
    )
    
    $colors = @{
        'Red' = 'Red'
        'Green' = 'Green' 
        'Yellow' = 'Yellow'
        'Blue' = 'Blue'
        'Magenta' = 'Magenta'
        'Cyan' = 'Cyan'
        'White' = 'White'
    }
    
    Write-Host $Message -ForegroundColor $colors[$Color]
}

function Write-Success { param([string]$Message) Write-ColorOutput "✅ $Message" -Color 'Green' }
function Write-Warning { param([string]$Message) Write-ColorOutput "⚠️  $Message" -Color 'Yellow' }
function Write-Error { param([string]$Message) Write-ColorOutput "❌ $Message" -Color 'Red' }
function Write-Info { param([string]$Message) Write-ColorOutput "ℹ️  $Message" -Color 'Blue' }
function Write-Progress { param([string]$Message) Write-ColorOutput "🔄 $Message" -Color 'Cyan' }

# Header
function Show-Header {
    Write-Host ""
    Write-ColorOutput "🚀 AI Profile Photo Maker - Infrastructure Deployment" -Color 'Magenta'
    Write-ColorOutput "=" * 60 -Color 'Magenta'
    Write-Info "Environment: $Environment"
    Write-Info "Resource Group: $ResourceGroupName"
    Write-Info "Location: $Location"
    Write-Info "Deployment Name: $DeploymentName"
    if ($ValidateOnly) { Write-Info "Mode: Validation Only" }
    if ($WhatIf) { Write-Info "Mode: What-If Analysis" }
    if ($Rollback) { Write-Info "Mode: Rollback" }
    Write-Host ""
}

# Check prerequisites
function Test-Prerequisites {
    Write-Progress "Checking prerequisites..."
    
    # Check if Azure PowerShell is installed
    try {
        $azVersion = Get-Module -Name Az -ListAvailable | Select-Object -First 1
        if (-not $azVersion) {
            throw "Azure PowerShell module (Az) is not installed"
        }
        Write-Success "Azure PowerShell module found: $($azVersion.Version)"
    }
    catch {
        Write-Error "Azure PowerShell module not found. Please install: Install-Module -Name Az"
        throw
    }
    
    # Check if logged in to Azure
    try {
        $context = Get-AzContext
        if (-not $context) {
            throw "Not logged in to Azure"
        }
        Write-Success "Azure context: $($context.Account.Id) - $($context.Subscription.Name)"
        
        # Switch subscription if specified
        if ($SubscriptionId -and $context.Subscription.Id -ne $SubscriptionId) {
            Set-AzContext -SubscriptionId $SubscriptionId | Out-Null
            Write-Success "Switched to subscription: $SubscriptionId"
        }
    }
    catch {
        Write-Error "Not logged in to Azure. Please run: Connect-AzAccount"
        throw
    }
    
    # Check required files
    $requiredFiles = @($TemplateFile, $ParametersFile)
    foreach ($file in $requiredFiles) {
        if (-not (Test-Path $file)) {
            Write-Error "Required file not found: $file"
            throw "Missing required file: $file"
        }
    }
    Write-Success "All required files found"
    
    # Check Bicep CLI if Bicep file exists
    if (Test-Path $BicepFile) {
        try {
            $bicepVersion = bicep --version 2>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Bicep CLI found: $bicepVersion"
            } else {
                Write-Warning "Bicep CLI not found - using pre-compiled ARM template"
            }
        }
        catch {
            Write-Warning "Bicep CLI not available - using pre-compiled ARM template"
        }
    }
}

# Process parameters with secrets
function Initialize-Parameters {
    Write-Progress "Processing deployment parameters..."
    
    try {
        # Read the parameters file
        $parametersContent = Get-Content -Path $ParametersFile -Raw
        
        # Check for required secrets in environment variables
        $requiredSecrets = @()
        
        if ($Environment -eq 'staging') {
            $requiredSecrets += @('STAGING_SQL_ADMIN_PASSWORD', 'STAGING_JWT_SECRET')
        } else {
            $requiredSecrets += @('PROD_SQL_ADMIN_PASSWORD', 'PROD_JWT_SECRET')
        }
        $requiredSecrets += @('REPLICATE_API_TOKEN', 'REPLICATE_WEBHOOK_SECRET')
        
        $missingSecrets = @()
        foreach ($secret in $requiredSecrets) {
            if (-not (Get-Item -Path "env:$secret" -ErrorAction SilentlyContinue)) {
                $missingSecrets += $secret
            }
        }
        
        if ($missingSecrets.Count -gt 0) {
            Write-Warning "Missing environment variables for secrets:"
            $missingSecrets | ForEach-Object { Write-Host "  • $_" }
            
            if (-not $Force) {
                $continue = Read-Host "Continue without secrets? This will cause deployment to fail (y/N)"
                if ($continue -ne 'y' -and $continue -ne 'Y') {
                    throw "Deployment cancelled - missing secrets"
                }
            }
        }
        
        # Replace secrets in parameters
        if ($Environment -eq 'staging') {
            $parametersContent = $parametersContent -replace 'REPLACE_WITH_STAGING_SQL_PASSWORD', $env:STAGING_SQL_ADMIN_PASSWORD
            $parametersContent = $parametersContent -replace 'REPLACE_WITH_STAGING_JWT_SECRET', $env:STAGING_JWT_SECRET
        } else {
            $parametersContent = $parametersContent -replace 'REPLACE_WITH_PROD_SQL_PASSWORD', $env:PROD_SQL_ADMIN_PASSWORD
            $parametersContent = $parametersContent -replace 'REPLACE_WITH_PROD_JWT_SECRET', $env:PROD_JWT_SECRET
        }
        
        $parametersContent = $parametersContent -replace 'REPLACE_WITH_REPLICATE_TOKEN', $env:REPLICATE_API_TOKEN
        $parametersContent = $parametersContent -replace 'REPLACE_WITH_WEBHOOK_SECRET', $env:REPLICATE_WEBHOOK_SECRET
        
        # Create temporary parameters file
        $script:TempParametersFile = Join-Path $env:TEMP "parameters.$Environment.$(Get-Date -Format 'yyyyMMddHHmmss').json"
        $parametersContent | Out-File -FilePath $script:TempParametersFile -Encoding UTF8
        
        # Validate JSON structure
        $parametersObject = $parametersContent | ConvertFrom-Json
        Write-Success "Parameters processed and validated"
        
        return $script:TempParametersFile
        
    }
    catch {
        Write-Error "Failed to process parameters: $($_.Exception.Message)"
        throw
    }
}

# Ensure resource group exists
function Initialize-ResourceGroup {
    Write-Progress "Checking resource group: $ResourceGroupName"
    
    try {
        $rg = Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue
        
        if (-not $rg) {
            if ($PSCmdlet.ShouldProcess($ResourceGroupName, "Create Resource Group")) {
                Write-Progress "Creating resource group: $ResourceGroupName"
                $rg = New-AzResourceGroup -Name $ResourceGroupName -Location $Location -Tag @{
                    Environment = $Environment
                    Application = "AI-ProfilePhotoMaker"
                    CreatedBy = "PowerShell-Script"
                    CreatedDate = (Get-Date).ToString("yyyy-MM-dd")
                }
                Write-Success "Resource group created: $ResourceGroupName"
            }
        } else {
            Write-Success "Resource group exists: $ResourceGroupName"
        }
        
        return $rg
    }
    catch {
        Write-Error "Failed to initialize resource group: $($_.Exception.Message)"
        throw
    }
}

# Validate ARM template
function Test-DeploymentTemplate {
    param([string]$ParametersFilePath)
    
    Write-Progress "Validating ARM template..."
    
    try {
        # Test the deployment
        $validationResult = Test-AzResourceGroupDeployment `
            -ResourceGroupName $ResourceGroupName `
            -TemplateFile $TemplateFile `
            -TemplateParameterFile $ParametersFilePath `
            -ErrorAction Stop
        
        if ($validationResult) {
            Write-Error "ARM template validation failed:"
            foreach ($error in $validationResult) {
                Write-Host "  • $($error.Message)" -ForegroundColor Red
                if ($error.Details) {
                    foreach ($detail in $error.Details) {
                        Write-Host "    - $($detail.Message)" -ForegroundColor Red
                    }
                }
            }
            throw "Template validation failed"
        } else {
            Write-Success "ARM template validation passed"
        }
    }
    catch {
        Write-Error "Template validation error: $($_.Exception.Message)"
        throw
    }
}

# Execute deployment
function Start-InfrastructureDeployment {
    param([string]$ParametersFilePath)
    
    if ($ValidateOnly) {
        Write-Success "Validation completed successfully - no deployment performed"
        return $null
    }
    
    Write-Progress "Starting infrastructure deployment..."
    Write-Info "This may take 10-20 minutes depending on the resources being created"
    
    try {
        if ($PSCmdlet.ShouldProcess($ResourceGroupName, "Deploy Infrastructure")) {
            # Start deployment with progress monitoring
            $deploymentJob = New-AzResourceGroupDeployment `
                -ResourceGroupName $ResourceGroupName `
                -Name $DeploymentName `
                -TemplateFile $TemplateFile `
                -TemplateParameterFile $ParametersFilePath `
                -Mode Incremental `
                -Force `
                -AsJob
            
            # Monitor deployment progress
            $startTime = Get-Date
            $timeout = $startTime.AddMinutes($TimeoutMinutes)
            
            Write-Progress "Deployment started at $($startTime.ToString('HH:mm:ss'))"
            Write-Info "Deployment Name: $DeploymentName"
            Write-Info "Timeout: $TimeoutMinutes minutes"
            
            do {
                Start-Sleep -Seconds 30
                $elapsed = (Get-Date) - $startTime
                $remaining = $timeout - (Get-Date)
                
                Write-Host "." -NoNewline
                
                if ($elapsed.TotalMinutes -gt 0 -and ($elapsed.TotalMinutes % 2) -eq 0) {
                    Write-Host ""
                    Write-Progress "Deployment in progress... (Elapsed: $($elapsed.ToString('mm\:ss')))"
                }
                
                if ((Get-Date) -gt $timeout) {
                    Write-Error "Deployment timeout reached ($TimeoutMinutes minutes)"
                    throw "Deployment timeout"
                }
                
            } while ($deploymentJob.State -eq 'Running')
            
            Write-Host ""
            
            # Get deployment result
            $deployment = Receive-Job -Job $deploymentJob
            Remove-Job -Job $deploymentJob
            
            if ($deployment.ProvisioningState -eq "Succeeded") {
                $duration = (Get-Date) - $startTime
                Write-Success "Infrastructure deployment completed successfully in $($duration.ToString('mm\:ss'))"
                return $deployment
            } else {
                Write-Error "Infrastructure deployment failed with state: $($deployment.ProvisioningState)"
                
                # Get detailed error information
                $deploymentDetails = Get-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName -Name $DeploymentName
                if ($deploymentDetails.ErrorDetails) {
                    Write-Host "Deployment errors:" -ForegroundColor Red
                    foreach ($error in $deploymentDetails.ErrorDetails) {
                        Write-Host "  • $($error.Message)" -ForegroundColor Red
                    }
                }
                
                throw "Deployment failed"
            }
        }
    }
    catch {
        Write-Error "Deployment failed: $($_.Exception.Message)"
        
        # Try to get additional error details
        try {
            $deploymentError = Get-AzResourceGroupDeploymentOperation -ResourceGroupName $ResourceGroupName -DeploymentName $DeploymentName |
                              Where-Object { $_.ProvisioningState -eq "Failed" } |
                              Select-Object -First 5
            
            if ($deploymentError) {
                Write-Host "Deployment operation errors:" -ForegroundColor Red
                foreach ($error in $deploymentError) {
                    Write-Host "  • Resource: $($error.TargetResource)" -ForegroundColor Red
                    Write-Host "    Error: $($error.StatusMessage)" -ForegroundColor Red
                }
            }
        }
        catch {
            Write-Warning "Could not retrieve detailed error information"
        }
        
        throw
    }
}

# Validate deployed resources
function Test-DeployedResources {
    param($Deployment)
    
    if ($ValidateOnly -or -not $Deployment) {
        return
    }
    
    Write-Progress "Validating deployed resources..."
    
    $validationResults = @{}
    
    try {
        # Extract outputs from deployment
        $outputs = $Deployment.Outputs
        
        if ($outputs.webAppName) {
            $webAppName = $outputs.webAppName.value
            Write-Info "Checking Web App: $webAppName"
            
            $webApp = Get-AzWebApp -ResourceGroupName $ResourceGroupName -Name $webAppName -ErrorAction SilentlyContinue
            if ($webApp -and $webApp.State -eq "Running") {
                Write-Success "Web App is running: $webAppName"
                $validationResults['WebApp'] = @{ Status = 'Success'; Name = $webAppName; Url = $outputs.webAppUrl.value }
            } else {
                Write-Warning "Web App validation failed: $webAppName"
                $validationResults['WebApp'] = @{ Status = 'Failed'; Name = $webAppName }
            }
        }
        
        if ($outputs.sqlServerName) {
            $sqlServerName = $outputs.sqlServerName.value
            Write-Info "Checking SQL Server: $sqlServerName"
            
            $sqlServer = Get-AzSqlServer -ResourceGroupName $ResourceGroupName -ServerName $sqlServerName -ErrorAction SilentlyContinue
            if ($sqlServer) {
                Write-Success "SQL Server deployed: $sqlServerName"
                $validationResults['SqlServer'] = @{ Status = 'Success'; Name = $sqlServerName }
            } else {
                Write-Warning "SQL Server validation failed: $sqlServerName"
                $validationResults['SqlServer'] = @{ Status = 'Failed'; Name = $sqlServerName }
            }
        }
        
        if ($outputs.storageAccountName) {
            $storageAccountName = $outputs.storageAccountName.value
            Write-Info "Checking Storage Account: $storageAccountName"
            
            $storageAccount = Get-AzStorageAccount -ResourceGroupName $ResourceGroupName -Name $storageAccountName -ErrorAction SilentlyContinue
            if ($storageAccount) {
                Write-Success "Storage Account deployed: $storageAccountName"
                $validationResults['StorageAccount'] = @{ Status = 'Success'; Name = $storageAccountName }
            } else {
                Write-Warning "Storage Account validation failed: $storageAccountName"
                $validationResults['StorageAccount'] = @{ Status = 'Failed'; Name = $storageAccountName }
            }
        }
        
        if ($outputs.keyVaultName) {
            $keyVaultName = $outputs.keyVaultName.value
            Write-Info "Checking Key Vault: $keyVaultName"
            
            $keyVault = Get-AzKeyVault -ResourceGroupName $ResourceGroupName -VaultName $keyVaultName -ErrorAction SilentlyContinue
            if ($keyVault) {
                Write-Success "Key Vault deployed: $keyVaultName"
                $validationResults['KeyVault'] = @{ Status = 'Success'; Name = $keyVaultName }
            } else {
                Write-Warning "Key Vault validation failed: $keyVaultName"
                $validationResults['KeyVault'] = @{ Status = 'Failed'; Name = $keyVaultName }
            }
        }
        
        # Summary
        $successCount = ($validationResults.Values | Where-Object { $_.Status -eq 'Success' }).Count
        $totalCount = $validationResults.Count
        
        if ($successCount -eq $totalCount) {
            Write-Success "All $totalCount resources validated successfully"
        } else {
            Write-Warning "$successCount of $totalCount resources validated successfully"
        }
        
        return $validationResults
    }
    catch {
        Write-Warning "Resource validation failed: $($_.Exception.Message)"
        return @{}
    }
}

# Show deployment summary
function Show-DeploymentSummary {
    param($Deployment, $ValidationResults)
    
    Write-Host ""
    Write-ColorOutput "📊 Deployment Summary" -Color 'Magenta'
    Write-ColorOutput "=" * 40 -Color 'Magenta'
    
    Write-Info "Environment: $Environment"
    Write-Info "Resource Group: $ResourceGroupName"
    Write-Info "Location: $Location"
    Write-Info "Deployment Name: $DeploymentName"
    
    if ($ValidateOnly) {
        Write-Success "Template validation completed successfully"
        return
    }
    
    if ($Deployment) {
        Write-Info "Deployment Status: $($Deployment.ProvisioningState)"
        Write-Info "Deployment Duration: $($Deployment.Duration)"
        
        if ($Deployment.Outputs) {
            Write-Host ""
            Write-ColorOutput "🎯 Deployed Resources:" -Color 'Blue'
            
            foreach ($output in $Deployment.Outputs.GetEnumerator()) {
                $name = $output.Key
                $value = $output.Value.value
                
                $status = "✅"
                if ($ValidationResults[$name] -and $ValidationResults[$name].Status -eq 'Failed') {
                    $status = "❌"
                }
                
                Write-Host "  $status $name`: $value"
            }
        }
    }
    
    Write-Host ""
    Write-ColorOutput "🔗 Quick Links:" -Color 'Blue'
    Write-Host "  • Azure Portal: https://portal.azure.com/#@/resource/subscriptions/$($(Get-AzContext).Subscription.Id)/resourceGroups/$ResourceGroupName"
    
    if ($Deployment -and $Deployment.Outputs.webAppUrl) {
        Write-Host "  • Web App: $($Deployment.Outputs.webAppUrl.value)"
    }
    
    if ($Deployment -and $Deployment.Outputs.staticWebAppUrl) {
        Write-Host "  • Static Web App: $($Deployment.Outputs.staticWebAppUrl.value)"
    }
    
    Write-Host ""
    Write-ColorOutput "⚠️  Next Steps:" -Color 'Yellow'
    Write-Host "  1. Deploy application code to the Web App"
    Write-Host "  2. Deploy frontend to the Static Web App"
    Write-Host "  3. Run database migrations"
    Write-Host "  4. Configure monitoring and alerts"
    Write-Host ""
}

# Rollback function
function Start-Rollback {
    Write-Progress "Initiating rollback process..."
    
    try {
        # Get previous successful deployments
        $previousDeployments = Get-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName |
                              Where-Object { $_.ProvisioningState -eq "Succeeded" -and $_.DeploymentName -like "infra-deploy-*" } |
                              Sort-Object Timestamp -Descending |
                              Select-Object -First 5
        
        if (-not $previousDeployments) {
            Write-Error "No previous successful deployments found for rollback"
            return
        }
        
        Write-Host ""
        Write-ColorOutput "📋 Available Rollback Targets:" -Color 'Blue'
        for ($i = 0; $i -lt $previousDeployments.Count; $i++) {
            $deployment = $previousDeployments[$i]
            Write-Host "  $($i + 1). $($deployment.DeploymentName) - $($deployment.Timestamp.ToString('yyyy-MM-dd HH:mm:ss'))"
        }
        Write-Host ""
        
        if (-not $Force) {
            $selection = Read-Host "Select deployment to rollback to (1-$($previousDeployments.Count), or 'c' to cancel)"
            if ($selection -eq 'c' -or $selection -eq 'C') {
                Write-Info "Rollback cancelled"
                return
            }
            
            try {
                $index = [int]$selection - 1
                if ($index -lt 0 -or $index -ge $previousDeployments.Count) {
                    throw "Invalid selection"
                }
            }
            catch {
                Write-Error "Invalid selection: $selection"
                return
            }
        } else {
            $index = 0  # Use most recent successful deployment
        }
        
        $targetDeployment = $previousDeployments[$index]
        Write-Info "Rolling back to: $($targetDeployment.DeploymentName)"
        
        # Note: Actual rollback would require storing the previous ARM template
        # For now, we'll show the information needed for manual rollback
        Write-Warning "Automatic rollback requires the original ARM template and parameters"
        Write-Info "Target Deployment: $($targetDeployment.DeploymentName)"
        Write-Info "Deployment Date: $($targetDeployment.Timestamp)"
        Write-Info "Template Used: $($targetDeployment.TemplateLink.Uri)"
        
        Write-Host ""
        Write-ColorOutput "Manual Rollback Steps:" -Color 'Yellow'
        Write-Host "1. Locate the ARM template used for deployment: $($targetDeployment.DeploymentName)"
        Write-Host "2. Re-run deployment with the previous template and parameters"
        Write-Host "3. Or restore from Azure backup if available"
        
    }
    catch {
        Write-Error "Rollback failed: $($_.Exception.Message)"
        throw
    }
}

# Cleanup function
function Invoke-Cleanup {
    if ($script:TempParametersFile -and (Test-Path $script:TempParametersFile)) {
        Remove-Item -Path $script:TempParametersFile -Force -ErrorAction SilentlyContinue
        Write-Info "Temporary files cleaned up"
    }
}

# Main execution
function Invoke-Main {
    try {
        Show-Header
        
        if ($Rollback) {
            Start-Rollback
            return
        }
        
        Test-Prerequisites
        $parametersFilePath = Initialize-Parameters
        Initialize-ResourceGroup
        Test-DeploymentTemplate -ParametersFilePath $parametersFilePath
        
        $deployment = Start-InfrastructureDeployment -ParametersFilePath $parametersFilePath
        $validationResults = Test-DeployedResources -Deployment $deployment
        
        Show-DeploymentSummary -Deployment $deployment -ValidationResults $validationResults
        
        if ($deployment -and $deployment.ProvisioningState -eq "Succeeded") {
            Write-Success "🎉 Infrastructure deployment completed successfully!"
        }
        
    }
    catch {
        Write-Error "❌ Deployment failed: $($_.Exception.Message)"
        Write-Host ""
        Write-ColorOutput "🔧 Troubleshooting Tips:" -Color 'Yellow'
        Write-Host "1. Check Azure service health: https://status.azure.com/"
        Write-Host "2. Verify your Azure permissions and subscription limits"
        Write-Host "3. Review the error messages above for specific issues"
        Write-Host "4. Try running with -ValidateOnly first to check template"
        Write-Host "5. Use -Verbose for more detailed output"
        Write-Host ""
        
        exit 1
    }
    finally {
        Invoke-Cleanup
    }
}

# Execute main function
Invoke-Main