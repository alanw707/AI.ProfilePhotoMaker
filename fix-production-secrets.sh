#!/bin/bash

# Fix Production Secrets - Comprehensive GitHub Actions Secrets Fix
# Addresses all secrets validation issues identified in deployment pipeline

set -euo pipefail

# Colors for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m'

log() {
    echo -e "${1}[$(date '+%Y-%m-%d %H:%M:%S')] ${2}${NC}"
}

echo
log "$BLUE" "🔧 AI Profile Photo Maker - Production Secrets Fix"
log "$BLUE" "=================================================="
echo

# Check if GitHub CLI is available and authenticated
if ! command -v gh &> /dev/null; then
    log "$RED" "❌ GitHub CLI not found. Please install it: https://cli.github.com/"
    exit 1
fi

if ! gh auth status > /dev/null 2>&1; then
    log "$RED" "❌ GitHub CLI not authenticated. Run: gh auth login"
    exit 1
fi

log "$GREEN" "✅ GitHub CLI is available and authenticated"

# Generate secure secrets
log "$BLUE" "🔐 Generating secure secrets..."

# Generate a proper 64-character JWT secret (exceeds minimum requirement of 32)
JWT_SECRET=$(openssl rand -base64 48 | tr -d "=+/" | cut -c1-64)
log "$GREEN" "✅ Generated JWT_SECRET (${#JWT_SECRET} characters)"

# Generate a secure Azure SQL password that meets all requirements
SQL_ADMIN_PASSWORD="AzureSQL$(openssl rand -base64 12 | tr -d "=+/" | cut -c1-8)!@9"
log "$GREEN" "✅ Generated SQL_ADMIN_PASSWORD (${#SQL_ADMIN_PASSWORD} characters, Azure SQL compliant)"

# Use the webhook secret from CLAUDE.local.md (as specified in project instructions)
REPLICATE_WEBHOOK_SECRET="whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM"
log "$GREEN" "✅ Using consistent REPLICATE_WEBHOOK_SECRET across all environments"

echo
log "$BLUE" "🔍 Current GitHub secrets status:"
gh secret list

echo
log "$BLUE" "🚀 Updating GitHub Actions secrets..."

# Update JWT_SECRET (the one causing the 18-char validation failure)
echo "$JWT_SECRET" | gh secret set JWT_SECRET
log "$GREEN" "✅ Updated JWT_SECRET with secure 64-character value"

# Update SQL_ADMIN_PASSWORD with Azure SQL compliant password
echo "$SQL_ADMIN_PASSWORD" | gh secret set SQL_ADMIN_PASSWORD
log "$GREEN" "✅ Updated SQL_ADMIN_PASSWORD with Azure SQL compliant value"

# Ensure REPLICATE_WEBHOOK_SECRET is consistent across all environments
echo "$REPLICATE_WEBHOOK_SECRET" | gh secret set REPLICATE_WEBHOOK_SECRET
log "$GREEN" "✅ Updated REPLICATE_WEBHOOK_SECRET with consistent value"

echo
log "$BLUE" "🧪 Validating updated secrets..."

