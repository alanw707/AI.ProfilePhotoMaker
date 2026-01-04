# Tech-Spec: SEO Indexing and Marketing Pages

**Created:** 2025-12-30
**Status:** Ready for Development

## Overview

### Problem Statement

The app is an Angular SPA with limited SEO coverage: several public routes reuse the landing component and end up with the same canonical, H1, and JSON-LD, which dilutes indexability. Robots and sitemap files still point at the apex domain while the canonical domain is `app.aiprofilephotomaker.com`. We also need to ship the playbook's SEO pages (pillar + cluster) and draft on-page content so Google and AI answer engines can index and surface the site.

### Solution

Create a dedicated SEO marketing surface on the `app` domain: add public SEO pages with unique copy, meta, and structured data; align discovery files (robots/sitemap/llms/ai) to `app`; fix canonical logic so each public route gets its own canonical; and add internal linking + CTA tracking. Use the playbook messaging: "Studio-quality headshots in minutes" and CTA "Get your headshot in minutes."

### Scope (In/Out)

In scope:
- Public marketing pages: `/how-it-works`, `/examples`, `/free-headshot-enhancer`, `/ai-headshot-generator`, `/linkedin-headshots`, `/professional-headshots`, `/headshots-for-job-search`, `/compare/aragon-ai`, `/compare/headshotpro`.
- SEO hygiene: route-specific titles/descriptions/canonicals, OpenGraph/Twitter, JSON-LD, internal linking.
- Discovery files: `robots.txt`, `sitemap.xml`, `llms.txt`, `ai.txt`, `.well-known/security.txt` alignment to `app`.
- Legal/compliance page review and updates (content accuracy + meta hygiene).
- Tracking updates for CTA clicks on new pages.

Out of scope (unless explicitly requested):
- Full SSR/Universal migration.
- Major UI redesign of the core app experience.
- Paid campaign execution or ad creative production.

## Context for Development

### Codebase Patterns

- Angular 19 SPA with standalone components and route-based lazy loading in `AI.ProfilePhotoMaker.UI/src/app/app.routes.ts`.
- SEO currently set in `landing.component.ts` and `premium.component.ts` via Meta/Title and JSON-LD injection.
- `app.component.ts` sets canonical on every navigation using the current origin and URL.
- Public assets are served from `AI.ProfilePhotoMaker.UI/public` and `AI.ProfilePhotoMaker.UI/src/robots.txt`.
- Nginx SPA fallback in `AI.ProfilePhotoMaker.UI/Dockerfile`.

### Files to Reference

- `AI.ProfilePhotoMaker.UI/src/app/app.routes.ts`
- `AI.ProfilePhotoMaker.UI/src/app/app.component.ts`
- `AI.ProfilePhotoMaker.UI/src/app/pages/landing/landing.component.ts`
- `AI.ProfilePhotoMaker.UI/src/app/pages/premium/premium.component.ts`
- `AI.ProfilePhotoMaker.UI/public/robots.txt`
- `AI.ProfilePhotoMaker.UI/src/robots.txt`
- `AI.ProfilePhotoMaker.UI/public/sitemap.xml`
- `AI.ProfilePhotoMaker.UI/public/.well-known/security.txt`
- `AI.ProfilePhotoMaker.UI/src/index.html`

### Technical Decisions

- Canonical domain is `https://app.aiprofilephotomaker.com` (apex redirects to app).
- Public SEO pages must have unique H1, title, description, canonical, and JSON-LD.
- Replace landing-component reuse for `/features`, `/examples`, `/help` with dedicated SEO pages or route-aware SEO logic to avoid duplicate canonicals.
- Add `llms.txt` and `ai.txt` at the app root to support AI indexing best practices.
- Keep SPA for now; consider build-time prerendering as a future enhancement if indexing is insufficient.

## Implementation Plan

### Tasks

- [x] Normalize SEO discovery files to `app` domain.
  - [x] Remove or consolidate duplicate `robots.txt` (choose one source of truth).
  - [x] Update `robots.txt` to allow public routes, disallow `/app`, `/auth`, `/admin`, and point to `https://app.aiprofilephotomaker.com/sitemap.xml`.
  - [x] Update `public/sitemap.xml` URLs to `app` domain; remove auth routes; include new SEO pages.
  - [x] Update `.well-known/security.txt` canonical to `app` domain.
- [x] Add AI indexing files in `AI.ProfilePhotoMaker.UI/public`:
  - [x] `llms.txt` with a brief product summary, allowed usage, key URLs, contact.
  - [x] `ai.txt` with AI policy summary and canonical URL references.
- [x] Implement SEO page architecture:
  - [x] Add dedicated components for new marketing pages listed in scope.
  - [x] Ensure each component sets title, description, canonical, OpenGraph/Twitter, and JSON-LD (WebPage + page-specific schema).
  - [x] Fix landing SEO so `/features`, `/examples`, `/help` get unique canonicals and metadata (or replace with dedicated pages).
  - [x] Add internal linking across pillar/cluster pages and footer/nav updates.
- [x] Draft and publish page copy from "Content Drafts" below.
- [x] Update CTA tracking:
  - [x] Add GA4 events for primary CTA clicks on each SEO page.
  - [x] Preserve UTM parameters when navigating to `/pricing`.
