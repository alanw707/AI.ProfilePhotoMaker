#!/bin/bash
# =============================================================================
# Deployment Validation Script
# Validates infrastructure, application health, and integration points
# =============================================================================

set -e

# Configuration
ENVIRONMENT=${1:-staging}
TIMEOUT=${2:-300}
RETRY_INTERVAL=${3:-10}

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Get deployment outputs
get_deployment_outputs() {
    log_info "Retrieving deployment outputs for environment: ${ENVIRONMENT}"
    
    RESOURCE_GROUP="rg-aiprofilemaker-${ENVIRONMENT}"
    
    # Get resource group deployment (latest)
    DEPLOYMENT_NAME=$(az deployment group list \
        --resource-group ${RESOURCE_GROUP} \
        --query "[0].name" -o tsv 2>/dev/null || echo "")
        
    if [[ -z "$DEPLOYMENT_NAME" ]]; then
        log_error "No deployments found in resource group: ${RESOURCE_GROUP}"
        return 1
    fi
    
    # Get outputs
    OUTPUTS=$(az deployment group show \
        --resource-group ${RESOURCE_GROUP} \
        --name ${DEPLOYMENT_NAME} \
        --query "properties.outputs" -o json 2>/dev/null || echo "{}")
        
    export BACKEND_URL=$(echo $OUTPUTS | jq -r '.backendUrl.value // empty')
    export FRONTEND_URL=$(echo $OUTPUTS | jq -r '.frontendUrl.value // empty')
    export REGISTRY_SERVER=$(echo $OUTPUTS | jq -r '.registryLoginServer.value // empty')
    export SQL_SERVER=$(echo $OUTPUTS | jq -r '.sqlServerFqdn.value // empty')
    export STORAGE_ACCOUNT=$(echo $OUTPUTS | jq -r '.storageAccountName.value // empty')
    export KEY_VAULT=$(echo $OUTPUTS | jq -r '.keyVaultName.value // empty')
    
    log_info "Deployment outputs retrieved:"
    log_info "  Backend URL: ${BACKEND_URL}"
    log_info "  Frontend URL: ${FRONTEND_URL}"
    log_info "  Registry: ${REGISTRY_SERVER}"
}

# Validate infrastructure resources
validate_infrastructure() {
    log_info "Validating infrastructure resources..."
    
    local validation_errors=0
    
    # Check resource group
    if az group show --name "rg-aiprofilemaker-${ENVIRONMENT}" >/dev/null 2>&1; then
        log_success "Resource group exists"
    else
        log_error "Resource group not found"
        ((validation_errors++))
    fi
    
    # Check container registry
    if [[ -n "$REGISTRY_SERVER" ]]; then
        REGISTRY_NAME=$(echo $REGISTRY_SERVER | cut -d'.' -f1)
        if az acr show --name ${REGISTRY_NAME} >/dev/null 2>&1; then
            log_success "Container registry accessible"
            
            # Check registry health
            if az acr check-health --name ${REGISTRY_NAME} --query "status" -o tsv | grep -q "healthy"; then
                log_success "Container registry is healthy"
            else
                log_warning "Container registry health check failed"
            fi
        else
            log_error "Container registry not accessible"
            ((validation_errors++))
        fi
    fi
    
    # Check SQL Server
    if [[ -n "$SQL_SERVER" ]]; then
        if az sql server show --name ${SQL_SERVER%.*} --resource-group "rg-aiprofilemaker-${ENVIRONMENT}" >/dev/null 2>&1; then
            log_success "SQL Server accessible"
        else
            log_error "SQL Server not accessible"
            ((validation_errors++))
        fi
    fi
    
    # Check Key Vault
    if [[ -n "$KEY_VAULT" ]]; then
        if az keyvault show --name ${KEY_VAULT} >/dev/null 2>&1; then
            log_success "Key Vault accessible"
        else
            log_error "Key Vault not accessible"
            ((validation_errors++))
        fi
    fi
    
    # Check Storage Account
    if [[ -n "$STORAGE_ACCOUNT" ]]; then
        if az storage account show --name ${STORAGE_ACCOUNT} --resource-group "rg-aiprofilemaker-${ENVIRONMENT}" >/dev/null 2>&1; then
            log_success "Storage Account accessible"
        else
            log_error "Storage Account not accessible"
            ((validation_errors++))
        fi
    fi
    
    return $validation_errors
}

