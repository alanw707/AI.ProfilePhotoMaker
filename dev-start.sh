#!/usr/bin/env bash
set -euo pipefail

# Start only API and UI dev servers. Does NOT touch Docker DB/azurite.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

START_API=true
START_UI=true

for arg in "$@"; do
  case "$arg" in
    --api-only) START_UI=false ;;
    --ui-only) START_API=false ;;
  esac
done

if $START_API; then
  echo "➡️  Starting API (dev)"
  bash "$SCRIPT_DIR/scripts/api-start.sh"
fi

if $START_UI; then
  echo "➡️  Starting UI (dev)"
  bash "$SCRIPT_DIR/scripts/ui-start.sh"
fi

echo "➡️  Starting ngrok tunnel"
ngrok http 5032 --domain clear-anteater-usually.ngrok-free.app &

echo "✅ Dev services started (API/UI)."

