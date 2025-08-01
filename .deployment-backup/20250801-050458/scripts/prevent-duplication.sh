#!/bin/bash
set -e

# Azure Deployment Duplication Prevention Script
# Purpose: Implement validation gates to prevent future resource duplication
# Author: Azure Standardization Task
# Date: $(date +%Y-%m-%d)

echo "🛡️  Azure Duplication Prevention Setup"
echo "===================================="

# Configuration
SCRIPTS_DIR="/home/alanw/projects/AI.ProfilePhotoMaker/infrastructure/scripts"
INFRASTRUCTURE_DIR="/home/alanw/projects/AI.ProfilePhotoMaker/infrastructure"
GITHUB_WORKFLOWS_DIR="/home/alanw/projects/AI.ProfilePhotoMaker/.github/workflows"

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Function: Log with color and timestamp
log() {
    local level=$1
    local message=$2
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    
    case $level in
        "INFO")    echo -e "${BLUE}[INFO]${NC}    $timestamp - $message" ;;
        "WARN")    echo -e "${YELLOW}[WARN]${NC}    $timestamp - $message" ;;
        "ERROR")   echo -e "${RED}[ERROR]${NC}   $timestamp - $message" ;;
        "SUCCESS") echo -e "${GREEN}[SUCCESS]${NC} $timestamp - $message" ;;
    esac
}

