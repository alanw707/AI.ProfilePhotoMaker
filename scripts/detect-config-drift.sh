#!/bin/bash

# =============================================================================
# Configuration Drift Detection Script
# =============================================================================
# Proactively monitors configuration mismatches between:
# 1. Application environment variable expectations (EnvironmentConfiguration.cs)
# 2. Infrastructure definitions (Bicep templates)  
# 3. Runtime environment variables
# 4. CI/CD configuration (GitHub Actions)
# 5. Validation scripts and expectations
#
# This script validates infrastructure-generated Azure Storage configuration
# and distinguishes between secret-based vs infrastructure-generated patterns.
# =============================================================================

# Use -u and pipefail for safety, avoid -e so analysis continues even if a subcommand returns non-zero
set -uo pipefail

# Script version for tracking changes
SCRIPT_VERSION="1.0.0"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
NC='\033[0m' # No Color

# Configuration
TARGET_ENV="${1:-Production}"
VERBOSE="${VERBOSE:-false}"
OUTPUT_FORMAT="${OUTPUT_FORMAT:-console}"  # console, json, github-actions
EXIT_ON_DRIFT="${EXIT_ON_DRIFT:-true}"

# Counters
TOTAL_CHECKS=0
CRITICAL_DRIFTS=0
WARNING_DRIFTS=0
INFO_ITEMS=0

# Results storage
declare -a CRITICAL_ISSUES=()
declare -a WARNING_ISSUES=()
declare -a INFO_ITEMS_ARRAY=()
declare -a REMEDIATION_STEPS=()

# Output functions
log_info() { 
    echo -e "${BLUE}[INFO]${NC} $1" 
    ((TOTAL_CHECKS++))
}

log_success() { 
    echo -e "${GREEN}[SUCCESS]${NC} $1" 
    ((TOTAL_CHECKS++))
}

log_warning() { 
    echo -e "${YELLOW}[WARNING]${NC} $1"
    WARNING_ISSUES+=("$1")
    ((WARNING_DRIFTS++))
    ((TOTAL_CHECKS++))
}

log_critical() { 
    echo -e "${RED}[CRITICAL]${NC} $1"
    CRITICAL_ISSUES+=("$1")
    ((CRITICAL_DRIFTS++))
    ((TOTAL_CHECKS++))
}

log_item() {
    echo -e "${CYAN}[ITEM]${NC} $1"
    INFO_ITEMS_ARRAY+=("$1")
    ((INFO_ITEMS++))
}

# Verbose logging
log_verbose() {
    if [[ "$VERBOSE" == "true" ]]; then
        echo -e "${MAGENTA}[VERBOSE]${NC} $1"
    fi
}

# Add remediation step
add_remediation() {
    REMEDIATION_STEPS+=("$1")
}

# GitHub Actions output format
github_output() {
    if [[ "$OUTPUT_FORMAT" == "github-actions" ]]; then
        echo "::$1::$2"
    fi
}

# =============================================================================
# HEADER AND INITIALIZATION
# =============================================================================

print_header() {
    echo -e "${BLUE}=============================================================================${NC}"
    echo -e "${BLUE}🔍 Configuration Drift Detection System v${SCRIPT_VERSION}${NC}"
    echo -e "${BLUE}Target Environment: ${TARGET_ENV}${NC}"
    echo -e "${BLUE}Timestamp: $(date -u '+%Y-%m-%d %H:%M:%S UTC')${NC}"
    echo -e "${BLUE}=============================================================================${NC}"
    echo ""
}

validate_prerequisites() {
    log_info "🔍 Validating prerequisites..."
    
    # Check if we're in the right directory
    if [[ ! -f "AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj" ]]; then
        log_critical "Must run from project root directory (AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj not found)"
        add_remediation "Change to the AI.ProfilePhotoMaker project root directory"
        return 1
    fi
    
    # Check for required files
    local required_files=(
        "AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs"
        "infrastructure/azure-env-config.bicep"
        "scripts/validate-secrets.sh"
        ".github/workflows/simple-deploy.yml"
    )
    
    for file in "${required_files[@]}"; do
        if [[ ! -f "$file" ]]; then
            log_critical "Required file missing: $file"
            add_remediation "Ensure all configuration files are present in the repository"
            return 1
        fi
    done
    
    log_success "✅ All prerequisites validated"
    return 0
}

# =============================================================================
# APPLICATION CONFIGURATION ANALYSIS
# =============================================================================

