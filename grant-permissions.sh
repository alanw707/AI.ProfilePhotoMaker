#!/bin/bash

# Azure Service Principal Permission Grant Script
# Service Principal ID: b19f1dae-b21a-4a63-b56d-085bad6b23b2

echo "🔧 Granting Azure permissions for service principal..."

# Get current subscription ID
SUBSCRIPTION_ID=$(az account show --query id --output tsv)
echo "📍 Current subscription: $SUBSCRIPTION_ID"

# Service principal object ID
SP_OBJECT_ID="b19f1dae-b21a-4a63-b56d-085bad6b23b2"

echo "👤 Service Principal: $SP_OBJECT_ID"

# Option 1: Grant Contributor role at subscription level (recommended)
echo "🚀 Granting Contributor role at subscription level..."
az role assignment create \
  --assignee $SP_OBJECT_ID \
  --role "Contributor" \
  --scope "/subscriptions/$SUBSCRIPTION_ID"

# Verify the role assignment
echo "✅ Verifying role assignment..."
az role assignment list \
  --assignee $SP_OBJECT_ID \
  --output table

echo "🎉 Permission grant completed!"
echo ""
echo "📋 Next steps:"
echo "1. Wait 2-3 minutes for permissions to propagate"
echo "2. Run: gh workflow run \"Deploy Infrastructure to Azure\" --field environment=staging"
echo "3. Monitor deployment: gh run list --workflow=\"Deploy Infrastructure to Azure\" --limit 1"