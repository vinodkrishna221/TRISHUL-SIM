# Phase Plan — `swarm-ai`
> Generated: 2026-10-04 · Owner: Orion (Architect persona)
> Track budget: ~56h usable (70% of ~80h allocated across 2 builders, 04–20 Oct 2026)
> Skeleton deadline: **09 Oct 2026 EOD** · Submission: 20 Oct 2026 23:59 IST

---

## Cross-Track Dependency DAG

```
unity-sim-P0-A  ──►  SA-P0-A   (WS IPC live; swarm-ai reads SCENARIO_START to seed Boids)
unity-sim-P0-C  ──►  SA-P0-A   (SCENARIO_TICK clock established; swarm-ai steps on TICK)
SA-P0-A         ──►  AA-P0-A   (SwarmAgentState shape frozen; AAR can log swarmSnapshot)
SA-P0-A         ──►  UX-P0-A   (sensor pane stubs need to know SwarmAgentState layout)
SA-P0-B         ──►  UX-P1-A   (SensorReadings.rfWaterfallPeakDbm/GHz live; RF Waterfall renders)
SA-P1-A         ──►  UX-P1-A   (Doppler + acoustic values live; Doppler scope + Acoustic FFT render)
SA-P1-B         ──►  UX-P1-B   (HBT pincer split live; EW + SMASH controls become meaningful)
SA-P1-C         ──►  AA-P1-A   (friendlyBlackoutRisk live; roeCompliance scorer has real input)
SA-P1-D         ──►  US-P1-B   (SwarmProfile drives scenario configs; layout agreed)

Mock path (P0/P1 isolation):
  • aar-engine reads swarmSnapshot from SCENARIO_TICK payload — in SA-P0-A this is real.
    If swarm-ai is delayed, aar-engine uses a static SwarmAgentState[] fixture.
  • ui-ux uses fake SensorReadings (all fields = constants) until SA-P0-B is complete.
  • Contract mocks live in: Assets/Scripts/Mocks/SwarmMock.cs, SensorMock.cs
```

**File surface audit — no overlaps:**
- `swarm-ai` owns: `Assets/Scripts/SwarmAI/` (Boids, HBT, tactics), `Assets/Scripts/Sensors/` (Friis, Doppler, BPF, EW deconfliction, SMASH)
- Does NOT touch: `Assets/Scripts/Network/`, `Assets/Scripts/AAR/`, `Assets/UI/`, `Assets/Scenes/`

---

## Load Ledger

| Phase | Cards | Timebox sum | Cumulative |
|---|---|---|---|
| P0 Skeleton | SA-P0-A, SA-P0-B | 4h + 3h = **7h** | 7h |
| P1 Core | SA-P1-A, SA-P1-B-i, SA-P1-B-ii, SA-P1-C, SA-P1-D | 4h + 4h + 4h + 3h + 3h = **18h** | 25h |
| P2 Integration | SA-P2-A, SA-P2-B | 4h + 2h = **6h** | 31h |
| P3 Polish | SA-P3-A, SA-P3-B | 3h + 2h = **5h** | **36h** |

**36h of 56h usable = 64% cap. ✓** (under ceiling — leaves 20h for integration friction across 2 builders)

---

## P0 · Skeleton (04–09 Oct 2026)

> Gate: 6 Boids visible on Trainee Cockpit · RF Waterfall shows non-flat signal

---

### SA-P0-A · Boids system — 6 agents spawn, flock, emit SwarmAgentState at 10 Hz ⭐ [timebox: 4h]

