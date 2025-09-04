#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PID_FILE="${REPO_ROOT}/ui.pid"

# Stop by PID if present
if [[ -f "$PID_FILE" ]]; then
  PID=$(cat "$PID_FILE" || true)
  if [[ -n "${PID}" ]] && kill -0 "$PID" >/dev/null 2>&1; then
    echo "Stopping UI PID $PID"
    kill "$PID" || true
    sleep 2
    if kill -0 "$PID" >/dev/null 2>&1; then
      echo "Force killing UI PID $PID"
      kill -9 "$PID" || true
    fi
  else
    echo "UI PID file present but process not running."
  fi
  rm -f "$PID_FILE"
fi

# Fallback: kill anything on the dev port 4200
PORT=${PORT:-4200}
if command -v lsof >/dev/null 2>&1; then
  PIDS=$(lsof -ti :"$PORT" || true)
  if [[ -n "${PIDS}" ]]; then
    echo "Killing process(es) on port $PORT: $PIDS"
    kill -9 ${PIDS} || true
  fi
elif command -v fuser >/dev/null 2>&1; then
  fuser -k "${PORT}/tcp" >/dev/null 2>&1 || true
fi

echo "UI processes stopped (if any)."

