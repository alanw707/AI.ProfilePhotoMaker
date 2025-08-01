#!/bin/bash

# Comprehensive Deployment Validation Script
# Validates all aspects of the deployed infrastructure and application

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
NC='\033[0m' # No Color

# Configuration
ENVIRONMENT=${1:-"staging"}
RESOURCE_GROUP="ai-profile-photo-maker-${ENVIRONMENT}"
TIMEOUT=30
VERBOSE=${VERBOSE:-false}

# Counters
TOTAL_CHECKS=0
PASSED_CHECKS=0
FAILED_CHECKS=0
WARNING_CHECKS=0

# Logging functions
log() {
    echo -e "${BLUE}[$(date +'%H:%M:%S')]${NC} $1"
}

success() {
    echo -e "${GREEN}✅ $1${NC}"
    ((PASSED_CHECKS++))
}

warning() {
    echo -e "${YELLOW}⚠️ $1${NC}"
    ((WARNING_CHECKS++))
}

error() {
    echo -e "${RED}❌ $1${NC}"
    ((FAILED_CHECKS++))
}

info() {
    echo -e "${PURPLE}ℹ️ $1${NC}"
}

check() {
    ((TOTAL_CHECKS++))
    if [ "$VERBOSE" = "true" ]; then
        log "Running check: $1"
    fi
}

# Header
print_header() {
    echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
    echo -e "${BLUE}    AI Profile Photo Maker - Deployment Validation${NC}"
    echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
    echo
    info "Environment: ${ENVIRONMENT}"
    info "Resource Group: ${RESOURCE_GROUP}"
    info "Validation Started: $(date)"
    echo
}

# Azure Authentication Check
check_azure_auth() {
    log "Checking Azure CLI authentication..."
    check "Azure CLI Authentication"
    
    if az account show &> /dev/null; then
        local ACCOUNT_INFO=$(az account show --query "{subscription:name,tenant:tenantId}" -o table)
        success "Azure CLI authenticated"
        if [ "$VERBOSE" = "true" ]; then
            echo "$ACCOUNT_INFO"
        fi
    else
        error "Azure CLI not authenticated. Run 'az login' first."
        return 1
    fi
}

# Resource Group Validation
check_resource_group() {
    log "Validating resource group..."
    check "Resource Group Existence"
    
    if az group exists --name "${RESOURCE_GROUP}"; then
        success "Resource group '${RESOURCE_GROUP}' exists"
        
        # Get resource count
        local RESOURCE_COUNT=$(az resource list --resource-group "${RESOURCE_GROUP}" --query "length([])" -o tsv)
        info "Resources in group: ${RESOURCE_COUNT}"
        
        if [ "$VERBOSE" = "true" ]; then
            log "Resource details:"
            az resource list --resource-group "${RESOURCE_GROUP}" \
                --query "[].{Name:name,Type:type,Status:properties.provisioningState}" \
                -o table
        fi
    else
        error "Resource group '${RESOURCE_GROUP}' does not exist"
        return 1
    fi
}