```
CONTEXT:
  unity-sim P0-A and P0-C must be complete — WS IPC live, SCENARIO_TICK broadcasting.
  This card is the swarm-ai track's P0 entry point.
  
  Files to read before starting:
    • board/contracts/scenario-events.md — SCENARIO_START payload (SwarmProfile used to seed
      Boids: agentCount, decoyCount, attackerCount, speedKmh, altitudeM, rfFreqGHz, rfPowerDbm);
      SCENARIO_TICK payload (swarmSnapshot: SwarmAgentState[]);
      SCENARIO_END consumer reaction (halt HBT, free memory)
    • docs/SCHEMA.md Entity 2 — SwarmAgentState field spec (all fields REQUIRED in payload)
    • docs/ARCHITECTURE.md §1 — Burst + Job System for Boids; HBT decision tree (custom or NodeCanvas)
    • board/contracts/websocket-ipc.md — Envelope format
  
  Repo state: Unity 2022 LTS URP project exists. WsClient.cs, ScenarioManager.cs exist.
  No swarm code exists yet. Target directory: Assets/Scripts/SwarmAI/
  
  Steps:
    1. Assets/Scripts/SwarmAI/BoidAgent.cs — MonoBehaviour per agent:
       Fields: agentId, isDecoy, positionWorld, velocityMs, headingDeg, altitudeM,
               rfEmitting, rfFreqGHz, rfPowerDbm, propellerRpm, rcs, isNeutralised, neutralisedAtMs
       Initialize from SwarmProfile: decoy agents get larger rcs (0.08), attackers get 0.012
       propellerRpm for attackers: 11400 (FPV at 120 km/h); decoys: 9800
    2. Assets/Scripts/SwarmAI/BoidFlockController.cs — classical Boids (3 rules):
       - Separation: repel when distance < 5m
       - Alignment: average velocity of neighbours within 20m
       - Cohesion: steer toward centroid of neighbours
       - Clamp speed to SwarmProfile.speedKmh / 3.6 (m/s)
       - Stay within altitude band: altitudeM ± 2m (spring force)
       NOTE: Run in Update() first (no Burst yet); add Burst as P3 optimisation if FPS drops.
    3. Assets/Scripts/SwarmAI/SwarmManager.cs — MonoBehaviour (singleton):
       - OnScenarioStart(ScenarioConfig config, int seed): seed UnityEngine.Random.InitState(seed);
         instantiate agentCount agents; mark first decoyCount as decoys
       - OnScenarioEnd(): foreach agent — Destroy(); swarmSnapshot = null
       - GetSwarmSnapshot(): returns SwarmAgentState[] from current agent positions
       - Registered with ScenarioManager.InjectSwarmSnapshot — called before each TICK broadcast
    4. Wire into ScenarioManager: on SCENARIO_START received, call SwarmManager.OnScenarioStart()
       On each TickLoop iteration, call SwarmManager.GetSwarmSnapshot() → inject into TICK payload
    5. Add a minimal sphere prefab (coloured red=attacker, blue=decoy) to visualize agents in
       TraineeCockpit scene — UI track replaces with tactical symbology in P1.

DELIVERABLE:
  Assets/Scripts/SwarmAI/BoidAgent.cs
  Assets/Scripts/SwarmAI/BoidFlockController.cs
  Assets/Scripts/SwarmAI/SwarmManager.cs
  Assets/Prefabs/SwarmAgentVisual.prefab  (sphere, red/blue tinted)

TEST:
  Play scenario in Unity Editor. Trainee Cockpit scene: 6 coloured spheres visible, moving in
  formation (coherent flock, not random scatter). Open SCENARIO_TICK log in Console:
  swarmSnapshot contains 6 entries, positionWorld values change each tick.

DEPENDS: unity-sim P0-A, P0-C     UNBLOCKS: AA-P0-A, UX-P0-A (shape frozen), SA-P0-B

DEMO: "6 moving dots visible on Trainee Cockpit." (Beat 2 first hint)
PERSONA: swarm-ai builder          STATUS: [x] done
```

---

### SA-P0-B · Friis propagation model → SensorReadings.rfWaterfallPeakDbm [timebox: 3h]