extract_app_environment_variables() {
    log_info "📊 Extracting application environment variable expectations..."
    
    local env_config_file="AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs"
    
    # Extract required environment variables
    REQUIRED_APP_VARS=$(grep -E "^\s*public const string [A-Z_]+ = \"[A-Z_]+\";" "$env_config_file" | \
        sed -E 's/.*= "([^"]+)".*/\1/' | sort -u || true)
    
    log_verbose "Required variables from EnvironmentConfiguration.cs:"
    echo "$REQUIRED_APP_VARS" | while read -r var; do
        log_verbose "  • $var"
    done || true
    
    # Extract optional variables (those checked but not required)
    OPTIONAL_APP_VARS=$(grep -E "GetEnvironmentVariable\(.*\)" "$env_config_file" | \
        grep -v "GetRequiredVariable" | \
        sed -E 's/.*GetEnvironmentVariable\(([^)]+)\).*/\1/' | \
        tr -d '"' | sort -u || true)
    
    log_verbose "Optional variables from EnvironmentConfiguration.cs:"
    echo "$OPTIONAL_APP_VARS" | while read -r var; do
        log_verbose "  • $var"
    done || true
    
    # Check for environment-specific validation logic
    if grep -q "IsProduction\(\)\|IsStaging\(\)" "$env_config_file"; then
        log_item "Environment-specific validation detected in application code"
        
        # Extract production/staging specific requirements
        if grep -A 10 -B 2 "IsProduction\(\)\|IsStaging\(\)" "$env_config_file" | grep -q "AZURE_STORAGE"; then
            log_item "Azure Storage is REQUIRED in Production/Staging environments"
        fi
    fi
    
    log_success "✅ Application configuration analysis completed"
}

# =============================================================================
# INFRASTRUCTURE CONFIGURATION ANALYSIS
# =============================================================================

extract_infrastructure_environment_variables() {
    log_info "🏗️ Extracting infrastructure environment variable definitions..."
    
    local bicep_files=(
        "infrastructure/azure-env-config.bicep"
        "infrastructure/simple-deploy.bicep"
    )
    
    # Combine all Bicep environment variables
    BICEP_ENV_VARS=""
    BICEP_CONFIG_VARS=""
    
    for bicep_file in "${bicep_files[@]}"; do
        if [[ -f "$bicep_file" ]]; then
            log_verbose "Analyzing $bicep_file..."
            
            # Extract direct environment variables
            local file_env_vars=$(grep -E "^\s*name:\s*'[A-Z_]+'" "$bicep_file" | \
                sed "s/.*name: '//" | sed "s/'.*$//" | sort -u || true)
            
            # Extract ASP.NET Core configuration pattern variables
            local file_config_vars=$(grep -E "^\s*name:\s*'[A-Za-z]+__[A-Za-z]+'" "$bicep_file" | \
                sed "s/.*name: '//" | sed "s/'.*$//" | sort -u || true)
            
            BICEP_ENV_VARS=$(echo -e "$BICEP_ENV_VARS\n$file_env_vars" | sort -u | grep -v '^$' || true)
            BICEP_CONFIG_VARS=$(echo -e "$BICEP_CONFIG_VARS\n$file_config_vars" | sort -u | grep -v '^$' || true)
        fi
    done
    
    log_verbose "Infrastructure environment variables:"
    echo "$BICEP_ENV_VARS" | while read -r var; do
        [[ -n "$var" ]] && log_verbose "  • $var"
    done || true
    
    log_verbose "Infrastructure configuration variables:"
    echo "$BICEP_CONFIG_VARS" | while read -r var; do
        [[ -n "$var" ]] && log_verbose "  • $var"
    done || true
    
    log_success "✅ Infrastructure configuration analysis completed"
}

# =============================================================================
# GITHUB ACTIONS CONFIGURATION ANALYSIS
# =============================================================================

extract_cicd_configuration() {
    log_info "🔄 Extracting CI/CD configuration requirements..."
    
    local workflow_file=".github/workflows/simple-deploy.yml"
    
    # Extract secrets used in GitHub Actions
    GITHUB_SECRETS=$(grep -E "secrets\.[A-Z_]+" "$workflow_file" | \
        sed -E 's/.*secrets\.([A-Z_]+).*/\1/' | sort -u || true)
    
    log_verbose "GitHub Actions secrets:"
    echo "$GITHUB_SECRETS" | while read -r secret; do
        [[ -n "$secret" ]] && log_verbose "  • $secret"
    done
    
    # Extract validation logic from workflow
    if grep -q "validate_secret" "$workflow_file"; then
        log_item "GitHub Actions includes secret validation logic"
    fi
    
    # Check for infrastructure validation steps
    if grep -q "Validate Infrastructure Configuration" "$workflow_file"; then
        log_item "GitHub Actions includes infrastructure validation step"
    fi
    
    log_success "✅ CI/CD configuration analysis completed"
}

# =============================================================================
# RUNTIME ENVIRONMENT ANALYSIS
# =============================================================================

