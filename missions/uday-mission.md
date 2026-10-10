# Mission Brief — Uday · `swarm-ai` (P1 second half + P2 + P3)
> Generated: 2026-10-04 · Assigned by: Orion
> Track: `swarm-ai` · Budget: 17h planned / 28h usable (61%)
> Skeleton deadline: N/A for your cards (P0 is Venu's) · Feature freeze: 19 Oct ~21:00 IST

---

## Project one-liner

*"The first battalion-deployable C-UAS trainer that teaches Indian soldiers to defeat a drone swarm in the dark — for ₹10 a seat."*
Full demo narrative → `docs/PRD.md §3`. Your work completes **Beat 4** (ROE deconfliction + SMASH kill) and makes all 4 scenarios distinct (Beat 1).

## Mission goal

Build EW jammer deconfliction (friendlyBlackoutRisk), SMASH 2000 lead-angle fire control + KineticEvent, two additional tactic trees (SATURATION_DIVE + MULTI-AXIS_SPLIT), Burst performance pass, and demo seed tuning — the **second half of swarm intelligence that resolves the wow moment**.

**Victory definition:** Punjab Pincer: trainee fires SMASH → KineticEvent.hit=true registered → ALL_NEUTRALISED fires. roeCompliance=1.0 when EW used correctly. All 4 scenarios play differently. 60 FPS on IL2CPP build.

---

## Contracts

| Contract | Your role | File |
|---|---|---|
| `scenario-events.md` | **CONSUMER** — SCENARIO_TICK.ewJammerState + traineeAction.kinetic | `board/contracts/scenario-events.md` |
| `telemetry.md` | **CONSUMER** — EWJammerState, KineticEvent, friendlyBlackoutRisk definitions | `board/contracts/telemetry.md` |
| `scorecard.md` | consumer (roeCompliance derived from your output) | `board/contracts/scorecard.md` |

**You own:** `Assets/Scripts/Sensors/EwDeconfliction.cs` · `Assets/Scripts/Sensors/SmashFireControl.cs` · `Assets/Scripts/SwarmAI/HBT/SaturationDiveTactic.cs` · `Assets/Scripts/SwarmAI/HBT/MultiAxisSplitTactic.cs` · `Assets/Scripts/SwarmAI/HBT/TacticTreeFactory.cs` · `Assets/Scripts/Mocks/`

## Branch names

| Card | Branch |
|---|---|
| SA-P1-C | `feat/swarm-ai-ew-deconfliction` |
| SA-P1-D | `feat/swarm-ai-smash-fire-control` |
| SA-P2-A | `feat/swarm-ai-extra-tactics` |
| SA-P2-B | `feat/swarm-ai-burst-performance` |
| SA-P3-A | `feat/swarm-ai-demo-seed-tune` |
| SA-P3-B | `feat/swarm-ai-mock-fixtures` |

## WAITS-ON

| Your card | Waits on | Mock path |
|---|---|---|
| SA-P1-C EW deconfliction | **Venu: SA-P1-B-ii** (EWJammerState in use; HBT must be live for evasion to trigger) | If SA-P1-B-ii delayed: stub EWJammerState inline → `new EWJammerState { engaged=true, bearingDeg=247f, bandGHz=5.825f }` |
| SA-P1-D SMASH fire control | **Venu: SA-P1-B-ii** (agents need real velocityMs for lead-angle computation) | Use SwarmMock.GetPunjabFixture() (SA-P3-B) for unit-testing lead-angle math in isolation |
| SA-P2-A extra tactics | Vinod: US-P1-B (4 scenario JSONs authored) | Start implementing SATURATION_DIVE logic while waiting — wire to JSON in final step |
| SA-P2-B Burst pass | Vinod: US-P2-B (IL2CPP build) | Profile in editor first; Unity Profiler gives same data before IL2CPP |

---

## Your cards

### SA-P1-C · EW jammer deconfliction — friendlyBlackoutRisk [timebox: 3h]
```
CONTEXT:
  Venu's SA-P1-B-ii must be merged first (EWJammerState actively in use by HBT).
  If delayed, use inline stub EWJammerState (see mock path above).

  Files to read:
    • board/contracts/telemetry.md — EWJammerState shape; friendlyBlackoutRisk definition;
      roeCompliance derived metric: "1.0 if zero records have friendlyBlackoutRisk == true"
    • docs/SCHEMA.md Entity 3 — EWJammerState, TelemetryRecord.friendlyVhfActive
    • docs/PRD.md §4 MUST — "EW Jammer — ROE deconfliction (prevents friendly VHF blackout)"

  Deconfliction logic:
    Friendly VHF antenna at bearing 90° (East), elevation 0°, half-power beamwidth 60°.
    friendlyBlackoutRisk = true when:
      ewJammerState.engaged == true
      AND ewJammerState.bandGHz < 0.5 OR bandGHz == 0.915  (VHF/UHF + 915 MHz borderline)
      AND angularDistance(jammer bearingDeg, 90°) < 30°
    For 5.825 GHz / 2.4 GHz: always false (out-of-band).

  Steps:
    1. Assets/Scripts/Sensors/EwDeconfliction.cs (static class):
       bool ComputeBlackoutRisk(EWJammerState jammer, bool friendlyVhfActive)
    2. SensorSuiteComputer.cs: call EwDeconfliction.ComputeBlackoutRisk() each tick
    3. Inject result into SCENARIO_TICK.ewJammerState.friendlyBlackoutRisk
    4. Populate SCENARIO_TICK.friendlyVhfActive from ScenarioConfig (Punjab: true)

DELIVERABLE: Assets/Scripts/Sensors/EwDeconfliction.cs · SensorSuiteComputer.cs updated

TEST:
  band=0.915, bearing=90°: friendlyBlackoutRisk=true in Console.
  band=5.825, any bearing: false.
  band=0.915, bearing=180°: false (out of beam).
DEMO: "EW engaged without friendly blackout → roeCompliance=1.0." (Beat 4 ROE proof)
STATUS: [x] done
```

---

### SA-P1-D · SMASH 2000 lead-angle reticle + KineticEvent computation ⭐ [timebox: 3h]
```
CONTEXT:
  Venu's SA-P1-B-ii merged (real agent velocities available).
  If delayed: use SwarmMock.GetPunjabFixture() for unit tests.

  Files:
    • board/contracts/telemetry.md — KineticEvent fields (firedAtMs, targetAgentId, hit, leadAngleDeg)
    • docs/SCHEMA.md Entity 3 — TraineeAction.kinetic
    • docs/PRD.md §4 MUST — "SMASH 2000 fire-control — lead-angle reticle for 7.62mm kinetic"

  Lead-angle computation:
    bulletSpeedMs = 900 m/s (7.62mm NATO MV)
    timeToTarget = distanceToTarget / bulletSpeedMs
    leadVector = agent.velocityMs * timeToTarget
    aimPoint = agent.positionWorld + leadVector
    leadAngleDeg = angle between (trainee aim direction) and (aimPoint − traineePos)
    hit = leadAngleDeg < 5° AND agent.isNeutralised == false

  Trainee aim direction = from TraineeAction.antennaBearingDeg / antennaElevationDeg
  (antenna direction doubles as kinetic fire axis — sentry uses same bearing for both)

  Steps:
    1. Assets/Scripts/Sensors/SmashFireControl.cs (static class):
       KineticEvent ComputeKineticEvent(TraineeAction, SwarmAgentState target, int t, Vector3 traineePos)
    2. On kineticFireTriggered: compute KineticEvent for locked target
    3. If hit: agent.isNeutralised=true, agent.neutralisedAtMs=t
    4. Inject KineticEvent into TraineeAction.kinetic in TICK payload

DELIVERABLE: Assets/Scripts/Sensors/SmashFireControl.cs · SwarmManager + SensorSuiteComputer updated

TEST:
  Aim at approaching agent; press FIRE key. Console: KineticEvent{hit:true/false, leadAngleDeg:X.X}
  hit=true → agent stops moving (isNeutralised in next TICK swarmSnapshot).
  All 4 attackers neutralised → ALL_NEUTRALISED → SCENARIO_END.

PUSH IMMEDIATELY — Sohail (AA-P1-A) needs real KineticEvents to compute kineticEfficiency.
DEMO: ⭐ "Trainee fires; hit registered; agentId neutralised." (Beat 4 core resolution)
STATUS: [x] done
```

---

### SA-P2-A · Additional tactic trees — SATURATION_DIVE + MULTI-AXIS_SPLIT [timebox: 4h]
```
CONTEXT:
  SA-P1-B-ii complete. Vinod's US-P1-B (4 scenario JSONs) should be done.
  If JSONs not ready: implement the logic first; wire to JSON in last step.

  Files:
    • docs/SCHEMA.md Entity 1 — SwarmProfile.tacticTree valid values
    • plans/unity-sim-phase-plan.md US-P1-B — scenario profiles for Ladakh (SATURATION_DIVE)
      and LoC (MULTI-AXIS_SPLIT)

  SATURATION_DIVE (Ladakh, 8 agents):
    All agents approach in broad front; at 100m: all dive to 1m alt simultaneously.
    EW evasion: random freq hop every 4s.

  MULTI-AXIS_SPLIT (LoC, 5 agents):
    1 decoy takes direct frontal approach; 4 attackers immediately split to 4 cardinal vectors.
    915 MHz only; no Doppler evasion.

  Steps:
    1. Assets/Scripts/SwarmAI/HBT/SaturationDiveTactic.cs : TacticTree
    2. Assets/Scripts/SwarmAI/HBT/MultiAxisSplitTactic.cs : TacticTree
    3. Assets/Scripts/SwarmAI/HBT/TacticTreeFactory.cs — Create(string) → TacticTree
    4. SwarmManager uses TacticTreeFactory instead of hard-coded PincerLowFastTactic

DELIVERABLE:
  SaturationDiveTactic.cs · MultiAxisSplitTactic.cs · TacticTreeFactory.cs

TEST:
  Load ladakh-lac-ridge-01 → 8 agents broad front, all dive at 100m.
  Load loc-mountain-pass-01 → 4 attackers immediately split to 4 compass points.
DEMO: "4 distinct scenarios, each with different swarm behaviour." (Beat 1 — variety)
STATUS: [ ] todo
```

---

### SA-P2-B · Burst compilation + 60 FPS validation [timebox: 2h]
```
CONTEXT:
  All swarm logic done. Vinod's IL2CPP build (US-P2-B) needed for final validation.
  Files: docs/ARCHITECTURE.md §1 ("Burst-compiled"), docs/BUILD-PLAN.md ("target Iris Xe 60+ FPS").

  Steps:
    1. Profile BoidFlockController in Editor with 8 agents:
       if Update() > 2ms → refactor to IJobParallelFor + [BurstCompile]
       BoidStepJob: per-agent separation/alignment/cohesion on NativeArray<float3>
       Schedule(agentCount) + JobHandle.Complete() in LateUpdate
    2. IL2CPP Release build → run on Intel Iris Xe → verify ≥60 FPS with 8 agents + full sensors
    3. If still below: early-out when agent distance > 500m in radarDetectedCount loop
    4. Note result in board/STATUS.md

DELIVERABLE: BoidFlockController.cs updated (Burst path if needed) · FPS result logged
TEST: IL2CPP build + 8 agents running Punjab: Unity Profiler < 16.7ms/frame.
STATUS: [ ] todo
```

---

### SA-P3-A · Demo scenario seed tuning — Punjab Pincer guaranteed wow [timebox: 3h]
```
CONTEXT:
  All swarm-ai code done. PRD beat timings: Beat 3 at T+30s, Beat 4 at T+45s.
  Files: docs/PRD.md §3, Assets/StreamingAssets/scenarios/punjab-fog-pincer-01.json.

  Target: first RF spike at T+15s; PINCER SPLIT at ~T+30s; all attackers close by T+45s.
  At speed 120 km/h (33 m/s): spawn at 375m → hits 150m trigger at T≈6.8s. Adjust:
  spawn distance ≈ 450m for T+30s split.
  (Add optional spawnDistanceM field to SwarmProfile if needed, default 300m)

  Steps:
    1. Run Punjab 5× with seed 42819; log: time of first RF spike, time of SPLIT
    2. Adjust spawn distance in JSON until split ≈ T+25–35s
    3. Run 3× with final seed — confirm Beat 3 at 25–35s, Beat 4 at 40–50s

DELIVERABLE: punjab-fog-pincer-01.json updated (if needed) · 3-run timing log
TEST: Beat 3 (RF spike + radar 6 sigs) at T+25–35s consistently.
STATUS: [ ] todo
```

---

### SA-P3-B · Mock fixtures for P0 contract isolation [timebox: 2h]
```
CONTEXT:
  Retrospective: create stable fixtures for cross-track debugging.
  Steps:
    1. Assets/Scripts/Mocks/SwarmMock.cs: static SwarmAgentState[] GetPunjabFixture()
       (6 hard-coded agents at T=45200ms from SCHEMA.md example)
    2. Assets/Scripts/Mocks/SensorMock.cs: static SensorReadings GetPunjabFixture()
       (rfWaterfallPeakGHz=5.825, dopplerPeakHz=220.4, acousticSplDb=54.2, etc.)
    3. #if UNITY_EDITOR guard in SwarmManager: PlayerPrefs flag → inject SwarmMock instead of live
    4. Assets/Scripts/Mocks/README.md explaining usage

DELIVERABLE: SwarmMock.cs · SensorMock.cs · README.md
TEST: Editor flag set → swarmSnapshot in TICK is always the 6-agent fixture. Sensor values constant.
STATUS: [ ] todo
```

---

## Sync expectations

- **Start SA-P1-C + SA-P1-D immediately after Venu pushes `feat/swarm-ai-hbt-pincer`** — don't wait for PR merge if it's passing locally; ask Venu to share the branch.
- **SA-P1-D push is critical for Sohail** — AAR-P1-A kineticEfficiency needs real KineticEvents.
- **Blocked on Venu:** Use inline stub EWJammerState / SwarmMock to keep moving.
- **PRs:** Tag Venu as reviewer for SA-P1-C/D (he owns the context). Tag Vinod for SA-P2-B (installer dependency).

---

## Accept

> I, Uday, have read this brief and will start SA-P1-C as soon as Venu's SA-P1-B-ii branch is pushed.

- [ ] **ACCEPTED**
