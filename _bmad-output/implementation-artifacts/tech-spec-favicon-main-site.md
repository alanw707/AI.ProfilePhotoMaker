# Tech-Spec: Main Site Favicon Refresh

**Created:** 2025-12-24
**Status:** Completed

## Overview

### Problem Statement
The main site (`app.aiprofilephotomaker.com`) still shows the default Angular favicon on some devices. The favicon appears inconsistent across devices, likely due to aggressive caching of the existing icon assets.

### Solution
Regenerate the full favicon and app icon set from the current logo, replace the existing public assets, and add cache-busting to the icon references (while keeping the build pipeline unchanged). Ensure both HTML link tags and the web manifest point to the new assets.

### Scope (In/Out)

**In scope**
- Update favicon and app icon assets under `AI.ProfilePhotoMaker.UI/public/`.
- Update favicon references in `AI.ProfilePhotoMaker.UI/src/index.html`.
- Update icon URLs in `AI.ProfilePhotoMaker.UI/public/manifest.json`.
- Keep `/favicon.ico` updated as a fallback for browsers that auto-request it.

**Out of scope**
- Backend/API changes.
- CDN/cache-control changes or infrastructure updates.
- Other sites or domains outside the main app.

## Context for Development

### Codebase Patterns
- Angular app with static assets served from `AI.ProfilePhotoMaker.UI/public/` per `AI.ProfilePhotoMaker.UI/angular.json` assets config.
- Favicon and touch icon links live in `AI.ProfilePhotoMaker.UI/src/index.html`.
- PWA icons are defined in `AI.ProfilePhotoMaker.UI/public/manifest.json`.

### Files to Reference
- `AI.ProfilePhotoMaker.UI/public/Logo.PNG` (source logo)
- `AI.ProfilePhotoMaker.UI/public/favicon.ico`
- `AI.ProfilePhotoMaker.UI/public/favicon-16x16.png`
- `AI.ProfilePhotoMaker.UI/public/favicon-32x32.png`
- `AI.ProfilePhotoMaker.UI/public/favicon.svg`
- `AI.ProfilePhotoMaker.UI/public/apple-touch-icon.png`
- `AI.ProfilePhotoMaker.UI/public/android-chrome-192x192.png`
- `AI.ProfilePhotoMaker.UI/public/android-chrome-512x512.png`
- `AI.ProfilePhotoMaker.UI/public/android-chrome-maskable-192x192.png`
- `AI.ProfilePhotoMaker.UI/public/android-chrome-maskable-512x512.png`
- `AI.ProfilePhotoMaker.UI/src/index.html`
- `AI.ProfilePhotoMaker.UI/public/manifest.json`

### Technical Decisions
- Use `Logo.PNG` as the source and generate new icons at: 16x16, 32x32, 180x180, 192x192, 512x512, plus multi-size `favicon.ico`.
- Keep `/favicon.ico` as a fallback for browsers that auto-request it.
- Add cache-busting to icon URLs (prefer versioned filenames or a `?v=YYYYMMDD` query) to reduce stale favicon caching.
- Add maskable icon variants with extra padding for Android PWA installs.
- Add an SVG favicon for modern browsers (with PNG fallback).
- No new build steps or dependencies; only static asset replacement and reference updates.

## Implementation Plan

### Tasks
- [x] Create a new branch (name TBD, e.g., `fix/ui-favicon-refresh`).
- [x] Generate a fresh favicon/app icon set from `AI.ProfilePhotoMaker.UI/public/Logo.PNG` in the required sizes.
- [x] Replace existing favicon and app icon files under `AI.ProfilePhotoMaker.UI/public/`.
- [x] Update `AI.ProfilePhotoMaker.UI/src/index.html` to reference the new icon filenames or add cache-busting query strings.
- [x] Update `AI.ProfilePhotoMaker.UI/public/manifest.json` icon URLs to match the new assets.

### Acceptance Criteria
- [ ] Browsers show the AI Profile Photo Maker logo favicon (not the Angular icon).
- [x] `index.html` references the updated icons and `/favicon.ico` remains valid.
- [x] `manifest.json` points to the updated icon assets.
- [ ] A hard refresh on desktop and a new tab on mobile display the correct favicon.

## Additional Context

### Dependencies
- None (use existing logo asset; no pipeline changes).

### Testing Strategy
- Manual: run the UI locally, hard-refresh, and verify favicon in a fresh tab.
- Post-deploy: validate favicon on production with a cache-busting hard refresh and a new tab on mobile.
- Optional: check application tab in DevTools to confirm manifest icon URLs resolve.

### Notes
- If cache-busting is done via versioned filenames, ensure both HTML and manifest are updated in lockstep.
- If the CDN caches `/favicon.ico`, invalidate or wait for TTL so the updated icon is served.

## Review Notes
- Adversarial review completed
- Findings: 10 total, 7 fixed, 3 skipped
- Resolution approach: auto-fix
