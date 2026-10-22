#!/usr/bin/env bash
#
# Publish the static project page (scripts/shutdown/showcase/) on the existing
# web Container App, keeping the aiprofilephotomaker.com binding and its
# managed certificate. No custom image or registry: the app runs Microsoft's
# public nginx image and the site files are mounted from Container App secrets
# (base64), decoded into /tmp/site at startup.
#
#   scripts/shutdown/publish-showcase.sh            # apply and verify
#   DRY_RUN=1 scripts/shutdown/publish-showcase.sh  # print the spec, change nothing
#   LOCAL=1 scripts/shutdown/publish-showcase.sh    # serve it locally on :18080 with Docker
#
# Re-runnable: each run replaces the app's secrets, registries and template
# with exactly this spec. Requires: az (signed in), jq, curl; Docker for LOCAL=1.

set -euo pipefail

RG="${RG:-aiprofilemaker-v1}"
APP="${APP:-aipm-web-v1}"
DOMAIN="${DOMAIN:-aiprofilephotomaker.com}"
IMAGE="mcr.microsoft.com/azurelinux/base/nginx:1.25"
API="2024-03-01"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SITE="$HERE/showcase"
CONF="$HERE/showcase-host/nginx.conf"
# shellcheck disable=SC2016  # expanded by the container's shell, not here
START='mkdir -p /tmp/site && for f in /mnt/site/*.b64; do base64 -d "$f" > "/tmp/site/$(basename "$f" .b64)"; done && exec nginx -c /mnt/site/nginx.conf -g "daemon off;"'

[[ -f "$SITE/index.html" && -f "$CONF" ]] || { echo "missing $SITE/index.html or $CONF" >&2; exit 1; }

# secret name from a file name: lowercase, [a-z0-9-] only, prefixed.
secret_name() { printf 'site-%s' "$(tr '[:upper:]' '[:lower:]' <<<"$1" | tr -c 'a-z0-9\n' '-')"; }

if [[ "${LOCAL:-0}" == 1 ]]; then
  tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
  for f in "$SITE"/*; do base64 -w0 "$f" > "$tmp/$(basename "$f").b64"; done
  cp "$CONF" "$tmp/nginx.conf"
  docker rm -f aipm-showcase-local >/dev/null 2>&1 || true
  docker run -d --name aipm-showcase-local -p 18080:80 -v "$tmp":/mnt/site:ro \
    --entrypoint sh "$IMAGE" -c "$START" >/dev/null
  sleep 2; curl -fsS -o /dev/null -w 'local: http://localhost:18080 → %{http_code}\n' http://localhost:18080/
  echo "stop with: docker rm -f aipm-showcase-local"
  read -r -p "Press Enter to stop and clean up " _ || true
  docker rm -f aipm-showcase-local >/dev/null; exit 0
fi

# Build secrets + secret-volume entries (one per site file, plus nginx.conf).
# Kept in files: the base64 payload is too big for command-line arguments.
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
echo '[]' > "$work/secrets.json"; echo '[]' > "$work/vol.json"
add() { # add SECRET_NAME PATH_IN_VOLUME VALUE_FILE
  jq --arg n "$1" --rawfile v "$3" '. + [{name:$n, value:$v}]' "$work/secrets.json" > "$work/s.tmp"
  mv "$work/s.tmp" "$work/secrets.json"
  jq --arg n "$1" --arg p "$2" '. + [{secretRef:$n, path:$p}]' "$work/vol.json" > "$work/v.tmp"
  mv "$work/v.tmp" "$work/vol.json"
}
for f in "$SITE"/*; do
  b=$(basename "$f"); base64 -w0 "$f" > "$work/$b.b64"
  add "$(secret_name "$b")" "$b.b64" "$work/$b.b64"
done
add site-nginx-conf nginx.conf "$CONF"

ID=$(az containerapp show -g "$RG" -n "$APP" --query id -o tsv)
az rest --method get --url "https://management.azure.com$ID?api-version=$API" > "$work/current.json"

jq --slurpfile secrets "$work/secrets.json" --slurpfile vol "$work/vol.json" --arg image "$IMAGE" --arg start "$START" '
  {
    location: .location,
    properties: {
      environmentId: (.properties.environmentId // .properties.managedEnvironmentId),
      configuration: {
        activeRevisionsMode: "Single",
        ingress: {
          external: true,
          targetPort: 80,
          transport: "auto",
          allowInsecure: false,
          customDomains: .properties.configuration.ingress.customDomains,
          traffic: [{latestRevision: true, weight: 100}]
        },
        secrets: $secrets[0],
        registries: []
      },
      template: {
        containers: [{
          name: "showcase",
          image: $image,
          command: ["sh"],
          args: ["-c", $start],
          resources: {cpu: 0.25, memory: "0.5Gi"},
          volumeMounts: [{volumeName: "site", mountPath: "/mnt/site"}],
          probes: [{type: "Liveness", httpGet: {path: "/healthz", port: 80}, periodSeconds: 30}]
        }],
        scale: {minReplicas: 0, maxReplicas: 1},
        volumes: [{name: "site", storageType: "Secret", secrets: $vol[0]}]
      }
    }
  }' "$work/current.json" > "$work/body.json"

echo "custom domains kept: $(jq -r '[.properties.configuration.ingress.customDomains[]?.name] | join(", ")' "$work/body.json")"
echo "site files: $(jq -r '[.properties.template.volumes[0].secrets[].path] | join(", ")' "$work/body.json")"
if [[ "${DRY_RUN:-0}" == 1 ]]; then
  jq '.properties.configuration.secrets |= map(.value = "<\(.value|length) chars>")' "$work/body.json"; exit 0
fi

az rest --method put --url "https://management.azure.com$ID?api-version=$API" \
  --body @"$work/body.json" -o none
echo "applied; waiting for the new revision"
for _ in $(seq 1 40); do
  state=$(az containerapp show -g "$RG" -n "$APP" --query properties.provisioningState -o tsv)
  [[ "$state" == Succeeded ]] && break; sleep 6
done
echo "provisioning: $state"

# Verify over HTTPS on the real domain (first request may cold-start).
ok=0
for _ in $(seq 1 20); do
  if curl -fsS --max-time 30 "https://$DOMAIN/" | grep -q "project archive"; then ok=1; break; fi
  sleep 6
done
(( ok )) || { echo "https://$DOMAIN does not show the project page yet" >&2; exit 1; }
echo "https://$DOMAIN serves the project page"
grep -o 'src="[^"]*"' "$SITE/index.html" | cut -d'"' -f2 | while read -r img; do
  printf '  %-20s %s\n' "$img" "$(curl -s -o /dev/null -w '%{http_code}' "https://$DOMAIN/$img")"
done