```
CONTEXT:
  SA-P0-A complete — SwarmAgentState[] available each tick.
  This card adds the physics model that makes the RF Waterfall pane non-flat.
  
  Files to read before starting:
    • docs/SCHEMA.md Entity 3 — SensorReadings fields (rfWaterfallPeakGHz, rfWaterfallPeakDbm,
      radarDetectedCount; dopplerPeakHz and acousticSplDb are P1 — leave as 0.0 for now)
    • board/contracts/scenario-events.md — SensorReadings embedded in SCENARIO_TICK payload
    • docs/ARCHITECTURE.md §1 — "Friis/Doppler math is 30–50 lines, no library needed (ponytail)"
  
  Friis free-space path loss formula:
    P_received_dBm = P_transmitted_dBm − FSPL_dB
    FSPL_dB = 20·log10(d) + 20·log10(f_GHz) + 92.45   (d in km, f in GHz)
    For each emitting agent: d = Vector3.Distance(agentPos, antennaPos) / 1000 (km)
    Sum received power across all emitting agents → peak (strongest) = rfWaterfallPeakDbm
    rfWaterfallPeakGHz = rfFreqGHz of the agent producing that peak
    radarDetectedCount = count of agents where their monostatic radar return exceeds -90 dBm:
      radar_return_dBm = rfPowerDbm − FSPL_dB (two-way: apply FSPL twice) + 10·log10(rcs)
  
  Antenna position: assumed at origin (0, 0, 0) — trainee antenna direction affects this in P1.
  
  Steps:
    1. Assets/Scripts/Sensors/RfPhysics.cs — static class:
       - float ComputeFriisDbm(float txPowerDbm, float freqGHz, float distanceMetres)
       - float ComputeRadarReturnDbm(float txPowerDbm, float freqGHz, float distanceMetres, float rcsM2)
    2. Assets/Scripts/Sensors/SensorSuiteComputer.cs — MonoBehaviour:
       - ComputeSensorReadings(SwarmAgentState[] snapshot, Vector3 antennaPos): returns SensorReadings
       - rfWaterfallPeakDbm: iterate emitting agents, compute Friis, return max
       - rfWaterfallPeakGHz: frequency of max-power agent
       - radarDetectedCount: count agents where radar return > -90 dBm
       - dopplerPeakHz: 0.0f (P1 placeholder)
       - acousticSplDb: 0.0f (P1 placeholder)
    3. Register SensorSuiteComputer with SwarmManager — called after GetSwarmSnapshot()
    4. Inject returned SensorReadings into SCENARIO_TICK payload (via ScenarioManager)

DELIVERABLE:
  Assets/Scripts/Sensors/RfPhysics.cs
  Assets/Scripts/Sensors/SensorSuiteComputer.cs

TEST:
  Play Punjab scenario. Open SCENARIO_TICK in Console: sensorReadings.rfWaterfallPeakDbm is
  a negative float that changes as agents approach (gets stronger, i.e., less negative).
  At 50m range, expected value ≈ 23 − (20·log10(0.05) + 20·log10(5.825) + 92.45) ≈ 23 − 59.6 ≈ -36 dBm.
  radarDetectedCount increases as agents approach.

DEPENDS: SA-P0-A               UNBLOCKS: UX-P1-A (RF Waterfall renderer needs live dBm values)

DEMO: "RF Waterfall pane shows non-flat signal during approach." (Beat 2/3)
PERSONA: swarm-ai builder     STATUS: [x] done
```

---

## P1 · Core (09–16 Oct 2026)

> Gate: Doppler scope shows 220 Hz spike · HBT pincer split visible · EW deconfliction live · SMASH 2000 fires

---

### SA-P1-A · Doppler blade-tip harmonic + Acoustic BPF + ISO 9613-1 attenuation [timebox: 4h]

```
CONTEXT:
  SA-P0-B complete — Friis model live, SensorReadings partially populated.
  This card completes sensor physics: Doppler and acoustic.
  
  Files to read before starting:
    • docs/SCHEMA.md Entity 3 — dopplerPeakHz (blade-tip harmonic), acousticSplDb (SPL in dB)
    • board/contracts/telemetry.md — SensorReadings shape and field semantics
    • docs/PRD.md §3 Beat 3 — "220 Hz acoustic spike appears"; "radar scope shows 6 RCS signatures"
    • docs/ARCHITECTURE.md §1 — "Friis/Doppler math is 30–50 lines, no library needed"
  
  Physics models (keep it simple — ponytail):
  
  Doppler blade-tip harmonic:
    f_blade_tip_Hz = (propellerRpm / 60) * bladeCount * (1 + v_radial / c)
    bladeCount = 4 (typical FPV quad propeller)
    v_radial = dot product of agent velocity and unit vector from agent to antenna (m/s)
    c = 343 m/s (speed of sound)
    Report peak across all non-neutralised agents → dopplerPeakHz
  
  Acoustic BPF (band-pass frequency = blade-tip harmonic, SPL model):
    SPL_dB = SWL_dB − 20·log10(d) − alpha·d/1000
    SWL_dB = 85 dB (reference sound power level for a FPV drone at full throttle)
    alpha = ISO 9613-1 atmospheric attenuation coefficient (dB/km):
      alpha ≈ 0.8·f_kHz^1.5 for mid-frequency (blade-tip ~0.22 kHz → alpha ≈ 0.08 dB/km)
      temperature correction: multiply alpha by (293 / (273 + temp_C))^0.5
    Sum SPL across all agents → acousticSplDb (use log-sum-exp for energy addition)
  
  Steps:
    1. Add DopplerPhysics.cs to Assets/Scripts/Sensors/:
       - float ComputeBladeTipHz(SwarmAgentState agent, Vector3 antennaPos)
    2. Add AcousticPhysics.cs:
       - float ComputeSplDb(SwarmAgentState agent, Vector3 antennaPos, float tempCelsius)
       - float LogSumSpl(float[] splValues)  // energy addition in dB domain
    3. Update SensorSuiteComputer.ComputeSensorReadings() to populate both fields
    4. Pass WeatherPreset.temperature to AcousticPhysics for ISO 9613-1 correction

DELIVERABLE:
  Assets/Scripts/Sensors/DopplerPhysics.cs
  Assets/Scripts/Sensors/AcousticPhysics.cs
  SensorSuiteComputer.cs updated

TEST:
  Punjab scenario (temperature 4°C, 6 agents at 50m, 11400 RPM):
  dopplerPeakHz ≈ (11400/60)·4·(1 + approach_v/343) ≈ ~760 Hz blade-pass + harmonic near 220 Hz
  (Check Console; value should be in 200–800 Hz range, varying with agent velocity)
  acousticSplDb should be > 40 dB at 50m range (audible threshold for detection).

DEPENDS: SA-P0-B               UNBLOCKS: UX-P1-A (Doppler scope, Acoustic FFT driven by these values)

DEMO: "Doppler scope shows 220 Hz spike on approach." (Beat 3)
PERSONA: swarm-ai builder     STATUS: [ ] todo
```

