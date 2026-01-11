#!/bin/bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
DEFAULT_OUTPUT_DIR="${REPO_ROOT}/AI.ProfilePhotoMaker.API/style-previews"

BASE_URL="${BASE_URL:-http://localhost:5032}"
AUTH_TOKEN="${AUTH_TOKEN:-}"
USER_ID="${USER_ID:-}"
STYLE_LIST="${STYLE_LIST:-linkedin,executive,academic}"
OUTPUT_DIR="${OUTPUT_DIR:-${DEFAULT_OUTPUT_DIR}}"
NUM_OUTPUTS="${NUM_OUTPUTS:-1}"
POLL_INTERVAL_SECONDS="${POLL_INTERVAL_SECONDS:-6}"
MAX_POLL_ATTEMPTS="${MAX_POLL_ATTEMPTS:-40}"
USE_DIAGNOSTIC="${USE_DIAGNOSTIC:-1}"
USE_JQ="${USE_JQ:-1}"

usage() {
    cat <<'EOF'
Usage: scripts/generate-style-previews.sh [options]

Options (env vars also supported):
  --base-url URL           API base URL (default: http://localhost:5032)
  --auth-token TOKEN       Bearer token for an authenticated user (replicate endpoint only)
  --user-id USER_ID        Optional user id (replicate endpoint only)
  --styles LIST            Comma-separated style list (default: linkedin,executive,academic)
  --output-dir PATH        Output directory (default: AI.ProfilePhotoMaker.API/style-previews)
  --num-outputs N          NumOutputs for generation (default: 1)
  --poll-interval SECONDS  Poll interval for prediction status (default: 6)
  --max-attempts N         Max polling attempts per style (default: 40)
  --use-replicate          Use /api/replicate endpoints (requires trained model + credits)
  --help                   Show help

Notes:
- Default mode uses dev-only /api/diagnostic endpoints and the base model.
- Use --use-replicate to call the authenticated /api/replicate endpoints.
- After generating previews, run: AI.ProfilePhotoMaker.API/upload-style-previews.sh --force
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --base-url)
            BASE_URL="$2"
            shift 2
            ;;
        --auth-token)
            AUTH_TOKEN="$2"
            shift 2
            ;;
        --user-id)
            USER_ID="$2"
            shift 2
            ;;
        --styles)
            STYLE_LIST="$2"
            shift 2
            ;;
        --output-dir)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --num-outputs)
            NUM_OUTPUTS="$2"
            shift 2
            ;;
        --poll-interval)
            POLL_INTERVAL_SECONDS="$2"
            shift 2
            ;;
        --max-attempts)
            MAX_POLL_ATTEMPTS="$2"
            shift 2
            ;;
        --use-replicate)
            USE_DIAGNOSTIC="0"
            shift
            ;;
        --help|-h)
            usage
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            usage
            exit 1
            ;;
    esac
done

if [[ "$USE_DIAGNOSTIC" != "1" && -z "$AUTH_TOKEN" ]]; then
    echo "Missing AUTH_TOKEN. Provide --auth-token or AUTH_TOKEN env var."
    exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
    echo "curl is required."
    exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
    USE_JQ="0"
    if ! command -v python3 >/dev/null 2>&1; then
        echo "jq or python3 is required for JSON handling."
        exit 1
    fi
fi

json_payload() {
    if [[ "$USE_JQ" == "1" ]]; then
        jq -n "$@"
        return
    fi
    python3 - <<'PY'
import json
import os

payload = json.loads(os.environ.get("JSON_PAYLOAD", "{}"))
print(json.dumps(payload))
PY
}

json_get() {
    local json="$1"
    local path="$2"
    if [[ "$USE_JQ" == "1" ]]; then
        echo "$json" | jq -r "$path"
        return
    fi
    JSON_INPUT="$json" JSON_PATH="$path" python3 - <<'PY'
import json
import os

data = json.loads(os.environ.get("JSON_INPUT", "{}"))
path = os.environ.get("JSON_PATH", "").strip()
path = path.replace("// empty", "").strip()
path = path.lstrip(".")

def lookup(obj, keys):
    for key in keys:
        if key.isdigit():
            idx = int(key)
            if not isinstance(obj, list) or idx >= len(obj):
                return None
            obj = obj[idx]
        else:
            if not isinstance(obj, dict) or key not in obj:
                return None
            obj = obj[key]
    return obj

keys = [k for k in path.split(".") if k]
value = lookup(data, keys)
if value is None:
    print("")
elif isinstance(value, bool):
    print("true" if value else "false")
else:
    print(value)
PY
}

mkdir -p "$OUTPUT_DIR"

IFS=',' read -r -a styles <<< "$STYLE_LIST"
success_count=0
failure_count=0

echo "Generating style previews into ${OUTPUT_DIR}"
echo "Styles: ${STYLE_LIST}"

