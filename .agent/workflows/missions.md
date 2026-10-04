---
description: Split phase plans into self-contained mission briefs — per teammate (team mode) or a persona-run queue with worktree parallelism (solo mode).
argument-hint: "[optional: member name to (re)brief]"
---

# /missions — Mission Assignment

Persona: Orion. Skill: `.agents/skills/mission-brief/SKILL.md`. Inputs: `plans/*`,
`board/TEAM.md` (roster, strengths, hours — if missing, create from template by asking the
human ONCE for the roster). Outputs: `missions/<name>-mission.md` (+ `missions/QUEUE.md` solo).

## Run — TEAM mode (roster ≥2)

1. Slice cards into missions by track + dependency (skill rules: contracts satisfy
   cross-mission needs; no shared file surfaces; ≤70% load vs declared hours).
2. Write each `missions/<member>-mission.md`: context capsule + cards verbatim + contracts
   OWNS/CONSUMES + stack rules to load + branch names + WAITS-ON lines (each with its mock
   path) + sync expectations (push cadence, standup times, blocker protocol) + accept box.
3. Herald gets a standing mission (spine, submission, demo script draft dates); Patch gets
   the ship mission (deploy from skeleton, freeze calendar); Warden is a rotating duty
   attached to every mission's PRs, not a separate mission.
4. Post the mission map to team chat: one line per member — mission name, first card,
   branch. Chase the accept boxes; unaccepted by next standup = lead reassigns.

## Run — SOLO mode (roster = 1)

1. Sequence ALL cards into `missions/QUEUE.md` by the DAG: numbered persona-runs
   (`M1 skeleton-data+be → Forge/Atlas`, `M2 skeleton-fe → Nova`, …), each ≤ a half-day.
2. Mark `PARALLEL-OK` pairs (independent DAG branches) with the worktree recipe from
   `10-git-protocol.md` + suggested tool split (Antigravity ↔ FE, Claude Code/OpenCode ↔
   BE/AI). Never more than 2 parallel sessions — supervision is a budget too.
3. Every mission ends: PR → **fresh-session Warden review** (non-negotiable solo) → merge →
   tick QUEUE → smoke ritual.
4. QUEUE.md top block = live status (current mission, parallel session, next up, blocked) —
   the single glance-source between sessions.

## Gates

- Cold-start test again at mission level: brief + repo alone must be enough in ANY tool.
- Commit: `docs: missions — <n> briefs` and update STATUS with the mission map.
- End state: everyone (or every worktree) knows exactly what to build first.
