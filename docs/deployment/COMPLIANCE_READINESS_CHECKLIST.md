# Compliance and Legal Readiness Checklist (GA Launch)

Last updated: 2025-12-19  
Scope: AI.ProfilePhotoMaker global baseline (US/UK/EU/CA). Not legal advice.

## Product data flow summary (from PRD + code)
- Users upload photos that are stored on the app server (local or Azure Blob storage).
- Custom model training and styled image generation use Replicate.
- Photo enhancement uses Replicate or OpenAI (gpt-image-1).
- API retention policy: original uploads deleted after 7 days; generated images deleted after 30 days (background job every 6 hours).
- User data controls: delete input photos, delete AI model, delete all data, delete account, and export data (JSON metadata only).

## Required legal pages (jurisdictional)
| ID | Page / Policy | Required where | Current location | Status | Evidence placeholder |
| --- | --- | --- | --- | --- | --- |
| LP-1 | Privacy Policy (data collection, purpose, retention, rights, third parties) | GDPR/UK GDPR, CCPA/CPRA | UI: `AI.ProfilePhotoMaker.UI/src/app/pages/privacy/privacy.component.ts`; Draft: `docs/deployment/evidence/legal/privacy-policy.md` | Updated in UI (needs legal review) | `docs/deployment/evidence/legal/privacy-policy.md` |
| LP-2 | Terms of Service (pricing, refunds, IP, user obligations) | Contract law | UI: `AI.ProfilePhotoMaker.UI/src/app/pages/terms/terms.component.ts`; Draft: `docs/deployment/evidence/legal/terms-of-service.md` | Updated in UI (needs legal review) | `docs/deployment/evidence/legal/terms-of-service.md` |
| LP-3 | Cookie Policy + consent banner | EU/UK ePrivacy | UI: `AI.ProfilePhotoMaker.UI/src/app/pages/cookies/cookies.component.ts`; Draft: `docs/deployment/evidence/legal/cookie-policy.md` | Updated in UI (consent pending) | `docs/deployment/evidence/legal/cookie-policy.md` |
| LP-4 | Biometric notice + consent + retention schedule | BIPA (IL), TX/Washington biometric laws (if applicable) | UI: `AI.ProfilePhotoMaker.UI/src/app/pages/biometric-consent/biometric-consent.component.ts` | Updated in UI (needs legal review) | `docs/deployment/evidence/legal/biometric-consent.md` |
| LP-5 | Children's privacy + age gate (COPPA) | US (if under-13 users) | UI: `AI.ProfilePhotoMaker.UI/src/app/pages/children-privacy/children-privacy.component.ts` | Updated in UI (needs legal review) | `docs/deployment/evidence/legal/children-privacy.md` |
| LP-6 | Subprocessor / third-party disclosure (Replicate, OpenAI, Stripe, storage) | GDPR/UK GDPR, CCPA/CPRA | UI: `AI.ProfilePhotoMaker.UI/src/app/pages/subprocessors/subprocessors.component.ts`; Draft: `docs/deployment/evidence/legal/subprocessors.md` | Updated in UI (needs legal review) | `docs/deployment/evidence/legal/subprocessors.md` |
| LP-7 | Data retention and deletion policy (public-facing) | GDPR/UK GDPR, BIPA | UI: `AI.ProfilePhotoMaker.UI/src/app/pages/retention-policy/retention-policy.component.ts`; settings page retention section | Updated in UI (needs legal review) | `docs/deployment/evidence/legal/retention-policy.md` |

