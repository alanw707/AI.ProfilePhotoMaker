#!/bin/bash
set -euo pipefail

BASE_APP_URL="${BASE_APP_URL:-https://aiprofilephotomaker.com}"
BASE_API_URL="${BASE_API_URL:-https://api.aiprofilephotomaker.com}"

RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m'

fail() {
  echo -e "${RED}❌ $1${NC}" >&2
  exit 1
}

info() {
  echo -e "${BLUE}ℹ️  $1${NC}"
}

pass() {
  echo -e "${GREEN}✅ $1${NC}"
}

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || fail "Missing required command: $1"
}

require_cmd curl
require_cmd grep

info "Smoke: app=${BASE_APP_URL} api=${BASE_API_URL}"

info "Checking root redirects..."
root_headers="$(curl -sS -I https://aiprofilephotomaker.com || true)"
echo "$root_headers" | grep -qi '^location: https://app\.aiprofilephotomaker\.com/' || fail "Root domain does not redirect to aiprofilephotomaker.com"
pass "Root redirects to app"

www_headers="$(curl -sS -I https://www.aiprofilephotomaker.com || true)"
echo "$www_headers" | grep -qi '^location: https://app\.aiprofilephotomaker\.com/' || fail "www does not redirect to aiprofilephotomaker.com"
pass "www redirects to app"

info "Checking API health..."
curl -sS "${BASE_API_URL}/api/health" | grep -q '"status":"Healthy"' || fail "API health is not Healthy"
pass "API health is Healthy"

info "Checking storage health..."
storage_json="$(curl -sS "${BASE_API_URL}/api/health/storage" || true)"
echo "$storage_json" | grep -q '"provider":"AzureBlobStorage"' || fail "Storage provider is not AzureBlobStorage"
echo "$storage_json" | grep -q '"status":"Healthy"' || fail "Storage health is not Healthy"
echo "$storage_json" | grep -q '"download":true' || fail "Storage download test is not passing"
pass "Storage health is Healthy (download=true)"

info "Checking app responds..."
curl -sS -I "${BASE_APP_URL}" | grep -qi '^http/' || fail "App is not reachable"
pass "App is reachable"

pass "Production smoke checks passed"

