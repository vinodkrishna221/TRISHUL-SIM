# Mission Brief — Sohail · `aar-engine`
> Generated: 2026-10-04 · Assigned by: Orion
> Track: `aar-engine` · Budget: 20h planned / 28h usable (71%)
> Skeleton deadline: **09 Oct 2026 EOD** · Feature freeze: 19 Oct ~21:00 IST

---

## Project one-liner

*"The first battalion-deployable C-UAS trainer that teaches Indian soldiers to defeat a drone swarm in the dark — for ₹10 a seat."*
Full demo narrative → `docs/PRD.md §3`. Your work powers **Beat 5** entirely — the Tactical Performance Scorecard rendering in ≤10 seconds, the Timeline Scrubber, and the PDF export.

## Mission goal

Build the 10 Hz TelemetryLogger (NDJSON), the stub + real Decision Tree Scorer, the Timeline Scrubber data engine, the iTextSharp PDF exporter, and harden against demo-day disk failures — **the entire AAR engine from log to dossier**.

**Victory definition:** Punjab scenario ends → within ≤10 s SCORECARD_PAYLOAD appears on both screens with real metrics (timeToDetectionMs non-zero, roeCompliance correct, kineticEfficiency correct) → Timeline Scrubber navigable → PDF exports in one click.

---

## Contracts

| Contract | Your role | File |
|---|---|---|
| `telemetry.md` | **OWNER** — write TelemetryRecord to NDJSON; own session file lifecycle | `board/contracts/telemetry.md` |
| `scorecard.md` | **OWNER** — compute Scorecard; emit SCORECARD_PAYLOAD; write scorecard.json | `board/contracts/scorecard.md` |
| `scenario-events.md` | **CONSUMER** — react to SCENARIO_START (open file), TICK (append line), END (flush + score) | `board/contracts/scenario-events.md` |
| `websocket-ipc.md` | **CONSUMER** — WsClient.SendAsync (Client→Server SCORECARD_PAYLOAD) | `board/contracts/websocket-ipc.md` |

