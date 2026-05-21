---
title: SEO Launch Spec - App Domain Canonicalization
date: 2025-12-25
owner: Alan
status: draft
---

# SEO Launch Spec - App Domain Canonicalization

## Purpose
Lock the primary SEO domain and document immediate technical SEO fixes so indexing targets the correct URLs.

## Decision
Use `https://aiprofilephotomaker.com` as the primary SEO domain.

## Rationale
- The root domain permanently redirects to the app; search engines treat the app as the authoritative destination.
- Current canonicals on app pages point to the root, which collapses indexing for app pages like `/pricing`.

## Required Changes (P0)
1) Canonical URLs
   - `/` -> `https://aiprofilephotomaker.com/`
   - `/pricing` -> `https://aiprofilephotomaker.com/pricing`
2) H1 structure
   - Remove H1 from the nav logo on `/pricing`; keep a single page H1 that includes a target keyword (e.g., "AI Headshot Pricing").
3) Social meta on `/pricing`
   - Add missing Twitter tags.
   - Use absolute `og:image` URL.
4) Schema on `/pricing`
   - Add JSON-LD for `Product`/`Offer` (or `FAQPage` if FAQs are visible).

## Recommended Enhancements (P1)
1) Section nav links
   - Replace button-based nav with anchor links for crawlable section references.
2) Keyword alignment
   - Ensure H1/H2 include "AI headshot generator" or "LinkedIn headshot" on primary pages.

## Verification
- Search Console property for `aiprofilephotomaker.com`.
- Confirm canonicals via page source.
- Validate schema with Rich Results Test.
- Re-crawl `/` and `/pricing` after deploy.
