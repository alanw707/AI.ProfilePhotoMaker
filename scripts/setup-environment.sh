#!/bin/bash

# =================================================================
# AI Profile Photo Maker - Environment Setup Script
# =================================================================
# This script helps set up environment variables for different environments
# Usage: ./setup-environment.sh [development|test|production]
# =================================================================

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default environment
ENVIRONMENT=${1:-development}

echo -e "${BLUE}🚀 Setting up environment: ${ENVIRONMENT}${NC}"
echo ""

# Validate environment parameter
case $ENVIRONMENT in
    development|dev)
        ENVIRONMENT="development"
        TEMPLATE_FILE=".env.development.template"
        ENV_FILE=".env"
        ;;
    test|testing)
        ENVIRONMENT="test"
        TEMPLATE_FILE=".env.test.template"
        ENV_FILE=".env.test"
        ;;
    production|prod)
        ENVIRONMENT="production"
        TEMPLATE_FILE=".env.production.template"
        ENV_FILE=".env.production"
        ;;
    *)
        echo -e "${RED}❌ Invalid environment: $ENVIRONMENT${NC}"
        echo "Valid options: development, test, production"
        exit 1
        ;;
esac

# Check if template exists
if [ ! -f "$TEMPLATE_FILE" ]; then
    echo -e "${RED}❌ Template file not found: $TEMPLATE_FILE${NC}"
    exit 1
fi

# Check if environment file already exists
if [ -f "$ENV_FILE" ]; then
    echo -e "${YELLOW}⚠️  Environment file already exists: $ENV_FILE${NC}"
    read -p "Do you want to overwrite it? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Cancelled."
        exit 0
    fi
fi

# Copy template to environment file
echo -e "${BLUE}📋 Copying template to $ENV_FILE...${NC}"
cp "$TEMPLATE_FILE" "$ENV_FILE"

echo -e "${GREEN}✅ Environment file created successfully!${NC}"
echo ""

# Helper functions for secret generation
generate_jwt_secret() {
    if command -v openssl &> /dev/null; then
        openssl rand -base64 64 | tr -d '\n'
    else
        echo "JWT_SECRET_PLEASE_GENERATE_64_CHAR_SECRET"
    fi
}

generate_webhook_secret() {
    if command -v openssl &> /dev/null; then
        openssl rand -hex 32 | tr -d '\n'
    else
        echo "WEBHOOK_SECRET_PLEASE_GENERATE_32_HEX_SECRET"
    fi
}

generate_password() {
    if command -v openssl &> /dev/null; then
        # Generate a 16-character password with mixed case, numbers, and special chars
        openssl rand -base64 24 | tr -d "=+/" | cut -c1-16
    else
        echo "GeneratedP@ssw0rd123!"
    fi
}

# Interactive setup
echo -e "${YELLOW}🔧 Interactive Setup${NC}"
echo "You can manually edit $ENV_FILE later, or provide values now:"
echo ""

# Required variables setup
echo -e "${BLUE}Required Variables:${NC}"
echo ""

# Database Password
echo "1. Database Password (MSSQL_SA_PASSWORD)"
echo "   Requirements: 8+ characters, mixed case, numbers, special characters"
echo "   (Special characters like # are supported; the generated .env entry will be quoted automatically.)"
if [ "$ENVIRONMENT" = "development" ]; then
    suggested_password="Dev@$(generate_password)"
elif [ "$ENVIRONMENT" = "test" ]; then
    suggested_password="Test@$(generate_password)"
else
    suggested_password="Prod@$(generate_password)!"
fi
echo "   Suggested: $suggested_password"
read -p "   Enter password (or press Enter to use suggested): " db_password
if [ -z "$db_password" ]; then
    db_password="$suggested_password"
fi

# JWT Secret
echo ""
echo "2. JWT Secret (JWT_SECRET)"
echo "   Requirements: 32+ characters, cryptographically secure"
jwt_secret=$(generate_jwt_secret)
echo "   Generated: ${jwt_secret:0:20}... (truncated for display)"
read -p "   Use generated secret? (Y/n): " -n 1 -r
echo
if [[ $REPLY =~ ^[Nn]$ ]]; then
    read -p "   Enter custom JWT secret: " jwt_secret
