# Owner walkthrough for ticket #377

Use the [local WSL preview](http://127.0.0.1:4311/prototypes/career-agent/) in a desktop browser; repeat the market comparison and assistant on a narrow/mobile window if convenient. This is a fictional, browser-session-only prototype. It does not need your resume, real pay, account details, or a purchase. Keep this task list separate from the UI while attempting each task, and write down the first place you hesitate before looking at the success cues below.

## Five tasks, without hints

1. The fictional profile extracted a fact incorrectly. Find the proposed change, check where it came from, and either accept or dismiss it. Then change the work-location preference.
2. Find the pay range. In your own words, what population and time period does it describe? Would you use it as your personal salary target? Find out why a personalized range is unavailable.
3. Compare Denver and Seattle. Which place would require relocation for this fictional profile? Can you tell whether a remote job there would accept the person's current state? Save one destination.
4. Choose a career route, change the available time per week, and mark one roadmap step complete. Can you tell what changed and what did not?
5. Edit the example career material and download the text version. Can you finish without buying or generating a photo?

After each task, record: **completed / partly / stuck**, first confusing label or step, any mistaken conclusion, and what you expected instead. A screenshot is useful for a visual bug; do not include your personal resume, salary or contact information. Also note device/browser and whether you used keyboard, zoom or a mobile viewport.

## What the prototype is meant to communicate

- All shown dollar amounts are fictional. The occupational wage benchmark is not live BLS data and not a predicted personal offer. A future live benchmark must name its occupation, geography, reference period and source.
- Personalized advertised-pay analysis is deferred. No current posting feed is connected.
- Remote eligibility is unknown in this example; a higher benchmark does not prove an accessible job market.
- The assistant is scripted, not live AI research. Photos are optional and the button is a handoff demonstration, not checkout.

Please report misunderstandings even if you eventually found the right control. An owner walkthrough can reveal defects and test whether the flow matches the intended product, but it is not independent target-user validation. We will log your results honestly and decide whether a later external review is needed before production release.

## Recorded results

### Task 3 retest (2026-09-30, after commit 50269106): stuck

- Saving a target market had no visible effect beyond one line of small text and a toast.
- Home could not be set on the market page (it lived only in the Profile goal form), and Denver appeared in the compare list as if it were an option.
- Compare only highlighted a row in the range chart, often off-screen; it did not open a comparison.
- Owner direction: simplify into wizard steps, keep the charts for visual appeal.

**Change:** market comparison rebuilt as a four-step wizard (where you live → places to consider → side by side → pick your target) with a before/after confirmation. Charts kept: step 2 highlights chosen places, step 3 charts only home and chosen places. Retest pending in a fresh browser window.

Tasks 1, 2, 4 and 5: not yet recorded.
