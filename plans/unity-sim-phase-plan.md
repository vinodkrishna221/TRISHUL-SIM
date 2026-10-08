# Phase Plan — `unity-sim`
> Generated: 2026-10-04 · Owner: Orion (Architect persona)
> Track budget: ~40h usable (70% of ~57h allocated across 1–2 builders, 04–20 Oct 2026)
> Skeleton deadline: **09 Oct 2026 EOD** · Submission: 20 Oct 2026 23:59 IST

---

## Cross-Track Dependency DAG

```
unity-sim-P0-A  ──►  swarm-ai-P0-A  (WS IPC live; swarm-ai needs SCENARIO_START to seed Boids)
unity-sim-P0-A  ──►  aar-engine-P0-A (WS IPC live; AAR needs TICK + END events)
unity-sim-P0-A  ──►  ui-ux-P0-A     (WS IPC live; UI polls connection status)
unity-sim-P0-B  ──►  ui-ux-P0-A     (scenario start flow; UI needs Launch button wired)
unity-sim-P1-A  ──►  ui-ux-P1-C     (split-screen fallback; UI must layout for 1-machine mode)
unity-sim-P1-B  ──►  swarm-ai-P1-D  (4 scenario configs; swarm-ai reads SwarmProfile from JSON)
unity-sim-P2-A  ──►  (all tracks)    (Warden P2 review gate — all tracks must be live)

swarm-ai-P0-B   ──►  ui-ux-P1-A     (SensorReadings shape; UI waterfall consumes it)
aar-engine-P0-B ──►  ui-ux-P1-D     (SCORECARD_PAYLOAD; Scorecard Renderer needs it)
aar-engine-P1-A ──►  ui-ux-P1-D     (full ScorecardMetrics; Renderer shows real values)
```

**File surface audit — no overlaps:**
- `unity-sim` owns: `Assets/Scripts/Network/`, `Assets/Scripts/Scenarios/`, `Assets/Scenes/`, `Assets/StreamingAssets/scenarios/*.json`, `Build/`
- `swarm-ai` owns: `Assets/Scripts/SwarmAI/`, `Assets/Scripts/Sensors/`
- `aar-engine` owns: `Assets/Scripts/AAR/`, `Assets/Scripts/PDF/`
- `ui-ux` owns: `Assets/UI/`, `Assets/Scripts/UI/`, `Assets/Localization/`
- **Shared read-only:** `board/contracts/` (no track writes contracts)

---

## Load Ledger

| Phase | Cards | Timebox sum | Cumulative |
|---|---|---|---|
| P0 Skeleton | US-P0-A, US-P0-B, US-P0-C | 4h + 3h + 1h = **8h** | 8h |
| P1 Core | US-P1-A, US-P1-B | 4h + 3h = **7h** | 15h |
| P2 Polish & Hardening | US-P2-A, US-P2-B, US-P2-C, US-P2-D | 3h + 4h + 2h + 2h = **11h** | 26h |
| P3 Endgame | US-P3-A | 2h | **28h** |

**28h of 40h usable = 70% cap exactly. ✓**
Remaining 12h = integration friction, cross-track debug, dry runs.

---

## P0 · Skeleton (04–09 Oct 2026)

> Gate: WS handshake live · Scenario triggers on both screens · 10 Hz log grows

---

### US-P0-A · Bootstrap Unity project + WebSocket IPC both ends ⭐ [timebox: 4h]

