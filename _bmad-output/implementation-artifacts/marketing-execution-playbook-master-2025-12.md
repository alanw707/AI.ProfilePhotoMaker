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
- Post 3x/week (Mon-Wed-Fri) on LinkedIn, cross-post to X (see Organic Posting Strategy).
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

## Content Calendar (Updated 2026-01-16)
- **LinkedIn:** 3x/week (Mon-Wed-Fri) — founder-led content (see "Organic Posting Strategy" for details).
- **X/Twitter:** Cross-post LinkedIn content 2-4 hours after LinkedIn (shorter format).
- **Instagram:** Pause until baseline established; carousel format if restarted.
- **Threads:** Pause until baseline established.
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
- 2026-01-12: Week 1 post content prepared (Posts 2-5 LinkedIn + X + Threads variants).
- 2026-01-12: Content files created: `linkedin-organic-posts-week1.md`, `x-threads-posts-week1.md`.
- 2026-01-12: LinkedIn organic post #2 published (checklist - 5 photo mistakes).
- 2026-01-15: LinkedIn organic post #3 published (vibe coding journey - founder-led content test).
- 2026-01-16: **STRATEGY PIVOT** - Shifted from product-first to founder-led content strategy. Updated organic posting section with new cadence (3x/week), content pillars, and AI coding content bank. Rationale: Personal/authentic posts outperform promotional content on LinkedIn. See "Organic Posting Strategy" section for details.
- 2026-01-16: Drafted 4 LinkedIn posts for Jan 17-24 (founder-led AI coding content). See "Scheduled Posts" section below.
- 2026-01-16: Added X/Twitter versions of all 4 posts (shorter, punchier format for cross-posting).
- 2026-01-16: Drafted week 2 posts (Jan 27-31) with LinkedIn + X versions. See "Scheduled Posts Week 2" section.
- 2026-01-16: **PLAYBOOK CONSISTENCY REVIEW** — Updated Phase 1 cadence, Content Calendar, X/Threads system to align with new 3x/week strategy. Deprecated legacy Week 1 organic posts. All sections now consistent with founder-led content pivot.

## Scheduled Posts (Jan 17-24, 2026)

**Cross-posting strategy:** Post LinkedIn version first (morning), then X version 2-4 hours later. Native formatting for each platform.

---

### Fri Jan 17 — "The Almost Right Problem" (🛠️ Tactical)

**X/Twitter Version:**
```
The hardest part of AI coding isn't getting code.

It's the code that's *almost* right.

My fix: treat every AI suggestion like a PR from a junior dev.

Review it. Test it. Question it.
```

**LinkedIn Version:**
```
The hardest part of AI coding isn't getting code.

It's the code that's almost right.

66% of developers say this is their #1 frustration with AI assistants. The output looks correct. It even runs. But something's slightly off.

Here's how I've learned to catch it faster:

1. Read the code before accepting it.
   Sounds obvious. But the temptation to hit Tab and move on is real.
   I now treat AI suggestions like a PR from a junior dev—review everything.

2. Test immediately.
   I don't batch AI changes. One suggestion, one test.
   If something breaks, I know exactly where to look.

3. Ask "why" when something feels weird.
   AI is great at pattern matching, terrible at explaining trade-offs.
   If I can't explain why the code works, I rewrite it until I can.

4. Keep context small.
   The more files I throw at the AI, the more "almost right" I get.
   Smaller, focused prompts = more accurate output.

The goal isn't to use AI less. It's to use it more intentionally.

What's your biggest frustration with AI-generated code?

#AIcoding #VibeCoding #SoloDeveloper #BuildInPublic #IndieHacker
```

### Mon Jan 20 — "Junior Dev Who Never Sleeps" (🎓 Lesson)

**X/Twitter Version:**
```
AI isn't a senior developer.

It's a junior dev who never sleeps, knows every framework (kind of), and has zero judgment.

Stop asking it to decide.
Start using it to execute YOUR decisions faster.
```

**LinkedIn Version:**
```
Stop treating AI like a senior developer.

It's not one.

AI is more like a junior dev who:
- Never sleeps
- Knows every framework (kind of)
- Types faster than anyone you've ever met
- Has zero judgment about what matters

This reframe changed how I work with it.

I stopped expecting AI to make decisions.
I started using it to execute MY decisions faster.

What does this look like in practice?

❌ "Build me an auth system"
✅ "Implement JWT refresh tokens using this pattern: [specific example]"

❌ "Fix this bug"
✅ "This function returns null on line 42. Here's the expected behavior: [details]"

❌ "Make this faster"
✅ "Profile shows this query runs 47 times. Batch it into one call like this: [approach]"

The more specific I am, the better the output.

AI is a multiplier. But it multiplies YOUR clarity.

Vague input = vague output.
Precise input = code that actually ships.

How do you prompt AI differently now vs when you started?

#AIcoding #VibeCoding #SoloDeveloper #BuildInPublic #IndieHacker
```

