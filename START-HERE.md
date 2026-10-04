# ⚔️ START HERE

**Only the lead needs to read the whole kit.** If you're a teammate: this page + your
mission file (`missions/<you>-mission.md`) is genuinely everything. The capsules carry
the rest.

## The five things that actually matter

1. **Cards** — your mission is task cards. Each one is self-contained: context,
   deliverable, the ONE test, timebox. Your agent can start any card cold. If a session
   goes stupid (contradicts the repo, hallucinates paths) — don't argue, **kill it and
   cold-start from the card**. Session-murder is cheap by design.
2. **Contracts** (`board/contracts/`) — the shapes you share with other tracks. Never
   change one silently: bump version → 3-line note in `board/DECISIONS.md` → ping →
   then code. This one habit is why integration hell won't happen.
3. **The skeleton law** — a thin end-to-end slice runs on live URLs by 30% of time
   (hour 8 at SIH). Everything before it serves it; everything after builds on calm.
4. **DEMO_FALLBACK** — every AI feature has cached responses for the scripted demo
   inputs from hour one. Venue Wi-Fi and rate limits are certainties, not risks.
5. **STATUS** (`board/STATUS.md`) — read it at session start, append one line at
   session end. It's how a team (or your own 3am shift) shares one brain.

## Boot your tool (60 seconds)

Run `scripts/sync.sh` once, then: **Antigravity** — reads everything natively; open a
persona file and say "adopt this". **Claude Code** — personas are `@`-mention subagents,
workflows are `/commands`. **OpenCode** — pick the agent by name, workflows are
`/commands`. Stuck? `docs/TOOL-PATHS.md`.

## Your loop

Read STATUS → open your mission → take the next card → summon its persona → build →
PR (template fills itself) → a FRESH session reviews it → merge → smoke → tick the card
→ one STATUS line. Repeat. Blocked >20 minutes? Write it in STATUS Blockers, ping the
owner, take your next card — silent hero-debugging is banned.

## Three rules people learn the hard way

- **Timeboxes are real:** at 2× the card's timebox, stop and descope — sunk cost isn't a
  demo feature. Most cards should be 90–120 minutes; a 4-hour card is two cards hiding.
- **The kit is frozen during events.** Improve it at `/retro`, not at hour 20 — a
  mid-event "rule improvement" syncs to every teammate's agent instantly. Don't.
- **After T-6h, the Endgame Doctrine** (`.agents/rules/40-endgame.md`) is the law:
  review tiering, the freeze calendar, and no irreversible decisions solo at 4am.

**Want the longer version of this page?** `docs/MEMBER-MANUAL.md` — the loop in detail,
agent craft, eval-round duties, and the when-things-go-wrong table.
**Leads:** your book is `docs/LEADER-MANUAL.md` (+ `docs/WORKFLOW.md` once, at home).
**New team?** Run `/drill` — the 2-hour rehearsal — before the event. It's the
difference between 70% and full speed.
