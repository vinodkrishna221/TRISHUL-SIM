---
description: Post-hackathon retro that converts lessons into concrete patches to this kit — WarRoom gets sharper every hackathon.
---

# /retro — The Kit Learns

When: within 48h of the hackathon (memory decays fast; do it on the train home).
Personas: Orion chairs, everyone contributes. Output: `docs/RETRO.md` + actual patches to
`.agents/` committed on a `retro/<hackathon>` branch.

## Run

1. **The honest ledger (15m):** result + what judges actually said/asked (verbatim while
   remembered — this is rubric ground truth for next time) · timeline reality: when did the
   skeleton walk vs the 30% target · pivots taken · what was cut · final rubric self-score
   vs any real feedback.
2. **Per-system autopsy (20m)** — for each: KEEP / FIX / KILL + one line of evidence:
   - Gauntlet: was our idea actually differentiated on the floor? (did other teams build
     ban-list ideas? — the delicious question)
   - Plans/cards: which cards lied (timebox, context, hidden deps)?
   - Contracts: did drift happen anyway? where?
   - Rituals: standups/freezes/smoke — followed or abandoned, and was abandoning right?
   - Personas/rules: which advice was wrong or missing? which Direction Check saved us /
     was ignored and shouldn't have been?
   - Tools: Antigravity vs Claude Code vs OpenCode — what broke in whose hands?
3. **Convert to patches (20m):** every FIX/KILL becomes a concrete edit — a rule line
   added/deleted, a stack-pack trap appended, a timebox default changed, a new lens for
   Maverick, a new mock-panel question for Oracle, a runbook line for the platform that
   burned you. Edit the files NOW, in this session, on the branch. A retro that produces
   prose instead of diffs is a diary entry.
4. **Update the seed corpus:** append this hackathon to `docs/RETRO.md` archive (what won
   at OUR venue included — Scout's archaeology gets a first-party data point).
5. Merge the retro branch. Tag it: `warroom-v<n>-post-<hackathon>`. Next clone starts
   sharper than this one did.

## Rules

- Blameless syntax, ruthless content: systems and files get criticized, people don't.
- Max 10 patches per retro — a kit that doubles in size every hackathon becomes the
  over-engineering it exists to prevent (ponytail applies to WarRoom itself).
