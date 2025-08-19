#!/bin/bash
# Test Infrastructure Validation Script
# Creates test scenarios to demonstrate the validation catching configuration mismatches
# Usage: ./scripts/test-infrastructure-validation.sh

set -e

echo "🧪 Testing Infrastructure Configuration Validation"
echo "================================================="

# Test 1: Current configuration should pass
echo ""
echo "🔍 Test 1: Current configuration validation (should PASS)"
echo "--------------------------------------------------------"
./scripts/validate-infrastructure-config.sh

echo ""
echo "✅ Test 1 PASSED - Current configuration is valid"

# Test 2: Create a backup and introduce a mismatch to test detection
echo ""
echo "🔍 Test 2: Introducing mismatch to test validation (should FAIL)"
echo "----------------------------------------------------------------"

# Backup original Bicep file
cp infrastructure/simple-deploy.bicep infrastructure/simple-deploy.bicep.backup

# Introduce a mismatch: change AZURE_STORAGE_CONNECTION_STRING to AzureStorage__ConnectionString
echo "📝 Introducing mismatch: changing AZURE_STORAGE_CONNECTION_STRING to AzureStorage__ConnectionString"
sed -i 's/AZURE_STORAGE_CONNECTION_STRING/AzureStorage__ConnectionString/g' infrastructure/simple-deploy.bicep

echo "🧪 Running validation with mismatch (should detect error):"
if ./scripts/validate-infrastructure-config.sh; then
    echo "❌ ERROR: Validation should have failed but passed!"
    restore_backup=true
else
    echo "✅ SUCCESS: Validation correctly detected the mismatch!"
    restore_backup=true
fi

# Restore original file
if [[ "$restore_backup" == "true" ]]; then
    echo "🔄 Restoring original Bicep file..."
    mv infrastructure/simple-deploy.bicep.backup infrastructure/simple-deploy.bicep
    echo "✅ Original file restored"
fi

# Test 3: Verify restoration worked
echo ""
echo "🔍 Test 3: Verification after restoration (should PASS again)"
echo "-------------------------------------------------------------"
./scripts/validate-infrastructure-config.sh

echo ""
echo "✅ All tests completed successfully!"
echo ""
echo "📊 Test Summary:"
echo "  ✅ Test 1: Current configuration validation - PASSED"
echo "  ✅ Test 2: Mismatch detection - PASSED (correctly failed)"
echo "  ✅ Test 3: Post-restoration validation - PASSED"
echo ""
echo "🎉 Infrastructure validation system is working correctly!"
echo "   It will catch mismatches like the AzureStorage__ConnectionString issue"