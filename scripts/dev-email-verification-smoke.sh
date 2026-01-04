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
  "ethnicity": "asian",
  "ageConfirmed": true$(if [[ -n "$turnstile_token" ]]; then echo ",\"turnstileToken\":\"$turnstile_token\""; fi)
}
EOF
)

register_response="$(curl -sS -c "$cookiejar" -b "$cookiejar" \
  -H 'Content-Type: application/json' \
  -X POST "$API_BASE_URL/api/auth/register" \
  -d "$register_payload")"

if ! echo "$register_response" | grep -q '"isSuccess":true'; then
  echo "Registration failed:"
  echo "$register_response"
  echo "Tip: if Turnstile is enabled locally, set TURNSTILE_TOKEN and retry."
  exit 1
fi

login_payload=$(cat <<EOF
{
  "email": "$email",
  "password": "$password",
  "ageConfirmed": true
}
EOF
)

ensure_authenticated() {
  local status_json
  status_json="$(curl -sS -c "$cookiejar" -b "$cookiejar" \
    -H 'Content-Type: application/json' \
    "$API_BASE_URL/api/auth/account-status")"

  if echo "$status_json" | grep -q '"success":true'; then
    echo "$status_json"
    return 0
  fi

  echo "Account status unauthorized; logging in to refresh session..."
  login_response="$(curl -sS -c "$cookiejar" -b "$cookiejar" \
    -H 'Content-Type: application/json' \
    -X POST "$API_BASE_URL/api/auth/login" \
    -d "$login_payload")"

  if ! echo "$login_response" | grep -q '"isSuccess":true'; then
    echo "Login failed:"
    echo "$login_response"
    return 1
  fi

  status_json="$(curl -sS -c "$cookiejar" -b "$cookiejar" \
    -H 'Content-Type: application/json' \
    "$API_BASE_URL/api/auth/account-status")"
  echo "$status_json"
  echo "$status_json" | grep -q '"success":true'
}

echo "Checking account status (expect emailConfirmed=false)..."
if ! status_before="$(ensure_authenticated)"; then
  exit 1
fi
echo "$status_before"

echo "Confirming email via dev endpoint (development only)..."
confirm_resp="$(curl -sS -c "$cookiejar" -b "$cookiejar" \
  -H 'Content-Type: application/json' \
  -X POST "$API_BASE_URL/api/auth/dev/confirm-email" \
  -d '{}' )"
echo "$confirm_resp"

echo "Checking account status again (expect emailConfirmed=true)..."
if ! status_after="$(ensure_authenticated)"; then
  exit 1
fi
echo "$status_after"

echo "Done."
echo "Next: open UI and verify /app/* is accessible now."
