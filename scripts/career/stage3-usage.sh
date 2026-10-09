#!/usr/bin/env bash
# Pull the career usage report and kill-switch state from production (admin only).
# Usage: CAREER_ADMIN_COOKIE='<Cookie header copied from a signed-in admin browser session>' \
#        scripts/career/stage3-usage.sh [fromIso] [toIso]
# Read-only: GETs only; never changes controls, flags or the allowlist.
set -euo pipefail
API="${CAREER_API:-https://api.aiprofilephotomaker.com}"
: "${CAREER_ADMIN_COOKIE:?set CAREER_ADMIN_COOKIE to an admin session Cookie header}"
FROM="${1:-2026-10-08T01:40:00Z}"   # Stage 2 deploy (#434)
TO="${2:-$(date -u +%Y-%m-%dT%H:%M:%SZ)}"
get() { curl -fsS -H "Cookie: $CAREER_ADMIN_COOKIE" "$API$1"; }
echo "== usage $FROM .. $TO"; get "/api/admin/career/usage?from=$FROM&to=$TO" | python3 -m json.tool
echo "== controls"; get "/api/admin/career/controls" | python3 -m json.tool
