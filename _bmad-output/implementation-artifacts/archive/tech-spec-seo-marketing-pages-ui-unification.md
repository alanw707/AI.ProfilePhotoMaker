# Tech-Spec: SEO Marketing Pages UI Unification & Navigation

**Created:** 2025-12-30
**Status:** Complete

## Overview

### Problem Statement

Public SEO marketing pages are visually and structurally detached from the core site. They render a separate header/menu and styling, which makes them feel like a different product and hides them from human users. Several public/legal pages do not share any footer, and the examples page uses placeholder content that feels unfinished. We need to consolidate public pages into a cohesive marketing surface that matches the existing brand and theme without redesigning the homepage.

### Solution

Create shared public-facing marketing header/footer components and apply them across all SEO marketing routes (and pricing), while keeping the homepage layout unchanged. The marketing header must include the required top-level nav items (Features, Pricing, Reviews, Login/Logout) and expose the SEO routes via dropdowns. Add a new `/reviews` marketing route for testimonials. Refresh the Examples page "before/after" section with designed placeholders that still feel intentional even without real photos. Attach a shared footer to legal pages.

### Scope (In/Out)

In scope:
- New shared `MarketingHeaderComponent` and `MarketingFooterComponent` for public pages.
- Replace the current SEO page header/footer with shared components.
- Use the marketing header on the pricing page.
- Add a `/reviews` route and content entry for testimonials.
- Add dropdowns in the marketing header to make all SEO routes navigable.
- Add the shared footer to all legal pages (footer only, no header).
- Redesign the Examples page "before/after" section to avoid placeholder visuals.

Out of scope:
- Redesigning or reworking the homepage layout/sections.
- SSR/prerendering or backend changes.
- New photo assets or actual before/after imagery.
- Changes to member-only routes under `/app`.

## Context for Development

### Codebase Patterns

- Angular 19 SPA with standalone components and route-based lazy loading.
- SEO routes are mapped to a single `SeoPageComponent` and data file.
- The SEO page currently hardcodes its own header and footer.
- App pages use `HeaderNavigationComponent`, which includes app-only links (dashboard, credits, etc.).
- Theme colors and typography live in `AI.ProfilePhotoMaker.UI/src/styles.sass` as CSS variables.
- Landing page has its own header/footer and should remain unchanged.
- Cookie preferences are opened via `CookieConsentService.requestPreferencesOpen()` (used on landing and cookies pages).

### Files to Reference

- `AI.ProfilePhotoMaker.UI/src/app/app.routes.ts`
- `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.html`
- `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.sass`
- `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.ts`
- `AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.data.ts`
- `AI.ProfilePhotoMaker.UI/src/app/shared/header-navigation/header-navigation.component.*`
- `AI.ProfilePhotoMaker.UI/src/app/pages/premium/premium.component.ts`
- `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html`
- `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.ts`
- `AI.ProfilePhotoMaker.UI/src/styles.sass`
- Legal pages in `AI.ProfilePhotoMaker.UI/src/app/pages/*`

### Technical Decisions

- Add new shared components:
  - `shared/marketing-header/marketing-header.component.*`
  - `shared/marketing-footer/marketing-footer.component.*`
- Use existing theme tokens (`--primary`, `--accent-primary`, etc.) and keep Inter/Poppins typography for consistency.
- Marketing header top-level nav: Features, Pricing, Reviews, Login/Logout.
- Features should be a dropdown exposing the SEO routes (How it Works, Examples, AI Headshot Generator, LinkedIn Headshots, Professional Headshots, Headshots for Job Search, Free Headshot Enhancer, Help, Compare pages).
- Add `/reviews` as a new public route backed by `SeoPageComponent` and `seoPages['reviews']` content.
- Replace the SEO page's header/footer blocks with the shared components.
- Update pricing page to use the new marketing header (not the app header).
- Legal pages get the marketing footer only; no header added.
- Preserve UTM query params for pricing when navigating from SEO pages (pass `utmParams` into the marketing header where needed).

## Implementation Plan

### Tasks

- [x] Audit all public routes and confirm nav taxonomy for the Features dropdown (ensure every SEO route is reachable).
- [x] Build `MarketingHeaderComponent`:
  - Auth-aware Login/Logout (use `AuthService.isAuthenticated$`).
  - Dropdown under Features (desktop) and accordion grouping (mobile).
  - Use theme tokens for colors/backgrounds, match landing visual language.
  - Optionally accept `queryParams` input to preserve UTM when linking to pricing.
  - Accessibility: `aria-expanded`, `aria-controls`, keyboard focus behavior.
