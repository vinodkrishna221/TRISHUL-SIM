# ⚔️ The WarRoom Playbook

The complete operating manual: how one human (or five) plus a cast of twelve AI personas
takes a problem statement to a winning demo. Read once before your first hackathon;
skim the phase you're in during one.

---

## 0 · The philosophy (why this beats the manual loop)

The old loop — prompt for PRD, prompt for architecture, prompt for phase plans, then grind
tasks one by one — loses time in four places: re-explaining context to every session,
integration hell between tracks, ideas the whole field also had, and demos that die on
stage. WarRoom attacks all four:

1. **Context lives in files, not chat history.** AGENTS.md + rules + contracts + task cards
   mean any agent session in any tool starts cold and productive in seconds.
2. **Contracts + missions make parallel work safe.** Tracks touch disjoint files; the only
   shared surface is a committed contract with a change ritual.
3. **The Ideation Gauntlet differentiates before you build.** Predict the field's ChatGPT
   consensus, ban it, diverge, then triage by demo-impact × build-cost.
4. **Demo insurance is engineered from hour one.** Seeded data, walking skeleton, backup
   video, pre-warm rituals, freeze calendar. The demo is a system, not a hope.

## 1 · The map

```
BEFORE ─── T-0 SETUP ── /ideate ── /kickoff ── /blueprint ── /phase-plan ── /missions
                                                                                │
              ┌─────────────────── THE BUILD LOOP (per member/worktree) ◄───────┘
              │  pick card → summon persona → build → PR → Warden (fresh) → merge
              │        ▲                                            │
              │        └── /standup every 3-4h ── (/pivot if red) ◄─┘
              └──► /integrate (phase boundaries) ──► /ship (last 3-4h) ──► 🏆 ──► /retro
```

## 2 · Before the hackathon (any calm evening, 20 minutes)

1. Clone this template somewhere permanent; run `./scripts/sync.sh`; open each of your
   tools once and confirm: personas visible (CC: `/agents`, OC: agent list), workflows
   callable (`/ideate` autocompletes), AGENTS.md loaded (ask "what are the prime directives?").
2. Run `/drill` — the 2-hour rehearsal mini-hack — with the real roster. Every member
   does card → branch → PR → fresh review → merge once, practices killing a drifted agent
   session and cold-starting from the card, and the team fixes its tool quirks while
   they're free. This is the single highest-ROI pre-event ritual in the kit.
3. Team: everyone clones, everyone syncs, everyone reads this file. Agree on the chat
   channel where standup blocks land.

## 3 · T-0 — the first 30 minutes (clock starts NOW)

```bash
./scripts/new-hackathon.sh "project-name" team "2026-08-02T18:00+05:30"
# fill board/TEAM.md — roster, strengths, tools, HONEST hours
git add -A && git commit -m "chore: warroom init" && git push
```
Then straight into `/ideate` with the problem statement pasted verbatim.
**Timing law:** idea locked by 5% of total time (hour 1-2 of 36). The gauntlet is 90
minutes MAX — a good idea chosen early beats a perfect idea chosen at hour 5.

## 4 · Phase timings (the spine — clock times, not vibes)

| Milestone | 24h | 36h | 48h |
|---|---|---|---|
| Idea locked (gauntlet done) | h2 | h1.5 | h2 |
| PRD + blueprint + plans + missions committed | h2.5 | h4 | h5 |
| **Walking skeleton on live URLs (30% law)** | **h7** | **h11** | **h14** |
| Core complete (P1), first big /integrate | h14 | h22 | h30 |
| Risky-feature freeze + video scripted | h19 | h31 | h43 |
| Feature freeze (cut list executes) + video recorded | h21 | h33 | h45 |
| Submission live + merge freeze (endgame tiering) | h22 | h34 | h46 |
| Deploy freeze (pre-warmed duplicate standing by) | h22.5 | h34.5 | h46.5 |
| Pitch | h24 | h36 | h48 |

The skeleton deadline is the only one that triggers automation: miss it and the next
/standup converts into /pivot, no debate.

## 5 · Planning chain (hour 1 → ~4): what each command buys you

- **/ideate** → `board/IDEA-BRIEF.md`. The edge. Ban list + divergence + triage + judge sim.
  Keep the ban list — it's pivot insurance (runner-up ideas are pre-vetted).
- **/kickoff** → `docs/PRD.md`. Five questions, zero fog; vocabulary freeze; scale gate
  (QUICK collapses ceremony for ≤24h/≤2 builders); cut list born pre-ranked.
- **/blueprint** → architecture + schema + **contracts**. The 30 minutes spent writing
  contracts here is the single highest-leverage block of the weekend — it's what lets
  missions run in parallel across cities without a call.
- **/phase-plan** → per-track cards. Every card cold-startable (CONTEXT, DELIVERABLE, TEST,
  DEPENDS, DEMO, timebox ≤4h). Load ledger caps everyone at 70%.
- **/missions** → per-member briefs (team) or QUEUE.md (solo). Context capsules: repo +
  brief = everything, in any tool.

## 6 · Team mode — remote missions

- Each member: `git clone` → `scripts/sync.sh` → open `missions/<me>-mission.md` in THEIR
  tool → tick ACCEPTED → create branch → first card.
- **The rhythm:** push ≤60m · standup blocks every 3-4h in chat (run `/standup`, paste) ·
  blocked >20m = STATUS + ping + switch cards, never silent grinding.
- **PR flow:** card done → PR (template auto-fills gates) → Warden review in a FRESH
  session — ideally a DIFFERENT member's machine (cross-review catches the most) → merge
  per DAG order → smoke ritual.
- **Contract changes:** only via the 60-second ritual (edit file + version bump →
  DECISIONS → ping consumers → then code). This rule has no exceptions; it is the tax
  that makes remote parallel work possible at all.
