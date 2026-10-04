---
description: Mine real problem statements from institutional evidence streams — for self-proposed / open-innovation hackathons. Scoped by industry, state, or SIH theme. Outputs ranked problem dossiers with SIH-format PS docs + gauntlet-ready briefs.
argument-hint: "[industry: <sector> | state: <state> | theme: <sih theme>]"
---

# /prospect — Problem Discovery

For events where finding the problem IS the competition (SIH self-proposed / Open
Innovation). Method bible: `.agents/skills/problem-prospector/SKILL.md` — mine, don't
brainstorm. Budget: 2-3 hours for a full run (it's pre-event work, not hackathon-clock
work — run it the week before the internal round). Output: `board/PROBLEM-DOSSIER-<n>.md`
for the top 3 + a ranked longlist.

## Run

1. **Scope (human + Orion, 5m):** parse $ARGUMENTS into a mode (INDUSTRY / STATE /
   THEME per the skill). THEME mode: translate the theme into 2-3 concrete sector ×
   user-group cells first and confirm with the human before mining.
2. **Mine (Scout, 5 pipelines in parallel, ~2h hard timebox):** admission · grievance ·
   procurement · graveyard · friction. Each pipeline logs 3-5 signals with links as it
   goes (the pipeline log is a deliverable — it's also next season's head start).
   In multi-member teams, split pipelines across people/agent sessions; solo, run them
   as sequential 20-25m timeboxes.
3. **Triangulate (Scout, 20m):** cluster signals into candidate problems; apply Gate 1
   (≥3 streams, ≥2 years, ≥1 numbered admission). Expect 60-70% of signals to die here —
   that's the gate working.
4. **Chair test (Maverick, 15m):** Gate 2 on survivors — named place, named number,
   named failed attempt, named pain owner. Rewrite framings that are real-but-vague
   down to their specific bleeding link (chain-of-loss decomposition from the gauntlet
   applies here too).
5. **Whitespace + wedge (Orion + Scout, 20m):** Gate 3 coverage scan; Orion names each
   survivor's smallest buildable WEDGE (the 36h software that attacks the bleeding link)
   and the RAIL it rides — no wedge, no candidacy.
6. **Score & rank (Oracle, 15m):** PQ scores → ranked table. Present the top 5 to the
   human with one line each; human picks 3 for full dossiers.
7. **Dossiers (Herald + Scout, 30m):** fill PROBLEM-DOSSIER.md per pick — evidence
   ledger, SIH-format PS (ministry register), gauntlet-ready brief. Commit:
   `docs: prospect — <scope>, top 3 dossiers`.

## Handoff

The chosen dossier's brief section feeds `/ideate` directly (its evidence file replaces
most of gauntlet step 2 — you already have ground truth; the gauntlet then runs ban-list
→ divergence → triage → judge-sim on the SOLUTION side). At SIH: dossier's PS section →
idea-PPT problem slides (ppt-builder maps it); internal round runs mini-finale mode on
the chosen wedge.

## Quality bars

- Every claim in every dossier carries a link — a judge asking "source?" gets one in 5 seconds.
- If two dossiers feel interchangeable with another college's likely picks, the chair
  test was run soft — re-run Gate 2 with the ban-shapes list in hand.
- The pipeline log gets committed even for candidates that died — dead signals are
  next event's shortcuts.
