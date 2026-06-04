# Evidence Log: Compliance Controls (Production)

## Evidence Metadata
- Evidence ID: prod-cc-controls-2025-12-23
- Requirement ID (AC/PC): CC-1, CC-2, CC-3, CC-4, CC-5, CC-6, CC-7, CC-8, CC-9
- Environment: production
- Date: 2025-12-23T21:34:14Z
- Operator: Alan

## Inputs
- Request payloads: N/A (public pages + documented endpoints)
- Test user/account: N/A (public pages only)
- Preconditions: production UI reachable; cookie consent stored locally to avoid banner

## Commands Executed
- Playwright capture via Codex CLI (browser automation)

## Output Summary
- Expected result: Public compliance-related pages and disclosures load; evidence references recorded.
- Actual result: Public pages loaded and screenshots captured; remaining authenticated flows noted as pending.
- Pass/Fail: Partial (auth-required captures pending)

## Evidence Files
- `docs/deployment/evidence/compliance/age-gate-register-production.png`
- `docs/deployment/evidence/compliance/age-gate-login-production.png`
- `docs/deployment/evidence/compliance/biometric-consent-workspace-production.png`
- `docs/deployment/evidence/compliance/biometric-consent-enhance-production.png`
- `docs/deployment/evidence/compliance/dsar-settings-production.png`
- `docs/deployment/evidence/compliance/dsar-export-production.json`
- `docs/deployment/evidence/legal/biometric-consent-production.png`
- `docs/deployment/evidence/legal/ai-transparency-production.png`
- `docs/deployment/evidence/legal/security-production.png`
- `docs/deployment/evidence/compliance/cookie-consent-state.json`
- `docs/deployment/evidence/cookie-consent-banner.png`
- `docs/deployment/evidence/cookie-consent-preferences.png`
- `docs/deployment/evidence/retention-policy-production.json`
- `docs/deployment/evidence/retention-expired-images-production.json`
- `docs/deployment/evidence/retention-delete-expired-production.json`
- `docs/deployment/evidence/retention-background-production.json`

## Redactions
- Tokens/PII removed: not applicable (public content only)

## Notes
- Authenticated flows (biometric consent checkbox in upload/enhance; DSAR settings) captured via test account.
- Turnstile still blocks transform execution; evidence focuses on consent gating and export availability.
- Third-party retention confirmation for training data remains pending provider response.
