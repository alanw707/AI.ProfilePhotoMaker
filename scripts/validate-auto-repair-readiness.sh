#!/bin/bash

# Auto-Repair Readiness Validation Script
# Validates that the system is ready for auto-repair re-enablement

set -e

echo "🔧 Auto-Repair Readiness Validation"
echo "===================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Validation results
VALIDATION_RESULTS=()
ERROR_COUNT=0
WARNING_COUNT=0

# Function to add validation result
add_result() {
    local status=$1
    local message=$2
    local details=$3
    
    case $status in
        "PASS")
            echo -e "${GREEN}✅ PASS${NC}: $message"
            ;;
        "WARN")
            echo -e "${YELLOW}⚠️  WARN${NC}: $message"
            if [ ! -z "$details" ]; then
                echo -e "   ${YELLOW}└─${NC} $details"
            fi
            WARNING_COUNT=$((WARNING_COUNT + 1))
            ;;
        "FAIL")
            echo -e "${RED}❌ FAIL${NC}: $message"
            if [ ! -z "$details" ]; then
                echo -e "   ${RED}└─${NC} $details"
            fi
            ERROR_COUNT=$((ERROR_COUNT + 1))
            ;;
    esac
    
    VALIDATION_RESULTS+=("$status: $message")
}

echo -e "\n${BLUE}1. Data Migration Status${NC}"
echo "-------------------------"

# Check if migration is complete
if [ -f "migration-status.json" ]; then
    MIGRATION_STATUS=$(jq -r '.status' migration-status.json 2>/dev/null || echo "unknown")
    if [ "$MIGRATION_STATUS" == "completed" ]; then
        add_result "PASS" "Data migration completed successfully"
    elif [ "$MIGRATION_STATUS" == "in_progress" ]; then
        add_result "FAIL" "Data migration still in progress" "Auto-repair should not be enabled during migration"
    else
        add_result "WARN" "Migration status unclear" "Check migration-status.json file"
    fi
else
    add_result "WARN" "Migration status file not found" "Manually verify migration completion"
fi

echo -e "\n${BLUE}2. Database Consistency${NC}"
echo "------------------------"

# Check database connection
if command -v dotnet >/dev/null 2>&1; then
    cd AI.ProfilePhotoMaker.API
    
    # Test database connection
    if dotnet run --project . --verbosity quiet -- --test-db-connection >/dev/null 2>&1; then
        add_result "PASS" "Database connection successful"
    else
        add_result "FAIL" "Database connection failed" "Check connection string and database availability"
    fi
    
    cd ..
else
    add_result "WARN" "dotnet CLI not available" "Cannot test database connection"
fi

echo -e "\n${BLUE}3. Azure Storage Validation${NC}"
echo "----------------------------"

# Check Azure Storage environment variables
if [ ! -z "$AZURE_STORAGE_CONNECTION_STRING" ] && [ "$AZURE_STORAGE_CONNECTION_STRING" != "UseDevelopmentStorage=true" ]; then
    add_result "PASS" "Azure Storage connection string configured"
else
    add_result "FAIL" "Azure Storage not properly configured" "AZURE_STORAGE_CONNECTION_STRING required for auto-repair"
fi

if [ ! -z "$AZURE_STORAGE_CONTAINER_NAME" ]; then
    add_result "PASS" "Azure Storage container name configured"
else
    add_result "FAIL" "Azure Storage container name not configured" "AZURE_STORAGE_CONTAINER_NAME required"
fi

echo -e "\n${BLUE}4. Feature Flag Configuration${NC}"
echo "-------------------------------"

# Check environment files for feature flag configuration
ENV_FILE="AI.ProfilePhotoMaker.UI/src/environments/environment.ts"
if [ -f "$ENV_FILE" ]; then
    if grep -q "enableImageValidation" "$ENV_FILE"; then
        add_result "PASS" "Image validation feature flag found"
    else
        add_result "WARN" "Image validation feature flag not found" "Add enableImageValidation to environment"
    fi
    
    if grep -q "enableAutoRepair" "$ENV_FILE"; then
        add_result "PASS" "Auto-repair feature flag found"
    else
        add_result "WARN" "Auto-repair feature flag not found" "Add enableAutoRepair to environment"
    fi
else
    add_result "WARN" "Environment file not found" "Cannot validate feature flag configuration"
fi

echo -e "\n${BLUE}5. Backend Repair Endpoint${NC}"
echo "----------------------------"

