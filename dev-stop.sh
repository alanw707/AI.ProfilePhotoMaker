#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

STOP_API=true
STOP_UI=true

for arg in "$@"; do
  case "$arg" in
    --api-only) STOP_UI=false ;;
    --ui-only) STOP_API=false ;;
  esac
done

if $STOP_UI; then
  echo "➡️  Stopping UI"
  bash "$SCRIPT_DIR/scripts/ui-stop.sh"
fi

if $STOP_API; then
  echo "➡️  Stopping API"
  bash "$SCRIPT_DIR/scripts/api-stop.sh"
fi

echo "➡️  Stopping ngrok tunnel"
pkill -f ngrok || true

echo "✅ Dev services stopped (API/UI)."