---

### SA-P1-B-i · HBT: PINCER_LOW_FAST tactic tree — phase 1: spawn + approach [timebox: 4h]

```
CONTEXT:
  SA-P0-A complete — Boids flocking established. This card adds HBT intelligence:
  the PINCER_LOW_FAST tactic tree (the demo scenario tactic).
  Split into 2 cards (SA-P1-B-i and SA-P1-B-ii) because the full HBT is >4h.
  
  Files to read before starting:
    • docs/PRD.md §3 Beat 3 — "radar scope shows 6 signatures — 2 large (decoys), 4 small fast-movers"
    • docs/PRD.md §3 Beat 4 ⭐ — "4 FPV munitions executing low-level pincer at 120 km/h"
    • docs/SCHEMA.md Entity 2 — SwarmProfile.tacticTree valid values
    • board/contracts/scenario-events.md — SCENARIO_START.seed used for procedural variation
  
  HBT design (custom state-machine, no NodeCanvas dependency — ponytail):
    States: APPROACH → SPLIT_TRIGGER → PINCER → DIVE_ATTACK → NEUTRALISED
    
    APPROACH state (this card):
      - All 6 agents move toward target origin as a flock (Boids active)
      - Decoys (2): higher altitude (altitudeM + 5m), diverge slightly left/right of main axis
      - Attackers (4): stay low (altitudeM), maintain formation
      - SPLIT_TRIGGER condition: closest attacker distance to origin < 150m → fire SA-P1-B-ii
  
  Steps:
    1. Create Assets/Scripts/SwarmAI/HBT/ folder
    2. TacticTree.cs — abstract base; TacticState enum {APPROACH, SPLIT, PINCER, DIVE, NEUTRALISED}
    3. PincerLowFastTactic.cs : TacticTree — implements APPROACH state:
       - TargetPosition = Vector3.zero (origin = defended position)
       - Each tick: steer agent toward TargetPosition; modify Boids weights
         (cohesion weight low in PINCER to allow independent motion; alignment weight high in APPROACH)
       - Decoys: add sinusoidal lateral oscillation (amplitude = 15m, period = 8s) from seed
    4. SwarmManager registers PincerLowFastTactic when ScenarioConfig.swarmProfile.tacticTree == "PINCER_LOW_FAST"
    5. Evaluate SPLIT_TRIGGER condition each tick; set state = SPLIT when met

DELIVERABLE:
  Assets/Scripts/SwarmAI/HBT/TacticTree.cs
  Assets/Scripts/SwarmAI/HBT/PincerLowFastTactic.cs
  SwarmManager.cs updated

TEST:
  Punjab scenario: 6 agents spawn ~300m out, flock toward origin. Decoys visible at higher
  altitude, oscillating laterally. When lead attacker hits 150m mark, Console logs
  "PINCER SPLIT TRIGGERED". All agents running APPROACH; radarDetectedCount = 6 visible.

DEPENDS: SA-P0-A               UNBLOCKS: SA-P1-B-ii

DEMO: "Plumbing for pincer split — all agents approaching in formation." (Beat 3 setup)
PERSONA: swarm-ai builder     STATUS: [ ] todo
```

---

### SA-P1-B-ii · HBT: PINCER_LOW_FAST — pincer execution + dynamic EW evasion ⭐ [timebox: 4h]

