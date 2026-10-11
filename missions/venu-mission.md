# Mission Brief — Venu · `swarm-ai` (P0 + P1 first half)
> Generated: 2026-10-04 · Assigned by: Orion
> Track: `swarm-ai` · Budget: 19h planned / 28h usable (68%)
> Skeleton deadline: **09 Oct 2026 EOD** · Feature freeze: 19 Oct ~21:00 IST

---

## Project one-liner

*"The first battalion-deployable C-UAS trainer that teaches Indian soldiers to defeat a drone swarm in the dark — for ₹10 a seat."*
Full demo narrative → `docs/PRD.md §3`. Your work powers **Beat 2** (sensors alive) and **Beat 3** (swarm detected) and sets up **Beat 4** (the wow moment — completed by Uday).

## Mission goal

Build the Boids flock system (6 agents), the RF physics model (Friis), the Doppler + Acoustic models, and the PINCER_LOW_FAST Hierarchical Behavior Tree (both phases) — the **sensor physics backbone and wow-moment swarm tactic**.

**Victory definition:** Punjab Pincer scenario plays end-to-end with 6 Boids flocking → RF Waterfall showing live signal → Doppler scope showing 220 Hz spike → 4 attackers executing pincer split at T+30s → EW evasion active.

---

## Contracts

| Contract | Your role | File |
|---|---|---|
| `scenario-events.md` | **CONSUMER** — read SCENARIO_START to seed Boids; populate swarmSnapshot in TICK | `board/contracts/scenario-events.md` |
| `telemetry.md` | **CONSUMER** — SensorReadings shape you produce each tick | `board/contracts/telemetry.md` |
| `websocket-ipc.md` | consumer (read for TICK envelope shape) | `board/contracts/websocket-ipc.md` |

**You own:** `Assets/Scripts/SwarmAI/` · `Assets/Scripts/Sensors/` (Friis, Doppler, Acoustic — partial)
**You do NOT touch:** `Assets/Scripts/Network/` · `Assets/Scripts/AAR/` · `Assets/UI/`

## Stack rules to load

- `docs/ARCHITECTURE.md §1` — Burst + Job System for Boids; custom HBT (no NodeCanvas)
- `docs/SCHEMA.md Entity 2` — SwarmAgentState all fields (REQUIRED in every TICK payload)
- Unity 2022 docs: MonoBehaviour, UnityEngine.Random, Vector3 math

## Branch names

| Card | Branch |
|---|---|
| SA-P0-A | `feat/swarm-ai-boids-spawn` |
| SA-P0-B | `feat/swarm-ai-friis-rf` |
| SA-P1-A | `feat/swarm-ai-doppler-acoustic` |
| SA-P1-B-i | `feat/swarm-ai-hbt-approach` |
| SA-P1-B-ii | `feat/swarm-ai-hbt-pincer` |

## WAITS-ON

| Your card | Waits on | Mock path |
|---|---|---|
| SA-P0-A | Vinod: US-P0-A (WS IPC) + US-P0-C (TICK clock) | Cannot be fully mocked — need WsClient.OnMessageReceived. **Coordinate with Vinod: he pushes `feat/unity-sim-ws-ipc` first.** |
| SA-P1-B-i | SA-P0-A (Boids base) | None needed — sequential on your own cards |
| SA-P1-B-ii | SA-P1-B-i (APPROACH state) | None needed — sequential |

**You unblock:** Uday (SA-P1-C, SA-P1-D depend on SA-P1-B-ii), Sohail (AA-P0-A needs swarmSnapshot shape frozen), Rani (UX-P1-A needs SensorReadings live).

---

## Your cards