analyze_runtime_environment() {
    log_info "⚡ Analyzing runtime environment configuration..."
    
    # Check current environment variables
    log_verbose "Checking current environment variables..."
    
    # Check for development vs production patterns
    if [[ -n "${ASPNETCORE_ENVIRONMENT:-}" ]]; then
        log_item "ASPNETCORE_ENVIRONMENT is set to: $ASPNETCORE_ENVIRONMENT"
        
        # Validate environment-specific requirements
        if [[ "$ASPNETCORE_ENVIRONMENT" == "Production" ]] || [[ "$ASPNETCORE_ENVIRONMENT" == "Staging" ]]; then
            check_production_requirements
        fi
    fi
    
    # Check for development storage patterns that shouldn't be in production
    if [[ -n "${AZURE_STORAGE_CONNECTION_STRING:-}" ]]; then
        if echo "$AZURE_STORAGE_CONNECTION_STRING" | grep -q "UseDevelopmentStorage=true"; then
            if [[ "$TARGET_ENV" == "Production" ]] || [[ "$TARGET_ENV" == "Staging" ]]; then
                log_critical "Development storage detected in $TARGET_ENV environment"
                add_remediation "Configure real Azure Storage connection string for $TARGET_ENV"
            fi
        fi
    fi
    
    log_success "✅ Runtime environment analysis completed"
}

check_production_requirements() {
    log_info "🎯 Checking production-specific requirements..."
    
    # Azure Storage is generated by Bicep infrastructure (not provided as secrets)
    log_info "Azure Storage credentials are dynamically generated by Bicep deployment"
    if [[ -n "${AZURE_STORAGE_CONNECTION_STRING:-}" ]]; then
        log_item "Azure Storage connection string detected in runtime environment (Bicep-generated)"
    fi
    
    if [[ -n "${AZURE_STORAGE_CONTAINER_NAME:-}" ]]; then
        log_item "Azure Storage container name detected in runtime environment (Bicep-generated)"
    fi
}

# =============================================================================
# CONFIGURATION DRIFT DETECTION
# =============================================================================

detect_configuration_drift() {
    log_info "🔍 Detecting configuration drift between systems..."
    
    # Critical variables that must be consistent across all systems
    local critical_vars=(
        "AZURE_STORAGE_CONNECTION_STRING"
        "AZURE_STORAGE_CONTAINER_NAME"
        "JWT_SECRET"
        "REPLICATE_API_TOKEN"
        "REPLICATE_WEBHOOK_SECRET"
        "GOOGLE_CLIENT_ID"
        "GOOGLE_CLIENT_SECRET"
        "MSSQL_SA_PASSWORD"
    )
    
    echo ""
    log_info "🔍 Cross-referencing critical environment variables..."
    
    for var in "${critical_vars[@]}"; do
        check_variable_consistency "$var"
    done
    
    # Check for common naming pattern mismatches
    detect_naming_mismatches
    
    # Check for missing mappings between systems
    detect_missing_mappings
}

check_variable_consistency() {
    local var_name="$1"
    
    log_verbose "Checking consistency for: $var_name"
    
    # Check if application expects this variable
    local app_expects_var=$(echo "$REQUIRED_APP_VARS" | grep -x "$var_name" || true)
    
    # Check if infrastructure provides this variable (direct or config pattern)
    local infrastructure_provides=$(check_infrastructure_provides "$var_name")
    
    # Check if GitHub Actions references this variable
    local github_references=$(echo "$GITHUB_SECRETS" | grep -x "$var_name" || true)
    
    echo -n "  Checking $var_name: "
    
    # Analyze consistency
    local issues=0
    
    if [[ -n "$app_expects_var" ]]; then
        if [[ -z "$infrastructure_provides" ]]; then
            log_critical "❌ Application expects $var_name but infrastructure doesn't provide it"
            add_remediation "Add $var_name to infrastructure Bicep templates"
            ((issues++))
        fi
        
        if [[ -z "$github_references" ]] && is_secret_variable "$var_name"; then
            log_warning "⚠️  Application expects $var_name but GitHub Actions doesn't reference it"
            add_remediation "Add $var_name to GitHub Actions secrets if needed for CI/CD"
            ((issues++))
        fi
    else
        if [[ -n "$infrastructure_provides" ]]; then
            log_warning "⚠️  Infrastructure provides $var_name but application doesn't expect it"
            ((issues++))
        fi
    fi
    
    if [[ $issues -eq 0 ]]; then
        if [[ -n "$app_expects_var" ]]; then
            echo -e "${GREEN}✅ CONSISTENT${NC}"
        else
            echo -e "${CYAN}ℹ️  Not configured${NC}"
        fi
    fi
}

