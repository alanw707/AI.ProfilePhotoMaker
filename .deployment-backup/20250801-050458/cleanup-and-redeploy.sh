#!/bin/bash
# Deployment cleanup and standardization script for AI Profile Photo Maker

set -euo pipefail

# Configuration
RESOURCE_GROUP="ai-profile-photo-maker-staging"
LOCATION="East US 2"

echo "🧹 Starting infrastructure cleanup and standardization..."

# Function to safely delete resource
delete_resource_safe() {
    local resource_type=$1
    local resource_name=$2
    local resource_group=$3
    
    echo "🗑️ Checking $resource_type: $resource_name"
    
    case $resource_type in
        "redis")
            if az redis show --name "$resource_name" --resource-group "$resource_group" >/dev/null 2>&1; then
                echo "⚠️ Deleting Redis Cache: $resource_name (not needed per current design)"
                az redis delete --name "$resource_name" --resource-group "$resource_group" --yes --no-wait
            else
                echo "✅ Redis Cache already removed"
            fi
            ;;
        "alert")
            if az monitor metrics alert show --name "$resource_name" --resource-group "$resource_group" >/dev/null 2>&1; then
                echo "⚠️ Deleting misconfigured alert: $resource_name"
                az monitor metrics alert delete --name "$resource_name" --resource-group "$resource_group" --yes
            else
                echo "✅ Alert already cleaned up"
            fi
            ;;
    esac
}

# Clean up problematic resources
echo "🔍 Phase 1: Resource Cleanup"
delete_resource_safe "redis" "aiprofilephotomaker-redis-staging-f544mjgkzprbe" "$RESOURCE_GROUP"
delete_resource_safe "alert" "aiprofilephotomaker-webapp-response-time-staging" "$RESOURCE_GROUP"
delete_resource_safe "alert" "aiprofilephotomaker-sql-dtu-staging" "$RESOURCE_GROUP"

# Wait for deletions to complete
echo "⏳ Waiting for resource deletions to complete..."
sleep 30

echo "✅ Cleanup phase completed"
echo ""
echo "📋 Next steps:"
echo "1. Fix the Bicep template issues (see corrected template)"
echo "2. Redeploy infrastructure with corrected configuration"
echo "3. Verify all resources are properly configured"
echo ""
echo "🚀 Ready for redeployment with fixed template"