### SA-P0-A · Boids system — 6 agents spawn, flock, emit SwarmAgentState at 10 Hz ⭐ [timebox: 4h]
```
CONTEXT:
  Vinod's US-P0-A + US-P0-C must be merged first (WS IPC + TICK loop).
  Files to read:
    • board/contracts/scenario-events.md — SCENARIO_START.swarmProfile (seed Boids from here);
      SCENARIO_TICK.swarmSnapshot (your output, SwarmAgentState[] shape)
    • docs/SCHEMA.md Entity 2 — SwarmAgentState ALL fields (all required)
    • docs/ARCHITECTURE.md §1 — Burst + Job System

  Target directory: Assets/Scripts/SwarmAI/

  Steps:
    1. BoidAgent.cs (MonoBehaviour per agent):
       Fields: agentId, isDecoy, positionWorld, velocityMs, headingDeg, altitudeM,
               rfEmitting, rfFreqGHz, rfPowerDbm, propellerRpm, rcs, isNeutralised, neutralisedAtMs
       From SwarmProfile: decoys → rcs=0.08, propellerRpm=9800; attackers → rcs=0.012, rpm=11400
    2. BoidFlockController.cs — 3 rules (Separation <5m, Alignment 20m, Cohesion centroid)
       Clamp speed to SwarmProfile.speedKmh/3.6 m/s; altitude spring ± 2m
       Run in Update() — no Burst yet (add in SA-P2-B if FPS drops)
    3. SwarmManager.cs (singleton MonoBehaviour):
       OnScenarioStart(ScenarioConfig, int seed): seed Random.InitState(seed); spawn agentCount agents
       OnScenarioEnd(): Destroy all agents
       GetSwarmSnapshot(): return SwarmAgentState[] from current positions
       InjectSwarmSnapshot(): called by ScenarioManager before each TICK broadcast
    4. Wire: on WsClient SCENARIO_START → SwarmManager.OnScenarioStart()
       on TickLoop → SwarmManager.GetSwarmSnapshot() → inject into TICK payload
    5. Sphere prefab (red=attacker, blue=decoy) for visual — ui-ux replaces in P1

DELIVERABLE:
  Assets/Scripts/SwarmAI/BoidAgent.cs
  Assets/Scripts/SwarmAI/BoidFlockController.cs
  Assets/Scripts/SwarmAI/SwarmManager.cs
  Assets/Prefabs/SwarmAgentVisual.prefab

TEST:
  Play Punjab scenario. Trainee Cockpit: 6 coloured spheres move in coherent formation.
  SCENARIO_TICK Console log: swarmSnapshot contains 6 entries; positionWorld changes each tick.
STATUS: [x] done
```

---

### SA-P0-B · Friis propagation model → SensorReadings.rfWaterfallPeakDbm [timebox: 3h]
```
CONTEXT:
  SA-P0-A merged — SwarmAgentState[] available each tick.
  Files: docs/SCHEMA.md Entity 3 (SensorReadings — rfWaterfallPeakGHz, rfWaterfallPeakDbm,
  radarDetectedCount; leave dopplerPeakHz=0.0, acousticSplDb=0.0 for now).
  board/contracts/scenario-events.md — SensorReadings in TICK payload.
  docs/ARCHITECTURE.md §1 — "Friis/Doppler math is 30–50 lines, no library needed (ponytail)".

  Friis formula:
    P_received_dBm = P_transmitted_dBm − FSPL_dB
    FSPL_dB = 20·log10(d_km) + 20·log10(f_GHz) + 92.45
    Peak: iterate emitting agents → compute Friis → return max power agent's dBm + GHz
    radarDetectedCount: agents where two-way radar return > -90 dBm

  Antenna position: Vector3.zero (trainee at origin)

  Steps:
    1. Assets/Scripts/Sensors/RfPhysics.cs (static class):
       float ComputeFriisDbm(float txPowerDbm, float freqGHz, float distanceMetres)
       float ComputeRadarReturnDbm(float txPowerDbm, float freqGHz, float distanceMetres, float rcsM2)
    2. Assets/Scripts/Sensors/SensorSuiteComputer.cs (MonoBehaviour):
       ComputeSensorReadings(SwarmAgentState[], Vector3 antennaPos): returns SensorReadings
       dopplerPeakHz = 0.0f; acousticSplDb = 0.0f (P1 placeholders)
    3. Register with SwarmManager; inject result into TICK

DELIVERABLE:
  Assets/Scripts/Sensors/RfPhysics.cs
  Assets/Scripts/Sensors/SensorSuiteComputer.cs

TEST:
  SCENARIO_TICK Console: sensorReadings.rfWaterfallPeakDbm is a negative float that gets
  less negative as agents approach. At 50m: expected ≈ -36 dBm. radarDetectedCount increases.
STATUS: [x] done
```

