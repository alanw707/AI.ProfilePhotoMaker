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
- Post daily for 14 days (LinkedIn + IG + X + Threads minimum).
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

## Operating System (Weekly Rhythm)
- **Monday:** Review last week's performance, pick 1-2 experiments, set creative brief.
- **Wednesday:** Mid-week checkpoint on CTR and checkout-start by creative.
- **Friday:** Decide keep/kill, update backlog, and queue next week's assets.

## Creative System
- **Formats:** Before/after carousel, single image with headline, 15s video loop.
- **Asset cadence:** 2 new creatives/week, 1 refreshed testimonial/week.
- **Naming convention:** `channel_persona_hook_v01` (e.g., `linkedin_jobseeker_timesaver_v01`).
- **Proof rules:** Only use approved proof assets with consent logged.

## Messaging Matrix (Initial)
- **Job seekers:** "Look hire-ready in minutes" + "Lower stress for interviews."
- **Founders/freelancers:** "Show up polished across your brand" + "Consistent headshots."
- **Recruiters/hiring managers:** "Speed up candidate readiness" + "Consistent profile quality."

## Channel Guardrails
- **Pause rule:** If CTR < 0.7% for 3 days, rotate creative.
- **Scale rule:** If purchase rate > 2% for 3 days, increase budget 20%.
- **Creative fatigue:** If CTR drops 30% WoW, replace top 2 creatives.

## Measurement + Attribution
- **UTM standard:** `utm_source`, `utm_medium`, `utm_campaign`, `utm_content`.
- **Event priority:** view -> upload_start -> checkout_start -> purchase.
- **Reporting:** Use daily export with creative ID, CTA variant, and persona tag.

## Experiment Backlog (Weeks 1-6)
- **Week 1:** Homepage vs pricing page test.
- **Week 2:** CTA copy test (minutes vs studio-quality).
- **Week 3:** Persona-specific headline variants.
- **Week 4:** Proof module placement (top vs mid page).
- **Week 5:** Pricing framing (single vs bundle).
- **Week 6:** Guarantee or risk-reversal message.

## Content Calendar (Weeks 1-2)
- **LinkedIn (daily):** 1 post/day focused on transformation, job-seeking, or speed.
- **Instagram (daily):** Carousel with before/after + caption with CTA.
- **X (Mon-Fri):** 2 posts/day; short hooks + proof + CTA.
- **Threads (Mon-Fri):** 1 post/day; short story + CTA.
- **Website blog (weekly):** 1 post aligned to SEO pillars from tech spec.

## Phase 5: Scale (Weeks 6-12)
**Goal:** Grow spend while protecting CAC.
- Expand geo targets if CPA stays within range.
- Add new channel (Meta or Google) only after stable baseline.
- Build referral loop and partner outreach.

## Ownership (Draft)
- **Strategy + approvals:** Alan
- **Creative production:** Design + copy owner
- **Tracking + analytics:** Engineering

## Execution Log
- 2026-01-09: LinkedIn organic post #1 published (transformation hook).

## LinkedIn First: Ads + Organic (Weeks 1-4)
**Goal:** Build signal fast with a combined paid + posting engine.

### Paid Ads (LinkedIn)
- **Objective:** Website conversions.
- **Formats:** Single image, carousel (before/after), 15s video loop.
- **Targeting (initial):** Australia, age 22-45, job-seeker signals (Open to work), roles: marketing, sales, engineering, customer success, operations.
- **Targeting refinement:**
  - **Industries:** Technology, Professional Services, Education, Finance, Healthcare, Real Estate.
  - **Seniority:** Entry, Associate, Mid-Senior, Manager.
  - **Skills:** Resume writing, interviewing, job search, sales, marketing, software development.
  - **Groups/interests:** Career development, job search, LinkedIn profile optimization.
- **Exclusions:** Current customers, employees (company list), low-intent audiences.
- **Budget start:** Set a daily cap per creative; rotate top 2 after Day 7.
- **Creative rules:** One clear hook, one visual proof, one CTA; no clutter.
- **Landing path:** Homepage test vs pricing page per Decision Gate Day 3.

### Organic Posting Strategy
- **Cadence:** 1 post/day, 5 days/week (Mon-Fri).
- **Content mix (weekly):**
  - 2x transformation posts (before/after + short story).
  - 1x practical tip for job seekers (photo-ready checklist).
  - 1x credibility post (behind-the-scenes, process, or quality bar).
  - 1x CTA post (limited-time offer, fast turnaround, or bundle).
- **Post structure:** Hook (1 line) -> proof or insight -> CTA -> 2-3 hashtags.
- **Hashtags (seed set):** #jobsearch #linkedinprofile #careeradvice #headshot #personalbrand.
- **Engagement:** Reply to all comments within 2 hours, tag relevant audiences when appropriate.