```
CONTEXT:
  Fresh repo — nothing exists yet. This is card 1 of 1 that unblocks ALL other tracks.
  
  Files to create/read before starting:
    • board/contracts/websocket-ipc.md  — wire protocol, envelope shape, message types,
      CLIENT_READY / SERVER_HEARTBEAT_ACK / heartbeat lifecycle, error codes
    • docs/ARCHITECTURE.md §1 (Stack Decision Record) — Unity 2022 LTS URP, System.Net.WebSockets,
      no Netcode package, ws://127.0.0.1:9700, IL2CPP target
    • docs/ARCHITECTURE.md §2 (Walking Skeleton) — the 6 P0 checklist items
  
  Repo state at start: empty (git init done). Unity Hub + Unity 2022 LTS already installed.
  
  Steps:
    1. Unity Hub → new project → Unity 2022 LTS URP template → name "TRISHUL-SIM"
       (saves to project root)
    2. Create two scenes: Assets/Scenes/InstructorC2.unity · Assets/Scenes/TraineeCockpit.unity
    3. Implement Assets/Scripts/Network/WsServer.cs:
       - System.Net.WebSockets.HttpListener on ws://127.0.0.1:9700
       - Start() / Stop() / BroadcastAsync(string json) / ConnectedCount property
       - On connect: read CLIENT_READY envelope, log clientRole, respond with SERVER_HEARTBEAT_ACK
       - Heartbeat thread: every 5 s send SERVER_HEARTBEAT_ACK to all clients; if client silent
         for 15 s mark as stale (don't abort scenario — degraded mode per contract)
       - Envelope parser: static JsonEnvelope Parse(string) → { type, v, ts, payload }
    4. Implement Assets/Scripts/Network/WsClient.cs:
       - ClientWebSocket connecting to ws://127.0.0.1:9700
       - On connect: send CLIENT_READY {clientRole:"TRAINEE_COCKPIT", buildVersion:"0.1.0"}
       - Receive loop: parse envelope → fire C# event OnMessageReceived(JsonEnvelope)
       - Heartbeat: send CLIENT_HEARTBEAT every 5 s
       - Reconnect on disconnect: exponential backoff, max 30 s
    5. Wire InstructorC2 scene: GameObject "NetworkManager" → WsServer component, auto-start
    6. Wire TraineeCockpit scene: GameObject "NetworkManager" → WsClient component, auto-start
    7. Add ConnectionStatusUI.cs (minimal): a single UnityEngine.UI.Text that reads
       WsServer.ConnectedCount > 0 → "● LAN LINK CONFIRMED" (green) else "○ Waiting..." (grey)
       (UI track will replace this stub with UITK in ui-ux-P0-A)

DELIVERABLE:
  Assets/Scripts/Network/WsServer.cs
  Assets/Scripts/Network/WsClient.cs
  Assets/Scripts/Network/JsonEnvelope.cs  (shared envelope struct)
  Assets/Scenes/InstructorC2.unity
  Assets/Scenes/TraineeCockpit.unity
  ConnectionStatusUI.cs stub

TEST:
  Play InstructorC2 scene in Unity Editor → open second Editor instance (or build) with
  TraineeCockpit scene → Unity Console on both sides shows:
    [WsServer] CLIENT_READY received from TRAINEE_COCKPIT
    [WsClient] SERVER_HEARTBEAT_ACK received — link green
  ConnectionStatusUI text reads "● LAN LINK CONFIRMED"

DEPENDS: —                   UNBLOCKS: US-P0-B, SA-P0-A, AA-P0-A, UX-P0-A

DEMO: "Two-laptop LAN link confirmed — green icon visible on C2 screen." (Beat 1)
PERSONA: unity-sim builder   STATUS: [x] done
```

---

### US-P0-B · Scenario Library loader + SCENARIO_START broadcast [timebox: 3h]

