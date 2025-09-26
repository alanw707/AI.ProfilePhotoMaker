#!/usr/bin/env bash
set -euo pipefail

# Start only API and UI dev servers. Does NOT touch Docker DB/azurite.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

START_API=true
START_UI=true
API_STARTED=false
UI_STARTED=false
NGROK_PID=""

API_PROJECT="${SCRIPT_DIR}/AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj"

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

