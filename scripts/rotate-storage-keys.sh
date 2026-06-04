#!/usr/bin/env bash
set -euo pipefail

RESOURCE_GROUP="${RESOURCE_GROUP:-aiprofilemaker-v1}"
STORAGE_ACCOUNT="${STORAGE_ACCOUNT:-aipmstv16j74jubocuukg}"
CONTAINER_APP="${CONTAINER_APP:-aipm-api-v1}"
CONTAINER_NAME="${AZURE_STORAGE_CONTAINER_NAME:-profile-images}"
REPO="${GITHUB_REPOSITORY:-alanw707/AI.ProfilePhotoMaker}"
HEALTH_URL="${HEALTH_URL:-https://api.aiprofilephotomaker.com/api/health}"

require() {
  command -v "$1" >/dev/null 2>&1 || { echo "missing command: $1" >&2; exit 1; }
}

mask() {
  local value="$1"
  if [[ -n "$value" && "${GITHUB_ACTIONS:-}" == "true" ]]; then
    echo "::add-mask::$value" 2>/dev/null || true
  fi
}

conn_string() {
  local key_value="$1"
  printf 'DefaultEndpointsProtocol=https;AccountName=%s;AccountKey=%s;EndpointSuffix=core.windows.net' "$STORAGE_ACCOUNT" "$key_value"
}

get_key() {
  local key_name="$1"
  az storage account keys list \
    --resource-group "$RESOURCE_GROUP" \
    --account-name "$STORAGE_ACCOUNT" \
    --query "[?keyName=='$key_name'].value | [0]" \
    --output tsv
}

set_container_app_storage_env() {
  local connection_string="$1"
  echo "Updating Container App storage env vars..."
  az containerapp update \
    --name "$CONTAINER_APP" \
    --resource-group "$RESOURCE_GROUP" \
    --set-env-vars \
      "AZURE_STORAGE_CONNECTION_STRING=$connection_string" \
      "AzureStorage__ConnectionString=$connection_string" \
      "ConnectionStrings__AzureStorage=$connection_string" \
      "AZURE_STORAGE_CONTAINER_NAME=$CONTAINER_NAME" \
      "AzureStorage__ContainerName=$CONTAINER_NAME" \
    --query '{revision:properties.latestRevisionName,state:properties.provisioningState}' \
    --output json
}

set_github_storage_secrets() {
  local connection_string="$1"
  echo "Updating GitHub Actions storage secrets in $REPO..."
  gh secret set AZURE_STORAGE_CONNECTION_STRING --repo "$REPO" --body "$connection_string" >/dev/null
  gh secret set AZURE_STORAGE_CONTAINER_NAME --repo "$REPO" --body "$CONTAINER_NAME" >/dev/null
}

wait_for_health() {
  echo "Waiting for API health..."
  for attempt in {1..30}; do
    status=$(node -e "fetch(process.argv[1]).then(r=>{console.log(r.status); process.exit(r.ok?0:1)}).catch(()=>{console.log('ERR'); process.exit(1)})" "$HEALTH_URL" 2>/dev/null || true)
    if [[ "$status" == "200" ]]; then
      echo "Health OK"
      return 0
    fi
    echo "Health not ready (attempt $attempt/30, status=$status)"
    sleep 10
  done
  echo "Health check failed" >&2
  return 1
}

require az
require gh
require node

if [[ "${CONFIRM_ROTATE_STORAGE_KEYS:-}" != "yes" ]]; then
  cat >&2 <<EOF
Refusing to rotate without explicit confirmation.
Run:
  CONFIRM_ROTATE_STORAGE_KEYS=yes $0

This will:
  1. Read current Container App storage connection string.
  2. Renew standby storage key.
  3. Switch Container App + GitHub Actions secrets to standby key.
  4. Renew original key.
  5. Switch Container App + GitHub Actions secrets back to original key.
  6. Renew standby key again.
EOF
  exit 2
fi

current_conn=$(az containerapp show \
  --name "$CONTAINER_APP" \
  --resource-group "$RESOURCE_GROUP" \
  --query "properties.template.containers[0].env[?name=='AZURE_STORAGE_CONNECTION_STRING'].value | [0]" \
  --output tsv)
mask "$current_conn"

key1_before=$(get_key key1)
key2_before=$(get_key key2)
mask "$key1_before"
mask "$key2_before"

if [[ "$current_conn" == *"AccountKey=$key1_before"* ]]; then
  active="key1"
  standby="key2"
elif [[ "$current_conn" == *"AccountKey=$key2_before"* ]]; then
  active="key2"
  standby="key1"
else
  echo "Could not determine active storage key from Container App env." >&2
  exit 1
fi

echo "Active key: $active"
echo "Standby key: $standby"

echo "Renewing standby key ($standby)..."
az storage account keys renew --resource-group "$RESOURCE_GROUP" --account-name "$STORAGE_ACCOUNT" --key "$standby" --output none
standby_value=$(get_key "$standby")
mask "$standby_value"
standby_conn=$(conn_string "$standby_value")
mask "$standby_conn"

set_container_app_storage_env "$standby_conn"
set_github_storage_secrets "$standby_conn"
wait_for_health

echo "Renewing original active key ($active)..."
az storage account keys renew --resource-group "$RESOURCE_GROUP" --account-name "$STORAGE_ACCOUNT" --key "$active" --output none
active_value=$(get_key "$active")
mask "$active_value"
active_conn=$(conn_string "$active_value")
mask "$active_conn"

set_container_app_storage_env "$active_conn"
set_github_storage_secrets "$active_conn"
wait_for_health

echo "Renewing standby key again ($standby), leaving app on $active..."
az storage account keys renew --resource-group "$RESOURCE_GROUP" --account-name "$STORAGE_ACCOUNT" --key "$standby" --output none

final_revision=$(az containerapp show --name "$CONTAINER_APP" --resource-group "$RESOURCE_GROUP" --query properties.latestRevisionName --output tsv)
echo "Storage key rotation complete. App active key: $active. Revision: $final_revision. GitHub secrets updated."
