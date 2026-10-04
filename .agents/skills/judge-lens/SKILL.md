---
name: judge-lens
description: Score ideas or the current build against the hackathon's judging rubric, simulate judge Q&A, and engineer the demo wow moment. Use at /ideate, every /standup, /integrate, and before /ship.
---

# Judge Lens

Purpose: keep the build pointed at what is SCORED, continuously — not just at ideation.
Run by Oracle.

## Mode 1 — Rubric scorecard (ideas or current build)

Score per criterion, 1-10, one line of evidence each. For the BUILD, score only what runs
on `main` right now — plans score zero.

```
RUBRIC SCORECARD — <idea | build @ hour N>
Innovation      x/10 — <evidence>
Technical       x/10 — <evidence: what a judge can SEE running>
Impact/fit      x/10 — <evidence: number, user, sponsor-API load-bearing?>
Presentation    x/10 — <current demo-ability, story, visuals>
Weighted        X.X   Trend vs last score: ↑ ↓ →
FLAG: <criterion trending down 2 checks in a row → Direction Check>
FIX-THIS-HOUR: <the single change that buys the most points>
FIRST-90s: <what a tired judge SEES and FEELS in the demo's first 90 seconds — scored
separately. Field data: judges score vibes in minute one and backfill the rubric after.>
```

`FIX-THIS-HOUR` is the point of the exercise — always the cheapest point-buying move
(usually: make invisible tech visible; sharpen the opening beat; kill a janky screen).

## Mode 2 — Wow-moment engineering (once at blueprint, protect forever)

Pick ONE 10-second beat people retell: live voice answering · agent visibly acting across
tools · realtime update on a second screen/phone · impossible speed · before/after that
stings. Criteria: happens in <15s, works with seeded data, survives bad Wi-Fi (or has a
recorded twin), demonstrably REAL (tool calls / data visibly move). Write it into
IDEA-BRIEF and DEMO-SCRIPT; it lives on the never-cut list; the demo lands it twice.

## Mode 3 — Mock judge panel (at /ship, 15 min)

Generate the 8 questions THIS panel asks (adapt to sponsor + domain):
1. How is this different from <the obvious existing product>?
2. What's real vs mocked in what we just saw?
3. Data privacy/safety — especially anything user-generated or AI-generated
4. Who pays / what's the business? (even at student hackathons)
5. Why this stack / what breaks at 10× users?
6. What was the hardest technical part? (they're probing for depth)
7. What would you build next with a month?
8. <sponsor> — why is our platform load-bearing here?

Drill each answer to ≤20 seconds, one concrete artifact per answer (number, screen, commit).
Honesty rule: mocked parts get disclosed CRISPLY before they're discovered.

**Judge dossier (any event, 5 minutes):** who actually judges — a VC rewards nothing a
professor rewards, a sponsor engineer rewards neither. One LinkedIn pass per judge →
one line each: background, what they'll probe, what impresses them. Adjust the question
bank and the pitch's cred-section accordingly.

**Pre-empt the weakness (generalized from the viva dossier):** name your obvious flaw
BEFORE Q&A — "you're probably wondering about X; here's our answer" — delivered in the
pitch's cred beat. Disarming the killer question outscores surviving it.

## Mode 4 — Rival simulation (shared problem statements: SIH finals, themed tracks)

When 4-5 teams build the SAME problem statement in the same room, differentiation pressure
returns at the finale. Run the gauntlet's consensus map AGAINST YOUR PS at build time:
predict what the rival teams most likely built (the modal approaches to this exact PS),
then sharpen: which of our beats do they almost certainly NOT have? Output: one
"unlike the other approaches" line for Herald's deck + the demo beat that proves it,
scheduled early in the demo. Delivered as confidence, never as comparison — judges score
teams that know their edge, and penalize teams that trash-talk. Re-run before E3; rivals
visible across the room may have pivoted.

## Judge psychology (apply everywhere)

30+ demos/day → they remember openings, wow beats, failures. Claims without visible artifacts
read as fiction. Smooth-90% beats crashing-100%. The first 20 seconds are the problem in
human terms — never the stack. Three retellable phrases = you existed after you left the room.