fi

# Replicate API Token
echo ""
echo "3. Replicate API Token (REPLICATE_API_TOKEN)"
echo "   Get from: https://replicate.com/account/api-tokens"
echo "   Format: r8_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
read -p "   Enter Replicate API token: " replicate_token

# Webhook Secret
echo ""
echo "4. Webhook Secret (REPLICATE_WEBHOOK_SECRET)"
webhook_secret=$(generate_webhook_secret)
echo "   Generated: ${webhook_secret:0:20}... (truncated for display)"
read -p "   Use generated secret? (Y/n): " -n 1 -r
echo
if [[ $REPLY =~ ^[Nn]$ ]]; then
    read -p "   Enter custom webhook secret: " webhook_secret
fi

# Optional variables
echo ""
echo -e "${BLUE}Optional Variables (press Enter to skip):${NC}"

# Google OAuth
echo ""
echo "5. Google OAuth (optional)"
read -p "   Google Client ID: " google_client_id
if [ ! -z "$google_client_id" ]; then
    read -p "   Google Client Secret: " google_client_secret
else
    google_client_secret=""
fi

# Stripe (only for test/production)
stripe_publishable=""
stripe_secret=""
stripe_webhook=""
if [ "$ENVIRONMENT" != "development" ]; then
    echo ""
    echo "6. Stripe Payment (optional)"
    read -p "   Stripe Publishable Key: " stripe_publishable
    if [ ! -z "$stripe_publishable" ]; then
        read -p "   Stripe Secret Key: " stripe_secret
        read -p "   Stripe Webhook Secret: " stripe_webhook
    fi
fi

# Azure Storage
echo ""
echo "7. Azure Storage (optional, defaults to local storage)"
read -p "   Azure Storage Connection String: " azure_storage

# Apply values to environment file
echo ""
echo -e "${BLUE}📝 Updating environment file...${NC}"

# Use sed to replace placeholder values (cross-platform compatible)
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    SED_ARGS=(-i '')
else
    # Linux
    SED_ARGS=(-i)
fi

