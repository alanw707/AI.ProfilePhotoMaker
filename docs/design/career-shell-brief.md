# Shape brief: signed-in app shell and career workspace home (round 1)

Status: **awaiting owner approval**. No code yet. Round 2 (public landing and pricing) is a later brief.

## 1. Job and audience
- An experienced professional exploring a career change, returning for a short session, often on a phone. Mode: **Operate**.
- They need three answers in the first viewport: what is my goal, what do I do next, and what changed since last time.

## 2. Outcome and proof
- **Primary action:** the single next step from the journey API (create profile → set goal → confirm occupation → … → export).
- **Success:** a returning user reaches their next step in one tap from any app page, and can always get back to the photo tools and vice versa.
- **Real evidence on the page:** goal and location, journey progress across the steps, stale marks after a profile or goal edit, drafting allowance (n of 20, reset date), runs in progress, latest result with date. No invented numbers, testimonials or career pricing.

## 3. Selected direction
- **Visual authority:** extend the incumbent. The career pages already follow the owner-reviewed prototype's *research dossier* contract (`.impeccable/surfaces/career-agent-prototype.md`): paper ground, deep ink, teal selected marks, Manrope/DM Sans, flat ruled surfaces, contents rail on the left. This round brings the production Angular pages up to that contract instead of the current stack of equal plain cards.
- **Structural thesis:** one shared app header across photo and career; inside career, a contents rail (journey steps with done/current/stale state) beside the working page. The home page leads with **Goal → Next step** as one strong brief, then progress, then secondary status (allowance, in-progress runs, latest result).
- **Focal moment:** the next-step brief. It is the only primary button on the page.

## 4. Scope and boundaries
- **In scope:**
  - Shared header: add a **Career** link, shown only while the career flag is on, and show the header on every career route (today the career pages render with no header at all).
  - Career contents rail used by all career pages.
  - Career home recomposition.
  - Light and dark themes; desktop, 390 px and 320 px widths.
- **Untouched:** the photo workspace's Studio Proof Desk look, all career page content and forms beyond the rail, the API, landing, pricing and admin.
- **Anti-goals:** a dashboard grid of equal cards, decorative charts, gradients, emoji icons, a chatbot-first layout, any career paywall.

## 5. States and ranges
- First run (no profile), profile only, goal set, mid-journey, everything done.
- Stale after a profile or goal edit.
- Allowance: full, partly reserved, used up (saved work stays usable).
- Runs in progress: 0–3. Loading; API error; career flag off (no link, routes guarded); replay-pending 503.
- Long occupation titles and locations must wrap at 320 px.

## 6. Interaction and layout
- **Desktop:** header, then rail (about 240 px) and the working column.
- **Below about 900 px:** the rail collapses to a compact "Step n of N · current step" control that opens the full list. The next-step brief stays first.
- The rail uses a nav landmark with `aria-current` on the current step. Done, current and stale are each shown with a text label as well as colour.
- Focus is visible everywhere; tap targets are at least 44 px; there is no page-level horizontal scroll.

## 7. Constraints and open decisions
- Angular 19 standalone components and the existing journey and allowance APIs. Test-first: Karma specs for the header link and flag gating, and the rail states; Playwright for navigation from photo to career and back, flag off, and axe at both widths.
- Design is captured in a surface brief, and DESIGN.md gains a career section that records the dossier world next to Studio Proof Desk.
- **Open decision for the owner:** the photo workspace uses cobalt and Archivo, while career uses teal and Manrope/DM Sans.
  - **Recommended for this round:** unify only the shared shell (header, page ground, ink colour, focus ring) and keep each area's accent colour and fonts.
  - **Alternative:** move everything to one accent colour and one typeface now, which widens this round into a restyle of the photo workspace.
