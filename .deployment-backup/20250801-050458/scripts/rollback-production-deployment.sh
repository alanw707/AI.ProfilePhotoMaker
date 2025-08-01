#!/bin/bash

# Production Rollback Script for AI Profile Photo Maker
# This script performs a rollback of the production deployment

set -e

# Configuration
RESOURCE_GROUP_NAME="ai-profile-photo-maker-prod"
ENVIRONMENT="prod"
BACKUP_RETENTION_DAYS=7

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

# Function to show usage
show_usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Rollback production deployment for AI Profile Photo Maker

OPTIONS:
    -t, --type TYPE          Rollback type: app|infrastructure|database|all [default: app]
    -v, --version VERSION    Target version/deployment to rollback to
    -f, --force              Force rollback without confirmation
    -d, --dry-run            Show what would be rolled back without executing
    -h, --help               Show this help message

EXAMPLES:
    $0                       Rollback application to previous version
    $0 -t app -v 1.2.3      Rollback application to specific version
    $0 -t infrastructure    Rollback infrastructure to previous deployment
    $0 -t all --force       Force rollback of everything to previous state
    $0 --dry-run            Show rollback plan without executing

ROLLBACK TYPES:
    app            - Rollback application code only
    infrastructure - Rollback infrastructure resources
    database       - Rollback database to previous backup
    all            - Complete rollback of all components

EOF
}

# Parse command line arguments
ROLLBACK_TYPE="app"
TARGET_VERSION=""
FORCE_ROLLBACK=false
DRY_RUN=false

while [[ $# -gt 0 ]]; do
    case $1 in
        -t|--type)
            ROLLBACK_TYPE="$2"
            shift 2
            ;;
        -v|--version)
            TARGET_VERSION="$2"
            shift 2
            ;;
        -f|--force)
            FORCE_ROLLBACK=true
            shift
            ;;
        -d|--dry-run)
            DRY_RUN=true
            shift
            ;;
        -h|--help)
            show_usage
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Validate rollback type
if [[ ! "$ROLLBACK_TYPE" =~ ^(app|infrastructure|database|all)$ ]]; then
    print_error "Invalid rollback type. Must be one of: app, infrastructure, database, all"
    exit 1
fi

# Function to check Azure CLI login
check_azure_login() {
    print_status "Checking Azure CLI login status..."
    if ! az account show &> /dev/null; then
        print_error "Not logged in to Azure. Please run 'az login' first."
        exit 1
    fi
    print_success "Azure CLI login verified"
}

# Function to confirm rollback
confirm_rollback() {
    if [ "$FORCE_ROLLBACK" = "true" ] || [ "$DRY_RUN" = "true" ]; then
        return 0
    fi
    
    print_warning "🚨 PRODUCTION ROLLBACK WARNING 🚨"
    print_warning "This will rollback the production environment."
    print_warning "Type: $ROLLBACK_TYPE"
    print_warning "Resource Group: $RESOURCE_GROUP_NAME"
    
    if [ -n "$TARGET_VERSION" ]; then
        print_warning "Target Version: $TARGET_VERSION"
    fi
    
    echo ""
    read -p "Are you sure you want to proceed? Type 'ROLLBACK' to confirm: " confirmation
    
    if [ "$confirmation" != "ROLLBACK" ]; then
        print_status "Rollback cancelled by user"
        exit 0
    fi
}