# Replace required variables
db_password_for_sed=${db_password//&/\\&}
db_password_for_sed=${db_password_for_sed//|/\\|}
sed "${SED_ARGS[@]}" "s|MSSQL_SA_PASSWORD=.*|MSSQL_SA_PASSWORD=\"$db_password_for_sed\"|g" "$ENV_FILE"
sed "${SED_ARGS[@]}" "s|JWT_SECRET=.*|JWT_SECRET=$jwt_secret|g" "$ENV_FILE"
sed "${SED_ARGS[@]}" "s|REPLICATE_API_TOKEN=.*|REPLICATE_API_TOKEN=$replicate_token|g" "$ENV_FILE"
sed "${SED_ARGS[@]}" "s|REPLICATE_WEBHOOK_SECRET=.*|REPLICATE_WEBHOOK_SECRET=$webhook_secret|g" "$ENV_FILE"

# Replace optional variables if provided
if [ ! -z "$google_client_id" ]; then
    sed "${SED_ARGS[@]}" "s|# GOOGLE_CLIENT_ID=.*|GOOGLE_CLIENT_ID=$google_client_id|g" "$ENV_FILE"
    sed "${SED_ARGS[@]}" "s|GOOGLE_CLIENT_ID=your_google_client_id.*|GOOGLE_CLIENT_ID=$google_client_id|g" "$ENV_FILE"
fi
if [ ! -z "$google_client_secret" ]; then
    sed "${SED_ARGS[@]}" "s|# GOOGLE_CLIENT_SECRET=.*|GOOGLE_CLIENT_SECRET=$google_client_secret|g" "$ENV_FILE"
    sed "${SED_ARGS[@]}" "s|GOOGLE_CLIENT_SECRET=your_google_client_secret.*|GOOGLE_CLIENT_SECRET=$google_client_secret|g" "$ENV_FILE"
fi

if [ ! -z "$stripe_publishable" ]; then
    sed "${SED_ARGS[@]}" "s|# STRIPE_PUBLISHABLE_KEY=.*|STRIPE_PUBLISHABLE_KEY=$stripe_publishable|g" "$ENV_FILE"
    sed "${SED_ARGS[@]}" "s|STRIPE_PUBLISHABLE_KEY=pk_.*|STRIPE_PUBLISHABLE_KEY=$stripe_publishable|g" "$ENV_FILE"
fi
if [ ! -z "$stripe_secret" ]; then
    sed "${SED_ARGS[@]}" "s|# STRIPE_SECRET_KEY=.*|STRIPE_SECRET_KEY=$stripe_secret|g" "$ENV_FILE"
    sed "${SED_ARGS[@]}" "s|STRIPE_SECRET_KEY=sk_.*|STRIPE_SECRET_KEY=$stripe_secret|g" "$ENV_FILE"
fi
if [ ! -z "$stripe_webhook" ]; then
    sed "${SED_ARGS[@]}" "s|# STRIPE_WEBHOOK_SECRET=.*|STRIPE_WEBHOOK_SECRET=$stripe_webhook|g" "$ENV_FILE"
    sed "${SED_ARGS[@]}" "s|STRIPE_WEBHOOK_SECRET=whsec_.*|STRIPE_WEBHOOK_SECRET=$stripe_webhook|g" "$ENV_FILE"
fi

if [ ! -z "$azure_storage" ]; then
    sed "${SED_ARGS[@]}" "s|# AZURE_STORAGE_CONNECTION_STRING=.*|AZURE_STORAGE_CONNECTION_STRING=$azure_storage|g" "$ENV_FILE"
    sed "${SED_ARGS[@]}" "s|AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol.*|AZURE_STORAGE_CONNECTION_STRING=$azure_storage|g" "$ENV_FILE"
fi

echo -e "${GREEN}✅ Environment file configured successfully!${NC}"
echo ""

# Validation
echo -e "${BLUE}🔍 Validating configuration...${NC}"

# Check required variables are set
missing_vars=()

if grep -q "YourSecurePassword\|REPLACE_WITH\|your_.*_token" "$ENV_FILE"; then
    missing_vars+=("Some variables still contain placeholder values")
fi

if [ ${#missing_vars[@]} -eq 0 ]; then
    echo -e "${GREEN}✅ Configuration validation passed!${NC}"
else
    echo -e "${YELLOW}⚠️  Configuration warnings:${NC}"
    for var in "${missing_vars[@]}"; do
        echo -e "${YELLOW}   - $var${NC}"
    done
    echo ""
    echo -e "${YELLOW}Please review and update $ENV_FILE manually if needed.${NC}"
fi

# Next steps
echo ""
echo -e "${BLUE}🎯 Next Steps:${NC}"
echo "1. Review the generated file: $ENV_FILE"
echo "2. Start the application:"
if [ "$ENVIRONMENT" = "development" ]; then
    echo "   dotnet run --project AI.ProfilePhotoMaker.API"
    echo "   OR"
    echo "   docker-compose up"
elif [ "$ENVIRONMENT" = "test" ]; then
    echo "   ASPNETCORE_ENVIRONMENT=Test dotnet run --project AI.ProfilePhotoMaker.API"
else
    echo "   Deploy to Azure App Service with environment variables"
fi
echo "3. Check application logs for any validation errors"
echo ""

# Security reminders
echo -e "${RED}🔒 Security Reminders:${NC}"
echo "- Never commit $ENV_FILE to version control"
echo "- Store production secrets in Azure Key Vault"
echo "- Rotate secrets every 90 days"
echo "- Use different secrets for each environment"
echo "- Monitor secret access and usage"

echo ""
echo -e "${GREEN}🎉 Environment setup complete!${NC}"
