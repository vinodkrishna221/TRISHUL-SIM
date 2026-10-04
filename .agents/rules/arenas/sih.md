# Arena Pack — Smart India Hackathon (load when the event is SIH)

SIH is not a demo-day hackathon — it's a **36-hour evaluated build** with judges walking
the floor three times, mentors mid-stream, a PPT-first culture, and 4-5 rival teams on YOUR
problem statement in the same room. This pack overlays the core rules; where it conflicts
with a core rule, THIS wins during SIH.

## 1 · The Evaluation Rhythm (overrides the 30% skeleton law)

Judges arrive before the skeleton law would save you. SIH software edition clock:

| Round | When (36h clock) | What's actually judged | Get ready via |
|---|---|---|---|
| Mentoring #1 | ~h4-8 | approach, architecture story, team clarity | `/eval-round M1` |
| **E1 First Evaluation** | ~h6-10 | idea clarity, early progress, PPT, feasibility | `/eval-round 1` |
| **E2 Mid Evaluation** | ~h18-26 (often midnight) | working core, feedback incorporated, privacy/offline grilling | `/eval-round 2` |
| **E3 Final + viva** | ~h30-36 | complete system, polish, presentation, deep technical Q&A | `/eval-round 3` + VIVA-DOSSIER |

- **Skeleton deadline moves to hour 8** (not 30%): E1 scores teams with something on screen
  above teams with diagrams. Ugly + running beats elegant + planned, at every round.
- **Always-evaluable law:** from hour 6 onward, the answer to "show us" is never "give us
  ten minutes". Freeze 30 min before each round (Patch calls it), smoke, stage the demo.
- Standup cadence pins to the rhythm: one standup 90 min before each round, minimum.

## 2 · Six-member role model (the SIH roster reality)

Four builders is the practical max; the other two are force multipliers, not spare devs:

| Seat | Persona home | Mission |
|---|---|---|
| TL + builder (fe/ai) | Orion+Nova/Sage | architecture, missions, integration calls |
| Builder ×3 | Forge/Volt/Atlas/Sage | tracks per phase plans |
| **Storyteller seat** | Herald + Oracle | PPT (all versions), demo script, eval-pack per round, speaker prep |
| **Intel seat** | Scout + feedback-triage | domain evidence (schemes/acts/numbers), judge dossier, feedback log, venue logistics |

- Non-dev missions are real missions: brief them with `mission-brief` like any track
  (cards: "E1 deck v2 [1h]", "DPDP answer researched + sourced [45m]").
- ≥1 female member is a REGISTRATION rule, not a seat — map people to seats by strength.
- Mentors (≤2): they advise, they don't commit code. Every mentor suggestion enters
  through `/eval-round`'s feedback triage — never directly into a mission.
- **Shift plan for 36h** (fill in TEAM.md): two overlapping shifts (e.g., A: 08-02, B: 20-10);
  handoff = STATUS read + 5-min voice note; never zero builders awake; TL sleeps BEFORE E2.

## 3 · Government-context judging (the questions that kill, pre-armed)

Judges at SIH grill on India-deployment reality. Intel seat owns sourced answers by E2:

- **Data privacy / DPDP Act 2023:** what personal data, where stored, consent flow,
  data residency. If Aadhaar-adjacent: you use DigiLocker/offline verification patterns,
  never raw Aadhaar numbers in your DB.
- **India Stack hooks:** can it ride DigiLocker, UPI, ONDC, or **Bhashini** (multilingual is
  the #1 crowd-pleaser — a Bhashini-powered answer beats a GPT-only answer with govt judges)?
- **Offline / low-connectivity:** what works with no signal? (PWA cache, SMS/IVR path,
  sync-on-reconnect). "The village has 2G on a good day" is coming; have the beat ready.
- **Feature phones:** the IVR/SMS/WhatsApp answer — even as roadmap with one working stub.
- **Post-hackathon sustainability:** who runs it, what it costs a district/Panchayat per
  month (a real number), which existing govt portal/scheme it plugs into vs duplicates.
- **"How is this different from <UMANG/existing portal>?"** — Scout's prior-art mode
  answers this BEFORE ideation locks; the differentiation line goes in every deck.

## 4 · Venue resilience (nodal centers are hostile terrain)

Pack and prep like the network is a rumor (checklist executes during /prep, verified T-0):

- Full stack runs on ONE laptop offline: local DB seed, DEMO_FALLBACK corpus for every
  scripted AI beat, `npm`/`pip`/model caches pre-downloaded, `next build` output cached.
- Cloud deploy is the bonus mirror, not the plan — reverse of the generic kit posture.
- Phone hotspot allowed as fallback at most centers (verify rules on arrival), power strip +
  spare HDMI, demo also rehearsed at 1366×768 on projector color profiles.
- Provider rate limits WILL hit when 40 teams share the venue pipe at E2 — fallback model +
  cached responses are load-bearing, not insurance (core ai-llm pack, now mandatory).

## 5 · Rival awareness (4-5 teams, same PS, same room)

The gauntlet's consensus map runs TWICE at SIH: at the internal round (vs the field) and
at the finale (vs your 4 rivals — judge-lens Mode 4). By E3, Herald's deck carries one
"unlike the other approaches to this PS" line — informed, never naming rivals, delivered
as confidence, not comparison.

## 6 · Internal round — pick the mode (ask the organizers ONE question: "is a working
demo expected, or pitch-only?")

**Mini-finale mode (default — most internals expect a prototype):** run the full chain
at QUICK scale: gauntlet → /kickoff (QUICK) → blueprint-lite (stack record + skeleton
definition + inline contracts, one file) → ONE combined phase plan (P0 skeleton + the
single wow beat, ≤10 cards) → build → demo from the laptop. Deliverables: working
walking skeleton + one wow moment, idea PPT (ppt-builder Engine B, prescribed format),
and a bolt-slides pitch deck (Engine A) if presentation time allows web slides. Run
/eval-round once as a live drill — the internal IS the dress rehearsal for E1.

**Paper mode (pitch-only internals):** gauntlet + kickoff-lite + idea PPT (Engine B),
skip the build chain. Optional Engine A deck for stage presence.

Either way: the IDEA-BRIEF's unfair-advantage line IS the novelty criterion answer, and
selection is the hardest filter of the whole SIH pipeline — treat the internal like the
final. Whatever gets built in mini-finale mode becomes /prep lane 1's head start
(spikes already proven), but re-read the finale rulebook on what may be pre-built.

## 7 · Self-proposed / Open Innovation mode (cycles where SIH gives NO problem statements)

When teams must find their own problem, the problem IS the competition — most colleges
will submit chair-written ChatGPT framings ("platform for farmer awareness"), and an
evidence-mined PS stands out on the novelty, feasibility, and impact criteria by
construction. The pipeline gains a stage at the front:

`/prospect (week before internal) → human picks a dossier → /ideate on the wedge →
internal round in mini-finale mode → idea PPT (dossier Section A feeds the problem
slides; ppt-builder maps it) → /prep → finale`

- Run /prospect scoped by your strongest sector or home state — judges reward problems
  the team can show LOCAL evidence and access for ("we called the mandi" beats a stat).
- The dossier's evidence ledger goes INTO the idea PPT as a "why this problem is real"
  slide — sourced numbers on the problem slide is the single cheapest credibility buy
  in a self-proposed cycle.
- The pre-seeded ban list in dossier Section B matters double here: other teams that
  found the same problem will build the obvious solution; you arrive already diverged.
