# ⚔️ WarRoom

**A hackathon operating system for AI-agent teams.** One `.agents/` brain — 12 specialist
personas, battle-tested rules, 12 skills, 10 slash-command workflows — synced into
**Antigravity**, **Claude Code**, and **OpenCode**, so every teammate's agent shows up
already knowing the plan, the stack, and the standards.

Built for the loop most teams do badly by hand:

```
/ideate → /kickoff → /blueprint → /phase-plan → /missions
   ↓ (build loop, per member, remote)
pick task → build with persona → PR → fresh-eyes review → merge
   ↓
/standup … /pivot (if needed) … /integrate → /ship → /retro
```

## 60-Second Setup (start of every hackathon)

```bash
git clone <this-template> my-hack && cd my-hack
./scripts/new-hackathon.sh        # name the project, pick team/solo, toggle stacks
./scripts/sync.sh                 # mirror the brain into all three tools
git init && git add -A && git commit -m "chore: warroom init" && git push
```

Then open your tool of choice and run `/ideate` (or `/kickoff` if the idea is fixed).

**Adoption note: only the LEAD needs to read the whole kit.** Teammates read
`START-HERE.md` + their own mission file — the context capsules carry everything else.
First event with a new team? Run `/drill` (a 2-hour rehearsal) before it counts —
field data says it's the difference between 70% and full speed.

## What Makes It Different

- **The Ideation Gauntlet** — predicts the ideas every other team's ChatGPT will pitch,
  bans them, and forces divergence under constraint lenses. Then triages by
  demo-impact × build-cost so you never pick the "impressive but unbuildable in 36h" trap.
- **The Problem Prospector** — for self-proposed events (SIH Open Innovation): mines
  real problems from streams where institutions admit them with numbers (CAG audits,
  parliamentary questions, grievance stats, tenders, failed pilots) — triangulated,
  chair-test filtered, and shipped as submission-ready PS docs. Found, not invented.
- **Judge simulation all the way down** — Oracle scores the idea AND the evolving build
  against the actual rubric at every standup. You never drift from what's scored.
- **Contract-first missions** — API/event contracts are committed files; teammates (and
  their agents) build in parallel without integration hell.
- **Advisor protocol** — personas are required to push back with evidence when you're
  over-scoping or rebuilding something that already exists. (Scout runs prior-art checks
  before any non-trivial build.)
- **Solo mode** — one human? The cast becomes your team: missions assigned to personas,
  parallel agent sessions via git worktrees, and a fresh-context Warden review before
  every merge. Branches + PRs stay.
- **Ponytail + Caveman** (bundled, MIT, upstream-credited) — YAGNI-enforced code,
  token-compressed chatter. Discipline for what gets built and what gets said.

## The Cast

Orion (architect) · Maverick (contrarian ideator) · Oracle (judge simulator) ·
Scout (intel & prior-art) · Nova (Next.js) · Forge (FastAPI) · Volt (Node) ·
Atlas (Mongo/Convex) · Sage (AI/RAG/voice/MCP) · Warden (review) · Patch (integrate/deploy) ·
Herald (pitch & demo).

## Read Next

- **Leads:** `docs/LEADER-MANUAL.md` — the command manual: the decisions only you make,
  per-phase sign-offs, fleet management, the ten ways leads lose
- **Members:** `docs/MEMBER-MANUAL.md` — the execution manual: the loop, the five laws,
  agent craft, eval-round duties, the when-things-go-wrong table
- `docs/WORKFLOW.md` — the full playbook: timelines (24/36/48h), team + solo modes, rituals
- `docs/TOOL-PATHS.md` — where everything lands in each tool, manual fallbacks
- `AGENTS.md` — the always-loaded law every agent obeys

## Credits

- [ponytail](https://github.com/DietrichGebert/ponytail) © Dietrich Gebert, MIT — bundled verbatim
- [caveman](https://github.com/JuliusBrussee/caveman) © Julius Brussee, MIT — bundled verbatim
- [bolt-slides](https://github.com/stackblitz/bolt-slides) © StackBlitz, MIT — deck engine + skill (bundled, hidden demo-trigger removed)
- Command-vocabulary inspiration: GitHub Spec Kit · Scale-gate inspiration: BMAD-METHOD

MIT. Fork it, win with it, `/retro` it better.
