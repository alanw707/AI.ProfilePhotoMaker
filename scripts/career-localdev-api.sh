#!/usr/bin/env bash
# Start the API for career UX review: LocalDev (in-memory DB, local file storage,
# external services stubbed), career flag on, on http://localhost:5032 (the UI
# proxy target). Never touches production. Logs: /tmp/api-localdev.log
#   scripts/career-localdev-api.sh start|stop
set -euo pipefail
cd "$(dirname "$0")/../AI.ProfilePhotoMaker.API"

stop() {
  local pids
  pids=$(ss -ltnp 'sport = :5032' 2>/dev/null | grep -o 'pid=[0-9]*' | cut -d= -f2 | sort -u || true)
  if [ -n "$pids" ]; then kill $pids; fi
}

case "${1:-start}" in
  stop) stop ;;
  start)
    stop
    ASPNETCORE_ENVIRONMENT=LocalDev \
    ASPNETCORE_URLS=http://localhost:5032 \
    Features__CareerWorkspace="${CAREER_FLAG:-true}" \
    AzureStorage__ConnectionString= \
    ConnectionStrings__AzureStorage= \
    AZURE_STORAGE_CONNECTION_STRING= \
      nohup dotnet run --no-launch-profile > /tmp/api-localdev.log 2>&1 &
    for _ in $(seq 1 60); do
      sleep 3
      if [ "$(curl -s -o /dev/null -w '%{http_code}' http://localhost:5032/api/config/client)" = 200 ]; then
        echo "API ready on http://localhost:5032 (career flag ${CAREER_FLAG:-true})"
        exit 0
      fi
    done
    echo "API did not start; see /tmp/api-localdev.log" >&2
    exit 1
    ;;
esac
