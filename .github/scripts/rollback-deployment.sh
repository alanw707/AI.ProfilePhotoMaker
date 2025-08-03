#!/bin/bash
# =============================================================================
# Emergency Rollback Script
# Performs complete rollback to previous working deployment
# =============================================================================

set -e

# Configuration
ENVIRONMENT=${1:-staging}
ROLLBACK_TO_VERSION=${2:-latest-stable}
FORCE_ROLLBACK=${3:-false}

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

# Validate Azure connection
validate_azure_connection() {
    log_info "Validating Azure connection..."
    
    if ! az account show >/dev/null 2>&1; then
        log_error "Not logged in to Azure. Please run 'az login' first."
        exit 1
    fi
    
    SUBSCRIPTION=$(az account show --query "name" -o tsv)
    log_success "Connected to Azure subscription: ${SUBSCRIPTION}"
}

# Get current deployment info
get_current_deployment_info() {
    log_info "Getting current deployment information..."
    
    RESOURCE_GROUP="rg-aiprofilemaker-${ENVIRONMENT}"
    
    # Check if resource group exists
    if ! az group show --name ${RESOURCE_GROUP} >/dev/null 2>&1; then
        log_error "Resource group not found: ${RESOURCE_GROUP}"
        exit 1
    fi
    
    # Get container app information
    APPS=$(az containerapp list --resource-group ${RESOURCE_GROUP} --query "[].name" -o tsv)
    JOBS=$(az containerapp job list --resource-group ${RESOURCE_GROUP} --query "[].name" -o tsv)
    
    if [[ -z "$APPS" && -z "$JOBS" ]]; then
        log_error "No container apps or jobs found in resource group"
        exit 1
    fi
    
    log_info "Found container apps: ${APPS}"
    log_info "Found container jobs: ${JOBS}"
    
    # Get registry information
    REGISTRY_NAME=$(az acr list --resource-group ${RESOURCE_GROUP} --query "[0].name" -o tsv)
    if [[ -n "$REGISTRY_NAME" ]]; then
        REGISTRY_SERVER=$(az acr show --name ${REGISTRY_NAME} --query "loginServer" -o tsv)
        log_info "Container registry: ${REGISTRY_SERVER}"
    else
        log_error "No container registry found"
        exit 1
    fi
}

# Backup current configuration
backup_current_configuration() {
    log_info "Backing up current configuration..."
    
    BACKUP_DIR="/tmp/rollback-backup-$(date +%Y%m%d-%H%M%S)"
    mkdir -p ${BACKUP_DIR}
    
    # Backup container app configurations
    for app in $APPS; do
        log_info "Backing up configuration for: ${app}"
        az containerapp show --name ${app} --resource-group ${RESOURCE_GROUP} > ${BACKUP_DIR}/${app}.json
    done
    
    # Backup container job configurations
    for job in $JOBS; do
        log_info "Backing up configuration for: ${job}"
        az containerapp job show --name ${job} --resource-group ${RESOURCE_GROUP} > ${BACKUP_DIR}/${job}.json
    done
    
    log_success "Configuration backed up to: ${BACKUP_DIR}"
    echo "BACKUP_DIR=${BACKUP_DIR}" >> /tmp/rollback.env
}

# Check for rollback images
check_rollback_images() {
    log_info "Checking for rollback images..."
    
    # Check if rollback images exist
    ROLLBACK_IMAGES_EXIST=true
    
    REQUIRED_IMAGES=("api" "frontend" "migration")
    
    for image in "${REQUIRED_IMAGES[@]}"; do
        IMAGE_TAG="${REGISTRY_SERVER}/${image}:${ROLLBACK_TO_VERSION}"
        log_info "Checking for image: ${IMAGE_TAG}"
        
        if az acr repository show --name ${REGISTRY_NAME} --repository ${image} --tag ${ROLLBACK_TO_VERSION} >/dev/null 2>&1; then
            log_success "✓ ${image}:${ROLLBACK_TO_VERSION}"
        else
            log_error "✗ ${image}:${ROLLBACK_TO_VERSION} not found"
            ROLLBACK_IMAGES_EXIST=false
        fi
    done
    
    if [[ "$ROLLBACK_IMAGES_EXIST" == "false" ]]; then
        if [[ "$FORCE_ROLLBACK" == "true" ]]; then
            log_warning "Rollback images missing but force rollback requested"
            log_warning "Will attempt to use 'latest' tags as fallback"
            ROLLBACK_TO_VERSION="latest"
        else
            log_error "Rollback images not available. Cannot proceed with safe rollback."
            log_error "Use --force to attempt rollback with 'latest' tags (dangerous)"
            exit 1
        fi
    fi
}

