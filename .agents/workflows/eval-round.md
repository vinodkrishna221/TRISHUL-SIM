---
description: Pre-round battle ritual + post-round feedback triage for multi-evaluation hackathons (SIH E1/E2/E3, mentoring sessions). Run 60 minutes before every judge or mentor touchpoint.
argument-hint: "[M1|1|2|3] — which round"
---

# /eval-round — Judge Touchpoint Ritual

For hackathons where judging happens DURING the build (SIH: mentoring + E1 + E2 + E3).
Personas: Patch (freeze+stage), Oracle (mock), Herald (pack+speaker), Scout/Intel seat
(feedback log). Skills: `judge-lens`, `feedback-triage`, `ppt-builder`.
Output: `board/EVAL-PACK-<round>.md` (template: EVAL-PACK.md) + updated plans.

## T-60m — Pre-round ritual

1. **Freeze (Patch):** merges pause; smoke ritual; demo staged on the LOCAL mirror (venue
   rule: cloud is the mirror, laptop is the truth); flags to known-good; tabs staged.
2. **Pack (Herald):** fill `EVAL-PACK-<round>.md` — this round's goal, the 3-6 demo beats
   judges see TODAY (not the final demo — the round-appropriate slice), delta-since-last-
   round in numbers ("+2 screens, latency 3.1s→0.9s, feedback items closed: 3/4"), deck
   delta (ppt-builder patches only the slides that changed).
3. **Feedback callbacks (Intel seat):** from the previous round's triage log — the 2-3
   items ADOPTED, each with its visible artifact. Judges who see their feedback shipped
   score it; the callback line is scripted: "you asked about X — here it is."
4. **Mock (Oracle, 15m):** judge-lens with round-appropriate heat — M1/E1: clarity +
   feasibility; E2: privacy/offline/integration grilling (SIH arena §3 bank); E3: viva
   depth (VIVA-DOSSIER drill). Assign ONE speaker + one demo driver; everyone else has
   exactly one line they own ("the schema answer is Riya's").
5. **T-5m:** STATUS banner `EVAL <round> — staged @ <sha>`; team at the table; builders
   not presenting keep building on branches — never idle, never merging.

## T+0 (after the round) — Feedback triage (skill: feedback-triage)

6. Within 15 minutes, while verbatim: log EVERY judge/mentor line → classify
   ADOPT-NOW / ADOPT-IF-TIME / DEFEND / DECLINE with cut-list math → DECISIONS entry for
   direction-changers → convert ADOPT-NOWs to cards on the right missions → note the
   callback plan for next round.
7. Un-freeze (Patch), post a standup block, resume the train.

## Rules

- The round slice beats the grand plan: showing 3 working beats > narrating 10 planned ones.
- Never argue with a judge live: DEFEND items get their evidence NEXT round via callback.
- If a round lands mid-crisis: show the walking part + the honest one-liner + the plan —
  composure under fire is itself scored (it reads as engineering maturity).