**You own:** `Assets/Scripts/AAR/` · `Assets/Scripts/PDF/` · `%AppData%\TRISHUL-SIM\sessions\` at runtime
**You do NOT touch:** `Assets/Scripts/Network/` (only subscribe to events) · `Assets/Scripts/SwarmAI/` · `Assets/UI/`

## Stack rules to load

- `docs/ARCHITECTURE.md §1` — "NDJSON to %AppData%; iTextSharp MIT; no SQLite"
- `docs/SCHEMA.md Entity 3` — TelemetryRecord (all REQUIRED fields + nested types)
- `docs/SCHEMA.md Entity 4` — Scorecard + ScorecardMetrics + DoctrineBenchmarks
- `docs/SCHEMA.md Entity 5` — SessionManifest + SessionSummary

## Branch names

| Card | Branch |
|---|---|
| AA-P0-A | `feat/aar-engine-telemetry-logger` |
| AA-P0-B | `feat/aar-engine-stub-scorecard` |
| AA-P1-A | `feat/aar-engine-decision-tree-scorer` |
| AA-P1-B | `feat/aar-engine-timeline-indexer` |
| AA-P2-A | `feat/aar-engine-pdf-export` |
| AA-P2-B | `feat/aar-engine-warden-review` |
| AA-P3-A | `feat/aar-engine-partial-guard` |

## WAITS-ON

| Your card | Waits on | Mock path |
|---|---|---|
| AA-P0-A TelemetryLogger | **Vinod: US-P0-A + US-P0-C** (WsClient must exist; TICK must broadcast) | Cannot be fully mocked — need WsClient.OnMessageReceived. Coordinate with Vinod. |
| AA-P1-A full Scorer | **Uday: SA-P1-C + SA-P1-D** (friendlyBlackoutRisk + KineticEvent must be real) | Use inline fixture for testing scorer math: create `AarTestFixture.cs` with hard-coded TelemetryRecord list matching SCHEMA.md example |
| AA-P1-B Timeline | AA-P1-A (timelineIndex must be built by scorer) | Start TimelineIndexer.cs in isolation — test with any NDJSON you write manually |
| AA-P2-A PDF | AA-P1-A (full Scorecard needed for metrics table) | Test with ScorecardMock: parse the JSON example from scorecard.md |

---

## Your cards

### AA-P0-A · TelemetryLogger — 10 Hz NDJSON append on SCENARIO_TICK ⭐ [timebox: 3h]
```
CONTEXT:
  Vinod's US-P0-A (WsClient exists) + US-P0-C (SCENARIO_TICK + SCENARIO_END broadcast) must be merged.
  Venu's SA-P0-A should be done (real swarmSnapshot); if not, the logger still works — empty array logs fine.

  Files to read:
    • board/contracts/telemetry.md — TelemetryRecord REQUIRED fields + session file lifecycle
      (SCENARIO_START → create file, TICK → append line, SCENARIO_END → flush + close)
    • board/contracts/scenario-events.md — SCENARIO_START.sessionId; SCENARIO_TICK all fields
    • docs/SCHEMA.md Entity 3 — TelemetryRecord + all nested types (full field list)
    • docs/ARCHITECTURE.md §1 — "%AppData%\TRISHUL-SIM\sessions\ as NDJSON"

  Target directory: Assets/Scripts/AAR/

  Steps:
    1. TelemetryRecord.cs: [Serializable] C# class matching contract.
       Nested classes: TraineeAction, KineticEvent, SensorReadings, EWJammerState.
       For SwarmAgentState[]: reuse the class from swarm-ai if already defined, or duplicate here.
       NOTE: Use Newtonsoft.Json (com.unity.nuget.newtonsoft-json — check Package Manager in Unity 2022)
       for nested array deserialization. JsonUtility doesn't handle arrays of objects reliably.
    2. TelemetryLogger.cs (MonoBehaviour, add to TraineeCockpit scene):
       Subscribe to WsClient.OnMessageReceived.
       On SCENARIO_START:
         sessionDir = Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData),
                                   "TRISHUL-SIM", "sessions")
         Directory.CreateDirectory(sessionDir)
         filePath = Path.Combine(sessionDir, sessionId + ".ndjson")
         writer = new StreamWriter(filePath, append:false, Encoding.UTF8) { AutoFlush=false }
       On SCENARIO_TICK:
         Build TelemetryRecord from TICK payload → JsonConvert.SerializeObject(record)
         writer.WriteLine(json)
         if (tickCount % 10 == 0) writer.Flush()  // flush every 1s
       On SCENARIO_END: writer.Flush(); writer.Close(); writer=null; log filePath.
    3. Stub TraineeAction input: a MonoBehaviour that reads keyboard (WASD for bearing/elevation,
       Space for kinetic fire) and exposes CurrentTraineeAction property. ui-ux replaces in P1.

DELIVERABLE:
  Assets/Scripts/AAR/TelemetryLogger.cs
  Assets/Scripts/AAR/TelemetryRecord.cs (with nested types)
  %AppData%\TRISHUL-SIM\sessions\sess-XXXXXXXX.ndjson (created at runtime)

TEST:
  Play Punjab scenario 30s → navigate to %AppData%\TRISHUL-SIM\sessions\
  File exists and grows (Explorer shows increasing Size).
  Open in Notepad: each line is valid JSON; line count after 10s ≈ 100.
  Fields: t (increasing by ~100), sessionId (matches), swarmSnapshot (array).
STATUS: [x] done
```

---

### AA-P0-B · Stub Scorecard compute + SCORECARD_PAYLOAD broadcast [timebox: 2h]
```
CONTEXT:
  AA-P0-A merged. NDJSON file writing confirmed.
  Files: board/contracts/scorecard.md (Scorecard shape, SCORECARD_PAYLOAD message, 10s timing guarantee),
  board/contracts/websocket-ipc.md (SCORECARD_PAYLOAD: Client→Server→broadcast).

  In P0: ALL Scorecard metrics are zeros/stubs. Goal: establish the plumbing (file + WS delivery).

  Steps:
    1. ScorecardComputer.cs (MonoBehaviour, same scene as TelemetryLogger):
       On SCENARIO_END: call ComputeScorecard(sessionId, scenarioId, reason, t)
       P0 stub: return Scorecard with all metrics=0.0/false; doctrinePassed=false
       timelineIndex=[]; sessionFilePath=filePath from TelemetryLogger
       Write to sessions/<sessionId>.scorecard.json (JsonConvert.SerializeObject)
       Update manifest (sessions/manifest.json):
         Read existing manifest (or create), append SessionSummary, write back.
       Broadcast: WsClient.SendAsync(SCORECARD_PAYLOAD envelope JSON)
         → NOTE: Add WsClient.SendAsync(string json) to Vinod's WsClient if not already there.
         Ask Vinod to add this method; it sends upstream (Client→Server).
         Server re-broadcasts to all clients (both screens) — no extra code needed on server side.
    2. SessionManifest.cs: simple class for manifest JSON (sessions[] array of SessionSummary).
    3. Log on both sides: "[AAREngine] Scorecard broadcast — sessionId: X"