---

### SA-P1-A · Doppler blade-tip harmonic + Acoustic BPF + ISO 9613-1 [timebox: 4h]
```
CONTEXT:
  SA-P0-B merged. SensorReadings partially populated.
  Files: docs/SCHEMA.md Entity 3 (dopplerPeakHz, acousticSplDb), board/contracts/telemetry.md,
  docs/PRD.md §3 Beat 3 ("220 Hz acoustic spike").

  Doppler blade-tip harmonic:
    f_blade_tip_Hz = (propellerRpm/60) * bladeCount * (1 + v_radial/c)
    bladeCount=4; c=343 m/s; v_radial = dot(agent.velocityMs, unit vec agent→antenna)
    Report peak across all non-neutralised agents.

  Acoustic BPF (ISO 9613-1):
    SPL_dB = SWL_dB − 20·log10(d) − alpha·d/1000
    SWL_dB=85 dB; alpha ≈ 0.08 dB/km for blade-tip ~220 Hz
    alpha correction: multiply by (293/(273+temp_C))^0.5
    Log-sum-exp for energy addition across agents.

  Steps:
    1. Assets/Scripts/Sensors/DopplerPhysics.cs:
       float ComputeBladeTipHz(SwarmAgentState agent, Vector3 antennaPos)
    2. Assets/Scripts/Sensors/AcousticPhysics.cs:
       float ComputeSplDb(SwarmAgentState, Vector3 antennaPos, float tempCelsius)
       float LogSumSpl(float[] splValues)
    3. Update SensorSuiteComputer.ComputeSensorReadings() to populate both fields
    4. Pass WeatherPreset.temperature for ISO 9613-1 correction

DELIVERABLE:
  Assets/Scripts/Sensors/DopplerPhysics.cs
  Assets/Scripts/Sensors/AcousticPhysics.cs
  SensorSuiteComputer.cs updated

TEST:
  Punjab (temp 4°C, 6 agents at 50m, rpm 11400):
  dopplerPeakHz in range 200–800 Hz, varying with approach velocity.
  acousticSplDb > 40 dB at 50m.
STATUS: [x] done
```

---

### SA-P1-B-i · HBT PINCER_LOW_FAST — approach + split trigger [timebox: 4h]
```
CONTEXT:
  SA-P0-A merged. Boids flocking established.
  Files: docs/PRD.md §3 Beat 3 + Beat 4 ⭐, docs/SCHEMA.md Entity 1 (SwarmProfile.tacticTree).
  Custom state-machine (no NodeCanvas — ponytail).

  HBT states: APPROACH → SPLIT_TRIGGER → PINCER → DIVE_ATTACK → NEUTRALISED
  This card: APPROACH + SPLIT_TRIGGER.

  APPROACH:
    - All 6 agents move toward target (Vector3.zero) as flock
    - Decoys (2): higher alt (altitudeM+5m), sinusoidal lateral oscillation (15m amplitude, 8s period, seed-driven)
    - Attackers (4): stay at altitudeM, maintain formation
    SPLIT_TRIGGER: closest attacker distance to origin < 150m → state = SPLIT

  Steps:
    1. Create Assets/Scripts/SwarmAI/HBT/ folder
    2. TacticTree.cs (abstract base) + TacticState enum {APPROACH, SPLIT, PINCER, DIVE, NEUTRALISED}
    3. PincerLowFastTactic.cs : TacticTree — implements APPROACH:
       steer toward target; cohesion weight low to allow independent motion
       evaluate SPLIT_TRIGGER each tick; log "PINCER SPLIT TRIGGERED" on trigger
    4. SwarmManager registers PincerLowFastTactic when tacticTree == "PINCER_LOW_FAST"

DELIVERABLE:
  Assets/Scripts/SwarmAI/HBT/TacticTree.cs
  Assets/Scripts/SwarmAI/HBT/PincerLowFastTactic.cs (APPROACH + SPLIT_TRIGGER states)
  SwarmManager.cs updated

TEST:
  Punjab: 6 agents spawn ~300m out, flock toward origin. Decoys oscillate laterally at higher alt.
  Console: "PINCER SPLIT TRIGGERED" when lead attacker hits 150m mark.
STATUS: [x] done
```