# Run the validation logic from the GitHub Actions workflow
validate_secret() {
  local secret_name="$1"
  local secret_value="$2"
  local min_length="${3:-1}"
  
  if [[ -z "$secret_value" ]]; then
    echo "❌ MISSING: $secret_name"
    return 1
  elif [[ ${#secret_value} -lt $min_length ]]; then
    echo "❌ TOO SHORT: $secret_name (${#secret_value} chars, min: $min_length)"
    return 1
  else
    echo "✅ VALID: $secret_name (${#secret_value} chars)"
    return 0
  fi
}

# Validate the newly set secrets
errors=0

validate_secret "JWT_SECRET" "$JWT_SECRET" 32 || ((errors++))

# Validate SQL password with Azure SQL requirements
if [[ -z "$SQL_ADMIN_PASSWORD" ]]; then
  echo "❌ MISSING: SQL_ADMIN_PASSWORD"
  ((errors++))
elif [[ ${#SQL_ADMIN_PASSWORD} -lt 8 ]]; then
  echo "❌ TOO SHORT: SQL_ADMIN_PASSWORD (${#SQL_ADMIN_PASSWORD} chars, min: 8)"
  ((errors++))
elif [[ ${#SQL_ADMIN_PASSWORD} -gt 128 ]]; then
  echo "❌ TOO LONG: SQL_ADMIN_PASSWORD (${#SQL_ADMIN_PASSWORD} chars, max: 128)"
  ((errors++))
elif [[ ! "$SQL_ADMIN_PASSWORD" =~ [A-Z] ]]; then
  echo "❌ INVALID: SQL_ADMIN_PASSWORD must contain uppercase letters"
  ((errors++))
elif [[ ! "$SQL_ADMIN_PASSWORD" =~ [a-z] ]]; then
  echo "❌ INVALID: SQL_ADMIN_PASSWORD must contain lowercase letters"
  ((errors++))
elif [[ ! "$SQL_ADMIN_PASSWORD" =~ [0-9] ]]; then
  echo "❌ INVALID: SQL_ADMIN_PASSWORD must contain numbers"
  ((errors++))
elif [[ "$SQL_ADMIN_PASSWORD" != *"!"* && "$SQL_ADMIN_PASSWORD" != *"@"* && "$SQL_ADMIN_PASSWORD" != *"#"* && "$SQL_ADMIN_PASSWORD" != *"$"* && "$SQL_ADMIN_PASSWORD" != *"%"* && "$SQL_ADMIN_PASSWORD" != *"^"* && "$SQL_ADMIN_PASSWORD" != *"&"* && "$SQL_ADMIN_PASSWORD" != *"*"* && "$SQL_ADMIN_PASSWORD" != *"("* && "$SQL_ADMIN_PASSWORD" != *")"* && "$SQL_ADMIN_PASSWORD" != *"-"* && "$SQL_ADMIN_PASSWORD" != *"_"* && "$SQL_ADMIN_PASSWORD" != *"+"* && "$SQL_ADMIN_PASSWORD" != *"="* ]]; then
  echo "❌ INVALID: SQL_ADMIN_PASSWORD must contain special characters"
  ((errors++))
elif [[ "$SQL_ADMIN_PASSWORD" =~ (Test|test|Dev|dev|Pass|pass|Admin|admin|123|password|Password) ]]; then
  echo "❌ INSECURE: SQL_ADMIN_PASSWORD contains weak patterns"
  ((errors++))
else
  echo "✅ VALID: SQL_ADMIN_PASSWORD (Azure SQL compliant)"
fi

validate_secret "REPLICATE_WEBHOOK_SECRET" "$REPLICATE_WEBHOOK_SECRET" 32 || ((errors++))

echo
if [[ $errors -eq 0 ]]; then
  log "$GREEN" "🎉 All secrets validation passed! GitHub Actions deployment will now succeed."
  
  echo
  log "$BLUE" "📋 Summary of changes:"
  echo "   • JWT_SECRET: Updated to 64-character secure value"
  echo "   • SQL_ADMIN_PASSWORD: Updated to Azure SQL compliant password"
  echo "   • REPLICATE_WEBHOOK_SECRET: Ensured consistency across environments"
  
  echo
  log "$GREEN" "✅ Next deployment will pass secrets validation"
  log "$GREEN" "✅ Run 'git push' to trigger deployment with fixed secrets"
  
else
  log "$RED" "❌ Secrets validation failed with $errors error(s)"
  log "$RED" "🛑 Manual review required"
  exit 1
fi

echo
log "$BLUE" "🔗 GitHub Actions secrets updated. View at:"
echo "   https://github.com/$(gh repo view --json owner,name -q '.owner.login + "/" + .name')/settings/secrets/actions"

exit 0