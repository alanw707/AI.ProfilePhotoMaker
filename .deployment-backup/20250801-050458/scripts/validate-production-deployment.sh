#!/bin/bash

# Production Deployment Validation Script for AI Profile Photo Maker
# This script validates the production deployment after infrastructure and application deployment

set -e

# Configuration
RESOURCE_GROUP_NAME="ai-profile-photo-maker-prod"
ENVIRONMENT="prod"
TIMEOUT_SECONDS=600
MAX_RETRIES=10

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check if Azure CLI is logged in
check_azure_login() {
    print_status "Checking Azure CLI login status..."
    if ! az account show &> /dev/null; then
        print_error "Not logged in to Azure. Please run 'az login' first."
        exit 1
    fi
    print_success "Azure CLI login verified"
}

# Function to validate resource group exists
validate_resource_group() {
    print_status "Validating resource group: $RESOURCE_GROUP_NAME"
    if ! az group show --name "$RESOURCE_GROUP_NAME" --output none 2>/dev/null; then
        print_error "Resource group $RESOURCE_GROUP_NAME not found"
        exit 1
    fi
    print_success "Resource group validated"
}

# Function to get resource names
get_resource_names() {
    print_status "Retrieving resource names..."
    
    # Get Web App name
    WEB_APP_NAME=$(az webapp list --resource-group "$RESOURCE_GROUP_NAME" --query "[0].name" -o tsv)
    if [ -z "$WEB_APP_NAME" ] || [ "$WEB_APP_NAME" = "null" ]; then
        print_error "Web App not found in resource group"
        exit 1
    fi
    
    # Get Static Web App name
    SWA_NAME=$(az staticwebapp list --resource-group "$RESOURCE_GROUP_NAME" --query "[0].name" -o tsv 2>/dev/null || echo "")
    
    # Get SQL Server name
    SQL_SERVER_NAME=$(az sql server list --resource-group "$RESOURCE_GROUP_NAME" --query "[0].name" -o tsv)
    
    # Get Storage Account name
    STORAGE_ACCOUNT_NAME=$(az storage account list --resource-group "$RESOURCE_GROUP_NAME" --query "[0].name" -o tsv)
    
    # Get Key Vault name
    KEY_VAULT_NAME=$(az keyvault list --resource-group "$RESOURCE_GROUP_NAME" --query "[0].name" -o tsv)
    
    # Get Redis Cache name
    REDIS_CACHE_NAME=$(az redis list --resource-group "$RESOURCE_GROUP_NAME" --query "[0].name" -o tsv 2>/dev/null || echo "")
    
    print_success "Resource names retrieved"
    echo "  • Web App: $WEB_APP_NAME"
    echo "  • Static Web App: $SWA_NAME"
    echo "  • SQL Server: $SQL_SERVER_NAME"
    echo "  • Storage Account: $STORAGE_ACCOUNT_NAME"
    echo "  • Key Vault: $KEY_VAULT_NAME"
    echo "  • Redis Cache: $REDIS_CACHE_NAME"
}

