# Tech-Spec: Social Quick-Win Campaign (AI Profile Photo Maker)

**Created:** 2025-12-25
**Status:** Completed

## Overview

### Problem Statement
The app is live but has minimal social traction and limited creative assets. The goal is to generate immediate traffic and conversions using social channels with low-to-moderate paid spend and minimal content production effort.

### Solution
Launch a 2-week "quick win" social campaign centered on the promise: studio-quality headshots at low cost. Use a lean content kit (templates + testimonial snippets), light paid amplification, and a daily posting cadence across LinkedIn, Instagram, X, and Facebook. Prioritize short-form posts, before/after style examples (even if staged), and user testimonial copy. Build a simple tracking loop (UTMs + landing page focus) to measure traffic and conversion quickly.

### Scope (In/Out)

**In scope**
- Social campaign strategy and execution plan
- Core message positioning and audience focus
- Minimum viable creative kit (template specs + copy bank)
- Paid boost plan (low spend)
- UTM tracking + success metrics

**Out of scope**
- SEO strategy (handled separately)
- Full brand redesign or new identity work
- Large-scale influencer programs
- Product feature changes

## Context for Development

### Codebase Patterns
Not applicable (campaign is marketing-focused; no code changes required).

### Files to Reference
- `docs/seo-launch-spec-2025-12-25.md` (for alignment, not in scope here)
- Site testimonials (source for social proof)

### Technical Decisions
- Use UTM-tagged links for every social post.
- Use `/pricing` as the default landing page; test homepage only if CR is weak after day 3.
- Use `utm_medium=paid-social` for boosted posts and `utm_medium=social` for organic.
- Prefer conversion objectives only if pixel/Insight Tag is active.

## Implementation Plan

### Tasks

- [x] Task 1: Define core messaging + audience personas
  - Primary: job seekers / professionals updating LinkedIn
  - Secondary: freelancers, founders, remote workers
  - Message: "Studio-quality headshots in minutes, at a fraction of the cost."

- [x] Task 2: Create minimum viable creative kit
  - 4 square templates (IG/FB)
  - 2 vertical story templates (IG/FB)
  - 2 horizontal templates (LinkedIn/X)
  - Testimonial text snippets from site

- [x] Task 3: Build a 2-week posting cadence
  - Daily posts (mix of testimonials, product promise, offer, CTA)
  - 2x weekly "before/after" or "why it works" post
  - 1x weekly founder post (personal angle)

- [x] Task 4: Launch low-budget paid boosts
  - $10-$25/day for 7-14 days
  - Focus: LinkedIn + Instagram
  - Objective: traffic to landing page

- [x] Task 5: Add tracking + reporting loop
  - UTM links per platform + post type
  - Daily check: clicks + conversion rate
  - End-of-week review and adjust

### Implementation Outputs

- Campaign plan: `_bmad-output/implementation-artifacts/social-quick-win-campaign-plan-2025-12-25.md`

### Acceptance Criteria

- [ ] AC 1: Given any social post, when clicked, then the link includes UTM parameters and resolves to the correct landing page.
- [ ] AC 2: Given the content calendar, when executed, then each platform receives at least 5 posts per week for 2 weeks.
- [ ] AC 3: Given the paid boost, when launched, then spend stays within budget and drives measurable clicks.
- [ ] AC 4: Given testimonial assets, when used in posts, then at least 3 posts include social proof.

## Additional Context

### Dependencies
- Access to testimonials from the site
- Simple design tool for templates (e.g., Canva/Figma)
- Social accounts and ad manager access

### Testing Strategy
- Validate UTMs via Google Analytics (or lightweight UTM checker).
- Monitor CTR and conversion daily; adjust creative after 3 days.

### Notes
- If no before/after assets exist, generate 2-3 internal examples quickly.
- Keep copy short, benefit-forward, and CTA-driven.
- If conversions are weak, test `/pricing` vs homepage.
- Review notes: see Review Notes section below.

## Review Notes
- Adversarial review completed
- Findings: 12 total, 12 fixed, 0 skipped
- Resolution approach: auto-fix
