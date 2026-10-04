---
name: fit-audit
description: Audit whether THIS project's solution needs the kit revamped — new rules, stack packs, skills, or personas — and whether it does NOT. Runs auto at /blueprint after the stack record; edits core kit files through the diff-approval ritual. The kit's one sanctioned self-modification window.
---

# Fit Audit

The kit ships with law for the team's USUAL stack. Some solutions need more — an
unfamiliar framework, a protocol, a domain with sharp edges — and building 30 hours on
generic law is how agents write confident nonsense. Equally: most projects need NOTHING
new, and fabricating packs is bloat wearing a helmet. This skill is the strict thinking
that decides which — both directions, every project. Run by Orion (chair), Scout
(research/fetch), Oracle (necessity votes).

## Step 1 — Capability inventory (from PRD + ARCHITECTURE stack record)

List every capability the chosen solution touches:
languages/frameworks · data stores · external APIs/SDKs (named, with auth models) ·
protocols (MQTT, WebRTC, serial, SMS/IVR gateways…) · domains with sharp edges
(payments, maps, health data, gov APIs, ML inference) · deploy targets · device targets.

## Step 2 — Coverage map

Each capability vs existing kit assets (`rules/stack/*`, `skills/*`, `personas/*`,
`mcp/servers.json`): **COVERED / PARTIAL / MISSING**, one line of evidence each
("nextjs.md covers App Router; nothing covers the WebRTC voice path").

## Step 3 — The necessity gate (the strict rule — both directions)

For each PARTIAL/MISSING, score three questions:
1. Will agents touch this across **≥3 task cards**?
2. Is getting it wrong **demo-fatal** (not just ugly)?
3. Is the team/agent **unfamiliar** with it (version-hell or hallucination risk)?

**Fewer than 2 yes → NO new asset.** A one-line CONTEXT note on the affected cards
covers it. Personas face a stricter bar: a new persona requires a whole TRACK of work
(≥8 cards) in that domain — otherwise the nearest existing persona takes the cards with
a stack pack behind it. Record the NOs — declined assets are logged in the audit report
so nobody relitigates at hour 20.

## Step 4 — Draft the approved-gap assets

- **New stack pack** (`rules/stack/<tech>.md`) — house format is mandatory: "Hackathon
  defaults (decided — don't relitigate)" + "Traps that kill demos" + version pins.
  Scout FIRST fetches official vendor AI/agent rules where they exist (the Convex
  precedent) and current-version docs — a pack written from model memory of an
  unfamiliar tech is the exact disease this audit exists to cure.
- **New skill** (`skills/<name>/SKILL.md`) — only for a repeatable procedure ≥2 missions need.
- **New persona** — rare (see gate above); union frontmatter, advisor duties included.
- **Edits to existing law** — when the project genuinely contradicts a rule (e.g., the
  solution IS a queue, and scope-guard side-eyes queues): add a scoped project exception
  line to the rule, never delete the rule.
- **AGENTS.md** — cast table + rules index rows for anything added; nothing added stays
  unindexed (unindexed assets don't exist under pressure).
- **MCP** — add servers to `mcp/servers.json` only if an agent needs the introspection
  across ≥3 cards (same gate).

## Step 5 — THE DIFF RITUAL (nothing writes without this)

Present to the human, in one message: files ADDED / EDITED, one line of why each, the
necessity-gate score that justified it, and the declined list. Human approves (whole or
per-item) → write → `scripts/sync.sh` → one commit: `chore: fit-audit — <n> assets` →
3-line DECISIONS entry. Then **the freeze re-engages** — this window is the kit's only
sanctioned self-edit between T-0 and /retro (10-git-protocol names it).

## Quality bars

- The audit itself is timeboxed: 30 minutes including research. It protects the build; it
  must not eat it.
- Generated packs are shorter than shipped ones (≤40 lines) — they cover THIS project's
  usage, not the technology.
- If the audit's answer is "nothing needed", say exactly that in one line and move on —
  a clean bill of health is the most common correct outcome and costs zero files.
- /retro reviews every fit-audit asset: the good ones graduate into the core kit;
  the rest die with the clone.
