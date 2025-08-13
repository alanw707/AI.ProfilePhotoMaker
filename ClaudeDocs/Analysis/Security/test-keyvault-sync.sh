#!/bin/bash
set -euo pipefail

# Simplified test version of the Key Vault sync script
# This version focuses on core functionality without complex error handling

echo "🔐 Testing Azure Key Vault to dotnet user-secrets sync..."
echo "======================================================="

# Configuration
KEYVAULT_NAME="aipm-kv-v1-6j74jubocuukg"
PROJECT_PATH="AI.ProfilePhotoMaker.API"

echo "📋 Checking prerequisites..."

# Check Azure auth
if ! az account show &>/dev/null; then
    echo "❌ Not authenticated to Azure"
    exit 1
fi
echo "✅ Azure authenticated"

# Check project
if [[ ! -f "$PROJECT_PATH/$PROJECT_PATH.csproj" ]]; then
    echo "❌ Project not found"
    exit 1
fi
echo "✅ Project found"

# Initialize user-secrets if needed
if ! dotnet user-secrets list --project "$PROJECT_PATH" &>/dev/null; then
    echo "🔧 Initializing user-secrets..."
    dotnet user-secrets init --project "$PROJECT_PATH"
fi
echo "✅ User-secrets ready"

echo ""
echo "📥 Retrieving secrets from Key Vault..."

# Get Replicate API Token
echo "  - Getting Replicate API Token..."
REPLICATE_TOKEN=$(az keyvault secret show --vault-name "$KEYVAULT_NAME" --name "ReplicateApiToken" --query "value" -o tsv)
if [[ -z "$REPLICATE_TOKEN" ]]; then
    echo "❌ Failed to get Replicate API Token"
    exit 1
fi
echo "  ✅ Got Replicate API Token (${#REPLICATE_TOKEN} chars)"

# Get Webhook Secret
echo "  - Getting Webhook Secret..."
WEBHOOK_SECRET=$(az keyvault secret show --vault-name "$KEYVAULT_NAME" --name "ReplicateWebhookSecret" --query "value" -o tsv)
if [[ -z "$WEBHOOK_SECRET" ]]; then
    echo "❌ Failed to get Webhook Secret"
    exit 1
fi
echo "  ✅ Got Webhook Secret (${#WEBHOOK_SECRET} chars)"

echo ""
echo "🔄 Setting user-secrets..."

# Set the secrets
dotnet user-secrets set "Replicate:ApiToken" "$REPLICATE_TOKEN" --project "$PROJECT_PATH"
dotnet user-secrets set "Replicate:WebhookSecret" "$WEBHOOK_SECRET" --project "$PROJECT_PATH"

echo ""
echo "🔍 Verifying secrets..."
dotnet user-secrets list --project "$PROJECT_PATH" | grep -i replicate

echo ""
echo "🎉 Test sync completed successfully!"

# Clear sensitive variables
unset REPLICATE_TOKEN WEBHOOK_SECRET