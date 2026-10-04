---
name: mission-brief
description: Package phase-plan tasks into a self-contained mission brief per teammate (team mode) or per persona-run (solo mode) — a context capsule any agent session can execute cold. Use during /missions.
---

# Mission Brief

Purpose: the handoff artifact. A teammate 400km away opens `missions/<name>-mission.md` in
ANY of the three tools and their agent knows everything: what to build, against which
contracts, on which branch, with which rules. Run by Orion. Template:
`.agents/templates/mission-brief.md`.

## Assembly (per mission)

1. **Slice by track + dependency**, not by convenience: a mission = one track's cards whose
   cross-track dependencies are satisfied by contracts (mocks), never by "wait for Rahul".
2. **Context capsule** (the brief carries, inline or by pointer):
   project one-liner + demo narrative (from PRD) · the mission's goal in one sentence ·
   its task cards (verbatim from the plan) · contracts it OWNS vs CONSUMES (paths) ·
   stack rules to load · branch name (`feat/<track>-…` per card) · sync expectations
   (push cadence, standup times, blocker protocol from 20-advisor).
3. **Definition of victory:** the mission's demo beats green on seeded data + cards checked +
   PRs merged. Not "code written".
4. **Load check:** cards' timeboxes vs the member's declared hours (TEAM.md) at ≤70%.

## Team mode

- One mission file per member, matched to their declared strengths in TEAM.md.
- Cross-mission dependencies surface as WAITS-ON lines with the owning mission named —
  and each WAITS-ON must have a mock-path so waiting never blocks building.
- The human lead assigns; the brief ends with a checkbox the member ticks on accept
  (silent unaccepted missions are how remote teams discover gaps at hour 20).

## Solo mode (roster of one)

- Missions map to PERSONA RUNS, sequenced by the dependency DAG into a queue:
  `M1 data+be-skeleton (Forge/Atlas) → M2 fe-skeleton (Nova) → M3 ai-core (Sage) → …`
- Mark PARALLEL-OK missions: independent DAG branches the human may run simultaneously in
  git worktrees (one agent session per worktree — see 10-git-protocol). Suggested pairing:
  Antigravity on FE mission, Claude Code/OpenCode on BE/AI mission.
- Every mission still ends in a PR + fresh-session Warden review before merge (the solo
  discipline that replaces a second pair of eyes).
- Queue lives at the top of `missions/QUEUE.md` with live status — the solo player's
  single glance-source for "what now".