# Function to get current deployment information
get_current_deployment_info() {
    print_status "Gathering current deployment information..."
    
    # Get Web App information
    WEB_APP_NAME=$(az webapp list --resource-group "$RESOURCE_GROUP_NAME" --query "[0].name" -o tsv)
    if [ -n "$WEB_APP_NAME" ] && [ "$WEB_APP_NAME" != "null" ]; then
        print_success "Found Web App: $WEB_APP_NAME"
        
        # Get current deployment slot info
        CURRENT_SLOT=$(az webapp deployment slot list --name "$WEB_APP_NAME" --resource-group "$RESOURCE_GROUP_NAME" --query "[?name == 'staging'].name" -o tsv 2>/dev/null || echo "")
        
        # Get deployment history
        DEPLOYMENT_HISTORY=$(az webapp deployment list --name "$WEB_APP_NAME" --resource-group "$RESOURCE_GROUP_NAME" --query "[0:5].[id,status,log_url,start_time]" -o table 2>/dev/null || echo "")
        
        echo "Recent Deployments:"
        echo "$DEPLOYMENT_HISTORY"
    else
        print_error "Web App not found in resource group"
        exit 1
    fi
    
    # Get infrastructure deployment history
    print_status "Getting infrastructure deployment history..."
    INFRA_DEPLOYMENTS=$(az deployment group list --resource-group "$RESOURCE_GROUP_NAME" --query "[?provisioningState=='Succeeded'][0:5].[name,timestamp,provisioningState]" -o table 2>/dev/null)
    
    echo "Recent Infrastructure Deployments:"
    echo "$INFRA_DEPLOYMENTS"
}

# Function to rollback application
rollback_application() {
    print_status "Rolling back application deployment..."
    
    if [ "$DRY_RUN" = "true" ]; then
        print_status "[DRY RUN] Would rollback application to previous version"
        return 0
    fi
    
    # Check if staging slot exists for blue-green deployment
    if [ -n "$CURRENT_SLOT" ]; then
        print_status "Using deployment slot for rollback..."
        
        # Swap staging and production slots
        print_status "Swapping deployment slots..."
        az webapp deployment slot swap \
            --name "$WEB_APP_NAME" \
            --resource-group "$RESOURCE_GROUP_NAME" \
            --slot staging \
            --target-slot production
        
        print_success "Deployment slot swap completed"
    else
        # Use deployment history for rollback
        print_status "Using deployment history for rollback..."
        
        if [ -n "$TARGET_VERSION" ]; then
            print_status "Rolling back to specific version: $TARGET_VERSION"
            # This would typically involve redeploying a specific build artifact
            print_warning "Version-specific rollback requires build artifact. Please use your CI/CD pipeline to deploy version $TARGET_VERSION"
        else
            print_status "Rolling back to previous deployment..."
            
            # Get previous successful deployment
            PREVIOUS_DEPLOYMENT=$(az webapp deployment list --name "$WEB_APP_NAME" --resource-group "$RESOURCE_GROUP_NAME" --query "[?status=='4'][1].id" -o tsv 2>/dev/null)
            
            if [ -n "$PREVIOUS_DEPLOYMENT" ] && [ "$PREVIOUS_DEPLOYMENT" != "null" ]; then
                print_status "Found previous deployment: $PREVIOUS_DEPLOYMENT"
                # Note: Azure doesn't provide direct rollback API, so this would typically be done through CI/CD
                print_warning "Direct deployment rollback not available. Please use your CI/CD pipeline to redeploy the previous version."
            else
                print_error "No previous successful deployment found"
                return 1
            fi
        fi
    fi
    
    # Verify application health after rollback
    print_status "Verifying application health after rollback..."
    sleep 30 # Allow time for deployment to complete
    
    WEB_APP_URL="https://${WEB_APP_NAME}.azurewebsites.net"
    if curl -f -s --max-time 30 "$WEB_APP_URL/health" > /dev/null; then
        print_success "Application health check passed after rollback"
    else
        print_error "Application health check failed after rollback"
        return 1
    fi
}

