# ⚔️ The Member's Manual

You read THIS + your mission file (`missions/<you>-mission.md`). That is genuinely
everything — the mission capsule carries the rest. (Skim `START-HERE.md` first if you
haven't; this manual is its longer twin for when you want the why.)

## 1 · Boot (once, 60 seconds per tool)

Run `scripts/sync.sh` from the repo root, then:

| Your tool | Personas | Workflows | Notes |
|---|---|---|---|
| **Antigravity** | open `.agents/personas/<x>.md` → "adopt this persona" | `/command` (native) | reads `.agents/` directly |
| **Claude Code** | `@nova-frontend` etc. (subagents) or `/agents` | `/command` | law loads via CLAUDE.md |
| **OpenCode** | select agent by name | `/command` | reads AGENTS.md natively |

Stuck on paths → `docs/TOOL-PATHS.md`. Broken setup → say so at `/drill`, not at hour 2.

## 2 · Your loop (repeat until victory)

1. **Read `board/STATUS.md`** (60 seconds — it's the team's shared brain)
2. **Take your next card** from your mission. Read its CONTEXT fully.
3. **Summon the card's persona** + load the stack rules it names.
4. **Ponytail gate:** does this card need to exist / is there a lazier rung? Say so
   BEFORE building. Non-trivial component → Scout's 5-minute prior-art check first.
5. **Build to the DELIVERABLE, nothing more.** Good extras go in a follow-up card.
6. **Run the card's TEST.** Non-trivial logic gets its one smallest failing-check.
7. **PR** with the template. Full prose — caveman never touches committed text.
8. **Fresh-session review:** a Warden session (new chat, or another tool, or a teammate's
   machine) reviews before merge. Verdict ≤15m, your fixes ≤20m.
9. **Merge in DAG order → smoke ritual → tick the card → one STATUS line.** Next card.

## 3 · The five laws you'll actually be judged by

1. **Contracts:** never change a shape silently. Edit the contract file → bump version →
   3-line DECISIONS note → ping consumers → THEN code. 60 seconds; it's what makes
   parallel work possible.
2. **Timeboxes:** most cards are 90-120m. At 2× with no green path — STOP, descope or
   flag. Sunk cost is not a demo feature.
3. **Blocked >20m:** STATUS Blockers + ping the owner + take your next unblocked card.
   Silent hero-debugging of someone else's surface is banned and it WILL be noticed.
4. **Push every ≤60m.** Small WIP commits on your branch are fine; invisible work isn't.
5. **Deps:** you don't install packages unless your track owns deps this phase — file a
   `DEPS-REQ` line in STATUS; the owner installs within 30m. Never hand-merge a lockfile.

## 4 · Agent craft (the skills that make you fast)

- **Session murder:** the moment a session drifts — contradicts the repo, invents paths,
  argues with reality — kill it. Cold-start a fresh one from the card. The card format
  exists precisely so restarts cost 2 minutes. Never negotiate with a confused agent.
- **LITE boot** for mechanical cards (renames, copy, config): core rules + the card only,
  skip persona and stack pack. Cheap and sufficient.
- **Version fights:** agent confidently writing last year's API syntax? >20 minutes of
  framework wrestling = pin/downgrade to the stack pack's version, log it, move on.
- **Trust nothing unrun:** an agent claiming "tests pass" without running them is why
  gate 3 of the review exists. Run the check yourself before the PR.
- **Caveman ON in build-loop chat** (token discipline) — OFF in anything committed.

## 5 · Eval rounds & mentoring sessions (multi-round events)

Before each round you'll be handed **one owned answer** (privacy, schema, scaling, cost…).
Drill it to 20 seconds with one artifact. During the round: builders not presenting keep
building on branches — never idle, never merging. After: feed everything you heard,
verbatim, to the triage table — even the throwaway judge comments; phrasing carries
scoring intent. Next round, whoever owns a callback says: "you asked about X — here it is."

## 6 · Non-dev seats (these are real missions, not support roles)

- **Storyteller seat** (Herald + Oracle): the deck lineage (one deck, patched per round —
  `ppt-builder`), demo script, eval packs, speaker prep, submission page by T-2h.
  Your cards look like: "E2 delta slides [45m]", "record backup video [30m, T-3h]".
- **Intel seat** (Scout + feedback-triage): the evidence — sourced numbers for slides,
  the judge dossier, the DPDP/privacy answers, the feedback log, venue logistics. In
  self-proposed cycles, you co-drive `/prospect`. Your cards: "cost-per-district number,
  sourced [45m]", "triage round-1 feedback [15m]".

## 7 · What changes at T-6h (endgame)

The tiering activates: contract/schema/data/deploy-touching changes still get a fresh
Warden review, always; demo-path polish (CSS, copy, seeds) becomes self-review + smoke.
The classifier is what it TOUCHES, not how small it feels. Freezes are real — risky
features T-5h, everything T-3h, merge T-2h, deploy T-90m. No irreversible decisions solo
between 3-5am; wake someone. If you're the freshest person at T-3h, congratulations —
you're probably demoing.

## 8 · When things go wrong (lookup table)

| Situation | Do this |
|---|---|
| Agent session drifting | Kill it. Cold-start from the card. (§4) |
| Blocked on another track | 20-min rule: STATUS + ping + next card (§3.3) |
| Card is 2× its timebox | Stop. Tell the lead. Descope or swap. (§3.2) |
| Need a shape another track owns | Contract change ritual — never silently (§3.1) |
| Mentor told me to add something | It goes to feedback triage, not into your mission |
| Demo path broke after my merge | Say it in chat immediately; revert beats archaeology |
| I disagree with a rule mid-event | STATUS note; work around; /retro fixes the kit — it's frozen |
| It's 4am and I want to refactor | Working code is sacred. No card, no refactor. Sleep. |
