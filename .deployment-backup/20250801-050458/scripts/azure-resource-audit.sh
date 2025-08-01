#!/bin/bash
set -e

# Azure Resource Audit Script
# Purpose: Comprehensive resource inventory and duplication detection
# Author: Azure Standardization Task
# Date: $(date +%Y-%m-%d)

echo "🔍 Azure Resource Audit - AI Profile Photo Maker"
echo "==============================================="

# Configuration
RESOURCE_GROUP="ai-profile-photo-maker-staging"
AUDIT_DIR="/tmp/azure-audit-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$AUDIT_DIR"

echo "📊 Audit Directory: $AUDIT_DIR"
echo "🎯 Resource Group: $RESOURCE_GROUP"
echo ""

# Function: Check Azure CLI login
check_azure_login() {
    echo "🔐 Checking Azure CLI authentication..."
    if ! az account show > /dev/null 2>&1; then
        echo "❌ Azure CLI not logged in. Please run: az login"
        exit 1
    fi
    CURRENT_SUBSCRIPTION=$(az account show --query "name" -o tsv)
    echo "✅ Authenticated to subscription: $CURRENT_SUBSCRIPTION"
    echo ""
}

# Function: Resource inventory
resource_inventory() {
    echo "📋 Phase 1: Complete Resource Inventory"
    echo "--------------------------------------"
    
    # All resources in the resource group
    echo "📦 All resources in $RESOURCE_GROUP:"
    az resource list \
        --resource-group "$RESOURCE_GROUP" \
        --output table \
        --query "[].{Name:name, Type:type, Location:location}" \
        | tee "$AUDIT_DIR/all-resources.txt"
    
    echo ""
    
    # Resources by naming pattern
    echo "🏷️  Resources with 'aiapp' naming pattern:"
    az resource list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiapp')].{Name:name, Type:type, Location:location}" \
        --output table \
        | tee "$AUDIT_DIR/aiapp-resources.txt"
    
    echo ""
    
    echo "🏷️  Resources with 'aiprofilephotomaker' naming pattern:"
    az resource list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiprofilephotomaker')].{Name:name, Type:type, Location:location}" \
        --output table \
        | tee "$AUDIT_DIR/aiprofilephotomaker-resources.txt"
    
    echo ""
}

