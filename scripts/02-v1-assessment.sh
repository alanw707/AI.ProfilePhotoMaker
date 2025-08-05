#!/bin/bash

# Azure V1 Environment Assessment
# Analyzes the aiprofilemaker-v1 resource group for cleanup planning

set -e

echo "🔍 Azure V1 Environment Assessment"
echo "=================================="
echo ""

# Configuration
V1_RG="aiprofilemaker-v1"
ASSESSMENT_DIR="azure-cleanup-backup/$(date +%Y%m%d-%H%M%S)-v1-assessment"

# Create assessment directory
mkdir -p "$ASSESSMENT_DIR"

echo "📋 Assessment Configuration:"
echo "  Target Resource Group: $V1_RG"
echo "  Assessment Directory: $ASSESSMENT_DIR"
echo ""

# Safety checks
echo "🔍 Pre-assessment Safety Checks..."

# Check if Azure CLI is available
if ! command -v az &> /dev/null; then
    echo "❌ Azure CLI not found. Please install Azure CLI first."
    exit 1
fi

# Check if logged in
if ! az account show &> /dev/null; then
    echo "❌ Not logged into Azure. Please run 'az login' first."
    exit 1
fi

echo "✅ Azure CLI available and authenticated"

# Check if resource group exists
if ! az group show --name "$V1_RG" &> /dev/null; then
    echo "✅ Resource group '$V1_RG' doesn't exist - clean slate for deployment"
    echo "🚀 Ready for fresh V1 deployment!"
    exit 0
fi

echo "📊 Found resource group '$V1_RG'"

# Comprehensive resource analysis
echo ""
echo "📊 Analyzing V1 Environment Resources..."

# Basic resource list
echo "📋 Generating resource inventory..."
az resource list -g "$V1_RG" --output table > "$ASSESSMENT_DIR/v1-resources-list.txt"
az resource list -g "$V1_RG" --output json > "$ASSESSMENT_DIR/v1-resources-details.json"

# Count resources
RESOURCE_COUNT=$(az resource list -g "$V1_RG" --query "length(@)" -o tsv)
echo "📊 Found $RESOURCE_COUNT resources in V1 environment"

# Display current resources
echo ""
echo "📋 Current V1 Resources:"
az resource list -g "$V1_RG" --output table

# Analyze specific resource types
echo ""
echo "🔍 Detailed Resource Analysis..."

