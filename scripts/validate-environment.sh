#!/bin/bash

# =================================================================
# Environment Variables Validation Script
# =================================================================
# This script validates environment variable configuration
# Usage: ./validate-environment.sh [environment-file]
# =================================================================

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Environment file to validate
ENV_FILE=${1:-.env}

echo -e "${BLUE}🔍 Validating Environment Configuration${NC}"
echo -e "${BLUE}Environment file: $ENV_FILE${NC}"
echo ""

# Check if environment file exists
if [ ! -f "$ENV_FILE" ]; then
    echo -e "${RED}❌ Environment file not found: $ENV_FILE${NC}"
    echo "Available templates:"
    ls -la .env*.template 2>/dev/null || echo "No templates found"
    exit 1
fi

# Load environment variables from file
set -a  # automatically export all variables
source "$ENV_FILE" 2>/dev/null || {
    echo -e "${RED}❌ Failed to load environment file: $ENV_FILE${NC}"
    echo "Please check the file format and syntax"
    exit 1
}
set +a

# Validation counters
ERRORS=0
WARNINGS=0
SUCCESS=0

# Helper functions
validate_required() {
    local var_name=$1
    local var_value=$2
    local description=$3
    
    if [ -z "$var_value" ]; then
        echo -e "${RED}❌ MISSING: $var_name - $description${NC}"
        ((ERRORS++))
        return 1
    else
        echo -e "${GREEN}✅ PRESENT: $var_name${NC}"
        ((SUCCESS++))
        return 0
    fi
}

validate_optional() {
    local var_name=$1
    local var_value=$2
    local description=$3
    
    if [ -z "$var_value" ]; then
        echo -e "${YELLOW}⚠️  OPTIONAL: $var_name - $description (not configured)${NC}"
        ((WARNINGS++))
    else
        echo -e "${GREEN}✅ PRESENT: $var_name${NC}"
        ((SUCCESS++))
    fi
}

validate_format() {
    local var_name=$1
    local var_value=$2
    local pattern=$3
    local description=$4
    
    if [ -z "$var_value" ]; then
        return 0  # Skip format validation if empty
    fi
    
    if [[ $var_value =~ $pattern ]]; then
        echo -e "${GREEN}✅ FORMAT: $var_name - valid format${NC}"
        return 0
    else
        echo -e "${RED}❌ FORMAT: $var_name - $description${NC}"
        echo -e "   Value: ${var_value:0:20}... (truncated)"
        ((ERRORS++))
        return 1
    fi
}

