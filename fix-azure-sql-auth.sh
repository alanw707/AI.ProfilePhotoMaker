#!/bin/bash

# Fix Azure SQL Authentication for Container Apps
# This script corrects the SQL authentication configuration and deploys the fix

set -e

echo "═══════════════════════════════════════════════════════════════════"
echo "  Azure SQL Authentication Fix Deployment"
echo "  Date: $(date)"
echo "═══════════════════════════════════════════════════════════════════"

# Configuration
RESOURCE_GROUP="aiprofilemaker-v1"
CONTAINER_APP_NAME="aipm-api-v1"
SQL_SERVER_NAME="aipm-sql-v1-6j74jubocuukg"
SQL_DATABASE_NAME="aipmdb"
SQL_ADMIN_USER="sqladmin"
ACR_NAME="aipmcrv16j74jubocuukg"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

# Check Azure CLI is logged in
echo "Checking Azure CLI authentication..."
if ! az account show &>/dev/null; then
    print_error "Not logged in to Azure CLI"
    echo "Please run: az login"
    exit 1
fi
print_status "Azure CLI authenticated"

# Get current subscription
SUBSCRIPTION=$(az account show --query name -o tsv)
echo "Using subscription: $SUBSCRIPTION"

# Step 1: Verify SQL Server configuration
echo ""
echo "Step 1: Verifying SQL Server configuration..."
echo "───────────────────────────────────────────"

SQL_SERVER_EXISTS=$(az sql server show \
    --name $SQL_SERVER_NAME \
    --resource-group $RESOURCE_GROUP \
    --query "name" -o tsv 2>/dev/null || echo "")

if [ -z "$SQL_SERVER_EXISTS" ]; then
    print_error "SQL Server $SQL_SERVER_NAME not found"
    exit 1
fi

print_status "SQL Server found: $SQL_SERVER_NAME"

# Get SQL Server FQDN
SQL_SERVER_FQDN=$(az sql server show \
    --name $SQL_SERVER_NAME \
    --resource-group $RESOURCE_GROUP \
    --query "fullyQualifiedDomainName" -o tsv)

echo "SQL Server FQDN: $SQL_SERVER_FQDN"

# Step 2: Update firewall rules to allow Azure services
echo ""
echo "Step 2: Updating SQL Server firewall rules..."
echo "───────────────────────────────────────────"

# Check if Azure services firewall rule exists
FIREWALL_RULE_EXISTS=$(az sql server firewall-rule show \
    --server $SQL_SERVER_NAME \
    --resource-group $RESOURCE_GROUP \
    --name "AllowAzureServices" \
    --query "name" -o tsv 2>/dev/null || echo "")

if [ -z "$FIREWALL_RULE_EXISTS" ]; then
    echo "Creating firewall rule for Azure services..."
    az sql server firewall-rule create \
        --server $SQL_SERVER_NAME \
        --resource-group $RESOURCE_GROUP \
        --name "AllowAzureServices" \
        --start-ip-address 0.0.0.0 \
        --end-ip-address 0.0.0.0 \
        --output none
    print_status "Firewall rule created for Azure services"
else
    print_status "Firewall rule for Azure services already exists"
fi

# Get Container App outbound IPs
echo "Getting Container App outbound IPs..."
CONTAINER_APP_IPS=$(az containerapp show \
    --name $CONTAINER_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --query "properties.outboundIpAddresses[]" -o tsv 2>/dev/null || echo "")

if [ ! -z "$CONTAINER_APP_IPS" ]; then
    for IP in $CONTAINER_APP_IPS; do
        RULE_NAME="ContainerApp-$(echo $IP | tr '.' '-')"
        echo "Adding firewall rule for IP: $IP"
        az sql server firewall-rule create \
            --server $SQL_SERVER_NAME \
            --resource-group $RESOURCE_GROUP \
            --name "$RULE_NAME" \
            --start-ip-address $IP \
            --end-ip-address $IP \
            --output none 2>/dev/null || print_warning "Rule for $IP may already exist"
    done
    print_status "Container App IP rules updated"
else
    print_warning "Could not retrieve Container App IPs"
fi

# Step 3: Test SQL authentication
echo ""
echo "Step 3: Testing SQL Server authentication..."
echo "───────────────────────────────────────────"

# Prompt for SQL password
echo "Enter SQL Admin password for user '$SQL_ADMIN_USER':"
read -s SQL_ADMIN_PASSWORD
echo ""

# Test connection using Azure Cloud Shell (if available)
echo "Testing SQL connection..."
CONNECTION_STRING="Server=$SQL_SERVER_FQDN;Database=$SQL_DATABASE_NAME;User ID=$SQL_ADMIN_USER;Password=$SQL_ADMIN_PASSWORD"

# We can't directly test from this script, but we'll verify the user exists
print_warning "Manual verification required: Please test the connection using Azure Cloud Shell"
echo ""
echo "Run this command in Azure Cloud Shell:"
echo "sqlcmd -S $SQL_SERVER_FQDN -d $SQL_DATABASE_NAME -U $SQL_ADMIN_USER -P '<your-password>' -Q 'SELECT 1'"

# Step 4: Update Container App configuration
echo ""
echo "Step 4: Updating Container App configuration..."
echo "───────────────────────────────────────────"

# Build the correct connection string
CONNECTION_STRING="Server=tcp:$SQL_SERVER_FQDN,1433;Initial Catalog=$SQL_DATABASE_NAME;User ID=$SQL_ADMIN_USER;Password=$SQL_ADMIN_PASSWORD;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;MultipleActiveResultSets=true;Max Pool Size=100;Min Pool Size=5;"