---

### SA-P1-B-ii · HBT PINCER — execution, EW evasion, exclusion breach ⭐ [timebox: 4h]
```
CONTEXT:
  SA-P1-B-i merged (APPROACH + SPLIT_TRIGGER done).
  Files: docs/PRD.md §3 Beat 4 ⭐ ("4 FPV munitions executing low-level pincer at 120 km/h"),
  board/contracts/scenario-events.md (SCENARIO_END reason "EXCLUSION_BREACH").

  SPLIT state:
    - Decoys: peel away at ±45° (seed-driven), climb to 20m alt
    - Attackers: pair A from azimuth 0°, pair B from 180° (true pincer)
    - rfEmitting = false for 5s after split (EW evasion — go silent)

  PINCER/DIVE state:
    - Pairs close independently (no Boids cohesion); at 50m: descend to 2m alt
    - At 20m: EXCLUSION_BREACH → ScenarioManager.BroadcastScenarioEnd("EXCLUSION_BREACH")
    - isNeutralised: stop + propellerRpm=0

  Dynamic EW evasion:
    - If ewJammerState.engaged AND bearingDeg within 15° of agent bearing:
      steer ±30° off heading (seed-alternated); rfEmitting=false for 3s

  Steps:
    1. Add SPLIT, PINCER, DIVE, NEUTRALISED states to PincerLowFastTactic.cs
    2. BoidAgent.CheckEvasion(EWJammerState ew): frequency-hop + heading steer
    3. SwarmManager: check ALL_NEUTRALISED → BroadcastScenarioEnd("ALL_NEUTRALISED")

DELIVERABLE: PincerLowFastTactic.cs complete · BoidAgent.cs updated · SwarmManager updated

TEST:
  No EW: all 4 attackers reach exclusion zone → EXCLUSION_BREACH fires.
  With EW on correct axis: attackers evade; scenario lasts longer.
  Neutralise all 4 → ALL_NEUTRALISED fires → SCENARIO_END.

PUSH THIS BRANCH IMMEDIATELY WHEN DONE — Uday (SA-P1-C, SA-P1-D) is waiting.
DEMO: ⭐ "4 FPV munitions executing low-level pincer in pitch-black fog." (Beat 4 wow moment)
STATUS: [ ] todo
```

---

## Sync expectations

- **Push `feat/swarm-ai-boids-spawn` (SA-P0-A) within 2h of Vinod pushing US-P0-A** — Sohail and Rani need the swarmSnapshot shape frozen.
- **Push SA-P1-B-ii immediately when done** — Uday (SA-P1-C, SA-P1-D) is blocked until then.
- **Blocked:** Post in team chat tagging Vinod for any WS IPC issues.
- **PRs:** Tag Uday as reviewer (he extends your work in SA-P1-C+D).

---

## Accept

> I, Venu, have read this brief and will push `feat/swarm-ai-boids-spawn` within 2h of Vinod's WS IPC branch landing.

- [x] **ACCEPTED**