# Function to validate infrastructure resources
validate_infrastructure() {
    print_status "Validating infrastructure resources..."
    local validation_errors=0
    
    # Validate Web App
    print_status "Checking Web App: $WEB_APP_NAME"
    if az webapp show --name "$WEB_APP_NAME" --resource-group "$RESOURCE_GROUP_NAME" --output none 2>/dev/null; then
        # Check if Web App is running
        APP_STATE=$(az webapp show --name "$WEB_APP_NAME" --resource-group "$RESOURCE_GROUP_NAME" --query "state" -o tsv)
        if [ "$APP_STATE" = "Running" ]; then
            print_success "Web App is running"
        else
            print_warning "Web App state: $APP_STATE"
            validation_errors=$((validation_errors + 1))
        fi
    else
        print_error "Web App validation failed"
        validation_errors=$((validation_errors + 1))
    fi
    
    # Validate SQL Server and Database
    print_status "Checking SQL Server: $SQL_SERVER_NAME"
    if az sql server show --name "$SQL_SERVER_NAME" --resource-group "$RESOURCE_GROUP_NAME" --output none 2>/dev/null; then
        print_success "SQL Server is accessible"
        
        # Check database
        DB_NAME=$(az sql db list --server "$SQL_SERVER_NAME" --resource-group "$RESOURCE_GROUP_NAME" --query "[?name != 'master'].name" -o tsv | head -n1)
        if [ -n "$DB_NAME" ]; then
            print_success "Database found: $DB_NAME"
        else
            print_error "No application database found"
            validation_errors=$((validation_errors + 1))
        fi
    else
        print_error "SQL Server validation failed"
        validation_errors=$((validation_errors + 1))
    fi
    
    # Validate Storage Account
    print_status "Checking Storage Account: $STORAGE_ACCOUNT_NAME"
    if az storage account show --name "$STORAGE_ACCOUNT_NAME" --resource-group "$RESOURCE_GROUP_NAME" --output none 2>/dev/null; then
        print_success "Storage Account is accessible"
        
        # Check blob container
        STORAGE_KEY=$(az storage account keys list --account-name "$STORAGE_ACCOUNT_NAME" --resource-group "$RESOURCE_GROUP_NAME" --query "[0].value" -o tsv)
        if az storage container show --name "profile-images" --account-name "$STORAGE_ACCOUNT_NAME" --account-key "$STORAGE_KEY" --output none 2>/dev/null; then
            print_success "Profile images container exists"
        else
            print_warning "Profile images container not found"
        fi
    else
        print_error "Storage Account validation failed"
        validation_errors=$((validation_errors + 1))
    fi
    
    # Validate Key Vault
    print_status "Checking Key Vault: $KEY_VAULT_NAME"
    if az keyvault show --name "$KEY_VAULT_NAME" --output none 2>/dev/null; then
        print_success "Key Vault is accessible"
        
        # Check critical secrets
        required_secrets=("JwtSecret" "ReplicateApiToken" "DatabaseConnectionString")
        for secret in "${required_secrets[@]}"; do
            if az keyvault secret show --vault-name "$KEY_VAULT_NAME" --name "$secret" --output none 2>/dev/null; then
                print_success "Secret exists: $secret"
            else
                print_error "Secret missing: $secret"
                validation_errors=$((validation_errors + 1))
            fi
        done
    else
        print_error "Key Vault validation failed"
        validation_errors=$((validation_errors + 1))
    fi
    
    # Validate Redis Cache (if exists)
    if [ -n "$REDIS_CACHE_NAME" ] && [ "$REDIS_CACHE_NAME" != "null" ]; then
        print_status "Checking Redis Cache: $REDIS_CACHE_NAME"
        if az redis show --name "$REDIS_CACHE_NAME" --resource-group "$RESOURCE_GROUP_NAME" --output none 2>/dev/null; then
            REDIS_STATUS=$(az redis show --name "$REDIS_CACHE_NAME" --resource-group "$RESOURCE_GROUP_NAME" --query "provisioningState" -o tsv)
            if [ "$REDIS_STATUS" = "Succeeded" ]; then
                print_success "Redis Cache is provisioned"
            else
                print_warning "Redis Cache status: $REDIS_STATUS"
            fi
        else
            print_error "Redis Cache validation failed"
            validation_errors=$((validation_errors + 1))
        fi
    fi
    
    if [ $validation_errors -eq 0 ]; then
        print_success "Infrastructure validation completed successfully"
        return 0
    else
        print_error "Infrastructure validation failed with $validation_errors errors"
        return 1
    fi
}

# Function to validate application health
validate_application_health() {
    print_status "Validating application health..."
    
    local web_app_url="https://${WEB_APP_NAME}.azurewebsites.net"
    local retry_count=0
    local health_check_passed=false
    
    print_status "Testing application health endpoint: $web_app_url/health"
    
    while [ $retry_count -lt $MAX_RETRIES ] && [ "$health_check_passed" = "false" ]; do
        retry_count=$((retry_count + 1))
        print_status "Health check attempt $retry_count/$MAX_RETRIES..."
        
        # Test health endpoint
        if curl -f -s --max-time 30 "$web_app_url/health" > /dev/null 2>&1; then
            print_success "Health endpoint responded successfully"
            health_check_passed=true
        else
            if [ $retry_count -lt $MAX_RETRIES ]; then
                print_warning "Health check failed, retrying in 30 seconds..."
                sleep 30
            fi
        fi
    done
    
    if [ "$health_check_passed" = "false" ]; then
        print_error "Application health check failed after $MAX_RETRIES attempts"
        return 1
    fi
    
    # Test additional endpoints
    print_status "Testing additional API endpoints..."
    
    # Test API endpoints (non-critical)
    endpoints=("/api/health" "/swagger" "/api/profile/status")
    for endpoint in "${endpoints[@]}"; do
        print_status "Testing: $endpoint"
        if curl -f -s --max-time 10 "$web_app_url$endpoint" > /dev/null 2>&1; then
            print_success "Endpoint accessible: $endpoint"
        else
            print_warning "Endpoint not accessible or timed out: $endpoint"
        fi
    done
    
    return 0
}

