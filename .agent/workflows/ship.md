---
description: The final descent — deploys frozen green, demo scripted and rehearsed, submission live, Q&A drilled. Run in the last 3-4 hours.
---

# /ship — Final Descent

Personas: Patch (infra + insurance) · Herald (script + submission) · Oracle (mock panel) ·
Warden (last-line QA). The freeze calendar (STATUS, set at kickoff) is the law this
workflow executes. Skills: `deploy-runbook`, `demo-script`, `judge-lens` mode 3.

## Run (against the clock, in order)

1. **T-5h — risky-feature freeze:** anything not yet demoable gets flagged off or cut.
   **T-3h — feature freeze.** Cut list executes without debate (it was pre-ranked for
   exactly this moment). Flags set to the demo config; the flag set written into
   DEMO-SCRIPT.md as the known-good state. Demo video: scripted T-4h, recorded T-3h.
2. **T-3h — submission sprint (Herald):** page live with spine, 3 screenshots/GIF, stack
   diagram, honest REAL-vs-simulated bullets, sponsor tech named. Submitted by **T-2h** —
   portals crash at T-15m, this is a law of nature, we do not test it.
3. **T-2.5h — demo script final (Herald+Oracle):** beats timed, wow lands twice, fallback
   lines written. **Rehearse 2× out loud, timed**, trimmed to 20s under the limit. Driver
   and speaker assigned (solo: pauses scripted — click, then talk).
4. **T-2h — mock panel (Oracle):** the 8 questions, answers drilled to ≤20s with one
   artifact each. The "what's real vs mocked" answer is written down verbatim — it's the
   one that kills teams live.
5. **T-2h — merge freeze.** Fixes only, routed through the endgame review tiering
   (`40-endgame.md`: contract/data/deploy-touching = fresh Warden, always; demo-path
   polish = self-review + smoke). **T-90m: deploy freeze** — free-tier build queues spike
   15+ minutes when every team ships at once; the last deploy TRIGGERS by T-90m, and the
   pre-warmed duplicate project (deployed at T-4h) stands by as fallback.
6. **T-45m — insurance final (Patch):** fresh backup video of the current build · pre-warm
   ritual executed · local mirror verified on the pitch laptop · all demo tabs staged ·
   seed refreshed · second screen tested on the actual venue cable if possible.
7. **T-15m — quiet cockpit:** no code, no deploys, no "one tiny fix" (the graveyard is full
   of them). Team breathes. Herald runs the spine once more. Go land it.

## Rules

- Any 🔴 during descent → fix forward ONLY if <20m and Warden-reviewed; else flag OFF /
  revert. The demo config always wins over completeness.
- Solo: same descent, compressed; the backup video matters MORE (no teammate can talk while
  you debug).
