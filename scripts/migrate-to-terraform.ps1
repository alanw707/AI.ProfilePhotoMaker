# Migrate AI Profile Photo Maker to Terraform Infrastructure
# Replaces Bicep deployment with deterministic Terraform approach

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName = "rg-aiprofilemaker-staging",
    
    [Parameter(Mandatory=$true)]
    [string]$SqlAdminPassword,
    
    [Parameter(Mandatory=$true)]
    [string]$JwtSecret,
    
    [Parameter(Mandatory=$true)]
    [string]$ReplicateApiToken,
    
    [switch]$ImportExisting = $true,
    [switch]$DryRun = $false,
    [string]$TerraformPath = "../terraform"
)

Write-Host "🏗️ AI Profile Photo Maker - Terraform Migration" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green

# Validation
if (-not (Test-Path $TerraformPath)) {
    Write-Host "❌ Terraform directory not found: $TerraformPath" -ForegroundColor Red
    exit 1
}

if ($JwtSecret.Length -lt 32) {
    Write-Host "❌ JWT secret must be at least 32 characters long" -ForegroundColor Red
    exit 1
}

if (-not $ReplicateApiToken.StartsWith("r8_")) {
    Write-Host "❌ Replicate API token must start with 'r8_'" -ForegroundColor Red
    exit 1
}

# Initialize Terraform
Write-Host "`n🔧 Initializing Terraform..." -ForegroundColor Green
Set-Location $TerraformPath

try {
    terraform init
    if ($LASTEXITCODE -ne 0) {
        throw "Terraform init failed"
    }
    Write-Host "✅ Terraform initialized successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ Terraform initialization failed: $_" -ForegroundColor Red
    exit 1
}

# Create terraform.tfvars file
Write-Host "`n📝 Creating terraform.tfvars..." -ForegroundColor Green
$tfvarsContent = @"
# AI Profile Photo Maker - Terraform Variables
# Generated on $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

app_name            = "aiprofilemaker"
environment         = "staging"
location           = "East US 2" 
resource_group_name = "$ResourceGroupName"

# Security Configuration
sql_admin_password   = "$SqlAdminPassword"
jwt_secret          = "$JwtSecret"
replicate_api_token = "$ReplicateApiToken"

# Cost Optimization (Staging Environment)
enable_cost_optimization = true
sql_sku                 = "Basic"
container_registry_sku   = "Basic"
storage_replication_type = "LRS"

# Scaling Configuration (Cost-optimized for staging)
backend_min_replicas  = 0
backend_max_replicas  = 3
frontend_min_replicas = 0
frontend_max_replicas = 2

# Monitoring
log_analytics_retention_days = 30
enable_application_insights  = true
"@

$tfvarsContent | Out-File "terraform.tfvars" -Encoding UTF8
Write-Host "✅ terraform.tfvars created with cost-optimized settings" -ForegroundColor Green

# Plan the deployment
Write-Host "`n📋 Planning Terraform deployment..." -ForegroundColor Green
if ($DryRun) {
    Write-Host "🔍 DRY RUN - Showing plan only" -ForegroundColor Cyan
}

try {
    terraform plan -out=tfplan
    if ($LASTEXITCODE -ne 0) {
        throw "Terraform plan failed"
    }
    Write-Host "✅ Terraform plan completed successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ Terraform plan failed: $_" -ForegroundColor Red
    exit 1
}

