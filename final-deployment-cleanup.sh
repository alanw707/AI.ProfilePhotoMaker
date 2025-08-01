#!/bin/bash

# Final cleanup of remaining deployment artifacts
# Removes old Azure pipelines and local deployment scripts

echo "🧹 Final deployment artifact cleanup..."

BACKUP_DIR=".deployment-backup/$(date +%Y%m%d-%H%M%S)-final"
mkdir -p "$BACKUP_DIR"

echo "📦 Creating final backup at $BACKUP_DIR"

# Remove old Azure DevOps pipelines (we use GitHub Actions now)
if [ -d ".azure" ]; then
    echo "🗑️  Removing Azure DevOps pipelines directory"
    cp -r .azure "$BACKUP_DIR/" 2>/dev/null || true
    rm -rf .azure
fi

# Remove old local deployment scripts
echo "🧹 Removing old local deployment scripts..."

local_scripts=(
    "deploy-local-reliable.sh"
    "validate-deployment.sh" 
    "validate-deployment-comprehensive.sh"
    "Deploy-Infrastructure.ps1"
)

for script in "${local_scripts[@]}"; do
    if [ -f "$script" ]; then
        echo "🗑️  Removing: $script"
        cp "$script" "$BACKUP_DIR/" 2>/dev/null || true
        rm "$script"
    fi
done

echo "✅ Final cleanup complete!"
echo "📦 Backup created at: $BACKUP_DIR"
echo ""
echo "🎯 Clean staging deployment setup:"
echo "   ✅ infrastructure/simple-deploy.bicep (staging infrastructure)"
echo "   ✅ .github/workflows/simple-deploy.yml (GitHub Actions CI/CD)"
echo "   ✅ SIMPLE-DEPLOYMENT-GUIDE.md (deployment instructions)"
echo "   ✅ NEXT-STEPS.md (roadmap)"
echo ""
echo "🗑️  Removed: Azure DevOps pipelines, local deployment scripts"
echo "💡 Project is now focused on single staging deployment approach"