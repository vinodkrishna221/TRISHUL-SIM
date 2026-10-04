# Stack Pack — Convex (load on DATA track when Convex chosen)

Convex = reactive DB + serverless functions + realtime subscriptions in one. At hackathons
its superpower is **live-updating UI for free** — the wow moment without a websocket in sight.

> Convex publishes official AI-agent rules — `scripts/sync.sh --convex-rules` fetches the
> latest from docs.convex.dev into this pack's companion file. Treat those as authoritative
> for API details; this file is the hackathon judgment layer.

## Pick Convex over Mongo when

- The demo benefits from realtime (dashboards, collaboration, feeds, live calls) — this is
  most AI-demo apps, honestly
- Frontend is Next.js/React and the team is TS-native (Convex is TS end-to-end)
- You want zero backend deploy (Convex hosts functions; pairs with Vercel FE)

Pick Mongo when: Python backend owns the data, you need raw aggregation freedom, or the team
already has Atlas muscle memory. **Atlas + Convex together is almost always over-engineering —
Direction Check it.**

## Hackathon defaults

- `npx convex dev` runs from hour one alongside the FE dev server; schema in
  `convex/schema.ts` mirrors `docs/SCHEMA.md` (`defineTable` + `v` validators + `.index()`).
- **Queries/mutations are the contract** on a Convex project: exported function signatures in
  `convex/` ARE `board/contracts/` — keep arg validators strict.
- `useQuery` in client components = live subscription; no refetch logic, no polling, delete
  that `useEffect`.
- **Actions** (`"use node"`) for external calls (LLMs, APIs) — mutations/queries must stay
  pure; actions call mutations to persist. Scheduler (`ctx.scheduler.runAfter`) covers
  background jobs — no queue infra at a hackathon.
- Auth: Convex Auth or Clerk template ONLY if the demo needs identity; anonymous session ids
  in a table otherwise (auth is the classic hour-sink — Direction Check it).
- Seed: a `seed` internal mutation callable via `npx convex run` — same demoable-anytime rule.

## Traps that kill demos

- Env vars for actions are set in the Convex dashboard, NOT your `.env` — LLM keys silently
  missing in prod is the classic Convex demo-killer.
- Queries must be deterministic (no `fetch`/`Date.now()` branching) — that's what actions are for.
- Deploy: `npx convex deploy` for prod + `NEXT_PUBLIC_CONVEX_URL` switched on Vercel — check
  before the final hour.
