---
description: The 2-hour pre-event mini-hack — every teammate runs the full WarRoom loop once BEFORE it counts. Kills the first-event learning tax.
argument-hint: "[optional: a past problem statement to drill on]"
---

# /drill — The Dress Rehearsal Sprint

Field data: first-time WarRoom teams run at ~70% because nobody reads 79 files under
adrenaline. The fix is not more docs — it's two hours of muscle memory, days before the
event. After a drill, teammates never need to read anything beyond START-HERE.md and
their mission file.

## Setup (lead, 10 min before)

Fresh clone → `./scripts/new-hackathon.sh drill-run team <2h from now>` → fill TEAM.md
with the real roster → everyone runs `scripts/sync.sh` on THEIR machine and confirms
their tool sees personas + commands (this alone catches half of day-zero problems).

## The two hours

1. **h0:00 — Gauntlet-lite (20m):** a past/fake PS ($ARGUMENTS or pick one). Ban list +
   ONE divergence lens + pick. Goal is rhythm, not the idea.
2. **h0:20 — Kickoff QUICK (15m):** one-page PRD, out loud, vocabulary frozen.
3. **h0:35 — Drill cards (10m):** Orion writes 1 tiny card per member against a trivial
   app (a page, an endpoint, a seed script, a deck slide — non-devs drill their real
   seats). Every card: CONTEXT, DELIVERABLE, TEST, timebox 25m.
4. **h0:45 — The loop, for real (45m):** each member: branch → summon their persona →
   build the card → PR with the template → **fresh-session review of SOMEONE ELSE'S PR**
   (cross-review teaches both sides) → merge in DAG order → smoke.
5. **h1:30 — One /eval-round mock (15m):** lead plays judge for 5 minutes; team runs the
   T-60m ritual compressed + feedback triage on what the "judge" said.
6. **h1:45 — Drill retro (15m):** what confused whom → fix it NOW (TEAM.md notes, a
   clarified rule line, tool quirks logged). Every confusion fixed here is 20 minutes
   saved at hour 2 of the real event.

## Definition of done

Every member has personally done card → branch → PR → review → merge once · the lead has
driven ideate/kickoff/eval-round once · all three tools booted with the kit on every
machine that will attend · session-kill practiced once (kill a session mid-card and
cold-start it from the card — the skill that makes agent-drift a non-event).

Run once per NEW team composition; a 30-minute refresher before big events. This is the
kit's onboarding — treat skipping it as the scope-guard treats skipping the skeleton.
