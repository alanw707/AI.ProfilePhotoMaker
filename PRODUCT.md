# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

The primary buyer is an individual professional who needs one excellent LinkedIn or profile photo quickly. They use the product independently, often from a phone, and need confidence that a paid package is being fulfilled without learning image-generation terminology or internal credit accounting.

## Product Purpose

AI Profile Photo Maker turns one clear source photo into a guided professional profile-photo package. Success means the user can assess source quality, generate the candidates included in their package, identify the strongest result, make restrained improvements, and export correctly sized files without confusion about what remains.

## Positioning

The product sells a guided professional-photo outcome rather than raw AI generation volume: source scoring, a deliberately small candidate set, best-shot recommendation, controlled refinement, and platform-ready exports form one package-fulfillment journey.

## Operating Context

Users upload one source photo, review its quality score, select a professional portrait style and use case, generate a Free Preview or paid candidate set, compare candidates, adjust or refine the selected result, and download a platform export kit. Paid users may return from Stripe checkout to a promoted Free Preview that counts as candidate one. Work can be interrupted by payment redirects, generation latency, mobile app switching, or a later return to the workspace.

## Capabilities and Constraints

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

The existing product name is **AI Profile Photo Maker**. Current product truth favors professional readiness, user control, privacy, and explainable guidance. No testimonial, customer-logo, benchmark, or broader visual-brand claim is established; future work must not fabricate one.

## Evidence on Hand

- `CONTEXT.md` defines the current domain model, package semantics, terminology, and product funnel.
- Existing professional portrait and before/after assets live under `AI.ProfilePhotoMaker.UI/src/assets/marketing/`.
- Existing brand assets include `AI.ProfilePhotoMaker.UI/src/assets/Logo.PNG`, `og-image.png`, and social-card assets.
- Existing implementation and package-state behavior live in `AI.ProfilePhotoMaker.UI/src/app/components/photo-enhancement/`.
- Existing Playwright flows under `AI.ProfilePhotoMaker.API/tests/playwright/tests/` provide mockable generation, entitlement, and export evidence.
- No verified testimonials, customer logos, conversion benchmarks, or external endorsements are present.

## Product Principles

1. Fulfillment before features: always show what the package owes the user and the next action that advances it.
2. One professional outcome, not generation volume: help users choose and export their best photo.
3. Allowances stay legible: candidates, refinements, augmentations, and exports never blur together.
4. Guidance earns trust: explain scores, gates, progress, and recovery in plain language.
5. Mobile interruption is normal: preserve context and make resumption obvious.

## Accessibility & Inclusion

The primary flow must be keyboard-operable, screen-reader understandable, visibly focused, and WCAG AA for text contrast. Mobile controls require at least 44×44 CSS-pixel targets, layouts must tolerate 200% zoom and narrow viewports without horizontal overflow, and status changes during scoring, generation, errors, and success must be announced without relying on color alone.