### Wed Jan 22 — "One Prompt Line" (🛠️ Tactical)

**X/Twitter Version:**
```
One line I add to every AI coding prompt:

"Follow security best practices and validate all inputs."

Veracode found this improves secure code output by 10%.

Takes 5 seconds. Saves hours of security reviews.
```

**LinkedIn Version:**
```
One line I add to every AI coding prompt:

"Follow security best practices and validate all inputs."

Sounds basic. But according to Veracode's research, adding a security reminder to prompts improved secure code output by 10%.

Here's why this matters:

AI models optimize for "working code."
Not "secure code."
Not "production-ready code."

They'll happily generate SQL queries without parameterization.
Create endpoints without authentication checks.
Store passwords in plain text if you don't specify otherwise.

My prompt template now looks like this:

---
Context: [what I'm building]
Task: [specific ask]
Constraints:
- Follow security best practices
- Validate all inputs
- Handle errors gracefully
- Add appropriate logging
---

The constraints section takes 10 seconds to write.

It saves hours of security reviews later.

What's in your standard prompt template?

#AIcoding #Security #SoloDeveloper #VibeCoding #BuildInPublic
```

### Fri Jan 24 — "AI Hallucinated That Package" (🏗️ Build in Public)

**X/Twitter Version:**
```
AI told me to install a package that doesn't exist.

npm install stripe-webhook-validator

Looked legit. Wasn't real.

New rule: before installing ANY AI-suggested package, I check npm directly.

Trust, but verify.
```

**LinkedIn Version:**
```
Last month, AI told me to install a package that doesn't exist.

npm install stripe-webhook-validator

Looked legitimate. The name made sense. I was building webhook validation for Stripe.

But when I ran the install: "Package not found."

I Googled it. Nothing.

The AI had hallucinated a dependency.

This is called "dependency confusion" — and it's a real security risk. Attackers can register these fake package names with malicious code. If a developer blindly runs the install command, they've just compromised their project.

Now I have a rule:

Before installing ANY package suggested by AI:
1. Search npm/PyPI directly
2. Check the package's GitHub repo
3. Look at download counts and last update date
4. If it's < 1000 weekly downloads, dig deeper

It adds 60 seconds to my workflow.

It's saved me from at least 3 phantom packages since I started checking.

AI is incredibly helpful. But it's also confidently wrong sometimes.

Trust, but verify.

Have you ever caught AI hallucinating a dependency?

#AIcoding #Security #BuildInPublic #SoloDeveloper #IndieHacker
```

---

## Scheduled Posts Week 2 (Jan 27-31, 2026)

### Mon Jan 27 — "19% Slower" (🎓 Lesson)

**X/Twitter Version:**
```
A study found developers using AI were 19% slower.

But they *thought* they were 24% faster.

I've felt this. The speed feels real. The output says otherwise.

The fix: measure what matters (shipped features), not what feels productive (lines generated).
```

**LinkedIn Version:**
```
AI coding made me mass produce code.

But it didn't make me mass produce features.

A fascinating study found developers using AI assistants were actually 19% slower on average. But here's the twist: they were convinced they'd been faster. Before starting, they predicted a 24% speedup. After finishing—with measurably slower results—they still believed AI had helped.

I've experienced this disconnect firsthand.

The keyboard feels busier. The files fill up faster. The dopamine hits when code appears instantly.

But then I'd realize:
- I spent 45 minutes debugging AI-generated code
- I rewrote a function 3 times because the AI "almost" got it
- I merged something that broke an unrelated feature

Here's what changed my results:

1. I stopped measuring "code written" and started measuring "features shipped."

2. I added friction back into the process—reviewing every suggestion before accepting.

3. I started smaller. One focused prompt beats five sprawling ones.

AI absolutely makes me more productive now. But only after I stopped chasing the feeling of speed and started tracking actual outcomes.

Are you measuring the right things?

#AIcoding #Productivity #SoloDeveloper #BuildInPublic #IndieHacker
```

### Wed Jan 29 — "3 Security Checks" (🛠️ Tactical)

**X/Twitter Version:**
```
3 security checks I run on every AI-generated function:

1. Input validation exists (not assumed)
2. No hardcoded secrets or credentials
3. Error messages don't leak internal details

AI optimizes for "works." You optimize for "works safely."
```

