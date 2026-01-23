#!/usr/bin/env bash
set -euo pipefail

# Start the API in Development environment (default)
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
API_DIR="${REPO_ROOT}/AI.ProfilePhotoMaker.API"
PID_FILE="${REPO_ROOT}/api.pid"
LOG_FILE="${REPO_ROOT}/api.log"

if [[ -z ${DEV_ENV_FILE:-} ]]; then
  if [[ -f "${REPO_ROOT}/.env" ]]; then
    export DEV_ENV_FILE="${REPO_ROOT}/.env"
  elif [[ -f "${REPO_ROOT}/local.env" ]]; then
    export DEV_ENV_FILE="${REPO_ROOT}/local.env"
  fi
fi

if [[ -n ${DEV_ENV_FILE:-} ]]; then
  echo "ℹ️  Using env file: ${DEV_ENV_FILE}" | tee -a "$LOG_FILE"
fi

cd "$API_DIR"
echo "Starting API from: $API_DIR (ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT)"

echo "Building..." | tee -a "$LOG_FILE"
dotnet build -clp:Summary --nologo >>"$LOG_FILE" 2>&1

# Run the API in background and capture PID
echo "Launching Kestrel..." | tee -a "$LOG_FILE"
nohup dotnet run --launch-profile https >>"$LOG_FILE" 2>&1 &
API_PID=$!
echo $API_PID > "$PID_FILE"
echo "API PID: $API_PID (logs: $LOG_FILE)"

# Wait for readiness
HEALTH_URL=${HEALTH_URL:-"http://localhost:5032/api/health/live"}
TIMEOUT=${TIMEOUT:-30}
echo "Waiting for readiness on $HEALTH_URL (timeout ${TIMEOUT}s)..."

start_ts=$(date +%s)
until curl -fsS "$HEALTH_URL" >/dev/null 2>&1; do
  sleep 1
  now=$(date +%s)
  if (( now - start_ts > TIMEOUT )); then
    echo "API did not become ready within ${TIMEOUT}s. Showing last 80 log lines:" >&2
    tail -n 80 "$LOG_FILE" || true
    exit 1
  fi
  # Check if process died
  if ! kill -0 "$API_PID" >/dev/null 2>&1; then
    echo "API process exited prematurely. Showing last 80 log lines:" >&2
    tail -n 80 "$LOG_FILE" || true
    exit 1
  fi
done

echo "API ready at $HEALTH_URL"
