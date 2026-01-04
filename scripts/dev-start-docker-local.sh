#!/usr/bin/env bash
set -euo pipefail

if [[ ! -f "./local.env" ]]; then
  echo "local.env not found in $(pwd)" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1091
source ./local.env
set +a

./dev-start.sh --docker
