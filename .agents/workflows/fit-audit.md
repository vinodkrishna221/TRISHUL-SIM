---
description: Auto-invoked at /blueprint — audit whether this project needs the kit revamped (new rules, packs, skills, personas) or explicitly does not; edits core kit files via human-approved diff. The one sanctioned self-modification window.
argument-hint: "[optional: run standalone after a stack-changing decision]"
---

# /fit-audit — Project-Fit Kit Audit

Auto-runs as blueprint step 6 (after the stack record + contracts, before BUILD-PLAN and
any phase planning — assets must exist before cards reference them). Method:
`.agents/skills/fit-audit/SKILL.md`, followed exactly. Personas: Orion chairs, Scout
researches/fetches, Oracle votes the necessity gate. Timebox: 30 minutes total.

## Run

1. **Inventory** capabilities from the stack record + PRD (skill step 1).
2. **Coverage map** vs existing kit assets — COVERED / PARTIAL / MISSING with evidence.
3. **Necessity gate** on every gap, BOTH directions: what must be added, and what is
   explicitly NOT needed (declined list with reasons — this list is the bloat firewall).
4. **Research & draft** approved gaps (Scout fetches official vendor AI rules + current
   docs first; packs follow house format with version pins).
5. **THE DIFF** → human. Files added/edited, why each, gate scores, declined list.
   Human approves whole or per-item. No approval, no write.
6. **Write → `scripts/sync.sh` → one commit** (`chore: fit-audit — <n> assets`) →
   DECISIONS entry → freeze re-engages.
7. Missions/phase plans generated after this point reference the new assets in their
   CONTEXT lines (cold-start rule: cards name the rules they need).

## Standalone invocations (manual only)

- After a stack-changing `/pivot` (the pivot chair calls it; same diff ritual).
- When a mid-build discovery reveals an uncovered sharp edge burning >1 card — the lead
  invokes it; everything still flows through the diff ritual. Ad-hoc `.agents/` edits
  remain banned; this workflow is the only door.

## Outputs

`board/FIT-AUDIT.md` — the coverage map, gate scores, approved diff, and declined list
(committed; /retro reads it to decide which assets graduate into the core kit).
