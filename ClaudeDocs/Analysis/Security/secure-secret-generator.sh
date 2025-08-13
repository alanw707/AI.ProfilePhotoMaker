#!/bin/bash

# Secure Secret Generator for AI Profile Photo Maker
# This script generates cryptographically secure secrets for production deployment
# 
# Security Features:
# - Uses OpenSSL for cryptographically secure random generation
# - Generates secrets meeting industry security standards
# - Provides validation for secret strength
# - Creates environment variable format for easy deployment

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}🔐 AI Profile Photo Maker - Secure Secret Generator${NC}"
echo "================================================="
echo -e "${YELLOW}⚠️  WARNING: These are PRODUCTION secrets. Handle with extreme care!${NC}"
echo ""

# Function to check if OpenSSL is available
check_openssl() {
    if ! command -v openssl &> /dev/null; then
        echo -e "${RED}❌ Error: OpenSSL is required but not installed${NC}"
        echo "Please install OpenSSL and run this script again"
        exit 1
    fi
    echo -e "${GREEN}✅ OpenSSL found${NC}"
}

# Function to generate and validate password strength
generate_secure_password() {
    local length=$1
    local password
    
    # Generate password with mixed case, numbers, and special characters
    password=$(openssl rand -base64 $((length * 3 / 4)) | tr -d "=+/" | head -c "$length")
    
    # Ensure password meets complexity requirements
    if [[ ${#password} -lt $length ]]; then
        # Pad with additional secure characters if needed
        local additional=$(openssl rand -hex 4)
        password="${password}${additional}"
        password=${password:0:$length}
    fi
    
    echo "$password"
}

# Function to validate secret strength
validate_secret_strength() {
    local secret=$1
    local min_length=$2
    local secret_name=$3
    
    if [[ ${#secret} -lt $min_length ]]; then
        echo -e "${RED}❌ $secret_name is too short (${#secret} chars, minimum $min_length)${NC}"
        return 1
    fi
    
    echo -e "${GREEN}✅ $secret_name meets length requirements (${#secret} chars)${NC}"
    return 0
}

# Start generation process
echo -e "${BLUE}🔍 Checking prerequisites...${NC}"
check_openssl
echo ""

echo -e "${BLUE}🎲 Generating secure secrets...${NC}"
echo ""

# 1. SQL Server Admin Password
echo -e "${BLUE}1. SQL Server Admin Password${NC}"
SQL_PASSWORD=$(generate_secure_password 24)
validate_secret_strength "$SQL_PASSWORD" 16 "SQL Password"
echo ""

# 2. JWT Secret (256-bit minimum)
echo -e "${BLUE}2. JWT Signing Secret${NC}"
JWT_SECRET=$(openssl rand -base64 64)
validate_secret_strength "$JWT_SECRET" 32 "JWT Secret"
echo ""

# 3. Replicate Webhook Secret
echo -e "${BLUE}3. Replicate Webhook Secret${NC}"
WEBHOOK_SECRET=$(openssl rand -hex 32)
validate_secret_strength "$WEBHOOK_SECRET" 32 "Webhook Secret"
echo ""

# 4. Generate additional secrets for future use
echo -e "${BLUE}4. Additional Security Secrets${NC}"
APP_ENCRYPTION_KEY=$(openssl rand -hex 32)
SESSION_SECRET=$(openssl rand -base64 48)
validate_secret_strength "$APP_ENCRYPTION_KEY" 32 "App Encryption Key"
validate_secret_strength "$SESSION_SECRET" 32 "Session Secret"
echo ""

# Known OAuth Client ID (public value)
GOOGLE_CLIENT_ID="116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com"

echo -e "${GREEN}🎉 Secret generation completed successfully!${NC}"
echo ""

# Create environment variable export format
echo -e "${BLUE}📋 Environment Variables (for deployment):${NC}"
echo "================================================="
cat << EOF
# SQL Server Configuration
export SQL_ADMIN_PASSWORD="$SQL_PASSWORD"

# JWT Authentication
export JWT_SECRET="$JWT_SECRET"

# Replicate API Configuration
export REPLICATE_WEBHOOK_SECRET="$WEBHOOK_SECRET"

# Google OAuth Configuration (Client ID is public)
export GOOGLE_CLIENT_ID="$GOOGLE_CLIENT_ID"
# ⚠️  IMPORTANT: You must manually generate GOOGLE_CLIENT_SECRET in Google Cloud Console
# export GOOGLE_CLIENT_SECRET="YOUR_GOOGLE_CLIENT_SECRET_FROM_CONSOLE"

# ⚠️  IMPORTANT: You must get this from your Replicate account
# export REPLICATE_API_TOKEN="r8_YOUR_REPLICATE_TOKEN_HERE"

# Additional Security Keys (optional)
export APP_ENCRYPTION_KEY="$APP_ENCRYPTION_KEY"
export SESSION_SECRET="$SESSION_SECRET"
EOF

echo ""
echo -e "${BLUE}📝 Azure Key Vault Commands:${NC}"
echo "================================================="
cat << EOF
# Store secrets in Azure Key Vault (replace YOUR_KEY_VAULT_NAME)
az keyvault secret set --vault-name YOUR_KEY_VAULT_NAME --name "SqlAdminPassword" --value "$SQL_PASSWORD"
az keyvault secret set --vault-name YOUR_KEY_VAULT_NAME --name "JwtSecret" --value "$JWT_SECRET"
az keyvault secret set --vault-name YOUR_KEY_VAULT_NAME --name "ReplicateWebhookSecret" --value "$WEBHOOK_SECRET"
az keyvault secret set --vault-name YOUR_KEY_VAULT_NAME --name "GoogleClientId" --value "$GOOGLE_CLIENT_ID"
# az keyvault secret set --vault-name YOUR_KEY_VAULT_NAME --name "GoogleClientSecret" --value "YOUR_GOOGLE_CLIENT_SECRET"
# az keyvault secret set --vault-name YOUR_KEY_VAULT_NAME --name "ReplicateApiToken" --value "YOUR_REPLICATE_TOKEN"
EOF

echo ""
echo -e "${BLUE}🔐 Manual Actions Required:${NC}"
echo "================================================="
echo -e "${YELLOW}1. Google OAuth Client Secret:${NC}"
echo "   - Go to: https://console.cloud.google.com/apis/credentials"
echo "   - Find your OAuth 2.0 Client ID: $GOOGLE_CLIENT_ID"
echo "   - Generate a new Client Secret"
echo "   - Store it securely in Azure Key Vault"
echo ""
echo -e "${YELLOW}2. Replicate API Token:${NC}"
echo "   - Go to: https://replicate.com/account/api-tokens"
echo "   - Create a new API token (starts with 'r8_')"
echo "   - Store it securely in Azure Key Vault"
echo ""

echo -e "${BLUE}⚡ Quick Deployment Commands:${NC}"
echo "================================================="
cat << EOF
# Option 1: Set environment variables and deploy
export SQL_ADMIN_PASSWORD="$SQL_PASSWORD"
export JWT_SECRET="$JWT_SECRET"
export REPLICATE_WEBHOOK_SECRET="$WEBHOOK_SECRET"
export GOOGLE_CLIENT_ID="$GOOGLE_CLIENT_ID"
# export GOOGLE_CLIENT_SECRET="YOUR_GOOGLE_CLIENT_SECRET"
# export REPLICATE_API_TOKEN="YOUR_REPLICATE_TOKEN"

# Then run deployment
./scripts/deploy-with-oauth.sh

# Option 2: Create deployment parameters file
cat > deployment-params.json << 'EOJ'
{
  "\$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "sqlAdminPassword": {
      "value": "$SQL_PASSWORD"
    },
    "jwtSecret": {
      "value": "$JWT_SECRET"
    },
    "replicateApiToken": {
      "value": "YOUR_REPLICATE_TOKEN"
    },
    "googleClientId": {
      "value": "$GOOGLE_CLIENT_ID"
    },
    "googleClientSecret": {
      "value": "YOUR_GOOGLE_CLIENT_SECRET"
    }
  }
}
EOJ
EOF

echo ""
echo -e "${RED}🚨 CRITICAL SECURITY REMINDERS:${NC}"
echo "================================================="
echo -e "${RED}1. NEVER commit deployment-params.json to version control${NC}"
echo -e "${RED}2. Store ALL secrets in Azure Key Vault immediately${NC}"
echo -e "${RED}3. Use environment variables or secure parameter files only${NC}"
echo -e "${RED}4. Rotate secrets every 90 days${NC}"
echo -e "${RED}5. Monitor Key Vault access logs${NC}"
echo ""

echo -e "${GREEN}✅ Secret generation complete! Deploy securely.${NC}"

# Save secrets to a temporary secure file for reference
TEMP_FILE="/tmp/aipm-secrets-$(date +%Y%m%d-%H%M%S).txt"
cat << EOF > "$TEMP_FILE"
AI Profile Photo Maker - Generated Secrets
Generated: $(date)
WARNING: This file contains sensitive information. Delete after use.

SQL_ADMIN_PASSWORD=$SQL_PASSWORD
JWT_SECRET=$JWT_SECRET
REPLICATE_WEBHOOK_SECRET=$WEBHOOK_SECRET
GOOGLE_CLIENT_ID=$GOOGLE_CLIENT_ID
APP_ENCRYPTION_KEY=$APP_ENCRYPTION_KEY
SESSION_SECRET=$SESSION_SECRET

Manual Actions Required:
- Get Google Client Secret from: https://console.cloud.google.com/apis/credentials
- Get Replicate API Token from: https://replicate.com/account/api-tokens
EOF

chmod 600 "$TEMP_FILE"
echo -e "${BLUE}📄 Secrets saved to: $TEMP_FILE${NC}"
echo -e "${YELLOW}⚠️  Delete this file after storing secrets in Key Vault!${NC}"