# Infrastructure Resource Validation
check_infrastructure_resources() {
    log "Validating infrastructure resources..."
    
    # Check App Service Plan
    check "App Service Plan"
    local ASP=$(az appservice plan list --resource-group "${RESOURCE_GROUP}" --query "[0].name" -o tsv 2>/dev/null || echo "")
    if [ -n "${ASP}" ] && [ "${ASP}" != "null" ]; then
        success "App Service Plan found: ${ASP}"
    else
        error "App Service Plan not found"
    fi
    
    # Check Web App
    check "Web App (API)"
    local WEBAPP=$(az webapp list --resource-group "${RESOURCE_GROUP}" --query "[?contains(name, 'api')].name | [0]" -o tsv 2>/dev/null || echo "")
    if [ -n "${WEBAPP}" ] && [ "${WEBAPP}" != "null" ]; then
        success "Web App found: ${WEBAPP}"
        
        # Check Web App status
        local APP_STATE=$(az webapp show --name "${WEBAPP}" --resource-group "${RESOURCE_GROUP}" --query "state" -o tsv 2>/dev/null || echo "")
        if [ "${APP_STATE}" = "Running" ]; then
            success "Web App is running"
        else
            warning "Web App state: ${APP_STATE}"
        fi
    else
        error "Web App not found"
    fi
    
    # Check Static Web App
    check "Static Web App (Frontend)"
    local SWA=$(az staticwebapp list --resource-group "${RESOURCE_GROUP}" --query "[0].name" -o tsv 2>/dev/null || echo "")
    if [ -n "${SWA}" ] && [ "${SWA}" != "null" ]; then
        success "Static Web App found: ${SWA}"
    else
        error "Static Web App not found"
    fi
    
    # Check SQL Server
    check "SQL Server"
    local SQL_SERVER=$(az sql server list --resource-group "${RESOURCE_GROUP}" --query "[0].name" -o tsv 2>/dev/null || echo "")
    if [ -n "${SQL_SERVER}" ] && [ "${SQL_SERVER}" != "null" ]; then
        success "SQL Server found: ${SQL_SERVER}"
        
        # Check SQL Database
        check "SQL Database"
        local SQL_DB=$(az sql db list --resource-group "${RESOURCE_GROUP}" --server "${SQL_SERVER}" --query "[?name != 'master'].name | [0]" -o tsv 2>/dev/null || echo "")
        if [ -n "${SQL_DB}" ] && [ "${SQL_DB}" != "null" ]; then
            success "SQL Database found: ${SQL_DB}"
        else
            error "SQL Database not found"
        fi
    else
        error "SQL Server not found"
    fi
    
    # Check Storage Account
    check "Storage Account"
    local STORAGE=$(az storage account list --resource-group "${RESOURCE_GROUP}" --query "[0].name" -o tsv 2>/dev/null || echo "")
    if [ -n "${STORAGE}" ] && [ "${STORAGE}" != "null" ]; then
        success "Storage Account found: ${STORAGE}"
    else
        error "Storage Account not found"
    fi
    
    # Check Key Vault
    check "Key Vault"
    local KV=$(az keyvault list --resource-group "${RESOURCE_GROUP}" --query "[0].name" -o tsv 2>/dev/null || echo "")
    if [ -n "${KV}" ] && [ "${KV}" != "null" ]; then
        success "Key Vault found: ${KV}"
    else
        warning "Key Vault not found (may not be critical)"
    fi
    
    # Check Application Insights
    check "Application Insights"
    local AI=$(az monitor app-insights component show --resource-group "${RESOURCE_GROUP}" --query "[0].name" -o tsv 2>/dev/null || echo "")
    if [ -n "${AI}" ] && [ "${AI}" != "null" ]; then
        success "Application Insights found: ${AI}"
    else
        warning "Application Insights not found (monitoring may be limited)"
    fi
}

# Service Endpoint Validation
check_service_endpoints() {
    log "Validating service endpoints..."
    
    # Get API URL
    local API_URL=$(az webapp list --resource-group "${RESOURCE_GROUP}" --query "[?contains(name, 'api')].defaultHostName | [0]" -o tsv 2>/dev/null || echo "")
    if [ -n "${API_URL}" ] && [ "${API_URL}" != "null" ]; then
        API_URL="https://${API_URL}"
        info "API URL: ${API_URL}"
        
        # Test API health endpoint
        check "API Health Endpoint"
        if curl -f --max-time ${TIMEOUT} "${API_URL}/health" &> /dev/null; then
            success "API health endpoint responding"
        else
            warning "API health endpoint not responding (may still be starting)"
        fi
        
        # Test API root endpoint
        check "API Root Endpoint"
        local API_STATUS=$(curl -s --max-time ${TIMEOUT} -o /dev/null -w "%{http_code}" "${API_URL}" || echo "000")
        if [ "${API_STATUS}" -eq 200 ] || [ "${API_STATUS}" -eq 404 ]; then
            success "API root endpoint accessible (${API_STATUS})"
        else
            warning "API root endpoint returned: ${API_STATUS}"
        fi
    else
        error "Could not determine API URL"
    fi
    
    # Get Frontend URL
    local SWA_URL=$(az staticwebapp list --resource-group "${RESOURCE_GROUP}" --query "[0].defaultHostname" -o tsv 2>/dev/null || echo "")
    if [ -n "${SWA_URL}" ] && [ "${SWA_URL}" != "null" ]; then
        SWA_URL="https://${SWA_URL}"
        info "Frontend URL: ${SWA_URL}"
        
        # Test Frontend endpoint
        check "Frontend Endpoint"
        local SWA_STATUS=$(curl -s --max-time ${TIMEOUT} -o /dev/null -w "%{http_code}" "${SWA_URL}" || echo "000")
        if [ "${SWA_STATUS}" -eq 200 ]; then
            success "Frontend endpoint accessible"
        else
            warning "Frontend endpoint returned: ${SWA_STATUS}"
        fi
    else
        error "Could not determine Frontend URL"
    fi
}

