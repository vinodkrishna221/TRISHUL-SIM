---
description: Scan git + plans + board → status report, rubric re-score, and a paste-ready team update. Run every 3-4 hours.
---

# /standup — Status Pulse

Personas: any session can run it; Oracle contributes the re-score. Cadence: **2 scheduled
per day-phase minimum (plus one 90m before any eval touchpoint) + event-driven triggers** —
phase end, anything red, a timebox breach. Overnight with half the team asleep, skip the
theater: the block is for the humans awake, written where the next shift reads first.
Cost: ≤10 minutes. Outputs: updated
`board/STATUS.md` + a paste-ready block for team chat.

## Run

1. **Scan, don't ask:** `git fetch && git log --oneline --all --since=<last standup>` ·
   open PRs + their age · `plans/*` checkbox deltas · `missions/*` progress · current
   deploy green? (hit the health URLs) · blockers section aging?
2. **Update `board/STATUS.md`:** per track — done since last / in flight / next up;
   Blockers (owner + age — anything >45m gets escalated per advisor protocol); Cut List
   deltas; clock vs skeleton deadline.
3. **Oracle re-score** (skill judge-lens mode 1) on what runs on main NOW → the scorecard +
   `FIX-THIS-HOUR` line. Trending-down criterion 2 checks running = Direction Check in the
   report, bold.
4. **Skeleton check** (until it walks): % of the walking-skeleton thread green. Past the
   30% deadline and not walking → this standup CONVERTS into `/pivot`, automatically, now.
5. **Emit the paste-ready block:**

```
⚔️ STANDUP <hour N>/<total>
Skeleton: 🟢 walking | 🟡 <what's left> | 🔴 PIVOT CALLED
Tracks: fe <done>/<total> cards · be … · data … · ai …
Merged since last: <n> PRs · Deploy: 🟢/🔴 <url>
Rubric: I x T x Im x P x → <weighted> (<trend>)  FIX-THIS-HOUR: <the move>
Blockers: <owner: thing, age> | none
Next sync: <time>
```

Solo mode: same block goes at the top of `missions/QUEUE.md` — it's the note your next
session (in whichever tool) reads first. It doubles as devlog material for the submission.
