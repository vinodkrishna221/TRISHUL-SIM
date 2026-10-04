# WarRoom Changelog

## v1.5 — The Fit Audit (2026-08-14)

The kit now audits ITSELF against each project. At blueprint (auto, step 6), `/fit-audit`
inventories the solution's capabilities, maps them against existing rules/packs/skills/
personas, and runs a strict necessity gate in BOTH directions — what must be added, and
what is explicitly not needed (the declined list is the bloat firewall).

### Added
- **`fit-audit` skill** — capability inventory → coverage map → necessity gate (≥3 cards ·
  demo-fatal · unfamiliar; <2 yes = no new asset; personas need a whole track) → asset
  drafting (house-format stack packs with vendor-AI-rules fetch first, skills, rare
  personas, scoped exceptions to existing law) → THE DIFF ritual
- **`/fit-audit` workflow** — auto at blueprint step 6; standalone only after
  stack-changing pivots or lead invocation; outputs `board/FIT-AUDIT.md`
- **Diff-approval ritual** — nothing writes without the human approving the full diff;
  then sync → one commit → freeze re-engages

### Changed
- Kit-freeze rule (Prime Directive 7 + git protocol) now names `/fit-audit` as the ONE
  sanctioned self-modification door — ad-hoc `.agents/` edits remain banned
- Blueprint gains step 6; /pivot chairs may invoke the audit after stack changes
- Leader manual: fit-audit diff approval added to the lead-only decisions table
- /retro reviews fit-audit assets: good ones graduate into the core kit

## v1.4.1 — Role Manuals (2026-08-14)

- **`docs/LEADER-MANUAL.md`** — the command manual: the decisions only the lead makes
  (scope calls, scale gate, missions, pivot chair, tiering activation, fleet/budget),
  per-phase sign-offs, the ten field-collected ways leads lose, and a pocket card
- **`docs/MEMBER-MANUAL.md`** — the execution manual: boot per tool, the loop, the five
  laws (contracts, timeboxes, 20-min rule, push cadence, deps), agent craft
  (session-murder, LITE boot, version fights), eval-round duties, non-dev seat guides,
  endgame changes, and a when-things-go-wrong lookup table
- README and START-HERE now route each role to its manual

## v1.4 — The Problem Prospector (2026-08-14)

For hackathon cycles where teams must FIND their own problems (SIH self-proposed /
Open Innovation). Core inversion: problems are mined from institutional evidence
streams where they're admitted with numbers — never brainstormed from pattern memory.

### Added
- **`problem-prospector` skill** — five mining pipelines (admission: CAG audits,
  parliamentary questions, scheme evaluations, budget gaps · grievance: CPGRAMS/state
  portals/RTI journalism · procurement: tenders + challenge archives, "buying is
  admitting" · graveyard: failed pilots naming the killing constraint · friction:
  seasonal recurrence, court orders) + three gates (triangulation: ≥3 streams, ≥2 years,
  ≥1 numbered official admission · anti-generic chair test: named place, number, failed
  attempt, pain owner · whitespace + scheme-overlap) + PQ scoring
- **`/prospect` workflow** — scope modes industry/state/theme; Scout mines, Maverick
  runs the chair test, Orion names the buildable wedge + rail, Oracle scores; top 3
  become full dossiers; run the week before the internal round
- **`PROBLEM-DOSSIER` template** — evidence ledger + SIH submission-format PS section
  (ministry register) + gauntlet-ready brief section with pre-seeded ban list
- **SIH arena §7** — self-proposed mode pipeline (prospect → ideate → mini-finale),
  local-evidence guidance, evidence-ledger-into-idea-PPT rule
- **Worked example** — a real /prospect run (Agriculture × Maharashtra) with live-mined
  evidence, in `examples/`

## v1.3.1 — Evidence & Deployability Codification (2026-07-24)

Codifies the gauntlet's epistemics so they run every event, not just when someone
remembers the theory:
- **Gauntlet step 1 → "Interrogate the PS":** author intent + chain-of-loss decomposition
  (pick ONE bleeding link, usually the unsexy one) alongside rubric extraction
- **Gauntlet step 2:** evidence-not-idea-shopping stance made explicit; deep-water
  sourcing; failed-pilot postmortems; closes with "why hasn't this been solved already?"
- **Gauntlet step 4:** every candidate must cite the step-2 finding it kills; collision
  lens gets the analogy discipline ("analogy proposes; evidence disposes")
- **Gauntlet step 6 → ground-deployability battery:** rails / operator / zero-training /
  cold-start / unit-economics / zero-bars tests — "works Monday morning" is scored;
  failures cap Impact and require a plan B in the risk register
- **prior-art-check:** the deep-water source ladder (primary pain → gov/institutional
  data → the graveyard → gray literature → working deployments elsewhere), with the
  no-link-no-claim rules
- **IDEA-BRIEF template:** deployability battery line added to "The pick"

## v1.3 — The Winners' Patch (2026-07-21)

Every change below comes from field feedback by a team that ran WarRoom at multiple
hackathons and won — triaged through the kit's own ADOPT/IF-TIME/DEFEND/DECLINE table.

### Added
- **`/drill`** — the 2h pre-event rehearsal (kills the first-event learning tax; includes
  session-murder practice: kill a drifted agent, cold-start from the card)