# SSL/TLS Certificate Validation
check_ssl_certificates() {
    log "Validating SSL/TLS certificates..."
    
    # Check API SSL
    local API_URL=$(az webapp list --resource-group "${RESOURCE_GROUP}" --query "[?contains(name, 'api')].defaultHostName | [0]" -o tsv 2>/dev/null || echo "")
    if [ -n "${API_URL}" ] && [ "${API_URL}" != "null" ]; then
        check "API SSL Certificate"
        if echo | timeout ${TIMEOUT} openssl s_client -connect "${API_URL}:443" -servername "${API_URL}" 2>/dev/null | openssl x509 -noout -dates &> /dev/null; then
            success "API SSL certificate valid"
        else
            warning "API SSL certificate validation failed"
        fi
    fi
    
    # Check Frontend SSL
    local SWA_URL=$(az staticwebapp list --resource-group "${RESOURCE_GROUP}" --query "[0].defaultHostname" -o tsv 2>/dev/null || echo "")
    if [ -n "${SWA_URL}" ] && [ "${SWA_URL}" != "null" ]; then
        check "Frontend SSL Certificate"
        if echo | timeout ${TIMEOUT} openssl s_client -connect "${SWA_URL}:443" -servername "${SWA_URL}" 2>/dev/null | openssl x509 -noout -dates &> /dev/null; then
            success "Frontend SSL certificate valid"
        else
            warning "Frontend SSL certificate validation failed"
        fi
    fi
}

# Performance Testing
check_performance() {
    log "Running performance checks..."
    
    local API_URL=$(az webapp list --resource-group "${RESOURCE_GROUP}" --query "[?contains(name, 'api')].defaultHostName | [0]" -o tsv 2>/dev/null || echo "")
    if [ -n "${API_URL}" ] && [ "${API_URL}" != "null" ]; then
        API_URL="https://${API_URL}"
        
        check "API Response Time"
        local RESPONSE_TIME=$(curl -o /dev/null -s -w "%{time_total}" --max-time ${TIMEOUT} "${API_URL}/health" 2>/dev/null || echo "0")
        local RESPONSE_MS=$(echo "${RESPONSE_TIME} * 1000" | bc -l 2>/dev/null || echo "0")
        
        if (( $(echo "${RESPONSE_TIME} < 2.0" | bc -l 2>/dev/null || echo "0") )); then
            success "API response time: ${RESPONSE_MS%.*}ms (Good)"
        elif (( $(echo "${RESPONSE_TIME} < 5.0" | bc -l 2>/dev/null || echo "0") )); then
            warning "API response time: ${RESPONSE_MS%.*}ms (Acceptable)"
        else
            error "API response time: ${RESPONSE_MS%.*}ms (Too slow)"
        fi
    fi
}