```
CONTEXT:
  SA-P1-B-i complete — APPROACH state + SPLIT_TRIGGER established.
  This card implements the split and the wow-moment behaviour: 4 attackers execute the pincer.
  
  Files to read before starting:
    • docs/PRD.md §3 Beat 4 ⭐ — "4 FPV munitions executing low-level pincer at 120 km/h"
    • docs/PRD.md §3 Beat 3 — "2 large (decoys), 4 small fast-movers"
    • board/contracts/scenario-events.md — SCENARIO_END reason "EXCLUSION_BREACH"
      (any attacker enters inner exclusion zone radius 20m of origin)
  
  HBT state continuation:
  
  SPLIT state:
    - Decoys: peel away at 45° angles (seed-driven left/right assignment), climb to 20m alt
    - Attackers pair into 2 groups: pair A approaches from azimuth 0°, pair B from 180°
      (true pincer — forces trainee to choose engagement axis)
    - rfEmitting: attackers set rfEmitting = false for 5 s after split (EW evasion — go silent)
  
  PINCER / DIVE_ATTACK state:
    - Both attacker pairs close independently; no Boids cohesion (each pair seeks its own vector)
    - At 50m: pairs descend to 2m altitude (skimming approach — harder to radar-detect)
    - At 20m: EXCLUSION_BREACH → ScenarioManager.BroadcastScenarioEnd("EXCLUSION_BREACH")
    - If agent isNeutralised: enter NEUTRALISED state, stop, set propellerRpm = 0
  
  Dynamic EW evasion:
    - If ewJammerState.engaged == true AND ewJammerState.bearingDeg within 15° of agent bearing:
      Agent steers ±30° off current heading (seed-seeded left/right alternation)
      rfEmitting = false for 3 s (frequency-hop behaviour)
  
  Steps:
    1. Add SPLIT, PINCER, DIVE_ATTACK, NEUTRALISED states to PincerLowFastTactic.cs
    2. Implement EW evasion logic in BoidAgent.cs: CheckEvasion(EWJammerState ew)
    3. On NEUTRALISED: stop movement, set isNeutralised=true, neutralisedAtMs = current t
       Notify SwarmManager → check ALL_NEUTRALISED condition

DELIVERABLE:
  PincerLowFastTactic.cs fully implemented (all states)
  BoidAgent.cs updated with EW evasion
  SwarmManager.cs updated with ALL_NEUTRALISED check

TEST:
  Run Punjab scenario without EW engagement: all 4 attackers reach exclusion zone → EXCLUSION_BREACH.
  Run with EW: jamming on correct axis → attackers evade → scenario survives longer.
  Neutralise all 4 → ALL_NEUTRALISED fires → SCENARIO_END.

DEPENDS: SA-P1-B-i             UNBLOCKS: UX-P1-B (EW + SMASH controls become meaningful), SA-P1-C

DEMO: ⭐ "4 FPV munitions executing low-level pincer in pitch-black fog — the wow moment." (Beat 4)
PERSONA: swarm-ai builder     STATUS: [ ] todo
```

---

### SA-P1-C · EW jammer deconfliction logic — friendlyBlackoutRisk [timebox: 3h]

```
CONTEXT:
  SA-P1-B-ii complete — HBT tactics live. EWJammerState is populated by trainee action.
  This card computes friendlyBlackoutRisk and emits the ROE deconfliction signal.
  
  Files to read before starting:
    • docs/SCHEMA.md Entity 3 — EWJammerState (engaged, bearingDeg, elevationDeg, bandGHz,
      friendlyBlackoutRisk); TelemetryRecord.friendlyVhfActive
    • board/contracts/telemetry.md — roeCompliance derived metric definition:
      "1.0 if zero records have ewJammerState.friendlyBlackoutRisk == true; else 0.0"
    • docs/PRD.md §4 MUST — "EW Jammer controls — directional, azimuth/elevation/band-select;
      ROE deconfliction (prevents friendly VHF blackout)"
  
  Deconfliction logic:
    friendlyVHF antenna is assumed to be at bearing 90° (East) from operator position,
    elevation 0°, half-power beamwidth 60°.
    friendlyBlackoutRisk = true when:
      ewJammerState.engaged == true AND
      ewJammerState.bandGHz < 0.5  (VHF/UHF range; 915 MHz ELRS is borderline — treat as risk)
      AND angularDistance(jammer bearing, 90°) < 30°  (within friendly antenna beam)
    
    For 5.825 GHz / 2.4 GHz bands: friendlyBlackoutRisk always = false (out-of-band).
    For 915 MHz: apply bearing check.
  
  Steps:
    1. Assets/Scripts/Sensors/EwDeconfliction.cs — static class:
       - bool ComputeBlackoutRisk(EWJammerState jammer, bool friendlyVhfActive)
    2. SensorSuiteComputer.cs: call EwDeconfliction.ComputeBlackoutRisk() each tick
    3. Inject result into SCENARIO_TICK.ewJammerState.friendlyBlackoutRisk
    4. Also populate SCENARIO_TICK.friendlyVhfActive from ScenarioConfig (Punjab scenario: true)

DELIVERABLE:
  Assets/Scripts/Sensors/EwDeconfliction.cs
  SensorSuiteComputer.cs updated

TEST:
  Set EW jammer to bandGHz=0.915, bearingDeg=90°: Console shows friendlyBlackoutRisk=true.
  Set to 5.825 GHz any bearing: friendlyBlackoutRisk=false.
  Set to 0.915 GHz bearingDeg=180°: friendlyBlackoutRisk=false (out of beam).

DEPENDS: SA-P1-B-ii (EWJammerState shape in use)     UNBLOCKS: AA-P1-A (roeCompliance scorer)

DEMO: "EW engaged without friendly blackout → roeCompliance = 1.0." (Beat 4 ROE proof)
PERSONA: swarm-ai builder                             STATUS: [x] done
```