validate_length() {
    local var_name=$1
    local var_value=$2
    local min_length=$3
    local description=$4
    
    if [ -z "$var_value" ]; then
        return 0  # Skip length validation if empty
    fi
    
    if [ ${#var_value} -ge $min_length ]; then
        echo -e "${GREEN}✅ LENGTH: $var_name - ${#var_value} characters (>= $min_length)${NC}"
        return 0
    else
        echo -e "${RED}❌ LENGTH: $var_name - ${#var_value} characters (< $min_length) - $description${NC}"
        ((ERRORS++))
        return 1
    fi
}

validate_placeholder() {
    local var_name=$1
    local var_value=$2
    local placeholders=$3
    
    if [ -z "$var_value" ]; then
        return 0  # Skip placeholder validation if empty
    fi
    
    for placeholder in $placeholders; do
        if [[ "$var_value" == *"$placeholder"* ]]; then
            echo -e "${YELLOW}⚠️  PLACEHOLDER: $var_name contains placeholder '$placeholder'${NC}"
            ((WARNINGS++))
            return 1
        fi
    done
    
    return 0
}

echo -e "${BLUE}=== REQUIRED VARIABLES ===${NC}"
echo ""

# Database Configuration
echo -e "${BLUE}Database Configuration:${NC}"
validate_required "MSSQL_SA_PASSWORD" "$MSSQL_SA_PASSWORD" "Database SA password"
if [ ! -z "$MSSQL_SA_PASSWORD" ]; then
    validate_length "MSSQL_SA_PASSWORD" "$MSSQL_SA_PASSWORD" 8 "Minimum 8 characters required"
    validate_format "MSSQL_SA_PASSWORD" "$MSSQL_SA_PASSWORD" "^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^a-zA-Z0-9]).{8,}$" "Must contain uppercase, lowercase, number, and special character"
    validate_placeholder "MSSQL_SA_PASSWORD" "$MSSQL_SA_PASSWORD" "YourSecure REPLACE_WITH Password123"
fi
echo ""

# JWT Configuration
echo -e "${BLUE}JWT Configuration:${NC}"
validate_required "JWT_SECRET" "$JWT_SECRET" "JWT signing secret"
if [ ! -z "$JWT_SECRET" ]; then
    validate_length "JWT_SECRET" "$JWT_SECRET" 32 "Minimum 32 characters required"
    validate_placeholder "JWT_SECRET" "$JWT_SECRET" "YourSuperSecret REPLACE_WITH STORED_IN_USER_SECRETS"
fi
validate_optional "JWT_VALID_AUDIENCE" "$JWT_VALID_AUDIENCE" "JWT token audience"
validate_optional "JWT_VALID_ISSUER" "$JWT_VALID_ISSUER" "JWT token issuer"
echo ""

# AI/ML Services
echo -e "${BLUE}AI/ML Services:${NC}"
validate_required "REPLICATE_API_TOKEN" "$REPLICATE_API_TOKEN" "Replicate API access token"
if [ ! -z "$REPLICATE_API_TOKEN" ]; then
    validate_format "REPLICATE_API_TOKEN" "$REPLICATE_API_TOKEN" "^r8_" "Must start with 'r8_'"
    validate_placeholder "REPLICATE_API_TOKEN" "$REPLICATE_API_TOKEN" "your_actual_replicate_token REPLACE_WITH"
fi
validate_required "REPLICATE_WEBHOOK_SECRET" "$REPLICATE_WEBHOOK_SECRET" "Webhook validation secret"
if [ ! -z "$REPLICATE_WEBHOOK_SECRET" ]; then
    validate_length "REPLICATE_WEBHOOK_SECRET" "$REPLICATE_WEBHOOK_SECRET" 32 "Minimum 32 characters recommended"
    validate_placeholder "REPLICATE_WEBHOOK_SECRET" "$REPLICATE_WEBHOOK_SECRET" "your_webhook_secret REPLACE_WITH"
fi
echo ""

echo -e "${BLUE}=== OPTIONAL VARIABLES ===${NC}"
echo ""

# Environment Configuration
echo -e "${BLUE}Environment Configuration:${NC}"
validate_optional "ASPNETCORE_ENVIRONMENT" "$ASPNETCORE_ENVIRONMENT" "Application environment"
validate_optional "APP_BASE_URL" "$APP_BASE_URL" "Application base URL"
echo ""

# Google OAuth
echo -e "${BLUE}Google OAuth:${NC}"
validate_optional "GOOGLE_CLIENT_ID" "$GOOGLE_CLIENT_ID" "Google OAuth client ID"
validate_optional "GOOGLE_CLIENT_SECRET" "$GOOGLE_CLIENT_SECRET" "Google OAuth client secret"
if [ ! -z "$GOOGLE_CLIENT_ID" ] && [ -z "$GOOGLE_CLIENT_SECRET" ]; then
    echo -e "${YELLOW}⚠️  INCOMPLETE: Google OAuth requires both client ID and secret${NC}"
    ((WARNINGS++))
elif [ -z "$GOOGLE_CLIENT_ID" ] && [ ! -z "$GOOGLE_CLIENT_SECRET" ]; then
    echo -e "${YELLOW}⚠️  INCOMPLETE: Google OAuth requires both client ID and secret${NC}"
    ((WARNINGS++))
fi
echo ""

# Stripe Payment
echo -e "${BLUE}Stripe Payment:${NC}"
validate_optional "STRIPE_PUBLISHABLE_KEY" "$STRIPE_PUBLISHABLE_KEY" "Stripe publishable key"
validate_optional "STRIPE_SECRET_KEY" "$STRIPE_SECRET_KEY" "Stripe secret key"
validate_optional "STRIPE_WEBHOOK_SECRET" "$STRIPE_WEBHOOK_SECRET" "Stripe webhook secret"
if [ ! -z "$STRIPE_PUBLISHABLE_KEY" ]; then
    validate_format "STRIPE_PUBLISHABLE_KEY" "$STRIPE_PUBLISHABLE_KEY" "^pk_" "Must start with 'pk_'"
fi
if [ ! -z "$STRIPE_SECRET_KEY" ]; then
    validate_format "STRIPE_SECRET_KEY" "$STRIPE_SECRET_KEY" "^sk_" "Must start with 'sk_'"
fi
if [ ! -z "$STRIPE_WEBHOOK_SECRET" ]; then
    validate_format "STRIPE_WEBHOOK_SECRET" "$STRIPE_WEBHOOK_SECRET" "^whsec_" "Must start with 'whsec_'"
fi
echo ""

# Azure Storage
echo -e "${BLUE}Azure Storage:${NC}"
validate_optional "AZURE_STORAGE_CONNECTION_STRING" "$AZURE_STORAGE_CONNECTION_STRING" "Azure Blob Storage connection"
validate_optional "AZURE_STORAGE_CONTAINER_NAME" "$AZURE_STORAGE_CONTAINER_NAME" "Storage container name"
if [ ! -z "$AZURE_STORAGE_CONNECTION_STRING" ]; then
    validate_format "AZURE_STORAGE_CONNECTION_STRING" "$AZURE_STORAGE_CONNECTION_STRING" "DefaultEndpointsProtocol" "Must be valid Azure Storage connection string"
fi
echo ""

echo -e "${BLUE}=== CONFIGURATION VALIDATION ===${NC}"
echo ""

# Database Configuration
echo -e "${BLUE}Database Settings:${NC}"
validate_optional "DB_MAX_RETRY_COUNT" "$DB_MAX_RETRY_COUNT" "Database retry count"
validate_optional "DB_MAX_RETRY_DELAY_SECONDS" "$DB_MAX_RETRY_DELAY_SECONDS" "Database retry delay"
validate_optional "DB_COMMAND_TIMEOUT_SECONDS" "$DB_COMMAND_TIMEOUT_SECONDS" "Database command timeout"
echo ""

# Logging Configuration
echo -e "${BLUE}Logging Settings:${NC}"
validate_optional "LOG_LEVEL_DEFAULT" "$LOG_LEVEL_DEFAULT" "Default log level"
validate_optional "LOG_LEVEL_ASPNETCORE" "$LOG_LEVEL_ASPNETCORE" "ASP.NET Core log level"
validate_optional "LOG_LEVEL_DATABASE" "$LOG_LEVEL_DATABASE" "Database log level"
echo ""

# Security Configuration
echo -e "${BLUE}Security Settings:${NC}"
validate_optional "ENABLE_HTTPS_REDIRECT" "$ENABLE_HTTPS_REDIRECT" "HTTPS redirect enabled"
validate_optional "REQUIRE_HTTPS_METADATA" "$REQUIRE_HTTPS_METADATA" "Require HTTPS metadata"
validate_optional "CORS_ALLOWED_ORIGINS" "$CORS_ALLOWED_ORIGINS" "CORS allowed origins"
echo ""

# Performance Configuration
echo -e "${BLUE}Performance Settings:${NC}"
validate_optional "ENABLE_RESPONSE_COMPRESSION" "$ENABLE_RESPONSE_COMPRESSION" "Response compression enabled"
validate_optional "ENABLE_REQUEST_LOGGING" "$ENABLE_REQUEST_LOGGING" "Request logging enabled"
echo ""

# Health Check Configuration
echo -e "${BLUE}Health Check Settings:${NC}"
validate_optional "HEALTH_CHECK_TIMEOUT_SECONDS" "$HEALTH_CHECK_TIMEOUT_SECONDS" "Health check timeout"
validate_optional "HEALTH_CHECK_INTERVAL_SECONDS" "$HEALTH_CHECK_INTERVAL_SECONDS" "Health check interval"
echo ""

# Summary
echo -e "${BLUE}=== VALIDATION SUMMARY ===${NC}"
echo ""

if [ $ERRORS -eq 0 ]; then
    echo -e "${GREEN}🎉 Configuration validation completed successfully!${NC}"
    echo -e "${GREEN}✅ Success: $SUCCESS variables${NC}"
    if [ $WARNINGS -gt 0 ]; then
        echo -e "${YELLOW}⚠️  Warnings: $WARNINGS optional/placeholder variables${NC}"
    fi
    echo ""
    echo -e "${GREEN}Your environment is ready to start the application!${NC}"
    exit_code=0
else
    echo -e "${RED}❌ Configuration validation failed!${NC}"
    echo -e "${RED}❌ Errors: $ERRORS critical issues${NC}"
    if [ $WARNINGS -gt 0 ]; then
        echo -e "${YELLOW}⚠️  Warnings: $WARNINGS optional/placeholder variables${NC}"
    fi
    echo -e "${GREEN}✅ Success: $SUCCESS variables${NC}"
    echo ""
    echo -e "${RED}Please fix the errors before starting the application.${NC}"
    exit_code=1
fi

echo ""
echo -e "${BLUE}=== NEXT STEPS ===${NC}"
if [ $ERRORS -eq 0 ]; then
    echo "1. Start the application:"
    echo "   dotnet run --project AI.ProfilePhotoMaker.API"
    echo "   OR"
    echo "   docker-compose up"
    echo ""
    echo "2. Check application logs for runtime validation"
    echo "3. Test the application endpoints"
else
    echo "1. Fix the validation errors listed above"
    echo "2. Review the .env.example file for correct format"
    echo "3. Run this validation script again"
    echo "4. Consider using the setup script: ./scripts/setup-environment.sh"
fi

echo ""
echo -e "${BLUE}=== SECURITY REMINDERS ===${NC}"
echo "- Keep your .env file secure and never commit it to version control"
echo "- Use strong, unique secrets for each environment"
echo "- Rotate secrets regularly (every 90 days)"
echo "- Use Azure Key Vault for production secrets"
echo "- Monitor secret access and usage"

exit $exit_code