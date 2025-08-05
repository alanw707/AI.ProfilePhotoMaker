#!/bin/bash
# Upload Style Preview Images to Azure Blob Storage
# This script uploads all style preview images using Azure CLI

set -e

# Configuration
CONTAINER_NAME="${AZURE_CONTAINER_NAME:-profile-images-staging}"
PREVIEWS_PATH="${PREVIEWS_PATH:-../style-previews}"
DRY_RUN="${DRY_RUN:-false}"
FORCE="${FORCE:-false}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}🚀 Azure Blob Storage Upload Script for Style Previews${NC}"
echo "=================================================="

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo -e "${RED}❌ Azure CLI is not installed. Please install it first.${NC}"
    echo "   Ubuntu/Debian: curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash"
    echo "   Windows: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check if logged in to Azure
if ! az account show &> /dev/null; then
    echo -e "${RED}❌ Not logged in to Azure. Please run 'az login' first.${NC}"
    exit 1
fi

# Validate storage account connection
if [ -z "$AZURE_STORAGE_CONNECTION_STRING" ] && [ -z "$AZURE_STORAGE_ACCOUNT" ]; then
    echo -e "${RED}❌ Azure Storage configuration is missing.${NC}"
    echo "   Set either AZURE_STORAGE_CONNECTION_STRING or AZURE_STORAGE_ACCOUNT environment variable."
    echo ""
    echo "Examples:"
    echo "   export AZURE_STORAGE_CONNECTION_STRING='DefaultEndpointsProtocol=https;...'"
    echo "   export AZURE_STORAGE_ACCOUNT='yourstorageaccount'"
    exit 1
fi

# Check if previews directory exists
if [ ! -d "$PREVIEWS_PATH" ]; then
    echo -e "${RED}❌ Style previews directory not found: $PREVIEWS_PATH${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Prerequisites check passed${NC}"

# Create container if it doesn't exist
echo -e "${BLUE}📦 Ensuring container exists: $CONTAINER_NAME${NC}"
if [ "$DRY_RUN" = "true" ]; then
    echo -e "${YELLOW}🔍 DRY RUN: Would ensure container '$CONTAINER_NAME' exists${NC}"
else
    az storage container create --name "$CONTAINER_NAME" --public-access blob --only-show-errors || {
        echo -e "${YELLOW}⚠️  Container might already exist or creation failed${NC}"
    }
    echo -e "${GREEN}✅ Container '$CONTAINER_NAME' ready${NC}"
fi

# Find all .jpg files in the previews directory
echo -e "${BLUE}🔍 Scanning for style preview images...${NC}"
cd "$PREVIEWS_PATH"
image_files=(*.jpg)

# Filter out empty placeholder files
valid_files=()
for file in "${image_files[@]}"; do
    if [ -f "$file" ] && [ -s "$file" ]; then
        valid_files+=("$file")
    fi
done

if [ ${#valid_files[@]} -eq 0 ]; then
    echo -e "${YELLOW}⚠️  No valid .jpg files found in $PREVIEWS_PATH${NC}"
    exit 0
fi

echo -e "${CYAN}📋 Found ${#valid_files[@]} style preview images to upload${NC}"

# Upload statistics
uploaded=0
skipped=0
failed=0
total_size=0

# Upload each file
for file in "${valid_files[@]}"; do
    blob_name="style-previews/$file"
    file_size=$(stat -f%z "$file" 2>/dev/null || stat -c%s "$file" 2>/dev/null || echo "0")
    file_size_kb=$((file_size / 1024))
    
    # Check if blob already exists
    if [ "$FORCE" != "true" ]; then
        if az storage blob exists --container-name "$CONTAINER_NAME" --name "$blob_name" --query "exists" --output tsv 2>/dev/null | grep -q "true"; then
            echo -e "${YELLOW}⏭️  Skipping $file (already exists, use FORCE=true to overwrite)${NC}"
            ((skipped++))
            continue
        fi
    fi
    
    if [ "$DRY_RUN" = "true" ]; then
        echo -e "${YELLOW}🔍 DRY RUN: Would upload $file (${file_size_kb} KB) → $blob_name${NC}"
        ((uploaded++))
    else
        # Upload the file
        if az storage blob upload --container-name "$CONTAINER_NAME" --name "$blob_name" --file "$file" --content-type "image/jpeg" --overwrite --only-show-errors; then
            echo -e "${GREEN}✅ Uploaded $file (${file_size_kb} KB) → $blob_name${NC}"
            ((uploaded++))
            total_size=$((total_size + file_size))
        else
            echo -e "${RED}❌ Failed to upload $file${NC}"
            ((failed++))
        fi
    fi
done

# Display summary
echo ""
echo -e "${CYAN}📊 Upload Summary:${NC}"
echo "   Total files: ${#valid_files[@]}"
echo -e "   Uploaded: ${GREEN}$uploaded${NC}"
echo -e "   Skipped: ${YELLOW}$skipped${NC}"
echo -e "   Failed: ${RED}$failed${NC}"

if [ "$DRY_RUN" != "true" ] && [ $total_size -gt 0 ]; then
    total_size_mb=$((total_size / 1024 / 1024))
    echo -e "   Total uploaded: ${CYAN}${total_size_mb} MB${NC}"
fi

# Generate sample URLs
if [ $uploaded -gt 0 ] && [ "$DRY_RUN" != "true" ]; then
    echo ""
    echo -e "${CYAN}🔗 Sample URLs (for verification):${NC}"
    
    # Extract storage account name
    if [ -n "$AZURE_STORAGE_CONNECTION_STRING" ]; then
        account_name=$(echo "$AZURE_STORAGE_CONNECTION_STRING" | grep -o 'AccountName=[^;]*' | cut -d'=' -f2)
    else
        account_name="$AZURE_STORAGE_ACCOUNT"
    fi
    
    if [ -n "$account_name" ]; then
        base_url="https://${account_name}.blob.core.windows.net/$CONTAINER_NAME/style-previews"
        
        # Show first 3 files as examples
        count=0
        for file in "${valid_files[@]}"; do
            if [ $count -lt 3 ]; then
                echo "   $base_url/$file"
                ((count++))
            else
                break
            fi
        done
    fi
fi

# Test API endpoint
if [ "$DRY_RUN" != "true" ] && [ $uploaded -gt 0 ]; then
    echo ""
    echo -e "${CYAN}🔄 Testing API endpoint...${NC}"
    api_url="https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io/api/style-preview/list"
    
    if response=$(curl -s -X GET "$api_url" 2>/dev/null); then
        if echo "$response" | grep -q '"success":true'; then
            count=$(echo "$response" | grep -o '"count":[0-9]*' | cut -d':' -f2)
            echo -e "${GREEN}✅ API endpoint working! Found $count style previews${NC}"
        else
            echo -e "${YELLOW}⚠️  API endpoint returned unexpected response${NC}"
        fi
    else
        echo -e "${YELLOW}⚠️  Could not test API endpoint (this is normal if API is not running)${NC}"
    fi
fi

# Final status
if [ $failed -eq 0 ]; then
    echo ""
    echo -e "${GREEN}🎉 Upload completed successfully!${NC}"
    exit 0
else
    echo ""
    echo -e "${YELLOW}⚠️  Upload completed with $failed errors${NC}"
    exit 1
fi