DELIVERABLE:
  Assets/Scripts/AAR/ScorecardComputer.cs
  Assets/Scripts/AAR/SessionManifest.cs
  %AppData%\TRISHUL-SIM\sessions\<sessionId>.scorecard.json (zeros, valid JSON)
  %AppData%\TRISHUL-SIM\sessions\manifest.json (updated)

TEST:
  Run scenario to end (abort or TIME_LIMIT). Within 10s of SCENARIO_END:
  scorecard.json exists — open; valid JSON, all metrics=0.
  Instructor C2 Console: "[WsServer] SCORECARD_PAYLOAD received — routing to all clients"
  Trainee Console: "[AAREngine] SCORECARD_PAYLOAD sent"
STATUS: [x] done
```

---

### AA-P1-A · Full Decision Tree Scorer — all ScorecardMetrics from NDJSON ⭐ [timebox: 4h]
```
CONTEXT:
  AA-P0-B merged. Stub Scorecard broadcasting.
  Uday's SA-P1-C (friendlyBlackoutRisk live) + SA-P1-D (KineticEvent live) should be done.
  If not: use AarTestFixture with the SCHEMA.md example TelemetryRecord as test input.

  Files:
    • board/contracts/telemetry.md §"Derived metrics" — EXACT algorithms for all metrics:
        timeToDetectionMs: first record where threatState != "GREEN"
        roeCompliance: 1.0 if zero records have ewJammerState.friendlyBlackoutRisk==true; else 0.0
        kineticRounds: count records where traineeAction.kinetic != null
        kineticHits: count records where traineeAction.kinetic.hit == true
        prematureEwPenalty: true if any record has ewEngaged==true AND threatState=="GREEN"
    • board/contracts/scorecard.md — ScorecardMetrics + DoctrineBenchmarks thresholds:
        timeToDetectionThresholdMs=15000, roeMinCompliance=1.0, kineticMinEfficiency=0.5
    • docs/SCHEMA.md Entity 4 — Scorecard example for sanity check
    classificationAccuracy: hardcoded 0.0 in v1 (per scorecard.md — UserClassificationEvent v2)
    agentsNeutralised: count isNeutralised==true in finalSwarmSnapshot (from SCENARIO_END payload)
    agentsMissed: scenarioConfig.swarmProfile.attackerCount − agentsNeutralised
    kineticEfficiency: kineticHits / kineticRounds (0.0 if rounds==0)
    doctrinePassed: timeToDetectionMs <= threshold AND roeCompliance >= 1.0 AND kineticEfficiency >= 0.5

  Timing budget: file read + score ≤ 8s (2s reserved for WS delivery → total ≤ 10s).
  Expected: 120s scenario = 1200 NDJSON lines ≈ 120KB — reads in < 1s.

  Steps:
    1. Assets/Scripts/AAR/DecisionTreeScorer.cs (static class):
       ScorecardMetrics Score(List<TelemetryRecord>, SwarmAgentState[] finalSwarm,
                              ScenarioConfig config, DoctrineBenchmarks benchmarks)
       Single O(n) pass where possible.
    2. ScorecardComputer.cs updated:
       After SCENARIO_END: StreamReader.ReadLine() loop → JsonConvert.Deserialize per line
       → List<TelemetryRecord> → DecisionTreeScorer.Score()
       Build real Scorecard; write .scorecard.json; broadcast SCORECARD_PAYLOAD
    3. #if UNITY_EDITOR test: DecisionTreeScorer.RunUnitTests() — feed SCHEMA.md example,
       assert: timeToDetectionMs=12300, roeCompliance=1.0, kineticEfficiency=0.8

DELIVERABLE:
  Assets/Scripts/AAR/DecisionTreeScorer.cs
  ScorecardComputer.cs updated (real Scorecard)
  scorecard.json with real values after Punjab scenario