- [x] Legal review:
  - [x] Review all legal pages for accuracy vs current product behavior.
  - [x] Update "Last updated" dates where content changes.
  - [x] Ensure legal pages keep `index, follow` and canonical to app domain.

### Acceptance Criteria

- [x] Given any public SEO route, when loaded in production, then the page has a unique title, description, canonical URL on `https://app.aiprofilephotomaker.com`, and a single H1 that matches the page topic.
- [x] Given `/robots.txt`, when fetched from `https://app.aiprofilephotomaker.com`, then it references the correct `sitemap.xml` and does not block the SEO pages.
- [x] Given `/sitemap.xml`, when fetched from `https://app.aiprofilephotomaker.com`, then it lists all SEO pages and excludes `/auth` and `/app` routes.
- [x] Given `llms.txt` and `ai.txt`, when fetched from `https://app.aiprofilephotomaker.com`, then they contain the product summary, allowed usage, and canonical URLs.
- [x] Given the new SEO pages, when navigating between them, then CTA links preserve UTM parameters and fire GA4 events.
- [x] Given comparison pages, when published, then all claims are verifiable or labeled as subjective and include a "verify pricing/features" note.

## Additional Context

### Dependencies

- No new backend changes required.
- Optional future: Angular prerender/SSR packages if needed for stronger indexing.

### Testing Strategy

- Manual: load each SEO page and verify title/description/canonical/H1/JSON-LD.
- Automated: Playwright smoke test checks metadata, canonicals, and JSON-LD for SEO routes plus `/robots.txt` and `/sitemap.xml` (`AI.ProfilePhotoMaker.UI/tests/seo-metadata-smoke.spec.ts`).
- Run `npm run lint` before shipping UI changes.

### Notes

- Current scan shows `/features`, `/examples`, and `/help` share the landing canonical and H1, which must be fixed for SEO.
- `public/robots.txt` and `src/robots.txt` both exist; unify to avoid deployment ambiguity.
- Use the playbook CTA: "Get your headshot in minutes."

## Progress Update (2025-12-31)

- Completed: static HTML generation for SEO routes (pre-build) to expose correct meta/canonical/JSON-LD for non-JS crawlers; build scripts updated to run the generator.
- Verification: Playwright SEO smoke suite expanded to validate server-rendered HTML; local and production runs passing.
- Deployment: PR #267 merged to main and production deploy succeeded.
- Next: monitor indexing and re-run the production smoke test after cache/CDN churn if needed.

#### Content Drafts (Appendix)

Use this copy as the baseline for the new pages. Keep sections concise and CTA consistent.

**/how-it-works**
- H1: How AI Profile Photo Maker Works
- Hero: Studio-quality headshots in minutes. Upload a photo, pick a style, get a professional result.
- Steps:
  1) Upload 8-12 clear selfies (front-facing + variety).
  2) Choose a style (LinkedIn, creative, classic).
  3) Receive a full set of polished headshots in minutes.
- Trust: Privacy-first processing, delete anytime, transparent retention.
- CTA: Get your headshot in minutes.

**/examples**
- H1: AI Headshot Examples (Before and After)
- Intro: Realistic, professional results tailored to you. Results vary based on lighting and input quality.
- Sections: Before/After grid, Style showcase, Testimonials.
- CTA: See your own results in minutes.

**/free-headshot-enhancer**
- H1: Free Headshot Enhancer
- Hero: Improve your existing profile photo fast with free weekly enhancement credits.
- Bullets: lighting fix, background cleanup, color correction, natural detail preservation.
- CTA: Try the free enhancer.

**/ai-headshot-generator** (pillar)
- H1: AI Headshot Generator for Professional Profiles
- Hero: Create studio-quality headshots for LinkedIn, resumes, and portfolios.
- Sections: Why AI headshots, How it works, Use cases, FAQ, CTA.
- CTA: Get your headshot in minutes.

**/linkedin-headshots** (pillar)
- H1: LinkedIn Headshots That Look Like You
- Hero: Professional, realistic headshots optimized for LinkedIn profiles.
- Sections: Ideal framing + background, outfit guidance, common mistakes, FAQ.
- CTA: Upgrade your LinkedIn photo.

**/professional-headshots** (pillar)
- H1: Professional Headshots Without the Studio
- Hero: Consistent, high-quality headshots for teams and individuals.
- Sections: Business use cases, realism focus, pricing link, FAQ.
- CTA: Create professional headshots.

**/headshots-for-job-search** (cluster)
- H1: Headshots for Job Seekers
- Hero: First impressions matter. Get a polished headshot before your next application.
- Sections: Why recruiters care, recommended styles, FAQ.
- CTA: Get job-ready headshots.

**/compare/aragon-ai**
- H1: AI Profile Photo Maker vs Aragon AI
- Intro: Compare workflows, turnaround expectations, and positioning. Verify current pricing/features before publishing.
- Sections: Quick comparison table, Best for, CTA.
- CTA: Try AI Profile Photo Maker.

**/compare/headshotpro**
- H1: AI Profile Photo Maker vs HeadshotPro
- Intro: Compare setup steps, output style, and pricing. Verify current pricing/features before publishing.
- Sections: Quick comparison table, Best for, CTA.
- CTA: Get your headshot in minutes.
