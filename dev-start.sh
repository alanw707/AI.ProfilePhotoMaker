#!/usr/bin/env bash
set -euo pipefail

# Start only API and UI dev servers. Does NOT touch Docker DB/azurite.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

START_API=true
START_UI=true
API_STARTED=false
UI_STARTED=false
NGROK_PID=""

cleanup() {
  local exit_code=$?
  if [[ $exit_code -ne 0 ]]; then
    echo "❌ dev-start encountered an error. Cleaning up partial state..." >&2
    if [[ -n ${NGROK_PID:-} ]] && kill -0 "$NGROK_PID" >/dev/null 2>&1; then
      kill "$NGROK_PID" 2>/dev/null || true
    fi
    if [[ $UI_STARTED == true ]]; then
      bash "$SCRIPT_DIR/scripts/ui-stop.sh" >/dev/null 2>&1 || true
    fi
    if [[ $API_STARTED == true ]]; then
      bash "$SCRIPT_DIR/scripts/api-stop.sh" >/dev/null 2>&1 || true
    fi
  fi
}
trap cleanup EXIT

for arg in "$@"; do
  case "$arg" in
    --api-only) START_UI=false ;;
    --ui-only) START_API=false ;;
  esac
done

if $START_API; then
  echo "➡️  Starting API (dev)"
  bash "$SCRIPT_DIR/scripts/api-start.sh"
  API_STARTED=true
fi

if $START_UI; then
  echo "➡️  Starting UI (dev)"
  bash "$SCRIPT_DIR/scripts/ui-start.sh"
  UI_STARTED=true
fi

if command -v ngrok >/dev/null 2>&1; then
  echo "➡️  Starting ngrok tunnel"
  ngrok http 5032 --domain clear-anteater-usually.ngrok-free.app >/dev/null 2>&1 &
  NGROK_PID=$!
else
  echo "⚠️  ngrok not found on PATH; skipping tunnel startup"
fi

trap - EXIT

echo "✅ Dev services started (API/UI)."

