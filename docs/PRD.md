# PRD — TRISHUL-SIM

> Scale gate: **FULL** · Hackathon: SIH 2026 · Problem: SIH26247 (MoD / Innovation Cell)
> Submission deadline: **20 Oct 2026 (T=0)** · Skeleton deadline (30%): **09 Oct 2026**
> Vocabulary freeze: the nouns below are final across code, schema, UI, and pitch.

---

## 1 · Problem

Infantry sentries at forward posts along Punjab/Jammu borders face rogue drone incursions with a reaction window of **15–45 seconds**, yet 80 %+ of incursions occur at night or in dense winter fog — conditions where visual acquisition is impossible until the drone is under 50 m ([BSF Punjab Border Reports / Tribune India, 2024](https://www.tribuneindia.com/news/punjab/bsf-counter-drone-drills)). The Indian Army's 400 + infantry battalions and BSF Border Outposts have no unit-level C-UAS simulator: existing rigs (Zen Technologies ZADS, Rafael Drone Dome) are multi-crore enterprise hardware installed only at central training schools, leaving frontline Jawans with classroom slides and rare, expensive skeet drills that simulate none of the real RF, radar, or acoustic signatures of a swarm attack ([CENJOWS Air Defence Studies, 2024](https://cenjows.in/); [MoD Problem Statement SIH26247](https://sih.gov.in)). TRISHUL-SIM closes this gap with a **software-only, sensor-physics twin** that runs on existing battalion laptops — no hardware procurement, no cloud, no internet.

---

## 2 · User & Core Loop

**The demo user:** *Havildar Rajan — infantry sentry, 5 years service, posted at a Punjab border BOP, no prior simulator training.*

**Core loop:**
- **Does:** Havildar Rajan sits at the Trainee Cockpit. The Instructor (JCO) triggers a night-fog scenario. Rajan monitors the synthetic RF Waterfall, Micro-Doppler Radar scope, and binaural Acoustic sonar — the same sensor suite his real post would have — to detect, classify, and neutralise an autonomous swarm.
- **Gets:** Within 10 seconds of scenario end, the AAR Engine renders a Tactical Performance Scorecard: kill-chain latency, ROE compliance, deconfliction accuracy, kinetic efficiency — all timestamped to the microsecond.
- **Returns because:** Each Scenario is procedurally seeded; spawn points and swarm split-tactics vary every run. Rajan cannot memorise — he must develop reflexes. The Scorecard benchmarks him against doctrinal thresholds, giving him a concrete number to beat next session.

---

## 3 · Demo Narrative (this section IS the feature list)

> Herald spine sentence: *"The first battalion-deployable C-UAS trainer that teaches Indian soldiers to defeat a drone swarm in the dark — for ₹10 a seat."*

| Beat | Judge sees | Powered by (MUST ↓) |
|---|---|---|
| **1** · T+0s · Setup | Instructor (JCO) opens the C2 Station on Laptop 1. Selects "Punjab Border — Zero Visibility Pincer" from the Scenario Library. Sets weather: midnight fog, crosswind. Two-laptop LAN link confirmed (green icon). | Scenario Library · WebSocket IPC · Dual-station architecture |
| **2** · T+15s · Sensor Boot | Trainee Cockpit (Laptop 2) wakes. Havildar Rajan's screen fills with three live sensor panes: RF Waterfall (2.4 / 5.8 GHz / 915 MHz ELRS), Micro-Doppler Radar scope, Acoustic FFT Sonar. All showing baseline noise — the border is quiet. | Multi-Spectral Sensor Suite · real-time physics engine |
| **3** · T+30s · Incursion detected | A 220 Hz acoustic spike appears. RF waterfall erupts at 5.825 GHz. Rajan slews the virtual directional antenna. The radar scope shows 6 radar cross-section signatures — 2 large (decoys), 4 small fast-movers. Threat state jumps: Green → Amber → Red. | Swarm AI (Reynolds Boids + HBT) · Doppler blade-tip harmonic engine · Acoustic BPF model |
| **4 ⭐ wow** · T+45s · Zero-Visibility Pincer Swarm Defeat | Rajan resists premature RF jamming (avoids friendly VHF blackout). He locks the Directional EW jammer on the attack axis (4 FPV munitions executing low-level pincer at 120 km/h) and engages the SMASH 2000 fire-control lead-angle reticle on the lead FPV. Swarm neutralised. Judges see: pitch-black screen, two sensor panes, one correct decision, zero friendly fire. | EW Jammer (azimuth / elevation / band-select) · SMASH 2000 lead-angle fire control · ROE deconfliction logic |
| **5** · T+55s · AAR Scorecard | 10 seconds after scenario end, the AAR Engine renders the Tactical Performance Scorecard on both screens: Time-to-Detection, Classification Accuracy, ROE compliance (100 %), kinetic rounds fired vs hits. Interactive timeline scrubber lets the judge replay any moment with spatial overlay. | AAR Engine · 10 Hz telemetry log · Timeline Scrubber · Scorecard renderer |
| **6** · T+70s · Deployability close | Instructor shows: no internet connection active. Scenario re-triggered instantly. Hindi / English toggle. Scorecard exported to PDF in one click. Judge question answered: "Same binary runs in a Ladakh underground bunker." | Air-gap validation · Bilingual UI · PDF export |

---

## 4 · Scope

**MUST** (walking skeleton + wow machinery — every item maps to a beat above):

- [ ] **Dual-station architecture** — Instructor C2 (Laptop 1) + Trainee Cockpit (Laptop 2) linked over localhost WebSocket IPC; single-laptop split-screen fallback (Beat 1)
- [ ] **Scenario Library** — ≥4 procedurally seeded Indian border threat scenarios (Punjab riverine fog, Ladakh LAC ridge, LoC mountain pass, desert FOB); no two runs identical (Beat 1)
- [ ] **Multi-Spectral Sensor Suite** — RF Waterfall (2.4 / 5.8 GHz / 915 MHz), Micro-Doppler Radar scope (Friis propagation + Doppler blade-tip harmonics), Acoustic FFT Sonar (BPF model, ISO 9613-1 attenuation), Electro-Optical / Thermal IR sights (Beat 2)
- [ ] **Swarm AI** — 2-tier: Reynolds Boids flocking + Hierarchical Behavior Trees for decoy-split and saturation-dive tactics; dynamic EW evasion; no scripted waypoints (Beat 3)
- [ ] **EW Jammer controls** — directional, azimuth / elevation / band-select; ROE deconfliction (prevents friendly VHF blackout) (Beat 4)
- [ ] **SMASH 2000 fire-control** — lead-angle reticle for 7.62 mm kinetic engagement; kinetic accuracy scoring (Beat 4)
- [ ] **AAR Engine** — 10 Hz telemetry log; doctrinal Decision Tree Scoring (Time-to-Detection, Classification Accuracy, ROE compliance, kinetic efficiency); Tactical Performance Scorecard rendered ≤10 s after scenario end (Beat 5)
- [ ] **Interactive Timeline Scrubber** — synchronized spatial replay with judge-navigable scrub bar (Beat 5)
- [ ] **Air-gapped Windows .exe** — zero external API calls; single installer; runs on Intel Iris Xe / RTX 3050 at 60 + FPS; Unity 2022/6 LTS URP (Beat 6)
- [ ] **Bilingual UI** — Hindi + English with MIL-STD-2525 tactical symbology and colour-coded threat state (Green / Amber / Red) (Beat 6)
- [ ] **PDF Scorecard export** — one-click export of the Tactical Performance Dossier (Beat 6)

**SHOULD** (if time; pre-ranked):

1. Smartphone WebXR / Cardboard VR sentry spotting mode (360° binaural) — future-dev scaffold only; Unity XR plugin installed, scene stubbed
2. Scenario Editor UI for the Instructor to author custom threat profiles without code
3. Campaign Mode — persistent trainee progress ledger across sessions (SQLite local)
4. Hindi voice-over for scenario briefings (pre-recorded .wav; no TTS API)
5. Additional scenario: high-altitude Ladakh (thin-air acoustic attenuation variant)

**CUT LIST** (born ranked; scope-guard executes top-down at T-3h):

1. WebXR / VR sentry mode active playthrough — scaffold only is fine, saves ~12h of XR rig work
2. Scenario Editor UI — hard-coded JSON configs suffice for the demo
3. Campaign Mode / persistent ledger — session-scoped scoring is enough to wow judges
4. Hindi voice-over audio — text subtitles cover the demo
5. Live LAN multi-machine sync beyond 2 stations — split-screen fallback already in MUST

**NON-GOALS** (we will not build, on purpose):

- Cloud connectivity, online leaderboards, or any external API call
- Unreal Engine build target
- High-end VR headset support (HTC Vive, Meta Quest) — cardboard scaffold only
- Mobile / Android / iOS build
- Real drone hardware integration or live RF sensor feed
- Multiplayer beyond the 2-station Instructor ↔ Trainee architecture
- Any generative AI / LLM component

---

## 5 · Success =

**Wow moment lands live:** Havildar Rajan defeats the Zero-Visibility Pincer Swarm in pitch-black fog, and the Tactical Performance Scorecard renders in ≤10 seconds — with judges able to scrub the replay timeline and ask "show me the moment he fired."

**Three retellable phrases:**
- *"The first battalion-deployable C-UAS trainer that teaches Indian soldiers to defeat a drone swarm in the dark — for ₹10 a seat."*
- *"The Zero-Visibility Pincer Swarm Defeat."*
- *"100% air-gapped, runs on any battalion laptop, automated AAR in under 10 seconds."*

**Submission complete by T-2h** (18 Oct 2026 ~22:00) · **Skeleton walking by 30% deadline** (09 Oct 2026) · **Feature freeze at T-3h** (19 Oct ~21:00) · **Risky-feature freeze at T-5h** (19 Oct ~19:00).
