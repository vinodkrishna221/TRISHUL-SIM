---
description: Interview → PRD with scale gate, scope, cut list. The hackathon's constitution.
argument-hint: "[optional: paste idea/constraints if /ideate was skipped]"
---

# /kickoff — PRD & Constitution

Persona: Orion. Skill: `.agents/skills/prd-writer/SKILL.md`. Input: `board/IDEA-BRIEF.md`
(or $ARGUMENTS if ideation was skipped — then run a compressed gauntlet sanity check:
"is this idea on the field's consensus list? y/n" and say so).
Output: `docs/PRD.md` + seeded `board/` files.

## Run

1. Read IDEA-BRIEF.md; run the prd-writer interview (≤6 questions, only what the brief
   doesn't answer, one at a time).
2. **Scale gate decision** (QUICK vs FULL) — propose with reasoning, human confirms, write
   it at the top of the PRD. QUICK collapses: one-page PRD, single phase plan, missions =
   queue only. **Ceremony ceiling (hard):** at 24h, ALL planning — kickoff + blueprint +
   phase-plan + missions — fits in ≤2.5h total, run as ONE working session with merged doc
   passes; idea locks by hour 2. Beautiful committed docs with zero code at hour 4.5 is a
   documented way to lose.
3. Draft `docs/PRD.md` per the skill's five-question structure. The DEMO NARRATIVE section
   is written WITH Oracle (wow moment #1 placed) and Herald (spine sentence drafted).
4. Seed the board:
   - `board/STATUS.md` from template (freeze calendar computed from the actual end time —
     Patch's numbers; Cut List section initialized from the PRD's CUT list)
   - `board/DECISIONS.md` from template (entry #1: the idea choice + scale gate)
   - `board/contracts/` directory created (filled at /blueprint)
5. Read the PRD back to the human in 60 seconds (the five answers, out loud). Explicit
   sign-off, THEN commit: `docs: PRD — <project name>`.

## Gates

- PRD vocabulary freeze: the nouns here are final (schema/UI/pitch reuse them verbatim).
- Every MUST ↔ demo beat mapping verified; orphans deleted before sign-off.
- End state: PRD committed, board live, ready for `/blueprint`.
