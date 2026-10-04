# Tool Paths — where the brain lands in each tool

`.agents/` is the single source of truth. `scripts/sync.sh` mirrors it. This file is the
map (and the manual fallback if a tool changes its conventions mid-season — they do).

| WarRoom source | Antigravity | Claude Code | OpenCode (v1) |
|---|---|---|---|
| `AGENTS.md` (always-loaded law) | native | via `CLAUDE.md` → `@AGENTS.md` first line | native (project + global merge) |
| `.agents/rules/*.md` | native (`.agents/rules/`) | `.claude/rules/` (copied) | indexed from AGENTS.md links |
| `.agents/personas/*.md` | open file, adopt persona | `.claude/agents/` (subagents) | `.opencode/agent/` |
| `.agents/workflows/*.md` | native (`.agents/workflows/`) | `.claude/commands/` (slash) | `.opencode/command/` (slash) |
| `.agents/skills/*/SKILL.md` | referenced by path from AGENTS.md | `.claude/skills/` | `.opencode/skill/` |
| `.agents/mcp/servers.json` | `.agents/mcp_config.json` | `.mcp.json` | `opencode.json` `mcp` block |

## Known quirks (July 2026 — re-verify each season, conventions drift)

- **Claude Code does NOT read `AGENTS.md` natively** — the `CLAUDE.md` bridge (`@AGENTS.md`
  as its first line) is the officially documented pattern. Don't delete `CLAUDE.md`.
- **Antigravity** moved from `.agent/` (singular) to `.agents/` (plural); sync keeps a legacy
  `.agent` symlink for older builds. MCP: workspace `.agents/mcp_config.json`; if your build
  expects the global config instead, add servers via the IDE's MCP settings UI (global
  scope lives under `~/.gemini/config/`).
- **OpenCode**: this kit targets **stable v1** (`opencode.ai/docs`). The 2.0 beta changes the
  agent permission schema — if your teammate runs the beta and agents misbehave, that's why;
  frontmatter here uses only fields both accept (`name`, `description`, `mode`).
- **Persona frontmatter is a union** (`name`, `description`, `mode`) — each tool ignores the
  fields it doesn't know. Don't add tool-specific fields to shared persona files.

## Fallback: any tool, zero native support

Everything is plain markdown. Worst case in ANY tool: "Read AGENTS.md, adopt
.agents/personas/<x>.md, follow .agents/workflows/<y>.md" pasted as the first message
gives you 90% of the system. The files, not the wiring, are the product.

## Optional: syncing to OTHER tools (Cursor, Copilot, Windsurf teammates)

[rulesync](https://github.com/dyoshikawa/rulesync) (v14+) generates configs for most agents
from one source. Quick recipe: point its source at generated output of this kit
(`npx rulesync init`, map `.agents/rules/*` into `.rulesync/rules/`, then
`npx rulesync generate --targets <tool>`). We don't depend on it for the core three —
`sync.sh` is deterministic and offline.

## Refresh official vendor rules (optional, per project)

- Convex: `./scripts/sync.sh --convex-rules` (fetches the official AI rules file)
- Next.js / MongoDB: both vendors publish agent rules — link them into
  `.agents/rules/stack/` if your build leans hard on either; our packs cover the
  hackathon-critical subset.
