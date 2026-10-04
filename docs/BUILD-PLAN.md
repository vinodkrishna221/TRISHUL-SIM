# BUILD-PLAN — TRISHUL-SIM
> Blueprint date: 2026-10-04 · Owner: Orion + Patch
> Clock: T=0 = 20 Oct 2026 23:59 IST (submission) · Skeleton deadline: 09 Oct 2026 EOD

---

## Phase spine

| Phase | Wall-clock window | Goal | Gate |
|---|---|---|---|
| **P0 · Skeleton** | 04–09 Oct (6 days) | Walking skeleton green (P0 checklist in ARCHITECTURE §2) | All 6 P0 items pass; WS handshake live; 10 Hz log file grows |
| **P1 · Wow Machinery** | 09–16 Oct (7 days) | Zero-Visibility Pincer Swarm Defeat end-to-end; Scorecard renders ≤10 s | Demo beat 4 and beat 5 live; both screens update |
| **P2 · Polish & Hardening** | 16–19 Oct (3 days) | All MUST items done; bilingual UI; PDF export; air-gap validated | Feature freeze 19 Oct ~21:00 |
| **P3 · Endgame** | 19–20 Oct (~18h) | Submission package; video recorded; dry run 3×; backup smoke | Submitted by 20 Oct ~22:00 |

---

## Phase × Track grid

### P0 · Skeleton (04–09 Oct)

| Track | Task card | Timebox | Demoable moment | Owner |
|---|---|---|---|---|
| `unity-sim` | Unity project created (URP); two scenes: InstructorC2 + TraineeCockpit; WS server binds on :9700; WS client connects and receives SCENARIO_START; connection status icon turns green | 24h | Green "LAN link confirmed" icon on C2 screen | unity-sim |
| `unity-sim` | ScenarioLibrary loads `punjab-fog-pincer-01.json`; Instructor can trigger scenario; SCENARIO_START broadcasts correctly | 8h | Instructor clicks "Launch" → Trainee screen transitions | unity-sim |
| `swarm-ai` | 6 Boid agents spawn and flock; each agent publishes SwarmAgentState every 100 ms; no HBT yet | 16h | 6 moving dots visible on Trainee Cockpit | swarm-ai |
| `swarm-ai` | Friis propagation model computes received RF power per agent → SensorReadings.rfWaterfallPeakDbm populated | 8h | RF Waterfall pane shows non-flat signal | swarm-ai |
| `aar-engine` | TelemetryRecord appended to NDJSON at 10 Hz during SCENARIO_TICK; file grows on disk | 8h | Session file visible in Explorer growing in real time | aar-engine |
| `aar-engine` | SCENARIO_END triggers Scorecard compute; stub Scorecard (zeros) written to disk and broadcast via SCORECARD_PAYLOAD | 8h | "Scorecard received" log line on both screens | aar-engine |
| `ui-ux` | UITK shell: C2 screen (scenario selector + launch button + status icon) + Cockpit screen (3 sensor pane placeholders + threat-state indicator) | 16h | Both screens render with correct layout at 1920×1080 | ui-ux |

**P0 timebox total:** ~88h distributed across 4 tracks over 6 days. Comfortable — ~15h/track/day at 1 builder each, or shared across team.

---

### P1 · Wow Machinery (09–16 Oct)

| Track | Task card | Timebox | Demoable moment | Owner |
|---|---|---|---|---|
| `swarm-ai` | Doppler blade-tip harmonic model → SensorReadings.dopplerPeakHz; acoustic BPF + ISO 9613-1 attenuation → acousticSplDb | 16h | Doppler scope shows 220 Hz spike on approach | swarm-ai |
| `swarm-ai` | HBT: PINCER_LOW_FAST tactic tree; 4 attackers execute low-level pincer split at T+30s; 2 decoys diverge | 20h | Radar scope shows 2 large + 4 small signatures splitting | swarm-ai |
| `swarm-ai` | EW jammer deconfliction logic: friendlyBlackoutRisk computed from antenna overlap geometry; ROE signal emitted | 8h | EW engaged without friendly blackout → roeCompliance = 1.0 | swarm-ai |
| `swarm-ai` | SMASH 2000 lead-angle reticle: KineticEvent computed on trigger; hit/miss logic based on lead angle vs velocity cone | 12h | Trainee fires; hit registered; agentId neutralised | swarm-ai |
| `aar-engine` | Full Decision Tree Scorer: compute all ScorecardMetrics from NDJSON (timeToDetectionMs, roeCompliance, kineticEfficiency, prematureEwPenalty); doctrinePassed = all benchmarks met | 16h | Scorecard shows non-zero metrics with pass/fail colours | aar-engine |
| `aar-engine` | Timeline Scrubber data: timelineIndex built at Scorecard load; NDJSON seek logic for scrub position | 12h | Scrub bar navigable; spatial overlay shows agent positions at T+X | aar-engine |
| `unity-sim` | Split-screen fallback: both C2 and Cockpit scenes render on same machine in side-by-side layout | 8h | Single laptop running full demo | unity-sim |
| `unity-sim` | All 4 scenario JSON configs authored: Punjab riverine fog, Ladakh LAC ridge, LoC mountain pass, Desert FOB | 8h | Scenario library shows 4 entries | unity-sim |
| `ui-ux` | Live RF Waterfall (scrolling spectrogram), Micro-Doppler scope (PPI radar display), Acoustic FFT sonar — all driven by SensorReadings | 24h | Beat 2 and Beat 3 fully visual | ui-ux |
| `ui-ux` | EW Jammer controls UI: azimuth/elevation sliders + band selector; SMASH 2000 lead-angle reticle overlay | 12h | Trainee can operate EW and kinetic controls | ui-ux |
| `ui-ux` | Scorecard Renderer UI: all ScorecardMetrics displayed; doctrine badge; pass/fail thresholds annotated | 16h | Beat 5 fully visual on both screens simultaneously | ui-ux |

