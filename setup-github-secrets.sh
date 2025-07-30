#!/bin/bash

# GitHub Secrets Automation Script
# Adds all required secrets for Azure deployment

set -e

echo "🔐 Setting up GitHub Actions secrets for Azure deployment..."

# Check if GitHub CLI is authenticated
if ! gh auth status > /dev/null 2>&1; then
    echo "❌ GitHub CLI not authenticated. Please run: gh auth login"
    exit 1
fi

# Get current repository info
REPO=$(gh repo view --json nameWithOwner -q .nameWithOwner)
echo "📦 Repository: $REPO"

# Define secrets
declare -A SECRETS=(
    ["STAGING_SQL_ADMIN_PASSWORD"]="UnPxWvveYHDkCiCH2025!@#"
    ["STAGING_JWT_SECRET"]="e7b7c0c2-4b6e-4c2d-8e7a-1b2f3c4d5e6f"
    ["PROD_SQL_ADMIN_PASSWORD"]="JkGNdDTct101gGAj2025!$%"
    ["PROD_JWT_SECRET"]="oznZk9rcI2LWwPbX6LoIx3BFGu0s4ldq4OwdIMy8/II="
    ["REPLICATE_API_TOKEN"]="r8_FvCjahczdLfNmFTcDjhMzMuEuqKQswx2BxWD1"
    ["REPLICATE_WEBHOOK_SECRET"]="whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM"
)

echo ""
echo "🚀 Adding secrets to GitHub repository..."

# Add each secret
for SECRET_NAME in "${!SECRETS[@]}"; do
    SECRET_VALUE="${SECRETS[$SECRET_NAME]}"
    
    echo "  ➤ Adding secret: $SECRET_NAME"
    
    # Add secret using GitHub CLI
    echo "$SECRET_VALUE" | gh secret set "$SECRET_NAME"
    
    if [ $? -eq 0 ]; then
        echo "    ✅ $SECRET_NAME added successfully"
    else
        echo "    ❌ Failed to add $SECRET_NAME"
    fi
done

echo ""
echo "🔍 Verifying secrets were added..."

# List all secrets to verify
gh secret list

echo ""
echo "✅ GitHub Actions secrets setup complete!"
echo ""
echo "🎯 Next steps:"
echo "1. Push your code to main branch"
echo "2. Staging will auto-deploy"
echo "3. Use GitHub Actions UI for production deployment"
echo ""
echo "🔗 GitHub Actions: https://github.com/$REPO/actions"