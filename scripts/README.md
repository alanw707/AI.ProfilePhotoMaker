# Local Build Workflow Scripts

Essential scripts for the AI Profile Photo Maker local build workflow.

## 🚀 Daily Workflow Scripts

### Core Build Process
```bash
# 1. Build images locally (2-3 minutes)
./scripts/build-local.sh

# 2. Push to Azure Container Registry
./scripts/push-to-acr.sh

# 3. Trigger deployment
git push origin main
```

## 🧪 Validation & Testing

### `./test-local-workflow.sh`
**Environment validation and troubleshooting**

```bash
./scripts/test-local-workflow.sh
```

Tests: Docker setup, Azure CLI, local builds, ACR authentication, templates

**Risk Level:** 🟢 Zero - Local testing only

## 🛠️ DevOps Tools

### `./trigger-workflow.sh`
**Manual workflow control**

```bash
# Trigger deployment
./scripts/trigger-workflow.sh simple-deploy.yml main trigger

# Monitor workflow
./scripts/trigger-workflow.sh simple-deploy.yml main monitor

# Check status
./scripts/trigger-workflow.sh simple-deploy.yml main status
```

### `./rollback-deployment.sh`
**Emergency rollback capabilities**

```bash
./scripts/rollback-deployment.sh
```

## 🔧 Troubleshooting

**Local Build Issues:**
```bash
docker info
docker system prune -f
./scripts/build-local.sh
```

**ACR Authentication:**
```bash
az login
az acr list --query "[].{Name:name, ResourceGroup:resourceGroup}"
```

**Workflow Issues:**
```bash
./scripts/trigger-workflow.sh simple-deploy.yml main status
gh run list --limit 5
```

## ⚡ Performance

- **Build Time:** 2-3 minutes (vs 5-8 minutes in CI)
- **Feedback:** Immediate local validation
- **Debugging:** Full local control
- **Iteration:** Instant local rebuilds

## Quick Start

New to the workflow? Start here:

```bash
# Validate your environment
./scripts/test-local-workflow.sh

# If validation passes, deploy:
./scripts/build-local.sh && ./scripts/push-to-acr.sh && git push origin main
```