---
description: Run the Ideation Gauntlet — out-think the field's ChatGPT-consensus ideas and produce board/IDEA-BRIEF.md
argument-hint: "[paste the problem statement + judging criteria + sponsors + duration + team size]"
---

# /ideate — The Ideation Gauntlet

Orchestrates: Oracle → Scout → Maverick → Orion → Oracle → human decision.
Procedure bible: `.agents/skills/ideation-gauntlet/SKILL.md` (follow its seven steps exactly).
Output: `board/IDEA-BRIEF.md` from `.agents/templates/IDEA-BRIEF.md`. Budget: ≤90 minutes.

## Run

1. **Collect inputs** from $ARGUMENTS / the human: problem statement verbatim, judging
   criteria verbatim (or mark ASSUMED), sponsors, duration, team size, hard constraints.
   Missing pieces: ask once, together, not one-by-one.
2. **Oracle** — rubric extraction (skill: judge-lens mode 1 table, empty scorecard ready).
3. **Scout** — ground-truth sweep, 30m hard timebox (skill: prior-art-check modes 2+3 for
   archaeology + sponsor intel; PAIN/ASSETS/FIELD bullets, links mandatory).
4. **Maverick** — consensus map → BAN LIST (12-15 ideas, phrased as the field will phrase
   them). Show the human the ban list — it's motivating and occasionally someone insists on
   a banned idea; that insistence gets ONE Direction Check, then their call.
5. **Maverick** — divergence rounds under the six lenses → 10-15 candidates.
6. **Orion** — feasibility triage: impact × cost matrix, kill list applied → 4-6 survivors.
7. **Oracle** — judge simulation on survivors → ranked scorecards + wow-moment sketch each.
8. **Present top 3** to the human as the decision table (pitch · unfair advantage · wow ·
   risk · score). Human picks. Fill IDEA-BRIEF.md completely, commit it:
   `docs: idea brief — <chosen name>`.

## Gates

- No step skipped even in QUICK mode (compress timeboxes, not steps — the ban list is the
  whole edge).
- Every claim in the brief that came from step 3 keeps its link.
- End state: IDEA-BRIEF.md committed + the one-line pitch pinned in team chat + ready for
  `/kickoff`.
