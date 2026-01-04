---
docType: execution-tracker-template
date: 2025-12-30
owner: Alan
status: draft
---

# Marketing Execution Tracker (Notion-ready)

## How to use
1. Create a new Notion database (table).
2. Add the columns below.
3. Paste the Seed Rows table to bootstrap tasks (or import as CSV).

## Columns
- Task ID (text)
- Task (title)
- Channel (select)
- Phase (select)
- Owner (person)
- Status (select)
- Priority (select)
- Due Date (date)
- KPI Target (text)
- KPI Result (text)
- Link (url)
- Notes (text)

## Suggested Select Values
- Channel: LinkedIn, Instagram, Meta Ads, X, Facebook, Google Search, Email, Website
- Phase: Phase 0, Phase 1, Phase 2, Phase 3, Phase 4
- Status: Backlog, Ready, In Progress, Blocked, Done
- Priority: P0, P1, P2

## Seed Rows
| Task ID | Task | Channel | Phase | Owner | Status | Priority | Due Date | KPI Target | KPI Result | Link | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| MKT-001 | Confirm tracking IDs + consent gating | Website | Phase 0 | Alan | Backlog | P0 | | GA4 + pixels verified | | | Requires dev support |
| MKT-002 | Lock primary CTA + pricing copy | Website | Phase 0 | Alan | Backlog | P0 | | CTA consistent | | | Use playbook wording |
| MKT-003 | Produce 2 proof assets (before/after + testimonial) | LinkedIn | Phase 0 | Alan | Backlog | P0 | | 2 assets ready | | | Use real outputs |
| MKT-004 | Build 4 UTM links for Day 1 | LinkedIn | Phase 1 | Alan | Backlog | P0 | | 4 links live | | | Use campaign slug |
| MKT-005 | Schedule Day 1 posts (LI + IG) | LinkedIn | Phase 1 | Alan | Backlog | P0 | | Posts scheduled | | | Peak times |
| MKT-006 | Launch LI organic cadence (Week 1) | LinkedIn | Phase 1 | Alan | Backlog | P1 | | 4-5 posts/week | | | Use playbook copy |
| MKT-007 | Launch IG organic cadence (Week 1) | Instagram | Phase 1 | Alan | Backlog | P1 | | 3-4 posts/week | | | Repurpose LI assets |
| MKT-008 | Set up daily KPI logging | Website | Phase 1 | Alan | Backlog | P1 | | Daily log maintained | | | Use tracking sheet |

## Suggested Views
- This Week (filter Due Date in next 7 days)
- Paid Only (Channel contains Ads)
- Blocked (Status = Blocked)
- Done (Status = Done)
