#!/bin/bash

# Azure V1 Selective Cleanup
# Removes deployment conflicts while preserving valuable resources

set -e

echo "🧹 Azure V1 Selective Cleanup"
echo "============================="
echo ""

# Configuration
V1_RG="aiprofilemaker-v1"
CLEANUP_LOG_DIR="azure-cleanup-backup/$(date +%Y%m%d-%H%M%S)-v1-cleanup"

# Create cleanup log directory
mkdir -p "$CLEANUP_LOG_DIR"

echo "📋 Cleanup Configuration:"
echo "  Target Resource Group: $V1_RG"
echo "  Cleanup Log Directory: $CLEANUP_LOG_DIR"
echo ""

# Safety checks
echo "🔍 Pre-cleanup Safety Checks..."

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
    exit 0
fi

echo "📊 Found resource group '$V1_RG'"

# Document current state
echo "📋 Documenting current state before cleanup..."
az resource list -g "$V1_RG" --output table > "$CLEANUP_LOG_DIR/pre-cleanup-resources.txt"
az resource list -g "$V1_RG" --output json > "$CLEANUP_LOG_DIR/pre-cleanup-resources.json"

# Interactive cleanup with safety confirmations
echo ""
echo "🎯 Selective Cleanup Strategy:"
echo "  ✅ PRESERVE: Container Registry, Key Vault, Storage Account"
echo "  🧹 REMOVE: Container Apps, SQL Database, Application Insights"
echo "  ⚠️  ASSESS: Other resources case-by-case"
echo ""

