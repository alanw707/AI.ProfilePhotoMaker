#!/bin/bash

# Clean up obsolete deployment documentation files
# Keeps only the current staging deployment guides

echo "🧹 Cleaning up deployment documentation..."

# Create backup directory for docs
BACKUP_DIR=".deployment-backup/$(date +%Y%m%d-%H%M%S)-docs"
mkdir -p "$BACKUP_DIR"

echo "📦 Creating documentation backup at $BACKUP_DIR"

# Function to backup and remove
backup_and_remove_doc() {
    local file="$1"
    if [ -f "$file" ]; then
        echo "🗑️  Removing: $file"
        cp "$file" "$BACKUP_DIR/$(basename "$file")" 2>/dev/null || true
        rm "$file"
    fi
}

# Files to KEEP (current staging deployment guides):
# - SIMPLE-DEPLOYMENT-GUIDE.md (current deployment instructions)
# - NEXT-STEPS.md (roadmap and next actions)

echo "📚 Removing obsolete deployment documentation..."

# Remove all the old/duplicate deployment docs
backup_and_remove_doc "FINAL_DEPLOYMENT_INSTRUCTIONS.md"
backup_and_remove_doc "DEPLOYMENT_CHECKLIST.md"
backup_and_remove_doc "AZURE_DEPLOYMENT_BACKLOG.md"
backup_and_remove_doc "docs/AZURE_DEPLOYMENT_IMPLEMENTATION.md"
backup_and_remove_doc "docs/AZURE_DEPLOYMENT_GUIDE.md"
backup_and_remove_doc "AZURE_DEPLOYMENT_STATUS.md"
backup_and_remove_doc "DEPLOYMENT_ORCHESTRATION_GUIDE.md"
backup_and_remove_doc "DEPLOYMENT_READY_STATUS.md"
backup_and_remove_doc "DEPLOYMENT_EXECUTION_COMPLETE.md"
backup_and_remove_doc "AZURE_DEPLOYMENT_EXECUTION_GUIDE.md"
backup_and_remove_doc "AUTOMATED_DEPLOYMENT_GUIDE.md"
backup_and_remove_doc "DEPLOYMENT_STATUS_FINAL.md"
backup_and_remove_doc "LIVE_DEPLOYMENT_STATUS.md"
backup_and_remove_doc "DEPLOYMENT-GUIDE.md"
backup_and_remove_doc "DEPLOYMENT_FIXES_APPLIED.md"
backup_and_remove_doc "DEPLOYMENT_OPTIMIZATION_REPORT.md"
backup_and_remove_doc "DEPLOYMENT_EXECUTION_PLAN.md"
backup_and_remove_doc "EXECUTE_DEPLOYMENT_NOW.md"

# Check if docs directory is empty and remove if so
if [ -d "docs" ] && [ -z "$(ls -A docs)" ]; then
    echo "🗑️  Removing empty docs directory"
    rmdir docs
fi

echo "✅ Documentation cleanup complete!"
echo "📦 Backup created at: $BACKUP_DIR"
echo ""
echo "🎯 Active deployment documentation preserved:"
echo "   ✅ SIMPLE-DEPLOYMENT-GUIDE.md (current deployment instructions)"
echo "   ✅ NEXT-STEPS.md (roadmap and next actions)"
echo ""
echo "🔄 To restore any file: cp $BACKUP_DIR/FILENAME.md ./"