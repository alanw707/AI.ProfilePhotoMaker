---
inclusion: always
---
# Local Run Cheatsheet

## Prerequisites
- Ensure required env vars present via user-secrets or `.env` in repo root. Minimal set:
  - `MSSQL_SA_PASSWORD` (for local SQL container) or `ConnectionStrings__DefaultConnection`
  - `JWT_SECRET`
  - `REPLICATE_API_TOKEN`, `REPLICATE_WEBHOOK_SECRET` (can use dummy values for dev)

## Start API (Development)
- Launch profile `https` exposes: `https://0.0.0.0:7173; http://0.0.0.0:5032`
- File: [Properties/launchSettings.json](mdc:AI.ProfilePhotoMaker.API/Properties/launchSettings.json)

## Start UI (Angular)
- Dev server on `http://localhost:4200` with proxy to API: [proxy.conf.json](mdc:AI.ProfilePhotoMaker.UI/proxy.conf.json)
- Scripts: [package.json](mdc:AI.ProfilePhotoMaker.UI/package.json)

## Health Checks
- Basic: `GET /api/health`
- Readiness: `GET /api/health/ready`
- Liveness: `GET /api/health/live`
- Simple: `GET /health`
- Controller: [Controllers/HealthController.cs](mdc:AI.ProfilePhotoMaker.API/Controllers/HealthController.cs)
