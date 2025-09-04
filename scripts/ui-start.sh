#!/usr/bin/env bash
set -euo pipefail

# Start Angular dev server only (does NOT touch Docker/DB)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
UI_DIR="${REPO_ROOT}/AI.ProfilePhotoMaker.UI"
PID_FILE="${REPO_ROOT}/ui.pid"
LOG_FILE="${REPO_ROOT}/ui.log"

PORT=${PORT:-4200}

cd "$UI_DIR"

# Ensure dependencies are installed once
if [[ ! -d node_modules ]]; then
  echo "Installing UI dependencies (npm ci)..."
  npm ci
fi

echo "Starting Angular dev server on port ${PORT}..."
nohup npm run dev:local >>"$LOG_FILE" 2>&1 &
UI_PID=$!
echo $UI_PID > "$PID_FILE"
echo "UI PID: $UI_PID (logs: $LOG_FILE)"

# Wait for readiness
READY_URL=${READY_URL:-"http://localhost:${PORT}"}
TIMEOUT=${TIMEOUT:-45}
echo "Waiting for UI at $READY_URL (timeout ${TIMEOUT}s)..."
start_ts=$(date +%s)
until curl -fsS "$READY_URL" >/dev/null 2>&1; do
  sleep 1
  now=$(date +%s)
  if (( now - start_ts > TIMEOUT )); then
    echo "UI did not become ready within ${TIMEOUT}s. Showing last 80 UI log lines:" >&2
    tail -n 80 "$LOG_FILE" || true
    exit 1
  fi
  if ! kill -0 "$UI_PID" >/dev/null 2>&1; then
    echo "UI process exited prematurely. Showing last 80 UI log lines:" >&2
    tail -n 80 "$LOG_FILE" || true
    exit 1
  fi
done

echo "UI ready at $READY_URL"