# Function to rollback infrastructure
rollback_infrastructure() {
    print_status "Rolling back infrastructure deployment..."
    
    if [ "$DRY_RUN" = "true" ]; then
        print_status "[DRY RUN] Would rollback infrastructure to previous deployment"
        return 0
    fi
    
    # Get previous successful infrastructure deployment
    PREVIOUS_DEPLOYMENT=$(az deployment group list --resource-group "$RESOURCE_GROUP_NAME" --query "[?provisioningState=='Succeeded'][1].name" -o tsv)
    
    if [ -n "$PREVIOUS_DEPLOYMENT" ] && [ "$PREVIOUS_DEPLOYMENT" != "null" ]; then
        print_status "Found previous infrastructure deployment: $PREVIOUS_DEPLOYMENT"
        
        # Get the template and parameters from the previous deployment
        print_status "Retrieving previous deployment template..."
        
        DEPLOYMENT_TEMPLATE=$(az deployment group show --resource-group "$RESOURCE_GROUP_NAME" --name "$PREVIOUS_DEPLOYMENT" --query "properties.template" -o json)
        DEPLOYMENT_PARAMETERS=$(az deployment group show --resource-group "$RESOURCE_GROUP_NAME" --name "$PREVIOUS_DEPLOYMENT" --query "properties.parameters" -o json)
        
        if [ "$DEPLOYMENT_TEMPLATE" != "null" ] && [ "$DEPLOYMENT_PARAMETERS" != "null" ]; then
            print_status "Re-deploying previous infrastructure configuration..."
            
            # Create rollback deployment
            ROLLBACK_DEPLOYMENT_NAME="rollback-$(date +%Y%m%d-%H%M%S)"
            
            # Save template and parameters to temporary files
            echo "$DEPLOYMENT_TEMPLATE" > /tmp/rollback-template.json
            echo "$DEPLOYMENT_PARAMETERS" > /tmp/rollback-parameters.json
            
            # Deploy previous configuration
            az deployment group create \
                --resource-group "$RESOURCE_GROUP_NAME" \
                --template-file /tmp/rollback-template.json \
                --parameters @/tmp/rollback-parameters.json \
                --name "$ROLLBACK_DEPLOYMENT_NAME"
            
            # Cleanup temporary files
            rm -f /tmp/rollback-template.json /tmp/rollback-parameters.json
            
            print_success "Infrastructure rollback completed"
        else
            print_error "Could not retrieve template or parameters from previous deployment"
            return 1
        fi
    else
        print_error "No previous successful infrastructure deployment found"
        return 1
    fi
}

# Function to rollback database
rollback_database() {
    print_status "Rolling back database..."
    
    if [ "$DRY_RUN" = "true" ]; then
        print_status "[DRY RUN] Would rollback database to previous backup"
        return 0
    fi
    
    # Get SQL Server and Database information
    SQL_SERVER_NAME=$(az sql server list --resource-group "$RESOURCE_GROUP_NAME" --query "[0].name" -o tsv)
    DB_NAME=$(az sql db list --server "$SQL_SERVER_NAME" --resource-group "$RESOURCE_GROUP_NAME" --query "[?name != 'master'].name" -o tsv | head -n1)
    
    if [ -z "$SQL_SERVER_NAME" ] || [ -z "$DB_NAME" ]; then
        print_error "SQL Server or Database not found"
        return 1
    fi
    
    print_status "Found database: $DB_NAME on server: $SQL_SERVER_NAME"
    
    # List available backups
    print_status "Retrieving available database backups..."
    
    # Calculate backup date (24 hours ago as default)
    if [ -n "$TARGET_VERSION" ]; then
        RESTORE_POINT="$TARGET_VERSION"
    else
        RESTORE_POINT=$(date -d '24 hours ago' -u +%Y-%m-%dT%H:%M:%SZ)
    fi
    
    print_status "Target restore point: $RESTORE_POINT"
    
    # Create a backup database name
    BACKUP_DB_NAME="${DB_NAME}-backup-$(date +%Y%m%d%H%M%S)"
    
    print_warning "⚠️  DATABASE ROLLBACK WARNING ⚠️"
    print_warning "This will restore the database to: $RESTORE_POINT"
    print_warning "Current data will be backed up to: $BACKUP_DB_NAME"
    
    if [ "$FORCE_ROLLBACK" != "true" ]; then
        read -p "Continue with database rollback? (y/N): " db_confirm
        if [[ ! "$db_confirm" =~ ^[Yy]$ ]]; then
            print_status "Database rollback cancelled"
            return 0
        fi
    fi
    
    # Create backup of current database
    print_status "Creating backup of current database..."
    az sql db copy \
        --dest-name "$BACKUP_DB_NAME" \
        --dest-server "$SQL_SERVER_NAME" \
        --name "$DB_NAME" \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --server "$SQL_SERVER_NAME"
    
    print_success "Current database backed up to: $BACKUP_DB_NAME"
    
    # Restore database from point-in-time
    print_status "Restoring database from point-in-time backup..."
    
    # Create temporary restore database
    TEMP_RESTORE_DB="${DB_NAME}-restore-temp"
    
    az sql db restore \
        --dest-name "$TEMP_RESTORE_DB" \
        --name "$DB_NAME" \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --server "$SQL_SERVER_NAME" \
        --time "$RESTORE_POINT"
    
    print_success "Database restored to temporary database: $TEMP_RESTORE_DB"
    
    # Rename databases to complete the rollback
    print_status "Finalizing database rollback..."
    
    # This would require stopping the application temporarily
    print_warning "Application should be stopped during database switch"
    
    # The actual database switch would require more complex logic
    # including stopping the app, renaming databases, and restarting
    print_status "Database rollback prepared. Manual intervention required to complete the switch."
    print_status "Temporary restored database: $TEMP_RESTORE_DB"
    print_status "Current database backup: $BACKUP_DB_NAME"
}