# Cleanup Container Apps (safe to remove - will be redeployed)
echo "🚀 Container Apps Cleanup..."
CONTAINER_APPS=$(az containerapp list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")

if [ -n "$CONTAINER_APPS" ]; then
    echo "  Found Container Apps: $CONTAINER_APPS"
    echo "  These will be redeployed fresh during deployment"
    
    read -p "🤔 Remove all Container Apps? (y/N): " remove_apps
    if [[ "$remove_apps" =~ ^[Yy]$ ]]; then
        for app in $CONTAINER_APPS; do
            echo "    Removing Container App: $app"
            if az containerapp delete --name "$app" -g "$V1_RG" --yes; then
                echo "      ✅ Removed: $app"
                echo "REMOVED: Container App $app" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            else
                echo "      ❌ Failed to remove: $app"
                echo "FAILED: Container App $app" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            fi
        done
    else
        echo "  Skipped: Container Apps preserved"
        echo "SKIPPED: Container Apps preserved by user choice" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
    fi
else
    echo "  No Container Apps found"
fi

# Cleanup Container Apps Environment (after apps are removed)
echo ""
echo "🏗️  Container Apps Environment Cleanup..."
CONTAINER_ENVS=$(az containerapp env list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")

if [ -n "$CONTAINER_ENVS" ]; then
    echo "  Found Container Environments: $CONTAINER_ENVS"
    
    read -p "🤔 Remove Container Environments? (y/N): " remove_envs
    if [[ "$remove_envs" =~ ^[Yy]$ ]]; then
        for env in $CONTAINER_ENVS; do
            echo "    Removing Container Environment: $env"
            if az containerapp env delete --name "$env" -g "$V1_RG" --yes; then
                echo "      ✅ Removed: $env"
                echo "REMOVED: Container Environment $env" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            else
                echo "      ❌ Failed to remove: $env"
                echo "FAILED: Container Environment $env" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            fi
        done
    else
        echo "  Skipped: Container Environments preserved"
    fi
else
    echo "  No Container Environments found"
fi

# SQL Database Assessment (data loss risk)
echo ""
echo "🗄️  SQL Database Assessment..."
SQL_SERVERS=$(az sql server list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")

if [ -n "$SQL_SERVERS" ]; then
    echo "  Found SQL Servers: $SQL_SERVERS"
    echo "  ⚠️  WARNING: Removing SQL Database will cause DATA LOSS"
    echo "  📦 Ensure you have backed up any critical data first"
    
    read -p "🤔 Remove SQL Databases? (type 'DELETE' to confirm): " remove_sql
    if [ "$remove_sql" = "DELETE" ]; then
        for server in $SQL_SERVERS; do
            echo "    Removing SQL Server: $server"
            if az sql server delete --name "$server" -g "$V1_RG" --yes; then
                echo "      ✅ Removed: $server"
                echo "REMOVED: SQL Server $server" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            else
                echo "      ❌ Failed to remove: $server"
                echo "FAILED: SQL Server $server" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            fi
        done
    else
        echo "  Skipped: SQL Servers preserved"
        echo "SKIPPED: SQL Servers preserved by user choice" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
    fi
else
    echo "  No SQL Servers found"
fi

# Application Insights Cleanup (logs will be lost)
echo ""
echo "📊 Application Insights Cleanup..."
APP_INSIGHTS=$(az monitor app-insights component list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")

if [ -n "$APP_INSIGHTS" ]; then
    echo "  Found Application Insights: $APP_INSIGHTS"
    echo "  📋 Note: Historical logs and metrics will be lost"
    
    read -p "🤔 Remove Application Insights? (y/N): " remove_insights
    if [[ "$remove_insights" =~ ^[Yy]$ ]]; then
        for insight in $APP_INSIGHTS; do
            echo "    Removing Application Insights: $insight"
            if az monitor app-insights component delete --app "$insight" -g "$V1_RG"; then
                echo "      ✅ Removed: $insight"
                echo "REMOVED: Application Insights $insight" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            else
                echo "      ❌ Failed to remove: $insight"
                echo "FAILED: Application Insights $insight" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            fi
        done
    else
        echo "  Skipped: Application Insights preserved"
    fi
else
    echo "  No Application Insights found"
fi

# Log Analytics Workspace Cleanup
echo ""
echo "📈 Log Analytics Workspace Cleanup..."
LOG_WORKSPACES=$(az monitor log-analytics workspace list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")

if [ -n "$LOG_WORKSPACES" ]; then
    echo "  Found Log Analytics Workspaces: $LOG_WORKSPACES"
    
    read -p "🤔 Remove Log Analytics Workspaces? (y/N): " remove_logs
    if [[ "$remove_logs" =~ ^[Yy]$ ]]; then
        for workspace in $LOG_WORKSPACES; do
            echo "    Removing Log Analytics Workspace: $workspace"
            if az monitor log-analytics workspace delete --workspace-name "$workspace" -g "$V1_RG" --yes; then
                echo "      ✅ Removed: $workspace"
                echo "REMOVED: Log Analytics Workspace $workspace" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            else
                echo "      ❌ Failed to remove: $workspace"
                echo "FAILED: Log Analytics Workspace $workspace" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            fi
        done
    else
        echo "  Skipped: Log Analytics Workspaces preserved"
    fi
else
    echo "  No Log Analytics Workspaces found"
fi

# Container Registry Assessment (HIGH VALUE - preserve by default)
echo ""
echo "📦 Container Registry Assessment..."
REGISTRIES=$(az acr list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")

if [ -n "$REGISTRIES" ]; then
    echo "  Found Container Registries: $REGISTRIES"
    echo "  ✅ RECOMMENDED: Preserve registries for reuse in deployment"
    echo "  📋 These registries may contain valuable container images"
    
    for registry in $REGISTRIES; do
        REPO_COUNT=$(az acr repository list --name "$registry" --query "length(@)" -o tsv 2>/dev/null || echo "0")
        echo "    Registry: $registry (Repositories: $REPO_COUNT)"
    done
    
    read -p "🤔 Remove Container Registries? (type 'DELETE' to confirm): " remove_registries
    if [ "$remove_registries" = "DELETE" ]; then
        for registry in $REGISTRIES; do
            echo "    Removing Container Registry: $registry"
            if az acr delete --name "$registry" -g "$V1_RG" --yes; then
                echo "      ✅ Removed: $registry"
                echo "REMOVED: Container Registry $registry" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            else
                echo "      ❌ Failed to remove: $registry"
                echo "FAILED: Container Registry $registry" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            fi
        done
    else
        echo "  ✅ Preserved: Container Registries (RECOMMENDED)"
        echo "PRESERVED: Container Registries by user choice" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
    fi
else
    echo "  No Container Registries found"
fi

# Key Vault Assessment (HIGH VALUE - preserve by default)
echo ""
echo "🔐 Key Vault Assessment..."
KEY_VAULTS=$(az keyvault list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")

if [ -n "$KEY_VAULTS" ]; then
    echo "  Found Key Vaults: $KEY_VAULTS"
    echo "  ✅ RECOMMENDED: Preserve Key Vaults (contain secrets)"
    echo "  ⚠️  WARNING: Removing Key Vault will lose all secrets"
    
    for vault in $KEY_VAULTS; do
        SECRET_COUNT=$(az keyvault secret list --vault-name "$vault" --query "length(@)" -o tsv 2>/dev/null || echo "No access")
        echo "    Key Vault: $vault (Secrets: $SECRET_COUNT)"
    done
    
    read -p "🤔 Remove Key Vaults? (type 'DELETE' to confirm): " remove_keyvaults
    if [ "$remove_keyvaults" = "DELETE" ]; then
        for vault in $KEY_VAULTS; do
            echo "    Removing Key Vault: $vault"
            if az keyvault delete --name "$vault" -g "$V1_RG"; then
                echo "      ✅ Removed: $vault"
                echo "REMOVED: Key Vault $vault" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
                
                # Also purge the key vault to allow name reuse
                echo "    Purging Key Vault: $vault (for name reuse)"
                az keyvault purge --name "$vault" 2>/dev/null || echo "      ⚠️  Could not purge (may require admin permissions)"
            else
                echo "      ❌ Failed to remove: $vault"
                echo "FAILED: Key Vault $vault" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            fi
        done
    else
        echo "  ✅ Preserved: Key Vaults (RECOMMENDED)"
        echo "PRESERVED: Key Vaults by user choice" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
    fi
else
    echo "  No Key Vaults found"
fi

# Storage Account Assessment (HIGH VALUE - preserve by default)
echo ""
echo "🗄️  Storage Account Assessment..."
STORAGE_ACCOUNTS=$(az storage account list -g "$V1_RG" --query "[].name" -o tsv 2>/dev/null || echo "")

if [ -n "$STORAGE_ACCOUNTS" ]; then
    echo "  Found Storage Accounts: $STORAGE_ACCOUNTS"
    echo "  ✅ RECOMMENDED: Preserve Storage Accounts (may contain data)"
    echo "  ⚠️  WARNING: Removing Storage Account will lose all blob data"
    
    read -p "🤔 Remove Storage Accounts? (type 'DELETE' to confirm): " remove_storage
    if [ "$remove_storage" = "DELETE" ]; then
        for account in $STORAGE_ACCOUNTS; do
            echo "    Removing Storage Account: $account"
            if az storage account delete --name "$account" -g "$V1_RG" --yes; then
                echo "      ✅ Removed: $account"
                echo "REMOVED: Storage Account $account" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            else
                echo "      ❌ Failed to remove: $account"
                echo "FAILED: Storage Account $account" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
            fi
        done
    else
        echo "  ✅ Preserved: Storage Accounts (RECOMMENDED)"
        echo "PRESERVED: Storage Accounts by user choice" >> "$CLEANUP_LOG_DIR/cleanup-actions.log"
    fi
else
    echo "  No Storage Accounts found"
fi

# Handle any remaining resources
echo ""
echo "🔍 Checking for remaining resources..."
REMAINING_RESOURCES=$(az resource list -g "$V1_RG" --query "[].{name:name, type:type}" -o tsv 2>/dev/null || echo "")

if [ -n "$REMAINING_RESOURCES" ]; then
    echo "  Found additional resources:"
    echo "$REMAINING_RESOURCES" | while IFS=$'\t' read -r name type; do
        echo "    - $name ($type)"
    done
    
    echo ""
    echo "🤔 Additional resources found. Manual review recommended."
    echo "   Review these resources and remove manually if not needed:"
    echo "$REMAINING_RESOURCES" | while IFS=$'\t' read -r name type; do
        echo "   az resource delete --name '$name' --resource-group '$V1_RG' --resource-type '$type'"
    done
else
    echo "  No additional resources found"
fi

# Document final state
echo ""
echo "📋 Documenting final state after cleanup..."
az resource list -g "$V1_RG" --output table > "$CLEANUP_LOG_DIR/post-cleanup-resources.txt"
az resource list -g "$V1_RG" --output json > "$CLEANUP_LOG_DIR/post-cleanup-resources.json"

# Generate cleanup summary
echo ""
echo "📊 Generating cleanup summary..."
cat > "$CLEANUP_LOG_DIR/cleanup-summary.md" << EOF
# V1 Selective Cleanup Summary

## Cleanup Details
- Date: $(date)
- Resource Group: $V1_RG
- Cleanup Log: $CLEANUP_LOG_DIR

## Actions Taken
$(cat "$CLEANUP_LOG_DIR/cleanup-actions.log" 2>/dev/null || echo "No actions logged")

## Resources Before Cleanup
\`\`\`
$(cat "$CLEANUP_LOG_DIR/pre-cleanup-resources.txt" 2>/dev/null || echo "No pre-cleanup data")
\`\`\`

## Resources After Cleanup
\`\`\`
$(cat "$CLEANUP_LOG_DIR/post-cleanup-resources.txt" 2>/dev/null || echo "No post-cleanup data")
\`\`\`

## Recommendations
1. Review remaining resources in post-cleanup list
2. Ensure valuable resources are properly backed up
3. Test deployment with current resource state
4. Monitor costs of preserved resources

## Next Steps
1. Run pre-deployment validation
2. Execute GitHub Actions deployment
3. Verify deployment success
4. Clean up any deployment conflicts if they arise
EOF

echo "✅ Selective cleanup completed successfully"
echo ""
echo "📋 Cleanup Summary:"
echo "  • Cleanup actions logged to: $CLEANUP_LOG_DIR/cleanup-actions.log"
echo "  • Detailed summary: $CLEANUP_LOG_DIR/cleanup-summary.md"
echo "  • Pre-cleanup state: $CLEANUP_LOG_DIR/pre-cleanup-resources.txt"
echo "  • Post-cleanup state: $CLEANUP_LOG_DIR/post-cleanup-resources.txt"
echo ""
echo "🚀 Next Steps:"
echo "  1. Review cleanup summary and remaining resources"
echo "  2. Run pre-deployment validation script"
echo "  3. Execute V1 deployment via GitHub Actions"
echo "  4. Monitor deployment for any remaining conflicts"
echo ""
echo "✅ Environment prepared for V1 deployment!"