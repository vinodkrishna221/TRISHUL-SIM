# Mission Brief — Vinod · `unity-sim`
> Generated: 2026-10-04 · Assigned by: Orion
> Track: `unity-sim` · Budget: 28h planned / 40h usable (70%)
> Skeleton deadline: **09 Oct 2026 EOD** · Feature freeze: 19 Oct ~21:00 IST

---

## Project one-liner

*"The first battalion-deployable C-UAS trainer that teaches Indian soldiers to defeat a drone swarm in the dark — for ₹10 a seat."*
Full demo narrative → `docs/PRD.md §3` (read this before your first card).

## Mission goal

Stand up the Unity project, WebSocket IPC, scenario lifecycle, split-screen fallback, all 4 scenario JSON configs, the IL2CPP installer, and the air-gap proof — the **entire infrastructure skeleton** that every other track builds on top of.

**Victory definition:** `docs/ARCHITECTURE.md §2` P0 checklist all 6 items green by 09 Oct EOD **+** IL2CPP installer runs clean on Intel Iris Xe at ≥60 FPS **+** Warden smoke passes all 6 demo beats.

---

## Contracts

| Contract | Your role | File |
|---|---|---|
| `websocket-ipc.md` | **OWNER** — implement WsServer + WsClient | `board/contracts/websocket-ipc.md` |
| `scenario-events.md` | **OWNER** — broadcast SCENARIO_START / TICK / END | `board/contracts/scenario-events.md` |
| `scorecard.md` | **CONSUMER** — route SCORECARD_PAYLOAD to both clients | `board/contracts/scorecard.md` |
| `telemetry.md` | consumer (read-only awareness) | `board/contracts/telemetry.md` |

## Stack rules to load

- `docs/ARCHITECTURE.md §1` — every technology decision (Unity 2022 LTS URP, System.Net.WebSockets, IL2CPP, InnoSetup)
- `docs/SCHEMA.md Entity 1` — ScenarioConfig, WeatherPreset, SwarmProfile JSON shapes
- Unity 2022 LTS docs: UIDocument, SceneManager.LoadSceneAsync, Camera.rect

## Branch names (one branch per card — merge when card TEST passes)

| Card | Branch |
|---|---|
| US-P0-A | `feat/unity-sim-ws-ipc` |
| US-P0-B | `feat/unity-sim-scenario-library` |
| US-P0-C | `feat/unity-sim-tick-loop` |
| US-P1-A | `feat/unity-sim-split-screen` |
| US-P1-B | `feat/unity-sim-scenario-configs` |
| US-P2-A | `feat/unity-sim-airgap-validation` |
| US-P2-B | `feat/unity-sim-installer` |
| US-P2-C | `feat/unity-sim-xr-scaffold` |
| US-P2-D | `feat/unity-sim-warden-smoke` |
| US-P3-A | `feat/unity-sim-submission` |

## WAITS-ON (cross-track)

| Your card | Waits on | Mock path (build without waiting) |
|---|---|---|
| US-P2-D Warden smoke | All P1 cards from all tracks | Run smoke with stub swarm (SwarmMock in swarm-ai SA-P3-B) + stub Scorecard (ScorecardMock from scorecard.md example JSON) |
| US-P3-A Submission | All tracks P2 complete | Cannot be mocked — this is the final gate |

**You unblock every other builder.** US-P0-A must be pushed before anyone else can start.

---

## Your cards (all phases)