# Update Container App secret
echo "Updating connection string secret..."
az containerapp secret set \
    --name $CONTAINER_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --secrets "connection-string=$CONNECTION_STRING" \
    --output none

print_status "Connection string secret updated"

# Update environment variables to ensure proper mapping
echo "Updating environment variables..."
az containerapp update \
    --name $CONTAINER_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --set-env-vars \
        "ConnectionStrings__DefaultConnection=secretref:connection-string" \
        "Database__AutoMigrateOnStartup=false" \
        "Database__ValidateOnStartup=true" \
        "Database__MaxRetryCount=5" \
        "Database__MaxRetryDelaySeconds=30" \
    --output none

print_status "Environment variables updated"

# Step 5: Build and deploy updated application
echo ""
echo "Step 5: Building and deploying application..."
echo "───────────────────────────────────────────"

# Update the DatabaseProviderService registration if using enhanced version
echo "Updating service registration..."
cd AI.ProfilePhotoMaker.API

# Check if we need to update the service registration
if grep -q "DatabaseProviderService" Extensions/DatabaseServiceExtensions.cs; then
    print_warning "Manual update required: Update DatabaseServiceExtensions.cs to use EnhancedDatabaseProviderService"
    echo "Change line 19 from:"
    echo "  services.AddSingleton<IDatabaseProviderService, DatabaseProviderService>();"
    echo "To:"
    echo "  services.AddSingleton<IDatabaseProviderService, EnhancedDatabaseProviderService>();"
    echo ""
    echo "Press Enter after making this change..."
    read
fi

# Build Docker image
echo "Building Docker image..."
docker build -t $ACR_NAME.azurecr.io/aipm-api-v1:latest -f Dockerfile .
print_status "Docker image built"

# Push to ACR
echo "Pushing image to Azure Container Registry..."
az acr login --name $ACR_NAME
docker push $ACR_NAME.azurecr.io/aipm-api-v1:latest
print_status "Image pushed to ACR"

# Step 6: Deploy new revision
echo ""
echo "Step 6: Deploying new Container App revision..."
echo "───────────────────────────────────────────"

# Update Container App with new image
az containerapp update \
    --name $CONTAINER_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --image $ACR_NAME.azurecr.io/aipm-api-v1:latest \
    --output none

print_status "New revision deployed"

# Get the latest revision name
LATEST_REVISION=$(az containerapp revision list \
    --name $CONTAINER_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --query "[0].name" -o tsv)

echo "Latest revision: $LATEST_REVISION"

# Step 7: Monitor deployment
echo ""
echo "Step 7: Monitoring deployment..."
echo "───────────────────────────────────────────"

# Wait for revision to be ready
echo "Waiting for revision to become ready..."
RETRY_COUNT=0
MAX_RETRIES=30

while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    REVISION_STATUS=$(az containerapp revision show \
        --name $LATEST_REVISION \
        --app $CONTAINER_APP_NAME \
        --resource-group $RESOURCE_GROUP \
        --query "properties.runningState" -o tsv 2>/dev/null || echo "Unknown")
    
    if [ "$REVISION_STATUS" == "Running" ]; then
        print_status "Revision is running"
        break
    fi
    
    echo "Current status: $REVISION_STATUS (attempt $((RETRY_COUNT + 1))/$MAX_RETRIES)"
    sleep 10
    RETRY_COUNT=$((RETRY_COUNT + 1))
done

if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
    print_error "Revision did not become ready in time"
    exit 1
fi

# Step 8: Test health endpoints
echo ""
echo "Step 8: Testing health endpoints..."
echo "───────────────────────────────────────────"

# Get Container App FQDN
APP_FQDN=$(az containerapp show \
    --name $CONTAINER_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --query "properties.configuration.ingress.fqdn" -o tsv)

echo "Testing: https://$APP_FQDN/api/health/live"
HEALTH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" https://$APP_FQDN/api/health/live --max-time 10 || echo "timeout")

if [ "$HEALTH_STATUS" == "200" ]; then
    print_status "Liveness endpoint responding (HTTP 200)"
else
    print_warning "Liveness endpoint returned: $HEALTH_STATUS"
fi

echo "Testing: https://$APP_FQDN/api/health/ready"
READY_STATUS=$(curl -s -o /dev/null -w "%{http_code}" https://$APP_FQDN/api/health/ready --max-time 10 || echo "timeout")

if [ "$READY_STATUS" == "200" ]; then
    print_status "Readiness endpoint responding (HTTP 200)"
else
    print_warning "Readiness endpoint returned: $READY_STATUS"
fi

# Step 9: Show logs
echo ""
echo "Step 9: Recent container logs..."
echo "───────────────────────────────────────────"

az containerapp logs show \
    --name $CONTAINER_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --follow false \
    --tail 20

echo ""
echo "═══════════════════════════════════════════════════════════════════"
echo "  Deployment Complete"
echo "═══════════════════════════════════════════════════════════════════"
echo ""
echo "Summary:"
echo "  • SQL Server: $SQL_SERVER_FQDN"
echo "  • Database: $SQL_DATABASE_NAME"
echo "  • User: $SQL_ADMIN_USER"
echo "  • Container App: https://$APP_FQDN"
echo "  • Latest Revision: $LATEST_REVISION"
echo ""
echo "Next Steps:"
echo "  1. Monitor health endpoints for stability"
echo "  2. Check application logs for any errors"
echo "  3. Test API functionality"
echo ""
echo "To view logs:"
echo "  az containerapp logs show --name $CONTAINER_APP_NAME --resource-group $RESOURCE_GROUP --follow"
echo ""
echo "To check revision status:"
echo "  az containerapp revision list --name $CONTAINER_APP_NAME --resource-group $RESOURCE_GROUP"