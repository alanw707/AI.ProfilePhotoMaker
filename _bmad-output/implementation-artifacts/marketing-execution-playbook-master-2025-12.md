# Marketing Execution Playbook: Master (Full-Funnel)

**Date:** 2025-12-25
**Owner:** Alan
**Status:** Draft

## Purpose
Single source of truth for the full marketing execution path: launch readiness, marketing pages, SEO foundation, advertising channels, conversion optimization, and lifecycle.

## Scope
This master playbook references focused sub-playbooks and technical specs:
- Advertising channels: `_bmad-output/implementation-artifacts/marketing-execution-playbook-2025-12.md`
- SEO indexing + marketing pages: `_bmad-output/implementation-artifacts/tech-spec-seo-indexing-marketing-pages.md`
- Prompt/style differentiation (if used in messaging): `_bmad-output/implementation-artifacts/tech-spec-prompt-style-differentiation-professional.md`

If a detail is owned by a sub-playbook, follow that source. This file defines overall sequencing, gates, and priorities.

## Decision Lock (Week 1)
- **Core promise:** Studio-quality headshots in minutes at a fraction of the cost.
- **Primary CTA:** Get your headshot in minutes.
- **Primary persona:** Job seekers updating LinkedIn.
- **Secondary personas:** Recruiters, hiring managers, freelancers, founders, remote workers.
- **Geo focus (Week 1):** Australia; expand to NZ/US/UK/CA if CPA is acceptable.
- **Primary KPI:** Paid conversion rate.

## Phase 0: Launch Readiness (Week 0)
**Goal:** Do not spend on ads until these are green.
- Landing page live (or pricing page live) with correct CTA.
- Payment flow verified end-to-end.
- Tracking installed and verified (GA4, Meta Pixel, LinkedIn Insight Tag).
- 2-3 approved proof assets (before/after + testimonial).
- Legal/consent confirmed for any testimonials.

## Phase 1: Baseline Acquisition (Weeks 1-2)
**Goal:** Establish baseline conversion and channel signal.
- Execute the Advertising Channels playbook.
- Post daily for 14 days (LinkedIn + IG minimum).
- Boost one creative only after baseline CTR is known.
- Decision Gate Day 3: test homepage vs pricing if CR weak.
- Decision Gate Day 7: reallocate spend to top 2 creatives.

## Phase 2: SEO Foundation (Weeks 2-6)
**Goal:** Build compounding organic acquisition.
- Implement SEO indexing + marketing pages tech spec.
- Ship sitemap, robots, metadata, canonical rules.
- Publish priority marketing pages and long-form SEO pillars.
- Verify indexation and rankings weekly.

## Phase 3: Conversion Optimization (Weeks 3-6)
**Goal:** Improve paid and organic conversion efficiency.
- A/B test CTA and pricing framing.
- Experiment with headline variants per persona.
- Add social proof modules and measurable claims.
- Monitor funnel drop-offs (upload -> checkout -> purchase).

## Phase 4: Lifecycle + Referral (Weeks 4-8)
**Goal:** Capture and convert non-buyers, and drive referrals.
- Add email capture for non-converters.
- Add 2-step email follow-up (value + social proof).
- Launch referral prompt post-delivery.

## Reporting Cadence
- **Daily:** CTR, checkout-start, purchase rate by creative id.
- **Weekly:** top 2 creatives, underperformers, next tests.
- **Targets to start:** CTR >= 1%, checkout-start >= 3%, purchase >= 1.5%.

## Risks + Mitigations
- **No live pages:** pause spend until Phase 0 is complete.
- **Weak conversion:** switch to homepage test and adjust CTA.
- **Creative fatigue:** rotate new before/after and testimonials weekly.

## Next Actions (Immediate)
- Confirm Phase 0 readiness.
- Lock LinkedIn as primary channel.
- Pick first 3 creatives and approve copy.