```
CONTEXT:
  US-P0-A is complete — WsServer running, WsClient connected.
  
  Files to read before starting:
    • board/contracts/scenario-events.md — SCENARIO_START payload shape (full ScenarioConfig
      embedded), consumer reactions, 1-second ACK requirement
    • docs/SCHEMA.md Entity 1 — ScenarioConfig, WeatherPreset, SwarmProfile JSON shapes
    • board/contracts/websocket-ipc.md — Envelope format
  
  Repo state: WsServer.cs, WsClient.cs, JsonEnvelope.cs exist. Two scenes exist.
  
  Steps:
    1. Create Assets/StreamingAssets/scenarios/ directory
    2. Author Assets/StreamingAssets/scenarios/punjab-fog-pincer-01.json:
       Use the verbatim example from SCHEMA.md Entity 1 (seed: 42819)
    3. Add 3 stub scenario JSONs (minimal valid shape, real content in US-P1-B):
       ladakh-lac-ridge-01.json · loc-mountain-pass-01.json · desert-fob-01.json
    4. Implement Assets/Scripts/Scenarios/ScenarioLibrary.cs:
       - LoadAll(): reads all *.json from StreamingAssets/scenarios/ using Application.streamingAssetsPath
       - Returns List<ScenarioConfig>
       - Generates new seed each run: config.seed = Random.Range(10000, 99999)
    5. Implement Assets/Scripts/Scenarios/ScenarioManager.cs:
       - LaunchScenario(ScenarioConfig config): generates sessionId = "sess-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")
       - Broadcasts SCENARIO_START envelope via WsServer.BroadcastAsync()
       - Listens for WsClient.OnMessageReceived on Trainee side → if type == "SCENARIO_START",
         calls SceneLoader.LoadAsync("TraineeCockpit")  [or activates hidden root]
    6. InstructorC2 scene: minimal UI — UnityEngine.UI dropdown populated with scenario names
       + "Launch" button → calls ScenarioManager.LaunchScenario(selected)

DELIVERABLE:
  Assets/StreamingAssets/scenarios/punjab-fog-pincer-01.json  (real data)
  Assets/StreamingAssets/scenarios/ladakh-lac-ridge-01.json   (stub)
  Assets/StreamingAssets/scenarios/loc-mountain-pass-01.json  (stub)
  Assets/StreamingAssets/scenarios/desert-fob-01.json         (stub)
  Assets/Scripts/Scenarios/ScenarioLibrary.cs
  Assets/Scripts/Scenarios/ScenarioManager.cs
  Assets/Scripts/Scenarios/ScenarioConfig.cs  (C# class matching SCHEMA.md Entity 1)

TEST:
  Play InstructorC2 → select "Punjab Border — Zero Visibility Pincer" → click Launch.
  Unity Console (Trainee side) shows: [WsClient] SCENARIO_START received — sessionId: sess-XXXXXXXX
  Trainee Cockpit scene begins loading.

DEPENDS: US-P0-A               UNBLOCKS: SA-P0-A, AA-P0-A, UX-P0-A (SCENARIO_START shape frozen)

DEMO: "Instructor clicks Launch → Trainee screen transitions." (Beat 1)
PERSONA: unity-sim builder     STATUS: [ ] todo
```

---

### US-P0-C · SCENARIO_TICK + SCENARIO_END broadcast loop [timebox: 1h]

```
CONTEXT:
  US-P0-B complete — SCENARIO_START broadcasts correctly.
  
  Files to read:
    • board/contracts/scenario-events.md — SCENARIO_TICK (10 Hz) and SCENARIO_END payloads,
      terminal conditions (TIME_LIMIT | ALL_NEUTRALISED | EXCLUSION_BREACH | INSTRUCTOR_ABORT)
  
  In P0, the TICK payload carries STUB data (swarmSnapshot = [], sensorReadings = zeros).
  Real data is injected by swarm-ai in P1. This card only establishes the broadcast loop.
  
  Steps:
    1. In ScenarioManager.cs, add a coroutine TickLoop():
       - Runs every 0.1 s (10 Hz): InvokeRepeating or StartCoroutine with WaitForSeconds(0.1f)
       - Increments t by 100 ms
       - Builds SCENARIO_TICK envelope with stub swarmSnapshot=[], sensorReadings zeros, threatState="GREEN"
       - Broadcasts via WsServer.BroadcastAsync()
       - Checks terminal condition: t >= durationMaxSecs*1000 → fires SCENARIO_END
    2. BroadcastScenarioEnd(string reason): builds SCENARIO_END envelope, broadcasts, stops TickLoop
    3. "Abort" button on InstructorC2 UI → calls BroadcastScenarioEnd("INSTRUCTOR_ABORT")
    4. swarm-ai will later call ScenarioManager.InjectSwarmSnapshot(SwarmAgentState[]) each tick
       (interface stubbed now; implementation wired in P2)

DELIVERABLE:
  ScenarioManager.cs updated with TickLoop(), BroadcastScenarioEnd(), stub InjectSwarmSnapshot()

TEST:
  Play scenario → wait 120 s (or click Abort).
  Console shows SCENARIO_END broadcast. NDJSON file begins growing (AAR-P0-A must be done first,
  but Tick broadcast is verifiable by Console output alone).
  Run: grep "SCENARIO_TICK" in Unity Console — should appear at ~10 Hz.

DEPENDS: US-P0-B               UNBLOCKS: AA-P0-A (TICK + END events), SA-P0-A (tick clock)

DEMO: "Plumbing for 10 Hz log file — telemetry grows during scenario." (Beat 5 plumbing)
PERSONA: unity-sim builder     STATUS: [ ] todo
```