### US-P0-A · Bootstrap Unity project + WebSocket IPC both ends ⭐ [timebox: 4h]
```
CONTEXT:
  Fresh repo — nothing exists yet. This is card 1 of 1 that unblocks ALL other tracks.

  Files to read before starting:
    • board/contracts/websocket-ipc.md — wire protocol, envelope shape, message types,
      CLIENT_READY / SERVER_HEARTBEAT_ACK / heartbeat lifecycle, error codes
    • docs/ARCHITECTURE.md §1 (Stack Decision Record) — Unity 2022 LTS URP, System.Net.WebSockets,
      no Netcode package, ws://127.0.0.1:9700, IL2CPP target
    • docs/ARCHITECTURE.md §2 (Walking Skeleton) — the 6 P0 checklist items

  Steps:
    1. Unity Hub → new project → Unity 2022 LTS URP template → name "TRISHUL-SIM"
    2. Create two scenes: Assets/Scenes/InstructorC2.unity · Assets/Scenes/TraineeCockpit.unity
    3. Implement Assets/Scripts/Network/WsServer.cs:
       - System.Net.WebSockets.HttpListener on ws://127.0.0.1:9700
       - Start() / Stop() / BroadcastAsync(string json) / ConnectedCount property
       - On connect: read CLIENT_READY, respond SERVER_HEARTBEAT_ACK
       - Heartbeat: every 5s → SERVER_HEARTBEAT_ACK; stale if silent 15s (degraded, don't abort)
       - Envelope parser: static JsonEnvelope Parse(string) → {type, v, ts, payload}
    4. Implement Assets/Scripts/Network/WsClient.cs:
       - ClientWebSocket connecting to ws://127.0.0.1:9700
       - On connect: send CLIENT_READY {clientRole:"TRAINEE_COCKPIT", buildVersion:"0.1.0"}
       - Receive loop: parse envelope → fire C# event OnMessageReceived(JsonEnvelope)
       - Heartbeat: send CLIENT_HEARTBEAT every 5s
       - Reconnect on disconnect: exponential backoff, max 30s
    5. Wire InstructorC2: GameObject "NetworkManager" → WsServer (auto-start)
    6. Wire TraineeCockpit: GameObject "NetworkManager" → WsClient (auto-start)
    7. ConnectionStatusUI.cs stub: UI.Text → "● LAN LINK CONFIRMED" green / "○ Waiting" grey

DELIVERABLE:
  Assets/Scripts/Network/WsServer.cs
  Assets/Scripts/Network/WsClient.cs
  Assets/Scripts/Network/JsonEnvelope.cs
  Assets/Scenes/InstructorC2.unity
  Assets/Scenes/TraineeCockpit.unity

TEST:
  Play InstructorC2 → connect TraineeCockpit (2nd editor or build).
  Console: [WsServer] CLIENT_READY received from TRAINEE_COCKPIT
  Console: [WsClient] SERVER_HEARTBEAT_ACK received — link green
  ConnectionStatusUI: "● LAN LINK CONFIRMED"

DEPENDS: —    UNBLOCKS: everyone (push this branch immediately when TEST passes)
DEMO: "Two-laptop LAN link confirmed — green icon on C2." (Beat 1)
STATUS: [x] done
```

---

### US-P0-B · Scenario Library loader + SCENARIO_START broadcast [timebox: 3h]
```
CONTEXT:
  US-P0-A merged. WsServer + WsClient running.
  Files: board/contracts/scenario-events.md (SCENARIO_START payload), docs/SCHEMA.md Entity 1.
  Steps:
    1. Create Assets/StreamingAssets/scenarios/
    2. Author punjab-fog-pincer-01.json (real data from SCHEMA.md example, seed 42819)
    3. Author 3 stub JSONs: ladakh-lac-ridge-01, loc-mountain-pass-01, desert-fob-01 (minimal valid)
    4. ScenarioLibrary.cs: LoadAll() reads all *.json from StreamingAssets/scenarios/
       regenerate seed each run: config.seed = Random.Range(10000, 99999)
    5. ScenarioManager.cs: LaunchScenario(config) → sessionId = "sess-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")
       → broadcast SCENARIO_START envelope; Trainee side: on SCENARIO_START → load TraineeCockpit scene
    6. InstructorC2 stub UI: dropdown + "Launch" button → ScenarioManager.LaunchScenario()

DELIVERABLE:
  Assets/StreamingAssets/scenarios/punjab-fog-pincer-01.json (real)
  Assets/StreamingAssets/scenarios/[3 stubs]
  Assets/Scripts/Scenarios/ScenarioLibrary.cs
  Assets/Scripts/Scenarios/ScenarioManager.cs
  Assets/Scripts/Scenarios/ScenarioConfig.cs (C# class matching SCHEMA.md Entity 1)

TEST:
  Click Launch → Trainee Console: [WsClient] SCENARIO_START received — sessionId: sess-XXXXXXXX
STATUS: [x] done
```

---