- **`.agents/rules/40-endgame.md`** — the degraded mode as doctrine: review tiering
  (contract/data/deploy = fresh Warden always; demo-path polish = self-review + smoke),
  the corrected freeze calendar (risky T-5h · feature T-3h · merge T-2h · deploy T-90m
  with a pre-warmed duplicate project), event-driven standups, final-third-at-50% load,
  and the human layer (no irreversible decisions solo 3–5am; freshest person demos;
  15-minute scope-argument ritual)
- **`START-HERE.md`** — the one-page adoption unlock: only the lead reads everything;
  teammates read this + their mission file
- **`brand-sprint` skill** — 30-minute name/logo/OG sprint (the "feels finished" multiplier)

### Changed
- Ceremony ceiling: at 24h ALL planning ≤2.5h in one merged session; idea locks by h2
- Timebox norm 90–120m (4h = "two cards hiding"); plan the final third at 50% capacity
- Direction Check budget: full format only for irreversible/contract-touching/>1h decisions
- 20-minute dependency-fight law + pin-majors-at-kickoff (version hell = #1 agent burn);
  deploy dashboards/env config explicitly human-owned; post-skeleton working code is sacred
- LITE boot profile for mechanical cards (core law only — token/cost relief)
- Deps-ownership ritual: one track owns installs per phase; lockfiles never hand-merged
- The kit is FROZEN during events (Prime Directive 7); only /retro edits it
- Gauntlet: prize-portfolio strategy (main + 1–2 sponsor tracks ≤2h integration) + the
  consensus asterisk ("steal the obvious idea's clarity, differentiate the delivery")
- Judge-lens: FIRST-90s metric; generic-event judge dossier; pre-empt-the-weakness beat
- Demo-script: judge-hands-on beat, mandatory personal-stake spine slot, video scripted
  T-4h / recorded T-3h; standups: 2 scheduled minimum + event-driven triggers
- Venue doctrine generalized beyond SIH: every venue's Wi-Fi is a rumor

### Defended (unchanged on purpose)
Skeleton law, contracts, cold-startable cards, DEMO_FALLBACK, paperwork-stays-human.

### Declined (recorded)
The meta-consensus paradox (WarRoom teams becoming the new predictable field) — real,
logged as a horizon item; revisit when the kit has market share worth countering.
Volt→Forge persona merge — deferred: personas are opt-in files that cost nothing unused,
and Node-vs-Python track needs differ per project; revisit if two more retros agree.

## v1.2 — Deck Engine + Internal Mini-Finale (2026-07-18)

### Added
- `bolt-slides` skill — stackblitz/bolt-slides (MIT) bundled: interactive web decks
  (Vite+React, presenter mode, click-builds) authorable by any agent. Modifications
  disclosed in-file: upstream's hidden demo-trigger section removed; skill renamed to
  match folder convention.
- `scripts/new-deck.sh` — one-command deck scaffold into `deck/` (degit + install)
- `ppt-builder` two-engine routing: Engine A bolt-slides (free formats), Engine B
  prescribed-template paste (SIH idea-PPT); one source→slide map feeds both
- SIH arena §6 rewritten: internal round now has **mini-finale mode** (working solution
  at QUICK scale: skeleton + one wow beat + both deck engines + /eval-round as dress
  rehearsal) alongside paper mode; organizers' "demo expected?" question decides
- Herald wiring, /prep lane 2 pre-scaffold caveat, README credits

## v1.1 — SIH Arena Pack (2026-07-18)

Born from a full Smart India Hackathon simulation (36h software edition, 6-member team,
three evaluation rounds, closed-room viva). Ten failure modes found; nine fixed, one
accepted (see simulation report).

### Added
- `.agents/rules/arenas/sih.md` — SIH overlay: evaluation rhythm (skeleton law → hour 8,
  always-evaluable law), 6-member role model (storyteller + intel seats, shift plan),
  govt-context judging bank (DPDP, India Stack/Bhashini, offline-first, feature phones,
  cost-per-district), venue resilience (local-first demo posture), rival awareness,
  internal-round paper mode
- `/eval-round` workflow — T-60m battle ritual + T+15m feedback triage for every judge/
  mentor touchpoint; produces `board/EVAL-PACK-<n>.md`
- `/prep` workflow — the shortlist→finale weeks: risk-retirement spikes, fallback corpus,
  mock eval rounds, intel dossier, venue kit
- `feedback-triage` skill — ADOPT-NOW / IF-TIME / DEFEND / DECLINE with cut-list math,
  DECISIONS traceability, scripted next-round callbacks
- `ppt-builder` skill — prescribed-format decks derived from WarRoom docs (idea PPT,
  per-round deltas, finale deck), one lineage, markdown source
- Templates: `EVAL-PACK.md`, `VIVA-DOSSIER.md`
- `judge-lens` Mode 4 — rival simulation for shared problem statements
- `TEAM.md` shift-plan table for 36h+ continuous events
- AGENTS.md arena-pack override rule; sync.sh mirrors `rules/arenas/`

### Design principle
Arena packs are optional overlays — the core kit stays lean for generic hackathons;
ponytail applies to WarRoom itself.

## v1.0 — Initial release (2026-07-18)

69 files: 12 personas, 12 skills (incl. bundled ponytail + caveman, MIT, credited),
10 workflows, 11 templates, 4+7 rule packs, 3-tool sync (Antigravity, Claude Code,
OpenCode), team + solo modes.
