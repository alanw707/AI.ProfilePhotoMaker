#!/bin/bash

# Container Revision Verification and Startup Failure Detection Script
# Ensures container apps are running the expected image tag and are healthy
# Provides rollback capability if new revision fails

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ENVIRONMENT="${ENVIRONMENT:-v1}"
RESOURCE_GROUP="aiprofilemaker-${ENVIRONMENT}"
EXPECTED_IMAGE_TAG="${IMAGE_TAG:-latest}"
MAX_WAIT_TIME="${MAX_WAIT_TIME:-600}" # 10 minutes
CHECK_INTERVAL="${CHECK_INTERVAL:-30}" # 30 seconds
VERIFICATION_LOG="${PROJECT_ROOT}/container-verification-$(date +%Y%m%d-%H%M%S).log"

# Container Apps to verify
CONTAINER_APPS=(
    "aipm-api-${ENVIRONMENT}"
    "aipm-web-${ENVIRONMENT}"
)

# Logging functions
log_info() { echo -e "${BLUE}[INFO]${NC} $1" | tee -a "$VERIFICATION_LOG"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $1" | tee -a "$VERIFICATION_LOG"; }
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $1" | tee -a "$VERIFICATION_LOG"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1" | tee -a "$VERIFICATION_LOG"; }
log_step() { echo -e "${CYAN}${BOLD}[STEP]${NC} $1" | tee -a "$VERIFICATION_LOG"; }

# Parse command line arguments
ROLLBACK_ON_FAILURE=false
WAIT_FOR_HEALTH=true
VERBOSE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --rollback-on-failure)
            ROLLBACK_ON_FAILURE=true
            shift
            ;;
        --no-health-wait)
            WAIT_FOR_HEALTH=false
            shift
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        --expected-tag)
            EXPECTED_IMAGE_TAG="$2"
            shift 2
            ;;
        --max-wait)
            MAX_WAIT_TIME="$2"
            shift 2
            ;;
        --help)
            cat << EOF
Container Revision Verification Script

Usage: $0 [OPTIONS]

Options:
    --rollback-on-failure    Automatically rollback to previous revision if new revision fails
    --no-health-wait        Skip waiting for health checks (faster but less thorough)
    --verbose              Enable verbose logging
    --expected-tag TAG      Expected image tag (default: latest or IMAGE_TAG env var)
    --max-wait SECONDS      Maximum time to wait for revision (default: 600)
    --help                 Show this help message

Environment Variables:
    IMAGE_TAG              Expected image tag
    ENVIRONMENT            Target environment (default: v1)
    RESOURCE_GROUP         Azure resource group (default: aiprofilemaker-{env})

Examples:
    $0                                          # Basic verification
    $0 --rollback-on-failure                   # Enable automatic rollback
    $0 --expected-tag 123-456 --verbose        # Verify specific tag with verbose output
    $0 --no-health-wait --max-wait 300         # Quick verification with 5min timeout
EOF
            exit 0
            ;;
        *)
            log_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Initialize verification log
echo "Container Revision Verification started at $(date -Iseconds)" > "$VERIFICATION_LOG"
log_info "Starting container revision verification..."
log_info "📝 Configuration:"
log_info "  Environment: $ENVIRONMENT"
log_info "  Resource Group: $RESOURCE_GROUP"
log_info "  Expected Image Tag: $EXPECTED_IMAGE_TAG"
log_info "  Max Wait Time: ${MAX_WAIT_TIME}s"
log_info "  Rollback on Failure: $ROLLBACK_ON_FAILURE"
log_info "  Wait for Health: $WAIT_FOR_HEALTH"

# Function to get container app image tag
get_container_image_tag() {
    local app_name="$1"
    local image=$(az containerapp show \
        --name "$app_name" \
        --resource-group "$RESOURCE_GROUP" \
        --query "properties.template.containers[0].image" \
        --output tsv 2>/dev/null || echo "")
    
    if [[ -n "$image" ]]; then
        # Extract tag from image (after last colon)
        echo "${image##*:}"
    else
        echo ""
    fi
}

