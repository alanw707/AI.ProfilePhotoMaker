#!/usr/bin/env bash
set -euo pipefail

# Starts local dev servers by default.
# Use `--docker` to start the full docker-compose stack with secrets injected from dotnet user-secrets.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

START_API=true
START_UI=true
API_STARTED=false
UI_STARTED=false
NGROK_PID=""
START_DOCKER=false
NGROK_PID_FILE="${SCRIPT_DIR}/ngrok.pid"
NGROK_LOG_FILE="${SCRIPT_DIR}/ngrok.log"
NGROK_STARTED_BY_SCRIPT=false
NGROK_START_TIMEOUT_SECONDS="${NGROK_START_TIMEOUT_SECONDS:-15}"
API_PORT="${API_HTTP_PORT:-5032}"

API_PROJECT="${SCRIPT_DIR}/AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj"
DOCKER_OVERRIDE_FILE="${SCRIPT_DIR}/docker-compose.override.yml"
DEFAULT_NGROK_DOMAIN="clear-anteater-usually.ngrok-free.app"
DEV_ENV_FILE_PATH="${DEV_ENV_FILE:-}"

get_user_secret() {
  local key="$1"
  local secrets="$2"
  printf '%s\n' "$secrets" | awk -F ' = ' -v search="$key" '$1==search {print substr($0, index($0, " = ")+3); exit}'
}

sanitize_secret_value() {
  # Remove Windows CRLF and surrounding whitespace which commonly break container env parsing.
  printf '%s' "${1:-}" | tr -d '\r' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//'
}

load_user_secrets_env() {
  if ! command -v dotnet >/dev/null 2>&1; then
    echo "⚠️  dotnet CLI not available; skipping user-secrets export"
    return
  fi

  if [[ ! -f $API_PROJECT ]]; then
    echo "⚠️  API project file not found at $API_PROJECT; skipping user-secrets export"
    return
  fi

  local secrets
  if ! secrets=$(dotnet user-secrets list --project "$API_PROJECT" 2>/dev/null); then
    echo "⚠️  Unable to read dotnet user-secrets; skipping user-secrets export"
    return
  fi

  if [[ -z ${REPLICATE_API_TOKEN:-} ]]; then
    local value
    value=$(get_user_secret 'Replicate:ApiToken' "$secrets")
    value=$(sanitize_secret_value "$value")
    if [[ -n $value ]]; then
      export REPLICATE_API_TOKEN="$value"
      echo "ℹ️  Loaded Replicate:ApiToken from user-secrets"
    else
      echo "⚠️  Replicate:ApiToken missing from user-secrets"
    fi
  fi

  if [[ -z ${REPLICATE_WEBHOOK_SECRET:-} ]]; then
    local value
    value=$(get_user_secret 'Replicate:WebhookSecret' "$secrets")
    value=$(sanitize_secret_value "$value")
    if [[ -n $value ]]; then
      export REPLICATE_WEBHOOK_SECRET="$value"
      echo "ℹ️  Loaded Replicate:WebhookSecret from user-secrets"
    else
      echo "⚠️  Replicate:WebhookSecret missing from user-secrets"
    fi
  fi

  if [[ -z ${GOOGLE_CLIENT_ID:-} ]]; then
    local value
    value=$(get_user_secret 'Authentication:Google:ClientId' "$secrets")
    value=$(sanitize_secret_value "$value")
    if [[ -n $value ]]; then
      export GOOGLE_CLIENT_ID="$value"
      echo "ℹ️  Loaded Authentication:Google:ClientId from user-secrets"
    fi
  fi

  if [[ -z ${GOOGLE_CLIENT_SECRET:-} ]]; then
    local value
    value=$(get_user_secret 'Authentication:Google:ClientSecret' "$secrets")
    value=$(sanitize_secret_value "$value")
    if [[ -n $value ]]; then
      export GOOGLE_CLIENT_SECRET="$value"
      echo "ℹ️  Loaded Authentication:Google:ClientSecret from user-secrets"
    fi
  fi
}