# Function: SQL Server analysis
sql_analysis() {
    echo "🗄️  Phase 2: SQL Server & Database Analysis"
    echo "-------------------------------------------"
    
    # Find all SQL servers
    echo "📊 SQL Servers in resource group:"
    SQL_SERVERS=$(az sql server list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[].name" \
        --output tsv)
    
    if [ -z "$SQL_SERVERS" ]; then
        echo "⚠️  No SQL servers found in $RESOURCE_GROUP"
        return
    fi
    
    # Analyze each SQL server
    for SERVER in $SQL_SERVERS; do
        echo ""
        echo "🔍 Analyzing SQL Server: $SERVER"
        
        # Server details
        az sql server show \
            --resource-group "$RESOURCE_GROUP" \
            --name "$SERVER" \
            --query "{Name:name, Location:location, Version:version, AdminLogin:administratorLogin, State:state}" \
            --output table
        
        # Databases on this server
        echo "📊 Databases on $SERVER:"
        az sql db list \
            --resource-group "$RESOURCE_GROUP" \
            --server "$SERVER" \
            --query "[].{Name:name, Status:status, Edition:edition, ServiceObjective:currentServiceObjectiveName, MaxSizeBytes:maxSizeBytes}" \
            --output table \
            | tee "$AUDIT_DIR/sql-server-$SERVER-databases.txt"
        
        # Database sizes
        echo "💾 Database usage for $SERVER:"
        az sql db list \
            --resource-group "$RESOURCE_GROUP" \
            --server "$SERVER" \
            --query "[].{Database:name, CurrentSizeGB:currentServiceObjectiveName, MaxSizeGB:maxSizeBytes}" \
            --output table
        
        echo ""
    done
}

# Function: Storage account analysis
storage_analysis() {
    echo "💾 Phase 3: Storage Account Analysis"
    echo "-----------------------------------"
    
    # Find all storage accounts
    STORAGE_ACCOUNTS=$(az storage account list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[].name" \
        --output tsv)
    
    if [ -z "$STORAGE_ACCOUNTS" ]; then
        echo "⚠️  No storage accounts found in $RESOURCE_GROUP"
        return
    fi
    
    # Analyze each storage account
    for STORAGE in $STORAGE_ACCOUNTS; do
        echo ""
        echo "🔍 Analyzing Storage Account: $STORAGE"
        
        # Storage account details
        az storage account show \
            --resource-group "$RESOURCE_GROUP" \
            --name "$STORAGE" \
            --query "{Name:name, Location:location, Sku:sku.name, Kind:kind, AccessTier:accessTier}" \
            --output table
        
        # Get storage account key for container analysis
        STORAGE_KEY=$(az storage account keys list \
            --resource-group "$RESOURCE_GROUP" \
            --account-name "$STORAGE" \
            --query "[0].value" \
            --output tsv)
        
        # List containers
        echo "📦 Containers in $STORAGE:"
        az storage container list \
            --account-name "$STORAGE" \
            --account-key "$STORAGE_KEY" \
            --query "[].{Name:name, LastModified:properties.lastModified, PublicAccess:properties.publicAccess}" \
            --output table \
            | tee "$AUDIT_DIR/storage-$STORAGE-containers.txt"
        
        # Storage usage metrics
        echo "📊 Storage metrics for $STORAGE:"
        az monitor metrics list \
            --resource "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE" \
            --metric "UsedCapacity" \
            --interval PT1H \
            --query "value[0].timeseries[0].data[-1].{Timestamp:timeStamp, UsedCapacityBytes:average}" \
            --output table 2>/dev/null || echo "⚠️  Metrics not available for $STORAGE"
        
        echo ""
    done
}

# Function: Web app analysis
webapp_analysis() {
    echo "🌐 Phase 4: Web Application Analysis"
    echo "-----------------------------------"
    
    # App Service Plans
    echo "📊 App Service Plans:"
    az appservice plan list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[].{Name:name, Location:location, Sku:sku.name, NumberOfSites:numberOfSites}" \
        --output table \
        | tee "$AUDIT_DIR/app-service-plans.txt"
    
    echo ""
    
    # Web Apps
    echo "🌐 Web Apps:"
    az webapp list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[].{Name:name, State:state, DefaultHostName:defaultHostName, Kind:kind}" \
        --output table \
        | tee "$AUDIT_DIR/web-apps.txt"
    
    echo ""
    
    # Static Web Apps
    echo "⚡ Static Web Apps:"
    az staticwebapp list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[].{Name:name, DefaultHostname:defaultHostname, RepositoryUrl:repositoryUrl}" \
        --output table \
        | tee "$AUDIT_DIR/static-web-apps.txt" 2>/dev/null || echo "⚠️  No static web apps found"
    
    echo ""
}

# Function: Security and monitoring analysis
security_monitoring_analysis() {
    echo "🔐 Phase 5: Security & Monitoring Analysis"
    echo "------------------------------------------"
    
    # Key Vaults
    echo "🔑 Key Vaults:"
    az keyvault list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[].{Name:name, Location:location, Sku:properties.sku.name}" \
        --output table \
        | tee "$AUDIT_DIR/key-vaults.txt"
    
    echo ""
    
    # Application Insights
    echo "📊 Application Insights:"
    az monitor app-insights component show \
        --resource-group "$RESOURCE_GROUP" \
        --query "[].{Name:name, Location:location, Kind:kind, ApplicationType:applicationType}" \
        --output table 2>/dev/null \
        | tee "$AUDIT_DIR/application-insights.txt" || echo "⚠️  No Application Insights found"
    
    echo ""
    
    # Log Analytics Workspaces
    echo "📝 Log Analytics Workspaces:"
    az monitor log-analytics workspace list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[].{Name:name, Location:location, Sku:sku.name, RetentionInDays:retentionInDays}" \
        --output table \
        | tee "$AUDIT_DIR/log-analytics.txt"
    
    echo ""
}

# Function: Cost analysis
cost_analysis() {
    echo "💰 Phase 6: Cost Analysis"
    echo "------------------------"
    
    echo "📊 Current month cost for resource group $RESOURCE_GROUP:"
    
    # Get current month costs (requires Cost Management API)
    az consumption usage list \
        --top 20 \
        --query "[?contains(instanceName, 'aiapp') || contains(instanceName, 'aiprofilephotomaker')].{Resource:instanceName, MeterName:meterName, UsageQuantity:usageQuantity, Cost:pretaxCost}" \
        --output table 2>/dev/null \
        | tee "$AUDIT_DIR/cost-analysis.txt" || echo "⚠️  Cost data not available (requires billing access)"
    
    echo ""
}

# Function: Dependency mapping
dependency_mapping() {
    echo "🔗 Phase 7: Resource Dependency Mapping"
    echo "---------------------------------------"
    
    echo "📊 Resource dependencies and references:"
    
    # Get all resources with their IDs for dependency analysis
    az resource list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[].{Name:name, Type:type, Id:id}" \
        --output json > "$AUDIT_DIR/resources-with-ids.json"
    
    # Check for resources that reference Key Vault
    echo "🔑 Resources with Key Vault references:"
    az resource list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(type, 'Microsoft.Web/sites')].{Name:name, Type:type}" \
        --output table
    
    # Check for resources that depend on storage accounts
    echo "💾 Resources potentially using storage accounts:"
    az resource list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(type, 'Microsoft.Web/sites') || contains(type, 'Microsoft.Web/staticSites')].{Name:name, Type:type}" \
        --output table
    
    echo ""
}

