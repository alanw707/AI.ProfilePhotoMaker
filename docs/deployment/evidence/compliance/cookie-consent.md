# Cookie Consent Banner (Implementation Notes)

Last updated: 2025-12-19  
Status: Not doing (analytics deferred); consent evidence captured.

## Goal
Provide a consent banner for non-essential cookies (EU/UK ePrivacy + GDPR), while keeping essential cookies available for authentication and security.

## Implemented Behavior
- Banner shows on first visit when no consent is stored.
- Choices: Accept all, Reject non-essential, Manage preferences.
- Consent decision (category + timestamp) stored in local storage.
- "Cookie Preferences" button allows updating selections after initial consent.
- Analytics/marketing scripts can be gated using the consent state.

## Cookie Categories
- **Strictly necessary:** authentication, security, session state. Always enabled.
- **Preferences:** optional UI settings (if added).
- **Analytics:** usage tracking (if added).
- **Marketing:** ads/retargeting (not planned).

## Implementation Notes (Angular)
- `CookieConsentService` persists consent state to local storage.
- `CookieConsentComponent` renders banner and preferences modal in the app shell.
- Preferences are stored per category (preferences, analytics, marketing).
- Region-specific enforcement can be layered on top if needed.

## Code References
- `AI.ProfilePhotoMaker.UI/src/app/services/cookie-consent.service.ts`
- `AI.ProfilePhotoMaker.UI/src/app/components/shared/cookie-consent/cookie-consent.component.ts`
- `AI.ProfilePhotoMaker.UI/src/app/app.component.html`

## Evidence captured
- Capture date: 2025-12-23 (production UI).
- Screenshot of banner: `docs/deployment/evidence/cookie-consent-banner.png`.
- Screenshot of preferences modal: `docs/deployment/evidence/cookie-consent-preferences.png`.
- JSON snapshot of consent state: `docs/deployment/evidence/compliance/cookie-consent-state.json`.
- List of scripts gated by consent (if/when analytics added).
