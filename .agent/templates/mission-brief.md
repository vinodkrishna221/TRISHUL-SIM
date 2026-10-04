# Mission — <MEMBER or PERSONA-RUN> · <mission name>

> Context capsule: this file + the repo = everything needed, in any tool, cold.
> ACCEPTED: [ ] <member ticks on read — unaccepted by next standup gets reassigned>

## The project in 30 seconds

<one-liner + demo narrative summary from PRD — 4 lines max>

## Your mission

**Goal (one sentence):** <…>
**Victory =** your demo beats green on seeded data · cards below checked · PRs merged.
**Branch pattern:** `feat/<track>-<card>-<slug>` · **Load these rules:**
`.agents/rules/stack/<…>.md` + core four.

## Your cards (verbatim from `plans/<track>-phase-plan.md`)

- [ ] <TRACK>-1 <title> [1h] → PR
- [ ] <TRACK>-2 <title> [2h] → PR
<!-- full card text lives in the plan; ids here MUST match -->

## Contracts

**You OWN (implement, may propose changes via ritual):** `board/contracts/<…>.md`
**You CONSUME (build against, never edit silently):** `board/contracts/<…>.md`
**WAITS-ON:** <mission X's card Y> — mock path until then: <the contract-mock recipe>

## Sync expectations

Push every ≤60m · standups at <times> (read `board/STATUS.md` first) · blocked >20m →
STATUS Blockers + ping <lead/chat> + switch to next unblocked card · every PR gets a
fresh-session Warden review before merge.