# Check if repair endpoint exists in ImageController
CONTROLLER_FILE="AI.ProfilePhotoMaker.API/Controllers/ImageController.cs"
if [ -f "$CONTROLLER_FILE" ]; then
    if grep -q "reconcile-database" "$CONTROLLER_FILE"; then
        add_result "PASS" "Repair endpoint found in ImageController"
    else
        add_result "FAIL" "Repair endpoint not found" "Check ImageController for reconcile-database endpoint"
    fi
else
    add_result "FAIL" "ImageController file not found" "Backend repair functionality missing"
fi

echo -e "\n${BLUE}6. Frontend Service Integration${NC}"
echo "--------------------------------"

# Check frontend repair service method
SERVICE_FILE="AI.ProfilePhotoMaker.UI/src/app/services/file-upload.service.ts"
if [ -f "$SERVICE_FILE" ]; then
    if grep -q "repairImageDatabase" "$SERVICE_FILE"; then
        add_result "PASS" "Frontend repair service method found"
    else
        add_result "FAIL" "Frontend repair service method not found" "Add repairImageDatabase method to file-upload.service.ts"
    fi
else
    add_result "FAIL" "File upload service not found" "Frontend repair integration missing"
fi

echo -e "\n${BLUE}7. Monitoring and Logging${NC}"
echo "---------------------------"

# Check for logging configuration
if [ -f "AI.ProfilePhotoMaker.API/appsettings.json" ]; then
    if grep -q "Logging" "AI.ProfilePhotoMaker.API/appsettings.json"; then
        add_result "PASS" "Logging configuration found"
    else
        add_result "WARN" "Logging configuration not found" "Ensure proper logging for auto-repair operations"
    fi
else
    add_result "WARN" "appsettings.json not found" "Cannot validate logging configuration"
fi

echo -e "\n${BLUE}8. Test Coverage${NC}"
echo "-----------------"

# Check for auto-repair tests
TEST_FILES=$(find . -name "*test*" -type f -name "*.ts" -o -name "*.spec.ts" 2>/dev/null | grep -i "repair\|image.*state\|dashboard.*state" | wc -l)
if [ "$TEST_FILES" -gt 0 ]; then
    add_result "PASS" "Auto-repair related tests found ($TEST_FILES files)"
else
    add_result "WARN" "Auto-repair tests not found" "Create comprehensive test suite before enabling"
fi

echo -e "\n${BLUE}9. Security Validation${NC}"
echo "-----------------------"

# Check if repair endpoints are properly secured
if [ -f "$CONTROLLER_FILE" ]; then
    if grep -A 5 -B 5 "reconcile-database" "$CONTROLLER_FILE" | grep -q "Authorize"; then
        add_result "PASS" "Repair endpoint is properly secured"
    else
        add_result "FAIL" "Repair endpoint not secured" "Add [Authorize] attribute to repair endpoints"
    fi
fi

echo -e "\n${BLUE}10. Documentation${NC}"
echo "------------------"

# Check for documentation
if [ -f "AUTO_REPAIR_RE_ENABLEMENT_PLAN.md" ]; then
    add_result "PASS" "Auto-repair documentation found"
else
    add_result "WARN" "Auto-repair documentation not found" "Create comprehensive documentation before enabling"
fi

# Summary
echo -e "\n${BLUE}Validation Summary${NC}"
echo "=================="
echo -e "Total Checks: ${#VALIDATION_RESULTS[@]}"
echo -e "${RED}Errors: $ERROR_COUNT${NC}"
echo -e "${YELLOW}Warnings: $WARNING_COUNT${NC}"
echo -e "${GREEN}Passed: $((${#VALIDATION_RESULTS[@]} - ERROR_COUNT - WARNING_COUNT))${NC}"

if [ $ERROR_COUNT -gt 0 ]; then
    echo -e "\n${RED}❌ VALIDATION FAILED${NC}"
    echo "Auto-repair should NOT be enabled until all errors are resolved."
    exit 1
elif [ $WARNING_COUNT -gt 0 ]; then
    echo -e "\n${YELLOW}⚠️  VALIDATION PASSED WITH WARNINGS${NC}"
    echo "Auto-repair can be enabled, but warnings should be addressed."
    exit 2
else
    echo -e "\n${GREEN}✅ VALIDATION PASSED${NC}"
    echo "System is ready for auto-repair re-enablement."
    exit 0
fi