#
# AI Profile Photo Maker - Database Deployment Script (PowerShell)
# Migration: FixStylePromptsDataDriftAndQualityAudit (20260220132108)
#
# Usage: .\deploy-database.ps1 [environment]
# Example: .\deploy-database.ps1 production
#

param(
    [string]$Environment = "development"
)

$ErrorActionPreference = "Stop"

# Configuration
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$MigrationName = "20260220132108_FixStylePromptsDataDriftAndQualityAudit"
$SqlFile = Join-Path $ScriptDir "deploy-migration-style-prompts.sql"

# Logging functions
function Write-Info { param($Message) Write-Host "[INFO] $Message" -ForegroundColor Green }
function Write-Warn { param($Message) Write-Host "[WARN] $Message" -ForegroundColor Yellow }
function Write-Error { param($Message) Write-Host "[ERROR] $Message" -ForegroundColor Red }

# Check prerequisites
function Test-Prerequisites {
    Write-Info "Checking prerequisites..."
    
    if (-not (Test-Path $SqlFile)) {
        throw "SQL file not found: $SqlFile"
    }
    
    # Check for dotnet
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet CLI not found. Please install .NET SDK."
    }
    
    Write-Info "Prerequisites check passed"
}

# Get connection string
function Get-ConnectionString {
    # Priority: Environment variable > appsettings.json
    if ($env:DATABASE_CONNECTION_STRING) {
        return $env:DATABASE_CONNECTION_STRING
    }
    
    $ConfigFile = if ($Environment -eq "production" -and (Test-Path "AI.ProfilePhotoMaker.API/appsettings.Production.json")) {
        "AI.ProfilePhotoMaker.API/appsettings.Production.json"
    } elseif (Test-Path "AI.ProfilePhotoMaker.API/appsettings.json") {
        "AI.ProfilePhotoMaker.API/appsettings.json"
    } else {
        $null
    }
    
    if ($ConfigFile) {
        $Config = Get-Content $ConfigFile | ConvertFrom-Json
        if ($Config.ConnectionStrings.DefaultConnection) {
            return $Config.ConnectionStrings.DefaultConnection
        }
    }
    
    # Prompt user
    Write-Warn "Connection string not found in environment or config"
    return Read-Host "Enter database connection string" -AsSecureString
}

# Deploy migration
function Deploy-Migration {
    Write-Info "Deploying migration..."
    
    Write-Info "Migration will update:"
    Write-Info "  - Id 1: corporate → beach-vibes"
    Write-Info "  - Id 3: consultant → fresh"
    Write-Info "  - All 20 styles: upgraded negative prompts"
    
    Push-Location AI.ProfilePhotoMaker.API
    try {
        dotnet ef database update $MigrationName --verbose
        if ($LASTEXITCODE -ne 0) { throw "Migration failed" }
    } finally {
        Pop-Location
    }
    
    Write-Info "Migration applied successfully"
}

# Verify deployment
function Test-Deployment {
    Write-Info "Verifying deployment..."
    
    Push-Location AI.ProfilePhotoMaker.API
    try {
        $Migrations = dotnet ef migrations list 2>$null
        if ($Migrations -match $MigrationName) {
            Write-Info "✓ Migration found in history"
        } else {
            Write-Warn "⚠ Migration not in history - verify manually"
        }
    } finally {
        Pop-Location
    }
}

# Main execution
try {
    Write-Info "=========================================="
    Write-Info "AI Profile Photo Maker Database Deployment"
    Write-Info "Environment: $Environment"
    Write-Info "Migration: $MigrationName"
    Write-Info "=========================================="
    
    Test-Prerequisites
    
    # Production confirmation
    if ($Environment -eq "production") {
        Write-Warn "⚠️  PRODUCTION DEPLOYMENT DETECTED ⚠️"
        $Confirm = Read-Host "Type 'yes' to continue"
        if ($Confirm -ne "yes") {
            Write-Info "Deployment cancelled"
            exit 0
        }
    }
    
    Deploy-Migration
    Test-Deployment
    
    Write-Info "=========================================="
    Write-Info "✅ DEPLOYMENT SUCCESSFUL"
    Write-Info "=========================================="
    
} catch {
    Write-Error "Deployment failed: $_"
    exit 1
}
