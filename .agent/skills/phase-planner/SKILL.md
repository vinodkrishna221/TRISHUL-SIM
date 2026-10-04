---
name: phase-planner
description: Turn PRD + architecture into per-track phase plans made of agent-executable task cards with context, deliverable, test, dependencies, timebox, and demoable moment. Use during /phase-plan.
---

# Phase Planner

Purpose: plans an agent can execute without asking a human anything. Run by Orion after
/blueprint. Template: `.agents/templates/phase-plan.md` → outputs
`plans/<track>-phase-plan.md` (tracks from ARCHITECTURE: typically fe, be, data, ai, ship).

## Phase spine (every track, same four phases)

- **P0 Skeleton** — the track's slice of the walking skeleton (due: 30% time, hard)
- **P1 Core** — the MUST scope; the wow moment's machinery
- **P2 Integration** — swap mocks→live, contract verification, cross-track joins
- **P3 Polish** — demo-path states, seed story, wow tuning (everything here is pre-cut-listed)

## The task card (the atom of WarRoom — agents live on these)

```
### <TRACK>-<n> · <verb-first title>                        [timebox: 30m|1h|2h|4h]
CONTEXT: why this exists; what state the repo is in when you start; pointers
         (files, contracts, rules to load) — enough that a COLD agent session can start
DELIVERABLE: the artifact — file(s)/endpoint/screen that exists after
TEST: the ONE check that proves it (command to run, or click-path + expected result)
DEPENDS: <card ids or —>          UNBLOCKS: <card ids or —>
DEMO: what the judge sees because this card happened (or "plumbing for <card>")
PERSONA: <who executes>           STATUS: [ ] todo
```

## Rules

- **4h is the max timebox.** Bigger = split until it fits. 30m cards are legitimate.
- **CONTEXT is written for a cold session** — mission handoffs and worktree parallelism
  depend on cards that carry their own context. Test: could OpenCode with zero chat history
  execute this card from the repo alone?
- **P0 cards are mock-shaped:** FE builds on contract mocks, BE returns contract-shaped
  fakes — tracks never wait on each other in P0/P1 (the swap is an explicit P2 card).
- **Dependency map:** after writing all tracks, emit the cross-track DAG (which cards gate
  which) at the top of each plan + flag any two cards touching the same file surface
  (= mission-split bug, fix now).
- **Load ledger:** sum of timeboxes per track ≤ 70% of that builder's realistic hours —
  the other 30% is integration friction and sleep. A plan at 100% is a plan to fail
  publicly. **Plan the final third of the event at 50% capacity** — field data: load
  holds to ~hour 18, halves overnight, and timebox math done at hour 2 lies about hour 28.
- QUICK mode: one combined plan file, P0+P1 only, ≤12 cards total.