for style in "${styles[@]}"; do
    style_trimmed="$(echo "$style" | xargs)"
    if [[ -z "$style_trimmed" ]]; then
        continue
    fi

    echo ""
    echo "Starting generation for style: ${style_trimmed}"

    if [[ "$USE_DIAGNOSTIC" == "1" ]]; then
        if [[ "$USE_JQ" == "1" ]]; then
            request_payload=$(json_payload \
                --arg style "$style_trimmed" \
                --argjson numOutputs "$NUM_OUTPUTS" \
                '{style: $style, numOutputs: $numOutputs}')
        else
            JSON_PAYLOAD=$(python3 - <<PY
import json
print(json.dumps({"style": "${style_trimmed}", "numOutputs": int(${NUM_OUTPUTS})}))
PY
)
            request_payload=$(JSON_PAYLOAD="$JSON_PAYLOAD" json_payload)
        fi
        response=$(curl -sS -X POST "${BASE_URL}/api/diagnostic/style-previews/generate" \
            -H "Content-Type: application/json" \
            -d "$request_payload")
    else
        if [[ "$AUTH_TOKEN" != *.*.* ]]; then
            echo "Warning: AUTH_TOKEN does not look like a JWT. Ensure it's valid."
        fi
        if [[ "$USE_JQ" == "1" ]]; then
            request_payload=$(json_payload \
                --arg style "$style_trimmed" \
                --arg userId "$USER_ID" \
                --argjson numOutputs "$NUM_OUTPUTS" \
                '{style: $style, userId: $userId, numOutputs: $numOutputs}')
        else
            JSON_PAYLOAD=$(python3 - <<PY
import json
print(json.dumps({"style": "${style_trimmed}", "userId": "${USER_ID}", "numOutputs": int(${NUM_OUTPUTS})}))
PY
)
            request_payload=$(JSON_PAYLOAD="$JSON_PAYLOAD" json_payload)
        fi
        response=$(curl -sS -X POST "${BASE_URL}/api/replicate/generate" \
            -H "Authorization: Bearer ${AUTH_TOKEN}" \
            -H "Content-Type: application/json" \
            -d "$request_payload")
    fi

    success=$(json_get "$response" '.success')
    if [[ -z "$success" ]]; then
        success="false"
    fi
    if [[ "$success" != "true" ]]; then
        echo "Failed to start generation for ${style_trimmed}:"
        if [[ "$USE_JQ" == "1" ]]; then
            echo "$response" | jq '.'
        else
            echo "$response"
        fi
        failure_count=$((failure_count + 1))
        continue
    fi

    prediction_id=$(json_get "$response" '.data.prediction.id')
    if [[ -z "$prediction_id" ]]; then
        echo "Missing prediction id for ${style_trimmed}."
        failure_count=$((failure_count + 1))
        continue
    fi

    echo "Prediction id: ${prediction_id}"

    attempt=1
    while [[ $attempt -le $MAX_POLL_ATTEMPTS ]]; do
        if [[ "$USE_DIAGNOSTIC" == "1" ]]; then
            status_response=$(curl -sS "${BASE_URL}/api/diagnostic/style-previews/status/${prediction_id}")
        else
            status_response=$(curl -sS "${BASE_URL}/api/replicate/generate/status/${prediction_id}" \
                -H "Authorization: Bearer ${AUTH_TOKEN}")
        fi

        status=$(json_get "$status_response" '.data.status')
        if [[ "$status" == "succeeded" ]]; then
            data_url=$(json_get "$status_response" '.data.dataUrl')
            output_url=$(json_get "$status_response" '.data.output[0]')
            output_path="${OUTPUT_DIR}/${style_trimmed}.png"

            if [[ -n "$output_url" ]]; then
                curl -sS -L "$output_url" -o "$output_path"
                echo "Saved preview to ${output_path}"
                success_count=$((success_count + 1))
            elif [[ -n "$data_url" && "$data_url" == data:* ]] && command -v python3 >/dev/null 2>&1; then
                printf '%s' "$data_url" | python3 - "$output_path" <<'PY'
import base64
import sys

data_url = sys.stdin.read().strip()
if not data_url or "," not in data_url:
    sys.exit("Invalid dataUrl")
_, encoded = data_url.split(",", 1)
payload = base64.b64decode(encoded)
with open(sys.argv[1], "wb") as f:
    f.write(payload)
PY
                echo "Saved preview to ${output_path}"
                success_count=$((success_count + 1))
            elif [[ -n "$data_url" && "$data_url" == data:* ]]; then
                echo "Data URL available but python3 is missing. Install python3 or use the output URL."
                failure_count=$((failure_count + 1))
            else
                echo "Succeeded but no output URL found for ${style_trimmed}."
                failure_count=$((failure_count + 1))
            fi

            break
        fi

        if [[ "$status" == "failed" || "$status" == "canceled" ]]; then
            echo "Prediction ${prediction_id} for ${style_trimmed} ended with status: ${status}"
            if [[ "$USE_JQ" == "1" ]]; then
                echo "$status_response" | jq '.'
            else
                echo "$status_response"
            fi
            failure_count=$((failure_count + 1))
            break
        fi

        echo "Waiting for ${style_trimmed} (${status:-unknown})... attempt ${attempt}/${MAX_POLL_ATTEMPTS}"
        attempt=$((attempt + 1))
        sleep "$POLL_INTERVAL_SECONDS"
    done

    if [[ $attempt -gt $MAX_POLL_ATTEMPTS ]]; then
        echo "Timed out waiting for ${style_trimmed}."
        failure_count=$((failure_count + 1))
    fi
done

echo ""
echo "Generation complete. Success: ${success_count}, Failed: ${failure_count}"
if [[ $success_count -eq 0 ]]; then
    echo "No previews generated successfully."
    exit 1
fi
echo "Next: AI.ProfilePhotoMaker.API/upload-style-previews.sh --force"
