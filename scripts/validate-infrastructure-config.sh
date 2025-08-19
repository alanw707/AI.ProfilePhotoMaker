#!/bin/bash
# Infrastructure Configuration Validation Script
# Validates alignment between Bicep templates and application environment variable expectations
# 
# NOTE: For comprehensive configuration drift detection across all systems, use:
#       ./scripts/detect-config-drift.sh [Environment]
#
# Usage: ./scripts/validate-infrastructure-config.sh [--verbose]

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Verbose mode
VERBOSE=false
if [[ "$1" == "--verbose" ]]; then
    VERBOSE=true
fi

echo -e "${BLUE}🔍 Infrastructure Configuration Validation${NC}"
echo "=================================================="

# Function for verbose logging
log_verbose() {
    if [[ "$VERBOSE" == "true" ]]; then
        echo -e "${BLUE}[VERBOSE]${NC} $1"
    fi
}

# Check if required files exist
if [[ ! -f "infrastructure/simple-deploy.bicep" ]]; then
    echo -e "${RED}❌ ERROR: infrastructure/simple-deploy.bicep not found${NC}"
    exit 1
fi

if [[ ! -f "AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs" ]]; then
    echo -e "${RED}❌ ERROR: AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs not found${NC}"
    exit 1
fi

# Extract environment variables from Bicep template
echo -e "${BLUE}📊 Analyzing Bicep template...${NC}"
BICEP_ENV_VARS=$(grep -E "^\s*name:\s*'[A-Z_]+'" infrastructure/simple-deploy.bicep | sed "s/.*name: '//" | sed "s/'.*$//" | sort -u)

# Also extract ASP.NET Core configuration pattern variables (Section__Key format)
BICEP_CONFIG_VARS=$(grep -E "^\s*name:\s*'[A-Za-z]+__[A-Za-z]+'" infrastructure/simple-deploy.bicep | sed "s/.*name: '//" | sed "s/'.*$//" | sort -u)

log_verbose "Raw Bicep grep output:"
if [[ "$VERBOSE" == "true" ]]; then
    grep -E "^\s*name:\s*'[A-Z_]+'" infrastructure/simple-deploy.bicep | head -10
fi

echo -e "${GREEN}✅ Environment variables defined in Bicep template:${NC}"
if [[ -n "$BICEP_ENV_VARS" ]]; then
    echo "$BICEP_ENV_VARS" | while read var; do
        echo "  • $var"
    done
else
    echo -e "${YELLOW}⚠️  No environment variables found in Bicep template${NC}"
fi

if [[ -n "$BICEP_CONFIG_VARS" ]]; then
    echo ""
    echo -e "${GREEN}✅ ASP.NET Core configuration variables in Bicep template:${NC}"
    echo "$BICEP_CONFIG_VARS" | while read var; do
        echo "  • $var"
    done
fi

# Extract environment variable constants from EnvironmentConfiguration.cs
echo ""
echo -e "${BLUE}📊 Analyzing application configuration...${NC}"
APP_ENV_VARS=$(grep -E "^\s*public const string [A-Z_]+ = \"[A-Z_]+\";" AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs | sed -E 's/.*= "([^"]+)".*/\1/' | sort -u)

log_verbose "Raw EnvironmentConfiguration.cs grep output:"
if [[ "$VERBOSE" == "true" ]]; then
    grep -E "^\s*public const string [A-Z_]+ = \"[A-Z_]+\";" AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs | head -10
fi

echo -e "${GREEN}✅ Environment variables expected by application:${NC}"
if [[ -n "$APP_ENV_VARS" ]]; then
    echo "$APP_ENV_VARS" | while read var; do
        echo "  • $var"
    done
else
    echo -e "${YELLOW}⚠️  No environment variable constants found in application configuration${NC}"
fi

# Cross-reference critical environment variables
echo ""
echo -e "${BLUE}🔍 Cross-referencing critical environment variables...${NC}"

# Critical variables that must match between Bicep and application
CRITICAL_VARS=(
    "AZURE_STORAGE_CONNECTION_STRING"
    "AZURE_STORAGE_CONTAINER_NAME"
    "JWT_SECRET"
    "REPLICATE_API_TOKEN"
    "REPLICATE_WEBHOOK_SECRET"
    "GOOGLE_CLIENT_ID"
    "GOOGLE_CLIENT_SECRET"
    "MSSQL_SA_PASSWORD"
    "ASPNETCORE_ENVIRONMENT"
)

validation_errors=0
validation_warnings=0

# Function to check if a variable is provided in infrastructure (direct or config pattern)
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
        # Add more mappings as needed
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

# Check each critical variable
for var in "${CRITICAL_VARS[@]}"; do
    infrastructure_provides=$(check_infrastructure_provides "$var")
    app_expects_var=$(echo "$APP_ENV_VARS" | grep -x "$var" || true)
    
    echo -n "  Checking $var: "
    
    if [[ -n "$app_expects_var" ]]; then
        if [[ -n "$infrastructure_provides" ]]; then
            if [[ "$infrastructure_provides" == "direct" ]]; then
                echo -e "${GREEN}✅ MATCH${NC} (Direct environment variable)"
            else
                echo -e "${GREEN}✅ MATCH${NC} (Via $infrastructure_provides)"
            fi
        else
            echo -e "${RED}❌ MISSING in Bicep template${NC} (Application expects this variable)"
            ((validation_errors++))
        fi
    else
        if [[ -n "$infrastructure_provides" ]]; then
            echo -e "${YELLOW}⚠️  PROVIDED but not explicitly expected${NC} (May be unused)"
            ((validation_warnings++))
        else
            echo -e "${BLUE}ℹ️  Not configured${NC} (Neither provided nor expected)"
        fi
    fi
