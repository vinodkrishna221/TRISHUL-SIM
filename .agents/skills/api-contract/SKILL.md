---
name: api-contract
description: Write and change the API/event/type contracts in board/contracts/ that let tracks build in parallel without integration hell. Use at /blueprint and whenever a shape must change.
---

# API Contract

Purpose: contracts are the only shared surface between missions — cheap to write, brutal to
skip. Owned by Orion at blueprint; changed by anyone via the change ritual. Template:
`.agents/templates/api-contract.md` → files in `board/contracts/` (one per resource domain:
`leads.md`, `scoring.md`, `events.md`…).

## What a contract file contains

```
# Contract: <domain>            version: N (bump on breaking change)
OWNER: <mission/track that implements>   CONSUMERS: <missions/tracks>

## <METHOD> <path>                        e.g. POST /api/leads/score
REQUEST  { field: type — constraint/example }
RESPONSE 200 { … }    ERRORS { code, message } shapes actually returned
NOTES: auth?, pagination?, idempotency?, latency budget if demo-relevant

## Types                                  the shared nouns (PRD vocabulary!)
Lead { id: string, name: string, score: number 0-100, … }   + one example object

## Events (if any)                        realtime/webhook payloads, same rigor
```

Rules: every field has a type + example · IDs are strings, dates are ISO-8601 at the
boundary (driver types never leak) · error shapes are part of the contract · Convex
projects: exported function signatures in `convex/` ARE the contract — this file then only
lists types + events.

## Mirroring (each side derives, never hand-copies twice)

zod (`src/contracts.ts`) and/or pydantic (`app/models.py`) mirror the contract file; FE
types import/mirror from the same place. One source per language, compiler finds the drift.

## The change ritual (60 seconds, mandatory, no exceptions)

1. Edit the contract file, bump `version`, one-line changelog at the bottom
2. Three-line entry in `board/DECISIONS.md` (what/why/who's affected)
3. Ping CONSUMERS in team chat (or STATUS blockers, solo)
4. THEN change code — both sides in the same working session if possible

Silent contract drift is the single most expensive bug class a hackathon team has. Warden
BLOCKS any PR whose shapes disagree with the contract file, in either direction.
