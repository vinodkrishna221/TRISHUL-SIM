---
name: prior-art-check
description: Five-minute verdict on build vs wrap vs steal before building any non-trivial component, plus deep modes for hackathon-winner archaeology and sponsor intel. Use before building, and during /ideate ground-truth sweeps.
---

# Prior-Art Check

Purpose: never spend 4 hackathon hours building what `npm i` or a free-tier API delivers in
20 minutes — and never pitch what forty teams already pitched last edition. Run by Scout.

## Mode 1 — Component check (5 minutes, before any non-trivial build)

Triggers (non-exhaustive): auth, rich text/editors, charts, tables-with-features, parsers,
schedulers/cron, vector search, OCR, transcription, payments-ish, PDF generation, scraping,
diagram/canvas, notifications.

Ladder (stop at first hit — mirrors ponytail rungs 2-5):
1. Framework/platform already does it? (Next.js, FastAPI, Convex, browser API)
2. An installed dep does it?
3. A maintained library does 90%? (check: last release <12mo, real docs, issues alive)
4. A free-tier API does 90%? (check: signup friction, rate limits, latency — VERIFY the
   free tier exists TODAY, pricing pages move)
5. Only then: build.

Verdict format (chat + one line in STATUS if it changed a plan):
```
PRIOR-ART: build | wrap <lib/API> | steal pattern from <repo>
WHY: <one line>   COST: <integration guess>   LINKS: <docs/repo>
RISK: <the catch — auth model, limits, license, abandonware smell>
```
Rules: check the ACTUAL docs (hallucinated APIs burn hours) · license sanity (MIT/Apache
fine; GPL flag for closed demos is a non-issue at hackathons, note it anyway) · "wrap" beats
"build" beats "fight a giant framework for one feature".

## Mode 2 — Winner archaeology (/ideate, 10-15m)

Search Devpost + past editions of THIS hackathon + similar problem statements:
- WON-BEFORE: 3-5 winners, the pattern behind them (not the topic — the *shape*: live
  hardware beat, social-impact + real pilot, dev-tool with instant aha)
- OVERDONE: categories judges are numb to this season (the RAG chatbot, the generic
  dashboard, the "copilot for X" with no action)
- OPEN-GAP: the recurring unserved angle
Output feeds the gauntlet's ban list and divergence rounds. Links required.

## Mode 3 — Sponsor intel (/ideate, 10m)

Sponsor's docs + launches + blog + devrel posts this year →
`SPONSOR-WANTS: <adoption target>` · `UNDER-LOVED API: <the one to make load-bearing>` ·
`JUDGE BAIT: <one sentence for the pitch that names it>`. If multiple sponsors: rank by
prize relevance and judge presence.

## The deep-water source ladder (applies to every mode)

Surface search returns listicles — the tip of the ocean, and the same tip every other
team is reading. Fish below it, in order of signal per minute:

1. **Primary pain:** regional-language forums, user comments/videos, news archives,
   RTI-based journalism — the affected people in their own words, not summaries of them
2. **Government & institutional data:** open data portals (data.gov.in and sector
   boards), committee and annual reports, scheme documents, budget lines — where the
   problem's real numbers and existing delivery rails live
3. **The graveyard:** failed pilots, shutdown startups' postmortems, "why we failed"
   writeups, abandoned GitHub repos with honest READMEs — the highest-value documents
   on the internet, because they name the constraint that kills naive solutions
4. **Academic & gray literature:** field studies, NGO/foundation evaluations — abstracts
   and conclusions only, hard timebox
5. **Working deployments elsewhere:** the same problem solved in another state/country —
   and specifically WHICH rail they rode to reach users

Rules of the ladder: every claim keeps its link · no link = labeled `unverified-memory` ·
timeboxes hold (partial evidence now beats perfect evidence late) · every sweep closes
with "why hasn't this been solved already?" — the answer usually IS the real problem.