# Function: Generate summary report
generate_summary() {
    echo "📋 Phase 8: Summary Report Generation"
    echo "------------------------------------"
    
    # Count resources by pattern
    AIAPP_COUNT=$(az resource list --resource-group "$RESOURCE_GROUP" --query "[?contains(name, 'aiapp')] | length(@)" --output tsv)
    AIPROFILE_COUNT=$(az resource list --resource-group "$RESOURCE_GROUP" --query "[?contains(name, 'aiprofilephotomaker')] | length(@)" --output tsv)
    TOTAL_COUNT=$(az resource list --resource-group "$RESOURCE_GROUP" --query "length(@)" --output tsv)
    
    echo "📊 Resource Summary:"
    echo "==================="
    echo "Total Resources: $TOTAL_COUNT"
    echo "Resources with 'aiapp' pattern: $AIAPP_COUNT"
    echo "Resources with 'aiprofilephotomaker' pattern: $AIPROFILE_COUNT"
    echo "Other resources: $((TOTAL_COUNT - AIAPP_COUNT - AIPROFILE_COUNT))"
    echo ""
    
    # Generate summary file
    cat > "$AUDIT_DIR/SUMMARY.md" << EOF
# Azure Resource Audit Summary
Generated: $(date)
Resource Group: $RESOURCE_GROUP

## Resource Overview
- **Total Resources**: $TOTAL_COUNT
- **'aiapp' Pattern Resources**: $AIAPP_COUNT
- **'aiprofilephotomaker' Pattern Resources**: $AIPROFILE_COUNT
- **Other Resources**: $((TOTAL_COUNT - AIAPP_COUNT - AIPROFILE_COUNT))

## Duplication Analysis
$(if [ "$AIAPP_COUNT" -gt 0 ] && [ "$AIPROFILE_COUNT" -gt 0 ]; then
    echo "⚠️  **DUPLICATION DETECTED**: Both naming patterns exist"
    echo "- Cleanup required to standardize on single naming convention"
    echo "- Recommend consolidating to 'aiprofilephotomaker' pattern (standard template)"
else
    echo "✅ **NO DUPLICATION**: Single naming pattern in use"
fi)

## Next Steps
1. Review detailed audit files in: $AUDIT_DIR
2. Plan data migration strategy for duplicated resources
3. Execute systematic cleanup following dependency order
4. Implement naming convention validation

## Audit Files Generated
$(ls -la "$AUDIT_DIR" | grep -v "^total" | awk '{print "- " $9}')
EOF

    echo "✅ Summary report generated: $AUDIT_DIR/SUMMARY.md"
    echo ""
}

# Main execution
main() {
    echo "🚀 Starting Azure Resource Audit..."
    
    check_azure_login
    resource_inventory
    sql_analysis
    storage_analysis
    webapp_analysis
    security_monitoring_analysis
    cost_analysis
    dependency_mapping
    generate_summary
    
    echo "✅ Azure Resource Audit Complete!"
    echo "📂 Audit results saved to: $AUDIT_DIR"
    echo "📋 Summary report: $AUDIT_DIR/SUMMARY.md"
    echo ""
    echo "🔍 To review findings:"
    echo "   cat $AUDIT_DIR/SUMMARY.md"
    echo ""
    echo "📊 To continue with cleanup:"
    echo "   ./azure-resource-cleanup.sh $AUDIT_DIR"
}

# Execute main function
main "$@"