check_infrastructure_provides() {
    local var_name="$1"
    
    # Check direct environment variable
    local direct_match=$(echo "$BICEP_ENV_VARS" | grep -x "$var_name" || true)
    if [[ -n "$direct_match" ]]; then
        echo "direct"
        return 0
    fi
    
    # Check ASP.NET Core configuration pattern mappings
    case "$var_name" in
        "JWT_SECRET")
            local config_match=$(echo "$BICEP_CONFIG_VARS" | grep -x "Jwt__Secret" || true)
            if [[ -n "$config_match" ]]; then
                echo "config:Jwt__Secret"
                return 0
            fi
            ;;
        "REPLICATE_API_TOKEN")
            local config_match=$(echo "$BICEP_CONFIG_VARS" | grep -x "Replicate__ApiToken" || true)
            if [[ -n "$config_match" ]]; then
                echo "config:Replicate__ApiToken"
                return 0
            fi
            ;;
        "REPLICATE_WEBHOOK_SECRET")
            local config_match=$(echo "$BICEP_CONFIG_VARS" | grep -x "Replicate__WebhookSecret" || true)
            if [[ -n "$config_match" ]]; then
                echo "config:Replicate__WebhookSecret"
                return 0
            fi
            ;;
    esac
    
    # Check if it's provided via connection string pattern
    if [[ "$var_name" == "MSSQL_SA_PASSWORD" ]]; then
        local conn_match=$(echo "$BICEP_CONFIG_VARS" | grep -x "ConnectionStrings__DefaultConnection" || true)
        if [[ -n "$conn_match" ]]; then
            echo "config:ConnectionStrings__DefaultConnection"
            return 0
        fi
    fi
    
    echo ""
    return 1
}

is_secret_variable() {
    local var_name="$1"
    
    # Variables that should be in GitHub secrets
    local secret_vars=(
        "JWT_SECRET"
        "REPLICATE_API_TOKEN"
        "REPLICATE_WEBHOOK_SECRET"
        "GOOGLE_CLIENT_SECRET"
        "MSSQL_SA_PASSWORD"
        "AZURE_STORAGE_CONNECTION_STRING"
    )
    
    for secret_var in "${secret_vars[@]}"; do
        if [[ "$var_name" == "$secret_var" ]]; then
            return 0
        fi
    done
    
    return 1
}