---

## P1 · Core (09–16 Oct 2026)

> Gate: Split-screen fallback working · 4 scenario configs authored

---

### US-P1-A · Split-screen fallback — both stations on one machine ⭐ [timebox: 4h]

```
CONTEXT:
  P0 complete — two scenes exist, WS IPC live, scenario start/tick/end broadcasting.
  PRD MUST item: single-laptop split-screen fallback (Beat 1, Beat 6).
  
  Files to read:
    • docs/PRD.md §4 (MUST: Dual-station architecture — single-laptop split-screen fallback)
    • docs/ARCHITECTURE.md §3 system sketch (Shared state / split-screen fallback note)
    • board/contracts/websocket-ipc.md (same process can host both WsServer and WsClient
      simultaneously — no conflict; ws://127.0.0.1:9700 loopback)
  
  Repo state: Two scenes (InstructorC2, TraineeCockpit). WsServer + WsClient both exist.
  swarm-ai P0 done — 6 Boids moving. aar-engine P0 done — NDJSON growing.
  
  Split-screen approach (ponytail — simplest that works):
    Use Unity Camera.rect to render C2 on left half [0, 0, 0.5, 1] and Cockpit on right [0.5, 0, 0.5, 1].
    One scene loads both C2 root and Cockpit root as sub-hierarchies.
    WsServer + WsClient both run in same process (loopback — no OS restriction).
  
  Steps:
    1. Create Assets/Scenes/SplitScreen.unity:
       - Two Camera components: CameraC2 (rect 0,0,0.5,1) + CameraCockpit (rect 0.5,0,0.5,1)
       - Instantiate InstructorC2Root prefab (left) + TraineeCockpitRoot prefab (right)
       - Both NetworkManager GameObjects active: WsServer starts first, WsClient connects in ~200 ms
    2. Refactor InstructorC2 and TraineeCockpit to prefabs so they can be instantiated in split-screen
    3. Add a "Mode" toggle on InstructorC2 UI: "Two-Laptop" vs "Single-Laptop (Split)" → loads appropriate scene
    4. Ensure WsServer does NOT bind if already bound (try/catch; log warning only)
    5. Verify: scenario launches in split-screen, 6 Boids visible in right pane, NDJSON grows

DELIVERABLE:
  Assets/Scenes/SplitScreen.unity
  Assets/Prefabs/InstructorC2Root.prefab
  Assets/Prefabs/TraineeCockpitRoot.prefab
  Mode toggle wired in ScenarioManager

TEST:
  Launch SplitScreen.unity. Click Launch on C2 (left). Trainee Cockpit (right) shows Boids.
  Scorecard renders on both sides after scenario end. No external network required.

DEPENDS: US-P0-C, SA-P0-A, AA-P0-A     UNBLOCKS: UX-P1-C (ui-ux split-screen layout)

DEMO: "Single laptop running full demo — no second machine needed." (Beat 6)
PERSONA: unity-sim builder               STATUS: [ ] todo
```

---

### US-P1-B · Author all 4 scenario JSON configs [timebox: 3h]