### US-P0-C · SCENARIO_TICK + SCENARIO_END broadcast loop [timebox: 1h]
```
CONTEXT:
  US-P0-B merged.
  Files: board/contracts/scenario-events.md (SCENARIO_TICK 10 Hz, SCENARIO_END terminal conditions).
  In P0: TICK payload has stub swarmSnapshot=[], sensorReadings=zeros, threatState="GREEN".
  Steps:
    1. ScenarioManager: TickLoop coroutine (WaitForSeconds 0.1s), increments t by 100ms
       broadcasts SCENARIO_TICK with stubs; checks t >= durationMaxSecs*1000 → SCENARIO_END
    2. BroadcastScenarioEnd(string reason): builds SCENARIO_END, broadcasts, stops TickLoop
    3. Abort button → BroadcastScenarioEnd("INSTRUCTOR_ABORT")
    4. Stub InjectSwarmSnapshot(SwarmAgentState[] s): sets swarm data for next TICK (wired in P2)

DELIVERABLE: ScenarioManager.cs updated with TickLoop + BroadcastScenarioEnd + stub inject

TEST: Run scenario; Console shows SCENARIO_TICK at ~10Hz; SCENARIO_END on abort.
STATUS: [x] done
```

---

### US-P1-A · Split-screen fallback — both stations on one machine ⭐ [timebox: 4h]
```
CONTEXT:
  P0 done. PRD MUST: single-laptop split-screen fallback (Beat 1 + 6).
  Files: docs/PRD.md §4 MUST, docs/ARCHITECTURE.md §3 sketch.
  Approach: Camera.rect — C2 on left [0,0,0.5,1], Cockpit on right [0.5,0,0.5,1]. Both scenes
  as sub-hierarchies in SplitScreen.unity. WsServer+WsClient run in same process (loopback).
  Steps:
    1. Create Assets/Scenes/SplitScreen.unity with CameraC2 + CameraCockpit rect split
    2. Refactor InstructorC2 + TraineeCockpit into prefabs (InstructorC2Root, TraineeCockpitRoot)
    3. Mode toggle on C2 UI: "Two-Laptop" / "Single-Laptop (Split)" → load appropriate scene
    4. WsServer: try/catch on bind — log warning if already bound (loopback safe)
    5. Verify: 6 Boids in right pane, NDJSON grows, Scorecard renders both sides

DELIVERABLE:
  Assets/Scenes/SplitScreen.unity
  Assets/Prefabs/InstructorC2Root.prefab
  Assets/Prefabs/TraineeCockpitRoot.prefab

TEST: Launch SplitScreen → click Launch (left) → Boids in right pane → Scorecard on both.
DEMO: "Single laptop running full demo." (Beat 6)
STATUS: [x] done
```

---

### US-P1-B · Author all 4 scenario JSON configs [timebox: 3h]
```
CONTEXT:
  US-P0-B stubs exist. PRD MUST: ≥4 procedurally seeded scenarios.
  Files: docs/SCHEMA.md Entity 1, docs/PRD.md §4 MUST scenario list.
  Scenario designs:
    1. punjab-fog-pincer-01 → already real; visibility 15m, night, PINCER_LOW_FAST, 6 agents
    2. ladakh-lac-ridge-01 → altitude 3800m, -12°C, SATURATION_DIVE, 8 agents, day, 200m vis
    3. loc-mountain-pass-01 → MULTI-AXIS_SPLIT, 5 agents, 915 MHz ELRS only, 80 km/h, crosswind 35
    4. desert-fob-01 → SATURATION_DIVE, 8 agents, 5000m vis, day, dust storm 55 km/h, 2.4+5.8 GHz
  Each: different seed so procedural variation is immediately visible.

DELIVERABLE: 4 real JSON files in Assets/StreamingAssets/scenarios/

TEST: ScenarioLibrary.LoadAll() returns 4 configs; dropdown shows all 4 names.
DEMO: "Scenario library shows 4 entries — no two runs identical." (Beat 1)
STATUS: [ ] todo
```

---

### US-P2-A · Air-gap validation pass [timebox: 3h]
```
CONTEXT:
  All P1 integrated. PRD MUST: air-gapped .exe — zero external API calls (Beat 6).
  Files: docs/PRD.md §4 NON-GOALS, docs/BUILD-PLAN.md Deploy targets.
  Steps:
    1. Build Windows x86-64, IL2CPP, Master config
    2. Wireshark loopback capture during full demo run: filter tcp.port != 9700 → expect zero packets
    3. Disable Unity Analytics, URP auto-update checker in Project Settings
    4. Assert in WsServer.Start(): log warning if Application.internetReachability != NotReachable
    5. Document: add "Air-gap: CONFIRMED — <date>" to docs/BUILD-PLAN.md

DELIVERABLE: Build artifact at Build/TRISHUL-SIM-airgap-test-YYYYMMDD.exe + evidence log
TEST: Run .exe with WiFi/Ethernet disabled; full scenario runs; no Task Manager outbound connections.
DEMO: "No internet active — same binary runs in a Ladakh underground bunker." (Beat 6)
STATUS: [ ] todo
```

