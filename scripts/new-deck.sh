#!/usr/bin/env bash
# WarRoom deck scaffold — bolt-slides (stackblitz/bolt-slides, MIT) into deck/
# Usage: ./scripts/new-deck.sh [target-dir]   (default: deck)
set -euo pipefail
cd "$(dirname "$0")/.."
TARGET="${1:-deck}"

if [ -e "$TARGET" ]; then echo "⚠️  $TARGET/ already exists — presenting deck lives there; aborting."; exit 1; fi

echo "⚡ scaffolding bolt-slides → $TARGET/"
if command -v npx >/dev/null 2>&1 && npx -y degit stackblitz/bolt-slides "$TARGET" 2>/dev/null; then
  echo "  ✅ degit clone"
else
  git clone --depth 1 https://github.com/stackblitz/bolt-slides "$TARGET" && rm -rf "$TARGET/.git"
  echo "  ✅ git clone (degit unavailable)"
fi

(cd "$TARGET" && npm install)

cat <<NEXT

⚡ Deck ready at $TARGET/ (separate Vite app — never shares deps with the product)
  present:  cd $TARGET && npm run dev     (P = presenter · G = grid · A = annotate)
  author:   summon Herald; load BOTH skills:
              .agents/skills/bolt-slides/SKILL.md   (HOW slides are built)
              .agents/skills/ppt-builder/SKILL.md   (WHAT goes on them — source→slide map)
            then author $TARGET/src/App.tsx from the REAL docs (PRD, IDEA-BRIEF, STATUS).
  rules:    delete the starter slides · theme tokens.css :root only · engine src/deck/ is LOCKED
NEXT
