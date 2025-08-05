#!/bin/bash
# Phase 3: Deployment Readiness - Validate clean foundation
# Testing environment focused - deployment preparation

echo "🎯 Phase 3: Deployment Readiness"
echo "=================================="

# Set variables
TARGET_RG="aiprofilemaker-v1"
LOCATION="East US 2"

echo "🔍 Step 1: Validate Target Environment"

# Ensure target resource group exists
if ! az group exists --name "$TARGET_RG"; then
    echo "🚨 Creating target resource group..."
    az group create --name "$TARGET_RG" --location "$LOCATION"
fi

echo "✅ Target resource group: $TARGET_RG"
echo "📍 Location: $LOCATION"

echo ""
echo "🐳 Step 2: Container Registry Validation"

# Note: Container Registry was manually removed - will be created fresh by Bicep deployment
echo "ℹ️ Container Registry was removed (random naming issue)"
echo "✅ Fresh Container Registry will be created by Bicep deployment"

# Check Container Registry (should be empty now)
ACR_LIST=$(az acr list --resource-group "$TARGET_RG" --query "[].name" -o tsv)
if [ -n "$ACR_LIST" ]; then
    ACR_NAME=$(echo "$ACR_LIST" | head -n 1)
    echo "⚠️ Unexpected Container Registry found: $ACR_NAME"
    
    # Test ACR connectivity
    echo "🔍 Testing ACR connectivity..."
    ACR_LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --resource-group "$TARGET_RG" --query "loginServer" -o tsv)
    
    # Check if we can access the registry
    if az acr repository list --name "$ACR_NAME" --output table >/dev/null 2>&1; then
        echo "✅ ACR accessible and ready for deployments"
        
        # Save deployment variables with existing ACR
        cat > .env.deployment << EOF
# Deployment Environment Variables
export RESOURCE_GROUP="$TARGET_RG"
export LOCATION="$LOCATION"
export ACR_NAME="$ACR_NAME"
export ACR_LOGIN_SERVER="$ACR_LOGIN_SERVER"

# Container App Settings
export APP_NAME_API="aiprofilemaker-api-v1"
export APP_NAME_WEB="aiprofilemaker-web-v1"
export CONTAINER_APP_ENV="aiprofilemaker-env-v1"

# Database Settings (will be created fresh)
export SQL_SERVER_NAME="aiprofilemaker-sql-v1"
export SQL_DATABASE_NAME="aiprofilemakerdb"

# Storage Settings (will be created fresh)
export STORAGE_ACCOUNT_NAME="aiprofilemaker\${RANDOM}v1"
export STORAGE_CONTAINER_NAME="profile-images"
EOF
        echo "📝 Deployment variables saved to .env.deployment"
    else
        echo "⚠️ ACR exists but may need permissions setup"
    fi
else
    echo "✅ No Container Registry found (as expected)"
    echo "🚀 Bicep deployment will create fresh Container Registry with proper naming"
    
    # Create deployment variables for fresh deployment
    cat > .env.deployment << EOF
# Deployment Environment Variables
export RESOURCE_GROUP="$TARGET_RG"
export LOCATION="$LOCATION"
# Note: Container Registry was removed - will be created fresh by Bicep deployment
export ACR_NAME=""
export ACR_LOGIN_SERVER=""

# Container App Settings
export APP_NAME_API="aiprofilemaker-api-v1"
export APP_NAME_WEB="aiprofilemaker-web-v1"
export CONTAINER_APP_ENV="aiprofilemaker-env-v1"

# Database Settings (will be created fresh)
export SQL_SERVER_NAME="aiprofilemaker-sql-v1"
export SQL_DATABASE_NAME="aiprofilemakerdb"

# Storage Settings (will be created fresh)
export STORAGE_ACCOUNT_NAME="aiprofilemaker\${RANDOM}v1"
export STORAGE_CONTAINER_NAME="profile-images"
EOF
    echo "📝 Deployment variables saved to .env.deployment"
    echo "ℹ️ Container Registry details will be populated after Bicep deployment"
fi

echo ""
echo "🧹 Step 3: Final Cleanup Validation"

# Check for any remaining old resources
echo "🔍 Scanning for old/conflicting resources..."
OLD_RESOURCES=$(az resource list --resource-group "$TARGET_RG" --query "[?contains(name, 'staging')]" -o tsv 2>/dev/null || true)
if [ -n "$OLD_RESOURCES" ]; then
    echo "⚠️ Found staging resources in target group:"
    az resource list --resource-group "$TARGET_RG" --query "[?contains(name, 'staging')].{Name:name, Type:type}" -o table
    echo "💡 Consider removing these before deployment"
else
    echo "✅ No conflicting staging resources found"
fi

echo ""
echo "🎯 Step 4: Deployment Readiness Check"

# Validate prerequisites
READY=true

echo "📋 Prerequisites Check:"
echo "• Resource Group: $(az group exists --name "$TARGET_RG" && echo "✅" || echo "❌") $TARGET_RG"
echo "• Container Registry: $([ -n "$ACR_NAME" ] && echo "✅" || echo "❌") ACR Setup"
echo "• Clean Environment: $([ -z "$OLD_RESOURCES" ] && echo "✅" || echo "⚠️") No staging conflicts"
echo "• Deployment Config: $([ -f .env.deployment ] && echo "✅" || echo "❌") Environment variables"

echo ""
if [ -f .env.deployment ]; then
    echo "🚀 Ready for Deployment!"
    echo ""
    echo "Next steps:"
    echo "1. Source deployment variables: source .env.deployment"
    echo "2. Build and push container image to ACR"
    echo "3. Deploy Container Apps with infrastructure"
    echo ""
    echo "🔧 Quick deployment commands:"
    echo "# Load environment"
    echo "source .env.deployment"
    echo ""
    echo "# Build and push image"
    echo "docker build -t \$ACR_LOGIN_SERVER/aiprofilemaker-api:v1 ."
    echo "az acr login --name \$ACR_NAME"
    echo "docker push \$ACR_LOGIN_SERVER/aiprofilemaker-api:v1"
else
    echo "⚠️ Manual setup required - check Container Registry configuration"
fi

echo ""
echo "🎉 Phase 3 Complete - Environment ready for v1 deployment!"