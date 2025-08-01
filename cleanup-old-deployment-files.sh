#!/bin/bash

echo "🧹 Cleaning up old deployment files..."

# Remove old deployment documentation
rm -f DEPLOYMENT*.md
rm -f AZURE_DEPLOYMENT*.md
rm -f LIVE_DEPLOYMENT_STATUS.md
rm -f GITHUB_SECRETS_READY.md
rm -f AUTOMATED_DEPLOYMENT_GUIDE.md
rm -f OPERATIONAL_RUNBOOK.md
rm -f DEVOPS_ANALYSIS.md

# Remove old shell scripts
rm -f deploy-local-reliable.sh
rm -f validate-deployment.sh
rm -f validate-deployment-comprehensive.sh
rm -f Deploy-Infrastructure.ps1
rm -f get-pip.py

# Remove infrastructure subdirectory old files
rm -rf infrastructure/scripts/
rm -f infrastructure/Deploy-Infrastructure.ps1
rm -f infrastructure/deploy*.sh
rm -f infrastructure/cleanup-and-redeploy.sh
rm -f infrastructure/parameters.*.local.json
rm -f infrastructure/main.test.json
rm -f infrastructure/main.validated.json
rm -f infrastructure/DEPLOYMENT*.md
rm -f infrastructure/deployment-recovery-plan.md

# Remove old workflow files (keep the new simple one)
rm -f .github/workflows/deploy-infrastructure.yml
rm -f .github/workflows/deploy-application.yml

# Remove old bicep files (keep the simple one)
rm -f infrastructure/main.bicep
rm -f infrastructure/container-apps-main.bicep

echo "✅ Cleanup complete!"
echo ""
echo "📁 Remaining deployment files:"
echo "  ✅ infrastructure/simple-deploy.bicep (main infrastructure)"
echo "  ✅ .github/workflows/simple-deploy.yml (CI/CD pipeline)"
echo "  ✅ SIMPLE-DEPLOYMENT-GUIDE.md (setup instructions)"
echo ""
echo "🚀 You're ready to deploy with a clean, simple setup!"