# Import existing resources if requested
if ($ImportExisting -and -not $DryRun) {
    Write-Host "`n🔄 Importing existing resources..." -ForegroundColor Green
    
    # Get existing resources
    $resources = az resource list -g $ResourceGroupName --output json | ConvertFrom-Json
    $deterministic_suffix = "z3bawc74"  # Known deterministic suffix
    
    # Import target resources (those with deterministic naming)
    $importCommands = @()
    
    foreach ($resource in $resources) {
        $name = $resource.name
        $type = $resource.type
        $resourceId = $resource.id
        
        # Only import resources with deterministic naming
        if ($name -match "z3bawc74$" -or $name -match "(api|web|env)-staging$") {
            switch ($type) {
                "Microsoft.ContainerRegistry/registries" {
                    if ($name -eq "aiprofilemakercrz3bawc74") {
                        $importCommands += @{
                            Address = "azurerm_container_registry.main"
                            Id = $resourceId
                            Description = "Container Registry"
                        }
                    }
                }
                "Microsoft.Storage/storageAccounts" {
                    if ($name -eq "aiprofilemaker​stz3bawc74") {
                        $importCommands += @{
                            Address = "azurerm_storage_account.main"
                            Id = $resourceId
                            Description = "Storage Account"
                        }
                    }
                }
                "Microsoft.KeyVault/vaults" {
                    if ($name -eq "aiprofilemakerkvz3bawc74") {
                        $importCommands += @{
                            Address = "azurerm_key_vault.main"
                            Id = $resourceId
                            Description = "Key Vault"
                        }
                    }
                }
                "Microsoft.Sql/servers" {
                    if ($name -eq "aiprofilemaker-sql-z3bawc74") {
                        $importCommands += @{
                            Address = "azurerm_mssql_server.main"
                            Id = $resourceId
                            Description = "SQL Server"
                        }
                    }
                }
                "Microsoft.App/containerApps" {
                    if ($name -eq "aiprofilemaker-api-staging") {
                        $importCommands += @{
                            Address = "azurerm_container_app.backend"
                            Id = $resourceId
                            Description = "Backend Container App"
                        }
                    } elseif ($name -eq "aiprofilemaker-web-staging") {
                        $importCommands += @{
                            Address = "azurerm_container_app.frontend"
                            Id = $resourceId
                            Description = "Frontend Container App"
                        }
                    }
                }
                "Microsoft.App/managedEnvironments" {
                    if ($name -eq "aiprofilemaker-env-staging") {
                        $importCommands += @{
                            Address = "azurerm_container_app_environment.main"
                            Id = $resourceId
                            Description = "Container App Environment"
                        }
                    }
                }
                "Microsoft.Insights/components" {
                    if ($name -eq "aiprofilemaker-ai-staging") {
                        $importCommands += @{
                            Address = "azurerm_application_insights.main"
                            Id = $resourceId
                            Description = "Application Insights"
                        }
                    }
                }
            }
        }
    }
    
    # Execute import commands
    if ($importCommands.Count -gt 0) {
        Write-Host "📦 Found $($importCommands.Count) resources to import:" -ForegroundColor Yellow
        
        foreach ($import in $importCommands) {
            Write-Host "   Importing $($import.Description): $($import.Address)" -ForegroundColor Gray
            
            try {
                terraform import $import.Address $import.Id
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "   ✅ Successfully imported $($import.Description)" -ForegroundColor Green
                } else {
                    Write-Host "   ⚠️ Import failed for $($import.Description) - will create new" -ForegroundColor Yellow
                }
            } catch {
                Write-Host "   ⚠️ Import error for $($import.Description): $_" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "ℹ️ No existing resources found to import - will create all new" -ForegroundColor Cyan
    }
}

# Apply the Terraform configuration
if (-not $DryRun) {
    Write-Host "`n🚀 Applying Terraform configuration..." -ForegroundColor Green
    
    try {
        terraform apply tfplan
        if ($LASTEXITCODE -ne 0) {
            throw "Terraform apply failed"
        }
        Write-Host "✅ Terraform deployment completed successfully!" -ForegroundColor Green
    } catch {
        Write-Host "❌ Terraform apply failed: $_" -ForegroundColor Red
        Write-Host "💡 Check terraform.log for details" -ForegroundColor Yellow
        exit 1
    }
    
    # Show outputs
    Write-Host "`n📋 Deployment Outputs:" -ForegroundColor Green
    terraform output
    
} else {
    Write-Host "`n🔍 DRY RUN completed - Run without -DryRun to execute deployment" -ForegroundColor Cyan
}

# Generate GitHub Actions workflow update
Write-Host "`n⚙️ Updating GitHub Actions workflow..." -ForegroundColor Green

$workflowUpdate = @"
# Updated GitHub Actions workflow to use Terraform
# Replace the existing Bicep deployment steps with:

      - name: 🏗️ Deploy Infrastructure with Terraform
        working-directory: terraform
        run: |
          terraform init
          terraform plan -out=tfplan
          terraform apply tfplan
          
      - name: 📋 Get Terraform Outputs
        id: terraform
        working-directory: terraform
        run: |
          echo "registry-name=`$(terraform output -raw container_registry_name)" >> `$GITHUB_OUTPUT
          echo "registry-server=`$(terraform output -raw container_registry_login_server)" >> `$GITHUB_OUTPUT
          echo "frontend-url=`$(terraform output -raw frontend_url)" >> `$GITHUB_OUTPUT
          echo "backend-url=`$(terraform output -raw backend_url)" >> `$GITHUB_OUTPUT
"@

$workflowUpdate | Out-File "github-actions-terraform-update.yml" -Encoding UTF8
Write-Host "✅ GitHub Actions workflow update generated: github-actions-terraform-update.yml" -ForegroundColor Green

# Generate summary report
Write-Host "`n📊 TERRAFORM MIGRATION SUMMARY" -ForegroundColor Green
Write-Host "════════════════════════════════════════════════════════════════════════════════════" -ForegroundColor Gray
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Gray
Write-Host "Terraform Path: $TerraformPath" -ForegroundColor Gray
Write-Host "Import Existing: $ImportExisting" -ForegroundColor Gray
Write-Host "Dry Run: $DryRun" -ForegroundColor Gray

Write-Host "`n🏗️ Infrastructure Features:" -ForegroundColor Green
Write-Host "✅ Deterministic resource naming (prevents duplicates)" -ForegroundColor Green
Write-Host "✅ Cost-optimized settings for staging environment" -ForegroundColor Green
Write-Host "✅ Infrastructure as Code with version control" -ForegroundColor Green
Write-Host "✅ Comprehensive resource tagging" -ForegroundColor Green
Write-Host "✅ Security best practices (RBAC, TLS, etc.)" -ForegroundColor Green
Write-Host "✅ Scalable container app configuration" -ForegroundColor Green

Write-Host "`n💰 Cost Optimization:" -ForegroundColor Green
Write-Host "• SQL Database: Basic tier (~$5/month)" -ForegroundColor Gray
Write-Host "• Container Registry: Basic tier (~$5/month)" -ForegroundColor Gray
Write-Host "• Storage Account: LRS replication (~$2/month)" -ForegroundColor Gray
Write-Host "• Container Apps: Scale-to-zero (pay per use)" -ForegroundColor Gray
Write-Host "• Log Analytics: 30-day retention (cost-optimized)" -ForegroundColor Gray

Write-Host "`n🔄 Next Steps:" -ForegroundColor Yellow
Write-Host "1. Review the Terraform deployment outputs above" -ForegroundColor Gray
Write-Host "2. Update GitHub Actions workflow with generated configuration" -ForegroundColor Gray
Write-Host "3. Run cleanup script to remove any remaining duplicates" -ForegroundColor Gray
Write-Host "4. Test application functionality with new infrastructure" -ForegroundColor Gray
Write-Host "5. Monitor costs in Azure Cost Management" -ForegroundColor Gray

if (-not $DryRun) {
    Write-Host "`n🎉 Terraform migration completed successfully!" -ForegroundColor Green
    Write-Host "Your infrastructure is now managed by Terraform with deterministic naming." -ForegroundColor Green
} else {
    Write-Host "`n🔍 Dry run completed. Execute without -DryRun to apply changes." -ForegroundColor Cyan
}

Write-Host "`n📁 Generated Files:" -ForegroundColor Cyan
Write-Host "• terraform/terraform.tfvars - Configuration file" -ForegroundColor Gray
Write-Host "• terraform/tfplan - Terraform execution plan" -ForegroundColor Gray
Write-Host "• github-actions-terraform-update.yml - Workflow update" -ForegroundColor Gray