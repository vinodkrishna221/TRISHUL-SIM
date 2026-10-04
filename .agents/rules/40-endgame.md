# Endgame Doctrine (activates at the final quarter / T-6h)

Field data: teams go feral in the last hours no matter what the rules say — so the
degraded mode IS the doctrine. Fully feral kills demos; fully ceremonial kills features.
This file is the law between them. Where it conflicts with core rules after T-6h, THIS wins.

## Review tiering (replaces universal fresh-session review at T-6h)

| Tier | What | Review |
|---|---|---|
| **A — always full ceremony** | anything touching contracts, schema, data, deploy config, auth, flags | fresh-session Warden, six gates, no exceptions, to the last minute |
| **B — fast lane** | demo-path polish: CSS, copy, layout, seed tweaks, prompt wording | self-review against the card + smoke test + no console errors |

If a "polish" change touches state, shapes, or config, it's Tier A — the classifier is
what it TOUCHES, not how small it feels.

## The freeze calendar (authoritative — Patch enforces without apology)

- **T-5h · risky-feature freeze** — anything not yet demoable gets flagged off or cut now
- **T-4h · demo video scripted · T-3h · recorded** — rendering + upload ALWAYS overrun,
  and half the platforms require the video; recording is not a T-1h activity
- **T-3h · feature freeze** — the pre-ranked cut list executes without debate
- **T-2h · submission live · merge freeze** — fixes only, through the tiering above
- **T-90m · deploy freeze** — free-tier build queues spike 15+ minutes when every team
  deploys at once; the last deploy TRIGGERS by T-90m. A pre-warmed duplicate project
  (same code, second URL, deployed at T-4h) stands by as the fallback target
- **T-60m → 0 · quiet cockpit** — pre-warm ritual, tabs staged, no "one tiny fix"

## Standups switch to event-driven

Scheduled standups stop; triggers take over — phase end, anything red, a timebox breach,
an eval touchpoint. Overnight blocks with half the team asleep are ritual theater; the
block is for the humans awake, written where the next shift will read it.

## Load reality

Plan the final third of any event at **50% capacity** — timebox math done at hour 2
lies about hour 28. The cut list absorbs the difference; that's what it's for.

## The human layer (the crashes no linter catches)

- **No irreversible decisions solo between 3–5am** — schema rewrites, pivots, force
  pushes need a second awake human, or they wait 20 minutes for one.
- **The freshest person demos, not the founder-est.** Decide at T-3h, out loud.
- **Scope-argument ritual:** two humans deadlocked = 15-minute timebox → each states
  their case in 2 minutes → lead decides → 3-line DECISIONS entry → nobody relitigates.
  The argument you don't timebox costs more than either option on the table.
- Post-skeleton, **working code is sacred**: no "cleanup" refactors of anything green
  without a card. The graveyard is full of demos killed by hour-30 tidiness.
