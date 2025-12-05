# UX Acceptance Addendum (No Formal UX Doc)

Scope: Provide baseline UX/accessibility/responsive criteria to apply across stories in `bdocs/epics.md` without a full UX design spec.

## Global UX Criteria
- Accessibility: All form controls have labels/ARIA, visible focus states, keyboard navigation, no keyboard traps.
- Responsive: Support desktop/tablet/phone breakpoints with readable layout, no horizontal scroll on mobile; images scale with aspect ratio preserved.
- Validation: Client-side validation mirrors API rules; inline errors adjacent to fields; summaries for forms with multiple errors.
- States: Loading spinners/skeletons for network calls; empty states with guidance; error states with retry/info; success confirmations.
- Buttons/Inputs: Primary/secondary styles consistent; disabled states non-interactive; destructive actions require confirmation.
- Messages: Plain-language errors, avoid leaking technical details; follow security guidance (no PII in messages).

## Flow-Specific Notes
- Auth: Show password requirements and strength feedback; rate-limit feedback uses generic messaging; OAuth errors redirect with safe copy.
- Uploads: Drag/drop + picker; show per-file validation errors; progress/complete indicators; reject oversize/invalid types with specific reasons.
- Gallery: Thumbnails with lazy load; delete confirms; empty gallery guidance; download uses accessible links/buttons.
- Training/Generation: Show job status/pending/complete; surface remaining credits; handle model-unavailable errors clearly.
- Payments: Communicate simulation vs live; show credit deltas on success; error/retry guidance for declined/failed intents.
- Retention: Display retention windows (7/30 days) in UI where relevant; warn before destructive actions.

## Application to Stories
- Apply Global UX Criteria to all UI-facing stories.
- Add flow-specific notes to related stories (Auth, Uploads/Gallery, Training/Generation, Payments, Retention flows).