---

### SA-P1-D · SMASH 2000 lead-angle reticle + KineticEvent computation ⭐ [timebox: 3h]

```
CONTEXT:
  SA-P1-B-ii complete — agents have real velocities and positions.
  This card implements the fire-control system: lead-angle computation and hit/miss logic.
  
  Files to read before starting:
    • docs/SCHEMA.md Entity 3 — KineticEvent (firedAtMs, targetAgentId, hit, leadAngleDeg);
      TraineeAction.kinetic field
    • board/contracts/telemetry.md — KineticEvent field semantics, hit logic
    • docs/PRD.md §4 MUST — "SMASH 2000 fire-control — lead-angle reticle for 7.62 mm kinetic
      engagement; kinetic accuracy scoring"
  
  Lead angle computation (ponytail — simple trigonometry):
    bulletSpeedMs = 900 m/s (7.62 mm NATO MV)
    timeToTarget = distanceToTarget / bulletSpeedMs  (seconds)
    leadVector = agent.velocityMs * timeToTarget
    aimPoint = agent.positionWorld + leadVector
    leadAngleDeg = angle between (trainee aim direction) and (aimPoint − traineePosition)
    hit = leadAngleDeg < accuracyConeHalfAngle (threshold: 5°) AND agent.isNeutralised == false
  
  Trainee aim direction comes from TraineeAction.antennaBearingDeg / antennaElevationDeg
  (the antenna direction doubles as the kinetic fire axis — sentry uses same bearing for EW and kinetic).
  
  Steps:
    1. Assets/Scripts/Sensors/SmashFireControl.cs — static class:
       - KineticEvent ComputeKineticEvent(TraineeAction action, SwarmAgentState target, int t, Vector3 traineePos)
    2. When trainee fires (ewEngaged is toggled in a special "FIRE" event — use a separate bool
       kineticFireTriggered in TraineeAction), compute KineticEvent for the locked target
    3. If hit == true: agent.isNeutralised = true, agent.neutralisedAtMs = t
    4. Inject KineticEvent into TraineeAction.kinetic in SCENARIO_TICK payload

DELIVERABLE:
  Assets/Scripts/Sensors/SmashFireControl.cs
  SwarmManager.cs + SensorSuiteComputer.cs updated with hit/neutralise logic

TEST:
  Play scenario. Aim antenna at approaching agent. Press "FIRE" key.
  Console log: KineticEvent { hit: true/false, leadAngleDeg: X.X }.
  If hit=true: agent stops moving (isNeutralised=true visible in next TICK swarmSnapshot).
  If all 4 attackers neutralised: ALL_NEUTRALISED fires.

DEPENDS: SA-P1-B-ii             UNBLOCKS: AA-P1-A (kinetic metrics need real KineticEvents), UX-P1-B

DEMO: ⭐ "Trainee fires; hit registered; agentId neutralised." (Beat 4 — core wow-moment action)
PERSONA: swarm-ai builder       STATUS: [x] done
```

---

## P2 · Integration (16–19 Oct 2026)

---

### SA-P2-A · Additional tactic trees — SATURATION_DIVE + MULTI-AXIS_SPLIT [timebox: 4h]

