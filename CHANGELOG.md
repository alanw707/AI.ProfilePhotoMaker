# Changelog

All notable changes to this project will be documented in this file.

## 2025-08-20 — CORS Hotfix, Quality Improvements, and Guardrails

- fix(api): Set Blob `Content-Type` when uploading to Azure Storage so images return correct MIME types (jpeg/png/webp/zip). Ensures proper rendering and caching.
- fix(api): Correct Azure Blob overwrite logic when using `BlobUploadOptions` by deleting existing blobs before upload.
- fix(ui): Avoid sending dev-only header in production gallery downloads to reduce unnecessary CORS preflight requests.
- infra(bicep): Fix CORS array syntax and remove unnecessary `dependsOn` entries; keep `allowedOrigins` minimal and explicit.
- ci(deploy): Add post-deploy validation to verify Azure Blob CORS configuration and preflight behavior; fail fast on misconfiguration.
- chore: Remove temporary hotfix workflow and runbook now that infra + CI enforce CORS.

Verification
- UI lint passes (errors-only) and production build succeeds.
- Bicep template builds clean.
- Production Blob preflight and GET with Origin now include `Access-Control-Allow-Origin` and expected headers.