- [x] Build `MarketingFooterComponent`:
  - Base on landing footer structure and links.
  - Include cookie preferences button (use `CookieConsentService.requestPreferencesOpen()`).
  - Use theme tokens and keep legal link list consistent.
- [x] Update `SeoPageComponent`:
  - Replace `.seo-header` and `.seo-footer` blocks with `<app-marketing-header>` and `<app-marketing-footer>`.
  - Pass UTM params into header if needed for pricing link.
  - Remove or refactor now-unused SEO header/footer styles.
- [x] Add `/reviews` route in `app.routes.ts` and create `seoPages['reviews']` entry with a testimonials section.
- [x] Update `seo-pages.data.ts` for the Examples page:
  - Replace current placeholder "before/after" visuals with designed, non-photo mock visuals (e.g., gradient portrait cards, silhouette blocks, or branded frame placeholders) so the section feels intentional without real images.
- [x] Update `premium.component.ts` to use `MarketingHeaderComponent` for the pricing page.
- [x] Append `MarketingFooterComponent` to all legal page templates and import the component in their standalone declarations.
- [x] QA all public pages for nav presence, dropdown behavior, and theme alignment.

### Review Follow-ups (AI)

- [x] [AI-Review][HIGH] Update Product label to Features per direction; no further nav consolidation requested. [AI.ProfilePhotoMaker.UI/src/app/shared/marketing-header/marketing-header.component.html:35]
- [x] [AI-Review][HIGH] Remove marketing header from legal pages (footer-only requirement). [AI.ProfilePhotoMaker.UI/src/app/pages/privacy/privacy.component.ts:13]
- [x] [AI-Review][HIGH] Revert landing page header/footer changes to keep homepage layout unchanged (left in place per request). [AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html:38]
- [x] [AI-Review][HIGH] Review examples layout with existing photos; no further layout changes required. [AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.data.ts:222]
- [x] [AI-Review][MEDIUM] Add Dev Agent Record with full File List covering all git changes. [_bmad-output/implementation-artifacts/tech-spec-seo-marketing-pages-ui-unification.md:149]
- [x] [AI-Review][MEDIUM] Fix Twitter card meta tags to use `name=` instead of `property=` in static generator. [AI.ProfilePhotoMaker.UI/scripts/generate-seo-static-pages.cjs:61]
- [x] [AI-Review][MEDIUM] Update UI navigation smoke test to match footer link elements (FAQ/Pricing). [tests/e2e/ui-navigation-smoke.spec.js:242]
- [x] [AI-Review][MEDIUM] Make static compare test read from the generator output path (dist vs public) or enforce a single output. [tests/e2e/seo-compare-static.spec.js:4]
- [x] [AI-Review][LOW] Remove `Zone.Identifier` files and add ignore rule to prevent reintroduction. [AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/casual_2.png:Zone.Identifier:1]

### Acceptance Criteria

- [x] All SEO routes render the shared marketing header with Features/Pricing/Reviews/Login-Logout; the old SEO header is gone.
- [x] Pricing page uses the marketing header (no app-only navigation items).
- [x] Legal pages include the marketing footer (footer-only, no header).
- [x] `/reviews` route exists, is linked in the header, and has unique SEO metadata and H1.
- [x] Examples page shows a designed before/after section without blank placeholders.
- [x] All public pages respect current theme colors and typography.
- [x] Features dropdown provides access to all SEO routes.

## Additional Context

### Dependencies

- UI-only changes; no backend work required.

### Testing Strategy

- Manual verification: open each public route, confirm marketing header/footer presence and visual alignment.
- Manual check: verify Login/Logout toggles based on auth state.
- Optional: add or update Playwright smoke checks for header/footer presence on SEO and legal pages.

### Notes

- Keep homepage layout unchanged to preserve the current top-level experience.
- Avoid introducing new color palettes; use existing CSS variables and shared mixins.
- Dropdown behavior should be accessible and keyboard-friendly.
- Preserve SEO metadata and canonical handling in `SeoPageComponent`.

## Progress Update (2025-12-31)

- Implemented shared marketing header/footer components and wired them into SEO pages and pricing.
- Added `/reviews` route + content; refreshed Examples “before/after” visuals with styled placeholders.
- Added marketing footer to all legal pages.
- Added static SEO compare pages (`/compare/aragon-ai`, `/compare/headshotpro`) and Playwright smoke test for their metadata.
- Added header accessibility polish (outside click/focus/escape close; focus-visible states; aria attributes).
- Docker-first local validation rule captured in `AGENTS.md`.
- Docker rebuild + local Playwright run completed; 11 passed, 10 skipped, 3 failed in `tests/e2e` (footer section lookup + 2 auth-dependent tests).

