---
description: The weeks between shortlist and finale (SIH-style events) — de-risk, pre-build fallbacks, rehearse evaluations, pack the venue kit. Paced, not a crunch.
argument-hint: "[weeks remaining]"
---

# /prep — Shortlist → Finale Preparation

For staged hackathons (SIH: internal round → weeks of prep → 36h finale). The finale is
won here: teams that arrive with risks retired and rituals rehearsed beat teams that
arrive with enthusiasm. Personas: Orion paces; everyone owns a lane. NOT a build-the-
product phase — rules usually require the build to happen at the venue; this phase builds
*readiness*, retires *risk*, and pre-makes *legal* assets (know your event's rules; log
what you pre-made in DECISIONS for honest disclosure).

## The five lanes (parallel, ~2-4h each per week)

1. **Risk retirement (builders):** prototype the risky 20% as THROWAWAY spikes — the
   Bhashini/translation call, the OCR accuracy, the realtime sync, the model latency on
   real data. Each spike ends in a DECISIONS entry: works / works-with-caveat / swap plan.
   Spikes are deleted or quarantined in `spikes/` — the finale build starts clean.
2. **Fallback corpus (Sage):** build DEMO_FALLBACK for every scripted AI beat with
   realistic regional data; pre-download models/deps/caches into the venue kit; verify
   the whole stack boots offline on the actual demo laptop. Pre-scaffold the deck too:
   `./scripts/new-deck.sh` + `npm install` at home — never against venue Wi-Fi.
3. **Evaluation rehearsal (Oracle + Herald):** 2 full mock eval rounds with faculty/
   mentors as judges (use the SIH arena §3 question bank); idea PPT → finale deck v2;
   the spine + differentiation line tightened; every member drilled on their one answer.
4. **Intel dossier (Scout/Intel seat):** the DPDP answer, the cost-per-district number,
   the existing-portal differentiation, scheme/act citations, judge/org research (who
   evaluates for this PS org; what they've said publicly they want).
5. **Logistics (Patch):** venue kit packed (laptop mirror, hotspot, power strip, HDMI,
   printed consent letters/IDs per rulebook), travel, TEAM.md shift plan agreed, tools
   synced on EVERY member's machine (`scripts/sync.sh` run + personas visible).

## Weekly rhythm

One /standup per week (same block format, weeks not hours) + one mock round per fortnight.
Final week: taper — no new spikes, rehearse + pack + sleep-bank. Walk in bored and ready.
