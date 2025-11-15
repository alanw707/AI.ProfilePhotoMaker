#!/usr/bin/env bash
set -euo pipefail

# Starts a Stripe CLI webhook forwarder and automatically updates the API's webhook secret.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
API_PROJECT="${REPO_ROOT}/AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj"

if ! command -v stripe >/dev/null 2>&1; then
  echo "❌ stripe CLI not found on PATH. Install it from https://stripe.com/docs/stripe-cli before running this script." >&2
  exit 1
fi

load_stripe_secret_key() {
  if [[ -n ${STRIPE_SECRET_KEY:-} ]]; then
    return
  fi

  if [[ ! -f $API_PROJECT ]]; then
    echo "⚠️  API project not found at $API_PROJECT; cannot read user-secrets for Stripe key." >&2
    return
  fi

  local value
  if value=$(dotnet user-secrets list --project "$API_PROJECT" 2>/dev/null | awk -F ' = ' '$1=="Stripe:SecretKey"{print $2; exit}'); then
    if [[ -n $value ]]; then
      export STRIPE_SECRET_KEY="$value"
      echo "ℹ️  Loaded Stripe:SecretKey from dotnet user-secrets."
    fi
  fi
}

load_stripe_secret_key

if [[ -z ${STRIPE_SECRET_KEY:-} ]]; then
  echo "❌ STRIPE_SECRET_KEY is not set and could not be loaded from user-secrets." >&2
  echo "   Run: dotnet user-secrets set \"Stripe:SecretKey\" \"sk_test_...\"" >&2
  exit 1
fi

API_PORT="${API_HTTP_PORT:-5032}"
FORWARD_URL="${STRIPE_FORWARD_URL:-http://localhost:${API_PORT}/api/webhooks/stripe}"
echo "➡️  Forwarding Stripe events to $FORWARD_URL"
echo "   (Press Ctrl+C to stop)"

update_webhook_secret() {
  local secret="$1"
  if [[ -z $secret ]]; then
    return
  fi

  if [[ -f $API_PROJECT ]]; then
    if dotnet user-secrets set --project "$API_PROJECT" "Stripe:WebhookSecret" "$secret" >/dev/null; then
      echo "✅ Updated Stripe:WebhookSecret in dotnet user-secrets."
    else
      echo "⚠️  Failed to update Stripe:WebhookSecret automatically. Please run:" >&2
      echo "    dotnet user-secrets set \"Stripe:WebhookSecret\" \"$secret\"" >&2
    fi
  else
    echo "⚠️  API project not found; please update Stripe:WebhookSecret to $secret manually." >&2
  fi
}

stripe_cmd=(stripe listen --forward-to "$FORWARD_URL" --api-key "$STRIPE_SECRET_KEY")

webhook_secret=""
stdbuf -oL -eL "${stripe_cmd[@]}" 2>&1 | while IFS= read -r line; do
  if [[ -z $webhook_secret && $line =~ whsec_[A-Za-z0-9]+ ]]; then
    webhook_secret="${BASH_REMATCH[0]}"
    update_webhook_secret "$webhook_secret"
    echo "ℹ️  Listening with webhook secret: $webhook_secret"
  fi

  printf '%s\n' "$line"
done