done

# Check for specific naming mismatches that have caused issues
echo ""
echo -e "${BLUE}🔍 Checking for common naming pattern issues...${NC}"

# Look for similar but incorrect variable names in Bicep
BICEP_SIMILAR_VARS=$(grep -E "^\s*name:\s*'[A-Za-z_]*[Ss]torage[A-Za-z_]*'" infrastructure/simple-deploy.bicep | sed "s/.*name: '//" | sed "s/'.*$//" || true)
if [[ -n "$BICEP_SIMILAR_VARS" ]]; then
    echo -e "${BLUE}🔍 Storage-related variables in Bicep:${NC}"
    echo "$BICEP_SIMILAR_VARS" | while read var; do
        if [[ "$var" != "AZURE_STORAGE_CONNECTION_STRING" && "$var" != "AZURE_STORAGE_CONTAINER_NAME" ]]; then
            echo -e "  ${YELLOW}⚠️  $var${NC} (Potential mismatch - check if this should be AZURE_STORAGE_*)"
            ((validation_errors++))
        else
            echo -e "  ${GREEN}✅ $var${NC} (Correct naming)"
        fi
    done
fi

# Check for ConnectionStrings pattern vs direct environment variables
BICEP_CONN_STRINGS=$(grep -E "ConnectionStrings__" infrastructure/simple-deploy.bicep || true)
if [[ -n "$BICEP_CONN_STRINGS" ]]; then
    echo ""
    echo -e "${BLUE}ℹ️  Found ConnectionStrings__ pattern in Bicep (this is correct for ASP.NET Core):${NC}"
    echo "$BICEP_CONN_STRINGS" | while read line; do
        echo "    $line"
    done
fi

# Check for alternative/legacy variable naming patterns
echo ""
echo -e "${BLUE}🔍 Checking for alternative naming patterns...${NC}"

# Look for variations of Azure Storage naming
AZURE_VARIATIONS=$(grep -E "^\s*name:\s*'[A-Za-z_]*[Aa]zure[A-Za-z_]*'" infrastructure/simple-deploy.bicep | sed "s/.*name: '//" | sed "s/'.*$//" || true)
if [[ -n "$AZURE_VARIATIONS" ]]; then
    echo -e "${BLUE}🔍 Azure-related variables in Bicep:${NC}"
    echo "$AZURE_VARIATIONS" | while read var; do
        if [[ "$var" != "AZURE_STORAGE_CONNECTION_STRING" && "$var" != "AZURE_STORAGE_CONTAINER_NAME" ]]; then
            echo -e "  ${YELLOW}⚠️  $var${NC} (Check if this follows standard naming convention)"
            ((validation_warnings++))
        else
            echo -e "  ${GREEN}✅ $var${NC} (Standard naming)"
        fi
    done
fi

# Final validation summary
echo ""
echo "=================================================="
echo -e "${BLUE}📊 Infrastructure Validation Summary${NC}"
echo "=================================================="

if [[ $validation_errors -eq 0 ]]; then
    echo -e "${GREEN}✅ All critical environment variables are properly aligned between infrastructure and application${NC}"
    echo -e "${GREEN}✅ No critical naming mismatches detected${NC}"
    
    if [[ $validation_warnings -gt 0 ]]; then
        echo -e "${YELLOW}⚠️  Found $validation_warnings warnings (non-critical)${NC}"
        echo -e "${YELLOW}✅ Infrastructure configuration validation PASSED with warnings${NC}"
    else
        echo -e "${GREEN}✅ Infrastructure configuration validation PASSED${NC}"
    fi
    
    echo ""
    echo -e "${GREEN}🚀 Deployment can proceed safely${NC}"
    exit 0
else
    echo -e "${RED}❌ Found $validation_errors critical error(s)${NC}"
    if [[ $validation_warnings -gt 0 ]]; then
        echo -e "${YELLOW}⚠️  Found $validation_warnings warning(s)${NC}"
    fi
    echo -e "${RED}❌ Infrastructure and application environment variable configuration MISMATCH detected${NC}"
    echo ""
    echo -e "${YELLOW}🛠️  To resolve these issues:${NC}"
    echo "  1. Check infrastructure/simple-deploy.bicep for environment variable names"
    echo "  2. Compare against constants in AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs"
    echo "  3. Ensure exact name matching (case-sensitive)"
    echo "  4. Common mistake: AzureStorage__ConnectionString vs AZURE_STORAGE_CONNECTION_STRING"
    echo "  5. Run this script with --verbose for detailed debugging"
    echo ""
    echo -e "${CYAN}💡 For comprehensive drift detection and monitoring, run:${NC}"
    echo "     ./scripts/detect-config-drift.sh Production"
    echo ""
    echo -e "${RED}🚫 DEPLOYMENT BLOCKED - Fix environment variable mismatches before deploying${NC}"
    exit 1
fi