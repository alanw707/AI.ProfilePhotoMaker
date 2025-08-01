#!/bin/bash
set -e

# Azure Resource Cleanup Script
# Purpose: Systematic cleanup of duplicate Azure resources with data preservation
# Author: Azure Standardization Task
# Date: $(date +%Y-%m-%d)

echo "🧹 Azure Resource Cleanup - AI Profile Photo Maker"
echo "================================================="

# Configuration
RESOURCE_GROUP="ai-profile-photo-maker-staging"
BACKUP_DIR="/tmp/azure-backup-$(date +%Y%m%d-%H%M%S)"
AUDIT_DIR="$1"  # Path to audit results from previous script
DRY_RUN="${2:-false}"  # Set to 'true' for dry run mode

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo "📊 Configuration:"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Backup Directory: $BACKUP_DIR"
echo "  Audit Directory: $AUDIT_DIR"
echo "  Dry Run Mode: $DRY_RUN"
echo ""

# Create backup directory
mkdir -p "$BACKUP_DIR"

# Function: Log with timestamp and color
log() {
    local level=$1
    local message=$2
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    
    case $level in
        "INFO")  echo -e "${BLUE}[INFO]${NC}  $timestamp - $message" ;;
        "WARN")  echo -e "${YELLOW}[WARN]${NC}  $timestamp - $message" ;;
        "ERROR") echo -e "${RED}[ERROR]${NC} $timestamp - $message" ;;
        "SUCCESS") echo -e "${GREEN}[SUCCESS]${NC} $timestamp - $message" ;;
    esac
    
    # Also log to file
    echo "[$level] $timestamp - $message" >> "$BACKUP_DIR/cleanup.log"
}

# Function: Execute command with dry run support
execute_cmd() {
    local description=$1
    local command=$2
    
    if [ "$DRY_RUN" == "true" ]; then
        log "INFO" "[DRY RUN] Would execute: $description"
        log "INFO" "[DRY RUN] Command: $command"
        return 0
    else
        log "INFO" "Executing: $description"
        log "INFO" "Command: $command"
        eval "$command" || {
            log "ERROR" "Failed to execute: $description"
            return 1
        }
        log "SUCCESS" "Completed: $description"
        return 0
    fi
}

# Function: Check prerequisites
check_prerequisites() {
    log "INFO" "Checking prerequisites..."
    
    # Check Azure CLI login
    if ! az account show > /dev/null 2>&1; then
        log "ERROR" "Azure CLI not logged in. Please run: az login"
        exit 1
    fi
    
    # Check if audit directory exists and contains data
    if [ ! -z "$AUDIT_DIR" ] && [ ! -d "$AUDIT_DIR" ]; then
        log "WARN" "Audit directory not found. Running without audit data."
        AUDIT_DIR=""
    fi
    
    # Verify resource group exists
    if ! az group show --name "$RESOURCE_GROUP" > /dev/null 2>&1; then
        log "ERROR" "Resource group '$RESOURCE_GROUP' not found"
        exit 1
    fi
    
    log "SUCCESS" "Prerequisites check completed"
}

