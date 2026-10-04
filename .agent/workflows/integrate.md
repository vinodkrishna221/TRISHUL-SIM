---
description: The merge train — dependency-ordered merges with contract verification and the smoke ritual after each. Run at phase boundaries and before /ship.
---

# /integrate — The Merge Train

Persona: Patch conducts; Warden reviews every wagon; builders fix on the spot.
When: end of P0 (skeleton assembly), end of P1/P2, and T-4h (the last big one).
Skills: `pr-review`, `deploy-runbook` (smoke ritual).

## Run

1. **Marshal (5m):** list open branches/PRs → order by the DAG: contracts/schema → data/
   seed → backend → frontend → ai/polish. Post the train order in chat/STATUS. Branches
   not on the train say so explicitly (nothing ambient).
2. **Per wagon:** author rebases on main (`git pull --rebase origin main`) → Warden
   fresh-session review (six gates; crunch bar only if the freeze calendar says so) →
   merge → **smoke ritual** (seed → boot → demo click-through → `smoke ✅ @ <sha>` in
   STATUS) → deploy auto-ships → next wagon.
3. **Contract verification pass** (after the last wagon): for every file in
   `board/contracts/` — one real request/response against the LIVE deploy (or `scripts/
   smoke.*` runs them all); shapes diff'd against the contract, field by field. Any drift:
   fix NOW while both track authors are warm — this 15-minute pass is where integration
   hell goes to die.
4. **Cross-track click-through:** the full demo path on the deployed URLs, seeded data,
   venue-sim conditions (throttle the network tab once, honestly). Breaks become fix-tasks
   with screenshots, merged as hotfix wagons.
5. **Close the train:** STATUS updated (deploy sha, smoke green, what's next) · Oracle
   re-score on the now-integrated build · backup video re-recorded if the demo path changed
   (Patch will not forget this; do not fight him).

## Rules

- Two red smokes in a row = train halts; revert the last wagon (revert > archaeology) and
  fix on a branch.
- Nothing merges outside the train during a train.
- Solo mode: identical, just faster — the train is you, but the ORDER and the smoke ritual
  are exactly as sacred, and Warden still gets a fresh session per wagon.
