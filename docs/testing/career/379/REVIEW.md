# #379 simulated UX review: resume import (Impeccable audit)

**Simulated agent review, not target-user evidence.**

## How it was produced
- Real stack, no mocks: `scripts/career-localdev-api.sh start` (LocalDev API with the dev-only NoThreatsScanner and the built-in parser) and `npm run dev:local`.
- `AXE_PATH=<axe.min.js> PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH=/usr/bin/chromium node tests/ux/career-import-review.mjs ../docs/testing/career/379` from `AI.ProfilePhotoMaker.UI/`.
- Synthetic, fictional PDFs built in memory: a two-page resume, and an empty text layer standing in for a scan.
- Steps: empty page → upload without consent → upload → review suggestions → accept one → profile → upload of an unreadable file → paste fallback. Captured at 1280, 390 and 320 px.

## Result
| Check | Result |
|---|---|
| axe WCAG 2.0/2.1/2.2 A+AA | 0 violations across 15 captures |
| Horizontal overflow | 0 px at every width |
| Touch targets < 44 px | none |
| One h1 per page | yes |
| Focus after processing | moves to the "Review suggestions" heading |
| Default selection | every suggestion starts unchecked; "Accept selected (0)" is disabled |
| Provenance after accept | profile shows "From your resume · confirmed …" and "Profile updated from your resume" |
| Unreadable file | "We could not read this file" plus a paste box; no OCR promise |
| Page errors | 0 |

## Findings
| Sev | Finding | Status |
|---|---|---|
| P2 | The "Years of experience: 8" suggestion quotes only one job line, although it is computed from all the date ranges. The excerpt should name the span. | Logged for the parser-selection follow-up |
| P2 | With 10 suggestions there is no "Select all in this group"; choosing many takes many taps. | Logged |
| P3 | The native file input is styled by the browser. | Logged |
| P3 | The progress text changes on timers while one synchronous request runs. It is honest (no percentage), but will become real states with the async worker (#380). | Logged |

No open P0/P1 findings.