# Stop traffic to applications
stop_traffic() {
    log_info "Stopping traffic to applications during rollback..."
    
    # Scale down to 0 replicas temporarily
    for app in $APPS; do
        log_info "Scaling down ${app} to 0 replicas..."
        az containerapp update --name ${app} --resource-group ${RESOURCE_GROUP} --min-replicas 0 --max-replicas 0 >/dev/null 2>&1 || true
    done
    
    log_info "Waiting for traffic to stop..."
    sleep 10
}

# Rollback container applications
rollback_applications() {
    log_info "Rolling back container applications..."
    
    local rollback_errors=0
    
    # Rollback API
    API_APP=$(echo $APPS | tr ' ' '\n' | grep -i api | head -1)
    if [[ -n "$API_APP" ]]; then
        log_info "Rolling back API application: ${API_APP}"
        IMAGE_TAG="${REGISTRY_SERVER}/api:${ROLLBACK_TO_VERSION}"
        
        if az containerapp update \
            --name ${API_APP} \
            --resource-group ${RESOURCE_GROUP} \
            --image ${IMAGE_TAG} \
            --min-replicas 1 \
            --max-replicas 10 >/dev/null 2>&1; then
            log_success "API rollback completed"
        else
            log_error "API rollback failed"
            ((rollback_errors++))
        fi
    fi
    
    # Rollback Frontend
    FRONTEND_APP=$(echo $APPS | tr ' ' '\n' | grep -i frontend | head -1)
    if [[ -n "$FRONTEND_APP" ]]; then
        log_info "Rolling back Frontend application: ${FRONTEND_APP}"
        IMAGE_TAG="${REGISTRY_SERVER}/frontend:${ROLLBACK_TO_VERSION}"
        
        if az containerapp update \
            --name ${FRONTEND_APP} \
            --resource-group ${RESOURCE_GROUP} \
            --image ${IMAGE_TAG} \
            --min-replicas 1 \
            --max-replicas 10 >/dev/null 2>&1; then
            log_success "Frontend rollback completed"
        else
            log_error "Frontend rollback failed"
            ((rollback_errors++))
        fi
    fi
    
    # Rollback Migration Job
    MIGRATION_JOB=$(echo $JOBS | tr ' ' '\n' | grep -i migration | head -1)
    if [[ -n "$MIGRATION_JOB" ]]; then
        log_info "Rolling back Migration job: ${MIGRATION_JOB}"
        IMAGE_TAG="${REGISTRY_SERVER}/migration:${ROLLBACK_TO_VERSION}"
        
        if az containerapp job update \
            --name ${MIGRATION_JOB} \
            --resource-group ${RESOURCE_GROUP} \
            --image ${IMAGE_TAG} >/dev/null 2>&1; then
            log_success "Migration job rollback completed"
        else
            log_error "Migration job rollback failed"
            ((rollback_errors++))
        fi
    fi
    
    return $rollback_errors
}

# Verify rollback
verify_rollback() {
    log_info "Verifying rollback success..."
    
    # Wait for applications to start
    log_info "Waiting for applications to start..."
    sleep 30
    
    # Get application URLs
    FRONTEND_URL=$(az containerapp show --name ${FRONTEND_APP} --resource-group ${RESOURCE_GROUP} --query "properties.configuration.ingress.fqdn" -o tsv 2>/dev/null || echo "")
    API_URL=$(az containerapp show --name ${API_APP} --resource-group ${RESOURCE_GROUP} --query "properties.configuration.ingress.fqdn" -o tsv 2>/dev/null || echo "")
    
    if [[ -n "$FRONTEND_URL" ]]; then
        FRONTEND_URL="https://${FRONTEND_URL}"
    fi
    if [[ -n "$API_URL" ]]; then
        API_URL="https://${API_URL}"
    fi
    
    local verification_errors=0
    
    # Test frontend
    if [[ -n "$FRONTEND_URL" ]]; then
        log_info "Testing frontend: ${FRONTEND_URL}"
        if curl -f -s --connect-timeout 10 --max-time 20 "${FRONTEND_URL}" >/dev/null; then
            log_success "Frontend is accessible"
        else
            log_error "Frontend is not accessible"
            ((verification_errors++))
        fi
    fi
    
    # Test API health
    if [[ -n "$API_URL" ]]; then
        log_info "Testing API health: ${API_URL}/health"
        if curl -f -s --connect-timeout 10 --max-time 20 "${API_URL}/health" >/dev/null; then
            log_success "API health check passed"
        else
            log_error "API health check failed"
            ((verification_errors++))
        fi
    fi
    
    return $verification_errors
}

