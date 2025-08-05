#!/bin/bash

# Azure Staging Environment Cleanup
# Safely removes the legacy rg-aiprofilemaker-staging resource group

set -e

echo "🧹 Azure Staging Environment Cleanup"
echo "======================================"
echo ""

# Configuration
STAGING_RG="rg-aiprofilemaker-staging"
BACKUP_DIR="azure-cleanup-backup/$(date +%Y%m%d-%H%M%S)"

# Create backup directory
mkdir -p "$BACKUP_DIR"

echo "📋 Cleanup Configuration:"
echo "  Target Resource Group: $STAGING_RG"
echo "  Backup Directory: $BACKUP_DIR"
echo ""

# Safety checks
echo "🔍 Pre-cleanup Safety Checks..."

# Check if Azure CLI is available
if ! command -v az &> /dev/null; then
    echo "❌ Azure CLI not found. Please install Azure CLI first."
    echo "   Install: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check if logged in
if ! az account show &> /dev/null; then
    echo "❌ Not logged into Azure. Please run 'az login' first."
    exit 1
fi

echo "✅ Azure CLI available and authenticated"

# Check if resource group exists
if ! az group show --name "$STAGING_RG" &> /dev/null; then
    echo "✅ Resource group '$STAGING_RG' doesn't exist - nothing to clean up"
    exit 0
fi

echo "📊 Found resource group '$STAGING_RG'"

# List resources for backup documentation
echo "📦 Documenting existing resources..."
az resource list -g "$STAGING_RG" --output table > "$BACKUP_DIR/staging-resources-list.txt"
az resource list -g "$STAGING_RG" --output json > "$BACKUP_DIR/staging-resources-details.json"
echo "✅ Resource documentation saved to $BACKUP_DIR/"

# Count resources
RESOURCE_COUNT=$(az resource list -g "$STAGING_RG" --query "length(@)" -o tsv)
echo "📊 Found $RESOURCE_COUNT resources to remove"

# Show what will be deleted
echo ""
echo "🗑️  Resources to be deleted:"
az resource list -g "$STAGING_RG" --output table

# Confirmation prompt
echo ""
echo "⚠️  WARNING: This will permanently delete the staging environment!"
echo "   Resource Group: $STAGING_RG"
echo "   Resources: $RESOURCE_COUNT items"
echo ""
read -p "🤔 Are you sure you want to proceed? (type 'DELETE' to confirm): " confirmation

if [ "$confirmation" != "DELETE" ]; then
    echo "❌ Cleanup cancelled by user"
    exit 1
fi

# Execute cleanup
echo ""
echo "🧹 Starting staging environment cleanup..."
echo "   This may take several minutes..."

# Start deletion (async)
if az group delete --name "$STAGING_RG" --yes --no-wait; then
    echo "✅ Staging environment deletion initiated successfully"
    echo ""
    echo "📋 Cleanup Summary:"
    echo "  • Deletion started for resource group: $STAGING_RG"
    echo "  • Backup documentation saved to: $BACKUP_DIR/"
    echo "  • Resources backed up: $RESOURCE_COUNT items"
    echo ""
    echo "⏳ Note: Deletion is running in the background"
    echo "   Use 'az group show --name $STAGING_RG' to check status"
    echo "   Expect completion in 5-15 minutes"
    echo ""
    echo "🚀 You can proceed to the next cleanup phase while this runs"
else
    echo "❌ Failed to initiate staging environment deletion"
    echo "📞 Manual intervention required"
    exit 1
fi

echo "✅ Staging cleanup script completed successfully"