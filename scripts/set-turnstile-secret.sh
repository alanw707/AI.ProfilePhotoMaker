#!/bin/bash

# Set Cloudflare Turnstile secret key on the Azure Container App API.
#
# Usage:
#   TURNSTILE_SECRET_KEY="..." ./scripts/set-turnstile-secret.sh
#
# Optional overrides:
#   RESOURCE_GROUP="aiprofilemaker-v1" API_APP_NAME="aipm-api-v1" TURNSTILE_SECRET_KEY="..." ./scripts/set-turnstile-secret.sh

set -euo pipefail

RESOURCE_GROUP="${RESOURCE_GROUP:-aiprofilemaker-v1}"
API_APP_NAME="${API_APP_NAME:-aipm-api-v1}"

if [[ -z "${TURNSTILE_SECRET_KEY:-}" ]]; then
  echo "Missing TURNSTILE_SECRET_KEY env var." >&2
  exit 1
fi

if ! command -v az >/dev/null 2>&1; then
  echo "Azure CLI not found. Install it first: https://learn.microsoft.com/cli/azure/install-azure-cli" >&2
  exit 1
fi

if ! az account show >/dev/null 2>&1; then
  echo "Not logged into Azure CLI. Run: az login" >&2
  exit 1
fi

echo "Updating Turnstile secret key for API container app '${API_APP_NAME}' in resource group '${RESOURCE_GROUP}'..."

# Do not echo the secret value. Prefer Container App secrets so the value is not stored as a plain env var.
az containerapp secret set \
  --name "${API_APP_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --secrets "turnstile-secret-key=${TURNSTILE_SECRET_KEY}" \
  >/dev/null

# Force a new revision so the app reads updated secrets at startup.
REV_TS=$(date +%s)
az containerapp update \
  --name "${API_APP_NAME}" \
  --resource-group "${RESOURCE_GROUP}" \
  --set-env-vars REVISION_ROLL_TIMESTAMP=$REV_TS \
  >/dev/null

echo "Done. A new revision should be rolling out now."
