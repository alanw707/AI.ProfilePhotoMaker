#!/bin/bash

# Reliable Local Deployment Script for AI Profile Photo Maker
# This script provides a bulletproof local deployment method

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
ENVIRONMENT=${1:-"staging"}
RESOURCE_GROUP="ai-profile-photo-maker-${ENVIRONMENT}"
LOCATION="East US 2"
DEPLOYMENT_NAME="local-deploy-$(date +%Y%m%d-%H%M%S)"

# Logging function
log() {
    echo -e "${BLUE}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1"
}

success() {
    echo -e "${GREEN}✅ $1${NC}"
}

warning() {
    echo -e "${YELLOW}⚠️ $1${NC}"
}

error() {
    echo -e "${RED}❌ $1${NC}"
    exit 1
}

# Pre-flight checks
preflight_checks() {
    log "Running pre-flight checks..."
    
    # Check Azure CLI
    if ! command -v az &> /dev/null; then
        error "Azure CLI not found. Please install it first."
    fi
    
    # Check authentication
    if ! az account show &> /dev/null; then
        error "Not authenticated with Azure. Run 'az login' first."
    fi
    
    # Check Bicep CLI
    if ! command -v bicep &> /dev/null; then
        warning "Bicep CLI not found. Installing..."
        az bicep install
    fi
    
    success "Pre-flight checks passed"
}

# Build templates
build_templates() {
    log "Building Bicep templates..."
    
    cd infrastructure
    
    # Build main template
    if bicep build main.bicep --outfile main.json; then
        success "Bicep template built successfully"
    else
        error "Failed to build Bicep template"
    fi
    
    # Verify parameter file exists
    if [ ! -f "parameters.${ENVIRONMENT}.json" ]; then
        error "Parameter file not found: parameters.${ENVIRONMENT}.json"
    fi
    
    success "Templates prepared"
    cd ..
}

# Create or verify resource group
setup_resource_group() {
    log "Setting up resource group: ${RESOURCE_GROUP}"
    
    if az group exists --name "${RESOURCE_GROUP}"; then
        success "Resource group already exists"
    else
        log "Creating resource group..."
        az group create \
            --name "${RESOURCE_GROUP}" \
            --location "${LOCATION}" \
            --tags Environment="${ENVIRONMENT}" Application=AI-ProfilePhotoMaker CreatedBy=LocalScript
        success "Resource group created"
    fi
}

# Deploy infrastructure with robust error handling
deploy_infrastructure() {
    log "Starting infrastructure deployment..."
    
    cd infrastructure
    
    # Deploy with comprehensive error handling
    local MAX_RETRIES=3
    local RETRY_COUNT=0
    local DEPLOYMENT_SUCCESS=false
    
    while [ $RETRY_COUNT -lt $MAX_RETRIES ] && [ "$DEPLOYMENT_SUCCESS" = "false" ]; do
        log "Deployment attempt $((RETRY_COUNT + 1))/${MAX_RETRIES}..."
        
        if timeout 30m az deployment group create \
            --resource-group "${RESOURCE_GROUP}" \
            --name "${DEPLOYMENT_NAME}-attempt-$((RETRY_COUNT + 1))" \
            --template-file main.json \
            --parameters @parameters.${ENVIRONMENT}.json \
            --output table \
            --verbose; then
            
            DEPLOYMENT_SUCCESS=true
            success "Infrastructure deployed successfully"
        else
            RETRY_COUNT=$((RETRY_COUNT + 1))
            warning "Deployment attempt $RETRY_COUNT failed"
            
            if [ $RETRY_COUNT -lt $MAX_RETRIES ]; then
                warning "Retrying in 60 seconds..."
                sleep 60
            fi
        fi
    done
    
    if [ "$DEPLOYMENT_SUCCESS" = "false" ]; then
        error "Infrastructure deployment failed after ${MAX_RETRIES} attempts"
    fi
    
    cd ..
}

