# Decisions — TRISHUL-SIM

> ADR-lite: 3 lines per entry, 30 seconds to write, saves the 2am "why is it like this".
> Log: irreversible choices, contract/schema changes, pivots, Direction Checks overruled.

---

### 1 · Idea choice + Scale Gate                     2026-10-04 · by team (/kickoff)
CHOSE: TRISHUL-SIM — physics-grounded C-UAS tactical simulation trainer + AAR engine; FULL mode PRD.
BECAUSE: SIH26247 MoD problem statement; 16 days to submission; 4–6 builders warranting full 3-page PRD and phase plans.
AFFECTS: All tracks — vocabulary frozen (Scenario, Sensor Suite, Swarm, AAR Engine, Scorecard, Trainee, Instructor, Kill Chain).

### 2 · Primary demo user                            2026-10-04 · by Orion (/kickoff)
CHOSE: Havildar Rajan (Jawan/Sentry) as the ONE judge-facing user; Instructor (JCO) is supporting infrastructure.
BECAUSE: Emotional demo arc requires a single underdog succeeding — not a commander orchestrating.
AFFECTS: Demo Narrative beat order, UI copy tone, Herald pitch spine sentence.

### 3 · Mandatory tech stack                         2026-10-04 · by team (/kickoff)
CHOSE: Unity 2022/6 LTS URP + Windows .exe + localhost WebSocket IPC; XR plugin scaffolded for future VR.
BECAUSE: SIH26247 constraint (Unity mandatory), MoD air-gap requirement (no cloud), team capability.
AFFECTS: All tracks — Unreal Engine banned; no cloud API calls permitted at any layer.

### 4 · Data store: NDJSON file over SQLite            2026-10-04 · by Atlas (/blueprint)
CHOSE: Newline-delimited JSON written to %AppData%\TRISHUL-SIM\sessions\ at 10 Hz.
BECAUSE: Zero extra dependency; grep-able; Timeline Scrubber can seek by byte offset; PDF export is a C# loop. SQLite adds a package and migration surface for zero benefit at 2-station scale.
AFFECTS: aar-engine (owns write); ui-ux (reads for scrubber); PDF export reads same file.

### 5 · WebSocket authority: Instructor C2 is server   2026-10-04 · by Orion (/blueprint)
CHOSE: Instructor C2 app binds ws://127.0.0.1:9700 as server; Trainee Cockpit connects as client.
BECAUSE: Scenario state authority must have a single owner; JCO machine is the natural command node; avoids split-brain in split-screen fallback (same process hosts both sides).
AFFECTS: unity-sim (implements both server + client); all contracts reference this topology.

### 6 · Swarm AI: deterministic Boids + HBT, no ML     2026-10-04 · by Sage (/blueprint)
CHOSE: Reynolds Boids (Burst/Job System, C#) + Hierarchical Behavior Trees for tactic logic.
BECAUSE: Deterministic = replay-safe (AAR Scrubber can reproduce any tick exactly); ML-Agents are non-deterministic black boxes incompatible with MoD trust model; HBT is auditable and timeboxable.
AFFECTS: swarm-ai track exclusively; sensor physics models consume SwarmAgentState (contract).

### 7 · 4-track confirmation                           2026-10-04 · by Orion (/blueprint)
CHOSE: 4 tracks confirmed — unity-sim · swarm-ai · aar-engine · ui-ux. No collapse.
BECAUSE: Each track has a clean file-ownership boundary with zero overlap; contracts are the only shared surface. Collapsing any two would create ambiguous ownership of the walking skeleton critical path.
AFFECTS: All phase plans; missions will be generated per this 4-track split.

### 8 · Fit audit: clean bill of health                2026-10-04 · by Orion (/blueprint)
CHOSE: No new kit rules/packs required; one optional micro-addition (rules/stack/unity-cs.md, 3 lines) pending human approval.
BECAUSE: All tech gaps are Unity/C# stdlib — no external vendor rules needed; existing WarRoom rules cover all process concerns.
AFFECTS: Kit is frozen; unity-cs.md added only if human approves the diff in board/FIT-AUDIT.md.

### 9 · Punjab military watchtower environment & fog   2026-10-17 · by Vinod (vr-gamepad)
CHOSE: Centered Punjab watchtower FBX at origin with cabin door swung 75° open; removed Blender volumetric fog cube and used native Unity URP ExponentialSquared fog.
BECAUSE: Blender volumetric fog exports as an opaque solid mesh to Unity FBX; URP native fog handles atmospheric riverine fog properly without occluding viewports; origin centering aligns sentry eye level (1.65m) and HUD panels seamlessly.
AFFECTS: Assets/Scenes/VRSentryPost.unity, Assets/Models/punjab_military_terrain_watchtower.fbx, Assets/Scripts/VR/Editor/VRHudSetup.cs.

### 10 · Standalone VR Mission Selector, Stop Control & Floating 3D Holographic AAR  2026-10-17 · by Vinod (vr-gamepad)
CHOSE: Dynamic runtime VRScenarioSelector & VRHolographicScorecard quad (512x384) with keyboard [Esc]/Gamepad [B] stop and [Space]/[R]/[P] AAR actions.
BECAUSE: Allows closed-loop VR training without InstructorC2; in-flight stop provides immediate mission halt into AAR; runtime generation preserves zero-drone scene YAML doctrine.
AFFECTS: Assets/Scripts/VR/VRScenarioSelector.cs, Assets/Scripts/VR/VRHolographicScorecard.cs, Assets/Scripts/VR/VRHudController.cs, Assets/Scripts/VR/VRSentryPostSelfTest.cs.


