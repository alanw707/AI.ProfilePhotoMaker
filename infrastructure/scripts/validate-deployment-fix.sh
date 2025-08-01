#!/bin/bash
set -e

# Deployment Validation and Fix Script
# Validates Azure infrastructure deployment and fixes common issues

RESOURCE_GROUP="$1"
ENVIRONMENT="$2"

if [ -z "$RESOURCE_GROUP" ] || [ -z "$ENVIRONMENT" ]; then
    echo "Usage: $0 <resource-group> <environment>"
    exit 1
fi

echo "🔍 Validating deployment for Resource Group: $RESOURCE_GROUP"

# Function to check resource exists
check_resource() {
    local resource_type="$1"
    local resource_name="$2"
    local query="$3"
    
    echo "🔍 Checking $resource_type: $resource_name"
    
    if az resource show --resource-group "$RESOURCE_GROUP" --name "$resource_name" --resource-type "$resource_type" --output none 2>/dev/null; then
        echo "✅ $resource_type exists: $resource_name"
        return 0
    else
        echo "❌ $resource_type missing: $resource_name"
        return 1
    fi
}

# Validate core resources
echo "📋 Core Resource Validation:"

# App Service Plan
APP_SERVICE_PLAN=$(az appservice plan list --resource-group "$RESOURCE_GROUP" --query "[0].name" -o tsv 2>/dev/null || echo "")
if [ -n "$APP_SERVICE_PLAN" ]; then
    echo "✅ App Service Plan: $APP_SERVICE_PLAN"
    
    # Check if it's F1 tier (problematic)
    SKU=$(az appservice plan show --name "$APP_SERVICE_PLAN" --resource-group "$RESOURCE_GROUP" --query "sku.name" -o tsv)
    if [ "$SKU" = "F1" ]; then
        echo "⚠️  WARNING: F1 tier detected - this may cause deployment failures"
        echo "   Recommendation: Upgrade to B1 tier for staging"
    else
        echo "✅ App Service Plan tier: $SKU"
    fi
else
    echo "❌ App Service Plan not found"
fi

# Web App
WEB_APP=$(az webapp list --resource-group "$RESOURCE_GROUP" --query "[0].name" -o tsv 2>/dev/null || echo "")
if [ -n "$WEB_APP" ]; then
    echo "✅ Web App: $WEB_APP"
    
    # Check identity
    IDENTITY=$(az webapp identity show --name "$WEB_APP" --resource-group "$RESOURCE_GROUP" --query "principalId" -o tsv 2>/dev/null || echo "")
    if [ -n "$IDENTITY" ] && [ "$IDENTITY" != "null" ]; then
        echo "✅ Web App managed identity: $IDENTITY"
    else
        echo "⚠️  Web App managed identity not configured"
    fi
else
    echo "❌ Web App not found"
fi

# Key Vault
KEY_VAULT=$(az keyvault list --resource-group "$RESOURCE_GROUP" --query "[0].name" -o tsv 2>/dev/null || echo "")
if [ -n "$KEY_VAULT" ]; then
    echo "✅ Key Vault: $KEY_VAULT"
    
    # Check access policies
    if [ -n "$WEB_APP" ] && [ -n "$IDENTITY" ]; then
        POLICY_EXISTS=$(az keyvault show --name "$KEY_VAULT" --resource-group "$RESOURCE_GROUP" --query "properties.accessPolicies[?objectId=='$IDENTITY']" -o tsv 2>/dev/null || echo "")
        if [ -n "$POLICY_EXISTS" ]; then
            echo "✅ Key Vault access policy configured for Web App"
        else
            echo "⚠️  Key Vault access policy missing for Web App - attempting fix..."
            
            # Attempt to add access policy
            if az keyvault set-policy --name "$KEY_VAULT" --object-id "$IDENTITY" --secret-permissions get list 2>/dev/null; then
                echo "✅ Key Vault access policy added successfully"
            else
                echo "❌ Failed to add Key Vault access policy"
            fi
        fi
    fi
    
    # Check secrets
    echo "🔍 Validating Key Vault secrets..."
    EXPECTED_SECRETS=("JwtSecret" "ReplicateApiToken" "ReplicateWebhookSecret" "DatabaseConnectionString")
    
    for secret in "${EXPECTED_SECRETS[@]}"; do
        if az keyvault secret show --vault-name "$KEY_VAULT" --name "$secret" --output none 2>/dev/null; then
            echo "✅ Secret exists: $secret"
        else
            echo "⚠️  Secret missing: $secret"
        fi
    done
else
    echo "❌ Key Vault not found"
fi

# SQL Server
SQL_SERVER=$(az sql server list --resource-group "$RESOURCE_GROUP" --query "[0].name" -o tsv 2>/dev/null || echo "")
if [ -n "$SQL_SERVER" ]; then
    echo "✅ SQL Server: $SQL_SERVER"
    
    # Check database
    SQL_DB=$(az sql db list --server "$SQL_SERVER" --resource-group "$RESOURCE_GROUP" --query "[?name!='master'].name" -o tsv 2>/dev/null || echo "")
    if [ -n "$SQL_DB" ]; then
        echo "✅ SQL Database: $SQL_DB"
    else
        echo "❌ SQL Database not found"
    fi
else
    echo "❌ SQL Server not found"
fi

# Storage Account
STORAGE=$(az storage account list --resource-group "$RESOURCE_GROUP" --query "[0].name" -o tsv 2>/dev/null || echo "")
if [ -n "$STORAGE" ]; then
    echo "✅ Storage Account: $STORAGE"
else
    echo "❌ Storage Account not found"
fi

# Redis Cache
REDIS=$(az redis list --resource-group "$RESOURCE_GROUP" --query "[0].name" -o tsv 2>/dev/null || echo "")
if [ -n "$REDIS" ]; then
    echo "✅ Redis Cache: $REDIS"
else
    echo "❌ Redis Cache not found"
fi

# Static Web App
SWA=$(az staticwebapp list --resource-group "$RESOURCE_GROUP" --query "[0].name" -o tsv 2>/dev/null || echo "")
if [ -n "$SWA" ]; then
    echo "✅ Static Web App: $SWA"
else
    echo "❌ Static Web App not found"
fi

# Application Insights
APP_INSIGHTS=$(az monitor app-insights component show --resource-group "$RESOURCE_GROUP" --query "[0].name" -o tsv 2>/dev/null || echo "")
if [ -n "$APP_INSIGHTS" ]; then
    echo "✅ Application Insights: $APP_INSIGHTS"
else
    echo "❌ Application Insights not found"
fi

echo ""
echo "📊 Deployment validation complete!"
echo "🔧 If issues were found, consider re-running the deployment with corrected parameters."