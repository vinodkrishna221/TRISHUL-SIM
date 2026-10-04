# Stack Pack — MongoDB (load on DATA track when Mongo chosen)

Defaults assume **MongoDB Atlas free tier (M0)** + official driver.

## Hackathon defaults (decided — don't relitigate)

- **Atlas M0** created at kickoff; connection string in `.env` + `.env.example` placeholder.
  Network access: `0.0.0.0/0` during the hackathon (yes, really — you don't know your deploy
  platform's egress IPs; tighten after judging).
- **Official driver over Mongoose.** Schemas already live in `docs/SCHEMA.md` + contracts;
  Mongoose adds a modeling layer you'll fight. Validation belongs to zod/pydantic at the edge.
- **One `db.ts`/`db.py`** exporting a lazily-connected singleton client + typed collection
  getters (`getLeads(): Collection<Lead>`). Serverless (Vercel/Lambda): cache the client on
  `globalThis` to survive hot reloads/invocations — new client per request WILL exhaust M0.
- **Schema style:** embed what you demo together (post + its comments), reference what grows
  unbounded or is queried alone. When in doubt at a hackathon: embed. Joins you don't do are
  bugs you don't have.
- **IDs:** ObjectId internally; serialize to string at the contract boundary (`_id.toString()`)
  — the FE never sees ObjectId.
- **Indexes:** create in `scripts/seed.*` (idempotent `createIndex`), one per real query
  pattern (the field you filter/sort the demo on). M0 without an index on a 10k-doc scan is
  visibly slow in demos.
- **Aggregations:** fine for the wow chart, but keep each stage commented and test against
  seed data — a 5-stage pipeline written blind at 4am is a classic death.

## Traps that kill demos

- SRV connection string needs `mongodb+srv://` and URL-encoded password (`@` in a password
  breaks everything — regenerate with a simple one).
- Free tier pauses/throttles: first query after idle is slow → Patch pre-warms before pitch.
- Date fields: store real `Date` objects, not strings — sorting "2026-07-04" strings works
  until it doesn't.