# Function to get container app revision status
get_revision_status() {
    local app_name="$1"
    az containerapp revision list \
        --name "$app_name" \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?properties.active==\`true\`].{name:name,provisioning:properties.provisioningState,replicas:properties.replicas,traffic:properties.trafficWeight}" \
        --output json 2>/dev/null || echo "[]"
}

# Function to get container app logs for troubleshooting
get_container_logs() {
    local app_name="$1"
    local lines="${2:-50}"
    
    log_info "📋 Retrieving recent logs for $app_name (last $lines lines)..."
    
    # Get logs from the container app
    local logs=$(az containerapp logs show \
        --name "$app_name" \
        --resource-group "$RESOURCE_GROUP" \
        --follow false \
        --tail "$lines" \
        --output table 2>/dev/null || echo "")
    
    if [[ -n "$logs" ]]; then
        echo "$logs"
    else
        log_warning "Could not retrieve logs for $app_name"
        echo "No logs available"
    fi
}

# Function to analyze startup failures
analyze_startup_failures() {
    local app_name="$1"
    
    log_step "🔍 Analyzing startup failures for $app_name..."
    
    # Get recent logs and look for common failure patterns
    local logs=$(get_container_logs "$app_name" 100)
    
    local failure_patterns=(
        "Application startup exception"
        "System.Exception"
        "System.ArgumentException"
        "System.InvalidOperationException"
        "Could not execute because the specified command or file was not found"
        "Connection refused"
        "Connection timeout"
        "Authentication failed"
        "Configuration error"
        "Missing environment variable"
        "Database.*connection.*failed"
        "Storage.*connection.*failed"
        "Failed to bind to address"
        "Port.*already in use"
        "Process exited with code"
        "Container terminated"
        "OOMKilled"
        "CrashLoopBackOff"
    )
    
    local failures_found=()
    for pattern in "${failure_patterns[@]}"; do
        if echo "$logs" | grep -qi "$pattern"; then
            failures_found+=("$pattern")
        fi
    done
    
    if [[ ${#failures_found[@]} -gt 0 ]]; then
        log_error "🚨 Startup failure patterns detected in $app_name:"
        for failure in "${failures_found[@]}"; do
            log_error "  • $failure"
        done
        
        # Show relevant log excerpts
        log_info "📋 Recent logs showing failures:"
        echo "$logs" | tail -20 | while read -r line; do
            echo "    $line"
        done
        
        return 1
    else
        log_success "✅ No startup failure patterns detected in $app_name"
        return 0
    fi
}

# Function to check container app health endpoint
check_health_endpoint() {
    local app_name="$1"
    
    # Get the app URL
    local app_url=$(az containerapp show \
        --name "$app_name" \
        --resource-group "$RESOURCE_GROUP" \
        --query "properties.configuration.ingress.fqdn" \
        --output tsv 2>/dev/null || echo "")
    
    if [[ -z "$app_url" ]]; then
        log_warning "Could not retrieve URL for $app_name"
        return 1
    fi
    
    local health_url="https://$app_url/api/health"
    if [[ "$app_name" == *"web"* ]]; then
        health_url="https://$app_url"  # Frontend doesn't have /api/health
    fi
    
    log_info "🔍 Testing health endpoint: $health_url"
    
    # Try health check with timeout
    if curl -f -s --max-time 15 "$health_url" > /dev/null 2>&1; then
        log_success "✅ Health check passed for $app_name"
        return 0
    else
        log_warning "⚠️ Health check failed for $app_name"
        return 1
    fi
}

# Function to get previous revision for rollback
get_previous_revision() {
    local app_name="$1"
    
    # Get all revisions sorted by creation time
    local previous_revision=$(az containerapp revision list \
        --name "$app_name" \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?properties.active==\`false\`] | sort_by(@, &properties.createdTime) | [-1].name" \
        --output tsv 2>/dev/null || echo "")
    
    echo "$previous_revision"
}

# Function to rollback to previous revision
rollback_to_previous_revision() {
    local app_name="$1"
    
    log_step "🔄 Attempting rollback for $app_name..."
    
    local previous_revision=$(get_previous_revision "$app_name")
    
    if [[ -z "$previous_revision" ]]; then
        log_error "❌ No previous revision found for rollback"
        return 1
    fi
    
    log_info "🔄 Rolling back to revision: $previous_revision"
    
    # Activate the previous revision
    if az containerapp revision activate \
        --name "$app_name" \
        --resource-group "$RESOURCE_GROUP" \
        --revision "$previous_revision" \
        --output none 2>/dev/null; then
        
        log_success "✅ Rollback initiated for $app_name"
        
        # Wait for rollback to complete
        log_info "⏳ Waiting for rollback to complete..."
        sleep 30
        
        # Verify rollback health
        if check_health_endpoint "$app_name"; then
            log_success "✅ Rollback completed successfully for $app_name"
            return 0
        else
            log_error "❌ Rollback completed but health check still fails"
            return 1
        fi
    else
        log_error "❌ Failed to initiate rollback for $app_name"
        return 1
    fi
}

# Function to verify single container app
verify_container_app() {
    local app_name="$1"
    local start_time=$(date +%s)
    local verification_passed=false
    
    log_step "🔍 Verifying container app: $app_name"
    
    while true; do
        local current_time=$(date +%s)
        local elapsed=$((current_time - start_time))
        
        if [[ $elapsed -gt $MAX_WAIT_TIME ]]; then
            log_error "❌ Verification timeout (${MAX_WAIT_TIME}s) for $app_name"
            break
        fi
        
        log_info "⏳ Checking revision status... (${elapsed}s elapsed)"
        
        # Get current image tag
        local current_tag=$(get_container_image_tag "$app_name")
        
        if [[ -z "$current_tag" ]]; then
            log_warning "⚠️ Could not retrieve current image tag for $app_name"
        else
            log_info "📊 Current image tag: $current_tag"
            log_info "📊 Expected image tag: $EXPECTED_IMAGE_TAG"
            
            if [[ "$current_tag" == "$EXPECTED_IMAGE_TAG" ]]; then
                log_success "✅ Image tag matches expected tag"
                
                # Check revision status
                local revision_status=$(get_revision_status "$app_name")
                local active_revisions=$(echo "$revision_status" | jq length)
                
                if [[ "$active_revisions" -gt 0 ]]; then
                    log_info "📊 Active revisions: $active_revisions"
                    
                    if [[ "$VERBOSE" == "true" ]]; then
                        echo "$revision_status" | jq '.'
                    fi
                    
                    # Check if all active revisions are ready
                    local ready_revisions=$(echo "$revision_status" | jq '[.[] | select(.provisioning == "Succeeded")] | length')
                    
                    if [[ "$ready_revisions" -eq "$active_revisions" ]]; then
                        log_success "✅ All active revisions are ready"
                        
                        # Optional health check
                        if [[ "$WAIT_FOR_HEALTH" == "true" ]]; then
                            if check_health_endpoint "$app_name"; then
                                log_success "✅ Health check passed"
                                verification_passed=true
                                break
                            else
                                log_warning "⚠️ Health check failed, analyzing startup issues..."
                                if ! analyze_startup_failures "$app_name"; then
                                    log_error "❌ Startup failures detected"
                                    break
                                fi
                            fi
                        else
                            log_info "ℹ️ Skipping health check (--no-health-wait enabled)"
                            verification_passed=true
                            break
                        fi
                    else
                        log_warning "⚠️ Some revisions not ready yet ($ready_revisions/$active_revisions)"
                    fi
                else
                    log_warning "⚠️ No active revisions found"
                fi
            else
                log_warning "⚠️ Image tag mismatch (current: $current_tag, expected: $EXPECTED_IMAGE_TAG)"
            fi
        fi
        
        log_info "⏳ Waiting ${CHECK_INTERVAL}s before next check..."
        sleep "$CHECK_INTERVAL"
    done
    
    if [[ "$verification_passed" == "true" ]]; then
        log_success "✅ Verification completed successfully for $app_name"
        return 0
    else
        log_error "❌ Verification failed for $app_name"
        
        # Analyze failures
        analyze_startup_failures "$app_name"
        
        # Optional rollback
        if [[ "$ROLLBACK_ON_FAILURE" == "true" ]]; then
            log_warning "🔄 Rollback enabled, attempting to restore previous revision..."
            if rollback_to_previous_revision "$app_name"; then
                log_success "✅ Rollback completed for $app_name"
            else
                log_error "❌ Rollback failed for $app_name"
            fi
        fi
        
        return 1
    fi
}

# Function to generate verification report
generate_verification_report() {
    local overall_success="$1"
    local failed_apps=("${@:2}")
    
    local report_file="${PROJECT_ROOT}/container-verification-report-$(date +%Y%m%d-%H%M%S).json"
    
    log_step "📊 Generating verification report..."
    
    # Collect status for all apps
    local app_statuses=()
    for app in "${CONTAINER_APPS[@]}"; do
        local current_tag=$(get_container_image_tag "$app")
        local revision_status=$(get_revision_status "$app")
        local is_healthy=false
        
        if check_health_endpoint "$app"; then
            is_healthy=true
        fi
        
        app_statuses+=("{
            \"appName\": \"$app\",
            \"currentImageTag\": \"$current_tag\",
            \"expectedImageTag\": \"$EXPECTED_IMAGE_TAG\",
            \"tagMatches\": $([ "$current_tag" == "$EXPECTED_IMAGE_TAG" ] && echo "true" || echo "false"),
            \"isHealthy\": $is_healthy,
            \"activeRevisions\": $(echo "$revision_status" | jq length),
            \"revisionDetails\": $revision_status
        }")
    done
    
    # Create JSON report
    cat > "$report_file" << EOF
{
    "verificationTimestamp": "$(date -Iseconds)",
    "environment": "$ENVIRONMENT",
    "resourceGroup": "$RESOURCE_GROUP",
    "expectedImageTag": "$EXPECTED_IMAGE_TAG",
    "overallSuccess": $overall_success,
    "failedApps": [$(printf '"%s",' "${failed_apps[@]}" | sed 's/,$//')],
    "configuration": {
        "maxWaitTime": $MAX_WAIT_TIME,
        "checkInterval": $CHECK_INTERVAL,
        "rollbackOnFailure": $ROLLBACK_ON_FAILURE,
        "waitForHealth": $WAIT_FOR_HEALTH
    },
    "containerApps": [$(IFS=','; echo "${app_statuses[*]}")]
}
EOF
    
    log_success "📊 Verification report generated: $report_file"
    
    # Display summary
    log_info "📋 Verification Summary:"
    log_info "  Overall Success: $overall_success"
    log_info "  Expected Tag: $EXPECTED_IMAGE_TAG"
    log_info "  Failed Apps: ${#failed_apps[@]}"
    if [[ ${#failed_apps[@]} -gt 0 ]]; then
        for failed_app in "${failed_apps[@]}"; do
            log_info "    • $failed_app"
        done
    fi
    
    return 0
}

# Main verification function
main() {
    echo -e "${BLUE}${BOLD}================================================${NC}"
    echo -e "${BLUE}${BOLD} Container Revision Verification${NC}"
    echo -e "${BLUE}${BOLD}================================================${NC}"
    echo ""
    
    local overall_success=true
    local failed_apps=()
    
    # Verify each container app
    for app in "${CONTAINER_APPS[@]}"; do
        if ! verify_container_app "$app"; then
            overall_success=false
            failed_apps+=("$app")
        fi
        echo ""
    done
    
    # Generate verification report
    generate_verification_report "$overall_success" "${failed_apps[@]}"
    
    echo ""
    if [[ "$overall_success" == "true" ]]; then
        echo -e "${GREEN}${BOLD}🎉 CONTAINER REVISION VERIFICATION SUCCESSFUL! 🎉${NC}"
        echo ""
        echo -e "${CYAN}📊 All container apps are running the expected revision and are healthy${NC}"
    else
        echo -e "${RED}${BOLD}❌ CONTAINER REVISION VERIFICATION FAILED ❌${NC}"
        echo ""
        echo -e "${YELLOW}⚠️ Some container apps failed verification:${NC}"
        for failed_app in "${failed_apps[@]}"; do
            echo -e "  ${RED}• $failed_app${NC}"
        done
        echo ""
        echo -e "${CYAN}📝 Next Steps:${NC}"
        echo "  1. Check container app logs for startup errors"
        echo "  2. Verify image exists in ACR with correct tag"
        echo "  3. Check environment variable configuration"
        echo "  4. Consider manual rollback if automatic rollback failed"
    fi
    
    echo ""
    echo -e "${BLUE}📁 Generated Files:${NC}"
    echo "  Verification Log: $VERIFICATION_LOG"
    echo "  Verification Report: $report_file"
    echo ""
    
    if [[ "$overall_success" == "true" ]]; then
        exit 0
    else
        exit 1
    fi
}

# Script entry point
main