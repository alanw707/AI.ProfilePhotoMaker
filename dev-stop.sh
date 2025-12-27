#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NGROK_PID_FILE="${SCRIPT_DIR}/ngrok.pid"
COMPOSE_FILE="${SCRIPT_DIR}/docker-compose.yml"

STOP_API=true
STOP_UI=true
STOP_DOCKER=false
FORCE_NGROK_KILL=false

for arg in "$@"; do
  case "$arg" in
    --api-only) STOP_UI=false ;;
    --ui-only) STOP_API=false ;;
    --docker|--containers) STOP_DOCKER=true ;;
    --force-ngrok) FORCE_NGROK_KILL=true ;;
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

if [[ $STOP_DOCKER == true && -f $COMPOSE_FILE ]] && command -v docker >/dev/null 2>&1; then
  if docker compose ps -q >/dev/null 2>&1; then
    echo "➡️  Stopping docker-compose stack"
    docker compose down
  fi
fi

echo "➡️  Stopping ngrok tunnel"
if [[ -f $NGROK_PID_FILE ]]; then
  ngrok_pid="$(cat "$NGROK_PID_FILE" 2>/dev/null || true)"
  if [[ -n ${ngrok_pid:-} ]] && kill -0 "$ngrok_pid" >/dev/null 2>&1; then
    kill "$ngrok_pid" 2>/dev/null || true
  fi
  rm -f "$NGROK_PID_FILE"
fi
if [[ $FORCE_NGROK_KILL == true ]]; then
  pkill -f ngrok || true
fi

echo "✅ Dev services stopped (API/UI)."