# Verify deployment and get resource information
verify_deployment() {
    log "Verifying deployment..."
    
    # Check if resources exist
    local RESOURCES=$(az resource list --resource-group "${RESOURCE_GROUP}" --query "length([])" -o tsv)
    
    if [ "${RESOURCES}" -gt 0 ]; then
        success "Found ${RESOURCES} resources in resource group"
        
        # List key resources
        log "Deployed resources:"
        az resource list --resource-group "${RESOURCE_GROUP}" \
            --query "[].{Name:name,Type:type,Status:properties.provisioningState}" \
            -o table
        
        # Get service URLs
        log "Getting service endpoints..."
        
        local API_URL=$(az webapp list --resource-group "${RESOURCE_GROUP}" \
            --query "[?contains(name, 'api')].defaultHostName | [0]" -o tsv 2>/dev/null || echo "")
        
        local SWA_URL=$(az staticwebapp list --resource-group "${RESOURCE_GROUP}" \
            --query "[0].defaultHostname" -o tsv 2>/dev/null || echo "")
        
        if [ -n "${API_URL}" ] && [ "${API_URL}" != "null" ]; then
            success "API URL: https://${API_URL}"
            echo "https://${API_URL}" > deployment-api-url.txt
        fi
        
        if [ -n "${SWA_URL}" ] && [ "${SWA_URL}" != "null" ]; then
            success "Frontend URL: https://${SWA_URL}"
            echo "https://${SWA_URL}" > deployment-frontend-url.txt
        fi
        
    else
        error "No resources found - deployment may have failed"
    fi
}

# Health check
health_check() {
    log "Running health checks..."
    
    if [ -f "deployment-api-url.txt" ]; then
        local API_URL=$(cat deployment-api-url.txt)
        log "Checking API health at ${API_URL}/health"
        
        # Give API time to start
        sleep 30
        
        if curl -f --max-time 30 "${API_URL}/health" &> /dev/null; then
            success "API health check passed"
        else
            warning "API health check failed (may still be starting)"
        fi
    fi
    
    success "Health checks completed"
}

# Cleanup function for failures
cleanup_on_failure() {
    warning "Cleaning up partial deployment..."
    
    # List resources for manual cleanup
    if az group exists --name "${RESOURCE_GROUP}"; then
        log "Resources that may need manual cleanup:"
        az resource list --resource-group "${RESOURCE_GROUP}" \
            --query "[].{Name:name,Type:type}" -o table || true
    fi
}

# Main execution
main() {
    log "Starting reliable local deployment for environment: ${ENVIRONMENT}"
    
    # Set trap for cleanup on failure
    trap cleanup_on_failure ERR
    
    preflight_checks
    build_templates
    setup_resource_group
    deploy_infrastructure
    verify_deployment
    health_check
    
    success "🎉 Deployment completed successfully!"
    log "Environment: ${ENVIRONMENT}"
    log "Resource Group: ${RESOURCE_GROUP}"
    
    if [ -f "deployment-api-url.txt" ]; then
        log "API URL: $(cat deployment-api-url.txt)"
    fi
    
    if [ -f "deployment-frontend-url.txt" ]; then
        log "Frontend URL: $(cat deployment-frontend-url.txt)"
    fi
    
    log "Next steps:"
    log "1. Deploy application code using GitHub Actions or manual deployment"
    log "2. Configure database and run migrations"
    log "3. Test end-to-end functionality"
}

# Show usage if no arguments
if [ $# -eq 0 ]; then
    echo "Usage: $0 [staging|production]"
    echo "Example: $0 staging"
    exit 1
fi

# Validate environment argument
if [[ ! "${ENVIRONMENT}" =~ ^(staging|production)$ ]]; then
    error "Invalid environment: ${ENVIRONMENT}. Use 'staging' or 'production'"
fi

# Run main function
main "$@"