TEST:
  Run Punjab demo to completion. Scorecard JSON:
    timeToDetectionMs: positive integer (e.g. 12300)
    roeCompliance: 1.0 (no blackout events)
    kineticEfficiency: non-zero if trainee fired
    doctrinePassed: consistent with threshold comparison

PUSH IMMEDIATELY — Rajeswari (UX-P1-D) is waiting for real ScorecardMetrics.
DEMO: ⭐ "Scorecard shows non-zero metrics with pass/fail colours." (Beat 5 resolution)
STATUS: [x] done
```

---

### AA-P1-B · Timeline Scrubber data engine — timelineIndex + NDJSON seek ⭐ [timebox: 4h]
```
CONTEXT:
  AA-P1-A merged. Real Scorecard with sessionFilePath populated.
  This card provides the data API for Rajeswari's UX-P1-D (Timeline Scrubber UI).

  Files:
    • board/contracts/scorecard.md §"UI rendering contract" — "timelineIndex + sessionFilePath →
      navigable; spatial overlay required"
    • docs/SCHEMA.md Entity 4 — timelineIndex: sorted array of t values; O(n) scan once, O(log n) seek
    • board/contracts/telemetry.md — TelemetryRecord.t semantics

  Steps:
    1. Assets/Scripts/AAR/TimelineIndexer.cs (static class):
       int[] BuildIndex(string ndjsonFilePath):
         ReadLine() loop → parse t field only → return sorted int[]
         Run on background thread (Task.Run) to avoid blocking main thread
       TelemetryRecord SeekToTime(string ndjsonFilePath, int[] index, int targetT):
         Binary search index for closest t → line number i
         Read file: skip i-1 lines → deserialize line i → return TelemetryRecord
         (For ≤1200 lines this is fast enough — < 20ms)
       ScorecardComputer: call BuildIndex() after scoring; populate Scorecard.timelineIndex
       before broadcasting SCORECARD_PAYLOAD.
    2. Assets/Scripts/AAR/TimelinePlaybackController.cs (MonoBehaviour):
       void SetScrubPosition(int targetT):
         → TimelineIndexer.SeekToTime() → updates CurrentRecord property
       TelemetryRecord CurrentRecord { get; } (observable — UX-P1-D binds to this)
       Expose to Rajeswari's ScorecardPanelController via this MonoBehaviour.

DELIVERABLE:
  Assets/Scripts/AAR/TimelineIndexer.cs
  Assets/Scripts/AAR/TimelinePlaybackController.cs
  Scorecard.timelineIndex populated (≥100 entries for a 10s scenario)

TEST:
  After scenario: Scorecard JSON shows timelineIndex: [0, 100, 200, ...].
  In editor: TimelinePlaybackController.SetScrubPosition(45000) → CurrentRecord.t == 45000 (or nearest).
  Console: "Seeked to t=45000 — agentCount=6, threatState=RED"

