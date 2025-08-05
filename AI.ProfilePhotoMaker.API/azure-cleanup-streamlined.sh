#!/bin/bash
# Streamlined Azure Cleanup Master Script
# Smart but simple approach for testing environment

echo "🚀 AI Profile Maker - Streamlined Azure Cleanup"
echo "================================================"
echo ""
echo "This streamlined approach focuses on:"
echo "✅ Quick assessment without over-engineering"
echo "✅ Strategic cleanup preserving Container Registry"
echo "✅ Deployment readiness for v1 foundation"
echo ""
echo "Environment: Testing (no production data preservation)"
echo "Target: Clean v1 deployment foundation"
echo ""

# Check Azure CLI
if ! command -v az &> /dev/null; then
    echo "❌ Azure CLI not found. Please install: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check login
if ! az account show &>/dev/null; then
    echo "🔐 Please login to Azure first:"
    az login
fi

echo "Current Azure subscription:"
az account show --query "{name:name, id:id}" -o table

echo ""
read -p "Continue with cleanup? (yes/no): " PROCEED

if [ "$PROCEED" != "yes" ]; then
    echo "❌ Cleanup cancelled"
    exit 0
fi

echo ""
echo "🎯 Starting 3-Phase Streamlined Cleanup..."
echo ""

# Phase 1: Assessment
echo "⏱️ Phase 1: Smart Assessment (5 minutes)"
if [ -f "cleanup-phase1-assess.sh" ]; then
    chmod +x cleanup-phase1-assess.sh
    ./cleanup-phase1-assess.sh
    
    echo ""
    read -p "Assessment complete. Continue to cleanup? (yes/no): " CONTINUE_PHASE2
    if [ "$CONTINUE_PHASE2" != "yes" ]; then
        echo "⏸️ Stopped after assessment"
        exit 0
    fi
else
    echo "❌ Phase 1 script not found"
    exit 1
fi

echo ""
# Phase 2: Strategic Cleanup
echo "⏱️ Phase 2: Strategic Cleanup (10 minutes)"
if [ -f "cleanup-phase2-strategic.sh" ]; then
    chmod +x cleanup-phase2-strategic.sh
    ./cleanup-phase2-strategic.sh
    
    echo ""
    echo "Phase 2 complete. Proceeding to readiness check..."
    sleep 2
else
    echo "❌ Phase 2 script not found"
    exit 1
fi

echo ""
# Phase 3: Deployment Readiness
echo "⏱️ Phase 3: Deployment Readiness (5 minutes)"
if [ -f "cleanup-phase3-readiness.sh" ]; then
    chmod +x cleanup-phase3-readiness.sh
    ./cleanup-phase3-readiness.sh
else
    echo "❌ Phase 3 script not found"
    exit 1
fi

echo ""
echo "🎉 Streamlined Azure Cleanup Complete!"
echo "======================================"
echo ""
echo "📋 Summary:"
echo "✅ Smart assessment completed"
echo "✅ Strategic cleanup executed"  
echo "✅ Deployment readiness validated"
echo ""
echo "🚀 Your environment is ready for v1 deployment!"
echo ""
echo "Next Steps:"
echo "1. Review .env.deployment for deployment variables"
echo "2. Build and push your container image"
echo "3. Deploy your v1 infrastructure"
echo ""
echo "💡 Tip: Keep these scripts as templates for future production deployments"