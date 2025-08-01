#!/bin/bash

# Local Azure Infrastructure Deployment Script
# Bypasses GitHub Actions Azure CLI API issues

set -e

echo "🚀 Starting local Azure infrastructure deployment..."

# Configuration
ENVIRONMENT="staging"
RESOURCE_GROUP="ai-profile-photo-maker-staging"
LOCATION="East US"

# Check if Azure CLI is logged in
if ! az account show > /dev/null 2>&1; then
    echo "❌ Azure CLI not logged in. Please run: az login"
    exit 1
fi

echo "✅ Azure CLI authenticated"

# Get current subscription
SUBSCRIPTION=$(az account show --query name --output tsv)
echo "📋 Using subscription: $SUBSCRIPTION"

# Ensure resource group exists
echo "🔍 Checking resource group: $RESOURCE_GROUP"
if az group show --name "$RESOURCE_GROUP" --output none 2>/dev/null; then
    echo "✅ Resource group exists"
else
    echo "⏳ Creating resource group..."
    az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output table
fi

# Prepare parameters file with secrets
echo "🔧 Preparing deployment parameters..."
cd "$(dirname "$0")"

# Create parameters file with real values
cat > parameters.staging.local.json << EOF
{
  "\$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "namePrefix": {
      "value": "aiprofilephotomaker"
    },
    "environmentName": {
      "value": "staging"
    },
    "sqlAdminUsername": {
      "value": "aiprofileadmin"
    },
    "sqlAdminPassword": {
      "value": "AzureSQL#2024#Staging!67Px"
    },
    "jwtSecret": {
      "value": "e7b7c0c2-4b6e-4c2d-8e7a-1b2f3c4d5e6f"
    },
    "replicateApiToken": {
      "value": "r8_FvCjahczdLfNmFTcDjhMzMuEuqKQswx2BxWD1"
    },
    "replicateWebhookSecret": {
      "value": "whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM"
    }
  }
}
EOF

echo "✅ Parameters file prepared"

# Convert Bicep to ARM template (alternative approach)
echo "🔄 Converting Bicep to ARM template..."
./bicep build main.bicep --outfile main.json

if [ $? -eq 0 ]; then
    echo "✅ ARM template generated successfully"
else
    echo "❌ Bicep compilation failed"
    exit 1
fi

# Deploy using ARM template
echo "🚀 Deploying infrastructure..."
echo "   This may take 10-15 minutes..."

DEPLOYMENT_NAME="infrastructure-$(date +%Y%m%d-%H%M%S)"

az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --template-file main.json \
    --parameters @parameters.staging.local.json \
    --name "$DEPLOYMENT_NAME" \
    --output table

if [ $? -eq 0 ]; then
    echo ""
    echo "🎉 Deployment completed successfully!"
    echo ""
    echo "📊 Deployment Summary:"
    echo "   Resource Group: $RESOURCE_GROUP"
    echo "   Deployment Name: $DEPLOYMENT_NAME"
    echo "   Environment: $ENVIRONMENT"
    echo ""
    echo "🔗 Next Steps:"
    echo "1. Verify resources in Azure Portal"
    echo "2. Get Static Web App deployment token:"
    echo "   az staticwebapp secrets list --name aiprofilephotomaker-swa-staging --resource-group $RESOURCE_GROUP --query properties.apiKey -o tsv"
    echo "3. Configure frontend deployment with the token"
    echo ""
    echo "✅ Azure infrastructure is ready!"
else
    echo ""
    echo "❌ Deployment failed. Check the error messages above."
    echo "💡 You can also try deploying via Azure Portal:"
    echo "   1. Upload main.json as template"
    echo "   2. Upload parameters.staging.local.json as parameters"
    echo "   3. Deploy to resource group: $RESOURCE_GROUP"
    exit 1
fi

# Cleanup sensitive files
echo "🧹 Cleaning up temporary files..."
rm -f parameters.staging.local.json main.json

echo "🎯 Local deployment script completed!"