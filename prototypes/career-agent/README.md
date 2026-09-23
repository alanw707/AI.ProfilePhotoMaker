# Connected career workspace prototype

Status: interactive **prototype**, not the authenticated career product. Six linked pages use a fictional operations professional. Every pay figure and location value is illustrative. The right assistant has scripted responses and no model or market feed.

## Open

From the WSL worktree root:

```sh
python3 -m http.server 4311 --bind 127.0.0.1
```

Then open [the local preview](http://127.0.0.1:4311/prototypes/career-agent/). If another service owns 4311, choose a free port. Assets are served from the repository root because the existing brand logo is referenced. Data stays in browser sessionStorage. Use the browser's session storage clear action to restore the fictional starting point.

## Review journey

1. Career agent: read current goal, inspect a useful next action, and preview empty, working, needs-input, partial, stale, source-error and quota states.
2. Career profile: propose a resume-derived title change, inspect provenance, accept or dismiss, and edit role/location/time preferences.
3. Career analytics: explain the fictional occupational wage interval and why a personalized pay interval is unavailable.
4. Career heatmap: search and compare two locations, switch between list and schematic map on mobile, and save a selected destination.
5. Career roadmaps: choose closest fit, higher ambition or steadier transition; adjust available time and complete a task.
6. Career materials: review a fictional resume and summary, save an edit, download a plain-text example, and see the optional photo handoff.

The photo button demonstrates a handoff message only. No account, upload, real checkout, PDF/DOCX export or paid operation is implemented in this prototype.

## Five-task target-user review

Observe an experienced U.S. professional without explaining the UI:

| Task | Successful evidence |
| --- | --- |
| Correct an extracted profile fact | Finds the proposal, understands provenance, accepts or dismisses correctly. |
| Explain the pay range | Says it is a fictional occupational benchmark, not a personal salary promise; identifies why personalized pay is unavailable. |
| Compare two eligible markets | Finds the table, chooses two places, notices relocation and unknown remote eligibility. |
| Select and edit a roadmap | Can choose a path, adjust time and mark a task complete. |
| Prepare materials without buying photos | Edits/downloads the text sample and understands the photo step is optional. |

Record task success, errors, hesitation and direct quotes with the participant's consent. The researcher must distinguish prototype limitations from comprehension failures. **No target-user sessions have been conducted yet.** This remains the human validation gate of ticket #377.

Use the [anonymized observation template](review/session-template.md) for each session, then summarize the counts, severe failures, fixes and retests in the ticket. Keep participant contact details and any real career data outside this repository.

If no participant is available, the [opt-in LinkedIn recruitment draft](review/linkedin-recruitment-draft.md) is ready for the account owner to review. It has **not** been posted or sent to anyone.

## Technical verification

Run the existing Playwright package against the prototype with:

```sh
NODE_PATH=/home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.UI/node_modules node prototypes/career-agent/smoke.cjs
```

The smoke script visits all six pages, tests the profile proposal, location selection, roadmap task, materials download and mobile assistant, captures desktop/mobile review images, checks mobile overflow and reports JavaScript page errors. It also checks focus and scroll restoration on route history, visible keyboard focus and assistant focus return, 47 representative text/background contrast pairs across six pages (lowest measured ratio 4.6:1), reduced-motion styling, and reflow at a 640-CSS-pixel viewport with device scale factor 2 (a 1280-physical-pixel, 200%-scale equivalent). This is not a native browser-zoom, exhaustive contrast, or assistive-technology audit. It uses `CHROME_BIN` if the local Chrome binary differs.

Current review captures are under `review/`. They were generated from the WSL browser pass; inspect them before treating any visual decision as complete. This prototype has not been merged into the production Angular routes.

## Known handoff boundaries

- The simplified geography drawing is a layout sketch. Production heatmap requires validated U.S. state/metro boundaries and accessible table parity.
- Source and pay displays use fictional numbers until the evidence pipeline passes ticket #383 and subsequent implementation tickets.
- Browser session persistence demonstrates navigation recovery, not authenticated, durable account storage.
- The prototype uses existing logo and a separate career design record. Product implementation must reconcile shared site shell and final branding rules.
