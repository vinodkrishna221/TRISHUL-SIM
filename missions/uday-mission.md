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
STATUS: [x] done
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
STATUS: [x] done
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

## ⚡ NEW TRACK — `vr-gamepad` (added 11 Oct 2026)

> Full phase plan → `plans/vr-gamepad-phase-plan.md`
> You own: **VG-P1-C** (Floating 3D Sensor HUD panels) + **VG-P1-E** (spacedesk display mirror + VR mode toggle)
> Budget: ~8h (5h + 3h). Timeline: 11–18 Oct 2026.
> **Depends on VG-P1-B (Vinod's 3D scene) being done first.**

### VG branch names (Uday's)

| Card | Branch |
|---|---|
| VG-P1-C | `feat/vr-hud-panels` |
| VG-P1-E | `feat/vr-spacedesk-mode-toggle` |

### WAITS-ON (Uday's VG cards)

| Your card | Waits on | Mock path |
|---|---|---|
| VG-P1-C | VG-P1-B (VRRig + VRSentryPost.unity exist), VG-P1-A (GamepadInputController) | Can stub panels as plain coloured quads to test layout |
| VG-P1-E | VG-P1-B, VG-P1-C, VG-P1-D (Sohail's GyroReceiver.IsConnected) | Laptop-only stereo (no phone); stub `IsConnected = false` until Sohail's VG-P1-D lands |

---

### VG-P1-C · Floating 3D Sensor HUD panels [timebox: 5h]
```
CONTEXT:
  The visual wow moment — Iron Man helmet display in the dark.
  Converts existing 2D sensor renderers into world-space 3D floating panels via RenderTextures.
  ADDITIVE change: existing 2D HUD path is unchanged.

  Files to read:
    • Assets/Scripts/UI/RfWaterfallRenderer.cs (Initialize, UpdateWaterfall)
    • Assets/Scripts/UI/DopplerScopeRenderer.cs (Initialize, UpdateScope)
    • Assets/Scripts/UI/AcousticFftRenderer.cs (Initialize, UpdateAcoustic)
    • Assets/Scripts/UI/CockpitController.cs (UpdateSensors, how renderers are called each tick)
    • plans/vr-gamepad-phase-plan.md VG-P1-C CONTEXT (full step list + RenderTexture approach)

  Steps:
    1. Create 4 RenderTexture assets (Assets/RenderTextures/):
       RT_RfWaterfall.renderTexture     512×512 R8G8B8A8
       RT_DopplerScope.renderTexture    512×512 R8G8B8A8
       RT_AcousticFft.renderTexture     512×256 R8G8B8A8
       RT_EwSmashPanel.renderTexture    512×256 R8G8B8A8
    2. Create Assets/Scripts/VR/VRHudController.cs MonoBehaviour on VRRig:
       - Serialized RenderTexture fields for all 4 RTs
       - Subscribe to ScenarioManager events same as CockpitController
       - On tick: update renderers to draw into RenderTextures
         (use Graphics.Blit or camera-per-panel approach — see phase plan)
       - Threat torus color: update MeshRenderer material color per threatState
         GREEN=#00B300, AMBER=#FFB300, RED=#CC0000 (MIL-STD-2525)
    3. In VRSentryPost.unity, create 4 floating Quad GameObjects:
       Panel_RF     at (-2.0, 1.5, 2.5), scale (1.6,0.9,1), rot Y=+30°
                    Material: Unlit, MainTex=RT_RfWaterfall, HDR emissive #00FF8844
       Panel_Doppler at (0, 1.8, 2.8), scale (1.6,1.6,1), rot Y=0°
                    Material: RT_DopplerScope
       Panel_Acoustic at (2.0, 1.5, 2.5), scale (1.2,0.6,1), rot Y=-30°
                    Material: RT_AcousticFft
       Panel_EwSmash at (0, 0.7, 2.0), scale (1.8,0.8,1), rot X=+20° (desk-tilt)
                    Material: RT_EwSmashPanel
    4. TextMeshPro world-space labels above each panel:
       "RF WATERFALL" · "MICRO-DOPPLER RADAR" · "ACOUSTIC SONAR" · "EW / SMASH 2000"
    5. Threat indicator torus around Panel_Doppler:
       Torus mesh, scale (1.8,1.8,0.05), emissive color updated by VRHudController

DELIVERABLE:
  Assets/RenderTextures/ — 4 RT assets
  Assets/Scripts/VR/VRHudController.cs
  VRSentryPost.unity updated (4 panels + labels + threat torus)
  Unlit emissive materials for each panel

TEST:
  Play VRSentryPost.unity → launch Punjab scenario from C2 (WebSocket).
  → RF Waterfall scrolling on left panel
  → Doppler scope with 6 contacts on centre panel
  → Acoustic FFT bars on right panel
  → EW/SMASH read-out on bottom panel
  → Threat torus: GREEN → AMBER → RED
  → Both left/right eye cameras see panels at correct positions

DEPENDS: VG-P1-B (scene + VRRig exist)    UNBLOCKS: VG-P1-E (integration)
DEMO: "Iron Man HUD — four holographic sensor panels glowing in darkness." (Beat 4 wow)
PERSONA: Uday (swarm-ai)   STATUS: [ ] todo
```

---

### VG-P1-E · spacedesk display mirror + VR mode toggle [timebox: 3h]
```
CONTEXT:
  spacedesk makes phone = Windows Display 2. Unity cameras target Display 2.
  Instructor C2 gets [VR MODE] button. Falls back cleanly to desktop mode.

  Manual pre-requisites (do BEFORE coding):
    a. Install spacedesk DRIVER on laptop: https://www.spacedesk.net
    b. Install spacedesk Android app on phone (Play Store)
    c. Enable Windows WiFi hotspot (Settings → Network → Mobile hotspot)
    d. Connect phone to laptop hotspot → open spacedesk → select laptop → Display 2 appears

  Files to read:
    • Assets/Scripts/UI/C2StationController.cs (button/mode wiring)
    • Assets/Scripts/Scenarios/ScenarioManager.cs (OperationMode enum)
    • plans/vr-gamepad-phase-plan.md VG-P1-E CONTEXT (full step list)

  Steps:
    1. ScenarioManager.cs: add VRMode to OperationMode enum
    2. C2StationController.cs: add [VR MODE] toggle button
       → ScenarioManager.Instance.SetMode(OperationMode.VRMode)
    3. Assets/Scripts/VR/VRModeController.cs MonoBehaviour (persistent GO):
       - On VRMode: LoadScene("VRSentryPost") → all VR cameras targetDisplay = 1
       - On exit: LoadScene("TraineeCockpit") → targetDisplay = 0
    4. C2 status badge: "VR MODE: ON ● GYRO: LIVE" / "VR MODE: ON ○ GYRO: OFFLINE"
    5. Create docs/vr-setup-guide.md (6-step demo-day guide)

DELIVERABLE:
  Assets/Scripts/VR/VRModeController.cs
  ScenarioManager.cs updated (VRMode enum)
  C2StationController.cs updated (VR MODE button)
  docs/vr-setup-guide.md

TEST:
  WITH spacedesk: VR MODE → VRSentryPost on phone (Display 2), C2 on laptop (Display 1).
  WITHOUT phone: VR MODE → VRSentryPost on laptop stereo (fallback).
  VR MODE OFF → desktop Trainee Cockpit loads, no regression.

DEPENDS: VG-P1-B, VG-P1-C, VG-P1-D    UNBLOCKS: VG-P1-F
DEMO: "Instructor clicks VR MODE — trainee puts on headset — border post appears." (Beat 1)
PERSONA: Uday (swarm-ai)   STATUS: [x] done
```

---

## Sync expectations

- **Start SA-P1-C + SA-P1-D immediately after Venu pushes `feat/swarm-ai-hbt-pincer`** — don't wait for PR merge if it's passing locally; ask Venu to share the branch.
- **SA-P1-D push is critical for Sohail** — AAR-P1-A kineticEfficiency needs real KineticEvents.
- **Blocked on Venu:** Use inline stub EWJammerState / SwarmMock to keep moving.
- **PRs:** Tag Venu as reviewer for SA-P1-C/D (he owns the context). Tag Vinod for SA-P2-B (installer dependency).
- **VG-P1-C & VG-P1-E:** Start VG-P1-C only after Vinod pushes `feat/vr-sentry-post-scene` (VG-P1-B done). Tag Vinod in PRs.

---

## Accept

> I, Uday, have read this brief and will start SA-P1-C as soon as Venu's SA-P1-B-ii branch is pushed.

- [ ] **ACCEPTED**
