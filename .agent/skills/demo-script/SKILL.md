---
name: demo-script
description: Produce the timed, rehearsable demo script plus pitch structure and submission checklist. Use during /ship (draft the moment the walking skeleton works).
---

# Demo Script

Purpose: the demo is a performance with a single take. Script it like one. Run by Herald
(script + story) with Oracle (wow placement + Q&A) and Patch (flags + insurance). Output:
`docs/DEMO-SCRIPT.md`. First draft the day the skeleton walks; final at /ship.

## Structure of the 3 minutes (adjust to the actual limit, keep the ratios)

```
0:00-0:20  THE OPEN — the problem in human terms; the spine sentence; NO stack, NO team intro
0:20-0:40  wow moment #1 (early! judges decide fast) — the 10-second beat, named
0:40-2:10  THE LOOP — the core user journey, 4-6 beats, each: [click] + [line spoken over it]
2:10-2:30  THE CRED — 15 seconds of "how": the sponsor API load-bearing, the real numbers
           (latency, rows, calls), one honest "this part is simulated"
2:30-3:00  THE CLOSE — wow moment again from a new angle + impact number + the ask/next
```

## Script format (per beat)

```
BEAT n [t=0:40]  WHO: <driver>   SCREEN: <route/state>
CLICK: <exact action>            SAY: "<the line, verbatim, ≤2 sentences>"
FALLBACK: <if it hiccups: the line + the recovery click>
```

## Hard rules

- Written against SEEDED data + known-good flag set (from Patch) — never against "whatever
  state the DB is in".
- Rehearse 2× out loud, timed, before pitching; script trimmed to finish 20s under the limit
  (venue clocks are hostile; being cut off mid-close is a self-inflicted wound).
- Every SAY line ≤2 sentences; silence over a working screen beats narration over a spinner.
- The driver never apologizes on stage. Hiccup → FALLBACK line, keep moving; the backup
  video exists (Patch) if it truly dies.
- One team member drives, one talks (solo: script the pauses — click, THEN speak).

## High-scoring beats (engineer these in — field data from winners)

- **The judge-hands-on beat:** whenever the product can survive it, hand a judge the
  phone / have them scan a QR and watch THEIR input appear on screen. Held beats watched —
  it's a different grade of attention. Design it, seed it, rehearse the recovery if they
  type something weird.
- **The personal-stake line (mandatory spine slot):** one sentence of why THIS team —
  "my grandmother actually has this problem" outscores an architecture slide. Real ones
  only; judges smell manufactured stakes.
- **Pre-empt the weakness:** the cred beat names your obvious flaw before Q&A does
  (judge-lens has the format).
- **Demo-video-early law:** script the backup video at T-4h, record at T-3h — rendering
  and upload always overrun, and half the platforms require the video. A T-1h recording
  plan is a missing-video plan.

## Submission checklist (same file, bottom — done by T-2h, portals crash at T-15m)

Title with the retellable phrase · 3 screenshots or hero GIF · 60-90s backup video linked ·
spine paragraph · 3 bullets REAL (not aspirational) · stack diagram image · team + repo
link · sponsor tech named explicitly (search-keyword-friendly for sponsor-prize sweeps).
