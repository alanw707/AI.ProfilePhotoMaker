#!/bin/bash

# Update Azure Container Apps API health probes to match application endpoints

set -euo pipefail

ENVIRONMENT="${ENVIRONMENT:-v1}"
RESOURCE_GROUP="${RESOURCE_GROUP:-aiprofilemaker-${ENVIRONMENT}}"
APP_NAME="${APP_NAME:-aipm-api-${ENVIRONMENT}}"

echo "Updating health probes for $APP_NAME in $RESOURCE_GROUP"

if ! command -v az >/dev/null 2>&1; then
  echo "Azure CLI not found. Install: https://aka.ms/azcli"
  exit 1
fi

if ! az account show >/dev/null 2>&1; then
  echo "Please login first: az login"
  exit 1
fi

# Fetch current template
TMP_JSON=$(mktemp)
az containerapp show \
  --name "$APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --output json > "$TMP_JSON"

# Patch probes to use /api/health/live and /api/health/ready
jq '.properties.template.containers[0].probes = [
      {
        type: "Liveness",
        httpGet: { path: "/api/health/live", port: 8080, scheme: "HTTP" },
        initialDelaySeconds: 15, periodSeconds: 10, timeoutSeconds: 10,
        failureThreshold: 3, successThreshold: 1
      },
      {
        type: "Readiness",
        httpGet: { path: "/api/health/ready", port: 8080, scheme: "HTTP" },
        initialDelaySeconds: 10, periodSeconds: 10, timeoutSeconds: 5,
        failureThreshold: 5, successThreshold: 1
      }
    ]' "$TMP_JSON" > "$TMP_JSON.patched"

echo "Applying probe update (this creates a new revision)..."
az containerapp update \
  --name "$APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --set-template-file "$TMP_JSON.patched" \
  --only-show-errors --output none

echo "Probes updated. Monitoring revision for readiness..."
ENVIRONMENT="$ENVIRONMENT" "$PWD/scripts/verify-container-revision.sh" --max-wait 600 || true

echo "Done."

