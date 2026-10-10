# Team — TRISHUL-SIM (SIH 2026 · Problem SIH26247)

> Roster drives /missions. Load ledger caps missions at 70% of declared hours.
> Skeleton deadline: 09 Oct 2026 EOD · Submission: 20 Oct 2026 23:59 IST

| Member | Track | Declared hours | Cards assigned | Budget used | Timezone |
|---|---|---|---|---|---|
| **Vinod** | `unity-sim` | ~57h raw / **40h usable** | US-P0-A → US-P3-A (10 cards, 28h) | 70% | IST |
| **Venu** | `swarm-ai` (P0+P1 first half) | ~40h raw / **28h usable** | SA-P0-A, SA-P0-B, SA-P1-A, SA-P1-B-i, SA-P1-B-ii (5 cards, 19h) | 68% | IST |
| **Uday** | `swarm-ai` (P1 second half+P2+P3) | ~40h raw / **28h usable** | SA-P1-C, SA-P1-D, SA-P2-A, SA-P2-B, SA-P3-A, SA-P3-B (6 cards, 17h) | 61% | IST |
| **Sohail** | `aar-engine` | ~40h raw / **28h usable** | AA-P0-A → AA-P3-A (7 cards, 20h) | 71% | IST |
| **Rani** | `ui-ux` (P0+P1 first half) | ~40h raw / **28h usable** | UX-P0-A, UX-P0-B, UX-P1-A, UX-P1-B, UX-P2-D (5 cards, 18h) | 64% | IST |
| **Rajeswari** | `ui-ux` (P1 second half+P2+P3) | ~40h raw / **28h usable** | UX-P1-C, UX-P1-D, UX-P2-A, UX-P2-B, UX-P2-C, UX-P3-A (6 cards, 22h) | 79% | IST |

**Lead (assigns missions, chairs pivots):** Vinod
**Team chat:** *(fill in WhatsApp/Discord link)*
**Driver / speaker for the pitch:** *(fill in)*
**Standup:** Ad-hoc — push every ≤60 min; blocked >20 min → post in team chat + switch tasks

## Card → Member map (quick reference)

| Card | Owner | Status |
|---|---|---|
| US-P0-A · WS IPC bootstrap | Vinod | [ ] |
| US-P0-B · Scenario Library + SCENARIO_START | Vinod | [ ] |
| US-P0-C · SCENARIO_TICK + SCENARIO_END loop | Vinod | [ ] |
| US-P1-A · Split-screen fallback | Vinod | [ ] |
| US-P1-B · 4 scenario JSON configs | Vinod | [ ] |
| US-P2-A · Air-gap validation | Vinod | [ ] |
| US-P2-B · IL2CPP + InnoSetup installer | Vinod | [ ] |
| US-P2-C · XR scaffold stub | Vinod | [x] |
| US-P2-D · Warden smoke | Vinod | [ ] |
| US-P3-A · Submission package coordination | Vinod | [ ] |
| SA-P0-A · Boids 6-agent spawn | Venu | [ ] |
| SA-P0-B · Friis RF model | Venu | [ ] |
| SA-P1-A · Doppler + Acoustic physics | Venu | [ ] |
| SA-P1-B-i · HBT PINCER approach | Venu | [ ] |
| SA-P1-B-ii · HBT PINCER execution + EW evasion | Venu | [ ] |
| SA-P1-C · EW deconfliction / friendlyBlackoutRisk | Uday | [x] |
| SA-P1-D · SMASH 2000 lead-angle + KineticEvent | Uday | [ ] |
| SA-P2-A · SATURATION_DIVE + MULTI-AXIS_SPLIT tactics | Uday | [ ] |
| SA-P2-B · Burst + 60 FPS validation | Uday | [ ] |
| SA-P3-A · Demo seed tuning | Uday | [ ] |
| SA-P3-B · Mock fixtures | Uday | [ ] |
| AA-P0-A · TelemetryLogger 10 Hz NDJSON | Sohail | [x] |
| AA-P0-B · Stub Scorecard + SCORECARD_PAYLOAD | Sohail | [x] |
| AA-P1-A · Decision Tree Scorer (full metrics) | Sohail | [x] |
| AA-P1-B · Timeline Scrubber data engine | Sohail | [x] |
| AA-P2-A · PDF export (iTextSharp) | Sohail | [x] |
| AA-P2-B · Warden contract fidelity | Sohail | [x] |
| AA-P3-A · Partial-file guard + manifest clean-up | Sohail | [x] |
| UX-P0-A · UITK shell (C2 + Cockpit layout) | Rani | [ ] |
| UX-P0-B · Unity Localization + Hindi/English toggle | Rani | [ ] |
| UX-P1-A · Live sensor suite (RF Waterfall, Doppler, Acoustic) | Rani | [x] |
| UX-P1-B · EW Jammer controls + SMASH reticle | Rani | [x] |
| UX-P2-D · Full demo-path smoke (all 6 beats) | Rani | [ ] |
| UX-P1-C · Split-screen layout compatibility | Rajeswari | [ ] |
| UX-P1-D · Scorecard Renderer + Timeline Scrubber UI | Rajeswari | [ ] |
| UX-P2-A · PDF Export button wiring | Rajeswari | [ ] |
| UX-P2-B · Full bilingual pass | Rajeswari | [ ] |
| UX-P2-C · Threat-state colour + night/fog ambience | Rajeswari | [ ] |
| UX-P3-A · Demo-script alignment + UI freeze | Rajeswari | [ ] |

## Blocker protocol

1. Blocked >20 min → post in team chat: `BLOCKED: [card] — [reason]`
2. Tag the owning track's builder if it's a cross-track dependency
3. Switch to any independent card while waiting (see WAITS-ON mock paths in mission files)
4. Never sit idle — every mission has a mock path for every dependency

## Working agreements

- Push every ≤60 min (even partial progress — draft commit is fine)
- Contracts in `board/contracts/` are read-only; change only via DECISIONS.md ritual
- No force-push to `main`; feature branches only: `feat/<track>-<card-slug>`
- Feature freeze: **19 Oct ~21:00 IST** — no new features after this (Vinod enforces)
