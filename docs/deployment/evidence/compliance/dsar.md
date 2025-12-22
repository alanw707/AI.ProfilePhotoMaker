# DSAR Workflow Evidence

Last updated: 2025-12-20  
Status: Implemented (export metadata only).

## 1. User self-service (UI)
- Settings page allows users to export data, delete photos, delete models, delete all data, or delete their account.

## 2. API endpoints
- `GET /api/profile/data/export`
- `DELETE /api/profile/data/photos`
- `DELETE /api/profile/data/model`
- `DELETE /api/profile/data/all`
- `DELETE /api/profile/account`

## 3. Export scope
- Export includes account metadata, usage logs, and image metadata.
- Export does not include photo binaries; users must download images individually.

## Evidence references
- API implementation: `AI.ProfilePhotoMaker.API/Controllers/ProfileController.cs`
- UI settings: `AI.ProfilePhotoMaker.UI/src/app/pages/settings/settings.component.html`
