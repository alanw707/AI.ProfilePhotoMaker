# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

**Career pivoters (primary, confirmed 2026-10-07).** Experienced U.S. professionals exploring a next role or a change of field. They arrive unsure which direction is realistic, and they want evidence, not encouragement: what the target work is, what it pays where, what is missing from their background, and what to do this week. They come back over weeks, often in short sessions, and must be able to see where they left off.

**Photo buyers (supporting).** The photo buyer is an individual professional who needs one excellent LinkedIn or profile photo quickly. They use the product independently, often from a phone, and need confidence that a paid package is being fulfilled without learning image-generation terminology or internal credit accounting.

## Product Purpose

AI Profile Photo Maker is a career-pivot workspace. A user confirms their background, sets a target, sees the occupation, market and pay evidence for it, chooses a roadmap, prepares targeted materials, and optionally gets a professional photo for the new role. Success means the user leaves each session knowing their goal, their next step and what changed, with every fact they rely on traceable to its source or their own confirmation.

The professional photo is one tool inside that journey and is also sold on its own. The photo workflow turns one clear source photo into a guided professional profile-photo package. Success means the user can assess source quality, generate the candidates included in their package, identify the strongest result, make restrained improvements, and export correctly sized files without confusion about what remains.

## Positioning

A personal research workspace for changing careers, not a chatbot and not a job board: the user's confirmed facts and public labor-market evidence stay in front, the assistant drafts against them, and nothing it writes is treated as true until the user confirms it. The photo package is the one paid, finished artifact the workspace can hand over today.

The photo product sells a guided professional-photo outcome rather than raw AI generation volume: source scoring, a deliberately small candidate set, best-shot recommendation, controlled refinement, and platform-ready exports form one package-fulfillment journey.

## Operating Context

Users upload one source photo, review its quality score, select a professional portrait style and use case, generate a Free Preview or paid candidate set, compare candidates, adjust or refine the selected result, and download a platform export kit. Paid users may return from Stripe checkout to a promoted Free Preview that counts as candidate one. Work can be interrupted by payment redirects, generation latency, mobile app switching, or a later return to the workspace.

## Capabilities and Constraints

### Career workspace
- Behind the `Features:CareerWorkspace` flag; with the flag off, no career surface, link or background work may appear.
- Journey: profile (manual or resume import) → goal → occupation match → market brief → pay analysis → market comparison → job observations → roadmap and tracking → targeted resume and summary → export → optional photo handoff.
- The assistant runs on a monthly drafting allowance (currently 20 runs, provisional policy). Saved work stays readable, editable and exportable when the allowance is used up.
- Profile and goal are versioned; a change marks dependent analysis stale rather than silently rewriting it.
- Evidence comes from O*NET, BLS (OEWS and projections) and USAJOBS snapshots. Personalized advertised-pay estimates are not approved and must show as unavailable.
- Users can export and delete all career data; deletion survives backup restores.
- No mass applications, no screenshot of third-party user data, no financial advice, no photo-based career scoring.
- Career pricing is **not decided**. Do not invent a career price, plan or paywall.

### Photo packages

- Free Preview includes one watermarked candidate and no platform export kit.
- Starter Package includes three candidates, best-shot selection, basic adjustment, refinements, and platform exports.
- Pro Package includes nine candidates, best-shot selection, score delta, basic adjustment, refinements, premium augmentations, platform exports, and extra role or vibe attempts.
- A promoted Free Preview becomes candidate one after purchase; only remaining candidate slots are generated.
- Candidate generation, refinements, premium augmentations, and export availability are separate package allowances and must never be presented or consumed interchangeably.
- The public default experience is the instant-headshot workflow. Replicate custom-model training is a hidden legacy/fallback capability.
- User-facing language uses outcome packages and package fulfillment, not the internal credit ledger.
- Biometric consent, email verification, quality gates, retention messaging, and existing trust-boundary validation remain required.
- The application is an Angular 19 SPA backed by an ASP.NET Core API, with Stripe checkout and Azure/local storage.

## Brand Commitments

The existing product name is **AI Profile Photo Maker** and stays for now; only the positioning changes (a rename would touch SEO, domain and email, and is an open decision). Current product truth favors professional readiness, user control, privacy, and explainable guidance. No testimonial, customer-logo, benchmark, or broader visual-brand claim is established; future work must not fabricate one.

## Evidence on Hand

- `GLOSSARY.md` defines the current domain model, package semantics, terminology, and product funnel.
- Existing professional portrait and before/after assets live under `AI.ProfilePhotoMaker.UI/src/assets/marketing/`.
- Existing brand assets include `AI.ProfilePhotoMaker.UI/src/assets/Logo.PNG`, `og-image.png`, and social-card assets.
- Existing implementation and package-state behavior live in `AI.ProfilePhotoMaker.UI/src/app/components/photo-enhancement/`.
- Existing Playwright flows under `AI.ProfilePhotoMaker.API/tests/playwright/tests/` provide mockable generation, entitlement, and export evidence.
- No verified testimonials, customer logos, conversion benchmarks, or external endorsements are present.

## Product Principles

Career workspace:

1. Evidence before advice: show the source, its date and its gaps next to every claim.
2. The user confirms; the assistant drafts. Nothing unconfirmed is presented as fact.
3. Always show the goal, the next step and what is stale.
4. Return is the normal case: every visit starts from where the user left off.
5. The photo is optional and never gates the career work.

Photo packages:

1. Fulfillment before features: always show what the package owes the user and the next action that advances it.
2. One professional outcome, not generation volume: help users choose and export their best photo.
3. Allowances stay legible: candidates, refinements, augmentations, and exports never blur together.
4. Guidance earns trust: explain scores, gates, progress, and recovery in plain language.
5. Mobile interruption is normal: preserve context and make resumption obvious.

## Accessibility & Inclusion

The primary flow must be keyboard-operable, screen-reader understandable, visibly focused, and WCAG AA for text contrast. Mobile controls require at least 44×44 CSS-pixel targets, layouts must tolerate 200% zoom and narrow viewports without horizontal overflow, and status changes during scoring, generation, errors, and success must be announced without relying on color alone.