```
CONTEXT:
  SA-P1-B-ii complete (PINCER_LOW_FAST done). Unity-sim P1-B scenario configs authored.
  This card adds the other two tactic trees so all 4 scenarios behave differently.
  
  Files to read before starting:
    • docs/SCHEMA.md Entity 1 — SwarmProfile.tacticTree valid values
    • plans/unity-sim-phase-plan.md US-P1-B — scenario profiles for Ladakh (SATURATION_DIVE)
      and LoC (MULTI-AXIS_SPLIT)
    • Assets/StreamingAssets/scenarios/ladakh-lac-ridge-01.json
    • Assets/StreamingAssets/scenarios/loc-mountain-pass-01.json
  
  SATURATION_DIVE (Ladakh, 8 agents):
    - All agents approach simultaneously in a broad front
    - At 100m: all dive from altitudeM to 1m altitude simultaneously
    - No split — overwhelming sensor count
    - EW evasion: random frequency hop every 4 s
  
  MULTI-AXIS_SPLIT (LoC, 5 agents):
    - 1 decoy takes direct frontal approach (loud, visible)
    - 4 attackers split into 4 cardinal approach vectors simultaneously from spawn
    - 915 MHz only (low-tech): no Doppler evasion but narrow-band RF signal
    - Forces trainee to choose correct attack axis under multi-vector pressure
  
  Steps:
    1. SaturationDiveTactic.cs : TacticTree
    2. MultiAxisSplitTactic.cs : TacticTree
    3. TacticTreeFactory.cs — static factory: Create(string tacticTree) → TacticTree
    4. SwarmManager uses TacticTreeFactory instead of hard-coded PincerLowFastTactic

DELIVERABLE:
  Assets/Scripts/SwarmAI/HBT/SaturationDiveTactic.cs
  Assets/Scripts/SwarmAI/HBT/MultiAxisSplitTactic.cs
  Assets/Scripts/SwarmAI/HBT/TacticTreeFactory.cs

TEST:
  Load ladakh-lac-ridge-01 → 8 agents approach broad front, all dive at 100m.
  Load loc-mountain-pass-01 → 4 attackers immediately split to 4 compass points.

DEPENDS: SA-P1-B-ii, US-P1-B     UNBLOCKS: —

DEMO: "4 distinct scenarios, each with different swarm behaviour." (Beat 1 — scenario variety)
PERSONA: swarm-ai builder         STATUS: [x] done
```

---

### SA-P2-B · Performance pass — Burst compilation + 60 FPS validation [timebox: 2h]

```
CONTEXT:
  All swarm-ai logic complete. This card adds Burst compilation to the Boids loop and
  validates FPS on the target hardware (Intel Iris Xe).
  
  Files to read before starting:
    • docs/ARCHITECTURE.md §1 — "Reynolds Boids (C# Job System, Burst-compiled)"
    • docs/BUILD-PLAN.md — "target Intel Iris Xe at 60+ FPS"
  
  Steps:
    1. Profile UnityEditor frame time with BoidFlockController ticking 8 agents:
       if Update() > 2 ms, move to IJobParallelFor + Burst
    2. Refactor BoidFlockController to use Unity.Jobs.IJobParallelFor with [BurstCompile]:
       - BoidStepJob: per-agent separation/alignment/cohesion calculation on NativeArray<float3>
       - Schedule(agentCount) + JobHandle.Complete() in LateUpdate
    3. Build IL2CPP Release → run on Intel Iris Xe → verify ≥60 FPS with 8 agents + full sensor compute
    4. If still below 60 FPS: profile; suspect: radarDetectedCount iterate — add early-out when dist > 500m

DELIVERABLE:
  BoidFlockController.cs updated (optional Burst path)
  FPS measurement noted in board/STATUS.md

TEST:
  IL2CPP build with 8 agents running Punjab scenario: Unity Profiler shows <16.7 ms per frame.

DEPENDS: All SA-P1 cards, US-P2-B (IL2CPP build)     UNBLOCKS: US-P2-D (Warden review)

DEMO: "Smooth 60 FPS with full swarm — no frame drops." (Beat 6 judge Q "can it run on a laptop?")
PERSONA: swarm-ai builder                             STATUS: [x] done
```

---

## P3 · Polish (19–20 Oct 2026)

---

### SA-P3-A · Demo scenario seed tuning — Punjab Pincer guaranteed wow [timebox: 3h]