```
CONTEXT:
  US-P0-B created 3 stub JSONs. This card completes them with real tactical data.
  PRD MUST item: ≥4 procedurally seeded Indian border scenarios.
  
  Files to read:
    • docs/SCHEMA.md Entity 1 — ScenarioConfig, WeatherPreset, SwarmProfile field specs
    • docs/PRD.md §4 MUST — scenario list: Punjab riverine fog, Ladakh LAC ridge,
      LoC mountain pass, Desert FOB
    • board/contracts/scenario-events.md — SwarmProfile.tacticTree valid values:
      "PINCER_LOW_FAST" | "SATURATION_DIVE" | "MULTI-AXIS_SPLIT"
  
  Repo state: Stub JSONs in Assets/StreamingAssets/scenarios/. ScenarioConfig.cs exists.
  
  Scenario design (each must feel distinct in sensor panes):
    1. punjab-fog-pincer-01 → already real (seed 42819); visibility 15m, night, PINCER_LOW_FAST,
       6 agents, 5.825+2.4 GHz, 120 km/h
    2. ladakh-lac-ridge-01 → high altitude (altitudeM 3800), thin air (temperature -12°C),
       SATURATION_DIVE, 8 agents (4 decoy + 4 attacker), visibility 200m, day
    3. loc-mountain-pass-01 → narrow corridor, MULTI-AXIS_SPLIT, 5 agents (1 decoy + 4 attacker),
       915 MHz ELRS only, 80 km/h, crosswind 35 km/h
    4. desert-fob-01 → flat open desert, SATURATION_DIVE, 8 agents (2+6), high visibility 5000m,
       day, dust storm (windSpeedKmh 55), 2.4 GHz + 5.8 GHz
  Each: generate a different seed so procedural variation is immediately visible.

DELIVERABLE:
  Assets/StreamingAssets/scenarios/punjab-fog-pincer-01.json   (confirmed real)
  Assets/StreamingAssets/scenarios/ladakh-lac-ridge-01.json    (real data)
  Assets/StreamingAssets/scenarios/loc-mountain-pass-01.json   (real data)
  Assets/StreamingAssets/scenarios/desert-fob-01.json          (real data)

TEST:
  ScenarioLibrary.LoadAll() returns 4 configs. Dropdown shows all 4 names in English + Hindi.
  Launch each → different SwarmProfile data visible in Console logs.

DEPENDS: US-P0-B, SA-P1-D       UNBLOCKS: SA-P1-D (swarm uses SwarmProfile from these JSONs)

DEMO: "Scenario library shows 4 entries — no two runs identical." (Beat 1)
PERSONA: unity-sim builder       STATUS: [ ] todo
```

---

## P2 · Polish & Hardening (16–19 Oct 2026)

---

### US-P2-A · Air-gap validation pass [timebox: 3h]

```
CONTEXT:
  All tracks integrated. P2 gate: zero external connections.
  PRD MUST item: Air-gapped Windows .exe — zero external API calls (Beat 6).
  
  Files to read:
    • docs/PRD.md §4 NON-GOALS (no cloud, no external API, no internet)
    • docs/BUILD-PLAN.md — Deploy targets (IL2CPP build, Intel Iris Xe at 60+ FPS)
  
  Steps:
    1. Unity Build Settings → Windows x86-64, IL2CPP scripting backend, Mono is fallback
    2. Run Windows Resource Monitor / Wireshark loopback capture during a full demo run:
       - Filter: tcp.port != 9700 → expect zero packets
    3. Check Unity PlayerPrefs / Registry: no API keys, no URLs except 127.0.0.1
    4. Verify Application.internetReachability == NetworkReachability.NotReachable
       (or add an assertion in WsServer.Start() that logs if internet is reachable)
    5. Note any violations; fix them (Unity Analytics, URP update checker must be OFF)
    6. Document result: add a line to docs/BUILD-PLAN.md "Air-gap: CONFIRMED" + date

DELIVERABLE:
  Build artifact at Build/TRISHUL-SIM-airgap-test-YYYYMMDD.exe
  Console/Wireshark screenshot saved to docs/evidence/airgap-pass.png

TEST:
  Run the .exe with WiFi/Ethernet disabled. Scenario runs end-to-end. No error dialogs.
  Task Manager → no outbound connections from TRISHUL-SIM.exe.

DEPENDS: All P1 cards complete     UNBLOCKS: US-P2-B (stable build for installer)

DEMO: "No internet connection active — same binary runs in a Ladakh underground bunker." (Beat 6)
PERSONA: unity-sim builder         STATUS: [ ] todo
```

---