# Tag current version as rollback candidate
tag_rollback_version() {
    if [[ "$ROLLBACK_TO_VERSION" != "latest-stable" ]]; then
        log_info "Tagging rolled-back version as latest-stable..."
        
        # Tag the images we just rolled back to as latest-stable
        REQUIRED_IMAGES=("api" "frontend" "migration")
        
        for image in "${REQUIRED_IMAGES[@]}"; do
            SOURCE_TAG="${REGISTRY_SERVER}/${image}:${ROLLBACK_TO_VERSION}"
            TARGET_TAG="${REGISTRY_SERVER}/${image}:latest-stable"
            
            # Import/tag the image
            az acr import \
                --name ${REGISTRY_NAME} \
                --source ${SOURCE_TAG} \
                --image ${TARGET_TAG} >/dev/null 2>&1 || true
                
            log_info "Tagged ${image}:${ROLLBACK_TO_VERSION} as latest-stable"
        done
    fi
}

# Generate rollback report
generate_rollback_report() {
    local success=$1
    
    echo ""
    echo "=============================================="
    echo "           ROLLBACK REPORT"
    echo "=============================================="
    echo "Environment: ${ENVIRONMENT}"
    echo "Rollback Version: ${ROLLBACK_TO_VERSION}"
    echo "Timestamp: $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
    echo "Duration: ${SECONDS}s"
    echo ""
    
    if [[ $success -eq 0 ]]; then
        echo -e "${GREEN}✅ ROLLBACK SUCCESSFUL${NC}"
        echo "System has been rolled back to previous working version."
        
        if [[ -n "$FRONTEND_URL" ]]; then
            echo "Frontend URL: ${FRONTEND_URL}"
        fi
        if [[ -n "$API_URL" ]]; then
            echo "API URL: ${API_URL}"
        fi
    else
        echo -e "${RED}❌ ROLLBACK FAILED${NC}"
        echo "Rollback encountered errors. Manual intervention may be required."
        
        if [[ -f "/tmp/rollback.env" ]]; then
            . /tmp/rollback.env
            echo "Configuration backup available at: ${BACKUP_DIR}"
        fi
    fi
    
    echo ""
    echo "Next Steps:"
    if [[ $success -eq 0 ]]; then
        echo "1. Verify application functionality manually"
        echo "2. Monitor application health and performance"
        echo "3. Investigate root cause of the original failure"
        echo "4. Plan corrective deployment when ready"
    else
        echo "1. Check Azure portal for detailed error messages"
        echo "2. Restore from configuration backup if needed"
        echo "3. Contact operations team for assistance"
    fi
    
    echo "=============================================="
}

# Confirm rollback
confirm_rollback() {
    if [[ "$FORCE_ROLLBACK" != "true" ]]; then
        echo ""
        echo -e "${YELLOW}⚠️  ROLLBACK CONFIRMATION REQUIRED${NC}"
        echo "Environment: ${ENVIRONMENT}"
        echo "Rollback to: ${ROLLBACK_TO_VERSION}"
        echo ""
        echo "This will:"
        echo "- Stop current application traffic"
        echo "- Rollback to previous container images"
        echo "- Restart applications with rolled-back version"
        echo ""
        
        read -p "Are you sure you want to proceed? (yes/no): " -r
        if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
            log_info "Rollback cancelled by user"
            exit 0
        fi
    fi
}

# Main rollback flow
main() {
    log_info "Starting emergency rollback for environment: ${ENVIRONMENT}"
    log_info "Target version: ${ROLLBACK_TO_VERSION}"
    
    # Validate prerequisites
    validate_azure_connection
    get_current_deployment_info
    
    # Confirm rollback
    confirm_rollback
    
    # Backup current state
    backup_current_configuration
    
    # Check rollback images
    check_rollback_images
    
    # Perform rollback
    log_info "Initiating rollback sequence..."
    
    stop_traffic
    
    local rollback_errors=0
    rollback_applications || ((rollback_errors += $?))
    
    if [[ $rollback_errors -eq 0 ]]; then
        verify_rollback || ((rollback_errors += $?))
    fi
    
    if [[ $rollback_errors -eq 0 ]]; then
        tag_rollback_version
    fi
    
    # Generate report
    generate_rollback_report $rollback_errors
    
    # Exit with appropriate code
    exit $rollback_errors
}

# Usage information
show_usage() {
    echo "Usage: $0 [ENVIRONMENT] [ROLLBACK_VERSION] [FORCE]"
    echo ""
    echo "Arguments:"
    echo "  ENVIRONMENT      Target environment (default: staging)"
    echo "  ROLLBACK_VERSION Target version tag (default: latest-stable)"
    echo "  FORCE            Set to 'true' to skip confirmations (default: false)"
    echo ""
    echo "Examples:"
    echo "  $0 staging"
    echo "  $0 production latest-stable"
    echo "  $0 staging v20240103-a1b2c3d4 true"
    echo ""
}

# Handle script arguments
if [[ "$1" == "--help" || "$1" == "-h" ]]; then
    show_usage
    exit 0
fi

if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi