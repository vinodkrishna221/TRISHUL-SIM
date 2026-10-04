---
name: ideation-gauntlet
description: Anti-consensus ideation procedure for hackathon problem statements. Predicts and bans the field's obvious ideas, forces divergence under constraint lenses, triages by demo-impact vs build-cost, and judge-simulates survivors into an IDEA-BRIEF. Use during /ideate or whenever choosing what to build.
---

# Ideation Gauntlet

Purpose: produce an idea the field won't have, that this team CAN build in the timebox,
that scores on the actual rubric. Run by Maverick + Scout + Oracle + Orion. 60-90 minutes
total. Output: `board/IDEA-BRIEF.md` (template: `.agents/templates/IDEA-BRIEF.md`).

## Inputs (collect before starting)

Problem statement (verbatim) · judging criteria (verbatim if published) · sponsor(s) ·
hackathon duration + team size · hard constraints (required APIs, themes, tracks).

## The seven steps

**1. Interrogate the PS (Oracle + Orion, 10m).** A problem statement is a claim written
by someone with an agenda — parse four layers before anything generates:
- Stated pain + stated user, verbatim (these nouns become vocabulary)
- **Author intent:** what does the PS-setter buy if this is solved (API adoption, a KPI
  they report, a fundable pilot)? Scored even when unwritten.
- **Chain of loss:** decompose WHERE the problem actually bleeds (post-harvest loss =
  grading → storage → transit → distress-sale timing). Each link is a different product.
  Pick ONE link to attack — usually the unsexy one.
- **Rubric extraction:** criteria → weights table. Unpublished? Assume 25/25/25/25
  (innovation/technical/impact/presentation) and mark ASSUMED.

**2. Ground truth (Scout, 20-30m, timeboxed hard).** Search is EVIDENCE GATHERING, not
idea shopping — the web is where ideas get killed or confirmed, and the model's job here
is reading fresh documents, not recalling training data. Three parallel sweeps, fished
below the surface (deep-water source ladder: see prior-art-check):
- PAIN: proof in the affected users' own words — regional-language forums, news archives,
  RTI-based journalism, user comments/videos — quotes + links, region-specific
- ASSETS: datasets/APIs/rails usable TODAY, auth model + limits actually verified
  ("has an API" ≠ "usable in 36h") — including delivery rails a solution could ride
  (WhatsApp, IVR, missed-call, UPI, DigiLocker, Bhashini, an existing scheme's network)
- FIELD: winner archaeology + sponsor intel + existing products' visible gaps + **failed
  pilots and postmortems** (the highest-signal documents online) →
  `OVERDONE / WON-BEFORE / OPEN-GAP / SPONSOR-WANTS`
Close the sweep with the killer question: **why hasn't this been solved already?** If the
answer is "it was — adoption failed because X", then X is your real problem statement.

**3. Consensus map → BAN LIST (Maverick, 10m).** Simulate the average ChatGPT brainstorm for
this exact problem statement, faithfully: list 12-15 ideas with the phrasing other teams will
use ("AI-powered dashboard for…", "RAG chatbot over…", "marketplace connecting…").
These are BANNED. A banned idea may only re-enter if transformed beyond recognition
(different user, different delivery, different core verb).

**4. Divergence rounds (Maverick, 20m).** Apply lenses; 2-3 candidates each:
inversion · no-UI delivery (WhatsApp/IVR/SMS) · unsexy-core-done-brilliantly ·
domain collision · constraint flip (offline / ₹6k phone / zero-signup / non-reader) ·
agent-native (what's only possible because agents ACT). 10-15 raw candidates, each:
one-line pitch + pain it kills + why the field won't have it + gut size (S/M/L).
**Every candidate must cite which step-2 finding it kills — no evidence link, no
candidacy.** Collision-lens discipline: transfer a MECHANISM from a far domain, then let
the evidence decide whether it survives this problem's verified constraints. Analogy
proposes; evidence disposes.

**5. Feasibility triage (Orion, 10m).** Score DEMO-IMPACT (1-5: how hard does the 3-min demo
hit?) × BUILD-COST (1-5 against OUR stack + agent velocity + hours remaining). Kill:
cost 5s, impact ≤2s, anything whose wow moment can't exist by 30% time. Keep 4-6.

**6. Judge simulation + ground-deployability battery (Oracle, 15m).** Score survivors per
rubric criterion 1-10 + sponsor-fit bonus + one line each: the wow moment, the killer
judge question, the one-sentence pitch. Then run the battery — "works Monday morning"
is scored, never assumed:
- **Rails test:** rides infrastructure that already reaches the user (WhatsApp, IVR,
  missed-call culture, UPI, an existing scheme's delivery network) — or demands new
  behavior plus an install? Rail-riders beat new-behavior products on deployment, always.
- **Operator test:** who runs it Monday morning without you (a clerk, an FPO, a shop
  owner)? "Nobody" = a demo, not a solution.
- **Zero-training test:** first successful use in under 2 minutes, no tutorial?
- **Cold-start test:** does day-one value require data you don't have yet?
- **Unit economics:** cost per user/district in real currency, sourced, said out loud.
- **Zero-bars test:** what still works at no signal?
Rank by weighted rubric score; battery failures cap the Impact score and land in the
IDEA-BRIEF risk register with a plan B.

**7. Decision (human, 5m).** Present top 3 as a table: pitch · unfair advantage · wow moment ·
build risk · rubric score. Human picks (gut counts — flag if they pick against the scores,
once, then commit). Fill IDEA-BRIEF.md → feeds `/kickoff`.

## Prize portfolio (step 6 addendum)

Score each survivor for the MAIN track **and** for sponsor side-tracks whose integration
costs ≤2h — sponsor prizes have weaker competition, and one build can win twice.
Deliberately pick main + 1–2 cheap sponsor tracks at decision time; Scout's sponsor-intel
names which API to make load-bearing. The IDEA-BRIEF records the portfolio, and the
submission names each sponsor tech explicitly (prize sweeps are keyword searches).

## The consensus asterisk (field-tested)

The ban list is correct at innovation-scored events. At sponsor-judged events, a
well-executed OBVIOUS idea with a flawless demo repeatedly beats novel-but-confusing.
The synthesis: **steal the obvious idea's clarity, differentiate the delivery** — a
banned idea transformed by a delivery lens (no-UI/IVR, offline-first, agent-native) is
legal and is where real wins come from. The divergence lenses are the money; the ban
list is the forcing function, not the prize.

## Quality bars

- Step 3 before step 4, always — divergence without the ban list regresses to consensus.
- Every step-2 claim carries a link; unverified = labeled.
- If ALL survivors are M/L builds with late wow moments, rerun step 4 with harsher lenses —
  the right answer often scores "too small" on ambition and demos like a monster.
- Total gauntlet ≤ 90 minutes. An idea chosen at hour 5 loses to a worse idea chosen at hour 1.
