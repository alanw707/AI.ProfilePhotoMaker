# Profitability + Pivot Evaluation

Date: 2026-06-02

## Verdict

Current app is technically strong enough to sell, but not differentiated enough if positioned as another generic AI headshot generator.

The safer path is not a full rebuild pivot. Keep the current one-upload instant workflow, but sharpen the product into a narrow professional profile-photo outcome product:

> Get one LinkedIn-ready professional profile photo package from one upload: score, improve, choose best shot, export.

## Current feature position

Implemented/currently planned surface:

- One-upload professional photo workflow.
- Instant OpenAI Images path; Replicate training hidden/legacy.
- Profile photo score.
- Portrait style selection.
- Free same-quality watermarked preview.
- Starter Package: 3 candidates, best shot selector, basic adjustment, platform exports, 2 refinements, $9.99.
- Pro Package: 9 candidates, score delta, exports, 5 refinements, 3 premium augmentations, $19.99.
- Gallery/workspace, auth, Stripe, storage, health checks, Docker local stack.

This is a better product direction than high-volume headshot packs, but it still needs sharper market wedge and proof.

## Market reality

AI headshots are now crowded and commoditized.

Competitors sell volume, speed, and guarantees:

- HeadshotPro claims 50+ headshots, 1-3 selfies, 196k+ customers, 18M+ headshots.
- PortraitPal claims 20+ headshots, 5-10 selfies, 3M+ professionals, starting near $35.
- Common market price is roughly $20-$50.
- Many tools compete on 40-200 images, turnaround speed, teams, style variety, and refund guarantees.

Demand still exists, but app-building is no longer the moat. Moat must be outcome quality, trust, niche distribution, and workflow.

## Profitability assessment

### Can current app make profit?

Yes, if acquisition cost is low and generation cost is controlled.

Best early channels:

- SEO landing pages for niche profile-photo use cases.
- LinkedIn/job-search content.
- Founder/realtor/doctor/lawyer vertical pages.
- Partnerships with resume writers, career coaches, recruiters, agencies.

Weak if relying on paid ads against established headshot brands. Generic keywords will be expensive and crowded.

### Current pricing issue

$9.99 / $19.99 may be too low unless generation cost is very low and support burden is minimal.

Use low price only as launch/test pricing. Better package framing:

- Free Preview: score + watermarked preview.
- Starter: $19-$29, one platform-ready package.
- Pro: $39-$59, multiple candidate directions + refinements + exports.
- Team/coach/agency: later B2B wedge.

## Differentiation gap

Current product has the right differentiated idea, but copy and UX must make it impossible to confuse with generic headshot volume tools.

Avoid saying:

- "AI headshot generator"
- "Get 100 professional headshots"
- "Choose styles and generate"

Say:

- "Your best LinkedIn profile photo from one upload"
- "Score, improve, and export a ready-to-use profile photo"
- "Know which photo to use, not just get more images"

## Recommended pivot

Do not pivot to a completely different product yet.

Pivot positioning from:

> AI headshot generator

To:

> Professional profile photo optimizer + export workflow

This is a repositioning pivot, not a codebase pivot.

## Wedge options

Pick one initial wedge. Do not serve everyone.

### Option A: Job seeker / LinkedIn refresh

Best B2C wedge.

Promise: "Improve your LinkedIn photo before applying."

Pros:
- Huge market.
- Clear pain.
- Easy SEO/content.

Cons:
- Price sensitivity.
- Crowded.

### Option B: Realtors / local professionals

Best vertical wedge.

Promise: "MLS, LinkedIn, website, and email-signature headshot package from one upload."

Pros:
- Clear export-kit value.
- Professional need.
- Local SEO possible.

Cons:
- Needs vertical-specific examples/copy.

### Option C: Career coaches / resume writers

Best distribution wedge.

Promise: "Add profile-photo upgrades to every resume package."

Pros:
- Lower CAC through partners.
- Repeat buyers.
- App feature set already fits.

Cons:
- Needs simple referral/team flow.

## 30-day plan

1. Freeze broad feature work.
2. Make `/app/enhance` preview-first and unmistakably outcome-led.
3. Add before/after trust proof: sample gallery, score explanation, privacy copy, refund promise.
4. Rename packages around outcomes, not credits.
5. Add 3 vertical landing pages:
   - LinkedIn profile photo
   - Realtor headshot package
   - Executive/profile photo package
6. Add analytics funnel events:
   - landing CTA click
   - upload start/success
   - score shown
   - preview generated
   - upgrade card shown
   - checkout started
   - payment completed
   - export downloaded
7. Run 20 manual user tests/interviews.
8. Ship only after measuring preview-to-paid conversion.

## Kill criteria

If after focused launch:

- <20% upload-to-preview completion: UX/quality problem.
- <5% preview-to-checkout intent: value proposition problem.
- <2% paid conversion from qualified visitors: pricing/trust/acquisition problem.
- Heavy support/refund rate: output quality problem.

Then consider a stronger pivot toward B2B partner/team workflow.

## Decision

Current offering is enough for a focused paid MVP, not enough for generic market dominance.

Next move: sharpen wedge, instrument funnel, test with real buyers. Do not add broad features until conversion data says where users drop.
