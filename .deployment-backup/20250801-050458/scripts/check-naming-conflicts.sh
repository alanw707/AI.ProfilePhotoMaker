#!/bin/bash
# Naming Convention Checker - Prevents resource name conflicts

RESOURCE_GROUP="$1"
NAME_PREFIX="$2"
ENVIRONMENT="$3"

if [ -z "$RESOURCE_GROUP" ] || [ -z "$NAME_PREFIX" ] || [ -z "$ENVIRONMENT" ]; then
    echo "Usage: $0 <resource-group> <name-prefix> <environment>"
    exit 1
fi

echo "🔍 Checking naming conflicts in resource group: $RESOURCE_GROUP"
echo "   Name prefix: $NAME_PREFIX"
echo "   Environment: $ENVIRONMENT"

# Check Azure CLI login
if ! az account show > /dev/null 2>&1; then
    echo "❌ Not logged into Azure. Please run: az login"
    exit 1
fi

# Check if resource group exists
if ! az group show --name "$RESOURCE_GROUP" > /dev/null 2>&1; then
    echo "❌ Resource group '$RESOURCE_GROUP' not found"
    exit 1
fi

CONFLICTS_FOUND=false

# Check for conflicting naming patterns
echo ""
echo "🏷️  Checking for conflicting naming patterns..."

# Check for old 'aiapp' pattern resources
OLD_PATTERN_COUNT=$(az resource list \
    --resource-group "$RESOURCE_GROUP" \
    --query "[?contains(name, 'aiapp')] | length(@)" -o tsv)

if [ "$OLD_PATTERN_COUNT" -gt 0 ]; then
    echo "⚠️  Found $OLD_PATTERN_COUNT resources with old 'aiapp' pattern"
    echo "   These should be cleaned up before deployment:"
    az resource list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiapp')].{Name:name, Type:type}" \
        --output table
    CONFLICTS_FOUND=true
else
    echo "✅ No old 'aiapp' pattern resources found"
fi

# Check for long namePrefix resources
LONG_PREFIX_COUNT=$(az resource list \
    --resource-group "$RESOURCE_GROUP" \
    --query "[?contains(name, 'aiprofilephotomaker')] | length(@)" -o tsv)

if [ "$LONG_PREFIX_COUNT" -gt 0 ]; then
    echo "⚠️  Found $LONG_PREFIX_COUNT resources with long 'aiprofilephotomaker' pattern"
    echo "   These may have naming limit issues:"
    az resource list \
        --resource-group "$RESOURCE_GROUP" \
        --query "[?contains(name, 'aiprofilephotomaker')].{Name:name, Type:type}" \
        --output table
    CONFLICTS_FOUND=true
else
    echo "✅ No long prefix pattern resources found"
fi

# Check current deployment targets
echo ""
echo "🎯 Checking current deployment targets..."

# Generate expected resource names
UNIQUE_SUFFIX=$(echo -n "$RESOURCE_GROUP" | md5sum | cut -c1-13)

EXPECTED_RESOURCES=(
    "${NAME_PREFIX}-asp-${ENVIRONMENT}"
    "${NAME_PREFIX}api-${ENVIRONMENT}"
    "${NAME_PREFIX}-swa-${ENVIRONMENT}"
    "${NAME_PREFIX}-sql-${ENVIRONMENT}-${UNIQUE_SUFFIX}"
    "${NAME_PREFIX}db"
    "${NAME_PREFIX:0:14}st${UNIQUE_SUFFIX:0:8}"
    "${NAME_PREFIX}-kv-${ENVIRONMENT}-${UNIQUE_SUFFIX}"
    "${NAME_PREFIX}-ai-${ENVIRONMENT}"
    "${NAME_PREFIX}-la-${ENVIRONMENT}"
)

for RESOURCE_NAME in "${EXPECTED_RESOURCES[@]}"; do
    if az resource show --resource-group "$RESOURCE_GROUP" --name "$RESOURCE_NAME" > /dev/null 2>&1; then
        echo "ℹ️  Resource '$RESOURCE_NAME' already exists (will be updated)"
    else
        echo "✅ Resource name '$RESOURCE_NAME' available"
    fi
done

# Final result
echo ""
if [ "$CONFLICTS_FOUND" = true ]; then
    echo "⚠️  NAMING CONFLICTS DETECTED"
    echo "   Run cleanup script before deploying: ./azure-resource-cleanup.sh"
    exit 1
else
    echo "✅ NO NAMING CONFLICTS - Safe to deploy"
    exit 0
fi
