#!/usr/bin/env bash
set -euo pipefail

# Lightweight local monitoring for training/generation without needing browser auth tokens.
# Works by reading API container logs and extracting the most recent training-related events.
#
# Usage:
#   ./scripts/monitor-training.sh            # show recent summary
#   ./scripts/monitor-training.sh --follow   # stream relevant logs
#   ./scripts/monitor-training.sh --since 2h # change lookback window
#
# Env:
#   AIPM_API_CONTAINER (default: aipm-api)

API_CONTAINER="${AIPM_API_CONTAINER:-aipm-api}"
SINCE="30m"
FOLLOW=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --follow|-f) FOLLOW=true; shift ;;
    --since) SINCE="${2:-}"; shift 2 ;;
    --container) API_CONTAINER="${2:-}"; shift 2 ;;
    -h|--help)
      sed -n '1,120p' "$0"
      exit 0
      ;;
    *)
      echo "Unknown arg: $1" >&2
      exit 2
      ;;
  esac
done

require() {
  command -v "$1" >/dev/null 2>&1 || { echo "Missing required command: $1" >&2; exit 1; }
}

require docker

if ! docker ps --format '{{.Names}}' | grep -qx "$API_CONTAINER"; then
  echo "API container '$API_CONTAINER' is not running."
  echo "Tip: start containers with ./dev-start.sh --docker"
  exit 1
fi

FILTER_RE='(TrainingPollingService|PendingGenerationService|ReplicateController|ReplicateApiClient|CreateTrainingZip|training completion|Model creation request|Creating training|Refunded .* credits|model_training|styled_generation|webhook|NormalizeImageUrl|Preflight)'

if [[ "$FOLLOW" == true ]]; then
  echo "Streaming logs for '$API_CONTAINER' (since $SINCE). Ctrl-C to stop."
  docker logs -f --since "$SINCE" "$API_CONTAINER" 2>&1 | stdbuf -oL -eL grep -E --line-buffered "$FILTER_RE" || true
  exit 0
fi

tmp="$(mktemp)"

docker logs --since "$SINCE" "$API_CONTAINER" 2>&1 >"$tmp"

echo "Recent training-related log lines (since $SINCE):"
echo "------------------------------------------------"
matches="$(mktemp)"
trap 'rm -f "$tmp" "$matches"' EXIT

grep -E "$FILTER_RE" "$tmp" >"$matches" || true
tail -n 120 "$matches" || true
echo "------------------------------------------------"

# Best-effort status inference from last meaningful line.
last_line="$(grep -E "(TrainingPollingService|PendingGenerationService|ReplicateController|ReplicateApiClient|CreateTrainingZip|Model creation request|training completion|Creating training)" "$matches" | tail -n 1 || true)"
if [[ -z "${last_line:-}" ]]; then
  echo "No matching training logs found in the last $SINCE."
  exit 0
fi

status="UNKNOWN"
case "$last_line" in
  *"marked as Ready"*|*"Model creation request"*"Ready"*)
    status="READY"
    ;;
  *"failed"*|*"Failed"*|*"canceled"*|*"Canceled"*|*"auth"*|*"Auth"*)
    status="FAILED"
    ;;
  *"Creating training"*|*"Starting polling for training"*|*"Creating model "*|*"Updated model creation request"*)
    status="IN_PROGRESS"
    ;;
esac

echo "Inferred status: $status"
