# Feature-set review and suggestions — 2026-10-11

A look at what the application offers today, where it is inconsistent, and what to do next.
Facts come from the repository at commit 05ea1c8d and the live `/api/config/client`. Nothing here
has been validated with users; where a suggestion is a judgement, it says so.

## What exists today

| Area | What it is | Live? | How it is paid for |
|---|---|---|---|
| Photo workflow (`/app/enhance`, `/app/gallery`) | One source photo → score → style → candidates → best shot → refine → platform exports | Yes | Free Preview, Starter, Pro packages (Stripe) |
| Photo add-ons | Photo score, creative style pack, premium augmentations, outcome packages | Yes (all four flags `true` in `simple-deploy.bicep`) | Inside packages |
| Career workspace (`/app/career/*`, 16 page components) | Profile or resume import → goal → occupation → market brief → pay → market comparison → roadmap → resume and summary → export → optional photo | Yes, for every signed-in user | Free; capped at 20 drafts per user per month and $2/day, $25/month overall |
| Career Jobs page | Open federal postings from USAJOBS | Hidden since 2026-10-11 | n/a |
| Marketing | Landing page, about 25 headshot SEO pages, two competitor comparisons, blog, 12 legal pages | Yes | n/a |
| Admin | Dashboard, product health, users, coupons, audit log, campaigns, career usage | Yes | n/a |
| Replicate custom-model training | Older photo flow | Hidden "legacy/fallback" (`PRODUCT.md`) | n/a |

Size: about 37,000 lines of UI TypeScript and 63,000 lines of API C# (excluding tests and
migrations); 101 route entries; 9 feature flags.

## Main findings

### 1. The product says two different things about itself

`PRODUCT.md` (confirmed 2026-10-07) says the product is a **career-pivot workspace** and photos are
one tool inside it. The site says the opposite:

- Page title: "AI Profile Photo Maker - Transform Your Photos into Professional Headshots".
- The landing page leads with headshots; career is one section further down (`landing.component.html:248`).
- After sign-in, `/app` goes to `/app/enhance`, not the career home.
- All ~25 SEO pages target headshot searches. None target career-change searches.

A visitor cannot tell that the career workspace is the main product. This is a judgement call for
the owner, not a bug: either the positioning in `PRODUCT.md` is ahead of the evidence, or the site
is behind it.

### 2. There is no way to see whether the career workspace is used

- The career pages send **no analytics events** (0 tracking calls under `pages/career`; 5 in the whole UI).
- There is no in-product feedback control on any career page.
- The admin usage report counts assistant runs and cost, not people moving through the steps.
- No independent user has been observed (#377).

So the largest feature in the product has no usage evidence of any kind. Every other career
decision (pricing, promotion, Jobs, Stage 3) depends on this.

### 3. Career has cost but no revenue path

Career pricing is explicitly undecided. The only link from career to revenue is the optional photo
handoff. Costs are capped, so the exposure is small (at most $25 a month), but there is also no
measure of whether career users go on to buy photos.

### 4. Dead and hidden weight

- **Replicate training flow:** 48 API files and about 25 UI files mention it, including four
  controllers of roughly 1,500 lines, for a flow the product calls hidden legacy.
- **Jobs page:** hidden, with about 60 API tests and a full page still maintained.
- **Redirect-only routes:** about 20 route entries exist only to redirect old URLs. These are cheap and worth keeping for SEO.

### 5. Open operational items

- Six admin issues are open (#365–#370): package entitlement grants and revocation, and Product
  Health purchase reporting that is described as incorrect (#368). If purchase reporting is wrong,
  the photo business is also being run partly blind.
- ImageSharp: five suppressed advisories, two possibly reachable (see `imagesharp-decision-2026-10.md`).

## Suggestions, in priority order

| # | Suggestion | Why | Size | Needs the owner? |
|---|---|---|---|---|
| 1 | **Add career funnel analytics**: one event per step reached (profile saved, goal saved, occupation confirmed, brief, pay, roadmap, draft, export, photo handoff), plus return visits | Without it nothing else about career can be decided on evidence | Small | No |
| 2 | **Run the five-person user test** (`docs/testing/career/377/USER-TEST-KIT.md`) | Only source of evidence on whether people understand the workspace | 1–2 days of the owner's time | Yes |
| 3 | **Add a one-question feedback control** on career results ("Was this useful? yes/no" plus optional text) | Cheap signal on output quality between user tests | Small | No |
| 4 | **Fix Product Health purchase reporting (#368)** | Revenue numbers the owner relies on are flagged as wrong | Medium | No |
| 5 | **Decide the positioning, then make the site match it.** If career leads: change the title, hero and post-sign-in landing, and add a few career-change SEO pages. If photos lead: say so in `PRODUCT.md` and keep career as a secondary section | Removes the contradiction in finding 1 | Medium | Yes (the decision) |
| 6 | **Restrict image decoders to JPEG/PNG/WebP**, then resolve the ImageSharp licence question | Closes a possibly reachable denial-of-service path | Small, then owner decision | Partly |
| 7 | **Measure career → photo conversion** once analytics exist | The only current revenue path for career | Small (after 1) | No |
| 8 | **Decide the Jobs page by a date** (suggested: the next review). Bring it back with a private-sector source such as Adzuna, or delete the code | Hidden code is carried cost | Medium or small | Yes (API key or the decision) |
| 9 | **Remove the Replicate training flow** after confirming no paying user depends on it | Largest block of unused code; fewer webhooks and secrets to maintain | Medium-large | Yes (confirm it is safe to drop) |
| 10 | **Hold career pricing** until 1, 2 and 7 have produced data | Pricing an unmeasured feature would be a guess | none | Yes |

### What I would not do yet

- Add more career pages or data sources. The workspace is already wide (16 page components) and unmeasured.
- Promote the workspace or raise its caps before the user test and one week of usage data
  (`docs/testing/career/launch/STAGE3-READINESS.md`).
- Build an employer-side job board. It is a different product.

## Limits of this review

- It is based on reading the code and configuration, not on user data, revenue figures or traffic,
  none of which were available.
- The feature inventory was done by one reviewer; a second, independent pass was attempted and the
  delegation tool failed.
- Line and file counts are approximate (simple text searches).
