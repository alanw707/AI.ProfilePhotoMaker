# Connected career workspace prototype

Status: interactive **prototype**, not the authenticated career product. Six linked pages use a fictional operations professional. Every pay figure and location value is illustrative. The right assistant has scripted responses and no model or market feed.

## Open

From the repository root:

```sh
python3 -m http.server 4311 --bind 127.0.0.1
```

Then open [the local preview](http://127.0.0.1:4311/prototypes/career-agent/). If another service owns 4311, choose a free port. Assets are served from the repository root because the existing brand logo is referenced. Data stays in browser sessionStorage. Use the browser's session storage clear action to restore the fictional starting point. The task-3 wizard uses a new session key (`career-prototype-v3`), so older fictional sessions (the v1 save that overwrote home, and the v2 table layout) reset to the starting point.

## Review journey

1. Career agent: read current goal, inspect a useful next action, and preview empty, working, needs-input, partial, stale, source-error and quota states.
2. Career profile: propose a resume-derived title change, inspect provenance, accept or dismiss, and edit role/location/time preferences.
3. Career analytics: explain the fictional occupational wage interval and why a personalized pay interval is unavailable.
4. Market comparison (still at the `#heatmap` URL) is a four-step wizard: (1) confirm or change the **home market** (the same value as the profile goal); (2) choose up to three places from a searchable list, which light up in the fictional wage chart; (3) compare home and the chosen places side by side as cards plus a chart of only those places, with the example midpoint difference from home and a relocation label; (4) pick one as the **target market** and see a before/after confirmation (target changed, home unchanged, brief out of date, no new analysis). Completed steps stay reachable from the step bar. This replaced the table-and-chart layout after the owner retest found that Compare and Save target market had no visible effect.
5. Career roadmaps: choose closest fit, higher ambition or steadier transition; adjust available time and complete a task.
6. Career materials: review a fictional resume and summary, save an edit, download a plain-text example, and see the optional photo handoff.

The photo button demonstrates a handoff message only. No account, upload, real checkout, PDF/DOCX export or paid operation is implemented in this prototype.

## Five-task target-user review

The owner elected to run the first walkthrough personally. Use the [owner walkthrough](review/owner-walkthrough.md) and record its findings as an **owner test**, not as an independent participant session. Owner feedback can fix the prototype, but it does not by itself satisfy the original representative-user observation criterion.

Observe an experienced U.S. professional without explaining the UI:

| Task | Successful evidence |
| --- | --- |
| Correct an extracted profile fact | Finds the proposal, understands provenance, accepts or dismisses correctly. |
| Explain the pay range | Says it is a fictional occupational benchmark, not a personal salary promise; identifies why personalized pay is unavailable. |
| Compare two eligible markets | Follows the wizard, chooses two places, reads the side-by-side cards, saves a target and sees home unchanged; notices relocation and unknown remote eligibility. |
| Select and edit a roadmap | Can choose a path, adjust time and mark a task complete. |
| Prepare materials without buying photos | Edits/downloads the text sample and understands the photo step is optional. |

Record task success, errors, hesitation and direct quotes with the participant's consent. The researcher must distinguish prototype limitations from comprehension failures. **No target-user sessions have been conducted yet.** This remains the human validation gate of ticket #377.

Use the [anonymized observation template](review/session-template.md) for each session, then summarize the counts, severe failures, fixes and retests in the ticket. Keep participant contact details and any real career data outside this repository.

If no participant is available, the [opt-in LinkedIn recruitment draft](review/linkedin-recruitment-draft.md) is ready for the account owner to review. It has **not** been posted or sent to anyone.

## Technical verification

Run the existing Playwright package against the prototype with:

```sh
NODE_PATH="$PWD/AI.ProfilePhotoMaker.UI/node_modules" CHROME_BIN="$(command -v chromium || command -v google-chrome)" node prototypes/career-agent/smoke.cjs
```

The smoke script visits all six pages, tests the profile proposal, the four-step market wizard (step focus, no-place block, three-place limit, side-by-side differences, before/after confirmation, stepper back-navigation), separate home and target markets, home edited in step 1 or on the profile, save/reload persistence, disabled save with no selected places, relocation labels after home-market edits, roadmap task, materials download and mobile assistant, captures desktop/mobile review images, checks mobile overflow and reports JavaScript page errors. It also checks the fictional interval graphics, chart/search/selection synchronization, navigation indicator position, roadmap milestone line, focus and scroll restoration on route history, visible keyboard focus and assistant focus return, sampled text/background contrast pairs across six pages (minimum measured ratio reported by the run), reduced-motion styling, and reflow at a 640-CSS-pixel viewport with device scale factor 2 (a 1280-physical-pixel, 200%-scale equivalent). This is not a native browser-zoom, exhaustive contrast, or assistive-technology audit. It uses `CHROME_BIN` if the local Chrome binary differs.

Current review captures are under `review/`. They were generated from the WSL browser pass; inspect them before treating any visual decision as complete. This prototype has not been merged into the production Angular routes.

## Known handoff boundaries

- The prototype deliberately has no geography drawing or remote-work filter: a real map would require validated boundaries, evidence tied to geography, and accessible table parity. Remote eligibility needs actual job-listing restrictions; it cannot be inferred from the fictional wage benchmark.
- Source and pay displays use fictional numbers. For the first career release, the owner selected a separately labeled BLS occupational benchmark and deferred personalized advertised-pay estimates. Real values require verified BLS ingestion, occupation/geography matching, suppression handling and source metadata; this prototype has none of those yet.
- Browser session persistence demonstrates navigation recovery, not authenticated, durable account storage.
- The prototype uses existing logo and a separate career design record. Product implementation must reconcile shared site shell and final branding rules.