# Container Registry Analysis
echo ""
echo "📦 Container Registry Analysis:"
REGISTRIES=$(az acr list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")
if [ -n "$REGISTRIES" ]; then
    for registry in $REGISTRIES; do
        echo "  Registry: $registry"
        echo "    Login Server: $(az acr show --name "$registry" -g "$V1_RG" --query "loginServer" -o tsv)"
        echo "    SKU: $(az acr show --name "$registry" -g "$V1_RG" --query "sku.name" -o tsv)"
        
        # List repositories
        REPOS=$(az acr repository list --name "$registry" -o tsv 2>/dev/null || echo "")
        if [ -n "$REPOS" ]; then
            echo "    Repositories:"
            for repo in $REPOS; do
                echo "      - $repo"
                # Get tags for this repo
                TAGS=$(az acr repository show-tags --name "$registry" --repository "$repo" -o tsv 2>/dev/null | head -5)
                if [ -n "$TAGS" ]; then
                    echo "        Tags: $(echo $TAGS | tr '\n' ', ' | sed 's/,$//')"
                fi
                # Save repository details
                echo "$registry/$repo" >> "$ASSESSMENT_DIR/container-images.txt"
            done
        else
            echo "    No repositories found"
        fi
        echo ""
    done
else
    echo "  No container registries found"
fi

# Container Apps Analysis
echo "🚀 Container Apps Analysis:"
CONTAINER_APPS=$(az containerapp list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")
if [ -n "$CONTAINER_APPS" ]; then
    for app in $CONTAINER_APPS; do
        echo "  App: $app"
        echo "    Status: $(az containerapp show --name "$app" -g "$V1_RG" --query "properties.provisioningState" -o tsv 2>/dev/null || echo "Unknown")"
        echo "    URL: $(az containerapp show --name "$app" -g "$V1_RG" --query "properties.configuration.ingress.fqdn" -o tsv 2>/dev/null || echo "No ingress")"
    done
else
    echo "  No container apps found"
fi

# SQL Database Analysis
echo ""
echo "🗄️  SQL Database Analysis:"
SQL_SERVERS=$(az sql server list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")
if [ -n "$SQL_SERVERS" ]; then
    for server in $SQL_SERVERS; do
        echo "  Server: $server"
        echo "    FQDN: $(az sql server show --name "$server" -g "$V1_RG" --query "fullyQualifiedDomainName" -o tsv)"
        # List databases
        DATABASES=$(az sql db list --server "$server" -g "$V1_RG" --query "[?name != 'master'].name" -o tsv 2>/dev/null || echo "")
        if [ -n "$DATABASES" ]; then
            echo "    Databases: $DATABASES"
        fi
    done
else
    echo "  No SQL servers found"
fi

# Storage Account Analysis
echo ""
echo "🗄️  Storage Account Analysis:"
STORAGE_ACCOUNTS=$(az storage account list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")
if [ -n "$STORAGE_ACCOUNTS" ]; then
    for account in $STORAGE_ACCOUNTS; do
        echo "  Account: $account"
        echo "    SKU: $(az storage account show --name "$account" -g "$V1_RG" --query "sku.name" -o tsv)"
        echo "    Location: $(az storage account show --name "$account" -g "$V1_RG" --query "location" -o tsv)"
    done
else
    echo "  No storage accounts found"
fi

# Key Vault Analysis
echo ""
echo "🔐 Key Vault Analysis:"
KEY_VAULTS=$(az keyvault list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")
if [ -n "$KEY_VAULTS" ]; then
    for vault in $KEY_VAULTS; do
        echo "  Vault: $vault"
        echo "    URL: $(az keyvault show --name "$vault" -g "$V1_RG" --query "properties.vaultUri" -o tsv)"
        # Count secrets (if we have access)
        SECRET_COUNT=$(az keyvault secret list --vault-name "$vault" --query "length(@)" -o tsv 2>/dev/null || echo "No access")
        echo "    Secrets: $SECRET_COUNT"
    done
else
    echo "  No key vaults found"
fi

# Generate cleanup recommendations
echo ""
echo "💡 Generating Cleanup Recommendations..."

# Create recommendations file
cat > "$ASSESSMENT_DIR/cleanup-recommendations.md" << EOF
# V1 Environment Cleanup Recommendations

## Assessment Summary
- Resource Group: $V1_RG
- Total Resources: $RESOURCE_COUNT
- Assessment Date: $(date)

## Cleanup Strategy

### Safe to Keep (High Value)
- Container Registry (if contains images)
- Key Vault (if contains secrets)
- Storage Account (if contains data)

### Selective Cleanup (Assess First)
- Container Apps (redeploy from scratch)
- SQL Database (backup data first)
- Application Insights (logs will be lost)

### Recommended Actions
1. **Backup Phase**
   - Export container images from registry
   - Backup SQL database if contains data
   - Document Key Vault secrets

2. **Selective Removal**
   - Remove Container Apps (will be redeployed)
   - Remove SQL Database (after backup)
   - Remove Application Insights (after log export)

3. **Resource Preservation**
   - Keep Container Registry (reuse for deployment)
   - Keep Key Vault (preserve secrets)
   - Keep Storage Account (preserve blob data)

## Risk Assessment
- **Low Risk**: Container Apps, Application Insights
- **Medium Risk**: SQL Database (data loss)
- **High Risk**: Container Registry (image loss), Key Vault (secret loss)

## Next Steps
1. Run backup scripts for high-risk resources
2. Execute selective cleanup
3. Validate clean state for deployment
EOF

echo "✅ Assessment completed successfully"
echo ""
echo "📋 Assessment Summary:"
echo "  • Resources analyzed: $RESOURCE_COUNT items"
echo "  • Assessment data saved to: $ASSESSMENT_DIR/"
echo "  • Cleanup recommendations: $ASSESSMENT_DIR/cleanup-recommendations.md"
echo ""
echo "🚀 Next Steps:"
echo "  1. Review cleanup recommendations"
echo "  2. Run backup scripts for valuable resources"
echo "  3. Execute selective cleanup based on recommendations"
echo ""
echo "📞 For detailed analysis, check files in: $ASSESSMENT_DIR/"