---
name: prd-writer
description: Produce a hackathon-grade PRD from an idea brief plus a short human interview. One page in QUICK mode, three in FULL. Use during /kickoff.
---

# PRD Writer

Purpose: the PRD is the contract between the team and the clock. Hackathon PRDs answer five
questions with zero fog; everything else is deleted. Run by Orion. Template:
`.agents/templates/PRD.md` → output `docs/PRD.md`.

## Interview (ask ONLY what the IDEA-BRIEF doesn't answer, one at a time, max 6)

1. Who is the ONE demo user? (name them: "Priya, solo agency owner in Pune" — not "SMBs")
2. The core loop in one sentence — what does the user do, get, and come back for?
3. Demo moment: what exactly do judges watch happen in 90 seconds?
4. Scale gate: QUICK (≤24h / ≤2 builders) or FULL? (propose, human confirms)
5. Hard constraints: required sponsor APIs, mandated tracks, banned tech
6. Non-goals worth writing down (the tempting things we will NOT build)

## Structure (the five questions)

1. **PROBLEM** — 3 sentences, human terms, with the pain number Scout sourced
2. **USER & CORE LOOP** — the named user; the loop as `does → gets → returns`
3. **DEMO NARRATIVE** — the 90-second judge-visible story, beat by beat (this section
   replaces "features": if it's not in a beat, it's not in scope)
4. **SCOPE** — MUST (walking skeleton + wow moment) / SHOULD (if time) / CUT LIST (born
   pre-ranked, feeds scope-guard) / NON-GOALS (explicit)
5. **SUCCESS =** the wow moment lands live + the three retellable phrases + submission
   complete by T-2h

## Rules

- Every MUST maps to a demo beat; every demo beat maps to ≥1 MUST. Orphans die.
- Vocabulary is frozen here: the nouns this PRD uses (Lead, Mission, Score…) are THE names —
  code, schema, UI copy, and pitch all use them verbatim. Renaming later costs an hour.
- QUICK mode: sections 1-2 collapse to three lines total; SHOULD list max 3 items.
- The PRD is re-read out loud at /pivot — write it so it still gives orders at hour 20.