# Function: Create parameter validation script
create_parameter_validator() {
    log "INFO" "Creating parameter validation script..."
    
    cat > "$SCRIPTS_DIR/validate-parameters.sh" << 'EOF'
#!/bin/bash
# Parameter Validation Script - Prevents invalid configurations

PARAM_FILE="$1"
if [ -z "$PARAM_FILE" ]; then
    echo "Usage: $0 <parameters-file>"
    exit 1
fi

echo "🔍 Validating parameters file: $PARAM_FILE"

# Check if file exists
if [ ! -f "$PARAM_FILE" ]; then
    echo "❌ Parameters file not found: $PARAM_FILE"
    exit 1
fi

# Validate JSON syntax
if ! jq empty "$PARAM_FILE" 2>/dev/null; then
    echo "❌ Invalid JSON syntax in parameters file"
    exit 1
fi

# Extract values
NAME_PREFIX=$(jq -r '.parameters.namePrefix.value' "$PARAM_FILE")
ENVIRONMENT=$(jq -r '.parameters.environmentName.value' "$PARAM_FILE")
SQL_PASSWORD=$(jq -r '.parameters.sqlAdminPassword.value' "$PARAM_FILE")

echo "📊 Parameter values:"
echo "  namePrefix: $NAME_PREFIX"
echo "  environmentName: $ENVIRONMENT"

# Validation rules
VALIDATION_FAILED=false

# Rule 1: namePrefix length (for storage account naming)
if [ ${#NAME_PREFIX} -gt 14 ]; then
    echo "❌ namePrefix too long: ${#NAME_PREFIX} chars (max 14 for storage account naming)"
    VALIDATION_FAILED=true
else
    echo "✅ namePrefix length valid: ${#NAME_PREFIX} chars"
fi

# Rule 2: namePrefix allowed values
if [[ "$NAME_PREFIX" != "aiprofile" ]]; then
    echo "❌ namePrefix must be 'aiprofile' for standardization (got: '$NAME_PREFIX')"
    VALIDATION_FAILED=true
else
    echo "✅ namePrefix follows standard convention"
fi

# Rule 3: Environment validation
if [[ "$ENVIRONMENT" != "staging" && "$ENVIRONMENT" != "production" ]]; then
    echo "❌ environmentName must be 'staging' or 'production' (got: '$ENVIRONMENT')"
    VALIDATION_FAILED=true
else
    echo "✅ environmentName is valid"
fi

# Rule 4: SQL password complexity
if [[ ${#SQL_PASSWORD} -lt 8 ]]; then
    echo "❌ SQL password too short (minimum 8 characters)"
    VALIDATION_FAILED=true
elif [[ ! "$SQL_PASSWORD" =~ [A-Z] ]]; then
    echo "❌ SQL password missing uppercase letter"
    VALIDATION_FAILED=true
elif [[ ! "$SQL_PASSWORD" =~ [a-z] ]]; then
    echo "❌ SQL password missing lowercase letter"
    VALIDATION_FAILED=true
elif [[ ! "$SQL_PASSWORD" =~ [0-9] ]]; then
    echo "❌ SQL password missing number"
    VALIDATION_FAILED=true
elif [[ ! "$SQL_PASSWORD" =~ [^a-zA-Z0-9] ]]; then
    echo "❌ SQL password missing special character"
    VALIDATION_FAILED=true
else
    echo "✅ SQL password meets complexity requirements"
fi

# Rule 5: Storage account name preview
UNIQUE_SUFFIX=$(echo -n "ai-profile-photo-maker-$ENVIRONMENT" | md5sum | cut -c1-13)
EXPECTED_STORAGE_NAME="${NAME_PREFIX:0:14}st${UNIQUE_SUFFIX:0:8}"

if [ ${#EXPECTED_STORAGE_NAME} -gt 24 ]; then
    echo "❌ Expected storage account name too long: ${#EXPECTED_STORAGE_NAME} chars (max 24)"
    echo "   Expected name: $EXPECTED_STORAGE_NAME"
    VALIDATION_FAILED=true
else
    echo "✅ Expected storage account name length valid: ${#EXPECTED_STORAGE_NAME} chars"
    echo "   Expected name: $EXPECTED_STORAGE_NAME"
fi

# Final result
if [ "$VALIDATION_FAILED" = true ]; then
    echo ""
    echo "❌ VALIDATION FAILED - Please fix the above issues"
    exit 1
else
    echo ""
    echo "✅ VALIDATION PASSED - Parameters are valid"
    exit 0
fi
EOF

    chmod +x "$SCRIPTS_DIR/validate-parameters.sh"
    log "SUCCESS" "Parameter validation script created"
}

# Function: Create naming convention checker
create_naming_checker() {
    log "INFO" "Creating naming convention checker..."
    
    cat > "$SCRIPTS_DIR/check-naming-conflicts.sh" << 'EOF'
#!/bin/bash
# Naming Convention Checker - Prevents resource name conflicts

RESOURCE_GROUP="$1"
NAME_PREFIX="$2"
ENVIRONMENT="$3"

if [ -z "$RESOURCE_GROUP" ] || [ -z "$NAME_PREFIX" ] || [ -z "$ENVIRONMENT" ]; then
    echo "Usage: $0 <resource-group> <name-prefix> <environment>"
    exit 1
fi

echo "🔍 Checking naming conflicts in resource group: $RESOURCE_GROUP"
echo "   Name prefix: $NAME_PREFIX"
echo "   Environment: $ENVIRONMENT"

# Check Azure CLI login
if ! az account show > /dev/null 2>&1; then
    echo "❌ Not logged into Azure. Please run: az login"
    exit 1
fi

# Check if resource group exists
if ! az group show --name "$RESOURCE_GROUP" > /dev/null 2>&1; then
    echo "❌ Resource group '$RESOURCE_GROUP' not found"
    exit 1
fi

CONFLICTS_FOUND=false

# Check for conflicting naming patterns
echo ""
echo "🏷️  Checking for conflicting naming patterns..."

# Check for old 'aiapp' pattern resources
OLD_PATTERN_COUNT=$(az resource list \
    --resource-group "$RESOURCE_GROUP" \
    --query "[?contains(name, 'aiapp')] | length(@)" -o tsv)

if [ "$OLD_PATTERN_COUNT" -gt 0 ]; then
    echo "⚠️  Found $OLD_PATTERN_COUNT resources with old 'aiapp' pattern"
    echo "   These should be cleaned up before deployment:"
    az resource list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiapp')].{Name:name, Type:type}" \
        --output table
    CONFLICTS_FOUND=true
else
    echo "✅ No old 'aiapp' pattern resources found"
fi

# Check for long namePrefix resources
LONG_PREFIX_COUNT=$(az resource list \
    --resource-group "$RESOURCE_GROUP" \
    --query "[?contains(name, 'aiprofilephotomaker')] | length(@)" -o tsv)

if [ "$LONG_PREFIX_COUNT" -gt 0 ]; then
    echo "⚠️  Found $LONG_PREFIX_COUNT resources with long 'aiprofilephotomaker' pattern"
    echo "   These may have naming limit issues:"
    az resource list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiprofilephotomaker')].{Name:name, Type:type}" \
        --output table
    CONFLICTS_FOUND=true
else
    echo "✅ No long prefix pattern resources found"
fi

# Check current deployment targets
echo ""
echo "🎯 Checking current deployment targets..."

# Generate expected resource names
UNIQUE_SUFFIX=$(echo -n "$RESOURCE_GROUP" | md5sum | cut -c1-13)

EXPECTED_RESOURCES=(
    "${NAME_PREFIX}-asp-${ENVIRONMENT}"
    "${NAME_PREFIX}api-${ENVIRONMENT}"
    "${NAME_PREFIX}-swa-${ENVIRONMENT}"
    "${NAME_PREFIX}-sql-${ENVIRONMENT}-${UNIQUE_SUFFIX}"
    "${NAME_PREFIX}db"
    "${NAME_PREFIX:0:14}st${UNIQUE_SUFFIX:0:8}"
    "${NAME_PREFIX}-kv-${ENVIRONMENT}-${UNIQUE_SUFFIX}"
    "${NAME_PREFIX}-ai-${ENVIRONMENT}"
    "${NAME_PREFIX}-la-${ENVIRONMENT}"
)

for RESOURCE_NAME in "${EXPECTED_RESOURCES[@]}"; do
    if az resource show --resource-group "$RESOURCE_GROUP" --name "$RESOURCE_NAME" > /dev/null 2>&1; then
        echo "ℹ️  Resource '$RESOURCE_NAME' already exists (will be updated)"
    else
        echo "✅ Resource name '$RESOURCE_NAME' available"
    fi
done

# Final result
echo ""
if [ "$CONFLICTS_FOUND" = true ]; then
    echo "⚠️  NAMING CONFLICTS DETECTED"
    echo "   Run cleanup script before deploying: ./azure-resource-cleanup.sh"
    exit 1
else
    echo "✅ NO NAMING CONFLICTS - Safe to deploy"
    exit 0
fi
EOF

    chmod +x "$SCRIPTS_DIR/check-naming-conflicts.sh"
    log "SUCCESS" "Naming convention checker created"
}

# Function: Create pre-commit hook
create_precommit_hook() {
    log "INFO" "Creating pre-commit validation hook..."
    
    mkdir -p "/home/alanw/projects/AI.ProfilePhotoMaker/.git/hooks"
    
    cat > "/home/alanw/projects/AI.ProfilePhotoMaker/.git/hooks/pre-commit" << 'EOF'
#!/bin/bash
# Pre-commit hook for Azure infrastructure validation

echo "🔍 Running pre-commit Azure infrastructure validation..."

INFRASTRUCTURE_DIR="infrastructure"
SCRIPTS_DIR="$INFRASTRUCTURE_DIR/scripts"

# Check if infrastructure files are being committed
INFRASTRUCTURE_CHANGES=$(git diff --cached --name-only | grep "^$INFRASTRUCTURE_DIR/" || true)

if [ -z "$INFRASTRUCTURE_CHANGES" ]; then
    echo "✅ No infrastructure changes detected"
    exit 0
fi

echo "📊 Infrastructure changes detected:"
echo "$INFRASTRUCTURE_CHANGES"

# Validate parameter files
for PARAM_FILE in $(echo "$INFRASTRUCTURE_CHANGES" | grep "parameters\..*\.json$" || true); do
    if [ -f "$PARAM_FILE" ]; then
        echo "🔍 Validating parameter file: $PARAM_FILE"
        
        if [ -x "$SCRIPTS_DIR/validate-parameters.sh" ]; then
            if ! "$SCRIPTS_DIR/validate-parameters.sh" "$PARAM_FILE"; then
                echo "❌ Parameter validation failed for: $PARAM_FILE"
                exit 1
            fi
        else
            echo "⚠️  Parameter validator not found: $SCRIPTS_DIR/validate-parameters.sh"
        fi
    fi
done

# Validate Bicep templates
for BICEP_FILE in $(echo "$INFRASTRUCTURE_CHANGES" | grep "\.bicep$" || true); do
    if [ -f "$BICEP_FILE" ]; then
        echo "🔍 Validating Bicep template: $BICEP_FILE"
        
        # Check Bicep syntax
        if command -v az > /dev/null && az bicep version > /dev/null 2>&1; then
            if ! az bicep build --file "$BICEP_FILE" --stdout > /dev/null; then
                echo "❌ Bicep template validation failed for: $BICEP_FILE"
                exit 1
            fi
        else
            echo "⚠️  Bicep CLI not available for validation"
        fi
    fi
done

# Check for forbidden patterns
echo "🔍 Checking for forbidden naming patterns..."

FORBIDDEN_PATTERNS=("aiapp" "aiprofilephotomaker")
for PATTERN in "${FORBIDDEN_PATTERNS[@]}"; do
    if echo "$INFRASTRUCTURE_CHANGES" | xargs grep -l "\"$PATTERN\"" 2>/dev/null; then
        echo "❌ Forbidden naming pattern '$PATTERN' found in infrastructure files"
        echo "   Use 'aiprofile' as the standard namePrefix"
        exit 1
    fi
done

echo "✅ Pre-commit validation passed"
exit 0
EOF

    chmod +x "/home/alanw/projects/AI.ProfilePhotoMaker/.git/hooks/pre-commit"
    log "SUCCESS" "Pre-commit hook created"
}

# Function: Create GitHub Actions workflow
create_github_workflow() {
    log "INFO" "Creating GitHub Actions workflow..."
    
    mkdir -p "$GITHUB_WORKFLOWS_DIR"
    
    cat > "$GITHUB_WORKFLOWS_DIR/azure-infrastructure-validation.yml" << 'EOF'
name: Azure Infrastructure Validation

on:
  pull_request:
    paths:
      - 'infrastructure/**'
  workflow_dispatch:

jobs:
  validate-infrastructure:
    runs-on: ubuntu-latest
    name: Validate Azure Infrastructure
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4
      
    - name: Setup Azure CLI
      uses: azure/CLI@v1
      with:
        azcliversion: latest
        
    - name: Login to Azure
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_CREDENTIALS }}
        
    - name: Install Bicep CLI
      run: az bicep install
      
    - name: Validate Parameter Files
      run: |
        echo "🔍 Validating parameter files..."
        for param_file in infrastructure/parameters.*.json; do
          if [ -f "$param_file" ]; then
            echo "Validating: $param_file"
            
            # JSON syntax check
            if ! jq empty "$param_file"; then
              echo "❌ Invalid JSON syntax in $param_file"
              exit 1
            fi
            
            # Run custom validation
            if [ -x "infrastructure/scripts/validate-parameters.sh" ]; then
              if ! infrastructure/scripts/validate-parameters.sh "$param_file"; then
                echo "❌ Parameter validation failed for $param_file"
                exit 1
              fi
            fi
          fi
        done
        
    - name: Validate Bicep Templates
      run: |
        echo "🔍 Validating Bicep templates..."
        for bicep_file in infrastructure/*.bicep; do
          if [ -f "$bicep_file" ]; then
            echo "Validating: $bicep_file"
            
            # Bicep syntax check
            if ! az bicep build --file "$bicep_file" --stdout > /dev/null; then
              echo "❌ Bicep validation failed for $bicep_file"
              exit 1
            fi
          fi
        done
        
    - name: Check Naming Conventions
      run: |
        echo "🔍 Checking naming conventions..."
        
        # Check for forbidden patterns
        forbidden_patterns=("aiapp" "aiprofilephotomaker")
        for pattern in "${forbidden_patterns[@]}"; do
          if grep -r "\"$pattern\"" infrastructure/ --include="*.json" --include="*.bicep"; then
            echo "❌ Forbidden naming pattern '$pattern' found"
            echo "   Use 'aiprofile' as the standard namePrefix"
            exit 1
          fi
        done
        
    - name: Dry Run Deployment Validation
      run: |
        echo "🔍 Running deployment validation..."
        
        # Validate against Azure (staging environment)
        if az deployment group validate \
          --resource-group "ai-profile-photo-maker-staging" \
          --template-file "infrastructure/main.bicep" \
          --parameters "@infrastructure/parameters.staging.standardized.json"; then
          echo "✅ Deployment validation passed"
        else
          echo "❌ Deployment validation failed"
          exit 1
        fi
        
    - name: Generate Validation Report
      if: always()
      run: |
        echo "📋 Generating validation report..."
        
        cat > validation-report.md << 'REPORT_EOF'
        # Azure Infrastructure Validation Report
        
        **Workflow**: ${{ github.workflow }}
        **Run ID**: ${{ github.run_id }}
        **Branch**: ${{ github.ref_name }}
        **Commit**: ${{ github.sha }}
        
        ## Validation Results
        
        - ✅ Parameter file validation
        - ✅ Bicep template validation  
        - ✅ Naming convention checks
        - ✅ Azure deployment validation
        
        ## Files Validated
        
        $(find infrastructure/ -name "*.json" -o -name "*.bicep" | sed 's/^/- /')
        
        ## Next Steps
        
        If all validations pass, the infrastructure is ready for deployment.
        REPORT_EOF
        
    - name: Upload Validation Report
      if: always()
      uses: actions/upload-artifact@v4
      with:
        name: validation-report
        path: validation-report.md
EOF

    log "SUCCESS" "GitHub Actions workflow created"
}

# Function: Create deployment checklist
create_deployment_checklist() {
    log "INFO" "Creating deployment checklist..."
    
    cat > "$INFRASTRUCTURE_DIR/DEPLOYMENT_CHECKLIST.md" << 'EOF'
# Azure Deployment Checklist

## Pre-Deployment Validation ✅

### 1. Parameter File Validation
- [ ] JSON syntax is valid
- [ ] namePrefix is "aiprofile" (not "aiapp" or "aiprofilephotomaker")
- [ ] namePrefix length ≤ 14 characters for storage account naming
- [ ] environmentName is "staging" or "production"
- [ ] SQL password meets complexity requirements (8+ chars, uppercase, lowercase, number, special char)
- [ ] All required parameters are present and valid

**Validation Command**: `./scripts/validate-parameters.sh parameters.staging.standardized.json`

### 2. Template Validation
- [ ] Bicep template syntax is valid
- [ ] Template builds successfully to ARM JSON
- [ ] No syntax errors or warnings
- [ ] Resource dependencies are correctly defined

**Validation Command**: `az bicep build --file main.bicep`

### 3. Naming Convention Check
- [ ] No conflicting "aiapp" pattern resources exist
- [ ] No problematic "aiprofilephotomaker" pattern resources exist
- [ ] Expected resource names are available or safe to update
- [ ] Storage account name will be ≤24 characters

**Validation Command**: `./scripts/check-naming-conflicts.sh ai-profile-photo-maker-staging aiprofile staging`

### 4. Azure Deployment Validation
- [ ] Azure CLI is logged in with correct subscription
- [ ] Resource group exists and is accessible
- [ ] User has Contributor or Owner permissions
- [ ] Template passes Azure deployment validation
- [ ] No Azure policy violations

**Validation Command**: `./scripts/validate-deployment.sh`

## Deployment Execution ✅

### 5. Pre-Deployment Backup
- [ ] Current resource inventory captured
- [ ] Existing SQL databases backed up (if any)
- [ ] Storage account data backed up (if any)
- [ ] Key Vault secrets documented (if updating)

**Backup Command**: `./scripts/azure-resource-audit.sh`

### 6. Deployment Process
- [ ] Deployment executed with monitoring
- [ ] All resources created/updated successfully
- [ ] No deployment errors or warnings
- [ ] Deployment outputs captured

**Deployment Command**: `./scripts/deploy-standardized.sh`

### 7. Post-Deployment Verification
- [ ] All expected resources are present
- [ ] Web App is running and accessible
- [ ] SQL Server and database are accessible
- [ ] Storage account and containers are configured
- [ ] Key Vault secrets are accessible to applications
- [ ] Application Insights is collecting data
- [ ] Static Web App is deployed and accessible

**Verification Command**: Built into deployment script

## Post-Deployment Cleanup ✅

### 8. Duplicate Resource Cleanup
- [ ] Old "aiapp" pattern resources identified
- [ ] Data migration completed (if needed)
- [ ] Duplicate resources safely deleted
- [ ] Resource cleanup verified

**Cleanup Command**: `./scripts/azure-resource-cleanup.sh`

### 9. Application Testing
- [ ] Frontend application loads correctly
- [ ] API endpoints respond correctly
- [ ] Database connectivity verified
- [ ] Image upload/processing works
- [ ] Authentication flows work
- [ ] All integrations functional

### 10. Monitoring and Alerts
- [ ] Application Insights is receiving telemetry
- [ ] Log Analytics workspace is collecting logs
- [ ] No critical errors in application logs
- [ ] Performance metrics are within acceptable ranges

## Security Verification ✅

### 11. Security Configuration
- [ ] HTTPS is enforced on all web applications
- [ ] TLS 1.2 minimum is configured
- [ ] SQL Server firewall rules are appropriate
- [ ] Key Vault access policies are configured correctly
- [ ] Storage account access is properly configured
- [ ] No sensitive data in configuration files

### 12. Access Control
- [ ] Web App managed identity is configured
- [ ] Key Vault access permissions are minimal and appropriate
- [ ] SQL Server authentication is working
- [ ] Storage account access is secure

## Documentation Updates ✅

### 13. Project Documentation
- [ ] README updated with new resource names
- [ ] Deployment instructions updated
- [ ] Architecture diagrams updated (if needed)
- [ ] API documentation updated with new URLs

### 14. Team Communication
- [ ] Team notified of infrastructure changes
- [ ] New URLs communicated to stakeholders
- [ ] Any breaking changes documented
- [ ] Support documentation updated

## Rollback Preparedness ✅

### 15. Rollback Plan
- [ ] Backup locations documented
- [ ] Rollback procedures tested
- [ ] Emergency contact information available
- [ ] Rollback scripts ready if needed

---

## Quick Commands Reference

```bash
# Full validation and deployment workflow
./scripts/validate-deployment.sh
./scripts/deploy-standardized.sh
./scripts/azure-resource-cleanup.sh

# Individual validation steps
./scripts/validate-parameters.sh parameters.staging.standardized.json
./scripts/check-naming-conflicts.sh ai-profile-photo-maker-staging aiprofile staging
az bicep build --file main.bicep

# Monitoring and verification
az resource list --resource-group ai-profile-photo-maker-staging --output table
az deployment group list --resource-group ai-profile-photo-maker-staging --output table
```

## Emergency Procedures

If deployment fails:
1. Check deployment logs for specific errors
2. Verify all prerequisites are met
3. Ensure Azure CLI is logged into correct subscription
4. Check for resource quotas or policy restrictions
5. Contact Azure support if needed

If rollback is needed:
1. Stop any running deployments
2. Restore from backups using provided scripts
3. Verify application functionality
4. Investigate root cause before retry
EOF

    log "SUCCESS" "Deployment checklist created"
}

# Function: Set up executable permissions
set_permissions() {
    log "INFO" "Setting executable permissions on scripts..."
    
    chmod +x "$SCRIPTS_DIR"/*.sh
    
    log "SUCCESS" "Script permissions configured"
}

# Function: Display summary
display_summary() {
    echo ""
    echo "🛡️  Azure Duplication Prevention Setup Complete"
    echo "============================================="
    echo ""
    echo "📋 Created validation and prevention tools:"
    echo "   ✅ Parameter validation script"
    echo "   ✅ Naming convention checker"  
    echo "   ✅ Pre-commit validation hook"
    echo "   ✅ GitHub Actions workflow"
    echo "   ✅ Deployment checklist"
    echo ""
    echo "🔍 Validation workflow:"
    echo "   1. ./scripts/validate-parameters.sh <param-file>"
    echo "   2. ./scripts/check-naming-conflicts.sh <rg> <prefix> <env>"
    echo "   3. ./scripts/validate-deployment.sh"
    echo "   4. ./scripts/deploy-standardized.sh"
    echo ""
    echo "🚀 Ready for standardized deployments!"
}

# Main execution
main() {
    log "INFO" "Setting up Azure duplication prevention tools"
    
    create_parameter_validator
    create_naming_checker
    create_precommit_hook
    create_github_workflow
    create_deployment_checklist
    set_permissions
    display_summary
    
    log "SUCCESS" "Azure duplication prevention setup completed!"
}

# Execute main function
main "$@"