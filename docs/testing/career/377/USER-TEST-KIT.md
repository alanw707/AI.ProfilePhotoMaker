# Career workspace — independent user test kit (#377)

**Status: not yet run.** Every walkthrough so far was coached by the owner, so none of it counts as
target-user evidence. This kit is what an observer needs to run the missing sessions. It cannot be
run by an agent: it needs real people.

## Who to recruit

Five people, one at a time, about 40 minutes each. Five is enough to surface the common problems.

- Experienced U.S. professionals (roughly 5+ years of work) who are seriously considering a different
  role or field in the next 12 months. This is the primary user in `PRODUCT.md`.
- Not friends who have already seen the product, and nobody who works in software design or testing.
- Aim for a mix: at least two on a phone, at least one changing field entirely, at least one whose
  target role is uncommon.
- They use their own real background. Tell them beforehand that they can delete everything afterwards
  (Privacy and your data → delete).

## Setup

- Production site, a fresh account per participant, their own device.
- Screen and voice recording with consent. If they decline recording, take notes only.
- One observer. A second person may take notes but does not speak.
- Each participant uses at most a handful of assistant drafts; the monthly allowance (20) and the cost
  caps are enough for five sessions.

## Observer rules

1. Read the task aloud, then stop talking.
2. Do not explain the product, point at the screen, or answer "what does this mean?". Reply: "What
   would you expect it to mean?"
3. If they are stuck for 2 minutes, ask "What are you looking for?". If still stuck after 1 more
   minute, mark the task failed, give the answer, and move on.
4. Write down their words, not your interpretation.

## Opening script

> "Thanks for doing this. We are testing the product, not you; nothing you do is wrong. Please think
> out loud. I will stay quiet and I can't help while you work, because we need to see where the site
> is unclear. Stop any time. Afterwards you can delete everything you entered."

Then: "Before you click anything, look at this page for a few seconds. What is this site for, and who
is it for?" (Record the answer word for word. This tests the homepage positioning.)

## Tasks

| # | Say this | Counts as success | Give up after |
|---|---|---|---|
| 1 | "You are thinking about a career change. Use this site to get started with your own background." | Reaches the career workspace and saves a profile (typed or from a resume) without help | 8 min |
| 2 | "Something in your profile is wrong or missing. Fix it." | Edits a fact, saves, and notices what was marked as needing review | 4 min |
| 3 | "Tell the site what role you want next, and where." | Saves a goal and confirms an occupation match they agree with | 6 min |
| 4 | "What does that role pay where you want to work? Explain the numbers to me." | States a range **and** correctly says where it comes from (government wage data for the occupation, not a personal prediction, not live job ads) | 6 min |
| 5 | "Would another city or state be better for this role? Find out." | Compares at least two places and names one difference | 5 min |
| 6 | "What should you do in the next few weeks to move toward this role?" | Gets a roadmap, changes or ticks off one step | 6 min |
| 7 | "Get a resume or summary you could send for this role." | Produces a draft, edits it, and exports or copies it **without** being pushed to buy photos | 6 min |
| 8 | "You are done for today. Come back tomorrow: where would you pick up?" (Have them sign out and in.) | Names their next step from the home page within 30 seconds | 2 min |

## Closing questions

1. "In one sentence, what does this product do?"
2. "What was the most confusing moment?"
3. "Did you trust the numbers? Why or why not?"
4. "Would you come back next week? What for?"
5. "What would you expect to pay for this, if anything?" (Career pricing is undecided; record the answer, promise nothing.)
6. "Was anything missing that you expected to find?" (Listen for job listings; the Jobs page is currently hidden.)

## Observation sheet (one per participant)

```
Participant: P_   Date:        Device: phone / laptop   Current role → target role:
First-impression answer ("what is this site for?"):

Task | Done alone? (Y / with prompt / N) | Time | Where they got stuck | What they said
1    |                                   |      |                      |
2    |                                   |      |                      |
3    |                                   |      |                      |
4    |                                   |      |                      |
5    |                                   |      |                      |
6    |                                   |      |                      |
7    |                                   |      |                      |
8    |                                   |      |                      |

Misunderstandings (anything they believed that is false):
Errors or broken states seen (time, page, what happened):
Closing answers 1–6:
```

## How to read the results

- **Launch-blocking:** a task that 2 or more of 5 fail alone; any participant who believes the pay
  figures are a personal salary prediction (task 4); anyone who thinks they must buy photos (task 7);
  any data loss or error page.
- **Fix soon:** a task that needs a prompt for 2 or more of 5; the same confusion voiced by 2 or more.
- **Note only:** single-participant preferences.

Record the five sheets and a one-page summary next to this file (`docs/testing/career/377/RESULTS.md`).
The Stage 3 readiness report makes promotion of the workspace conditional on this result.
