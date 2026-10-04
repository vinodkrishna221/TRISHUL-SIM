#!/usr/bin/env bash
# WarRoom init — run once at the start of every hackathon.
# Usage: ./scripts/new-hackathon.sh [project-name] [solo|team] ["ends-at ISO datetime"]
set -euo pipefail
cd "$(dirname "$0")/.."

NAME="${1:-}"; MODE="${2:-}"; ENDS="${3:-}"
[ -z "$NAME" ] && read -rp "⚔️  Project name: " NAME
[ -z "$MODE" ] && read -rp "⚔️  Mode [solo/team]: " MODE
[ -z "$ENDS" ] && read -rp "⚔️  Hackathon ends at (ISO, e.g. 2026-08-02T18:00+05:30): " ENDS
MODE="${MODE:-solo}"

# stamp the manifest (portable sed)
node - "$NAME" "$MODE" "$ENDS" <<'EOF'
const fs = require('fs');
const [name, mode, ends] = process.argv.slice(2);
let m = fs.readFileSync('.agents/manifest.yaml', 'utf8');
m = m.replace(/name: "UNNAMED"/, `name: "${name}"`)
     .replace(/mode: \w+(\s+#)/, `mode: ${mode}$1`)
     .replace(/ends_at: ""/, `ends_at: "${ends}"`);
fs.writeFileSync('.agents/manifest.yaml', m);
console.log(`  ✅ manifest: ${name} · ${mode} · ends ${ends}`);
EOF

mkdir -p plans missions board/contracts docs
cp -n .agents/templates/TEAM.md board/TEAM.md 2>/dev/null || true
echo "  ✅ dirs: plans/ missions/ board/contracts/  · board/TEAM.md seeded — fill the roster NOW"

./scripts/sync.sh

cat <<NEXT

⚔️  WarRoom armed: $NAME ($MODE mode)
Next:
  1. Fill board/TEAM.md (roster, hours, tools) — /missions reads it
  2. git add -A && git commit -m "chore: warroom init — $NAME" && push
  3. Open your tool (Antigravity / Claude Code / OpenCode) and run:
       /ideate   ← paste the problem statement + judging criteria + sponsors
     (or /kickoff if the idea is already locked)
NEXT
