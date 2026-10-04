# Stack Pack — Node API / Express / Nest (load on BE-NODE track)

Defaults assume **Node 22+, TypeScript strict, tsx for dev**.

## Choose the flavor (once, at blueprint)

- **Express 5 + zod** — default for hackathons: zero ceremony, everyone knows it.
- **NestJS** — only if the team already lives in Nest daily. Its DI + module ceremony costs
  real hours under pressure; Direction Check anyone reaching for it "because proper".
- If the frontend is Next.js and the API has no second consumer → Direction Check: server
  actions / route handlers may make this whole service unnecessary (ponytail rung 1).

## Hackathon defaults (Express flavor)

- `tsx watch src/index.ts` for dev; no build step until deploy (`tsup` then).
- **zod schemas mirror `board/contracts/`** — parse at the edge (`schema.parse(req.body)`),
  types inferred via `z.infer<>` and shared to consumers from one `contracts.ts`.
- Middleware order: `cors` (allowed origins incl. deployed FE) → `express.json()` → routes →
  central error handler returning `{code, message}` per contract.
- Async route errors: Express 5 forwards rejected promises to the error handler natively —
  still keep the central handler the ONLY place that shapes error responses.
- Structure: `src/routes/<domain>.ts` (Router per domain), `src/services/<domain>.ts` (logic),
  `src/db.ts`, `src/contracts.ts`. No controllers/repositories layers — that's Nest cosplay.
- `/health` route from minute one; `PORT` from env (deploy platforms inject it).
- Logs: `console.log` with a prefix convention `[svc:leads]` is FINE at a hackathon; pino only
  if you already know it.

## Traps that kill demos

- Forgetting `0.0.0.0` bind on containers/free tiers (localhost-only = invisible service).
- CORS with credentials needs exact origin (no `*`) + `credentials: true` both sides.
- ESM/CJS import hell: set `"type": "module"` at init and never mix `require`.
- Free-tier cold starts: first demo request is slow → Patch pre-warms before the pitch.
