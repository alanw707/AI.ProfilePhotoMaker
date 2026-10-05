# Ticket #394 review: homepage career entry behind the flag

Publication waits for the release gate. SEO canonical and sitemap checks are planned (routes, SEO pages, sitemap, canonical and redirects are unchanged here).

| Acceptance criterion | Evidence |
| --- | --- |
| Flag off: landing DOM and copy unchanged | New markup sits only inside `@if (careerEnabled)`. Playwright compares page headings, links and text with the flag on (career section removed) against flag off and finds them equal. Karma asserts no section and no CTA. |
| Flag on: brand kept, career section added | `[data-career-entry]` has a descriptor, an h2 and the outputs list. Karma and Playwright check the copy. |
| Primary CTA "Plan my next career move" to `/app/career` | `href=/app/career` is asserted. Guests go through the existing guard and login. The page also loads with `?e2eAuthBypass=1`. |
| Honest claims | The section has no "unlimited", "guarantee" or "all live jobs". It says "free with a monthly drafting allowance" and "pay benchmarks from public BLS data". There are no example figures. |
| Photos entry and pricing note | "Create AI headshots" link and the line "Existing pricing covers photos only". |
| Heading levels | One h1 on the page and one new h2. |
| Accessibility | axe WCAG 2.2 AA on the career section: 0 violations at 1280, 390 and 320. No horizontal overflow at 320. |
| Tests | Karma 708 pass. Playwright `career-homepage.spec.ts` 6 pass. Lint and `build:mvp-v1` pass. |

## Findings

| # | Finding | Severity | Status |
| --- | --- | --- | --- |
| 1 | The existing landing page has axe violations outside this ticket: testimonial `aria-label` on stars (aria-prohibited-attr) and some colour-contrast items. | P2 | Left as is, since the flag-off DOM must stay unchanged. Axe is scoped to the new section. |
| 2 | The page's "guarantee" copy is existing marketing text. Forbidden-word checks cover the career section only. | P3 | Out of scope. |


## Independent review (openai-codex/gpt-5.5)
| id | sev | finding | status |
|---|---|---|---|
| R1 | P2 | Career entry also rendered on the flag-on /404 page | fixed: gated with `!showNotFound`; Playwright test |
| R2 | P3 | Flag-off baseline is a CSR comparison, not a stored pre-change snapshot | accepted: landing HTML diff is additions-only inside the flag block |
| R3 | P3 | Forbidden-claims regex missed guaranteed pay / live vacancies | fixed: regex widened |
| R4 | P3 | No test of the async config load path | accepted: APP_INITIALIZER blocks bootstrap |

Gates after fixes: lint 0 errors; Karma 708; build:mvp-v1 succeeds; Playwright homepage 7 passed.

Open P0/P1: **none**
