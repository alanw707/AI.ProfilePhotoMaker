# Azure Storage Credentials Setup Script (PowerShell)
# Automates the process of configuring Azure Storage credentials for testing

param(
    [switch]$KeyOnly,
    [switch]$SasOnly,
    [switch]$TestOnly,
    [switch]$Help
)

# Configuration
$StorageAccount = "aipmstv16j74jubocuukg"
$ContainerName = "style-previews"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir

# Colors for output
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    $colors = @{
        "Red" = "Red"
        "Green" = "Green" 
        "Yellow" = "Yellow"
        "White" = "White"
    }
    Write-Host $Message -ForegroundColor $colors[$Color]
}

Write-ColorOutput "🔧 Azure Storage Credentials Setup" "Green"
Write-ColorOutput "=================================="
Write-ColorOutput "Storage Account: $StorageAccount"
Write-ColorOutput "Container: $ContainerName"
Write-Host ""

# Function to validate Azure CLI
function Test-AzureCLI {
    try {
        $null = Get-Command az -ErrorAction Stop
        Write-ColorOutput "✅ Azure CLI found" "Green"
        return $true
    }
    catch {
        Write-ColorOutput "❌ Azure CLI is not installed" "Red"
        Write-ColorOutput "Please install Azure CLI: https://docs.microsoft.com/cli/azure/install-azure-cli"
        return $false
    }
}

# Function to check Azure login
function Test-AzureLogin {
    try {
        $account = az account show --query "name" -o tsv
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "✅ Logged in to Azure: $account" "Green"
            return $true
        }
    }
    catch { }
    
    Write-ColorOutput "⚠️  Not logged in to Azure" "Yellow"
    Write-ColorOutput "Logging in..."
    az login
    return $LASTEXITCODE -eq 0
}

# Function to get storage account key
function Get-StorageKey {
    Write-ColorOutput "🔍 Retrieving storage account key..." "Yellow"
    
    # Try to get the resource group first
    $resourceGroup = az storage account list --query "[?name=='$StorageAccount'].resourceGroup" -o tsv
    
    if (-not $resourceGroup) {
        Write-ColorOutput "❌ Storage account '$StorageAccount' not found in your subscription" "Red"
        Write-ColorOutput "Please ensure you have access to the storage account"
        return $null
    }
    
    Write-ColorOutput "✅ Found storage account in resource group: $resourceGroup" "Green"
    
    # Get the account key
    $accountKey = az storage account keys list `
        --account-name $StorageAccount `
        --resource-group $resourceGroup `
        --query "[0].value" -o tsv
    
    if (-not $accountKey) {
        Write-ColorOutput "❌ Failed to retrieve storage account key" "Red"
        return $null
    }
    
    Write-ColorOutput "✅ Retrieved storage account key" "Green"
    return $accountKey
}

# Function to generate SAS token
function New-SasToken {
    Write-ColorOutput "🔑 Generating SAS token..." "Yellow"
    
    $resourceGroup = az storage account list --query "[?name=='$StorageAccount'].resourceGroup" -o tsv
    $expiryDate = (Get-Date).AddYears(1).ToString("yyyy-MM-ddTHH:mm:ssZ")
    
    $sasToken = az storage container generate-sas `
        --account-name $StorageAccount `
        --resource-group $resourceGroup `
        --name $ContainerName `
        --permissions rl `
        --expiry $expiryDate `
        --https-only `
        -o tsv
    
    if (-not $sasToken) {
        Write-ColorOutput "❌ Failed to generate SAS token" "Red"
        return $null
    }
    
    Write-ColorOutput "✅ Generated SAS token (expires: $expiryDate)" "Green"
    return $sasToken
}

# Function to create .env file
function New-EnvFile {
    param(
        [string]$AccountKey,
        [string]$SasToken
    )
    
    $envFile = Join-Path $RootDir ".env"
    
    Write-ColorOutput "📝 Creating .env file..." "Yellow"
    
    $envContent = @"
# Azure Storage Configuration for Playwright Tests
# Generated on $(Get-Date)

# Primary connection string (full access)
AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=$StorageAccount;AccountKey=$AccountKey;EndpointSuffix=core.windows.net"

# SAS token URL (read-only access)
AZURE_STORAGE_SAS_URL="https://$StorageAccount.blob.core.windows.net/$ContainerName?$SasToken"

# Test Configuration
FRONTEND_APP_URL="https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io"
AZURE_BLOB_BASE_URL="https://$StorageAccount.blob.core.windows.net/$ContainerName"

# Test Control Flags
RUN_UPLOAD_TESTS="true"
RUN_PERFORMANCE_TESTS="true"
ENABLE_VISUAL_TESTING="true"
TEST_TIMEOUT_MS="30000"

# Debugging
DEBUG_TESTS="false"
VERBOSE_LOGGING="false"
"@

    $envContent | Out-File -FilePath $envFile -Encoding UTF8
    Write-ColorOutput "✅ Created .env file: $envFile" "Green"
}

# Function to test connection
function Test-StorageConnection {
    Write-ColorOutput "🧪 Testing storage connection..." "Yellow"
    
    $result = az storage blob list `
        --account-name $StorageAccount `
        --container-name $ContainerName `
        --auth-mode key `
        --output table 2>$null
    
    if ($LASTEXITCODE -eq 0) {
        Write-ColorOutput "✅ Storage connection test passed" "Green"
    } else {
        Write-ColorOutput "⚠️  Storage connection test failed (container might be empty)" "Yellow"
    }
}

# Function to show usage
function Show-Usage {
    Write-Host "Usage: .\setup-credentials.ps1 [OPTIONS]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -KeyOnly      Only retrieve account key"
    Write-Host "  -SasOnly      Only generate SAS token"
    Write-Host "  -TestOnly     Only test connection"
    Write-Host "  -Help         Show this help"
}

# Main execution
if ($Help) {
    Show-Usage
    exit 0
}

if (-not (Test-AzureCLI)) {
    exit 1
}

if (-not (Test-AzureLogin)) {
    exit 1
}

if ($KeyOnly) {
    $key = Get-StorageKey
    if ($key) {
        Write-Output $key
    }
    exit ($key ? 0 : 1)
}

if ($SasOnly) {
    $sas = New-SasToken
    if ($sas) {
        Write-Output $sas
    }
    exit ($sas ? 0 : 1)
}

if ($TestOnly) {
    Test-StorageConnection
    exit 0
}

# Full setup
Write-Host ""
$accountKey = Get-StorageKey
if (-not $accountKey) {
    exit 1
}

$sasToken = New-SasToken
if (-not $sasToken) {
    exit 1
}

Write-Host ""
New-EnvFile -AccountKey $accountKey -SasToken $sasToken

Write-Host ""
Test-StorageConnection

Write-Host ""
Write-ColorOutput "🎉 Setup complete!" "Green"
Write-Host ""
Write-Host "Next steps:"
Write-Host "1. Review the generated .env file"
Write-Host "2. Run: npm install (in tests/playwright directory)"
Write-Host "3. Run: npm run test"