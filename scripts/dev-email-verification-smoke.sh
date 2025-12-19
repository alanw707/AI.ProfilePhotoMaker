#!/usr/bin/env bash
set -euo pipefail

API_BASE_URL="${API_BASE_URL:-http://localhost:5032}"

if ! command -v curl >/dev/null 2>&1; then
  echo "curl is required"
  exit 1
fi

tmpdir="$(mktemp -d)"
cookiejar="$tmpdir/cookies.txt"

cleanup() {
  rm -rf "$tmpdir"
}
trap cleanup EXIT

email="local-smoke-$(date +%s)-$RANDOM@example.com"
password="LocalSmoke!123"
turnstile_token="${TURNSTILE_TOKEN:-}"

echo "API_BASE_URL=$API_BASE_URL"
echo "Registering user: $email"

register_payload=$(cat <<EOF
{
  "email": "$email",
  "password": "$password",
  "firstName": "Local",
  "lastName": "Smoke",
  "gender": "male",
  "ethnicity": "asian"$(if [[ -n "$turnstile_token" ]]; then echo ",\"turnstileToken\":\"$turnstile_token\""; fi)
}
EOF
)

curl -sS -c "$cookiejar" -b "$cookiejar" \
  -H 'Content-Type: application/json' \
  -X POST "$API_BASE_URL/api/auth/register" \
  -d "$register_payload" >/dev/null

echo "Checking account status (expect emailConfirmed=false)..."
status_before="$(curl -sS -c "$cookiejar" -b "$cookiejar" \
  -H 'Content-Type: application/json' \
  "$API_BASE_URL/api/auth/account-status")"
echo "$status_before"

echo "Confirming email via dev endpoint (development only)..."
confirm_resp="$(curl -sS -c "$cookiejar" -b "$cookiejar" \
  -H 'Content-Type: application/json' \
  -X POST "$API_BASE_URL/api/auth/dev/confirm-email" \
  -d '{}' )"
echo "$confirm_resp"

echo "Checking account status again (expect emailConfirmed=true)..."
status_after="$(curl -sS -c "$cookiejar" -b "$cookiejar" \
  -H 'Content-Type: application/json' \
  "$API_BASE_URL/api/auth/account-status")"
echo "$status_after"

echo "Done."
echo "Next: open UI and verify /app/* is accessible now."
