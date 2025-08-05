#!/bin/bash
# Cross-Region Azure Resource Monitoring Script
# Monitors resource health and connectivity across multiple Azure regions
# Usage: ./cross-region-monitor.sh <resource-group-name> [timeout-seconds]

set -euo pipefail

RESOURCE_GROUP="${1:-aiprofilemaker-staging}"
TIMEOUT="${2:-60}"
REGIONS=("eastus" "eastus2" "westus2" "centralus" "westus")

echo "🌍 Cross-Region Resource Health Monitor"
echo "📍 Resource Group: $RESOURCE_GROUP"
echo "⏱️ Timeout: ${TIMEOUT}s per check"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Initialize monitoring results
declare -A RESOURCE_HEALTH
declare -A RESOURCE_LOCATIONS
declare -A REGION_LATENCY
TOTAL_RESOURCES=0
HEALTHY_RESOURCES=0
FAILED_RESOURCES=0

# Function to measure Azure CLI command latency
measure_latency() {
    local start_time=$(date +%s%N)
    "$@" >/dev/null 2>&1
    local end_time=$(date +%s%N)
    local duration_ms=$(( (end_time - start_time) / 1000000 ))
    echo "$duration_ms"
}

# Function to check resource health with timeout and retry
check_resource_health() {
    local resource_type="$1"
    local resource_name="$2"
    local check_command="$3"
    local max_attempts=3
    
    echo "🔍 Checking $resource_type: $resource_name"
    
    for attempt in $(seq 1 $max_attempts); do
        echo "   Attempt $attempt/$max_attempts..."
        
        if timeout "${TIMEOUT}s" bash -c "$check_command" 2>/dev/null; then
            # Get resource location
            local location_cmd
            case "$resource_type" in
                "SQL Server")
                    location_cmd="az sql server show --name $resource_name --resource-group $RESOURCE_GROUP --query location -o tsv"
                    ;;
                "Container Registry")
                    location_cmd="az acr show --name $resource_name --resource-group $RESOURCE_GROUP --query location -o tsv"
                    ;;
                "Container Environment")
                    location_cmd="az containerapp env show --name $resource_name --resource-group $RESOURCE_GROUP --query location -o tsv"
                    ;;
                "Container App")
                    location_cmd="az containerapp show --name $resource_name --resource-group $RESOURCE_GROUP --query location -o tsv"
                    ;;
                *)
                    location_cmd="echo 'unknown'"
                    ;;
            esac
            
            local location
            if location=$(timeout 30s bash -c "$location_cmd" 2>/dev/null); then
                RESOURCE_LOCATIONS["$resource_name"]="$location"
                echo "   ✅ Healthy in region: $location"
            else
                RESOURCE_LOCATIONS["$resource_name"]="unknown"
                echo "   ✅ Healthy (location unknown)"
            fi
            
            RESOURCE_HEALTH["$resource_name"]="healthy"
            ((HEALTHY_RESOURCES++))
            return 0
        fi
        
        if [ $attempt -lt $max_attempts ]; then
            echo "   ⏳ Retrying in 5 seconds..."
            sleep 5
        fi
    done
    
    echo "   ❌ Failed after $max_attempts attempts"
    RESOURCE_HEALTH["$resource_name"]="failed"
    RESOURCE_LOCATIONS["$resource_name"]="unknown"
    ((FAILED_RESOURCES++))
    return 1
}

# Function to test cross-region connectivity
test_cross_region_connectivity() {
    echo ""
    echo "🌐 Testing Cross-Region Connectivity"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    
    for region in "${REGIONS[@]}"; do
        echo "📡 Testing region: $region"
        
        # Test Azure CLI connectivity to region
        local latency
        latency=$(measure_latency az account list-locations --query "[?name=='$region'].displayName" -o tsv)
        REGION_LATENCY["$region"]="$latency"
        
        if [ "$latency" -lt 5000 ]; then  # Less than 5 seconds
            echo "   ✅ Connectivity: ${latency}ms (Good)"
        elif [ "$latency" -lt 10000 ]; then  # Less than 10 seconds
            echo "   ⚠️ Connectivity: ${latency}ms (Slow)"
        else
            echo "   ❌ Connectivity: ${latency}ms (Poor)"
        fi
    done
}

# Main monitoring execution
echo ""
echo "🔍 Resource Health Checks"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Check SQL Server
if [ -n "${SQL_SERVER_NAME:-}" ]; then
    check_resource_health "SQL Server" "$SQL_SERVER_NAME" \
        "az sql server show --name $SQL_SERVER_NAME --resource-group $RESOURCE_GROUP --output none"
    ((TOTAL_RESOURCES++))
fi

# Check Container Registry
if [ -n "${REGISTRY_NAME:-}" ]; then
    check_resource_health "Container Registry" "$REGISTRY_NAME" \
        "az acr show --name $REGISTRY_NAME --resource-group $RESOURCE_GROUP --output none"
    ((TOTAL_RESOURCES++))
fi

# Check Container Environment
if [ -n "${CONTAINER_ENV_NAME:-}" ]; then
    check_resource_health "Container Environment" "$CONTAINER_ENV_NAME" \
        "az containerapp env show --name $CONTAINER_ENV_NAME --resource-group $RESOURCE_GROUP --output none"
    ((TOTAL_RESOURCES++))
fi

# Check Container Apps
if [ -n "${API_APP_NAME:-}" ]; then
    check_resource_health "Container App" "$API_APP_NAME" \
        "az containerapp show --name $API_APP_NAME --resource-group $RESOURCE_GROUP --output none"
    ((TOTAL_RESOURCES++))
fi

if [ -n "${UI_APP_NAME:-}" ]; then
    check_resource_health "Container App" "$UI_APP_NAME" \
        "az containerapp show --name $UI_APP_NAME --resource-group $RESOURCE_GROUP --output none"
    ((TOTAL_RESOURCES++))
fi

# Test cross-region connectivity
test_cross_region_connectivity

# Generate monitoring report
echo ""
echo "📊 Monitoring Summary"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "🎯 Resources Monitored: $TOTAL_RESOURCES"
echo "✅ Healthy Resources: $HEALTHY_RESOURCES"
echo "❌ Failed Resources: $FAILED_RESOURCES"

if [ $TOTAL_RESOURCES -gt 0 ]; then
    HEALTH_PERCENTAGE=$(( (HEALTHY_RESOURCES * 100) / TOTAL_RESOURCES ))
    echo "📈 Health Percentage: ${HEALTH_PERCENTAGE}%"
    
    if [ $HEALTH_PERCENTAGE -eq 100 ]; then
        echo "🎉 All resources are healthy!"
        exit 0
    elif [ $HEALTH_PERCENTAGE -ge 80 ]; then
        echo "⚠️ Most resources are healthy, but some issues detected"
        exit 1
    else
        echo "🚨 Significant resource health issues detected"
        exit 2
    fi
else
    echo "⚠️ No resources found to monitor"
    exit 1
fi

echo ""
echo "🌍 Regional Distribution:"
for resource_name in "${!RESOURCE_LOCATIONS[@]}"; do
    location="${RESOURCE_LOCATIONS[$resource_name]}"
    health="${RESOURCE_HEALTH[$resource_name]}"
    status_icon="❌"
    [ "$health" = "healthy" ] && status_icon="✅"
    echo "   $status_icon $resource_name → $location"
done

echo ""
echo "📡 Regional Latency (Average):"
for region in "${!REGION_LATENCY[@]}"; do
    latency="${REGION_LATENCY[$region]}"
    echo "   $region: ${latency}ms"
done