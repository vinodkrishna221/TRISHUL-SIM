---
name: pr-review
description: Fresh-session PR review procedure — card match, contract fidelity, proof, ponytail audit, demo-path safety, hygiene — ending in a MERGE / FIX-THEN-MERGE / BLOCK verdict. Use for every PR; mandatory in solo mode.
---

# PR Review

Purpose: `main` is the demo; this is its immune system. Run by Warden — ALWAYS in a fresh
agent session (or a different tool than built the PR). Solo mode: no self-merge without this.

## Setup (fresh session)

```bash
git fetch && git checkout <branch> && git diff main...HEAD --stat   # scope first
```
Read: the task card (in `plans/`) → the PR body → contracts touched (`board/contracts/`).
THEN the diff. Card-first reading prevents "nice code" from hiding "wrong deliverable".

## The six gates (stop at first FAIL)

1. **Card match** — deliverable exists; scope creep flagged (good extras → follow-up PR)
2. **Contract fidelity** — request/response/type shapes vs contract files, field by field
   on touched surfaces; version bumped + DECISIONS entry if changed
3. **Proof** — run the PR's one check. No check on non-trivial logic = FIX-THEN-MERGE
   (author adds the smallest failing-if-broken assert)
4. **Ponytail audit** — name the rung: unrequested abstraction? new dep below rung 5?
   config for a constant? scaffold "for later"? Suggest the deletion, not a redesign
5. **Demo-path safety** — touched demo screens/endpoints: seeded click-through green,
   loading/empty states intact, no console errors, `next build`/boot passes
6. **Hygiene** — secrets/.env, debug prints on hot paths, dead code, caveman prose in
   committed files, missing `.env.example` line for new vars

## Verdict (always exactly this shape)

```
VERDICT: MERGE | FIX-THEN-MERGE | BLOCK
✅ <one earned line>
🔧 file:line — issue — suggested fix   (≤5 items; more = BLOCK with "split this PR")
⛔ <blocker + what unblocks>           (contract break | demo red | secret | no deliverable)
BAR: <normal | crunch>  — crunch relaxes polish gates (4,6), never gates 2,3,5-demo,secrets
```

Fix timebox: 20 minutes. Fixes the author can't land in 20m → task returns to the plan with
honest status. Reviews are ≤15 minutes — a review slower than the build is a process bug;
flag the card that caused it.