## Progress Update (2026-01-04)

- Marked tech spec complete after PR merge and QA confirmation.

## Dev Agent Record

### File List

- .gitignore
- AGENTS.md
- AI.ProfilePhotoMaker.UI/Dockerfile
- AI.ProfilePhotoMaker.UI/package.json
- AI.ProfilePhotoMaker.UI/public/sitemap.xml
- AI.ProfilePhotoMaker.UI/scripts/generate-seo-static-pages.cjs
- AI.ProfilePhotoMaker.UI/src/app/app.component.ts
- AI.ProfilePhotoMaker.UI/src/app/app.config.ts
- AI.ProfilePhotoMaker.UI/src/app/app.routes.ts
- AI.ProfilePhotoMaker.UI/src/app/auth/login/login.component.ts
- AI.ProfilePhotoMaker.UI/src/app/auth/verify-email/verify-email.component.ts
- AI.ProfilePhotoMaker.UI/src/app/components/credit-packages/credit-packages.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/acceptable-use/acceptable-use.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/ai-transparency/ai-transparency.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/biometric-consent/biometric-consent.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/children-privacy/children-privacy.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/cookies/cookies.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/ip-dmca/ip-dmca.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.html
- AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.sass
- AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.html
- AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.sass
- AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-page/seo-page.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/marketing/seo-pages.data.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/premium/premium.component.sass
- AI.ProfilePhotoMaker.UI/src/app/pages/premium/premium.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/privacy/privacy.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/refund-policy/refund-policy.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/retention-policy/retention-policy.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/security/security.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/subprocessors/subprocessors.component.ts
- AI.ProfilePhotoMaker.UI/src/app/pages/terms/terms.component.ts
- AI.ProfilePhotoMaker.UI/src/app/services/auth.service.ts
- AI.ProfilePhotoMaker.UI/src/app/shared/styles/_mixins.sass
- AI.ProfilePhotoMaker.UI/src/styles.sass
- _bmad-output/implementation-artifacts/marketing-execution-playbook-2025-12.md
- _bmad-output/implementation-artifacts/tech-spec-seo-indexing-marketing-pages.md
- nginx.conf
- scripts/dev-email-verification-smoke.sh
- tests/e2e/account-management-smoke.spec.js
- tests/e2e/auth-invalid-login.spec.js
- tests/e2e/image-upload-validation.spec.js
- tests/e2e/pricing-scroll.spec.js
- tests/e2e/support-responsive.spec.js
- tests/e2e/ui-navigation-smoke.spec.js
- AI.ProfilePhotoMaker.UI/public/ai-headshot-generator/index.html
- AI.ProfilePhotoMaker.UI/public/compare/aragon-ai/index.html
- AI.ProfilePhotoMaker.UI/public/compare/headshotpro/index.html
- AI.ProfilePhotoMaker.UI/public/examples/index.html
- AI.ProfilePhotoMaker.UI/public/features/index.html
- AI.ProfilePhotoMaker.UI/public/free-headshot-enhancer/index.html
- AI.ProfilePhotoMaker.UI/public/headshots-for-job-search/index.html
- AI.ProfilePhotoMaker.UI/public/help/index.html
- AI.ProfilePhotoMaker.UI/public/how-it-works/index.html
- AI.ProfilePhotoMaker.UI/public/linkedin-headshots/index.html
- AI.ProfilePhotoMaker.UI/public/professional-headshots/index.html
- AI.ProfilePhotoMaker.UI/public/reviews/index.html
- AI.ProfilePhotoMaker.UI/src/app/shared/marketing-footer/marketing-footer.component.html
- AI.ProfilePhotoMaker.UI/src/app/shared/marketing-footer/marketing-footer.component.sass
- AI.ProfilePhotoMaker.UI/src/app/shared/marketing-footer/marketing-footer.component.ts
- AI.ProfilePhotoMaker.UI/src/app/shared/marketing-header/marketing-header.component.html
- AI.ProfilePhotoMaker.UI/src/app/shared/marketing-header/marketing-header.component.sass
- AI.ProfilePhotoMaker.UI/src/app/shared/marketing-header/marketing-header.component.ts
- AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/set-1-after.png
- AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/set-1-before.jpg
- AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/set-2-after.png
- AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/set-2-before.jpg
- AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/set-3-after.png
- AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/set-3-before.jpg
- _bmad-output/implementation-artifacts/tech-spec-seo-marketing-pages-ui-unification.md
- scripts/dev-start-docker-local.sh
- tests/e2e/seo-compare-static.spec.js
- tests/e2e/setup/app-url.js
