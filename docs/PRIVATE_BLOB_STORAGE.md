# Private Blob Storage via API Proxy

This guide explains how to switch image delivery from public Azure Blob URLs to private containers served through the API proxy.

## Overview
- Default (current prod): public Blob access, UI fetches images from Azure URLs directly.
- Private mode: API serves images at `/profile-images/{storagePath}` via `EnhancedStorageProxyMiddleware`, adding strong cache headers and removing the need for public blobs.

## Steps
1. Enable API proxy (no deploy downtime)
   - Set `Storage:ProxyBlobRequests=true` (or env `Storage__ProxyBlobRequests=true`).
   - Ensure `AppBaseUrl` points to the public API base (e.g., `https://api.aiprofilephotomaker.com`).
   - Redeploy API. The API will return proxied URLs automatically.

2. Validate
   - Upload images; confirm UI image URLs point to `…/profile-images/...` and load correctly.
   - Check response headers include `Cache-Control: public, max-age=31536000, immutable`.

3. Lock down storage
   - Update infra to disable public blob access and container ACLs:
     - `infrastructure/simple-deploy.bicep`
       - At Storage Account: `allowBlobPublicAccess: false`.
       - For containers: set `publicAccess: 'None'` (instead of `'Blob'`).
   - Or via CLI (example):
     ```bash
     az storage container set-permission \
       --account-name <storageAccount> \
       --name profile-images \
       --public-access off
     ```

4. Re-deploy
   - Apply Bicep changes (GitHub workflow or `az deployment group create …`).
   - Verify that direct blob URLs 403/404 while API proxy continues to serve images.

## Notes & Compatibility
- Training ZIPs for Replicate still use short‑lived SAS; no change required.
- If any external links (emails, exports) used public blob URLs, switch to SAS or proxy.
- `style-previews` can remain public if desired; otherwise switch it to private and the proxy will serve from `/profile-images/style-previews/...`.
- Angular needs no changes; URLs come from the API.

## Rollback
- Re-enable public access (containers: `'Blob'`, account `allowBlobPublicAccess: true`) and set `Storage:ProxyBlobRequests=false`.