# Function to validate static web app (if exists)
validate_static_web_app() {
    if [ -n "$SWA_NAME" ] && [ "$SWA_NAME" != "null" ]; then
        print_status "Validating Static Web App: $SWA_NAME"
        
        SWA_URL=$(az staticwebapp show --name "$SWA_NAME" --resource-group "$RESOURCE_GROUP_NAME" --query "defaultHostname" -o tsv 2>/dev/null)
        
        if [ -n "$SWA_URL" ] && [ "$SWA_URL" != "null" ]; then
            FULL_SWA_URL="https://$SWA_URL"
            print_status "Testing Static Web App: $FULL_SWA_URL"
            
            if curl -f -s --max-time 30 "$FULL_SWA_URL" > /dev/null 2>&1; then
                print_success "Static Web App is accessible"
            else
                print_warning "Static Web App may not be fully deployed yet"
            fi
        else
            print_warning "Static Web App URL not available"
        fi
    else
        print_status "Static Web App not configured or not found"
    fi
}

# Function to validate monitoring and alerts
validate_monitoring() {
    print_status "Validating monitoring and alerting setup..."
    
    # Check Application Insights
    APP_INSIGHTS_NAME=$(az monitor app-insights component list --resource-group "$RESOURCE_GROUP_NAME" --query "[0].name" -o tsv 2>/dev/null)
    if [ -n "$APP_INSIGHTS_NAME" ] && [ "$APP_INSIGHTS_NAME" != "null" ]; then
        print_success "Application Insights configured: $APP_INSIGHTS_NAME"
    else
        print_warning "Application Insights not found"
    fi
    
    # Check Action Groups
    ACTION_GROUPS=$(az monitor action-group list --resource-group "$RESOURCE_GROUP_NAME" --query "length(@)" -o tsv 2>/dev/null)
    if [ "$ACTION_GROUPS" -gt 0 ]; then
        print_success "Action Groups configured: $ACTION_GROUPS"
    else
        print_warning "No Action Groups found"
    fi
    
    # Check Metric Alerts
    METRIC_ALERTS=$(az monitor metrics alert list --resource-group "$RESOURCE_GROUP_NAME" --query "length(@)" -o tsv 2>/dev/null)
    if [ "$METRIC_ALERTS" -gt 0 ]; then
        print_success "Metric Alerts configured: $METRIC_ALERTS"
    else
        print_warning "No Metric Alerts found"
    fi
}

# Function to generate validation report
generate_report() {
    local exit_code=$1
    
    print_status "Generating validation report..."
    
    cat > deployment-validation-report.json << EOF
{
  "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "environment": "$ENVIRONMENT",
  "resourceGroup": "$RESOURCE_GROUP_NAME",
  "validationStatus": "$([ $exit_code -eq 0 ] && echo "SUCCESS" || echo "FAILED")",
  "resources": {
    "webApp": {
      "name": "$WEB_APP_NAME",
      "url": "https://${WEB_APP_NAME}.azurewebsites.net"
    },
    "staticWebApp": {
      "name": "$SWA_NAME"
    },
    "sqlServer": {
      "name": "$SQL_SERVER_NAME"
    },
    "storageAccount": {
      "name": "$STORAGE_ACCOUNT_NAME"
    },
    "keyVault": {
      "name": "$KEY_VAULT_NAME"
    },
    "redisCache": {
      "name": "$REDIS_CACHE_NAME"
    }
  },
  "validationSteps": [
    "Resource Group Validation",
    "Infrastructure Resource Validation",
    "Application Health Check",
    "Static Web App Validation",
    "Monitoring Configuration Validation"
  ]
}
EOF
    
    print_success "Validation report generated: deployment-validation-report.json"
}

# Main execution
main() {
    print_status "Starting production deployment validation..."
    print_status "Environment: $ENVIRONMENT"
    print_status "Resource Group: $RESOURCE_GROUP_NAME"
    print_status "Timeout: $TIMEOUT_SECONDS seconds"
    
    local exit_code=0
    
    # Run validation steps
    check_azure_login || exit_code=1
    
    if [ $exit_code -eq 0 ]; then
        validate_resource_group || exit_code=1
    fi
    
    if [ $exit_code -eq 0 ]; then
        get_resource_names || exit_code=1
    fi
    
    if [ $exit_code -eq 0 ]; then
        validate_infrastructure || exit_code=1
    fi
    
    if [ $exit_code -eq 0 ]; then
        validate_application_health || exit_code=1
    fi
    
    if [ $exit_code -eq 0 ]; then
        validate_static_web_app
        validate_monitoring
    fi
    
    # Generate report
    generate_report $exit_code
    
    if [ $exit_code -eq 0 ]; then
        print_success "🎉 Production deployment validation completed successfully!"
        print_success "Your AI Profile Photo Maker application is ready for production use."
        print_success "Application URL: https://${WEB_APP_NAME}.azurewebsites.net"
    else
        print_error "❌ Production deployment validation failed!"
        print_error "Please check the errors above and resolve them before proceeding."
    fi
    
    exit $exit_code
}

# Run main function
main "$@"