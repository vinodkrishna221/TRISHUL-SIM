---
description: Architecture + schema + contracts + build plan from the PRD. The parallel-work enabler.
---

# /blueprint — Architecture, Schema, Contracts

Persona: Orion, with Atlas (schema/store call), Sage (AI feasibility), Oracle (wow-moment
machinery), Patch (deploy targets). Skills: `api-contract`. Input: `docs/PRD.md`.
Outputs: `docs/ARCHITECTURE.md` · `docs/SCHEMA.md` · `board/contracts/*` · `docs/BUILD-PLAN.md`.

## Run

1. **Stack decision record** (ARCHITECTURE.md §1): FE / BE flavor / store / AI / deploy —
   each one line + why + the Direction-Check-worthy alternative rejected. Atlas makes the
   Mongo-vs-Convex call here (DECISIONS entry). Volt's existence check runs here (does a
   separate Node service deserve to exist, or do server actions cover it?).
2. **The walking skeleton** (ARCHITECTURE.md §2): name the ONE thread — screen → call →
   store → response — that proves the core loop; it becomes P0 of every phase plan. Oracle
   confirms the wow moment's machinery is inside or adjacent to this thread.
3. **System sketch** (ARCHITECTURE.md §3): boxes-and-arrows in a mermaid block + the
   tracks derived from it (fe / be / data / ai / ship — only tracks that exist).
4. **Schema** (Atlas): SCHEMA.md per the data persona's standard — every entity: fields,
   example doc, demo queries, index per query. PRD nouns verbatim.
5. **Contracts** (skill api-contract): one file per domain in `board/contracts/` — every
   surface two tracks share, request/response/types/events with examples. This is the step
   that buys parallel remote work; do not rush it. Convex projects: types+events only,
   function signatures are the contract.
6. **Fit audit (auto — workflow `/fit-audit`, 30m timebox):** capability inventory from
   the stack record → coverage map vs kit assets → necessity gate (both directions: what
   must be added AND what is explicitly not needed) → Scout fetches vendor AI rules for
   approved gaps → **THE DIFF presented to the human** → approved edits written, synced,
   committed as one PR. This is the kit's one sanctioned self-modification window; phase
   plans reference the new assets. Most projects get a clean bill of health in one line.
7. **BUILD-PLAN.md**: phase spine × tracks grid, the 30%-time skeleton deadline as a real
   clock time, track load estimate, deploy targets + URLs-to-be (Patch).
8. Human review: the stack record + skeleton definition read aloud, sign-off, commit:
   `docs: blueprint — architecture, schema, contracts`.

## Gates

- No track may share a file surface with another (contracts are the only shared surface).
- Every contract field has type + example; error shapes included.
- QUICK mode: ARCHITECTURE+SCHEMA collapse into one file; contracts inline in it; skeleton
  definition is NEVER skipped.
- End state: a repo where `/phase-plan` can generate plans no agent needs to ask questions about.