- Lead (from TEAM.md) chairs /pivot and owns reassignment when a mission stalls.

## 7 · Solo mode — you + the cast

You are one human commanding twelve specialists. The pipeline is identical; the roster is 1.

- `/missions` emits `missions/QUEUE.md`: persona-runs sequenced by the DAG
  (`M1 Forge/Atlas skeleton-be+data → M2 Nova skeleton-fe → M3 Sage ai-core → …`),
  PARALLEL-OK pairs marked.
- **Worktree parallelism** (your force multiplier, max 2 lanes):
  ```bash
  git worktree add ../hack-be feat/be-1-skeleton   # lane 1: OpenCode/Claude Code = Forge
  git worktree add ../hack-fe feat/fe-1-shell      # lane 2: Antigravity = Nova
  ```
  One agent session per worktree; contracts are the only shared surface; you supervise
  both lanes like a lead supervises two juniors — review at PR, not over the shoulder.
- **The solo discipline that replaces a second pair of eyes:** every mission still ends in
  a PR, and Warden reviews it in a FRESH session (new chat, or the other tool) before you
  merge. You will be tempted to skip this at hour 20. The bug you merge at hour 20 is the
  demo that dies at hour 35.
- `/standup` between missions writes the block to QUEUE.md — it's the note your next
  session reads first, and free devlog material for the submission.

## 8 · The build loop (per card — team and solo identical)

1. Read the card. Load its stack pack(s). Summon its persona.
2. Ponytail check: does rung 1 kill or shrink this card? (Say so before building.)
3. Non-trivial component? Scout's 5-minute prior-art check FIRST.
4. Build to the card's DELIVERABLE, nothing more (extras → follow-up card).
5. Run the card's TEST. Add its one-check if logic is non-trivial.
6. PR with the template; caveman never touches the PR prose.
7. Warden, fresh session: six gates, verdict ≤15m, fixes ≤20m.
8. Merge per DAG → smoke ritual → tick card → STATUS line → next card.

## 9 · Rituals reference (the heartbeat)

| Ritual | When | Who | Artifact |
|---|---|---|---|
| Standup | every 3-4h | any + Oracle re-score | STATUS + chat block |
| Smoke | after every merge | merger | `smoke ✅ @ sha` line |
| Backup video | skeleton walks; re-record on change | Patch | 60-90s screen recording |
| Pivot | skeleton misses 30% / bet dies / 2× timebox on critical path | Orion chairs, human decides | DECISIONS + repointed plans |
| Merge train | phase boundaries, T-4h | Patch + Warden | green main, verified contracts |
| Freeze calendar | T-3h/T-90m/T-60m | Patch enforces | quiet cockpit at T-15m |
| Mock panel | T-2h | Oracle | 8 answers ≤20s each |
| Retro | ≤48h after | everyone | diffs to this kit, not prose |

## 10 · Tool cheat sheet

| Action | Antigravity | Claude Code | OpenCode |
|---|---|---|---|
| Run a workflow | `/ideate` (native workflows) | `/ideate` (commands) | `/ideate` (commands) |
| Summon persona | open `.agents/personas/x.md`, "adopt this" | `@nova-frontend` subagent / `/agents` | select agent `nova-frontend` |
| Fresh Warden session | new conversation | new session (`claude` fresh) | new session |
| MCP servers | `.agents/mcp_config.json` / IDE settings | `.mcp.json` (auto) | `opencode.json` (auto) |
| Rules loaded | native `.agents/rules/` | `CLAUDE.md → @AGENTS.md` + `.claude/rules/` | `AGENTS.md` native |

One brain, three bodies: a mission started in Antigravity can be continued in OpenCode —
the repo carries the context, not the chat.

## 11 · Anti-patterns (named so they can be called out in chat)

- **The Beautiful Corpse** — 60%-done architecture at judging. Antidote: 30% skeleton law.
- **Consensus Slop** — building ban-list idea #3 anyway. Antidote: gauntlet, honestly run.
- **The Silent Drift** — contract changed in code, file not updated. Antidote: Warden gate 2, always BLOCK.
- **Hero Debugging** — 90 silent minutes on a blocker someone else caused. Antidote: 20-minute rule.
- **The 3am Cathedral** — queues/microservices/auth nobody demos. Antidote: ponytail rung 1 + Direction Check.
- **Demo Roulette** — first full run of the demo IS the pitch. Antidote: rehearse 2×, backup video, pre-warm.
- **The One Tiny Fix** — a deploy at T-20m. Antidote: freeze calendar; Patch does not negotiate.

## 11.5 · Arena packs — event-format overlays

Generic hackathons get the core kit. Formatted events get an overlay in
`.agents/rules/arenas/` that OVERRIDES conflicting core rules. Shipped: **`sih.md`**
(Smart India Hackathon) — judges evaluate three times DURING the 36h build, so the
skeleton law moves to hour 8, `/eval-round` runs before/after every touchpoint
(feedback-triage turns judge/mentor comments into traceable cards + scripted callbacks),
`/prep` fills the shortlist→finale weeks, the 6-member roster gets storyteller + intel
seats, decks come from `ppt-builder`, and the viva gets a dossier. Pattern: copy `sih.md`
as a starting point for any multi-round or govt-context event; `/retro` grows the arena
library.

## 12 · Why this wins (the recap you tell your team)

Differentiated idea (gauntlet) · scored continuously against what judges actually score
(Oracle) · built in parallel without integration hell (contracts + missions) · always
demoable (skeleton + seeds + insurance) · told as a story judges retell (Herald) ·
and the kit gets sharper every event (/retro patches the kit itself).

Go land it. ⚔️