## Recommended legal pages
| ID | Page / Policy | Rationale | Status | Evidence placeholder |
| --- | --- | --- | --- | --- |
| LP-R1 | AI transparency / AI-generated content disclosure | Emerging AI transparency expectations | Updated in UI (needs legal review) | `docs/deployment/evidence/legal/ai-transparency.md` |
| LP-R2 | Acceptable Use / Content Policy | User content controls and safety | Updated in UI (needs legal review) | `docs/deployment/evidence/legal/acceptable-use.md` |
| LP-R3 | Refund / cancellation policy | Paid + free tiers | Updated in UI (needs legal review) | `docs/deployment/evidence/legal/refund-policy.md` |
| LP-R4 | Security / Trust page | User assurance and disclosures | Updated in UI (needs legal review) | `docs/deployment/evidence/legal/security.md` |
| LP-R5 | IP / DMCA policy | User content rights and takedowns | Updated in UI (needs legal review) | `docs/deployment/evidence/legal/ip-dmca.md` |

## Compliance controls checklist
| ID | Control | Evidence | Status | Evidence placeholder |
| --- | --- | --- | --- | --- |
| CC-1 | Data flow inventory and purpose limitation | PRD data flow summary | Updated in docs (needs validation) | `docs/deployment/evidence/compliance/data-flow.md` |
| CC-2 | Lawful basis + biometric consent capture | Consent UI gating + local storage | Updated in UI (server-side record pending) | `docs/deployment/evidence/compliance/biometric-consent-flow.md` |
| CC-3 | Retention enforcement (7/30 days) | API policy and background job | Partial (runtime evidence pending) | `docs/deployment/evidence/retention-policy.json` |
| CC-4 | Third-party retention alignment (Replicate) | No public disclosure | Partial (predictions + privacy policy + terms; training retention pending) | `docs/deployment/evidence/compliance/third-party-retention.md` |
| CC-5 | DSAR workflow (access/delete/export) | API endpoints exist | Partial (export metadata only, documented) | `docs/deployment/evidence/compliance/dsar.md` |
| CC-6 | Age gate / parental consent | UI age gate + policy | Implemented in UI + API validation | `docs/deployment/evidence/compliance/age-gate.md` |
| CC-7 | Cookie consent + preference management | Implemented banner + stored preferences | Updated in UI (no analytics gated yet) | `docs/deployment/evidence/compliance/cookie-consent.md` |
| CC-8 | AI transparency labeling or provenance | No disclosure or metadata | Updated in UI (disclosure page) | `docs/deployment/evidence/compliance/ai-transparency.md` |
| CC-9 | Security disclosure (encryption, access control, monitoring) | No public documentation | Updated in UI + security docs | `docs/deployment/evidence/compliance/security-controls.md` |

## Gap summary (top issues)
1. Third-party retention alignment needs training retention confirmation (CC-4).
2. DSAR export is metadata only (CC-5).
3. Retention enforcement runtime evidence pending (CC-3).

## Evidence placeholders (store under docs/deployment/evidence)
- `docs/deployment/evidence/legal/privacy-policy.md`
- `docs/deployment/evidence/legal/terms-of-service.md`
- `docs/deployment/evidence/legal/cookie-policy.md`
- `docs/deployment/evidence/legal/biometric-consent.md`
- `docs/deployment/evidence/legal/children-privacy.md`
- `docs/deployment/evidence/legal/subprocessors.md`
- `docs/deployment/evidence/legal/retention-policy.md`
- `docs/deployment/evidence/legal/ai-transparency.md`
- `docs/deployment/evidence/legal/acceptable-use.md`
- `docs/deployment/evidence/legal/refund-policy.md`
- `docs/deployment/evidence/legal/security.md`
- `docs/deployment/evidence/legal/ip-dmca.md`
- `docs/deployment/evidence/compliance/data-flow.md`
- `docs/deployment/evidence/compliance/biometric-consent-flow.md`
- `docs/deployment/evidence/compliance/third-party-retention.md`
- `docs/deployment/evidence/compliance/dsar.md`
- `docs/deployment/evidence/compliance/age-gate.md`
- `docs/deployment/evidence/compliance/cookie-consent.md`
- `docs/deployment/evidence/compliance/ai-transparency.md`
- `docs/deployment/evidence/compliance/security-controls.md`
