# ⚔️ The Leader's Manual

You are the one person who reads everything. This manual is your command guide — not a
repeat of `WORKFLOW.md` (the system) but the **decisions only you make** and the moments
you make them. Field-tested law: the kit owns the process; you own the calls.

## 0 · What only the lead does

| Decision | When | Default |
|---|---|---|
| Scope call (/prospect mode, target sector/state) | week before internal | your strongest local-evidence domain |
| Scale gate: QUICK vs FULL | /kickoff | QUICK at ≤24h or ≤2 builders |
| Mission assignment + accept-chasing | /missions | strengths from TEAM.md, ≤70% load |
| Pivot chair (forces a decision by minute 25) | skeleton misses 30% / bet dies | lowest-risk option wins ties |
| Scope-argument ritual (15m, you decide, log it) | two humans deadlocked | decide, DECISIONS entry, move |
| Speaker + demo-driver assignment | each eval round + /ship | freshest person demos, not the founder-est |
| Endgame tiering activation | T-6h | per `40-endgame.md`, no improvising |
| Kit freeze enforcement | T-0 → /retro | nobody edits `.agents/` mid-event, including you |
| Fit-audit diff approval (the kit's one sanctioned self-edit) | /blueprint step 6 | approve the minimal set — the necessity gate outranks enthusiasm; "nothing needed" is the most common right answer |
| Agent budget calls (LITE profiles, parallel sessions) | ongoing | LITE for mechanical cards; max 2 worktree lanes/person |

## 1 · Before any event (once per team, calm evening)

- Read `WORKFLOW.md` end to end — you're the only one who must.
- Fill `board/TEAM.md` honestly (strengths, tools, REAL hours). The load ledger believes it.
- Run **`/drill`** with the full roster (2h). Non-negotiable for a new team: it converts
  79 files of theory into muscle memory, and it's where you discover whose tool setup is
  broken while it's free. Re-drill 30 min before big events.
- Verify every member's machine: `scripts/sync.sh` run, personas visible, one command works.

## 2 · Self-proposed cycles (SIH Open Innovation)

Run `/prospect` the week BEFORE the internal round — it's 2-3 hours of agent mining, not
hackathon-clock work. Your calls: the scope (pick where your team can show *local*
evidence and access — "we called the mandi" beats a stat), and the final dossier pick
from Oracle's top 5. Then `/ideate` runs on the wedge, and the internal runs mini-finale
mode. The dossier's evidence ledger goes into the idea PPT — sourced numbers on the
problem slide is the cheapest credibility you will ever buy.

## 3 · The planning chain (your four sign-offs)

You sign off on each artifact OUT LOUD before it commits — 60 seconds each:
1. **PRD** — the five answers read aloud; every MUST maps to a demo beat; vocabulary frozen
2. **Blueprint** — the stack record's "rejected" column is real (not decoration); the
   walking skeleton is ONE traceable thread; contracts exist for every shared surface
3. **Phase plans** — the DAG has no two cards sharing a file; P0 sums to a real skeleton;
   cold-start test on 3 random cards
4. **Missions** — everyone knows their first card + branch before you say "go"; chase
   accept-boxes by the first standup

**Ceremony ceiling (enforce ruthlessly):** at 24h, ALL planning ≤2.5h in one merged
session. Beautiful docs + zero code at hour 4.5 is a documented way to lose — yours to
prevent.

## 4 · The build loop (what you actually watch)

You are not the best builder anymore — you are the person watching four dashboards:
- **STATUS blockers age** — anything >45m gets escalated or reassigned by YOU
- **The clock vs the skeleton deadline** — the only automated trigger; don't ride it manually
- **Load reality** — someone at 2× a timebox isn't heroic, they're stuck; swap the card
  or descope it. Plan the final third at 50% capacity.
- **Scope creep vectors** — mentors (route through feedback-triage, always), late ideas
  (v2 parking lot), and your own 2am ambitions (cut list, like everyone else's)

Reassignment call: an unaccepted or stalled mission at two consecutive standups moves —
say it plainly, log it, no ceremony. Remote teams die of politeness.

## 5 · Judge touchpoints (multi-round events)

Your moves per `/eval-round`: assign ONE speaker + one owned answer per member (nobody
freelances an answer they don't own) · chair the T+15m feedback triage — the cut-list
math is said out loud before any mentor suggestion becomes a card · script next round's
callbacks. You never argue with a judge live; DEFEND items return as evidence next round.

## 6 · The endgame (T-6h — your finest hour)

Activate `40-endgame.md` and say so in chat. From here: review tiering (contract/data/
deploy = fresh Warden always; polish = self-review + smoke), the freeze calendar executes
without debate (risky T-5h · feature T-3h · submission + merge T-2h · deploy T-90m with
the pre-warmed duplicate), demo video recorded by T-3h, standups go event-driven. Decide
the demo driver and speaker at T-3h out loud. Enforce the human layer: no irreversible
decisions solo between 3-5am — including yours; the 4am schema rewrite needs a second
awake human or a 20-minute wait. T-15m: quiet cockpit. The graveyard is full of "one tiny fix."

## 7 · Managing the agent fleet

- **Session-murder culture:** teach it at /drill — a drifting session (contradicts repo,
  hallucinates paths) gets killed and cold-started from the card. Nobody negotiates with
  a confused agent; cards make restarts cheap.
- **LITE boot** for mechanical cards (core rules only, no persona) — you make this call
  when the API bill or context budget matters; it's in `00-warroom-core.md`.
- **Worktree lanes:** max 2 parallel agent sessions per human — supervision is a budget.
  One track owns dependency installs per phase (deps-request ritual for everyone else).
- **Version-fight law:** anyone fighting a dependency >20 minutes pins/downgrades and
  moves. You enforce it when pride resists it.
- **Deploy dashboards are human-owned** — assign WHICH human at kickoff (usually Patch's
  operator). Agents draft env values; humans click.

## 8 · The ten ways leads lose (field-collected)

1. Planning theater — docs eat the morning (ceiling exists; use it)
2. Unassigned/unaccepted missions discovered at hour 20
3. Mentor suggestions adopted silently (route EVERYTHING through triage)
4. Skipping fresh review entirely at endgame instead of tiering it
5. Hero-debugging yourself while nobody leads (blockers age, standups die)
6. Riding a failing bet past 2× timebox because YOU picked it (pivot chair, even on your own bets)
7. The founder demos exhausted (freshest person demos — decided at T-3h, not T-5m)
8. Submission at T-30m (portals crash at T-15m, every single time — T-2h is law)
9. Mid-event kit "improvements" (frozen; STATUS note; /retro)
10. Skipping /retro — the kit you deserve next event is written in the 48h after this one

## 9 · Your pocket card

`STATUS first · blockers <45m · skeleton is law · triage all feedback · tier at T-6h ·
freshest demos · freeze means freeze · retro or it didn't happen`
