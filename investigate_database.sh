#!/bin/bash

# ===================================================================
# DATABASE INVESTIGATION SCRIPT
# AI Profile Photo Maker - Missing Tables Investigation
# ===================================================================
# Usage: ./investigate_database.sh
# 
# This script investigates the database migration and schema status
# to resolve the "Invalid object name 'CreditPackages'" errors.
# ===================================================================

set -e

API_BASE="https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io"
TIMESTAMP=$(date '+%Y%m%d_%H%M%S')
LOG_FILE="database_investigation_${TIMESTAMP}.log"

echo "==================================================================="
echo "DATABASE INVESTIGATION - $(date)"
echo "==================================================================="
echo "Target API: $API_BASE"
echo "Log file: $LOG_FILE"
echo ""

# Function to log and display
log_message() {
    local message="$1"
    echo "$message" | tee -a "$LOG_FILE"
}

# Function to make API calls and log responses
api_test() {
    local endpoint="$1"
    local description="$2"
    local expected_status="$3"
    
    log_message "📡 Testing: $description"
    log_message "   Endpoint: $API_BASE$endpoint"
    
    response=$(curl -s -w "%{http_code}" "$API_BASE$endpoint" 2>/dev/null || echo "CURL_ERROR")
    
    if [[ "$response" == "CURL_ERROR" ]]; then
        log_message "❌ CURL ERROR: Failed to connect to endpoint"
        return 1
    fi
    
    http_code="${response: -3}"
    body="${response%???}"
    
    log_message "   HTTP Status: $http_code"
    log_message "   Response: $body"
    
    # Check if response contains specific database errors
    if [[ "$body" == *"Invalid object name"* ]]; then
        log_message "🚨 DATABASE SCHEMA ERROR: Missing table detected"
        echo "$body" | grep -o "Invalid object name '[^']*'" | tee -a "$LOG_FILE"
    elif [[ "$body" == *"Cannot open database"* ]]; then
        log_message "🚨 DATABASE CONNECTION ERROR: Cannot access database"
    elif [[ "$body" == *"Login failed"* ]]; then
        log_message "🚨 DATABASE AUTH ERROR: Authentication failure"
    elif [[ "$http_code" == "200" ]]; then
        log_message "✅ SUCCESS: Endpoint working correctly"
    elif [[ "$http_code" == "404" ]]; then
        log_message "⚠️  ENDPOINT NOT FOUND: Route may not exist"
    else
        log_message "⚠️  UNEXPECTED RESPONSE: Status $http_code"
    fi
    
    log_message ""
    return 0
}

# Function to test database connectivity
test_connectivity() {
    log_message "🔍 STEP 1: Database Connectivity Test"
    log_message "================================================="
    
    # Test API health endpoint (if exists)
    api_test "/api/health" "Application Health Check" "200"
    
    # Test endpoints that require database
    api_test "/api/credit/packages" "Credit Packages (CreditPackages table)" "200"
    api_test "/api/styles/active" "Active Styles (Styles table)" "200"
    api_test "/api/credit/costs" "Credit Costs (Static data)" "200"
}