---

### US-P2-B · IL2CPP build + InnoSetup single-file installer [timebox: 4h]
```
CONTEXT:
  US-P2-A done. Files: docs/ARCHITECTURE.md §1 (IL2CPP, InnoSetup), docs/BUILD-PLAN.md Deploy targets.
  Steps:
    1. Unity Build Settings: IL2CPP, Master, 1920×1080 fullscreen → Build → Build/TRISHUL-SIM/
    2. InnoSetup 6: write trishulsim.iss (AppName, DefaultDirName={pf}\TRISHUL-SIM, Source=Build/TRISHUL-SIM/*)
    3. Compile → dist/TRISHUL-SIM-installer-YYYYMMDD.exe
    4. Smoke: fresh machine → install → scenario → Scorecard renders → FPS ≥60 on Iris Xe

DELIVERABLE: dist/TRISHUL-SIM-installer-<date>.exe · trishulsim.iss
TEST: Clean install on target machine → both demo paths complete. FPS ≥60.
DEMO: "One .exe installs, runs, hits 60 FPS." (Beat 6)
STATUS: [ ] todo
```

---

### US-P2-C · XR scaffold stub [timebox: 2h]
```
CONTEXT:
  PRD SHOULD #1 + CUT LIST #1: scaffold only (no full WebXR). Files: docs/PRD.md §4.
  Steps:
    1. Package Manager → com.unity.xr.management → install; enable "Mock HMD"
    2. Create Assets/Scenes/VRSentryStub.unity (NOT in Build Settings)
    3. InstructorC2 system info panel: add text "XR-Ready: Yes (WebXR scaffold)"

DELIVERABLE: VRSentryStub.unity + XR Plugin Management installed
TEST: Open VRSentryStub.unity — no compile errors. NOT in Build Settings (verify).
DEMO: "'VR-Ready' in system info panel." (Beat 6 judge Q)
STATUS: [ ] todo
```

---

### US-P2-D · Warden review coordination + smoke [timebox: 2h]
```
CONTEXT:
  All tracks P2 done. Files: board/contracts/websocket-ipc.md (latency budgets),
  docs/ARCHITECTURE.md §2 (P0 checklist), .agent/skills/pr-review/SKILL.md.
  Steps:
    1. Run full demo path (all 6 beats) twice: two-laptop + split-screen
    2. Confirm P0 checklist items green; check SCENARIO_START ≤200ms, SCORECARD_PAYLOAD ≤10s
    3. Log regressions in board/DECISIONS.md with "REGRESSION:" prefix
    4. Tag "warden-pass-v1"; write green status to board/STATUS.md

DELIVERABLE: board/STATUS.md updated (Warden pass)
TEST: All 6 P0 checklist items GREEN. No WS ERROR frames.
STATUS: [ ] todo
```

---

### US-P3-A · Final smoke + submission package coordination [timebox: 2h]
```
CONTEXT:
  Feature freeze 19 Oct ~21:00. Files: docs/BUILD-PLAN.md P3, .agent/rules/40-endgame.md.
  Steps:
    1. Final IL2CPP build from clean checkout
    2. Fresh-machine install: scenario loads <5s; 60 FPS
    3. Strip Debug.Log calls from hot path (IL2CPP strips, but verify Console filter)
    4. Hand dist/TRISHUL-SIM-installer-<date>.exe + source zip to Patch for submission

DELIVERABLE: dist/TRISHUL-SIM-installer-<final-date>.exe + board/STATUS.md updated
TEST: Fresh install; both demo paths complete.
STATUS: [ ] todo
```

---

## Sync expectations

- **Push frequency:** Every ≤60 min. `feat/unity-sim-ws-ipc` must be pushed **today (04 Oct)** — it unblocks 5 other people.
- **Blocked:** Post in team chat with card ID + reason. Tag Venu/Sohail/Rani depending on dependency.
- **PRs:** Open a PR per card when TEST passes. No self-merge — tag Venu as reviewer (he's next in dependency chain).
- **Standup:** Ad-hoc in team chat. Post one-liner on status after each card: `[US-P0-A] ✅ WS IPC live — branch pushed`.

---

## Accept

> I, Vinod, have read this brief, understand my cards and the dependency chain, and will push `feat/unity-sim-ws-ipc` by **04 Oct 2026 EOD**.

- [x] **ACCEPTED** — sign off with your name in team chat and tick this box.