**LinkedIn Version:**
```
AI writes working code.

It doesn't write safe code by default.

Here are 3 security checks I run on every AI-generated function before it touches my codebase:

1. Input validation actually exists

AI assumes inputs are clean. They're not.
I check: Are parameters validated? Are types enforced? Are edge cases handled?

If the AI wrote a function that accepts userId, I make sure it rejects null, undefined, negative numbers, and excessively long strings.

2. No hardcoded secrets

You'd be surprised how often AI generates code like:
const API_KEY = "sk-abc123..."

I scan for anything that looks like a key, token, password, or connection string. These should come from environment variables, not source code.

3. Error messages don't leak internals

AI loves helpful error messages:
"Database connection failed: postgres://user:pass@localhost:5432/mydb"

Great for debugging. Terrible for production. I rewrite errors to be useful without exposing infrastructure details.

These checks add maybe 2 minutes per function.

They've caught real issues that would've been embarrassing (or worse) in production.

What's on your AI code review checklist?

#AIcoding #Security #SoloDeveloper #BuildInPublic #CyberSecurity
```

### Fri Jan 31 — "My AI Tool Stack" (🏗️ Build in Public)

**X/Twitter Version:**
```
My AI coding stack in 2026:

• Claude Code for architecture + complex refactors
• Copilot for autocomplete while typing
• ChatGPT for quick questions + rubber ducking

No single tool does everything well. I use each for what it's best at.

What's your stack?
```

**LinkedIn Version:**
```
"What AI tools do you actually use?"

I get this question a lot. Here's my honest answer after building a full SaaS with AI assistance:

There's no single "best" tool. I use different tools for different jobs.

My current stack:

🧠 Claude Code — Complex refactors + architecture

When I need to restructure a feature across multiple files, Claude handles the context better than anything else. It's slower and more expensive, but for gnarly refactors, it's worth it.

I also use it when I'm stuck on architectural decisions. "Here are the trade-offs between approach A and B"—Claude actually explains the reasoning.

⚡ GitHub Copilot — Autocomplete while typing

For the flow state moments when I know what I want to write, Copilot fills in the boilerplate. Tab, tab, tab. It's not thinking for me; it's typing for me.

I keep it on for routine code, off for anything requiring judgment.

💬 ChatGPT — Quick questions + rubber ducking

When I need a fast answer or want to talk through a problem, ChatGPT is my rubber duck. It's not touching my codebase—it's just helping me think.

The key insight:

Each tool optimizes for something different:
- Claude: Depth and reasoning
- Copilot: Speed and flow
- ChatGPT: Conversation and exploration

Using one tool for everything is like using a hammer for every job.

What's your AI coding stack?

#AIcoding #VibeCoding #BuildInPublic #SoloDeveloper #IndieHacker
```

---

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

### Organic Posting Strategy (Founder-Led Content - Updated 2026-01-16)

**Strategy Pivot:** Shifted from product-first promotional content to founder-led authentic content. Personal stories and expertise-sharing outperform direct product promotion on LinkedIn.

**Rationale:**
- LinkedIn algorithm rewards personal narratives and comment-generating content
- Founder credibility drives product trust for indie SaaS
- "Build in public" content creates compounding audience growth
- Authentic expertise > corporate marketing voice

**Cadence:** 3x/week (Mon-Wed-Fri) — sustainable for founders with day jobs.

**Content Pillars:**
| Day | Type | Focus | Example |
|-----|------|-------|---------|
| **Monday** | 🎓 Lesson/Insight | AI coding wisdom, mistakes, learnings | "AI coding made me 19% slower. Here's why I kept using it." |
| **Wednesday** | 🛠️ Tactical tip | Practical how-to, prompts, workflows | "One prompt line that improved my AI code quality" |
| **Friday** | 🏗️ Build in public | Journey, wins, struggles, product updates | "This week I shipped X. Here's what broke." |

**Monthly Sprinkle (1-2x/month):**
- Soft product mention (woven into story, not hard sell)
- Milestone celebration ("Hit 100 users this week")
- Industry hot take or observation

**What NOT to do:**
- ❌ "Get your headshot in minutes" CTA-heavy posts
- ❌ Product process infographics (save for ads/website)
- ❌ Link in every post body (kills reach)
- ❌ Corporate voice or "Excited to announce..." openers

**Post Structure:**
1. **Hook** (first line visible before "see more" — must stop the scroll)
2. **Story/Value** (2-4 short paragraphs, lots of white space)
3. **Lesson or insight** (numbered if multiple)
4. **Engagement question** (invites comments, not just clicks)
5. **Hashtags** (3-5 max, at end)

**Hashtags (updated seed set):**
#IndieHacker #AITools #SoloDeveloper #Entrepreneurship #BuildInPublic #VibeCoding #AIcoding #StartupLife

**Engagement Rules:**
- Reply to ALL comments within 2 hours (algorithm boost)
- Ask follow-up questions to commenters
- Comments > likes as success metric
- Link goes in FIRST COMMENT, not post body

