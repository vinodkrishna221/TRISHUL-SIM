---
description: Structured descope-or-pivot decision when the skeleton misses its deadline or a core bet fails. 30 minutes, ends in a committed decision.
---

# /pivot — The Reality Checkpoint

Triggers (any one): skeleton not walking at 30% time (automatic, from /standup) · a core
technical bet failed (API dead, model unusable, data unavailable) · a task at 2× timebox on
the critical path · rubric score collapsed on a criterion. Personas: Orion chairs; Oracle
scores options; Maverick only if a fresh angle is needed; the HUMAN decides.
Budget: 30 minutes, hard. Output: DECISIONS entry + updated plans/missions.

## Run

1. **Freeze & face it (5m):** stop all building. One honest paragraph in chat: what was
   bet, what actually happened, hours remaining. No blame syntax — "the bet failed", not
   "X failed".
2. **Options table (10m)** — exactly three, no more:
   - **DESCOPE** — keep the idea, shrink to the smallest demoable loop (cut-list executes;
     which MUSTs become SHOULDs; what the demo loses)
   - **SWAP THE BROKEN PART** — same idea, different machinery (mock the dead API + disclose
     honestly in the pitch · swap provider/model · pre-computed instead of live)
   - **PIVOT THE WOW** — same build, different story: re-center the demo on what ALREADY
     works impressively (Maverick: 10 minutes, what's the best product whose skeleton is
     the code we have?)
   For each: hours to demoable · rubric delta (Oracle) · risk. Rebuild-from-scratch is not
   on the table after 30% time; do not present it.
3. **Decide (5m):** human picks. One Direction Check allowed if the room disagrees, then
   full commitment — a mediocre plan executed beats a perfect plan debated.
4. **Repoint (10m):** DECISIONS entry (the honest paragraph + choice) · PRD scope section
   edited · affected cards rewritten/cut · missions/QUEUE updated · STATUS banner:
   `PIVOTED @ hour N: <one line>` · fresh standup block posted. Stack-changing pivots:
   the chair may invoke `/fit-audit` manually (same diff-approval ritual — the only
   other sanctioned kit-edit moment).

## Rules

- A pivot session that ends without a committed decision is a failed pivot — the chair
  forces the pick at minute 25 by proposing the lowest-risk option.
- Post-pivot, the FIRST task is always: make the new skeleton walk (nothing else merges
  until it does).
- One pivot is a war story. Two is survivable. Three means QUICK-mode scope from here:
  smallest loop, polish, story — land the plane.
