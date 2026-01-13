#!/bin/bash
# Validate local development configuration consistency
# Run before dev-rebuild.sh to catch password mismatches

set -e
cd "$(dirname "$0")/.."

echo "🔍 Validating local development configuration..."
echo ""

ERRORS=0

# Extract passwords from different sources
ENV_PASSWORD=$(grep "^MSSQL_SA_PASSWORD=" .env 2>/dev/null | cut -d'=' -f2 | tr -d '"' || echo "")
ENV_DEV_PASSWORD=$(grep "^MSSQL_SA_PASSWORD=" AI.ProfilePhotoMaker.API/.env.development 2>/dev/null | cut -d'=' -f2 | tr -d '"' || echo "")

# Extract password from connection string in .env.development
ENV_DEV_CONN_PASSWORD=$(grep "ConnectionStrings__DefaultConnection=" AI.ProfilePhotoMaker.API/.env.development 2>/dev/null | sed -n 's/.*Password=\([^;]*\).*/\1/p' | tr -d '"' || echo "")

echo "📋 Configuration Sources:"
echo "  .env MSSQL_SA_PASSWORD: ${ENV_PASSWORD:-❌ NOT SET}"
echo "  .env.development MSSQL_SA_PASSWORD: ${ENV_DEV_PASSWORD:-❌ NOT SET}"
echo "  .env.development ConnectionString Password: ${ENV_DEV_CONN_PASSWORD:-❌ NOT SET}"
echo ""

# Check if passwords match
if [ -n "$ENV_PASSWORD" ] && [ -n "$ENV_DEV_PASSWORD" ] && [ "$ENV_PASSWORD" != "$ENV_DEV_PASSWORD" ]; then
    echo "❌ ERROR: .env and .env.development MSSQL_SA_PASSWORD don't match!"
    echo "   .env: $ENV_PASSWORD"
    echo "   .env.development: $ENV_DEV_PASSWORD"
    ERRORS=$((ERRORS + 1))
fi

if [ -n "$ENV_PASSWORD" ] && [ -n "$ENV_DEV_CONN_PASSWORD" ] && [ "$ENV_PASSWORD" != "$ENV_DEV_CONN_PASSWORD" ]; then
    echo "❌ ERROR: .env password doesn't match .env.development connection string!"
    echo "   .env: $ENV_PASSWORD"
    echo "   ConnectionString: $ENV_DEV_CONN_PASSWORD"
    ERRORS=$((ERRORS + 1))
fi

# Check for special characters that cause issues
if echo "$ENV_PASSWORD" | grep -q '#'; then
    echo "⚠️  WARNING: Password contains '#' which can cause parsing issues"
    ERRORS=$((ERRORS + 1))
fi

# Check if connection string is quoted in .env.development
if grep -q "^ConnectionStrings__DefaultConnection=[^\"']" AI.ProfilePhotoMaker.API/.env.development 2>/dev/null; then
    echo "❌ ERROR: ConnectionStrings__DefaultConnection in .env.development is NOT quoted!"
    echo "   This will cause the value to be truncated at the first ';'"
    ERRORS=$((ERRORS + 1))
fi

# Check SQL Server container status
if docker ps --format '{{.Names}}' 2>/dev/null | grep -q "aipm-sqlserver"; then
    echo "✅ SQL Server container is running"
else
    echo "⚠️  SQL Server container is not running"
fi

echo ""
if [ $ERRORS -eq 0 ]; then
    echo "✅ All configuration checks passed!"
    exit 0
else
    echo "❌ Found $ERRORS configuration issue(s). Fix before running dev-rebuild.sh"
    exit 1
fi