### US-P2-B · IL2CPP build + InnoSetup single-file installer [timebox: 4h]

```
CONTEXT:
  Air-gap validated (US-P2-A). This card produces the demo installer.
  
  Files to read:
    • docs/ARCHITECTURE.md §1 — IL2CPP, InnoSetup, Windows x86-64 target
    • docs/BUILD-PLAN.md — Deploy targets, submission package requirements
  
  Steps:
    1. Unity Build Settings: IL2CPP, Master config, 1920×1080 fullscreen default
    2. Build → Build/TRISHUL-SIM/ (folder with .exe + Data/)
    3. Install InnoSetup 6 (portable, already on build machine)
    4. Write installer script trishulsim.iss:
       - AppName="TRISHUL-SIM C-UAS Trainer"
       - DefaultDirName="{pf}\TRISHUL-SIM"
       - Source: Build/TRISHUL-SIM/* ; DestDir: {app}
       - Run: after install, launch TRISHUL-SIM.exe
    5. Compile → dist/TRISHUL-SIM-installer-YYYYMMDD.exe (single file)
    6. Smoke: fresh machine or fresh user account → install → scenario runs → Scorecard renders

DELIVERABLE:
  dist/TRISHUL-SIM-installer-<date>.exe
  trishulsim.iss (committed to repo)

TEST:
  On a clean machine: run installer → click TRISHUL-SIM → launch scenario → Scorecard renders.
  FPS counter shows ≥60 FPS on Intel Iris Xe.

DEPENDS: US-P2-A               UNBLOCKS: P3 submission package

DEMO: "One .exe installs, runs, hits 60 FPS." (Beat 6)
PERSONA: unity-sim builder     STATUS: [ ] todo
```

---

### US-P2-C · XR scaffold stub [timebox: 2h]

```
CONTEXT:
  PRD SHOULD #1: XR scaffold — Unity XR Plugin Management installed, stub VR scene present,
  NOT on hot path. CUT LIST #1 warns: full WebXR is cut; scaffold only proves extensibility.
  
  Files to read:
    • docs/PRD.md §4 SHOULD #1 and CUT LIST #1
    • docs/ARCHITECTURE.md §1 — XR scaffold row (XR Plugin Management, one stub VR scene)
  
  Steps:
    1. Package Manager → com.unity.xr.management (Unity XR Plugin Management) → install
    2. XR Plug-in Management settings → enable "Mock HMD" (dev only; no hardware required)
    3. Create Assets/Scenes/VRSentryStub.unity — a minimal scene with:
       - XRRig (from XR Interaction Toolkit sample)
       - A sphere labelled "VR SENTRY MODE — SCAFFOLD ONLY — NOT IN DEMO BUILD"
       - This scene is NOT in Build Settings → Scenes in Build
    4. Add to InstructorC2 system info panel: a text line "XR-Ready: Yes (WebXR scaffold)"
    5. Write a 2-line README in Assets/Scenes/VRSentryStub-README.txt explaining scaffold status

DELIVERABLE:
  Assets/Scenes/VRSentryStub.unity
  XR Plugin Management installed (appears in Package Manager)
  System info panel updated

TEST:
  Open VRSentryStub.unity in editor — no compile errors. Scene opens without crashing.
  Main demo build does NOT include this scene (verify Build Settings).

DEPENDS: US-P2-B               UNBLOCKS: —

DEMO: "'VR-Ready' shown in system info panel — extensibility signal to judges." (Beat 6 judge Q)
PERSONA: unity-sim builder     STATUS: [ ] todo
```

---

### US-P2-D · Warden review coordination + smoke [timebox: 2h]

