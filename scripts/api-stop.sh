#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PID_FILE="${REPO_ROOT}/api.pid"

# Try to stop by PID first (gracefully), then fall back to ports
if [[ -f "$PID_FILE" ]]; then
  PID=$(cat "$PID_FILE" || true)
  if [[ -n "${PID}" ]] && kill -0 "$PID" >/dev/null 2>&1; then
    echo "Stopping API PID $PID"
    kill "$PID" || true
    # Give it a moment to exit gracefully
    sleep 2
    if kill -0 "$PID" >/dev/null 2>&1; then
      echo "Force killing API PID $PID"
      kill -9 "$PID" || true
    fi
  else
    echo "PID file present but process not running."
  fi
  rm -f "$PID_FILE"
fi

# Stop any process listening on the API dev ports (HTTP 5032, HTTPS 7173)
ports=(5032 7173)

for p in "${ports[@]}"; do
  pids=""
  if command -v lsof >/dev/null 2>&1; then
    pids=$(lsof -ti :"$p" || true)
  elif command -v fuser >/dev/null 2>&1; then
    pids=$(fuser -n tcp "$p" 2>/dev/null || true)
  fi
  if [[ -n "${pids}" ]]; then
    echo "Killing process(es) on port $p: $pids"
    kill -9 ${pids} || true
  else
    echo "No process found on port $p"
  fi
done

echo "API processes stopped (if any)."
