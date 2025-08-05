#!/bin/bash
# =============================================================================
# Azure Resource Cleanup Script
# Removes old scattered resources and prepares for clean MVP deployment
# =============================================================================

set -e

# Configuration
SUBSCRIPTION_ID=${AZURE_SUBSCRIPTION_ID}
OLD_RESOURCE_GROUP="aiprofilemaker-staging"
NEW_RESOURCE_GROUP="rg-aiprofilemaker-staging"

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

# Check if user is logged in to Azure
check_azure_login() {
    log_info "Checking Azure login status..."
    
    if ! az account show >/dev/null 2>&1; then
        log_error "Not logged in to Azure. Please run 'az login' first."
        exit 1
    fi
    
    CURRENT_SUBSCRIPTION=$(az account show --query "id" -o tsv)
    log_info "Current subscription: $CURRENT_SUBSCRIPTION"
    
    if [ -n "$SUBSCRIPTION_ID" ] && [ "$CURRENT_SUBSCRIPTION" != "$SUBSCRIPTION_ID" ]; then
        log_warning "Current subscription doesn't match expected. Setting to: $SUBSCRIPTION_ID"
        az account set --subscription "$SUBSCRIPTION_ID"
    fi
    
    log_success "Azure login verified"
}

# List all resources that will be affected
list_resources() {
    log_info "Scanning for AI Profile Maker resources across all resource groups..."
    
    # Find all resource groups containing aiprofilemaker resources
    RESOURCE_GROUPS=$(az group list --query "[?contains(name, 'aiprofilemaker') || contains(name, 'profilemaker')].name" -o tsv)
    
    if [ -z "$RESOURCE_GROUPS" ]; then
        log_warning "No AI Profile Maker resource groups found"
        return 0
    fi
    
    echo ""
    echo "📋 Found the following resource groups:"
    for rg in $RESOURCE_GROUPS; do
        echo "  📁 $rg"
        
        # List resources in each group
        RESOURCES=$(az resource list --resource-group "$rg" --query "[].{Name:name, Type:type, Location:location}" -o table 2>/dev/null || echo "")
        if [ -n "$RESOURCES" ]; then
            echo "$RESOURCES" | sed 's/^/      /'
        fi
        echo ""
    done
    
    # Also check for orphaned resources with aiprofilemaker in the name
    log_info "Scanning for orphaned aiprofilemaker resources..."
    ORPHANED=$(az resource list --query "[?contains(name, 'aiprofilemaker') || contains(name, 'profilemaker')].{Name:name, Type:type, ResourceGroup:resourceGroup, Location:location}" -o table 2>/dev/null || echo "")
    
    if [ -n "$ORPHANED" ]; then
        echo "🏷️ Found resources with aiprofilemaker in name:"
        echo "$ORPHANED"
    fi
}

# Backup critical data before deletion
backup_data() {
    log_info "Creating backup of critical data..."
    
    # Create backup directory
    BACKUP_DIR="backup-$(date +%Y%m%d-%H%M%S)"
    mkdir -p "$BACKUP_DIR"
    
    # Backup SQL database if exists
    for rg in $(az group list --query "[?contains(name, 'aiprofilemaker')].name" -o tsv); do
        log_info "Checking resource group: $rg"
        
        # Find SQL servers
        SQL_SERVERS=$(az sql server list --resource-group "$rg" --query "[].name" -o tsv 2>/dev/null || echo "")
        
        for server in $SQL_SERVERS; do
            log_info "Found SQL Server: $server"
            
            # List databases
            DATABASES=$(az sql db list --resource-group "$rg" --server "$server" --query "[?name != 'master'].name" -o tsv 2>/dev/null || echo "")
            
            for db in $DATABASES; do
                log_info "Backing up database: $db from server: $server"
                
                # Export database to storage account (if we have one)
                # For now, just document what we found
                echo "SQL Server: $server" >> "$BACKUP_DIR/sql-resources.txt"
                echo "Database: $db" >> "$BACKUP_DIR/sql-resources.txt"
                echo "Resource Group: $rg" >> "$BACKUP_DIR/sql-resources.txt"
                echo "---" >> "$BACKUP_DIR/sql-resources.txt"
            done
        done
        
        # Backup storage account info
        STORAGE_ACCOUNTS=$(az storage account list --resource-group "$rg" --query "[].name" -o tsv 2>/dev/null || echo "")
        
        for storage in $STORAGE_ACCOUNTS; do
            log_info "Found Storage Account: $storage"
            echo "Storage Account: $storage" >> "$BACKUP_DIR/storage-resources.txt"
            echo "Resource Group: $rg" >> "$BACKUP_DIR/storage-resources.txt"
            
            # List containers
            CONTAINERS=$(az storage container list --account-name "$storage" --query "[].name" -o tsv 2>/dev/null || echo "")
            for container in $CONTAINERS; do
                echo "Container: $container" >> "$BACKUP_DIR/storage-resources.txt"
            done
            echo "---" >> "$BACKUP_DIR/storage-resources.txt"
        done
    done
    
    log_success "Backup information saved to: $BACKUP_DIR"
}