PUSH IMMEDIATELY — Rajeswari's UX-P1-D scrub bar needs SetScrubPosition() callable.
DEMO: ⭐ "Scrub bar navigable — judge replays 'the moment he fired'." (Beat 5)
STATUS: [x] done
```

---

### AA-P2-A · PDF Scorecard export — iTextSharp Tactical Performance Dossier [timebox: 4h]
```
CONTEXT:
  AA-P1-A merged (real Scorecard). PRD MUST: PDF export one-click.
  Files: board/contracts/scorecard.md §"PDF export shape" (6-section content order).
  docs/ARCHITECTURE.md §1 — "iTextSharp (MIT) embedded in .exe".

  PDF content (in order):
    1. Header: "TRISHUL-SIM · Tactical Performance Dossier" + completedAtIso + traineeId
    2. Scenario name (English + Hindi)
    3. Doctrine verdict badge (large, coloured: #00B300 green / #CC0000 red)
    4. Metrics table: [Metric, Value, Benchmark, Pass/Fail] rows for all ScorecardMetrics
    5. Timeline sparkline: line chart of threatState (GREEN=0, AMBER=1, RED=2) vs t in seconds
       using iTextSharp graphics primitives (MoveTo/LineTo)
    6. Footer: "CONFIDENTIAL — For training use only · Air-gapped system"

  Steps:
    1. Add iTextSharp 5.5.13 (MIT) .dll to Assets/Plugins/
       Download from NuGet (itextsharp 5.5.13) → copy itextsharp.dll to Assets/Plugins/
    2. Add NotoSansDevanagari-Regular.ttf to Assets/Fonts/ (Apache 2.0, free from Google Fonts)
    3. Assets/Scripts/PDF/PdfExporter.cs (static class):
       byte[] Export(Scorecard scorecard)
       Write to sessions/<sessionId>.pdf
    4. Expose: static string WriteToFile(Scorecard sc) — writes PDF, returns path
       (Rajeswari's UX-P2-A calls this on button click)

DELIVERABLE:
  Assets/Plugins/itextsharp.dll
  Assets/Fonts/NotoSansDevanagari-Regular.ttf
  Assets/Scripts/PDF/PdfExporter.cs
  %AppData%\TRISHUL-SIM\sessions\<sessionId>.pdf

TEST:
  Run scenario. Call PdfExporter.WriteToFile(scorecard). Open PDF:
  - All 6 sections present; Hindi in section 2 renders (Devanagari, not boxes)
  - Doctrine badge coloured correctly
  - Metrics table: 5 rows with values and benchmarks
  - Timeline sparkline visible
  File size < 500 KB.
DEMO: "Judge gets a PDF in one click." (Beat 6)
STATUS: [x] done
```

---

### AA-P2-B · Warden contract fidelity + timing verification [timebox: 2h]
```
CONTEXT:
  All AAR features complete.
  Files: board/contracts/scorecard.md (10s timing guarantee, ≤500ms SCORECARD_PAYLOAD delivery),
  board/contracts/telemetry.md (TelemetryRecord REQUIRED fields).

  Steps:
    1. Run Punjab scenario 3×; measure: SCENARIO_END ts → SCORECARD_PAYLOAD ts delta (target ≤10s)
    2. Spot-check 5 NDJSON lines: all REQUIRED fields non-null
    3. Verify: SCORECARD_PAYLOAD payload contains full Scorecard (not truncated at 64KB limit)
    4. Verify: both screens receive SCORECARD_PAYLOAD
    5. Verify: manifest.json updated after each run
    6. Fix violations; log board/STATUS.md "aar-engine Warden: PASSED — <date>"

DELIVERABLE: board/STATUS.md updated · 3 timing measurements logged
TEST: 3 runs; all SCORECARD_PAYLOAD arrivals within 10s of SCENARIO_END. No SCORECARD_COMPUTE_FAILED errors.
STATUS: [ ] todo
```

---

### AA-P3-A · Partial-file guard + session manifest hardening [timebox: 1h]
```
CONTEXT:
  Feature freeze imminent. Harden against disk errors during live judging.
  Files: board/contracts/telemetry.md §"Error handling", board/contracts/scorecard.md §"Error shape".

  Steps:
    1. TelemetryLogger: wrap writer.WriteLine in try/catch IOException → set partialFlag=true; log warning; continue
    2. ScorecardComputer: if NDJSON size==0 or read fails:
       Broadcast WS ERROR {code:"SCORECARD_COMPUTE_FAILED", message:"Session file corrupt or missing.", fatal:false}
    3. SessionManifest: set partial:true for failed sessions
    4. On startup: scan manifest for sessions missing .scorecard.json; log orphaned sessions (don't re-compute)

DELIVERABLE: TelemetryLogger.cs + ScorecardComputer.cs hardened; partial flag in manifest
TEST:
  Write-protect sessions/ → run scenario → Console "IOException during telemetry write — continuing in partial mode"
  After END: SCORECARD_COMPUTE_FAILED broadcast. Remove protection → next run works normally.
STATUS: [ ] todo
```

---

## Sync expectations

- **Push AA-P0-A the same day as Vinod's US-P0-C lands** — Rani needs NDJSON shape frozen for UX-P0-A stub.
- **Push AA-P1-A immediately when done** — Rajeswari (UX-P1-D) is waiting on real ScorecardMetrics.
- **Push AA-P1-B immediately** — Rajeswari needs TimelinePlaybackController.SetScrubPosition().
- **If Uday's SA-P1-C/D are delayed:** Test scorer with AarTestFixture (hard-coded TelemetryRecord list).
- **PRs:** Tag Rajeswari as reviewer for AA-P1-A/B (she consumes your API directly).

---

## Accept

> I, Sohail, have read this brief and will push `feat/aar-engine-telemetry-logger` the same day Vinod's WS IPC + Tick loop branches land.

- [ ] **ACCEPTED**