# Security Validation
check_security() {
    log "Running security checks..."
    
    # Check HTTPS redirect
    local API_URL=$(az webapp list --resource-group "${RESOURCE_GROUP}" --query "[?contains(name, 'api')].defaultHostName | [0]" -o tsv 2>/dev/null || echo "")
    if [ -n "${API_URL}" ] && [ "${API_URL}" != "null" ]; then
        check "HTTPS Redirect"
        local HTTP_STATUS=$(curl -s --max-time ${TIMEOUT} -o /dev/null -w "%{http_code}" "http://${API_URL}" || echo "000")
        if [ "${HTTP_STATUS}" -eq 301 ] || [ "${HTTP_STATUS}" -eq 302 ] || [ "${HTTP_STATUS}" -eq 308 ]; then
            success "HTTPS redirect working (${HTTP_STATUS})"
        else
            warning "HTTPS redirect may not be configured (${HTTP_STATUS})"
        fi
    fi
    
    # Check security headers (if API is responding)
    if [ -n "${API_URL}" ]; then
        check "Security Headers"
        local HEADERS=$(curl -s --max-time ${TIMEOUT} -I "${API_URL}/health" 2>/dev/null || echo "")
        if echo "${HEADERS}" | grep -i "x-frame-options\|x-content-type-options\|x-xss-protection" &> /dev/null; then
            success "Security headers present"
        else
            warning "Security headers may be missing"
        fi
    fi
}

# Generate Summary Report
generate_summary() {
    echo
    echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
    echo -e "${BLUE}                    VALIDATION SUMMARY${NC}"
    echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
    echo
    
    info "Environment: ${ENVIRONMENT}"
    info "Total Checks: ${TOTAL_CHECKS}"
    success "Passed: ${PASSED_CHECKS}"
    warning "Warnings: ${WARNING_CHECKS}"
    error "Failed: ${FAILED_CHECKS}"
    echo
    
    local SUCCESS_RATE=$((PASSED_CHECKS * 100 / TOTAL_CHECKS))
    info "Success Rate: ${SUCCESS_RATE}%"
    
    if [ ${FAILED_CHECKS} -eq 0 ]; then
        echo -e "${GREEN}🎉 Deployment validation PASSED${NC}"
        if [ ${WARNING_CHECKS} -gt 0 ]; then
            echo -e "${YELLOW}⚠️ ${WARNING_CHECKS} warnings - review recommended${NC}"
        fi
    else
        echo -e "${RED}❌ Deployment validation FAILED${NC}"
        echo -e "${RED}${FAILED_CHECKS} critical issues need attention${NC}"
    fi
    echo
    info "Validation Completed: $(date)"
}

# Save results to file
save_results() {
    local REPORT_FILE="validation-report-${ENVIRONMENT}-$(date +%Y%m%d-%H%M%S).txt"
    {
        echo "AI Profile Photo Maker - Deployment Validation Report"
        echo "Generated: $(date)"
        echo "Environment: ${ENVIRONMENT}"
        echo "Resource Group: ${RESOURCE_GROUP}"
        echo
        echo "Summary:"
        echo "Total Checks: ${TOTAL_CHECKS}"
        echo "Passed: ${PASSED_CHECKS}"
        echo "Warnings: ${WARNING_CHECKS}"
        echo "Failed: ${FAILED_CHECKS}"
        echo "Success Rate: $((PASSED_CHECKS * 100 / TOTAL_CHECKS))%"
    } > "${REPORT_FILE}"
    
    info "Report saved to: ${REPORT_FILE}"
}

# Main execution
main() {
    print_header
    
    # Run all validation checks
    check_azure_auth || exit 1
    check_resource_group || exit 1
    check_infrastructure_resources
    check_service_endpoints
    check_ssl_certificates
    check_performance
    check_security
    
    # Generate summary and save results
    generate_summary
    save_results
    
    # Exit with appropriate code
    if [ ${FAILED_CHECKS} -eq 0 ]; then
        exit 0
    else
        exit 1
    fi
}

# Show usage if no arguments
if [ $# -eq 0 ]; then
    echo "Usage: $0 [staging|production] [--verbose]"
    echo "Example: $0 staging --verbose"
    exit 1
fi

# Check for verbose flag
if [ "${2:-}" = "--verbose" ]; then
    VERBOSE=true
fi

# Validate environment argument
if [[ ! "${ENVIRONMENT}" =~ ^(staging|production)$ ]]; then
    echo "Invalid environment: ${ENVIRONMENT}. Use 'staging' or 'production'"
    exit 1
fi

# Run main function
main "$@"