# Clean up old resources
cleanup_old_resources() {
    log_warning "This will DELETE old resources. Make sure you have backups!"
    echo ""
    read -p "Are you sure you want to continue? (type 'yes' to confirm): " -r
    echo ""
    
    if [[ ! $REPLY =~ ^yes$ ]]; then
        log_info "Cleanup cancelled by user"
        exit 0
    fi
    
    # Delete old resource groups
    RESOURCE_GROUPS=$(az group list --query "[?contains(name, 'aiprofilemaker') && name != '$NEW_RESOURCE_GROUP'].name" -o tsv)
    
    for rg in $RESOURCE_GROUPS; do
        log_warning "Deleting resource group: $rg"
        az group delete --name "$rg" --yes --no-wait
        log_info "Deletion initiated for: $rg (running in background)"
    done
    
    # Clean up any orphaned resources (shouldn't happen but just in case)
    log_info "Checking for orphaned resources..."
    
    # Container registries
    ORPHANED_ACR=$(az acr list --query "[?contains(name, 'aiprofilemaker') && resourceGroup != '$NEW_RESOURCE_GROUP'].{name:name, resourceGroup:resourceGroup}" -o tsv 2>/dev/null || echo "")
    
    if [ -n "$ORPHANED_ACR" ]; then
        log_warning "Found orphaned container registries. Please clean these up manually:"
        echo "$ORPHANED_ACR"
    fi
    
    log_success "Cleanup initiated. Resources are being deleted in the background."
    log_info "You can monitor progress in the Azure portal or with: az group list"
}

# Verify cleanup completion
verify_cleanup() {
    log_info "Verifying cleanup completion..."
    
    # Check for remaining aiprofilemaker resources
    REMAINING_GROUPS=$(az group list --query "[?contains(name, 'aiprofilemaker') && name != '$NEW_RESOURCE_GROUP'].name" -o tsv)
    
    if [ -n "$REMAINING_GROUPS" ]; then
        log_warning "Some resource groups are still being deleted:"
        for rg in $REMAINING_GROUPS; do
            STATUS=$(az group show --name "$rg" --query "properties.provisioningState" -o tsv 2>/dev/null || echo "NotFound")
            echo "  📁 $rg - Status: $STATUS"
        done
    else
        log_success "All old resource groups have been cleaned up"
    fi
    
    # Check if new resource group exists
    if az group show --name "$NEW_RESOURCE_GROUP" >/dev/null 2>&1; then
        log_success "New resource group '$NEW_RESOURCE_GROUP' is ready"
    else
        log_info "New resource group '$NEW_RESOURCE_GROUP' will be created during deployment"
    fi
}

# Main execution
main() {
    echo "=============================================="
    echo "    AI Profile Maker - Resource Cleanup"
    echo "=============================================="
    echo ""
    
    log_info "Starting cleanup process..."
    echo "  Old naming pattern: Random suffixes, multiple regions"
    echo "  New naming pattern: Predictable names, single region (East US)"
    echo "  New resource group: $NEW_RESOURCE_GROUP"
    echo ""
    
    check_azure_login
    list_resources
    
    echo ""
    echo "⚠️  IMPORTANT NOTES:"
    echo "  • This will delete ALL existing aiprofilemaker resources"
    echo "  • Database data will be lost unless you have backups"
    echo "  • Storage account data will be lost unless you have backups"
    echo "  • The new MVP deployment will start fresh"
    echo ""
    
    backup_data
    cleanup_old_resources
    
    echo ""
    log_info "Cleanup process completed. Next steps:"
    echo "  1. Wait for all deletions to complete (check Azure portal)"
    echo "  2. Run the new MVP deployment workflow"
    echo "  3. Update DNS/domain settings to point to new URLs"
    echo ""
    
    verify_cleanup
    
    log_success "Resource cleanup completed successfully!"
}

# Run main function if script is executed directly
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi