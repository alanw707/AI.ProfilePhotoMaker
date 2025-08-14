#!/usr/bin/env pwsh
# Secret Management Validation Script
# Validates consistency between GitHub secrets and Azure Key Vault

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroup = "aiprofilemaker-v1",
    
    [Parameter(Mandatory=$false)]
    [string]$Environment = "v1",
    
    [Parameter(Mandatory=$false)]
    [switch]$UpdateKeyVault,
    
    [Parameter(Mandatory=$false)]
    [switch]$DryRun
)

Write-Host "🔐 Secret Management Validation" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Expected secrets configuration
$ExpectedSecrets = @{
    "REPLICATE_WEBHOOK_SECRET" = "whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM"
    "GOOGLE_CLIENT_ID" = "116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com"
}

$ValidationResults = @{
    "GitHubSecrets" = @{}
    "KeyVaultSecrets" = @{}
    "Mismatches" = @()
    "Success" = $true
}

Write-Host "📋 [INFO] Validating secrets for environment: $Environment" -ForegroundColor Blue
Write-Host "📋 [INFO] Resource Group: $ResourceGroup" -ForegroundColor Blue
Write-Host ""

# Function to get Key Vault name
function Get-KeyVaultName {
    param($ResourceGroup, $Environment)
    
    try {
        $keyVaults = az keyvault list --resource-group $ResourceGroup --query "[?contains(name, 'kv-$Environment')].name" --output tsv
        if ($keyVaults) {
            return $keyVaults.Split([Environment]::NewLine)[0].Trim()
        }
        return $null
    }
    catch {
        Write-Host "⚠️ [WARNING] Could not retrieve Key Vault name: $($_.Exception.Message)" -ForegroundColor Yellow
        return $null
    }
}

# Validate GitHub Secrets
Write-Host "🔍 [VALIDATE] Checking GitHub secrets..." -ForegroundColor Yellow

try {
    $githubSecrets = gh secret list --json name,updatedAt | ConvertFrom-Json
    
    foreach ($secret in $ExpectedSecrets.Keys) {
        $githubSecret = $githubSecrets | Where-Object { $_.name -eq $secret }
        if ($githubSecret) {
            $ValidationResults.GitHubSecrets[$secret] = @{
                "Exists" = $true
                "UpdatedAt" = $githubSecret.updatedAt
            }
            Write-Host "  ✅ GitHub secret '$secret' exists (updated: $($githubSecret.updatedAt))" -ForegroundColor Green
        } else {
            $ValidationResults.GitHubSecrets[$secret] = @{
                "Exists" = $false
                "UpdatedAt" = $null
            }
            $ValidationResults.Mismatches += "GitHub secret '$secret' is missing"
            $ValidationResults.Success = $false
            Write-Host "  ❌ GitHub secret '$secret' is missing" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "❌ [ERROR] Failed to check GitHub secrets: $($_.Exception.Message)" -ForegroundColor Red
    $ValidationResults.Success = $false
}

Write-Host ""

# Validate Azure Key Vault Secrets
Write-Host "🔍 [VALIDATE] Checking Azure Key Vault secrets..." -ForegroundColor Yellow

$keyVaultName = Get-KeyVaultName -ResourceGroup $ResourceGroup -Environment $Environment

if ($keyVaultName) {
    Write-Host "📝 [INFO] Using Key Vault: $keyVaultName" -ForegroundColor Blue
    
    try {
        $keyVaultSecrets = az keyvault secret list --vault-name $keyVaultName --query "[].{name:name, enabled:attributes.enabled}" | ConvertFrom-Json
        
        foreach ($secret in $ExpectedSecrets.Keys) {
            $kvSecretName = switch ($secret) {
                "REPLICATE_WEBHOOK_SECRET" { "ReplicateWebhookSecret" }
                "GOOGLE_CLIENT_ID" { "GoogleClientId" }
                default { $secret }
            }
            
            $kvSecret = $keyVaultSecrets | Where-Object { $_.name -eq $kvSecretName }
            if ($kvSecret -and $kvSecret.enabled) {
                $ValidationResults.KeyVaultSecrets[$secret] = @{
                    "Exists" = $true
                    "KeyVaultName" = $kvSecretName
                    "Enabled" = $kvSecret.enabled
                }
                Write-Host "  ✅ Key Vault secret '$kvSecretName' exists and is enabled" -ForegroundColor Green
            } else {
                $ValidationResults.KeyVaultSecrets[$secret] = @{
                    "Exists" = $false
                    "KeyVaultName" = $kvSecretName
                    "Enabled" = $false
                }
                $ValidationResults.Mismatches += "Key Vault secret '$kvSecretName' is missing or disabled"
                $ValidationResults.Success = $false
                Write-Host "  ❌ Key Vault secret '$kvSecretName' is missing or disabled" -ForegroundColor Red
            }
        }
    } catch {
        Write-Host "❌ [ERROR] Failed to check Key Vault secrets: $($_.Exception.Message)" -ForegroundColor Red
        $ValidationResults.Success = $false
    }
} else {
    Write-Host "⚠️ [WARNING] Could not find Key Vault in resource group '$ResourceGroup'" -ForegroundColor Yellow
    $ValidationResults.Success = $false
}

Write-Host ""

# Update Key Vault if requested
if ($UpdateKeyVault -and $keyVaultName -and !$DryRun) {
    Write-Host "🔄 [UPDATE] Updating Key Vault secrets..." -ForegroundColor Yellow
    
    foreach ($secret in $ExpectedSecrets.Keys) {
        $kvSecretName = switch ($secret) {
            "REPLICATE_WEBHOOK_SECRET" { "ReplicateWebhookSecret" }
            "GOOGLE_CLIENT_ID" { "GoogleClientId" }
            default { $secret }
        }
        
        $secretValue = $ExpectedSecrets[$secret]
        
        try {
            Write-Host "  📝 [INFO] Setting Key Vault secret: $kvSecretName" -ForegroundColor Blue
            az keyvault secret set --vault-name $keyVaultName --name $kvSecretName --value $secretValue --output none
            Write-Host "  ✅ Successfully updated Key Vault secret: $kvSecretName" -ForegroundColor Green
        } catch {
            Write-Host "  ❌ Failed to update Key Vault secret '$kvSecretName': $($_.Exception.Message)" -ForegroundColor Red
            $ValidationResults.Success = $false
        }
    }
    Write-Host ""
}

# Summary
Write-Host "📊 [SUMMARY] Validation Results" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan

if ($ValidationResults.Success) {
    Write-Host "✅ All secrets are properly configured!" -ForegroundColor Green
} else {
    Write-Host "❌ Secret configuration issues found:" -ForegroundColor Red
    foreach ($mismatch in $ValidationResults.Mismatches) {
        Write-Host "  • $mismatch" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "📋 [INFO] Next Steps:" -ForegroundColor Blue
if (!$ValidationResults.Success) {
    Write-Host "  1. Fix missing secrets in GitHub or Azure Key Vault" -ForegroundColor Yellow
    Write-Host "  2. Run with -UpdateKeyVault to sync secrets to Key Vault" -ForegroundColor Yellow
    Write-Host "  3. Re-run validation to confirm fixes" -ForegroundColor Yellow
}
Write-Host "  4. Deploy infrastructure: az deployment group create..." -ForegroundColor Yellow
Write-Host "  5. Verify application health after deployment" -ForegroundColor Yellow

Write-Host ""
Write-Host "🔗 [REFERENCE] Secret Management Architecture:" -ForegroundColor Blue
Write-Host "  • GitHub Secrets: Used during CI/CD deployment" -ForegroundColor Gray
Write-Host "  • Azure Key Vault: Runtime secret storage for applications" -ForegroundColor Gray
Write-Host "  • Bicep Template: Synchronizes GitHub secrets to Key Vault" -ForegroundColor Gray

# Exit with appropriate code
if ($ValidationResults.Success) {
    exit 0
} else {
    exit 1
}