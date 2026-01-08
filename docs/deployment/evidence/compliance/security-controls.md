# Security Controls Disclosure Evidence

Last updated: 2025-12-20  
Status: Evidence captured (production).

## Evidence (production)
- URL: https://aiprofilephotomaker.com/legal/security
- Screenshot: docs/deployment/evidence/legal/security-production.png
- Captured: 2025-12-23T20:53:46Z

## 1. UI disclosure
- Legal page: `/legal/security`
- Component: `AI.ProfilePhotoMaker.UI/src/app/pages/security/security.component.ts`

## 2. Technical evidence
- Security review summary: `docs/security/SECURITY_REVIEW_SUMMARY.md`
- Security notes (prod diagnostics + logging hygiene): `AI.ProfilePhotoMaker.API/SECURITY_NOTES.md`
- Monitoring summary: `AI.ProfilePhotoMaker.API/MONITORING_SYSTEM_SUMMARY.md`

## 3. Notes
- This is a public-facing summary. Detailed configurations remain in internal docs.