**Success Metrics (revised):**
- Primary: Comments per post
- Secondary: Profile views, follower growth
- Tertiary: Website clicks (but not primary goal)

### X/Twitter Cross-Posting System (Updated 2026-01-16)
- **X cadence:** 3x/week (Mon-Wed-Fri), posted 2-4 hours after LinkedIn version.
- **Format:** Shorter, punchier version of LinkedIn content. Remove engagement questions if too long.
- **Character limit:** Keep under 280 chars for single tweets; threads for tactical posts if needed.
- **Templates:** Hook (1-2 sentences) → key insight → brief CTA or question.
- **Hashtags:** 2-3 max on X (less hashtag-friendly than LinkedIn).

### Threads (Paused)
- Threads paused until LinkedIn + X baseline established.
- Future consideration: Multi-card narrative format for tactical posts.

### LinkedIn Creative System
- **Templates:** 3 headline styles (speed, quality, affordability).
- **Copy variants:** 5 hooks per persona (job seeker, founder, freelancer).
- **Proof library:** Maintain 10+ approved transformations with consent.

### AI Coding Content Bank (Founder-Led Topics)

**Why this content:** Positions Alan as an AI-augmented developer thought leader. Attracts audience interested in AI, productivity, and indie hacking. Product mentions are secondary to expertise-sharing.

**Research-backed pain points to address:**
- 66% of devs say AI code is "almost right, but not quite" (Stack Overflow)
- Devs think they're 24% faster but are actually 19% slower (METR study)
- 45% of AI-generated code fails security tests (Veracode)
- AI code has 1.7x more bugs than human code (CodeRabbit)

**Content Bank (10 posts):**

| # | Hook | Type | Angle |
|---|------|------|-------|
| 1 | "AI coding made me 19% slower. Here's why I kept using it." | 🎓 Lesson | Productivity paradox truth |
| 2 | "The 'almost right' problem—and how I fix it" | 🛠️ Tactical | Debugging AI code |
| 3 | "One prompt line that improved my AI code quality by 10%" | 🛠️ Tactical | Security reminder trick |
| 4 | "Stop treating AI like a senior dev. It's a junior who never sleeps." | 🎓 Lesson | Mindset reframe |
| 5 | "3 security checks I run on every AI-generated function" | 🛠️ Tactical | Security practices |
| 6 | "That npm package doesn't exist—AI hallucinated it" | 🏗️ Story | Hallucinated dependencies |
| 7 | "Why I review every AI change before hitting accept" | 🎓 Lesson | Butterfly effect |
| 8 | "Prompting is a skill. Here's how I got better at it." | 🛠️ Tactical | Structured prompting |
| 9 | "Claude vs Cursor vs Copilot: What I actually use" | 🏗️ Build in public | Tool stack breakdown |
| 10 | "Building a SaaS with AI: What worked, what didn't" | 🏗️ Build in public | Retrospective |

**Product-Adjacent Content (Monthly):**
- Milestone posts ("Hit X users", "Shipped Y feature")
- Behind-the-scenes of building AI Profile Photo Maker
- Lessons from launching a SaaS as a solo dev

### Legacy Post Ideas (Product-Focused - Deprioritized)
*Keep for reference but use sparingly (1-2x/month max):*
1. "From blurry selfie to hire-ready in minutes."
2. "5 photo mistakes recruiters notice immediately."
3. "How we turn one photo into a professional headshot."
4. "What makes a headshot look professional."

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

### Legacy Week 1 Organic Post Copy (DEPRECATED - Pre-Pivot Content)
*These product-focused posts were from the original strategy. Posts #1-2 were published before the founder-led pivot. Kept for reference only—use "Scheduled Posts" sections above for current content.*

<details>
<summary>Click to expand legacy posts (archived)</summary>

**Post 1 (Transformation) — PUBLISHED 2026-01-09:**
Hook: "This is the fastest professional upgrade you can make this week."
Body: "A new headshot changes first impressions instantly. We took a casual photo and turned it into a studio-quality look in minutes. If you are interviewing soon, your profile should say: hire-ready."
CTA: "Want yours today? Get your headshot in minutes."
Hashtags: #jobsearch #linkedinprofile #personalbrand

**Post 2 (Checklist) — PUBLISHED 2026-01-12:**
Hook: "Recruiters notice these 5 photo mistakes immediately."
Body: "1) Low light. 2) Busy background. 3) Cropped shoulders. 4) Casual posture. 5) Old photo. A clean headshot fixes all five and helps you look confident before you even speak."
CTA: "Upgrade your photo in minutes."
Hashtags: #careeradvice #jobsearch #headshot

**Post 3-5 — NOT PUBLISHED (superseded by new strategy):**
Archived for potential future ad copy inspiration.

</details>

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