**P1 timebox total:** ~152h distributed across 4 tracks over 7 days. ~22h/track/day — requires 2–3 builders per track or focused parallel work.

---

### P2 · Polish & Hardening (16–19 Oct)

| Track | Task card | Timebox | Demoable moment |
|---|---|---|---|
| `unity-sim` | Air-gap validation pass: Network Profiler + Wireshark capture; zero external connections confirmed | 4h | "No internet" in Task Manager visible to judge |
| `unity-sim` | InnoSetup single-file installer; IL2CPP build; target Intel Iris Xe at 60+ FPS validated | 8h | One .exe installs, runs, hits 60 FPS |
| `unity-sim` | XR plugin scaffold: Unity XR Plugin Management installed; stub VR scene present but not on hot path | 4h | "VR-ready" shown in system info panel |
| `aar-engine` | PDF Scorecard export: iTextSharp generates Tactical Performance Dossier; one-click button | 8h | Judge gets a PDF in one click |
| `ui-ux` | Bilingual UI: Hindi/English toggle wired to Unity Localization; all labels, scenario names, MIL-STD-2525 symbols | 12h | Toggle button; Hindi renders correctly |
| `ui-ux` | Threat-state colour coding throughout: MIL-STD-2525 palette applied to all indicators | 4h | GREEN / AMBER / RED consistent across panes |
| All tracks | Warden review pass: contract fidelity check; no shape drift; demo path smoke | 8h | All 6 demo beats pass in sequence |

**P2 timebox total:** ~48h across 4 tracks over 3 days.

---

### P3 · Endgame (19–20 Oct)

| Task | Timebox | Owner |
|---|---|---|
| Feature freeze enforcement (scope-guard executes cut list top-down) | — | Patch |
| Demo dry run × 3 (full 70-second beat sequence) | 3h | All |
| Backup video: recorded demo with voiceover (in case live demo fails) | 2h | Herald |
| Submission package: source zip + .exe installer + README + demo video | 2h | Patch |
| Final smoke on clean machine (fresh install, both screens) | 1h | Patch |
| Push to SIH portal by T-2h (20 Oct ~22:00 IST) | — | Patch |

---

## Track load estimate

| Track | P0 (h) | P1 (h) | P2 (h) | Total | Critical path? |
|---|---|---|---|---|---|
| `unity-sim` | 32 | 16 | 16 | **64h** | Yes (WS IPC blocks all others) |
| `swarm-ai` | 24 | 56 | 0 | **80h** | Yes (sensor models block ui-ux P1) |
| `aar-engine` | 16 | 28 | 8 | **52h** | Yes (Scorecard blocks P1 ui-ux) |
| `ui-ux` | 16 | 52 | 16 | **84h** | Yes (all visual beats) |

**Critical path:** `unity-sim` P0 WS IPC → `swarm-ai` P1 sensor models → `aar-engine` P1 Scorecard compute → `ui-ux` P1 Scorecard Renderer → Warden P2 review.

---

## Deploy targets

| Target | URL / path | Owner | Notes |
|---|---|---|---|
| Dev build (daily) | Local `Build/` in Unity project | unity-sim | Fast iteration; Mono scripting backend |
| Demo build (P1 gate) | `dist/TRISHUL-SIM-demo-<date>.exe` | Patch | IL2CPP; tested on Intel Iris Xe |
| Submission package | SIH portal upload | Patch | Source zip + installer; air-gap smoke before upload |

---

## Freeze calendar (Patch enforces — DO NOT move dates)

| Milestone | Datetime (IST) |
|---|---|
| **Skeleton walking (P0 gate)** | **09 Oct 2026 EOD** |
| Risky-feature freeze | 19 Oct 2026 ~19:00 |
| Feature freeze | 19 Oct 2026 ~21:00 |
| Video recorded | 19 Oct 2026 ~21:00 |
| Submission live | 20 Oct 2026 ~22:00 |
| Final smoke | 20 Oct 2026 ~22:30 |
| **T=0** | **20 Oct 2026 23:59** |