```
CONTEXT:
  All swarm-ai code complete. This card fine-tunes the Punjab demo scenario seed so that
  the demo beat timeline (Beat 2 at T+15s, Beat 3 at T+30s, Beat 4 at T+45s) is reliable.
  
  Files to read before starting:
    • docs/PRD.md §3 — exact beat timings (T+0, T+15, T+30, T+45, T+55, T+70)
    • Assets/StreamingAssets/scenarios/punjab-fog-pincer-01.json — seed value
  
  Steps:
    1. Run Punjab scenario 5× with seed 42819; record: time of first RF spike, time of SPLIT
    2. Tweak swarm spawn distance (default 300m) so:
       - First RF spike visible at T+15s (agents at ~200m range)
       - SPLIT fires at T+30s (agents at ~150m range at 120 km/h → 150/33 = 4.5 s travel from 300m)
       Required: spawn distance = 300m, speed = 33 m/s → T+9s to 150m → adjust spawn to 375m for ~T+30s split
    3. Update punjab-fog-pincer-01.json if spawn distance adjustment is needed
       (add optional spawnDistanceM field to SwarmProfile, default 300m)
    4. Run 3× with final seed — confirm beats land within ±5s of PRD timings

DELIVERABLE:
  punjab-fog-pincer-01.json updated (if seed/spawn adjustment needed)
  Demo run log: 3 run timestamps proving beat timing

TEST:
  Run Punjab demo 3×: Beat 3 (RF spike + radar 6 sigs) appears 25–35 s into scenario.
  Beat 4 (pincer attack visible) at 40–50 s. Consistent.

DEPENDS: SA-P1-B-ii, US-P1-B     UNBLOCKS: demo dry runs

DEMO: "Demo beat timing is reliable — no seed lottery on judging day." (internal insurance)
PERSONA: swarm-ai builder         STATUS: [ ] todo
```

---

### SA-P3-B · Stub mock file for P0 contract isolation [timebox: 2h]

```
CONTEXT:
  Retrospective: create permanent mock fixtures so future regression tests don't need
  the full sim running. Also useful if any P2 cross-track debugging needs isolated sensor data.
  
  Steps:
    1. Assets/Scripts/Mocks/SwarmMock.cs: static SwarmAgentState[] GetPunjabFixture() — returns
       6 hard-coded agents at T=45200 ms (the wow moment snapshot from SCHEMA.md example)
    2. Assets/Scripts/Mocks/SensorMock.cs: static SensorReadings GetPunjabFixture() — returns
       the values from SCHEMA.md example (rfWaterfallPeakGHz=5.825, dopplerPeakHz=220.4, etc.)
    3. Add a #if UNITY_EDITOR block in SwarmManager: if flag --use-mock-swarm is set in
       PlayerPrefs (editor only), inject SwarmMock.GetPunjabFixture() instead of live data
    4. Document in Assets/Scripts/Mocks/README.md

DELIVERABLE:
  Assets/Scripts/Mocks/SwarmMock.cs
  Assets/Scripts/Mocks/SensorMock.cs
  Assets/Scripts/Mocks/README.md

TEST:
  Editor flag set → swarmSnapshot in TICK is always the 6-agent fixture. Sensor values constant.
  ui-ux and aar-engine can test rendering against this stable input.

DEPENDS: All SA-P1 cards     UNBLOCKS: — (retrospective; any track can use mocks)

DEMO: "Plumbing — test isolation for future regression." (internal)
PERSONA: swarm-ai builder   STATUS: [ ] todo
```

---

## Oracle Pass — Wow Moment Analysis

| Card | Demo beat | Oracle verdict |
|---|---|---|
| SA-P0-A | Beat 2 (6 moving dots) | ⭐ **never-cut** — no Boids = no demo |
| SA-P0-B | Beat 2/3 (RF Waterfall) | ⭐ **never-cut** — sensor suite without RF is dead |
| SA-P1-A | Beat 3 (220 Hz spike) | ⭐ **never-cut** — Doppler scope is a visual centrepiece |
| SA-P1-B-i | Beat 3 (agents split) | ⭐ **never-cut** — the split setup is the wow buildup |
| SA-P1-B-ii | Beat 4 ⭐ (pincer execution) | ⭐ **never-cut** — THIS IS THE WOW MOMENT |
| SA-P1-C | Beat 4 (ROE proof) | never-cut — roeCompliance=1.0 is the doctrinal story |
| SA-P1-D | Beat 4 (kinetic hit) | ⭐ **never-cut** — no kill = no wow-moment resolution |
| SA-P2-A | Beat 1 (4 scenarios) | high-value; cut to 1 extra tactic if time-pressed |
| SA-P2-B | Beat 6 (60 FPS) | never-cut — judge laptops may be slow; FPS matters |
| SA-P3-A | Beat timing reliability | high-value; 3h well spent for demo insurance |
| SA-P3-B | Internal testing | cuttable — nice-to-have; does not affect demo |

**P3 cut order (if endgame is tight):** SA-P3-B → SA-P3-A (keep seed, skip tuning) → SA-P2-A (SATURATION_DIVE only, skip MULTI-AXIS)
