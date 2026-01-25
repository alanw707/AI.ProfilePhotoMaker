#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="${DEV_ENV_FILE:-./.env}"
if [[ -z ${DEV_ENV_FILE:-} && -f "./.env" && -f "./local.env" ]]; then
  echo "⚠️  Both .env and local.env exist; using .env. Set DEV_ENV_FILE to override."
fi
if [[ ! -f "$ENV_FILE" && -f "./local.env" ]]; then
  ENV_FILE="./local.env"
fi

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Env file not found. Set DEV_ENV_FILE or create .env/local.env in $(pwd)" >&2
  exit 1
fi

echo "ℹ️  Using env file: ${ENV_FILE}"
set -a
# shellcheck disable=SC1091
source "$ENV_FILE"
set +a

export DEV_ENV_FILE="$ENV_FILE"

./dev-start.sh --docker
