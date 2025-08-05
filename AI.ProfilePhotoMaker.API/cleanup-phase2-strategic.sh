#!/bin/bash
# Phase 2: Strategic Cleanup - Remove old staging, preserve essentials
# Testing environment focused - pragmatic approach

echo "🧹 Phase 2: Strategic Cleanup"
echo "=================================="

# Set variables
OLD_RG="rg-aiprofilemaker-staging"
TARGET_RG="aiprofilemaker-v1"

# Safety check
echo "⚠️  CLEANUP CONFIRMATION"
echo "This will DELETE the old staging resource group: $OLD_RG"
echo "This will PRESERVE the target resource group: $TARGET_RG"
echo ""
read -p "Are you sure you want to continue? (yes/no): " CONFIRM

if [ "$CONFIRM" != "yes" ]; then
    echo "❌ Cleanup cancelled"
    exit 1
fi

echo ""
echo "🎯 Step 1: Preserve Container Registry Info"
if az group exists --name "$TARGET_RG"; then
    echo "📦 Container Registry in target group:"
    az acr list --resource-group "$TARGET_RG" --query "[].{Name:name, LoginServer:loginServer, Location:location}" -o table
    
    # Save registry info for deployment scripts
    ACR_NAME=$(az acr list --resource-group "$TARGET_RG" --query "[0].name" -o tsv)
    if [ -n "$ACR_NAME" ]; then
        echo "✅ Container Registry preserved: $ACR_NAME"
        echo "export ACR_NAME=\"$ACR_NAME\"" > .env.acr
        echo "export ACR_LOGIN_SERVER=\"$(az acr list --resource-group "$TARGET_RG" --query "[0].loginServer" -o tsv)\"" >> .env.acr
        echo "📝 ACR info saved to .env.acr"
    fi
else
    echo "❌ Target resource group not found - creating it..."
    az group create --name "$TARGET_RG" --location "East US 2"
fi

echo ""
echo "🗑️ Step 2: Remove Old Staging Resources"
if az group exists --name "$OLD_RG"; then
    echo "🚨 Deleting old staging resource group..."
    echo "Resources being deleted:"
    az resource list --resource-group "$OLD_RG" --query "[].{Name:name, Type:type}" -o table
    
    echo ""
    echo "⏳ Starting deletion (this may take 5-10 minutes)..."
    az group delete --name "$OLD_RG" --yes --no-wait
    
    echo "✅ Deletion initiated - monitoring progress..."
    
    # Monitor deletion progress
    while az group exists --name "$OLD_RG" 2>/dev/null; do
        echo "⏳ Still deleting old resources..."
        sleep 30
    done
    
    echo "✅ Old staging resource group deleted successfully"
else
    echo "ℹ️  Old staging resource group already removed"
fi

echo ""
echo "🔍 Step 3: Clean Target Resource Group"
if az group exists --name "$TARGET_RG"; then
    echo "📦 Current resources in target group:"
    az resource list --resource-group "$TARGET_RG" --query "[].{Name:name, Type:type, Status:properties.provisioningState}" -o table
    
    # Remove any old/conflicting resources except Container Registry
    echo ""
    echo "🧹 Checking for cleanup candidates..."
    
    # Check for old container apps
    OLD_APPS=$(az containerapp list --resource-group "$TARGET_RG" --query "[?contains(name, 'staging')].name" -o tsv 2>/dev/null || true)
    if [ -n "$OLD_APPS" ]; then
        echo "🗑️ Removing old staging container apps..."
        for app in $OLD_APPS; do
            echo "  Removing: $app"
            az containerapp delete --name "$app" --resource-group "$TARGET_RG" --yes
        done
    fi
    
    # Check for old container app environments
    OLD_ENVS=$(az containerapp env list --resource-group "$TARGET_RG" --query "[?contains(name, 'staging')].name" -o tsv 2>/dev/null || true)
    if [ -n "$OLD_ENVS" ]; then
        echo "🗑️ Removing old staging environments..."
        for env in $OLD_ENVS; do
            echo "  Removing: $env"
            az containerapp env delete --name "$env" --resource-group "$TARGET_RG" --yes
        done
    fi
    
    echo "✅ Target resource group cleaned"
fi

echo ""
echo "📋 Cleanup Summary:"
echo "• Old staging group: $(az group exists --name "$OLD_RG" && echo "❌ Still exists" || echo "✅ Removed")"
echo "• Target group: $(az group exists --name "$TARGET_RG" && echo "✅ Ready" || echo "❌ Missing")"
echo "• Container Registry: $([ -f .env.acr ] && echo "✅ Preserved" || echo "⚠️ Check manually")"

echo ""
echo "⏱️ Strategic cleanup complete - proceed to cleanup-phase3-readiness.sh"