detect_naming_mismatches() {
    log_info "🔍 Detecting naming pattern mismatches..."
    
    # Known Azure Storage patterns (Bicep provides multiple formats)
    local azure_storage_patterns=(
        "ConnectionStrings__AzureStorage"
        "AzureStorage__ConnectionString" 
        "AZURE_STORAGE_CONNECTION_STRING"
    )
    
    # Common mismatches that have caused issues (excluding valid Azure Storage patterns)
    local known_mismatches=(
        "Azure__Storage__ConnectionString:AZURE_STORAGE_CONNECTION_STRING"
        "AZURE_STORAGE_CONN_STRING:AZURE_STORAGE_CONNECTION_STRING"
        "ConnectionString:AZURE_STORAGE_CONNECTION_STRING"
    )
    
    for mismatch in "${known_mismatches[@]}"; do
        local wrong_name="${mismatch%%:*}"
        local correct_name="${mismatch##*:}"
        
        # Check if wrong name appears in infrastructure
        if echo "$BICEP_ENV_VARS$BICEP_CONFIG_VARS" | grep -q "^$wrong_name$"; then
            log_critical "Naming mismatch detected: $wrong_name should be $correct_name"
            add_remediation "Rename $wrong_name to $correct_name in infrastructure configuration"
        fi
    done
    
    # Check for similar but incorrect variable names
    log_verbose "Checking for similar variable names that might be incorrect..."
    
    # Validate Azure Storage patterns from Bicep (should have all three patterns)
    local found_patterns=()
    for pattern in "${azure_storage_patterns[@]}"; do
        if echo "$BICEP_ENV_VARS$BICEP_CONFIG_VARS" | grep -q "^$pattern$"; then
            found_patterns+=("$pattern")
            log_success "Found valid Azure Storage pattern: $pattern"
        fi
    done
    
    if [[ ${#found_patterns[@]} -eq 0 ]]; then
        log_critical "No Azure Storage patterns found in Bicep infrastructure"
        add_remediation "Verify Bicep template provides Azure Storage environment variables"
    elif [[ ${#found_patterns[@]} -lt 3 ]]; then
        log_warning "Only ${#found_patterns[@]}/3 Azure Storage patterns found in Bicep"
        log_item "Bicep should provide all three patterns for maximum compatibility"
    else
        log_success "All Azure Storage patterns found in Bicep infrastructure"
    fi
}

detect_missing_mappings() {
    log_info "🔍 Detecting missing configuration mappings..."
    
    # Check for variables that exist in one system but not others
    
    # Variables in app but not in infrastructure
    echo "$REQUIRED_APP_VARS" | while read -r var; do
        if [[ -n "$var" ]]; then
            local infra_provides=$(check_infrastructure_provides "$var")
            if [[ -z "$infra_provides" ]]; then
                if is_critical_for_environment "$var"; then
                    log_critical "Missing infrastructure mapping for required variable: $var"
                    add_remediation "Add $var to infrastructure Bicep templates"
                else
                    log_warning "Application variable not in infrastructure: $var"
                fi
            fi
        fi
    done || true
    
    # Variables in infrastructure but not expected by app
    echo "$BICEP_ENV_VARS" | while read -r var; do
        if [[ -n "$var" ]]; then
            local app_expects=$(echo "$REQUIRED_APP_VARS" | grep -x "$var" || true)
            if [[ -z "$app_expects" ]]; then
                log_item "Infrastructure provides variable not explicitly expected by app: $var"
            fi
        fi
    done || true
}

is_critical_for_environment() {
    local var_name="$1"
    
    # Variables critical for the target environment
    case "$TARGET_ENV" in
        "Production"|"Staging")
            case "$var_name" in
                "AZURE_STORAGE_CONNECTION_STRING"|"AZURE_STORAGE_CONTAINER_NAME"|"JWT_SECRET"|"REPLICATE_API_TOKEN"|"REPLICATE_WEBHOOK_SECRET")
                    return 0
                    ;;
            esac
            ;;
    esac
    
    return 1
}

# =============================================================================
# VALIDATION AGAINST EXISTING SCRIPTS
# =============================================================================

validate_against_existing_scripts() {
    log_info "🔍 Validating against existing validation scripts..."
    
    # Check consistency with validate-secrets.sh
    local validate_secrets_file="scripts/validate-secrets.sh"
    
    if [[ -f "$validate_secrets_file" ]]; then
        # Extract required secrets from validate-secrets.sh
        local validate_secrets_vars=$(grep -E "REQUIRED.*SECRETS" -A 10 "$validate_secrets_file" | \
            grep -E "^\s*\".*\"" | sed 's/.*"\([^"]*\)".*/\1/' | sort -u || true)
        
        log_verbose "Variables checked by validate-secrets.sh:"
        echo "$validate_secrets_vars" | while read -r var; do
            [[ -n "$var" ]] && log_verbose "  • $var"
        done
        
        # Cross-reference with our findings
        echo "$validate_secrets_vars" | while read -r var; do
            if [[ -n "$var" ]]; then
                local app_expects=$(echo "$REQUIRED_APP_VARS" | grep -x "$var" || true)
                if [[ -z "$app_expects" ]]; then
                    log_warning "validate-secrets.sh checks $var but application doesn't explicitly require it"
                fi
            fi
        done || true
        
        echo "$REQUIRED_APP_VARS" | while read -r var; do
            if [[ -n "$var" ]]; then
                local script_checks=$(echo "$validate_secrets_vars" | grep -x "$var" || true)
                if [[ -z "$script_checks" ]] && is_secret_variable "$var"; then
                    log_warning "Application requires $var but validate-secrets.sh doesn't check it"
                    add_remediation "Add $var validation to scripts/validate-secrets.sh"
                fi
            fi
        done || true
    fi
    
    log_success "✅ Validation script consistency check completed"
}

# =============================================================================
# ENVIRONMENT-SPECIFIC DRIFT DETECTION
# =============================================================================

detect_environment_specific_drift() {
    log_info "🎯 Detecting environment-specific configuration drift for $TARGET_ENV..."
    
    case "$TARGET_ENV" in
        "Production"|"Staging")
            check_production_staging_drift
            ;;
        "Development"|"Test")
            check_development_test_drift
            ;;
        *)
            log_warning "Unknown target environment: $TARGET_ENV"
            ;;
    esac
}

check_production_staging_drift() {
    log_info "🔒 Checking Production/Staging specific requirements..."
    
    # Azure Storage validation (infrastructure-generated, not secret-based)
    if echo "$REQUIRED_APP_VARS" | grep -q "AZURE_STORAGE_CONNECTION_STRING"; then
        log_success "Application correctly requires Azure Storage for production (values provided by Bicep)"
    else
        log_warning "Application doesn't explicitly mark Azure Storage as required"
        add_remediation "Consider updating EnvironmentConfiguration.cs to validate Azure Storage requirements"
    fi
    
    # Check that development patterns aren't configured for production
    if echo "$BICEP_ENV_VARS" | grep -q "UseDevelopmentStorage"; then
        log_critical "Development storage patterns found in production infrastructure"
        add_remediation "Remove development storage configuration from production Bicep templates"
    fi
    
    # Ensure secrets are properly configured for production
    local production_required_secrets=(
        "JWT_SECRET"
        "REPLICATE_API_TOKEN"
        "REPLICATE_WEBHOOK_SECRET"
        "AZURE_STORAGE_CONNECTION_STRING"
    )
    
    for secret in "${production_required_secrets[@]}"; do
        if ! echo "$GITHUB_SECRETS" | grep -q "$secret"; then
            log_critical "Production required secret $secret not found in GitHub Actions"
            add_remediation "Add $secret to GitHub Actions secrets for production deployment"
        fi
    done
}

check_development_test_drift() {
    log_info "🔧 Checking Development/Test specific configuration..."
    
    # Development can use local storage
    log_item "Development/Test environment allows local storage fallbacks"
    
    # Check that production-only configurations aren't forced in development
    if echo "$BICEP_ENV_VARS" | grep -q "REQUIRE_HTTPS"; then
        local https_config=$(grep -A 5 -B 5 "REQUIRE_HTTPS" infrastructure/*.bicep | grep -v "dev" || true)
        if [[ -n "$https_config" ]]; then
            log_warning "HTTPS requirements may be enforced in development environment"
        fi
    fi
}

# =============================================================================
# CHANGE TRACKING AND HISTORY
# =============================================================================

track_configuration_changes() {
    log_info "📊 Tracking configuration changes over time..."
    
    # Create drift detection history directory
    local history_dir="ClaudeDocs/Config-Drift"
    mkdir -p "$history_dir"
    
    # Generate configuration snapshot
    local snapshot_file="$history_dir/config-snapshot-$(date +%Y%m%d-%H%M%S).json"
    
    cat > "$snapshot_file" << EOF
{
  "timestamp": "$(date -u '+%Y-%m-%d %H:%M:%S UTC')",
  "target_environment": "$TARGET_ENV",
  "script_version": "$SCRIPT_VERSION",
  "application_required_vars": [
$(echo "$REQUIRED_APP_VARS" | sed 's/^/    "/' | sed 's/$/"/' | paste -sd ',' -)
  ],
  "infrastructure_env_vars": [
$(echo "$BICEP_ENV_VARS" | sed 's/^/    "/' | sed 's/$/"/' | paste -sd ',' -)
  ],
  "infrastructure_config_vars": [
$(echo "$BICEP_CONFIG_VARS" | sed 's/^/    "/' | sed 's/$/"/' | paste -sd ',' -)
  ],
  "github_secrets": [
$(echo "$GITHUB_SECRETS" | sed 's/^/    "/' | sed 's/$/"/' | paste -sd ',' -)
  ],
  "drift_summary": {
    "total_checks": $TOTAL_CHECKS,
    "critical_drifts": $CRITICAL_DRIFTS,
    "warning_drifts": $WARNING_DRIFTS,
    "info_items": $INFO_ITEMS
  }
}
EOF
    
    log_item "Configuration snapshot saved to: $snapshot_file"
    
    # Compare with previous snapshot if available
    local latest_snapshot=$(find "$history_dir" -name "config-snapshot-*.json" -type f | sort | tail -2 | head -1)
    if [[ -f "$latest_snapshot" && "$latest_snapshot" != "$snapshot_file" ]]; then
        log_info "📈 Comparing with previous snapshot: $(basename "$latest_snapshot")"
        compare_configuration_snapshots "$latest_snapshot" "$snapshot_file"
    fi
}

compare_configuration_snapshots() {
    local old_snapshot="$1"
    local new_snapshot="$2"
    
    log_verbose "Comparing configuration snapshots..."
    
    # Extract arrays from JSON files and compare
    # This is a simplified comparison - in production, you'd use jq
    local old_vars=$(grep -E '^\s*"[A-Z_]+"\s*[,]?$' "$old_snapshot" | tr -d ' ,"' | sort)
    local new_vars=$(grep -E '^\s*"[A-Z_]+"\s*[,]?$' "$new_snapshot" | tr -d ' ,"' | sort)
    
    # Check for added variables
    local added_vars=$(comm -13 <(echo "$old_vars") <(echo "$new_vars") || true)
    if [[ -n "$added_vars" ]]; then
        log_item "🆕 New configuration variables detected:"
        echo "$added_vars" | while read -r var; do
            [[ -n "$var" ]] && log_item "  + $var"
        done || true
    fi
    
    # Check for removed variables
    local removed_vars=$(comm -23 <(echo "$old_vars") <(echo "$new_vars") || true)
    if [[ -n "$removed_vars" ]]; then
        log_warning "🗑️  Configuration variables removed:"
        echo "$removed_vars" | while read -r var; do
            [[ -n "$var" ]] && log_warning "  - $var"
        done || true
    fi
}

# =============================================================================
# REPORTING AND OUTPUT
# =============================================================================

generate_drift_report() {
    log_info "📊 Generating configuration drift report..."
    
    echo ""
    echo -e "${BLUE}=============================================================================${NC}"
    echo -e "${BLUE}📊 CONFIGURATION DRIFT DETECTION SUMMARY${NC}"
    echo -e "${BLUE}=============================================================================${NC}"
    echo ""
    
    # Summary statistics
    echo -e "${CYAN}Summary Statistics:${NC}"
    echo "  Total Checks Performed: $TOTAL_CHECKS"
    echo "  Critical Issues Found: $CRITICAL_DRIFTS"
    echo "  Warning Issues Found: $WARNING_DRIFTS"
    echo "  Information Items: $INFO_ITEMS"
    echo ""
    
    # Critical issues
    if [[ $CRITICAL_DRIFTS -gt 0 ]]; then
        echo -e "${RED}Critical Issues Requiring Immediate Attention:${NC}"
        for issue in "${CRITICAL_ISSUES[@]}"; do
            echo -e "  ${RED}❌${NC} $issue"
        done
        echo ""
    fi
    
    # Warnings
    if [[ $WARNING_DRIFTS -gt 0 ]]; then
        echo -e "${YELLOW}Warnings That Should Be Addressed:${NC}"
        for warning in "${WARNING_ISSUES[@]}"; do
            echo -e "  ${YELLOW}⚠️${NC} $warning"
        done
        echo ""
    fi
    
    # Remediation steps
    if [[ ${#REMEDIATION_STEPS[@]} -gt 0 ]]; then
        echo -e "${CYAN}Recommended Remediation Steps:${NC}"
        local step_num=1
        for step in "${REMEDIATION_STEPS[@]}"; do
            echo "  $step_num. $step"
            ((step_num++))
        done
        echo ""
    fi
    
    # Overall assessment
    if [[ $CRITICAL_DRIFTS -eq 0 ]] && [[ $WARNING_DRIFTS -eq 0 ]]; then
        echo -e "${GREEN}✅ Configuration Alignment Status: EXCELLENT${NC}"
        echo -e "${GREEN}   All systems are properly aligned with no drift detected.${NC}"
        echo -e "${GREEN}   Configuration management is following best practices.${NC}"
    elif [[ $CRITICAL_DRIFTS -eq 0 ]] && [[ $WARNING_DRIFTS -le 3 ]]; then
        echo -e "${YELLOW}⚠️  Configuration Alignment Status: GOOD${NC}"
        echo -e "${YELLOW}   Minor configuration drift detected but no critical issues.${NC}"
        echo -e "${YELLOW}   Consider addressing warnings to maintain optimal alignment.${NC}"
    elif [[ $CRITICAL_DRIFTS -le 2 ]]; then
        echo -e "${YELLOW}🔸 Configuration Alignment Status: NEEDS ATTENTION${NC}"
        echo -e "${YELLOW}   Some critical issues detected that should be addressed.${NC}"
        echo -e "${YELLOW}   System will likely function but may have reliability issues.${NC}"
    else
        echo -e "${RED}❌ Configuration Alignment Status: CRITICAL${NC}"
        echo -e "${RED}   Multiple critical configuration drift issues detected.${NC}"
        echo -e "${RED}   System may fail during deployment or runtime.${NC}"
    fi
    
    echo ""
    echo -e "${BLUE}=============================================================================${NC}"
}

generate_json_output() {
    if [[ "$OUTPUT_FORMAT" == "json" ]]; then
        cat << EOF
{
  "timestamp": "$(date -u '+%Y-%m-%d %H:%M:%S UTC')",
  "script_version": "$SCRIPT_VERSION",
  "target_environment": "$TARGET_ENV",
  "summary": {
    "total_checks": $TOTAL_CHECKS,
    "critical_drifts": $CRITICAL_DRIFTS,
    "warning_drifts": $WARNING_DRIFTS,
    "info_items": $INFO_ITEMS
  },
  "critical_issues": [
$(printf '    "%s"' "${CRITICAL_ISSUES[@]}" | paste -sd ',' -)
  ],
  "warning_issues": [
$(printf '    "%s"' "${WARNING_ISSUES[@]}" | paste -sd ',' -)
  ],
  "remediation_steps": [
$(printf '    "%s"' "${REMEDIATION_STEPS[@]}" | paste -sd ',' -)
  ],
  "status": "$(if [[ $CRITICAL_DRIFTS -eq 0 ]]; then echo "PASS"; else echo "FAIL"; fi)"
}
EOF
    fi
}

setup_monitoring_integration() {
    log_info "📅 Setting up monitoring integration..."
    
    # Generate cron job entry
    local script_path="$(realpath "$0")"
    local cron_entry="0 6 * * 1 cd $(pwd) && $script_path $TARGET_ENV >> /tmp/config-drift-$(date +%Y%m%d).log 2>&1"
    
    log_item "Weekly cron job entry (runs Mondays at 6 AM):"
    echo "  $cron_entry"
    
    # Generate GitHub Actions workflow snippet
    log_item "GitHub Actions weekly check workflow snippet:"
    cat << 'EOF'
  config-drift-check:
    name: 🔍 Weekly Configuration Drift Check
    runs-on: ubuntu-latest
    if: github.event_name == 'schedule'
    
    steps:
      - name: 📥 Checkout
        uses: actions/checkout@v4
        
      - name: 🔍 Check Configuration Drift
        run: |
          chmod +x scripts/detect-config-drift.sh
          ./scripts/detect-config-drift.sh Production
        env:
          OUTPUT_FORMAT: github-actions
          VERBOSE: true
EOF
    
    # Create monitoring webhook script template
    local webhook_script="scripts/config-drift-webhook.sh"
    if [[ ! -f "$webhook_script" ]]; then
        cat > "$webhook_script" << 'EOF'
#!/bin/bash
# Configuration Drift Monitoring Webhook
# Send alerts when configuration drift is detected

WEBHOOK_URL="${CONFIG_DRIFT_WEBHOOK_URL:-}"
SLACK_WEBHOOK="${SLACK_WEBHOOK_URL:-}"

if [[ -n "$WEBHOOK_URL" ]]; then
    curl -X POST "$WEBHOOK_URL" \
        -H "Content-Type: application/json" \
        -d '{"text":"Configuration drift detected in '"$TARGET_ENV"' environment"}'
fi
EOF
        chmod +x "$webhook_script"
        log_item "Created monitoring webhook script: $webhook_script"
    fi
}

# =============================================================================
# MAIN EXECUTION
# =============================================================================

main() {
    print_header
    
    # Initialize
    if ! validate_prerequisites; then
        exit 1
    fi
    
    # Extract configuration from all sources
    extract_app_environment_variables
    extract_infrastructure_environment_variables
    extract_cicd_configuration
    
    # Analyze current state
    analyze_runtime_environment
    
    # Detect drift
    detect_configuration_drift
    detect_environment_specific_drift
    
    # Validate against existing validation systems
    validate_against_existing_scripts
    
    # Track changes over time
    track_configuration_changes
    
    # Setup monitoring for future drift detection
    setup_monitoring_integration
    
    # Generate reports
    generate_drift_report
    generate_json_output
    
    # GitHub Actions outputs
    if [[ "$OUTPUT_FORMAT" == "github-actions" ]]; then
        github_output "notice" "Configuration drift check completed: $CRITICAL_DRIFTS critical, $WARNING_DRIFTS warnings"
        echo "drift-critical=$CRITICAL_DRIFTS" >> "$GITHUB_OUTPUT"
        echo "drift-warnings=$WARNING_DRIFTS" >> "$GITHUB_OUTPUT"
    fi
    
    # Exit with appropriate code
    if [[ "$EXIT_ON_DRIFT" == "true" ]] && [[ $CRITICAL_DRIFTS -gt 0 ]]; then
        echo ""
        echo -e "${RED}❌ CRITICAL CONFIGURATION DRIFT DETECTED${NC}"
        echo -e "${RED}   Exiting with error code due to critical drift issues.${NC}"
        exit 1
    elif [[ $WARNING_DRIFTS -gt 0 ]]; then
        echo ""
        echo -e "${YELLOW}⚠️  Configuration drift warnings detected${NC}"
        echo -e "${YELLOW}   Exiting with warning code. Consider addressing issues.${NC}"
        exit 2
    else
        echo ""
        echo -e "${GREEN}✅ No critical configuration drift detected${NC}"
        exit 0
    fi
}

# =============================================================================
# SCRIPT EXECUTION
# =============================================================================

# Handle script arguments
case "${1:-}" in
    --help|-h)
        cat << EOF
Configuration Drift Detection Script v$SCRIPT_VERSION

USAGE:
  $0 [ENVIRONMENT] [OPTIONS]

PARAMETERS:
  ENVIRONMENT    Target environment (Production, Staging, Development, Test)
                 Default: Production

ENVIRONMENT VARIABLES:
  VERBOSE               Enable verbose output (true/false)
  OUTPUT_FORMAT         Output format (console, json, github-actions)
  EXIT_ON_DRIFT         Exit with error on critical drift (true/false)

EXAMPLES:
  $0 Production
  VERBOSE=true $0 Staging
  OUTPUT_FORMAT=json $0 Development
  OUTPUT_FORMAT=github-actions $0 Production

EXIT CODES:
  0    No critical drift detected
  1    Critical configuration drift detected
  2    Warning-level drift detected
  >2   Script execution error

This script proactively detects configuration mismatches between:
- Application environment variable expectations
- Infrastructure definitions (Bicep templates)
- CI/CD configuration (GitHub Actions)
- Runtime environment variables

The script would have detected the Azure Storage configuration mismatch
that caused deployment failures in the past.
EOF
        exit 0
        ;;
    --version|-v)
        echo "Configuration Drift Detection Script v$SCRIPT_VERSION"
        exit 0
        ;;
    *)
        # Normal execution
        main
        ;;
esac