### X + Threads System
- **X cadence:** 2 posts/day (Mon-Fri). Morning post = short hook + proof mention + CTA. Afternoon post = quick result download, testimonial clip, or playbook insight with CTA.
- **Threads cadence:** 1 post/day (Mon-Fri). Multi-card narrative (3-5 cards) that walks through the transformation, proof, or checklist ending with CTA + question to invite replies.
- **Templates:** Hook (1-2 sentences) -> proof/fact (stat, testimonial, transformation) -> CTA (Get your headshot in minutes, upgrade my photo) -> hashtags (seed set + #X# or #Threads). Keep tone direct across channels.
- **Variation:** Pair job-seeker hooks with speed/value messaging (LinkedIn-friendly). Use founder/freelancer angle for Threads cards 3-4 when referencing consistent brand imagery.

### LinkedIn Creative System
- **Templates:** 3 headline styles (speed, quality, affordability).
- **Copy variants:** 5 hooks per persona (job seeker, founder, freelancer).
- **Proof library:** Maintain 10+ approved transformations with consent.

### First 10 Organic Post Ideas (Week 1-2)
1. **Transformation story:** "From blurry selfie to hire-ready in minutes."
2. **Checklist:** "5 photo mistakes recruiters notice immediately."
3. **Process proof:** "How the studio-quality look is created (quick steps)."
4. **Speed proof:** "Before lunch, new headshot live on LinkedIn."
5. **Value framing:** "Spend less than a coffee a day for a profile upgrade."
6. **Behind the scenes:** "What makes a headshot look professional."
7. **Social proof:** Short testimonial + before/after.
8. **Career tip:** "One profile change that increases replies."
9. **Myth busting:** "You do not need a photographer to look pro."
10. **CTA:** "Need a new headshot before next interview? 10-minute turnaround."

### Initial Ad Copy Variants (Job Seeker Persona)
- **Hook A (speed):** "Hire-ready in minutes. New headshot today."
- **Hook B (quality):** "Studio-quality headshot without the studio."
- **Hook C (value):** "Upgrade your LinkedIn photo for a fraction of the cost."
- **Body A:** "Upload one photo, get multiple professional options. Perfect before interviews."
- **Body B:** "Look confident, credible, and current. Results in minutes."
- **CTA options:** "Get your headshot", "See your new look", "Upgrade my photo"

### Initial Ad Copy Variants (Founder/Freelancer Persona)
- **Hook A (brand):** "Show up polished across every client touchpoint."
- **Hook B (consistency):** "Consistent headshots for your personal brand."
- **Hook C (speed):** "New headshot in minutes, not weeks."
- **Body A:** "Upgrade your website, proposals, and LinkedIn with a professional look."
- **Body B:** "One photo in, multiple studio-quality options out."
- **CTA options:** "Polish my brand", "Upgrade my headshot", "Get started"

### Creative Briefs (LinkedIn)
- **Brief 1: Before/After Carousel**
  - **Visual:** 2-4 panels, left = original, right = enhanced.
  - **Headline:** "From selfie to hire-ready."
  - **Proof note:** Use only consented transformations.
- **Brief 2: Single Image (Value)**
  - **Visual:** Final headshot centered, price/value badge.
  - **Headline:** "Studio look. Fraction of the cost."
  - **Proof note:** Add testimonial line if available.
- **Brief 3: 15s Video Loop (Process)**
  - **Visual:** 3-step animation: Upload -> Choose -> Download.
  - **Headline:** "New headshot in minutes."
  - **Proof note:** End card with CTA and logo.

### Week 1 Organic Post Copy (LinkedIn)
**Post 1 (Transformation):**
Hook: "This is the fastest professional upgrade you can make this week."
Body: "A new headshot changes first impressions instantly. We took a casual photo and turned it into a studio-quality look in minutes. If you are interviewing soon, your profile should say: hire-ready."
CTA: "Want yours today? Get your headshot in minutes."
Hashtags: #jobsearch #linkedinprofile #personalbrand

**Post 2 (Checklist):**
Hook: "Recruiters notice these 5 photo mistakes immediately."
Body: "1) Low light. 2) Busy background. 3) Cropped shoulders. 4) Casual posture. 5) Old photo. A clean headshot fixes all five and helps you look confident before you even speak."
CTA: "Upgrade your photo in minutes."
Hashtags: #careeradvice #jobsearch #headshot

**Post 3 (Process Proof):**
Hook: "How we turn one photo into a professional headshot."
Body: "Step 1: Upload a casual photo. Step 2: Choose your look. Step 3: Download multiple pro options. No studio, no waiting weeks."
CTA: "Try it before your next interview."
Hashtags: #linkedinprofile #personalbrand #career

**Post 4 (Credibility):**
Hook: "What makes a headshot look professional?"
Body: "Lighting, framing, and expression. We optimize all three so your photo feels confident and current. It is the easiest way to look more credible fast."
CTA: "See your new headshot today."
Hashtags: #professionalbranding #headshot #career

**Post 5 (CTA):**
Hook: "Interview next week? Update your headshot today."
Body: "First impressions happen in seconds. A current, professional headshot gives you an advantage before the conversation starts."
CTA: "Get your headshot in minutes."
Hashtags: #jobsearch #linkedinprofile #headshot

### LinkedIn Ads Manager Drafts (Initial)
**Ad A (Job Seeker - Speed)**
- **Primary text:** "Interview coming up? Get a studio-quality headshot in minutes. Upload one photo and choose your look."
- **Headline:** "Hire-ready in minutes"
- **Description:** "New professional headshot today."
- **CTA:** "Get your headshot"

**Ad B (Job Seeker - Quality)**
- **Primary text:** "Look confident and current on LinkedIn. Studio-quality headshots without a studio session."
- **Headline:** "Studio-quality without the studio"
- **Description:** "Upgrade your profile fast."
- **CTA:** "Upgrade my photo"

**Ad C (Founder/Freelancer - Brand)**
- **Primary text:** "Your photo shows up everywhere: website, proposals, LinkedIn. Make it professional and consistent."
- **Headline:** "Polish your personal brand"
- **Description:** "Multiple pro options in minutes."
- **CTA:** "Get started"