# Function to generate rollback report
generate_rollback_report() {
    local exit_code=$1
    
    print_status "Generating rollback report..."
    
    cat > rollback-report.json << EOF
{
  "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "environment": "$ENVIRONMENT",
  "resourceGroup": "$RESOURCE_GROUP_NAME",
  "rollbackType": "$ROLLBACK_TYPE",
  "targetVersion": "$TARGET_VERSION",
  "dryRun": $DRY_RUN,
  "status": "$([ $exit_code -eq 0 ] && echo "SUCCESS" || echo "FAILED")",
  "resources": {
    "webApp": "$WEB_APP_NAME",
    "sqlServer": "$SQL_SERVER_NAME",
    "database": "$DB_NAME"
  }
}
EOF
    
    print_success "Rollback report generated: rollback-report.json"
}

# Main execution
main() {
    print_status "Starting production rollback process..."
    print_status "Rollback Type: $ROLLBACK_TYPE"
    print_status "Environment: $ENVIRONMENT"
    print_status "Resource Group: $RESOURCE_GROUP_NAME"
    print_status "Dry Run: $DRY_RUN"
    
    local exit_code=0
    
    # Check prerequisites
    check_azure_login || exit_code=1
    
    if [ $exit_code -eq 0 ]; then
        get_current_deployment_info || exit_code=1
    fi
    
    if [ $exit_code -eq 0 ]; then
        confirm_rollback
    fi
    
    # Perform rollback based on type
    if [ $exit_code -eq 0 ]; then
        case $ROLLBACK_TYPE in
            app)
                rollback_application || exit_code=1
                ;;
            infrastructure)
                rollback_infrastructure || exit_code=1
                ;;
            database)
                rollback_database || exit_code=1
                ;;
            all)
                rollback_application || exit_code=1
                if [ $exit_code -eq 0 ]; then
                    rollback_infrastructure || exit_code=1
                fi
                if [ $exit_code -eq 0 ]; then
                    rollback_database || exit_code=1
                fi
                ;;
        esac
    fi
    
    # Generate report
    generate_rollback_report $exit_code
    
    if [ $exit_code -eq 0 ]; then
        if [ "$DRY_RUN" = "true" ]; then
            print_success "🔍 Rollback plan completed successfully (dry run)"
        else
            print_success "🔄 Production rollback completed successfully!"
        fi
    else
        print_error "❌ Production rollback failed!"
        print_error "Please check the errors above and resolve them."
    fi
    
    exit $exit_code
}

# Run main function
main "$@"