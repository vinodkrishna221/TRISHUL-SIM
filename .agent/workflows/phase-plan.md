---
description: Generate per-track phase plans made of cold-startable task cards with the cross-track dependency map.
argument-hint: "[optional: single track to (re)plan, e.g. fe]"
---

# /phase-plan — Per-Track Phase Plans

Persona: Orion. Skill: `.agents/skills/phase-planner/SKILL.md` (the task-card format lives
there — follow it exactly). Inputs: PRD, ARCHITECTURE, SCHEMA, contracts, BUILD-PLAN.
Outputs: `plans/<track>-phase-plan.md` per track from the blueprint ($1 restricts to one track).

## Run

1. For each track: generate P0 Skeleton / P1 Core / P2 Integration / P3 Polish cards per
   the skill. Every card: CONTEXT (cold-startable!), DELIVERABLE, TEST, DEPENDS/UNBLOCKS,
   DEMO, PERSONA, timebox ≤4h.
2. **Cross-track pass** (the step humans skip and regret):
   - Emit the dependency DAG at the top of each plan (which cards gate which, across tracks)
   - Verify no two cards touch the same file surface → split/re-sequence now
   - Verify every P0/P1 cross-track dependency has a mock path (FE on contract mocks, BE on
     contract-shaped fakes) — waiting is never a plan
3. **Load ledger**: per-track timebox sum ≤70% of that builder's hours (TEAM.md). Over →
   cards move to the cut list NOW, pre-ranked, not at hour 30.
4. **Oracle pass**: which cards carry the wow moment? Mark them `⭐ never-cut`. Which P3
   cards buy the most rubric points? Rank them.
5. Human review: read the P0 list + DAG aloud (5 min), adjust, commit:
   `docs: phase plans — <tracks>`.

## Gates

- The cold-start test on five random cards: could a fresh OpenCode session execute this
  card from the repo alone? Rewrite CONTEXT until yes.
- P0 across all tracks sums to a real walking skeleton (trace the thread through the cards).
- QUICK mode: one combined `plans/phase-plan.md`, P0+P1 only, ≤12 cards.
- End state: plans an agent can execute without asking a human → `/missions`.
