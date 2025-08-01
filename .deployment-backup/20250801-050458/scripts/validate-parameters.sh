#!/bin/bash
# Parameter Validation Script - Prevents invalid configurations

PARAM_FILE="$1"
if [ -z "$PARAM_FILE" ]; then
    echo "Usage: $0 <parameters-file>"
    exit 1
fi

echo "🔍 Validating parameters file: $PARAM_FILE"

# Check if file exists
if [ ! -f "$PARAM_FILE" ]; then
    echo "❌ Parameters file not found: $PARAM_FILE"
    exit 1
fi

# Validate JSON syntax
if ! jq empty "$PARAM_FILE" 2>/dev/null; then
    echo "❌ Invalid JSON syntax in parameters file"
    exit 1
fi

# Extract values
NAME_PREFIX=$(jq -r '.parameters.namePrefix.value' "$PARAM_FILE")
ENVIRONMENT=$(jq -r '.parameters.environmentName.value' "$PARAM_FILE")
SQL_PASSWORD=$(jq -r '.parameters.sqlAdminPassword.value' "$PARAM_FILE")

echo "📊 Parameter values:"
echo "  namePrefix: $NAME_PREFIX"
echo "  environmentName: $ENVIRONMENT"

# Validation rules
VALIDATION_FAILED=false

# Rule 1: namePrefix length (for storage account naming)
if [ ${#NAME_PREFIX} -gt 14 ]; then
    echo "❌ namePrefix too long: ${#NAME_PREFIX} chars (max 14 for storage account naming)"
    VALIDATION_FAILED=true
else
    echo "✅ namePrefix length valid: ${#NAME_PREFIX} chars"
fi

# Rule 2: namePrefix allowed values
if [[ "$NAME_PREFIX" != "aiprofile" ]]; then
    echo "❌ namePrefix must be 'aiprofile' for standardization (got: '$NAME_PREFIX')"
    VALIDATION_FAILED=true
else
    echo "✅ namePrefix follows standard convention"
fi

# Rule 3: Environment validation
if [[ "$ENVIRONMENT" != "staging" && "$ENVIRONMENT" != "production" ]]; then
    echo "❌ environmentName must be 'staging' or 'production' (got: '$ENVIRONMENT')"
    VALIDATION_FAILED=true
else
    echo "✅ environmentName is valid"
fi

# Rule 4: SQL password complexity
if [[ ${#SQL_PASSWORD} -lt 8 ]]; then
    echo "❌ SQL password too short (minimum 8 characters)"
    VALIDATION_FAILED=true
elif [[ ! "$SQL_PASSWORD" =~ [A-Z] ]]; then
    echo "❌ SQL password missing uppercase letter"
    VALIDATION_FAILED=true
elif [[ ! "$SQL_PASSWORD" =~ [a-z] ]]; then
    echo "❌ SQL password missing lowercase letter"
    VALIDATION_FAILED=true
elif [[ ! "$SQL_PASSWORD" =~ [0-9] ]]; then
    echo "❌ SQL password missing number"
    VALIDATION_FAILED=true
elif [[ ! "$SQL_PASSWORD" =~ [^a-zA-Z0-9] ]]; then
    echo "❌ SQL password missing special character"
    VALIDATION_FAILED=true
else
    echo "✅ SQL password meets complexity requirements"
fi

# Rule 5: Storage account name preview
UNIQUE_SUFFIX=$(echo -n "ai-profile-photo-maker-$ENVIRONMENT" | md5sum | cut -c1-13)
EXPECTED_STORAGE_NAME="${NAME_PREFIX:0:14}st${UNIQUE_SUFFIX:0:8}"

if [ ${#EXPECTED_STORAGE_NAME} -gt 24 ]; then
    echo "❌ Expected storage account name too long: ${#EXPECTED_STORAGE_NAME} chars (max 24)"
    echo "   Expected name: $EXPECTED_STORAGE_NAME"
    VALIDATION_FAILED=true
else
    echo "✅ Expected storage account name length valid: ${#EXPECTED_STORAGE_NAME} chars"
    echo "   Expected name: $EXPECTED_STORAGE_NAME"
fi

# Final result
if [ "$VALIDATION_FAILED" = true ]; then
    echo ""
    echo "❌ VALIDATION FAILED - Please fix the above issues"
    exit 1
else
    echo ""
    echo "✅ VALIDATION PASSED - Parameters are valid"
    exit 0
fi
