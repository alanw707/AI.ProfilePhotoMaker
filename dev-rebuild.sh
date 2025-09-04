#!/usr/bin/env bash
set -euo pipefail

# Rebuild and restart API and UI dev servers only (no DB/containers)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR" && pwd)"

REBUILD_API=true
REBUILD_UI=true

for arg in "$@"; do
  case "$arg" in
    --api-only) REBUILD_UI=false ;;
    --ui-only) REBUILD_API=false ;;
  esac
done

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

