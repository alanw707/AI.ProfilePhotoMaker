#!/bin/bash

# Safe Deployment Cleanup Script
# Removes obsolete deployment files while preserving active staging setup

echo "🧹 Starting deployment cleanup..."

# Create backup directory
mkdir -p .deployment-backup/$(date +%Y%m%d-%H%M%S)
BACKUP_DIR=".deployment-backup/$(date +%Y%m%d-%H%M%S)"

echo "📦 Creating backup at $BACKUP_DIR"

# Function to backup and remove
backup_and_remove() {
    local file="$1"
    if [ -f "$file" ]; then
        echo "🗑️  Removing: $file"
        cp "$file" "$BACKUP_DIR/$(basename "$file")" 2>/dev/null || true
        rm "$file"
    fi
}

backup_and_remove_dir() {
    local dir="$1"
    if [ -d "$dir" ]; then
        echo "🗑️  Removing directory: $dir"
        cp -r "$dir" "$BACKUP_DIR/" 2>/dev/null || true
        rm -rf "$dir"
    fi
}

# Remove redundant infrastructure files
echo "🏗️  Cleaning infrastructure files..."
backup_and_remove "infrastructure/main.bicep"
backup_and_remove "infrastructure/container-apps-main.bicep"
backup_and_remove "infrastructure/main.json"
backup_and_remove "infrastructure/main.test.json"
backup_and_remove "infrastructure/main.validated.json"

# Remove parameter files
echo "📋 Cleaning parameter files..."
backup_and_remove "infrastructure/parameters.dev.json"
backup_and_remove "infrastructure/parameters.prod.json"
backup_and_remove "infrastructure/parameters.staging.json"
backup_and_remove "infrastructure/parameters.staging.local.json"
backup_and_remove "infrastructure/parameters.staging.standardized.json"
backup_and_remove "infrastructure/parameters.staging.temp.json"
backup_and_remove "infrastructure/keyvault-access-policy.json"

# Remove old workflows
echo "⚙️  Cleaning GitHub workflows..."
backup_and_remove ".github/workflows/deploy-infrastructure.yml"
backup_and_remove ".github/workflows/deploy-infrastructure-fixed.yml"
backup_and_remove ".github/workflows/deploy-infrastructure-optimized.yml"
backup_and_remove ".github/workflows/deploy-application.yml"
backup_and_remove ".github/workflows/deploy-application-optimized.yml"
backup_and_remove ".github/workflows/test-and-quality.yml"

# Remove deployment scripts
echo "📜 Cleaning deployment scripts..."
backup_and_remove_dir "infrastructure/scripts"
backup_and_remove_dir "infrastructure/bicep"
backup_and_remove "infrastructure/Deploy-Infrastructure.ps1"
backup_and_remove "infrastructure/deploy.sh"
backup_and_remove "infrastructure/deploy-local.sh"
backup_and_remove "infrastructure/deploy-arm-direct.sh"
backup_and_remove "infrastructure/cleanup-and-redeploy.sh"
backup_and_remove "infrastructure/deploy_azure_sdk.py"
backup_and_remove "infrastructure/requirements.txt"

# Remove documentation files
echo "📚 Cleaning documentation files..."
backup_and_remove "infrastructure/DEPLOYMENT-README.md"
backup_and_remove "infrastructure/DEPLOYMENT_CHECKLIST.md"
backup_and_remove "infrastructure/DEPLOYMENT_SUCCESS.md"
backup_and_remove "infrastructure/MIGRATION-STRATEGY.md"
backup_and_remove "infrastructure/STANDARDIZATION_COMPLETE.md"
backup_and_remove "infrastructure/deployment-recovery-plan.md"
backup_and_remove "infrastructure/deployment-config.yml"

# Remove old deployment files from root
echo "🗂️  Cleaning root deployment files..."
backup_and_remove "docker-compose.production.yml"
backup_and_remove "cleanup-old-deployment-files.sh"

echo "✅ Cleanup complete!"
echo "📦 Backup created at: $BACKUP_DIR"
echo ""
echo "🎯 Active staging deployment files preserved:"
echo "   ✅ infrastructure/simple-deploy.bicep"  
echo "   ✅ .github/workflows/simple-deploy.yml"
echo ""
echo "🔄 To restore files: cp $BACKUP_DIR/* ./"