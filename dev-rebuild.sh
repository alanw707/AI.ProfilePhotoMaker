#!/usr/bin/env bash
set -euo pipefail

# Rebuild and restart dev servers
# Use --docker to rebuild containers instead of local dev servers

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR" && pwd)"

REBUILD_API=true
REBUILD_UI=true
REBUILD_DOCKER=false
NO_CACHE=""

DEFAULT_NGROK_DOMAIN="clear-anteater-usually.ngrok-free.app"
NGROK_PID_FILE="${SCRIPT_DIR}/ngrok.pid"
NGROK_LOG_FILE="${SCRIPT_DIR}/ngrok.log"
API_PORT="${API_HTTP_PORT:-5032}"

for arg in "$@"; do
  case "$arg" in
    --api-only) REBUILD_UI=false ;;
    --ui-only) REBUILD_API=false ;;
    --docker|--containers) REBUILD_DOCKER=true ;;
    --no-cache) NO_CACHE="--no-cache" ;;
  esac
done

get_ngrok_public_url() {
  local port="${1:-}"
  python3 - "$port" <<'PY' 2>/dev/null
import json, sys, urllib.request
port = sys.argv[1] if len(sys.argv) > 1 else ""
try:
    with urllib.request.urlopen("http://127.0.0.1:4040/api/tunnels") as resp:
        payload = json.loads(resp.read().decode("utf-8"))
except Exception:
    sys.exit(1)
for tunnel in payload.get("tunnels") or []:
    url = tunnel.get("public_url") or ""
    if not url.startswith("https://"):
        continue
    if port:
        addr = str(tunnel.get("config", {}).get("addr", ""))
        if f":{port}" not in addr:
            continue
    print(url)
    sys.exit(0)
sys.exit(1)
PY
}

start_ngrok() {
  local target_port="${1:-5032}"
  local domain="${NGROK_DOMAIN:-$DEFAULT_NGROK_DOMAIN}"

  # Check if ngrok is already running with correct domain
  if curl -fsS http://127.0.0.1:4040/api/tunnels >/dev/null 2>&1; then
    local current_url
    current_url="$(get_ngrok_public_url "$target_port" 2>/dev/null || true)"
    if [[ -n $current_url ]] && [[ $current_url == "https://${domain}" ]]; then
      echo "ℹ️  ngrok already running: ${current_url}"
      return 0
    fi
  fi

  # Kill any existing ngrok process
  if [[ -f $NGROK_PID_FILE ]]; then
    local pid
    pid="$(cat "$NGROK_PID_FILE" 2>/dev/null || true)"
    if [[ -n $pid ]] && kill -0 "$pid" >/dev/null 2>&1; then
      kill "$pid" 2>/dev/null || true
    fi
    rm -f "$NGROK_PID_FILE"
  fi

  echo "➡️  Starting ngrok tunnel for port ${target_port} with domain ${domain}"
  nohup ngrok http "${target_port}" --domain "${domain}" --log=stdout >"$NGROK_LOG_FILE" 2>&1 &
  local ngrok_pid=$!
  echo "$ngrok_pid" > "$NGROK_PID_FILE"
  disown "$ngrok_pid" 2>/dev/null || true

  # Wait for ngrok to be ready
  local attempts=60
  for _ in $(seq 1 "$attempts"); do
    if curl -fsS http://127.0.0.1:4040/api/tunnels >/dev/null 2>&1; then
      local url
      url="$(get_ngrok_public_url "$target_port" 2>/dev/null || true)"
      if [[ -n $url ]]; then
        echo "✅ ngrok running: ${url}"
        return 0
      fi
    fi
    sleep 0.25
  done

  echo "⚠️  ngrok did not start in time (see ${NGROK_LOG_FILE})"
  return 1
}

wait_for_api() {
  local port="${1:-5032}"
  local timeout_seconds="${2:-90}"
  local start
  start="$(date +%s)"

  echo "⏳ Waiting for API to be ready on port ${port}..."
  while true; do
    if curl -fsS "http://127.0.0.1:${port}/api/health" >/dev/null 2>&1; then
      echo "✅ API is ready"
      return 0
    fi
    if (( $(date +%s) - start >= timeout_seconds )); then
      echo "⚠️  API did not become ready in ${timeout_seconds}s"
      return 1
    fi
    sleep 1
  done
}

if [[ $REBUILD_DOCKER == true ]]; then
  echo "🐳 Rebuilding Docker containers"

  services=()
  if $REBUILD_API; then services+=(api); fi
  if $REBUILD_UI; then services+=(frontend); fi

  if [[ ${#services[@]} -eq 0 ]]; then
    echo "❌ No services to rebuild"
    exit 1
  fi

  # Build and restart containers
  echo "🏗️  Building: ${services[*]}"
  docker compose build $NO_CACHE "${services[@]}"

  echo "🔁 Restarting: ${services[*]}"
  docker compose up -d --force-recreate "${services[@]}"

  # Wait for API and start ngrok
  if $REBUILD_API; then
    if wait_for_api "$API_PORT" 90; then
      start_ngrok "$API_PORT" || true
    fi
  fi

  echo "✅ Docker rebuild complete."
  exit 0
fi

# Local dev mode (original behavior)
if $REBUILD_API; then
  echo "🏗️  Rebuilding API"
  pushd "$REPO_ROOT/AI.ProfilePhotoMaker.API" >/dev/null
  dotnet build -clp:Summary --nologo
  popd >/dev/null
  echo "🔁 Restarting API"
  bash "$REPO_ROOT/scripts/api-restart.sh"
fi

if $REBUILD_UI; then
  echo "🏗️  Rebuilding UI"
  pushd "$REPO_ROOT/AI.ProfilePhotoMaker.UI" >/dev/null
  npm run build:dev --silent || true
  popd >/dev/null
  echo "🔁 Restarting UI"
  bash "$REPO_ROOT/scripts/ui-stop.sh" || true
  bash "$REPO_ROOT/scripts/ui-start.sh"
fi

echo "✅ Rebuild complete. API/UI running."

