# Biometric Consent Flow (Implementation Notes)

Last updated: 2025-12-23
Status: Evidence captured (production).

## Evidence (production)
- Legal disclosure URL: https://app.aiprofilephotomaker.com/legal/biometric-consent
- Screenshot: docs/deployment/evidence/legal/biometric-consent-production.png
- Dashboard upload consent: docs/deployment/evidence/compliance/biometric-consent-dashboard-production.png
- Photo transform consent: docs/deployment/evidence/compliance/biometric-consent-enhance-production.png
- Captured: 2025-12-23T21:34:14Z

## Where Consent Is Collected
- Training upload flow (Dashboard > Upload Selfies).
- Photo enhancement flow (Transform Photo).

## Behavior
- Users must check the consent checkbox before uploading or transforming photos.
- Consent is stored in local storage (`biometric-consent-v1`) with a timestamp.
- Upload/transform actions are disabled until consent is granted.

## Code References
- `AI.ProfilePhotoMaker.UI/src/app/services/biometric-consent.service.ts`
- `AI.ProfilePhotoMaker.UI/src/app/components/dashboard/file-upload-section/file-upload-section.component.ts`
- `AI.ProfilePhotoMaker.UI/src/app/components/dashboard/file-upload-section/file-upload-section.component.html`
- `AI.ProfilePhotoMaker.UI/src/app/components/photo-enhancement/photo-enhancement.component.ts`
- `AI.ProfilePhotoMaker.UI/src/app/components/photo-enhancement/photo-enhancement.component.html`
- `AI.ProfilePhotoMaker.UI/src/app/pages/biometric-consent/biometric-consent.component.ts`
