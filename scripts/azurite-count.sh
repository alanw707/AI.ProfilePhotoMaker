#!/usr/bin/env bash

# Count blobs in local Azurite containers using Azure CLI
# Requires: Azure CLI (az)

set -euo pipefail

CONN="${AZURE_STORAGE_CONNECTION_STRING:-UseDevelopmentStorage=true}"

if ! command -v az >/dev/null 2>&1; then
  echo "❌ Azure CLI (az) not found. Install it, then re-run."
  echo "   https://learn.microsoft.com/cli/azure/install-azure-cli"
  exit 1
fi

echo "🔗 Using connection string: ${CONN}"

containers=(profile-images style-previews)

for c in "${containers[@]}"; do
  echo "\n=== Container: $c ==="
  if ! az storage container show --name "$c" --connection-string "$CONN" >/dev/null 2>&1; then
    echo "(container not found)"
    continue
  fi

  total=$(az storage blob list --container-name "$c" --connection-string "$CONN" --query "length(@)" -o tsv)
  echo "Total blobs: $total"

  if [ "$c" = "profile-images" ]; then
    for prefix in uploads/ generated/ enhanced/ training-zips/; do
      count=$(az storage blob list --container-name "$c" --connection-string "$CONN" --prefix "$prefix" --query "length(@)" -o tsv)
      printf "  %-15s %s\n" "$prefix" "$count"
    done
  fi
done