# Wait for endpoint to be ready
wait_for_endpoint() {
    local url=$1
    local description=$2
    local timeout=${3:-$TIMEOUT}
    
    log_info "Waiting for ${description} to be ready: ${url}"
    
    local counter=0
    while [ $counter -lt $timeout ]; do
        if curl -f -s --connect-timeout 10 --max-time 30 "${url}" >/dev/null 2>&1; then
            log_success "${description} is ready (${counter}s)"
            return 0
        fi
        
        log_info "Waiting for ${description}... (${counter}s/${timeout}s)"
        sleep $RETRY_INTERVAL
        counter=$((counter + RETRY_INTERVAL))
    done
    
    log_error "${description} failed to become ready within ${timeout}s"
    return 1
}

# Validate application health
validate_application_health() {
    log_info "Validating application health..."
    
    local health_errors=0
    
    # Backend health check
    if [[ -n "$BACKEND_URL" ]]; then
        if wait_for_endpoint "${BACKEND_URL}/health" "Backend API"; then
            # Detailed health check
            HEALTH_RESPONSE=$(curl -s "${BACKEND_URL}/health" -H "Accept: application/json" 2>/dev/null || echo "{}")
            HEALTH_STATUS=$(echo $HEALTH_RESPONSE | jq -r '.status // "unknown"')
            
            if [[ "$HEALTH_STATUS" == "healthy" ]]; then
                log_success "Backend health check passed"
                
                # Database connectivity check
                if curl -f -s "${BACKEND_URL}/api/health/database" >/dev/null 2>&1; then
                    log_success "Database connectivity verified"
                else
                    log_error "Database connectivity check failed"
                    ((health_errors++))
                fi
                
                # Storage connectivity check
                if curl -f -s "${BACKEND_URL}/api/health/storage" >/dev/null 2>&1; then
                    log_success "Storage connectivity verified"
                else
                    log_warning "Storage connectivity check failed (non-critical)"
                fi
                
            else
                log_error "Backend health check failed - Status: ${HEALTH_STATUS}"
                echo "Response: ${HEALTH_RESPONSE}"
                ((health_errors++))
            fi
        else
            log_error "Backend API is not accessible"
            ((health_errors++))
        fi
    else
        log_error "Backend URL not available"
        ((health_errors++))
    fi
    
    # Frontend health check
    if [[ -n "$FRONTEND_URL" ]]; then
        if wait_for_endpoint "${FRONTEND_URL}" "Frontend Application"; then
            # Check if it returns HTML
            CONTENT_TYPE=$(curl -s -I "${FRONTEND_URL}" | grep -i "content-type" | head -1)
            if [[ "$CONTENT_TYPE" == *"text/html"* ]]; then
                log_success "Frontend health check passed"
            else
                log_warning "Frontend returned unexpected content type: ${CONTENT_TYPE}"
            fi
        else
            log_error "Frontend application is not accessible"
            ((health_errors++))
        fi
    else
        log_error "Frontend URL not available"
        ((health_errors++))
    fi
    
    return $health_errors
}

