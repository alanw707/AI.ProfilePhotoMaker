# Progress Log

## 2025-12-25
- Completed market research workflow: customer insights and competitive analysis for AI headshot/profile photo service. Output file: `_bmad-output/planning-artifacts/research/market-ai-headshot-launch-seo-competitors-research-2025-12-25.md`.
- SEO decision: use `https://app.aiprofilephotomaker.com` as primary SEO domain because root domain permanently redirects to app; current canonicals on app pages point to root and should be updated.
- SEO audit via Playwright on app domain:
  - Home title/description set; robots index/follow; canonical currently `https://aiprofilephotomaker.com/`; JSON-LD present (SoftwareApplication + FAQPage).
  - Pricing page title/description set; canonical currently `https://aiprofilephotomaker.com/`; multiple H1s; missing Twitter meta; og:image relative; schema missing.
  - Recommended fixes: update canonicals to app URLs, remove extra H1 on pricing, add pricing schema + Twitter tags, make og:image absolute.
- Created spec doc: `docs/seo-launch-spec-2025-12-25.md`.
- Created social quick-win campaign artifacts:
  - Tech spec completed: `_bmad-output/implementation-artifacts/tech-spec-social-quick-win-campaign.md` (status: Completed; ACs checked).
  - Campaign plan: `_bmad-output/implementation-artifacts/social-quick-win-campaign-plan.md`.
  - Content kit + 2-week cadence: `_bmad-output/implementation-artifacts/social-quick-win-content-kit.md`.
  - Day-1 checklist: `_bmad-output/implementation-artifacts/social-quick-win-day1-checklist.md`.
  - First 5 posts with UTMs: `_bmad-output/implementation-artifacts/social-quick-win-first-5-posts.md`.
- Open item: confirm permissions for named testimonials before posting (F7 from review).
- Implemented SEO launch spec updates in UI:
  - Canonicals set to `https://app.aiprofilephotomaker.com` for home and `/pricing`.
  - Header logo H1 removed; pricing page uses keyworded H1 ("AI Headshot Pricing Plans").
  - Pricing page adds Twitter/Open Graph meta with absolute `og:image` and Product/AggregateOffer JSON-LD.
  - Landing JSON-LD/FAQ scripts cleaned up on destroy to prevent SPA bleed-over.
- Verification: Docker rebuild (`docker compose up -d --build`) succeeded; UI lint passed; Playwright checks confirm canonical/meta and single H1 on home/pricing (pricing credit status 401 when unauthenticated).
- Resumed work on `feature/seo-launch` and restored memory bank context.
