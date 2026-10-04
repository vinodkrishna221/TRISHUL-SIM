#!/usr/bin/env bash
# WarRoom sync — mirror .agents/ (source of truth) into tool-native locations.
# Deterministic, idempotent, zero deps beyond bash+node. Re-run any time .agents/ changes.
set -euo pipefail
cd "$(dirname "$0")/.."
ROOT="$(pwd)"
SRC="$ROOT/.agents"

echo "⚔️  WarRoom sync — $ROOT"

# ---------- Claude Code ----------
mkdir -p .claude/agents .claude/commands .claude/rules .claude/skills
cp -f "$SRC"/personas/*.md .claude/agents/
cp -f "$SRC"/workflows/*.md .claude/commands/
cp -f "$SRC"/rules/*.md .claude/rules/ 2>/dev/null || true
mkdir -p .claude/rules/stack && cp -f "$SRC"/rules/stack/*.md .claude/rules/stack/
if compgen -G "$SRC/rules/arenas/*.md" > /dev/null; then
  mkdir -p .claude/rules/arenas && cp -f "$SRC"/rules/arenas/*.md .claude/rules/arenas/
fi
for d in "$SRC"/skills/*/; do
  name="$(basename "$d")"
  mkdir -p ".claude/skills/$name"
  cp -f "$d"SKILL.md ".claude/skills/$name/SKILL.md"
done
echo "  ✅ Claude Code: .claude/{agents,commands,rules,skills} + CLAUDE.md bridge"

# ---------- OpenCode (v1 stable) ----------
mkdir -p .opencode/agent .opencode/command .opencode/skill
cp -f "$SRC"/personas/*.md .opencode/agent/
cp -f "$SRC"/workflows/*.md .opencode/command/
for d in "$SRC"/skills/*/; do
  name="$(basename "$d")"
  mkdir -p ".opencode/skill/$name"
  cp -f "$d"SKILL.md ".opencode/skill/$name/SKILL.md"
done
echo "  ✅ OpenCode: .opencode/{agent,command,skill} (AGENTS.md read natively)"

# ---------- Antigravity ----------
# Reads AGENTS.md + .agents/rules + .agents/workflows natively (that IS our source dir).
# Legacy singular-path fallback for older builds:
if [ ! -e ".agent" ]; then ln -s .agents .agent 2>/dev/null || true; fi
echo "  ✅ Antigravity: native .agents/ (legacy .agent symlink ensured)"

# ---------- MCP mirrors ----------
node - <<'EOF'
const fs = require('fs');
const canon = JSON.parse(fs.readFileSync('.agents/mcp/servers.json', 'utf8')).servers || {};
const strip = o => Object.fromEntries(Object.entries(o).filter(([k]) => !k.startsWith('_')));
const servers = Object.fromEntries(Object.entries(canon).map(([k, v]) => [k, strip(v)]));

// Claude Code + Antigravity share the mcpServers shape
const mcpServers = { mcpServers: servers };
fs.writeFileSync('.mcp.json', JSON.stringify(mcpServers, null, 2) + '\n');
fs.writeFileSync('.agents/mcp_config.json', JSON.stringify(mcpServers, null, 2) + '\n');

// OpenCode: merge into opencode.json without clobbering user keys
let oc = {};
try { oc = JSON.parse(fs.readFileSync('opencode.json', 'utf8')); } catch {}
oc['$schema'] = oc['$schema'] || 'https://opencode.ai/config.json';
const ocEnv = e => Object.fromEntries(Object.entries(e).map(([k, v]) =>
  [k, String(v).replace(/\$\{(\w+)\}/g, '{env:$1}')]));  // OpenCode expands {env:VAR}
oc.mcp = Object.fromEntries(Object.entries(servers).map(([k, v]) => [k, {
  type: 'local',
  command: [v.command, ...(v.args || [])],
  ...(v.env && Object.keys(v.env).length ? { environment: ocEnv(v.env) } : {})
}]));
fs.writeFileSync('opencode.json', JSON.stringify(oc, null, 2) + '\n');
console.log('  ✅ MCP: .mcp.json · .agents/mcp_config.json · opencode.json');
EOF

# ---------- Optional: fetch official Convex AI rules ----------
if [ "${1:-}" = "--convex-rules" ]; then
  echo "  ⏬ fetching official Convex AI rules…"
  curl -sL --max-time 30 "https://convex.link/convex_rules.txt" -o "$SRC/rules/stack/convex-official.md" \
    && echo "  ✅ .agents/rules/stack/convex-official.md" \
    || echo "  ⚠️  fetch failed — grab manually from docs.convex.dev/ai"
fi

echo "⚔️  sync complete. Commit the mirrors or keep them gitignored (default: gitignored)."