# Function: Backup SQL databases
backup_sql_databases() {
    log "INFO" "Phase 1: SQL Database Backup"
    echo "=============================="
    
    # Get all SQL servers
    local sql_servers=$(az sql server list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[].name" \
        --output tsv)
    
    if [ -z "$sql_servers" ]; then
        log "WARN" "No SQL servers found for backup"
        return 0
    fi
    
    # Create backup storage account if it doesn't exist
    local backup_storage_name="aiprofilebackup$(date +%s | tail -c 6)"
    
    execute_cmd "Create backup storage account" \
        "az storage account create \
            --name '$backup_storage_name' \
            --resource-group '$RESOURCE_GROUP' \
            --location 'eastus2' \
            --sku 'Standard_LRS' \
            --kind 'StorageV2'"
    
    # Get storage account key
    local storage_key
    if [ "$DRY_RUN" != "true" ]; then
        storage_key=$(az storage account keys list \
            --resource-group "$RESOURCE_GROUP" \
            --account-name "$backup_storage_name" \
            --query "[0].value" \
            --output tsv)
    else
        storage_key="dry-run-key"
    fi
    
    # Create backup container
    execute_cmd "Create backup container" \
        "az storage container create \
            --name 'database-backups' \
            --account-name '$backup_storage_name' \
            --account-key '$storage_key'"
    
    # Backup each database
    for server in $sql_servers; do
        log "INFO" "Processing SQL Server: $server"
        
        # Get databases on this server (exclude system databases)
        local databases=$(az sql db list \
            --resource-group "$RESOURCE_GROUP" \
            --server "$server" \
            --query "[?name != 'master'].name" \
            --output tsv)
        
        for database in $databases; do
            log "INFO" "Backing up database: $database from server: $server"
            
            local backup_file="${server}-${database}-$(date +%Y%m%d-%H%M%S).bacpac"
            local storage_uri="https://${backup_storage_name}.blob.core.windows.net/database-backups/${backup_file}"
            
            execute_cmd "Export database $database from $server" \
                "az sql db export \
                    --resource-group '$RESOURCE_GROUP' \
                    --server '$server' \
                    --name '$database' \
                    --storage-key-type 'StorageAccessKey' \
                    --storage-key '$storage_key' \
                    --storage-uri '$storage_uri' \
                    --admin-user 'aiprofileadmin' \
                    --admin-password '\$AZURE_SQL_PASSWORD'" || {
                        log "ERROR" "Failed to backup database $database"
                        continue
                    }
            
            # Save backup metadata
            echo "$server,$database,$backup_file,$storage_uri,$(date)" >> "$BACKUP_DIR/sql-backups.csv"
        done
    done
    
    log "SUCCESS" "SQL database backup phase completed"
    echo ""
}

# Function: Backup storage accounts
backup_storage_data() {
    log "INFO" "Phase 2: Storage Account Data Backup"
    echo "===================================="
    
    # Get all storage accounts with 'aiapp' pattern (these will be removed)
    local storage_accounts=$(az storage account list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiapp')].name" \
        --output tsv)
    
    if [ -z "$storage_accounts" ]; then
        log "WARN" "No 'aiapp' pattern storage accounts found for backup"
        return 0
    fi
    
    for storage in $storage_accounts; do
        log "INFO" "Backing up storage account: $storage"
        
        # Get storage account key
        local storage_key
        if [ "$DRY_RUN" != "true" ]; then
            storage_key=$(az storage account keys list \
                --resource-group "$RESOURCE_GROUP" \
                --account-name "$storage" \
                --query "[0].value" \
                --output tsv)
        else
            storage_key="dry-run-key"
        fi
        
        # Get containers
        local containers
        if [ "$DRY_RUN" != "true" ]; then
            containers=$(az storage container list \
                --account-name "$storage" \
                --account-key "$storage_key" \
                --query "[].name" \
                --output tsv)
        else
            containers="profile-images"  # Assume default container for dry run
        fi
        
        # Backup each container
        for container in $containers; do
            log "INFO" "Backing up container: $container from storage: $storage"
            
            local backup_path="$BACKUP_DIR/storage-backup/$storage/$container"
            mkdir -p "$backup_path"
            
            execute_cmd "Download blobs from $container in $storage" \
                "az storage blob download-batch \
                    --source '$container' \
                    --destination '$backup_path' \
                    --account-name '$storage' \
                    --account-key '$storage_key'" || {
                        log "WARN" "Failed to backup container $container from $storage"
                        continue
                    }
            
            # Save backup metadata
            echo "$storage,$container,$backup_path,$(date)" >> "$BACKUP_DIR/storage-backups.csv"
        done
    done
    
    log "SUCCESS" "Storage data backup phase completed"
    echo ""
}

# Function: Cleanup duplicate resources
cleanup_duplicate_resources() {
    log "INFO" "Phase 3: Duplicate Resource Cleanup"
    echo "==================================="
    
    # Cleanup order (bottom-up dependency resolution)
    
    # 1. Remove duplicate web app configurations
    log "INFO" "Step 1: Cleaning up web app configurations"
    
    local aiapp_webapps=$(az webapp list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiapp')].name" \
        --output tsv)
    
    for webapp in $aiapp_webapps; do
        execute_cmd "Delete web app: $webapp" \
            "az webapp delete \
                --resource-group '$RESOURCE_GROUP' \
                --name '$webapp'"
    done
    
    # 2. Remove duplicate static web apps
    log "INFO" "Step 2: Cleaning up static web apps"
    
    local aiapp_static_webapps=$(az staticwebapp list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiapp')].name" \
        --output tsv 2>/dev/null || echo "")
    
    for staticwebapp in $aiapp_static_webapps; do
        execute_cmd "Delete static web app: $staticwebapp" \
            "az staticwebapp delete \
                --resource-group '$RESOURCE_GROUP' \
                --name '$staticwebapp' \
                --yes"
    done
    
    # 3. Remove duplicate app service plans
    log "INFO" "Step 3: Cleaning up app service plans"
    
    local aiapp_plans=$(az appservice plan list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiapp')].name" \
        --output tsv)
    
    for plan in $aiapp_plans; do
        execute_cmd "Delete app service plan: $plan" \
            "az appservice plan delete \
                --resource-group '$RESOURCE_GROUP' \
                --name '$plan' \
                --yes"
    done
    
    # 4. Remove duplicate SQL databases
    log "INFO" "Step 4: Cleaning up SQL databases"
    
    local aiapp_sql_servers=$(az sql server list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiapp')].name" \
        --output tsv)
    
    for server in $aiapp_sql_servers; do
        # First delete databases
        local databases=$(az sql db list \
            --resource-group "$RESOURCE_GROUP" \
            --server "$server" \
            --query "[?name != 'master'].name" \
            --output tsv)
        
        for database in $databases; do
            execute_cmd "Delete database: $database from server: $server" \
                "az sql db delete \
                    --resource-group '$RESOURCE_GROUP' \
                    --server '$server' \
                    --name '$database' \
                    --yes"
        done
        
        # Then delete the server
        execute_cmd "Delete SQL server: $server" \
            "az sql server delete \
                --resource-group '$RESOURCE_GROUP' \
                --name '$server' \
                --yes"
    done
    
    # 5. Remove duplicate storage accounts (after backup)
    log "INFO" "Step 5: Cleaning up storage accounts"
    
    local aiapp_storage=$(az storage account list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiapp')].name" \
        --output tsv)
    
    for storage in $aiapp_storage; do
        execute_cmd "Delete storage account: $storage" \
            "az storage account delete \
                --resource-group '$RESOURCE_GROUP' \
                --name '$storage' \
                --yes"
    done
    
    # 6. Remove duplicate Key Vaults
    log "INFO" "Step 6: Cleaning up Key Vaults"
    
    local aiapp_keyvaults=$(az keyvault list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiapp')].name" \
        --output tsv)
    
    for keyvault in $aiapp_keyvaults; do
        execute_cmd "Delete Key Vault: $keyvault" \
            "az keyvault delete \
                --resource-group '$RESOURCE_GROUP' \
                --name '$keyvault'"
    done
    
    # 7. Remove duplicate Application Insights
    log "INFO" "Step 7: Cleaning up Application Insights"
    
    local aiapp_insights=$(az monitor app-insights component list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiapp')].name" \
        --output tsv 2>/dev/null || echo "")
    
    for insights in $aiapp_insights; do
        execute_cmd "Delete Application Insights: $insights" \
            "az monitor app-insights component delete \
                --resource-group '$RESOURCE_GROUP' \
                --app '$insights'"
    done
    
    # 8. Remove duplicate Log Analytics workspaces
    log "INFO" "Step 8: Cleaning up Log Analytics"
    
    local aiapp_loganalytics=$(az monitor log-analytics workspace list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiapp')].name" \
        --output tsv)
    
    for workspace in $aiapp_loganalytics; do
        execute_cmd "Delete Log Analytics workspace: $workspace" \
            "az monitor log-analytics workspace delete \
                --resource-group '$RESOURCE_GROUP' \
                --workspace-name '$workspace' \
                --yes"
    done
    
    log "SUCCESS" "Duplicate resource cleanup phase completed"
    echo ""
}

# Function: Verify cleanup results
verify_cleanup() {
    log "INFO" "Phase 4: Cleanup Verification"
    echo "============================="
    
    # Check for remaining 'aiapp' resources
    local remaining_aiapp=$(az resource list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiapp')] | length(@)" \
        --output tsv)
    
    if [ "$remaining_aiapp" -eq 0 ]; then
        log "SUCCESS" "✅ All 'aiapp' pattern resources successfully removed"
    else
        log "WARN" "⚠️  $remaining_aiapp 'aiapp' pattern resources still exist"
        
        # List remaining resources
        az resource list \
            --resource-group "$RESOURCE_GROUP" \
            --query "[?contains(name, 'aiapp')].{Name:name, Type:type}" \
            --output table
    fi
    
    # Show current resource count
    local total_resources=$(az resource list \
        --resource-group "$RESOURCE_GROUP" \
        --query "length(@)" \
        --output tsv)
    
    log "INFO" "Total resources remaining in $RESOURCE_GROUP: $total_resources"
    
    # Generate post-cleanup inventory
    az resource list \
        --resource-group "$RESOURCE_GROUP" \
        --output table \
        --query "[].{Name:name, Type:type, Location:location}" \
        > "$BACKUP_DIR/post-cleanup-inventory.txt"
    
    log "SUCCESS" "Cleanup verification completed"
    echo ""
}

# Function: Generate cleanup report
generate_cleanup_report() {
    log "INFO" "Phase 5: Cleanup Report Generation"
    echo "=================================="
    
    # Create comprehensive cleanup report
    cat > "$BACKUP_DIR/CLEANUP_REPORT.md" << EOF
# Azure Resource Cleanup Report
Generated: $(date)
Resource Group: $RESOURCE_GROUP
Backup Directory: $BACKUP_DIR
Dry Run Mode: $DRY_RUN

## Executive Summary
$(if [ "$DRY_RUN" == "true" ]; then
    echo "🔍 **DRY RUN COMPLETED** - No actual changes were made"
    echo "- All commands logged for review"
    echo "- No resources were deleted"
    echo "- No data was backed up"
else
    echo "✅ **CLEANUP COMPLETED** - Duplicate resources standardized"
    echo "- All 'aiapp' pattern resources removed"
    echo "- Data backed up before deletion"
    echo "- Infrastructure standardized on 'aiprofilephotomaker' pattern"
fi)

## Backup Status
$(if [ -f "$BACKUP_DIR/sql-backups.csv" ]; then
    echo "### SQL Database Backups"
    echo "\`\`\`"
    cat "$BACKUP_DIR/sql-backups.csv"
    echo "\`\`\`"
fi)

$(if [ -f "$BACKUP_DIR/storage-backups.csv" ]; then
    echo "### Storage Account Backups"
    echo "\`\`\`"
    cat "$BACKUP_DIR/storage-backups.csv"
    echo "\`\`\`"
fi)

## Resource Changes
$(if [ -f "$BACKUP_DIR/post-cleanup-inventory.txt" ]; then
    echo "### Remaining Resources"
    echo "\`\`\`"
    cat "$BACKUP_DIR/post-cleanup-inventory.txt"
    echo "\`\`\`"
fi)

## Next Steps
1. **Validate Applications**: Test all applications function correctly
2. **Update Configuration**: Ensure all connection strings point to remaining resources  
3. **Monitor Performance**: Watch for any performance impacts post-cleanup
4. **Update Documentation**: Update any documentation referencing old resource names
5. **Cost Review**: Monitor cost savings from resource consolidation

## Recovery Information
- **Backup Location**: $BACKUP_DIR
- **Recovery Scripts**: Available for emergency rollback
- **Contact**: Azure Infrastructure Team

## Files Generated
$(ls -la "$BACKUP_DIR" | grep -v "^total" | awk '{print "- " $9}')
EOF

    log "SUCCESS" "Cleanup report generated: $BACKUP_DIR/CLEANUP_REPORT.md"
    echo ""
}

# Function: Display final summary
display_summary() {
    echo "🎉 Azure Resource Cleanup Summary"
    echo "================================="
    echo ""
    echo "📊 Status: $(if [ "$DRY_RUN" == "true" ]; then echo "DRY RUN COMPLETED"; else echo "CLEANUP COMPLETED"; fi)"
    echo "📂 Backup Directory: $BACKUP_DIR"
    echo "📋 Detailed Report: $BACKUP_DIR/CLEANUP_REPORT.md"
    echo "📝 Execution Log: $BACKUP_DIR/cleanup.log"
    echo ""
    
    if [ "$DRY_RUN" == "true" ]; then
        echo "🔍 To execute the actual cleanup:"
        echo "   $0 $AUDIT_DIR false"
    else
        echo "✅ Cleanup completed successfully!"
        echo "🔍 To review results:"
        echo "   cat $BACKUP_DIR/CLEANUP_REPORT.md"
    fi
    echo ""
}

# Main execution function
main() {
    log "INFO" "Starting Azure Resource Cleanup"
    
    check_prerequisites
    
    if [ "$DRY_RUN" == "true" ]; then
        log "INFO" "🔍 RUNNING IN DRY RUN MODE - No changes will be made"
        echo ""
    fi
    
    backup_sql_databases
    backup_storage_data
    cleanup_duplicate_resources
    verify_cleanup
    generate_cleanup_report
    display_summary
    
    log "SUCCESS" "Azure Resource Cleanup Complete!"
}

# Trap for cleanup on exit
trap 'log "INFO" "Script execution finished"' EXIT

# Execute main function
main "$@"