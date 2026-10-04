# Core Engineering Law (always on)

Hackathon code is not throwaway code — it is **demo-critical** code. It must work at
19:58 when the judge is standing at the table. Optimize for: works > readable > fast > pretty.

## Non-negotiables

- **TypeScript strict** on TS tracks; **pydantic v2 + type hints** on Python tracks. Types are
  the cheapest integration test a team can buy.
- **Secrets never in git.** `.env` is gitignored; every new env var gets a line in
  `.env.example` **in the same PR** and a ping in STATUS. A teammate blocked on a missing
  env var at 3am is a self-inflicted wound.
- **Errors fail loud in dev, degrade soft in demo.** Wrap only the demo-critical path in
  friendly fallbacks (cached response, seeded data), never blanket try/catch that hides bugs.
- **Seeded data from hour one.** Every feature is built against `scripts/seed.*` data, so the
  app is demoable at ANY minute of the hackathon, not just after "real" data works.
- **Feature flags over deletion.** Half-broken feature at demo time? Flag it off
  (`lib/flags.ts` / `settings.py`), don't rip it out and destabilize main.
- **Conventional commits**: `feat|fix|chore|docs(scope): message`. Commit messages and PR
  descriptions are full prose — caveman mode never applies to committed text.
- **One runnable check per non-trivial unit** (ponytail rule): the smallest assert/test that
  fails if the logic breaks. No test frameworks-for-the-sake-of-frameworks.

## Dependency policy

Adding a dependency requires passing rung 5 of the ponytail ladder (nothing installed covers
it, and it's more than a few lines to write). Name the dependency in the PR body. Heavy deps
(ORMs, state libs, UI kits beyond the chosen one) need a Direction Check first.

**The 20-minute dependency-fight law:** agents confidently write last year's API syntax
against this year's major version and will fight the framework for an hour. Fighting a
dependency >20 minutes → pin/downgrade to the stack pack's known-good version and move
on; log the pin in STATUS. Pin your majors at kickoff (lockfile committed with the
skeleton) so every agent session builds against the same reality.

**Deploy dashboards and env config are HUMAN-owned.** Agents can't see the hosting
console — they draft values and checklists; a human clicks. An agent "fixing" deploy
config blind is how staging dies at T-2h.

## LITE boot profile (token budget — mechanical cards)

For mechanical cards (renames, copy tweaks, config changes, seed edits): boot the session
with ONLY this file + the card. Skip the persona, skip the stack pack, self-review +
smoke per the endgame tiering. Full boot (persona + stack pack) is for cards that need
judgment. Caveman handles mid-session burn; LITE handles boot cost — students pay real
API bills and a rename does not need a 40k-token ceremony.

## Definition of Done (every task)

1. Deliverable named on the task card exists and runs
2. Its one check passes; demo path still green with seeded data
3. Contract untouched — or DECISIONS entry + consumers pinged
4. Mission checkboxes updated; STATUS line appended