# Function to analyze missing tables
analyze_missing_tables() {
    log_message "🔍 STEP 2: Missing Tables Analysis"
    log_message "============================================="
    
    # Test each critical table by accessing endpoints that use them
    declare -A table_endpoints=(
        ["CreditPackages"]="/api/credit/packages"
        ["Styles"]="/api/styles/active"
        ["UserProfiles"]="/api/credit/status"  # Requires auth but will show different error
        ["Subscriptions"]="/api/subscription/plans"  # If exists
        ["ProcessedImages"]="/api/image/user-images"  # If exists
    )
    
    local missing_tables=()
    
    for table in "${!table_endpoints[@]}"; do
        endpoint="${table_endpoints[$table]}"
        log_message "Testing table: $table via $endpoint"
        
        response=$(curl -s "$API_BASE$endpoint" 2>/dev/null || echo "CURL_ERROR")
        
        if [[ "$response" == *"Invalid object name '$table'"* ]]; then
            missing_tables+=("$table")
            log_message "❌ MISSING: $table table not found"
        elif [[ "$response" == *"Invalid object name"* ]]; then
            # Extract the actual missing table name
            missing_table=$(echo "$response" | grep -o "Invalid object name '[^']*'" | sed "s/Invalid object name '//;s/'//" || echo "Unknown")
            if [[ ! " ${missing_tables[@]} " =~ " ${missing_table} " ]]; then
                missing_tables+=("$missing_table")
            fi
            log_message "❌ MISSING: $missing_table table not found (discovered via $table endpoint)"
        elif [[ "$response" == *"Unauthorized"* ]] || [[ "$response" == *"401"* ]]; then
            log_message "✅ EXISTS: $table table exists (auth required)"
        elif [[ "$response" == *"success"*true* ]] || [[ "$response" == "["* ]] || [[ "$response" == "{"* ]]; then
            log_message "✅ EXISTS: $table table exists and has data"
        else
            log_message "⚠️  UNKNOWN: $table status unclear - Response: ${response:0:100}..."
        fi
    done
    
    log_message ""
    if [ ${#missing_tables[@]} -gt 0 ]; then
        log_message "🚨 SUMMARY: Missing tables detected:"
        for table in "${missing_tables[@]}"; do
            log_message "   - $table"
        done
    else
        log_message "✅ SUMMARY: All tested tables appear to exist"
    fi
    log_message ""
}

# Function to check migration status
check_migration_status() {
    log_message "🔍 STEP 3: Migration Status Analysis"
    log_message "========================================="
    
    # Check if __EFMigrationsHistory table exists by testing a management endpoint
    log_message "Checking Entity Framework migration history..."
    
    # This is indirect since we can't directly query the database
    # We'll infer migration status from API responses
    response=$(curl -s "$API_BASE/api/credit/packages" 2>/dev/null || echo "CURL_ERROR")
    
    if [[ "$response" == *"Invalid object name"* ]]; then
        log_message "❌ MIGRATION STATUS: Migrations likely not applied or incomplete"
        log_message "   Evidence: Missing core tables that should be created by migrations"
    elif [[ "$response" == *"success"* ]] && [[ "$response" == *"["* ]]; then
        log_message "✅ MIGRATION STATUS: Migrations appear to be applied successfully"
        log_message "   Evidence: Core tables exist and return data"
    else
        log_message "⚠️  MIGRATION STATUS: Cannot determine migration status"
        log_message "   Response: ${response:0:200}..."
    fi
    
    log_message ""
}

# Function to check for schema-related errors
check_schema_errors() {
    log_message "🔍 STEP 4: Schema Error Pattern Analysis"
    log_message "==============================================" 
    
    # Test multiple endpoints to gather schema error patterns
    declare -a test_endpoints=(
        "/api/credit/packages"
        "/api/styles/active"
        "/api/credit/costs"
        "/api/credit/payment-config"
    )
    
    local schema_errors=()
    
    for endpoint in "${test_endpoints[@]}"; do
        response=$(curl -s "$API_BASE$endpoint" 2>/dev/null || echo "CURL_ERROR")
        
        if [[ "$response" == *"Invalid object name"* ]]; then
            # Extract table name from error
            table_name=$(echo "$response" | grep -o "Invalid object name '[^']*'" | sed "s/Invalid object name '//;s/'//" || echo "Unknown")
            if [[ ! " ${schema_errors[@]} " =~ " ${table_name} " ]]; then
                schema_errors+=("$table_name")
            fi
        fi
    done
    
    if [ ${#schema_errors[@]} -gt 0 ]; then
        log_message "🚨 SCHEMA ERRORS FOUND:"
        for error in "${schema_errors[@]}"; do
            log_message "   - Missing table: $error"
        done
        log_message ""
        log_message "🔧 LIKELY CAUSE: Database migrations did not run successfully"
        log_message "   The application is running but database schema is incomplete"
    else
        log_message "✅ No schema errors detected in tested endpoints"
    fi
    
    log_message ""
}

# Function to provide resolution recommendations
provide_resolution() {
    log_message "🔧 STEP 5: Resolution Recommendations"
    log_message "========================================="
    
    # Analyze the errors and provide specific recommendations
    response=$(curl -s "$API_BASE/api/credit/packages" 2>/dev/null || echo "CURL_ERROR")
    
    if [[ "$response" == *"Invalid object name 'CreditPackages'"* ]]; then
        log_message "🎯 PRIMARY ISSUE: CreditPackages table missing"
        log_message ""
        log_message "📋 RESOLUTION STEPS:"
        log_message ""
        log_message "1. FORCE MIGRATION EXECUTION:"
        log_message "   The application should auto-migrate on startup in non-development environments"
        log_message "   Check container app logs for migration failures:"
        log_message "   - Look for 'Running database migrations...' messages"
        log_message "   - Check for any database connection errors"
        log_message "   - Verify managed identity has DDL permissions"
        log_message ""
        log_message "2. MANUAL MIGRATION TRIGGER:"
        log_message "   If auto-migration failed, manually trigger via deployment:"
        log_message "   - Restart the container app to retry migrations"
        log_message "   - Check Azure SQL Database connectivity and permissions"
        log_message "   - Verify connection string in container app configuration"
        log_message ""
        log_message "3. DATABASE PERMISSIONS CHECK:"
        log_message "   Ensure managed identity has required permissions:"
        log_message "   - db_ddladmin role for creating/altering tables"
        log_message "   - db_datawriter role for seeding data"
        log_message "   - db_datareader role for reading data"
        log_message ""
        log_message "4. VERIFY LATEST MIGRATIONS:"
        log_message "   Latest migration should be: 20250729111502_RemoveEnterpriseAddStudioPack"
        log_message "   This should create/update CreditPackages with 3 packages"
        log_message ""
        log_message "5. CONNECTION STRING VALIDATION:"
        log_message "   Current connection from appsettings.Staging.json:"
        log_message "   Server: aiprofilemaker-sql-staging.database.windows.net"
        log_message "   Database: aiprofilemakerdb"
        log_message "   Authentication: Active Directory Managed Identity"
        log_message ""
        
    else
        log_message "✅ No CreditPackages errors detected"
        log_message "   The specific issue mentioned may be resolved"
    fi
    
    log_message "🔄 IMMEDIATE ACTION ITEMS:"
    log_message "1. Restart container app to retry migrations"
    log_message "2. Check container app logs for migration errors"
    log_message "3. Verify database connectivity and permissions"
    log_message "4. Test API endpoints after restart"
    log_message ""
}

# Function to test API endpoints after resolution
test_post_resolution() {
    log_message "🔍 STEP 6: Post-Resolution Verification"
    log_message "========================================="
    
    log_message "Run these tests after implementing resolution steps:"
    log_message ""
    log_message "# Test CreditPackages table:"
    log_message "curl '$API_BASE/api/credit/packages'"
    log_message ""
    log_message "# Test Styles table:"
    log_message "curl '$API_BASE/api/styles/active'"
    log_message ""
    log_message "# Test application health:"
    log_message "curl '$API_BASE/api/credit/costs'"
    log_message ""
    log_message "# Expected: All should return HTTP 200 with JSON data"
    log_message ""
}

# Main execution
main() {
    log_message "Starting database investigation for missing tables issue..."
    log_message "Target environment: Staging"
    log_message "Primary symptom: HTTP 500 'Invalid object name CreditPackages'"
    log_message ""
    
    test_connectivity
    analyze_missing_tables
    check_migration_status
    check_schema_errors
    provide_resolution
    test_post_resolution
    
    log_message "==================================================================="
    log_message "INVESTIGATION COMPLETE - $(date)"
    log_message "==================================================================="
    log_message "Log saved to: $LOG_FILE"
    log_message ""
    log_message "📋 QUICK SUMMARY:"
    
    # Check final status
    response=$(curl -s "$API_BASE/api/credit/packages" 2>/dev/null || echo "CURL_ERROR")
    if [[ "$response" == *"Invalid object name"* ]]; then
        log_message "🚨 STATUS: Issue still exists - CreditPackages table missing"
        log_message "🎯 ACTION: Follow resolution steps above to fix database schema"
    elif [[ "$response" == *"success"* ]]; then
        log_message "✅ STATUS: Issue resolved - CreditPackages table exists and working"
    else
        log_message "⚠️  STATUS: Unclear - Investigate further with resolution steps"
    fi
    
    log_message ""
    log_message "For detailed analysis, see full log: $LOG_FILE"
}

# Execute main function
main

echo ""
echo "🎯 Next Steps:"
echo "1. Review the investigation log: $LOG_FILE"
echo "2. If issues found, restart the container app to retry migrations"
echo "3. Check Azure Container App logs for migration execution details"
echo "4. Verify Azure SQL Database managed identity permissions"
echo "5. Re-run this script after implementing fixes to verify resolution"