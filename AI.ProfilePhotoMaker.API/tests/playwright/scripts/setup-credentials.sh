#!/bin/bash

# Azure Storage Credentials Setup Script
# Automates the process of configuring Azure Storage credentials for testing

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
STORAGE_ACCOUNT="aipmstv16j74jubocuukg"
CONTAINER_NAME="style-previews"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

echo -e "${GREEN}🔧 Azure Storage Credentials Setup${NC}"
echo "=================================="
echo "Storage Account: $STORAGE_ACCOUNT"
echo "Container: $CONTAINER_NAME"
echo ""

# Function to validate Azure CLI
check_azure_cli() {
    if ! command -v az &> /dev/null; then
        echo -e "${RED}❌ Azure CLI is not installed${NC}"
        echo "Please install Azure CLI: https://docs.microsoft.com/cli/azure/install-azure-cli"
        exit 1
    fi
    
    echo -e "${GREEN}✅ Azure CLI found${NC}"
}

# Function to check Azure login
check_azure_login() {
    if ! az account show &> /dev/null; then
        echo -e "${YELLOW}⚠️  Not logged in to Azure${NC}"
        echo "Logging in..."
        az login
    fi
    
    local account_name=$(az account show --query "name" -o tsv)
    echo -e "${GREEN}✅ Logged in to Azure: $account_name${NC}"
}

# Function to get storage account key
get_storage_key() {
    echo -e "${YELLOW}🔍 Retrieving storage account key...${NC}"
    
    # Try to get the resource group first
    local resource_group=$(az storage account list --query "[?name=='$STORAGE_ACCOUNT'].resourceGroup" -o tsv)
    
    if [ -z "$resource_group" ]; then
        echo -e "${RED}❌ Storage account '$STORAGE_ACCOUNT' not found in your subscription${NC}"
        echo "Please ensure you have access to the storage account"
        exit 1
    fi
    
    echo -e "${GREEN}✅ Found storage account in resource group: $resource_group${NC}"
    
    # Get the account key
    local account_key=$(az storage account keys list \
        --account-name "$STORAGE_ACCOUNT" \
        --resource-group "$resource_group" \
        --query "[0].value" -o tsv)
    
    if [ -z "$account_key" ]; then
        echo -e "${RED}❌ Failed to retrieve storage account key${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}✅ Retrieved storage account key${NC}"
    echo "$account_key"
}

# Function to generate SAS token
generate_sas_token() {
    echo -e "${YELLOW}🔑 Generating SAS token...${NC}"
    
    local resource_group=$(az storage account list --query "[?name=='$STORAGE_ACCOUNT'].resourceGroup" -o tsv)
    local expiry_date=$(date -d "+1 year" '+%Y-%m-%dT%H:%M:%SZ')
    
    local sas_token=$(az storage container generate-sas \
        --account-name "$STORAGE_ACCOUNT" \
        --resource-group "$resource_group" \
        --name "$CONTAINER_NAME" \
        --permissions rl \
        --expiry "$expiry_date" \
        --https-only \
        -o tsv)
    
    if [ -z "$sas_token" ]; then
        echo -e "${RED}❌ Failed to generate SAS token${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}✅ Generated SAS token (expires: $expiry_date)${NC}"
    echo "$sas_token"
}

# Function to create .env file
create_env_file() {
    local account_key="$1"
    local sas_token="$2"
    
    local env_file="$ROOT_DIR/.env"
    
    echo -e "${YELLOW}📝 Creating .env file...${NC}"
    
    cat > "$env_file" << EOF
# Azure Storage Configuration for Playwright Tests
# Generated on $(date)

# Primary connection string (full access)
AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=$STORAGE_ACCOUNT;AccountKey=$account_key;EndpointSuffix=core.windows.net"

# SAS token URL (read-only access)
AZURE_STORAGE_SAS_URL="https://$STORAGE_ACCOUNT.blob.core.windows.net/$CONTAINER_NAME?$sas_token"

# Test Configuration
FRONTEND_APP_URL="https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io"
AZURE_BLOB_BASE_URL="https://$STORAGE_ACCOUNT.blob.core.windows.net/$CONTAINER_NAME"

# Test Control Flags
RUN_UPLOAD_TESTS="true"
RUN_PERFORMANCE_TESTS="true"
ENABLE_VISUAL_TESTING="true"
TEST_TIMEOUT_MS="30000"

# Debugging
DEBUG_TESTS="false"
VERBOSE_LOGGING="false"
EOF

    chmod 600 "$env_file"
    echo -e "${GREEN}✅ Created .env file: $env_file${NC}"
}

# Function to test connection
test_connection() {
    echo -e "${YELLOW}🧪 Testing storage connection...${NC}"
    
    local resource_group=$(az storage account list --query "[?name=='$STORAGE_ACCOUNT'].resourceGroup" -o tsv)
    
    # Test listing blobs
    if az storage blob list \
        --account-name "$STORAGE_ACCOUNT" \
        --container-name "$CONTAINER_NAME" \
        --auth-mode key \
        --output table &> /dev/null; then
        echo -e "${GREEN}✅ Storage connection test passed${NC}"
    else
        echo -e "${YELLOW}⚠️  Storage connection test failed (container might be empty)${NC}"
    fi
}

# Function to show usage
show_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --key-only    Only retrieve account key"
    echo "  --sas-only    Only generate SAS token"
    echo "  --test-only   Only test connection"
    echo "  --help        Show this help"
}

# Main execution
main() {
    case "${1:-}" in
        --help)
            show_usage
            exit 0
            ;;
        --key-only)
            check_azure_cli
            check_azure_login
            get_storage_key
            ;;
        --sas-only)
            check_azure_cli
            check_azure_login
            generate_sas_token
            ;;
        --test-only)
            check_azure_cli
            check_azure_login
            test_connection
            ;;
        *)
            # Full setup
            check_azure_cli
            check_azure_login
            
            echo ""
            local account_key=$(get_storage_key)
            local sas_token=$(generate_sas_token)
            
            echo ""
            create_env_file "$account_key" "$sas_token"
            
            echo ""
            test_connection
            
            echo ""
            echo -e "${GREEN}🎉 Setup complete!${NC}"
            echo ""
            echo "Next steps:"
            echo "1. Review the generated .env file"
            echo "2. Run: npm install (in tests/playwright directory)"
            echo "3. Run: npm run test"
            ;;
    esac
}

main "$@"