```
CONTEXT:
  All tracks done. Warden review pass (from docs/BUILD-PLAN.md P2 grid: 8h across all tracks).
  unity-sim's share is this card: run the demo thread, verify contract fidelity.
  
  Files to read:
    • board/contracts/websocket-ipc.md — all message types, latency budgets
    • board/contracts/scenario-events.md — SCENARIO_START ≤200 ms, TICK ≤50 ms
    • docs/ARCHITECTURE.md §2 (P0 checklist — re-run all 6 items)
    • .agent/skills/pr-review/SKILL.md — card match, contract fidelity, demo-path safety
  
  Steps:
    1. Run full demo path (all 6 beats) twice: two-laptop mode + split-screen mode
    2. Check each P0 item passes (WS handshake, scenario start ≤1s, 6 Boids, RF spike, NDJSON grows, Scorecard ≤10s)
    3. Check SCENARIO_START latency ≤200 ms (measure with ts field delta)
    4. Check SCORECARD_PAYLOAD latency ≤10s (measure ts delta after SCENARIO_END)
    5. Log any regressions in board/DECISIONS.md with "REGRESSION:" prefix
    6. If all green: tag commit "warden-pass-v1" and write green status to board/STATUS.md

DELIVERABLE:
  board/STATUS.md updated with Warden pass status
  Console log capture for both latency checks

TEST:
  All 6 P0 architecture checklist items GREEN. No WS ERROR frames observed in Console.

DEPENDS: All P2 cards (all tracks)     UNBLOCKS: US-P3-A

DEMO: "All 6 demo beats pass in sequence." (plumbing)
PERSONA: unity-sim builder             STATUS: [ ] todo
```

---

## P3 · Endgame (19–20 Oct 2026)

---

### US-P3-A · Final smoke + submission package coordination [timebox: 2h]

```
CONTEXT:
  Risky-feature freeze 19 Oct ~19:00. Feature freeze 19 Oct ~21:00.
  This card coordinates the unity-sim track's contribution to the submission package.
  
  Files to read:
    • docs/BUILD-PLAN.md P3 section + Freeze calendar
    • .agent/rules/40-endgame.md — last-6-hours doctrine
  
  Steps:
    1. Final IL2CPP build from clean checkout (verify no dev-mode artifacts)
    2. Run installer on clean machine — verify scenario loads in <5 s
    3. Verify: no Unity Analytics, no URP auto-update, no Debug.Log calls in hot path
       (IL2CPP strips them but double-check with Console filter)
    4. Hand dist/TRISHUL-SIM-installer-<date>.exe + source zip to Patch for submission package
    5. Update board/STATUS.md: "unity-sim: DONE — installer @ dist/ — 20 Oct <time>"

DELIVERABLE:
  dist/TRISHUL-SIM-installer-<final-date>.exe (clean build)
  board/STATUS.md updated

TEST:
  Fresh install on target machine → both demo paths (split-screen + two-laptop) complete.

DEPENDS: US-P2-D               UNBLOCKS: Submission (Patch)

DEMO: "Clean installer, both screen modes, 60 FPS — ready to submit." (Beat 6)
PERSONA: unity-sim builder     STATUS: [ ] todo
```

---

## Oracle Pass — Wow Moment Analysis

| Card | Demo beat | Oracle verdict |
|---|---|---|
| US-P0-A | Beat 1 (green LAN icon) | ⭐ **never-cut** — WS IPC is the critical path root; everything else dies without this |
| US-P0-B | Beat 1 (Launch scenario) | ⭐ **never-cut** — scenario start is P0 gate item #2 |
| US-P0-C | P0 plumbing | core — cut unblocks nothing new but breaks AAR |
| US-P1-A | Beat 6 (single-laptop mode) | ⭐ **never-cut** — demo insurance; judges may have 1 laptop |
| US-P1-B | Beat 1 (4 scenarios in library) | high-value P1 — drop to 2 scenarios under extreme time pressure |
| US-P2-A | Beat 6 (air-gap proof) | never-cut — SIH26247 MoD requirement is non-negotiable |
| US-P2-B | Beat 6 (single .exe installer) | never-cut — cannot submit a Unity project folder |
| US-P2-C | Beat 6 (judge Q "VR-ready") | cuttable if P2-A/B are delayed — scaffold is a nice-to-have |
| US-P2-D | Warden gate | procedural — keep; 2h is cheap for insurance |
| US-P3-A | Submission | never-cut — it IS the submission |

**P3 cut order (if endgame is tight):** US-P2-C → US-P2-D (compress to 30 min) → US-P1-B (2 scenarios instead of 4)
