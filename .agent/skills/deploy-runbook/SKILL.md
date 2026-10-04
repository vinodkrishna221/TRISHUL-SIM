---
name: deploy-runbook
description: Per-platform deploy checklists (Vercel, Render, Railway, Convex, Atlas) plus the smoke ritual, pre-warm ritual, and freeze calendar. Use from the walking-skeleton moment and during /integrate and /ship.
---

# Deploy Runbook

Purpose: deploys are boring, early, and continuous — never an event. Run by Patch.
Skeleton goes live at the 30% mark; every merge after ships to the same URLs.

## Platform checklists (tick in order; each line is a classic 2am failure)

**Vercel (Next.js FE)** — import repo → framework auto → env vars (every `NEXT_PUBLIC_*` +
server keys) BEFORE first deploy → `next build` green locally first → remote image patterns
configured → after deploy: check a dynamic route + a server action, not just `/`.
**Render/Railway (FastAPI)** — start cmd `uvicorn app.main:app --host 0.0.0.0 --port $PORT`
→ `/health` route exists → env vars synced from `.env.example` → CORS updated with the
Vercel URL → free-tier sleep: expect 30-60s cold start (pre-warm ritual exists because of
this line).
**Render/Railway (Node)** — `npm start` runs the BUILT output → binds `0.0.0.0:$PORT` →
`"type": "module"` consistent → CORS exact origins (no `*` with credentials).
**Convex** — `npx convex deploy` → action env vars set in Convex DASHBOARD (not .env — the
classic silent killer) → `NEXT_PUBLIC_CONVEX_URL` switched on Vercel → run the seed mutation
against prod.
**Atlas** — network access `0.0.0.0/0` for the hackathon → simple password (URL-encoding
bugs) → seed against the prod cluster → indexes created by seed script.

## The smoke ritual (after EVERY merge to main — 3 minutes)

```
seed → boot/deploy → click the demo path (or run scripts/smoke.*) → check: no console
errors, no 500s, LLM call returns, realtime updates → STATUS line: "smoke ✅ @ <sha>"
```
Red smoke = merges frozen until green. Revert beats archaeology under pressure.

## Pre-warm ritual (T-10m before pitching)

Hit every free-tier URL (kill cold starts) → one scripted LLM call (warm the key, catch the
outage NOW) → open all demo tabs + second-screen views → verify flag set = DEMO-SCRIPT's
known-good list → seed refresh if state drifted → backup video queued in a tab.

## Freeze calendar (posted in STATUS at kickoff; Patch enforces without apology)

`T-5h risky-feature freeze` · `T-4h` pre-warm duplicate project + script demo video ·
`T-3h feature freeze` (cut list executes) + record video · `T-2h` submission live +
merge freeze (endgame tiering) · `T-90m deploy freeze` (build queues spike when 200
teams ship at once — last deploy triggers by now) · `T-45m` final backup video refresh.
**All-events doctrine, not just SIH:** every venue's Wi-Fi is a rumor — local mirror +
phone hotspot fallback travel to every in-person event.
