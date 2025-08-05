#!/bin/bash
# Phase 1: Smart Assessment - Quick resource inventory
# Testing environment focused - no over-engineering

echo "🔍 Phase 1: Smart Assessment"
echo "=================================="

# Set variables
OLD_RG="rg-aiprofilemaker-staging"
TARGET_RG="aiprofilemaker-v1"

echo "📊 Assessing Resource Groups..."

# Quick resource group status
echo "🏗️ Old Staging Resources ($OLD_RG):"
az group show --name "$OLD_RG" --query "{name:name, location:location, status:properties.provisioningState}" -o table 2>/dev/null || echo "❌ Resource group not found"

if az group exists --name "$OLD_RG"; then
    echo "📦 Key Resources in Old Staging:"
    az resource list --resource-group "$OLD_RG" --query "[].{Name:name, Type:type, Location:location}" -o table
fi

echo ""
echo "🎯 Target Resources ($TARGET_RG):"
az group show --name "$TARGET_RG" --query "{name:name, location:location, status:properties.provisioningState}" -o table 2>/dev/null || echo "❌ Resource group not found"

if az group exists --name "$TARGET_RG"; then
    echo "📦 Current Resources in Target:"
    az resource list --resource-group "$TARGET_RG" --query "[].{Name:name, Type:type, Location:location}" -o table
fi

echo ""
echo "🔍 Quick Conflict Check:"
echo "Checking for naming conflicts between groups..."

# Check for potential naming conflicts
if az group exists --name "$OLD_RG" && az group exists --name "$TARGET_RG"; then
    OLD_RESOURCES=$(az resource list --resource-group "$OLD_RG" --query "[].name" -o tsv)
    TARGET_RESOURCES=$(az resource list --resource-group "$TARGET_RG" --query "[].name" -o tsv)
    
    echo "🚨 Potential naming conflicts:"
    for resource in $OLD_RESOURCES; do
        if echo "$TARGET_RESOURCES" | grep -q "^$resource$"; then
            echo "  ⚠️  Conflict: $resource exists in both groups"
        fi
    done
fi

echo ""
echo "📋 Assessment Summary:"
echo "• Old staging group exists: $(az group exists --name "$OLD_RG" && echo "✅ Yes" || echo "❌ No")"
echo "• Target group exists: $(az group exists --name "$TARGET_RG" && echo "✅ Yes" || echo "❌ No")"
echo "• Ready for Phase 2: Strategic Cleanup"

echo ""
echo "⏱️ Assessment complete - proceed to cleanup-phase2-strategic.sh"