yaml_quote() {
  # YAML single-quoted scalar. Escapes single quotes by doubling them.
  # https://yaml.org/spec/1.2.2/#56-escaped-characters
  local value="${1:-}"
  value=${value//\'/\'\'}
  printf "'%s'" "$value"
}

is_ngrok_url() {
  local value="${1:-}"
  [[ -n $value ]] || return 1
  [[ $value == *"ngrok-free.app"* || $value == *"ngrok.app"* || $value == *"ngrok.io"* || $value == *"ngrok.dev"* ]]
}

should_start_ngrok() {
  if [[ -n ${NGROK_DOMAIN:-} ]]; then
    return 0
  fi
  if is_ngrok_url "${EXTERNAL_API_BASE_URL:-}" || is_ngrok_url "${OAUTH_BASE_URL:-}"; then
    return 0
  fi
  return 1
}

write_docker_override() {
  # docker compose automatically reads docker-compose.override.yml if present.
  # We generate it locally (gitignored) so manual `docker compose up` won't lose required secrets.
  local tmp_file
  tmp_file="$(mktemp)"

  {
    printf "version: '3.8'\n\n"
    printf "services:\n"
    printf "  api:\n"
    printf "    environment:\n"

    if [[ -n ${EXTERNAL_API_BASE_URL:-} ]]; then
      printf "      ExternalApiBaseUrl: %s\n" "$(yaml_quote "$EXTERNAL_API_BASE_URL")"
      printf "      EXTERNAL_API_BASE_URL: %s\n" "$(yaml_quote "$EXTERNAL_API_BASE_URL")"
      printf "      Webhooks__NgrokTunnelUrl: %s\n" "$(yaml_quote "$EXTERNAL_API_BASE_URL")"
    fi

    if [[ -n ${OAUTH_BASE_URL:-} ]]; then
      printf "      OAUTH_BASE_URL: %s\n" "$(yaml_quote "$OAUTH_BASE_URL")"
    fi

    if [[ -n ${APP_BASE_URL:-} ]]; then
      printf "      APP_BASE_URL: %s\n" "$(yaml_quote "$APP_BASE_URL")"
    fi

    if [[ -n ${REPLICATE_API_TOKEN:-} ]]; then
      printf "      REPLICATE_API_TOKEN: %s\n" "$(yaml_quote "$REPLICATE_API_TOKEN")"
      printf "      Replicate__ApiToken: %s\n" "$(yaml_quote "$REPLICATE_API_TOKEN")"
    fi

    if [[ -n ${REPLICATE_WEBHOOK_SECRET:-} ]]; then
      printf "      REPLICATE_WEBHOOK_SECRET: %s\n" "$(yaml_quote "$REPLICATE_WEBHOOK_SECRET")"
      printf "      Replicate__WebhookSecret: %s\n" "$(yaml_quote "$REPLICATE_WEBHOOK_SECRET")"
    fi

    if [[ -n ${GOOGLE_CLIENT_ID:-} ]]; then
      printf "      GOOGLE_CLIENT_ID: %s\n" "$(yaml_quote "$GOOGLE_CLIENT_ID")"
    fi

    if [[ -n ${GOOGLE_CLIENT_SECRET:-} ]]; then
      printf "      GOOGLE_CLIENT_SECRET: %s\n" "$(yaml_quote "$GOOGLE_CLIENT_SECRET")"
    fi

    if [[ -n ${STRIPE_SECRET_KEY:-} ]]; then
      printf "      STRIPE_SECRET_KEY: %s\n" "$(yaml_quote "$STRIPE_SECRET_KEY")"
    fi

    if [[ -n ${STRIPE_PUBLISHABLE_KEY:-} ]]; then
      printf "      STRIPE_PUBLISHABLE_KEY: %s\n" "$(yaml_quote "$STRIPE_PUBLISHABLE_KEY")"
    fi

    if [[ -n ${STRIPE_WEBHOOK_SECRET:-} ]]; then
      printf "      STRIPE_WEBHOOK_SECRET: %s\n" "$(yaml_quote "$STRIPE_WEBHOOK_SECRET")"
    fi

    printf "\n"
    printf "# NOTE: This file is generated by dev-start.sh and is gitignored.\n"
    printf "# It may contain secrets. Do not commit it.\n"
  } >"$tmp_file"

  mv "$tmp_file" "$DOCKER_OVERRIDE_FILE"
  echo "ℹ️  Wrote $DOCKER_OVERRIDE_FILE (gitignored) for docker compose secrets."
}

load_stripe_env() {
  if [[ -n ${STRIPE_SECRET_KEY:-} && -n ${STRIPE_PUBLISHABLE_KEY:-} && -n ${STRIPE_WEBHOOK_SECRET:-} ]]; then
    return
  fi

  if ! command -v dotnet >/dev/null 2>&1; then
    echo "⚠️  dotnet CLI not available; skipping Stripe secret export"
    return
  fi

  if [[ ! -f $API_PROJECT ]]; then
    echo "⚠️  API project file not found at $API_PROJECT; skipping Stripe secret export"
    return
  fi

  local secrets
  if ! secrets=$(dotnet user-secrets list --project "$API_PROJECT" 2>/dev/null); then
    echo "⚠️  Unable to read dotnet user-secrets; skipping Stripe secret export"
    return
  fi

  if [[ -z ${STRIPE_SECRET_KEY:-} ]]; then
    local value
    value=$(printf '%s\n' "$secrets" | awk -F ' = ' -v search='Stripe:SecretKey' '$1==search {print substr($0, index($0, " = ")+3); exit}')
    if [[ -n $value ]]; then
      export STRIPE_SECRET_KEY="$value"
      echo "ℹ️  Loaded Stripe:SecretKey from user-secrets"
    else
      echo "⚠️  Stripe:SecretKey missing from user-secrets"
    fi
  fi

  if [[ -z ${STRIPE_PUBLISHABLE_KEY:-} ]]; then
    local value
    value=$(printf '%s\n' "$secrets" | awk -F ' = ' -v search='Stripe:PublishableKey' '$1==search {print substr($0, index($0, " = ")+3); exit}')
    if [[ -n $value ]]; then
      export STRIPE_PUBLISHABLE_KEY="$value"
      echo "ℹ️  Loaded Stripe:PublishableKey from user-secrets"
    else
      echo "⚠️  Stripe:PublishableKey missing from user-secrets"
    fi
  fi

  if [[ -z ${STRIPE_WEBHOOK_SECRET:-} ]]; then
    local value
    value=$(printf '%s\n' "$secrets" | awk -F ' = ' -v search='Stripe:WebhookSecret' '$1==search {print substr($0, index($0, " = ")+3); exit}')
    if [[ -n $value ]]; then
      export STRIPE_WEBHOOK_SECRET="$value"
      echo "ℹ️  Loaded Stripe:WebhookSecret from user-secrets"
    else
      echo "⚠️  Stripe:WebhookSecret missing from user-secrets"
    fi
  fi
}

# Provide default E2E login credentials unless caller overrides
export STRIPE_E2E_EMAIL="${STRIPE_E2E_EMAIL:-testuser@example.com}"
export STRIPE_E2E_PASSWORD="${STRIPE_E2E_PASSWORD:-TestPassword123!}"

load_stripe_env
load_user_secrets_env

is_local_url() {
  local value="${1:-}"
  [[ -n $value ]] || return 1
  [[ $value == http://localhost* || $value == http://127.0.0.1* || $value == http://0.0.0.0* ]]
}

ensure_ngrok_defaults() {
  local external="${EXTERNAL_API_BASE_URL:-}"
  local oauth="${OAUTH_BASE_URL:-}"
  local needs_ngrok=false
  local started_by_script=false

  if [[ -z $external ]] || is_local_url "$external"; then
    needs_ngrok=true
  fi

  if [[ $needs_ngrok == false ]]; then
    if [[ -z $oauth && -n $external ]]; then
      export OAUTH_BASE_URL="$external"
      echo "ℹ️  Using External API base for OAuth: $OAUTH_BASE_URL"
    fi
    return 0
  fi

  if ! command -v ngrok >/dev/null 2>&1; then
    echo "⚠️  ngrok not found on PATH; external callbacks will not work without a public URL."
    return 1
  fi

  NGROK_STARTED_BY_SCRIPT=false
  if ! start_ngrok "$API_PORT"; then
    return 1
  fi
  started_by_script=$NGROK_STARTED_BY_SCRIPT

  local ngrok_url
  ngrok_url="$(get_ngrok_public_url "$API_PORT" 2>/dev/null || true)"
  if [[ -z $ngrok_url ]]; then
    if [[ -f $NGROK_LOG_FILE ]]; then
      if grep -qi "ERR_NGROK_108" "$NGROK_LOG_FILE"; then
        echo "❌ ngrok auth error: account is limited to 1 active agent session."
        echo "    Close the existing session at https://dashboard.ngrok.com/agents and retry."
      elif grep -qi "ERR_NGROK_3200" "$NGROK_LOG_FILE"; then
        echo "❌ ngrok reserved domain is offline (ERR_NGROK_3200). Check reserved domain status."
      fi
    fi
    if [[ $started_by_script == true ]]; then
      stop_ngrok "$API_PORT"
    fi
    echo "⚠️  Unable to determine ngrok public URL. See ${NGROK_LOG_FILE}"
    return 1
  fi

  if [[ -z $external ]] || is_local_url "$external"; then
    export EXTERNAL_API_BASE_URL="$ngrok_url"
    echo "ℹ️  Using ngrok External API base: $EXTERNAL_API_BASE_URL"
  fi

  if [[ -z $oauth ]] || is_local_url "$oauth"; then
    export OAUTH_BASE_URL="$ngrok_url"
    echo "ℹ️  Using ngrok OAuth base: $OAUTH_BASE_URL"
  fi
}

wait_for_local_api() {
  local port="${1:-5032}"
  local timeout_seconds="${2:-60}"
  local start
  start="$(date +%s)"

  while true; do
    if curl -fsS "http://127.0.0.1:${port}/swagger/v1/swagger.json" >/dev/null 2>&1; then
      return 0
    fi

    if (( $(date +%s) - start >= timeout_seconds )); then
      return 1
    fi

    sleep 0.5
  done
}

start_ngrok() {
  local target_port="${1:-5032}"
  local domain="${NGROK_DOMAIN:-$DEFAULT_NGROK_DOMAIN}"
  local fallback_allowed=true

  if [[ -n ${NGROK_DOMAIN:-} ]]; then
    fallback_allowed=false
  fi

  if ! command -v ngrok >/dev/null 2>&1; then
    echo "⚠️  ngrok not found on PATH; Replicate training will fail unless you expose the API publicly."
    return 1
  fi

  if [[ -f $NGROK_PID_FILE ]]; then
    local pid
    pid="$(cat "$NGROK_PID_FILE" 2>/dev/null || true)"
    if [[ -n $pid ]] && kill -0 "$pid" >/dev/null 2>&1; then
      if curl -fsS http://127.0.0.1:4040/api/tunnels >/dev/null 2>&1; then
        local current_url
        current_url="$(get_ngrok_public_url "$target_port" 2>/dev/null || true)"
        if [[ -n $current_url ]] && [[ -z $domain || $current_url == "https://${domain}" ]]; then
          echo "ℹ️  ngrok already running for port ${target_port} (${current_url})"
          return 0
        fi
      fi
    fi
    rm -f "$NGROK_PID_FILE"
  fi

  # If ngrok is already running, ensure it is serving the reserved domain we expect.
  if pgrep -f "ngrok http ${target_port}" >/dev/null 2>&1; then
    if curl -fsS http://127.0.0.1:4040/api/tunnels >/dev/null 2>&1; then
      local current_url
      current_url="$(get_ngrok_public_url "$target_port" 2>/dev/null || true)"
      if [[ -n $current_url ]] && [[ -z $domain || $current_url == "https://${domain}" ]]; then
        echo "ℹ️  ngrok already running for port ${target_port} with ${current_url}"
        return 0
      fi

      echo "⚠️  ngrok is running but not using reserved domain (current: ${current_url:-unknown}, expected: https://${domain}). Restarting..."
    else
      echo "⚠️  ngrok process detected but local API (4040) is not responding. Restarting..."
    fi

    if [[ -f $NGROK_PID_FILE ]]; then
      stop_ngrok "$target_port"
    elif [[ ${NGROK_FORCE_KILL:-false} == true ]]; then
      pkill -f "ngrok http ${target_port}" 2>/dev/null || true
      sleep 0.5
    else
      echo "⚠️  ngrok tunnel is running without a pid file; stop it or set NGROK_FORCE_KILL=true."
      return 1
    fi
  fi

  if start_ngrok_with_domain "$target_port" "$domain"; then
    if [[ -n $domain ]] && curl -I -m 5 -sS "https://${domain}" | grep -qi '^ngrok-error-code: *ERR_NGROK_3200'; then
      echo "⚠️  ngrok reserved domain is offline (ERR_NGROK_3200)."
      echo "    See ${NGROK_LOG_FILE}"
      if [[ $fallback_allowed == true ]]; then
        stop_ngrok "$target_port"
        start_ngrok_with_domain "$target_port" ""
        return $?
      fi
      return 1
    fi
    return 0
  fi

  if [[ $fallback_allowed == true && -n $domain ]]; then
    stop_ngrok "$target_port"
    start_ngrok_with_domain "$target_port" ""
    return $?
  fi

  return 1
}

get_ngrok_public_url() {
  # Returns the https public_url for the first tunnel from ngrok's local API.
  # If a port is provided, only returns a tunnel matching that port.
  local port="${1:-}"

  if command -v python3 >/dev/null 2>&1; then
    python3 - "$port" <<'PY'
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
    return $?
  fi

  local payload
  if ! payload="$(curl -fsS http://127.0.0.1:4040/api/tunnels 2>/dev/null)"; then
    return 1
  fi

  if command -v jq >/dev/null 2>&1; then
    local url
    if [[ -n $port ]]; then
      url="$(printf '%s' "$payload" | jq -r --arg port ":${port}" '.tunnels[] | select(.public_url | startswith("https://")) | select((.config.addr | tostring) | contains($port)) | .public_url' | head -n 1)"
    else
      url="$(printf '%s' "$payload" | jq -r '.tunnels[] | select(.public_url | startswith("https://")) | .public_url' | head -n 1)"
    fi
    [[ -n ${url:-} ]] || return 1
    printf '%s\n' "$url"
    return 0
  fi

  # Fallback parser for environments without python3/jq.
  local normalized
  normalized="$(printf '%s' "$payload" | tr -d '\n')"

  local url
  if [[ -n $port ]]; then
    url="$(printf '%s' "$normalized" | sed -n 's/.*"public_url":"\\(https:[^"]*\\)".*"addr":"[^"]*:'"$port"'".*/\\1/p' | head -n 1)"
  else
    url="$(printf '%s' "$normalized" | sed -n 's/.*"public_url":"\\(https:[^"]*\\)".*/\\1/p' | head -n 1)"
  fi
  [[ -n ${url:-} ]] || return 1
  printf '%s\n' "$url"
}

start_ngrok_with_domain() {
  local target="$1"
  local requested_domain="${2:-}"

  echo "➡️  Starting ngrok tunnel for port ${target}"
  if [[ -n $requested_domain ]]; then
    echo "ℹ️  Using NGROK_DOMAIN=${requested_domain}"
    if [[ $requested_domain == *.ngrok-free.app || $requested_domain == *.ngrok.dev || $requested_domain == *.ngrok.app ]]; then
      nohup ngrok http "${target}" --url "https://${requested_domain}" --log=stdout >"$NGROK_LOG_FILE" 2>&1 &
    else
      nohup ngrok http "${target}" --domain "${requested_domain}" --log=stdout >"$NGROK_LOG_FILE" 2>&1 &
    fi
  else
    echo "ℹ️  Starting ngrok with a random public URL"
    nohup ngrok http "${target}" --log=stdout >"$NGROK_LOG_FILE" 2>&1 &
  fi

  NGROK_PID=$!
  echo "$NGROK_PID" > "$NGROK_PID_FILE"
  NGROK_STARTED_BY_SCRIPT=true
  disown "$NGROK_PID" 2>/dev/null || true

  local ready=false
  local attempts=$((NGROK_START_TIMEOUT_SECONDS * 4))
  for _ in $(seq 1 "$attempts"); do
    if curl -fsS http://127.0.0.1:4040/api/tunnels >/dev/null 2>&1; then
      ready=true
      break
    fi
    sleep 0.25
  done

  if [[ $ready != true ]]; then
    echo "⚠️  ngrok did not start (see ${NGROK_LOG_FILE})"
    return 1
  fi

  for _ in $(seq 1 "$attempts"); do
    if get_ngrok_public_url "$target" >/dev/null 2>&1; then
      return 0
    fi
    sleep 0.25
  done

  echo "⚠️  ngrok did not publish a tunnel (see ${NGROK_LOG_FILE})"
  return 1
}

stop_ngrok() {
  local target_port="${1:-5032}"

  if [[ -n ${NGROK_PID:-} ]] && kill -0 "$NGROK_PID" >/dev/null 2>&1; then
    kill "$NGROK_PID" 2>/dev/null || true
  fi
  if [[ -f $NGROK_PID_FILE ]]; then
    local pid
    pid="$(cat "$NGROK_PID_FILE" 2>/dev/null || true)"
    if [[ -n $pid ]] && kill -0 "$pid" >/dev/null 2>&1; then
      kill "$pid" 2>/dev/null || true
    fi
    rm -f "$NGROK_PID_FILE"
  fi
  NGROK_STARTED_BY_SCRIPT=false
}

cleanup() {
  local exit_code=$?
  if [[ $exit_code -ne 0 ]]; then
    echo "❌ dev-start encountered an error. Cleaning up partial state..." >&2
    stop_ngrok "$API_PORT"
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
    --docker|--containers) START_DOCKER=true ;;
  esac
done

if [[ $START_DOCKER == true ]]; then
  echo "➡️  Starting docker-compose stack"

  if [[ -z ${DEV_ENV_FILE_PATH} && -f "${SCRIPT_DIR}/.env" && -f "${SCRIPT_DIR}/local.env" ]]; then
    echo "⚠️  Both .env and local.env exist; using .env. Set DEV_ENV_FILE to override."
  fi

  if [[ -z ${DEV_ENV_FILE_PATH} ]]; then
    if [[ -f "${SCRIPT_DIR}/.env" ]]; then
      DEV_ENV_FILE_PATH="${SCRIPT_DIR}/.env"
    elif [[ -f "${SCRIPT_DIR}/local.env" ]]; then
      DEV_ENV_FILE_PATH="${SCRIPT_DIR}/local.env"
    fi
  fi

  if [[ -n ${DEV_ENV_FILE_PATH} ]]; then
    echo "ℹ️  docker compose using env file: ${DEV_ENV_FILE_PATH}"
  fi

  # In docker mode, always use the reserved ngrok domain so URLs remain stable across restarts.
  export EXTERNAL_API_BASE_URL="${EXTERNAL_API_BASE_URL:-https://${DEFAULT_NGROK_DOMAIN}}"
  echo "ℹ️  Using External API base: $EXTERNAL_API_BASE_URL"
  if [[ -n ${OAUTH_BASE_URL:-} ]]; then
    echo "ℹ️  Using OAuth base override: $OAUTH_BASE_URL"
  else
    export OAUTH_BASE_URL="$EXTERNAL_API_BASE_URL"
    echo "ℹ️  Using External API base for OAuth: $OAUTH_BASE_URL"
  fi

  write_docker_override

  local_services=(sql-server azurite)
  if $START_API; then local_services+=(api); fi
  if $START_UI; then local_services+=(frontend); fi

  if [[ -n ${DEV_ENV_FILE_PATH} ]]; then
    docker compose --env-file "${DEV_ENV_FILE_PATH}" up -d --build --force-recreate "${local_services[@]}"
  else
    docker compose up -d --build --force-recreate "${local_services[@]}"
  fi

  # Bring up ngrok only after the API is reachable, so the reserved URL doesn't intermittently 5xx.
  if wait_for_local_api "$API_PORT" 90; then
    if should_start_ngrok; then
      if ! get_ngrok_public_url "$API_PORT" >/dev/null 2>&1; then
        start_ngrok "$API_PORT" || true
      fi
    else
      echo "ℹ️  Skipping ngrok startup (local OAuth/base URL in use)."
    fi
  else
    echo "⚠️  API did not become ready on http://127.0.0.1:${API_PORT} in time; ngrok startup skipped (see docker logs)."
  fi

  trap - EXIT
  echo "✅ Docker services started."
  exit 0
fi

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
  if should_start_ngrok; then
    start_ngrok "$API_PORT" || true
  else
    echo "ℹ️  Skipping ngrok startup (local OAuth/base URL in use)."
  fi
else
  echo "⚠️  ngrok not found on PATH; skipping tunnel startup"
fi

trap - EXIT

echo "✅ Dev services started (API/UI)."