# Validate API integration
validate_api_integration() {
    log_info "Validating API integration..."
    
    local integration_errors=0
    
    if [[ -z "$BACKEND_URL" ]]; then
        log_error "Backend URL not available for integration testing"
        return 1
    fi
    
    # Test public API endpoints
    ENDPOINTS=(
        "/api/styles"
        "/api/health"
        "/api/credit-packages"
    )
    
    for endpoint in "${ENDPOINTS[@]}"; do
        URL="${BACKEND_URL}${endpoint}"
        log_info "Testing endpoint: ${endpoint}"
        
        if curl -f -s --connect-timeout 10 --max-time 30 "${URL}" -H "Accept: application/json" >/dev/null; then
            log_success "✓ ${endpoint}"
        else
            log_error "✗ ${endpoint}"
            ((integration_errors++))
        fi
    done
    
    # Test error handling
    ERROR_URL="${BACKEND_URL}/api/nonexistent"
    if [[ $(curl -s -o /dev/null -w "%{http_code}" "${ERROR_URL}") == "404" ]]; then
        log_success "✓ Error handling working (404 for non-existent endpoint)"
    else
        log_warning "Error handling may not be working correctly"
    fi
    
    return $integration_errors
}

# Performance baseline check
validate_performance() {
    log_info "Validating performance baseline..."
    
    if [[ -z "$BACKEND_URL" ]]; then
        log_warning "Backend URL not available for performance testing"
        return 0
    fi
    
    # Measure response times
    log_info "Measuring API response times..."
    
    RESPONSE_TIME=$(curl -w "%{time_total}" -o /dev/null -s "${BACKEND_URL}/health")
    RESPONSE_TIME_MS=$(echo "$RESPONSE_TIME * 1000" | bc -l | cut -d'.' -f1)
    
    log_info "API response time: ${RESPONSE_TIME_MS}ms"
    
    # Performance thresholds
    if [[ $RESPONSE_TIME_MS -lt 2000 ]]; then
        log_success "API response time within acceptable range (<2000ms)"
        return 0
    elif [[ $RESPONSE_TIME_MS -lt 5000 ]]; then
        log_warning "API response time is slow (${RESPONSE_TIME_MS}ms)"
        return 0
    else
        log_error "API response time is unacceptable (${RESPONSE_TIME_MS}ms)"
        return 1
    fi
}

# Generate validation report
generate_report() {
    local total_errors=$1
    
    echo ""
    echo "=============================================="
    echo "         DEPLOYMENT VALIDATION REPORT"
    echo "=============================================="
    echo "Environment: ${ENVIRONMENT}"
    echo "Timestamp: $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
    echo "Validation Duration: ${SECONDS}s"
    echo ""
    
    if [[ $total_errors -eq 0 ]]; then
        echo -e "${GREEN}✅ VALIDATION PASSED${NC}"
        echo "All validation checks completed successfully."
    else
        echo -e "${RED}❌ VALIDATION FAILED${NC}"
        echo "Found ${total_errors} validation errors."
        echo "Please review the errors above and fix them before proceeding."
    fi
    
    echo ""
    echo "Application URLs:"
    echo "  Frontend: ${FRONTEND_URL:-'Not Available'}"
    echo "  Backend:  ${BACKEND_URL:-'Not Available'}"
    echo ""
    echo "=============================================="
}

# Main validation flow
main() {
    local total_errors=0
    
    log_info "Starting deployment validation for environment: ${ENVIRONMENT}"
    log_info "Timeout: ${TIMEOUT}s, Retry Interval: ${RETRY_INTERVAL}s"
    
    # Get deployment information
    if ! get_deployment_outputs; then
        log_error "Failed to retrieve deployment outputs"
        exit 1
    fi
    
    # Run validation checks
    log_info "Running infrastructure validation..."
    validate_infrastructure || ((total_errors += $?))
    
    log_info "Running application health validation..."
    validate_application_health || ((total_errors += $?))
    
    log_info "Running API integration validation..."
    validate_api_integration || ((total_errors += $?))
    
    log_info "Running performance validation..."
    validate_performance || ((total_errors += $?))
    
    # Generate report
    generate_report $total_errors
    
    # Exit with appropriate code
    if [[ $total_errors -eq 0 ]]; then
        exit 0
    else
        exit 1
    fi
}

# Handle script arguments
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi