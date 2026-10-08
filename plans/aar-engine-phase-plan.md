# Phase Plan — `aar-engine`
> Generated: 2026-10-04 · Owner: Orion (Architect persona)
> Track budget: ~28h usable (70% of ~40h allocated to 1 builder, 04–20 Oct 2026)
> Skeleton deadline: **09 Oct 2026 EOD** · Submission: 20 Oct 2026 23:59 IST

---

## Cross-Track Dependency DAG

```
unity-sim-P0-A  ──►  AA-P0-A   (WS IPC live; AAR subscribes to SCENARIO_START / TICK / END)
unity-sim-P0-C  ──►  AA-P0-A   (SCENARIO_TICK + SCENARIO_END broadcast; AAR logs on TICK)
SA-P0-A         ──►  AA-P0-A   (SwarmAgentState in swarmSnapshot — AAR can log real data)
AA-P0-A         ──►  UX-P0-A   (NDJSON file shape frozen; UI can build Timeline Scrubber stub)
AA-P0-B         ──►  UX-P0-A   (SCORECARD_PAYLOAD shape frozen; UI Scorecard stub can render)
SA-P1-C         ──►  AA-P1-A   (friendlyBlackoutRisk live → roeCompliance can be real)
SA-P1-D         ──►  AA-P1-A   (KineticEvent live → kineticRounds/hits/efficiency can be real)
AA-P1-A         ──►  UX-P1-D   (real ScorecardMetrics; Scorecard Renderer shows non-zero values)
AA-P1-B         ──►  UX-P1-D   (timelineIndex built; Timeline Scrubber navigable)
AA-P2-A         ──►  UX-P2-A   (PDF export; UI provides the "Export PDF" button)

Mock path (P0/P1 isolation):
  • AA-P0-A and AA-P0-B use a SwarmAgentState fixture (SwarmMock from swarm-ai P3-B or
    a local inline fixture in AarTestFixture.cs) if swarm-ai P0 is not yet complete.
  • ui-ux builds Scorecard Renderer against the example Scorecard JSON from scorecard.md
    (hard-coded in a ScorecardMock.cs) until AA-P1-A produces real values.
  • Mock file: Assets/Scripts/Mocks/AarMock.cs — static Scorecard GetPunjabFixture()
```

**File surface audit — no overlaps:**
- `aar-engine` owns: `Assets/Scripts/AAR/` (TelemetryLogger, DecisionTreeScorer, ScorecardComputer, TimelineIndexer), `Assets/Scripts/PDF/` (PdfExporter), `%AppData%/TRISHUL-SIM/sessions/` at runtime
- Does NOT touch: `Assets/Scripts/Network/` (reads WS events via event subscription only), `Assets/Scripts/SwarmAI/`, `Assets/UI/`
- Shared read: `Assets/Scripts/Scenarios/ScenarioConfig.cs` (read-only; unity-sim owns it)

---

## Load Ledger

| Phase | Cards | Timebox sum | Cumulative |
|---|---|---|---|
| P0 Skeleton | AA-P0-A, AA-P0-B | 3h + 2h = **5h** | 5h |
| P1 Core | AA-P1-A, AA-P1-B | 4h + 4h = **8h** | 13h |
| P2 Polish | AA-P2-A, AA-P2-B | 4h + 2h = **6h** | 19h |
| P3 Endgame | AA-P3-A | 1h | **20h** |

**20h of 28h usable = 71% cap. ✓** (at ceiling — 1 builder, 8h buffer for cross-track debug)

---

## P0 · Skeleton (04–09 Oct 2026)

> Gate: NDJSON log file grows during scenario · Stub Scorecard broadcast on SCENARIO_END

---

### AA-P0-A · TelemetryLogger — 10 Hz NDJSON append on SCENARIO_TICK ⭐ [timebox: 3h]

```
CONTEXT:
  Prerequisites: unity-sim P0-A (WsClient exists), unity-sim P0-C (SCENARIO_TICK + SCENARIO_END
  broadcast). SA-P0-A SHOULD be done (swarmSnapshot has real data), but if not, TelemetryLogger
  still works — swarmSnapshot will be an empty array in the payload, and we log it as-is.
  
  Files to read before starting:
    • board/contracts/telemetry.md — TelemetryRecord REQUIRED fields; session file lifecycle
      (SCENARIO_START → create file, TICK → append line, SCENARIO_END → flush + close)
    • board/contracts/scenario-events.md — SCENARIO_START.sessionId; SCENARIO_TICK.t,
      swarmSnapshot, sensorReadings, threatState, friendlyVhfActive, ewJammerState, traineeAction
    • docs/SCHEMA.md Entity 3 — TelemetryRecord, all nested types (full field list)
    • docs/ARCHITECTURE.md §1 — "telemetry written to %AppData%\TRISHUL-SIM\sessions\ as NDJSON"
  
  Repo state: Unity project exists. WsClient.cs raises OnMessageReceived(JsonEnvelope).
  No AAR code exists. Target directory: Assets/Scripts/AAR/
  
  Steps:
    1. Assets/Scripts/AAR/TelemetryLogger.cs — MonoBehaviour (add to TraineeCockpit scene):
       - Subscribe to WsClient.OnMessageReceived
       - On SCENARIO_START:
         sessionDir = Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData),
                                    "TRISHUL-SIM", "sessions")
         Directory.CreateDirectory(sessionDir)
         filePath = Path.Combine(sessionDir, sessionId + ".ndjson")
         writer = new StreamWriter(filePath, append:false, Encoding.UTF8)
         writer.AutoFlush = false  // batch flush for performance
       - On SCENARIO_TICK: deserialize payload → build TelemetryRecord → JsonUtility.ToJson()
         → writer.WriteLine(json)  → write count modulo 10 == 0 → writer.Flush()
         (flush every 1 s = 10 ticks; keep disk write overhead low)
       - On SCENARIO_END: writer.Flush(); writer.Close(); writer = null; Log file path
    2. TelemetryRecord.cs — C# class matching contract (use [Serializable] for JsonUtility;
       nested classes: TraineeAction, KineticEvent, SensorReadings, EWJammerState, SwarmAgentState)
       NOTE: If swarmSnapshot comes as raw JSON array from WS, deserialize with Newtonsoft.Json
       (Newtonsoft is included in Unity 2022 as com.unity.nuget.newtonsoft-json — check Package Manager)
    3. Add a minimal stub in TraineeCockpit scene: a MonoBehaviour that reads
       Input.GetKey(KeyCode.Alpha1..4) for antenna bearing changes and builds TraineeAction
       each tick (ui-ux will replace this with UITK controls in P1)

DELIVERABLE:
  Assets/Scripts/AAR/TelemetryLogger.cs
  Assets/Scripts/AAR/TelemetryRecord.cs  (with all nested types)
  %AppData%\TRISHUL-SIM\sessions\sess-XXXXXXXX.ndjson  (created at runtime)

TEST:
  Play Punjab scenario in editor → navigate to %AppData%\TRISHUL-SIM\sessions\
  File sess-XXXXXXXX.ndjson exists and is growing (visible in Explorer "Size" column updating).
  Open file in Notepad: each line is valid JSON matching TelemetryRecord shape.
  Line count after 10 s ≈ 100 lines.

DEPENDS: unity-sim P0-A, P0-C     UNBLOCKS: AA-P0-B, UX-P0-A (NDJSON shape frozen)

DEMO: "Session file visible in Explorer growing in real time." (P0 checklist item #5)
PERSONA: aar-engine builder        STATUS: [x] done
```

---

### AA-P0-B · Stub Scorecard compute + SCORECARD_PAYLOAD broadcast [timebox: 2h]

```
CONTEXT:
  AA-P0-A complete — NDJSON file exists. SCENARIO_END received triggers this card's logic.
  
  Files to read before starting:
    • board/contracts/scorecard.md — Scorecard shape; SCORECARD_PAYLOAD message definition;
      10-second timing guarantee (PRD hard requirement); UI rendering contract (display elements)
    • board/contracts/websocket-ipc.md — SCORECARD_PAYLOAD envelope (Client → Server → broadcast)
    • docs/SCHEMA.md Entity 4 — Scorecard JSON example
  
  In P0, Scorecard metrics are ALL ZEROS/stubs (real scoring in AA-P1-A).
  The goal is to establish the plumbing: file written, WS payload sent, both screens update.
  
  Steps:
    1. Assets/Scripts/AAR/ScorecardComputer.cs — MonoBehaviour:
       - On SCENARIO_END received: call ComputeScorecard(sessionId, scenarioId, reason, t)
       - P0 stub: return a Scorecard with metrics all 0.0 / false; doctrinePassed = false
         timelineIndex = [] ; sessionFilePath = filePath from TelemetryLogger
       - Write Scorecard JSON to sessions/<sessionId>.scorecard.json
       - Update manifest: sessions/manifest.json (append SessionSummary entry)
         (If manifest.json exists: read, append; else create fresh)
       - Broadcast via WsClient: send SCORECARD_PAYLOAD envelope via WsClient.SendAsync()
         NOTE: WsClient only connects to WsServer — it cannot send to server by default.
         Add WsClient.SendAsync(string json) method that sends upstream (Client → Server).
         Server routes SCORECARD_PAYLOAD to all connected clients (including C2 screen).
    2. SessionManifest.cs — simple class: sessions[] append on each run
    3. Log on both sides: "[AAREngine] Scorecard broadcast — sessionId: X, metrics: stub"

DELIVERABLE:
  Assets/Scripts/AAR/ScorecardComputer.cs
  Assets/Scripts/AAR/SessionManifest.cs
  %AppData%\TRISHUL-SIM\sessions\<sessionId>.scorecard.json  (zeros; valid JSON)
  %AppData%\TRISHUL-SIM\sessions\manifest.json  (updated)
  WsClient.cs updated with SendAsync()

TEST:
  Run scenario to end (abort or TIME_LIMIT). Within 10 s of SCENARIO_END:
  - scorecard.json exists in sessions/ — open; verify valid JSON, all metrics = 0
  - Unity Console (Instructor C2): "[WsServer] SCORECARD_PAYLOAD received — routing to all clients"
  - Unity Console (Trainee): "[AAREngine] SCORECARD_PAYLOAD sent"

DEPENDS: AA-P0-A, unity-sim P0-C     UNBLOCKS: UX-P0-A (SCORECARD_PAYLOAD shape frozen)

DEMO: "'Scorecard received' log line on both screens." (P0 checklist item #6)
PERSONA: aar-engine builder           STATUS: [x] done
```

---

## P1 · Core (09–16 Oct 2026)

> Gate: Real ScorecardMetrics with pass/fail colours · Timeline Scrubber navigable

---

### AA-P1-A · Full Decision Tree Scorer — all ScorecardMetrics from NDJSON ⭐ [timebox: 4h]

```
CONTEXT:
  AA-P0-B complete — stub Scorecard is broadcasting.
  SA-P1-C (friendlyBlackoutRisk live) and SA-P1-D (KineticEvent live) should be complete.
  If swarm-ai P1 is not yet done, use AarMock (see mock path in DAG header) to test scorer.
  
  Files to read before starting:
    • board/contracts/telemetry.md §"Derived metrics" — exact algorithms for all metrics:
      timeToDetectionMs: first record where threatState != "GREEN"
      roeCompliance: 1.0 if zero records have ewJammerState.friendlyBlackoutRisk == true; else 0.0
      kineticRounds: count records where traineeAction.kinetic != null
      kineticHits: count records where traineeAction.kinetic.hit == true
      prematureEwPenalty: true if any record has ewEngaged == true AND threatState == "GREEN"
    • board/contracts/scorecard.md — ScorecardMetrics field names + DoctrineBenchmarks thresholds
      (timeToDetectionThresholdMs=15000, roeMinCompliance=1.0, kineticMinEfficiency=0.5)
    • docs/SCHEMA.md Entity 4 — Scorecard.metrics example values for sanity-check
  
  classificationAccuracy: hardcoded to 0.0 in v1 per scorecard.md contract (UserClassificationEvent deferred to v2)
  agentsNeutralised: count agents where isNeutralised == true in finalSwarmSnapshot (from SCENARIO_END payload)
  agentsMissed: scenarioConfig.swarmProfile.attackerCount − agentsNeutralised
  kineticEfficiency: kineticHits / kineticRounds (0.0 if kineticRounds == 0)
  doctrinePassed: timeToDetectionMs <= threshold AND roeCompliance >= 1.0 AND kineticEfficiency >= 0.5
  
  Steps:
    1. Assets/Scripts/AAR/DecisionTreeScorer.cs — static class:
       - ScorecardMetrics Score(List<TelemetryRecord> records, SwarmAgentState[] finalSwarm,
                                ScenarioConfig config, DoctrineBenchmarks benchmarks)
       - Implements all algorithms above (sequential scan — O(n) single pass where possible)
    2. ScorecardComputer.cs updated:
       - After SCENARIO_END: read NDJSON file back into List<TelemetryRecord>
         (StreamReader.ReadLine() loop, JsonUtility.FromJson<TelemetryRecord> per line)
       - Pass to DecisionTreeScorer.Score()
       - Build real Scorecard, write to disk, broadcast SCORECARD_PAYLOAD
    3. Timing budget: file read + score must complete in ≤ 8 s (2 s reserved for WS delivery)
       Expected: 120 s scenario = 1200 NDJSON lines ≈ 120 KB — reads in < 1 s.
    4. Add a #if UNITY_EDITOR test method: DecisionTreeScorer.RunUnitTests() — feed the
       SCHEMA.md example TelemetryRecord, assert expected metric values

DELIVERABLE:
  Assets/Scripts/AAR/DecisionTreeScorer.cs
  ScorecardComputer.cs updated (real Scorecard, all fields populated)
  %AppData%\TRISHUL-SIM\sessions\<sessionId>.scorecard.json with real metric values

TEST:
  Run Punjab demo to completion. Scorecard JSON:
    timeToDetectionMs: a positive integer (e.g. 12300)
    roeCompliance: 1.0 (if no friendly blackout) or 0.0 (if blackout occurred)
    kineticEfficiency: kineticHits / kineticRounds (non-zero if trainee fired)
    doctrinePassed: true or false (consistent with threshold comparison)
  All values match the SCHEMA.md example when using the same scenario inputs.

DEPENDS: AA-P0-A, AA-P0-B, SA-P1-C, SA-P1-D     UNBLOCKS: UX-P1-D (Scorecard Renderer shows real values)

DEMO: ⭐ "Scorecard shows non-zero metrics with pass/fail colours." (Beat 5 — wow-moment resolution)
PERSONA: aar-engine builder                       STATUS: [ ] todo
```

---

### AA-P1-B · Timeline Scrubber — timelineIndex + NDJSON seek ⭐ [timebox: 4h]

```
CONTEXT:
  AA-P1-A complete — real Scorecard with sessionFilePath populated.
  This card builds the data engine for the Timeline Scrubber UI.
  
  Files to read before starting:
    • board/contracts/scorecard.md §"UI rendering contract" — Timeline Scrubber requirements:
      "timelineIndex + sessionFilePath → navigable; spatial overlay required"
    • docs/SCHEMA.md Entity 4 — timelineIndex: sorted array of t values; sessionFilePath: absolute path
    • docs/SCHEMA.md §"Index" note — "build in-memory index on t at Scorecard load time (O(n) scan once,
      then O(log n) seek)"
    • board/contracts/telemetry.md — TelemetryRecord.t field semantics
  
  The Timeline Scrubber is a ui-ux card (UX-P1-D). This card provides the data API it calls.
  
  Steps:
    1. Assets/Scripts/AAR/TimelineIndexer.cs — static class:
       - int[] BuildIndex(string ndjsonFilePath):
         Read all lines → parse t field only → return sorted int[] of all t values
         Build on a background thread (Thread / Task.Run) to avoid blocking main thread
       - TelemetryRecord SeekToTime(string ndjsonFilePath, int[] index, int targetT):
         Binary search index for closest t → seek file to that line → read and deserialize
         NOTE: NDJSON is not seekable by byte offset easily. Use the index as a line-number index:
         index[i] → line number i. Seek = read and skip i-1 lines from file start.
         (For 1200 lines max this is fast enough — < 20 ms.)
       - ScorecardComputer.ComputeScorecard(): after scoring, call BuildIndex() asynchronously
         and populate Scorecard.timelineIndex before broadcasting SCORECARD_PAYLOAD
    2. Assets/Scripts/AAR/TimelinePlaybackController.cs — MonoBehaviour:
       - Expose: SetScrubPosition(int targetT) → calls TimelineIndexer.SeekToTime()
         → returns TelemetryRecord at that timestamp
       - Expose: CurrentRecord property (observable; ui-ux binds to it)
       - ui-ux's scrub bar calls SetScrubPosition() on slider value change
    3. The spatial overlay (drone positions at scrub time) is a ui-ux responsibility —
       this card just surfaces the TelemetryRecord.swarmSnapshot[] at the scrub position

DELIVERABLE:
  Assets/Scripts/AAR/TimelineIndexer.cs
  Assets/Scripts/AAR/TimelinePlaybackController.cs
  Scorecard.timelineIndex populated in real Scorecard JSON (≥100 entries for a 10s scenario)

TEST:
  After scenario ends, Scorecard JSON shows timelineIndex: [0, 100, 200, ...] (one entry per tick).
  In editor: TimelinePlaybackController.SetScrubPosition(45000) → CurrentRecord.t == 45000 (or closest).
  Console log: "Seeked to t=45000 — agentCount=6, threatState=RED" (or similar).

DEPENDS: AA-P1-A               UNBLOCKS: UX-P1-D (scrub bar now has real data API to call)

DEMO: ⭐ "Scrub bar navigable — judge can replay the exact moment of firing." (Beat 5)
PERSONA: aar-engine builder   STATUS: [ ] todo
```

---

## P2 · Polish (16–19 Oct 2026)

---

### AA-P2-A · PDF Scorecard export — iTextSharp Tactical Performance Dossier [timebox: 4h]

```
CONTEXT:
  AA-P1-A complete — real Scorecard with all metrics.
  PRD MUST item: PDF export one-click. iTextSharp (MIT) embedded in .exe.
  
  Files to read before starting:
    • board/contracts/scorecard.md §"PDF export shape" — ordered content list:
      1. Header: "TRISHUL-SIM · Tactical Performance Dossier" + completedAtIso + traineeId
      2. Scenario name (English + Hindi)
      3. Doctrine verdict badge (large, coloured)
      4. Metrics table (all ScorecardMetrics fields, value vs benchmark)
      5. Timeline mini-chart (sparkline of threatState transitions)
      6. Footer: "CONFIDENTIAL — For training use only · Air-gapped system"
    • docs/ARCHITECTURE.md §1 — "iTextSharp (MIT) embedded in .exe; compiles to Windows standalone"
  
  Steps:
    1. Unity Package Manager → Add package from git URL or .dll:
       iTextSharp 5.5.13 (MIT) — add iTextSharp.dll to Assets/Plugins/
       Alternatively: use nuget directly; copy itext5 DLLs to Plugins/
    2. Assets/Scripts/PDF/PdfExporter.cs — static class:
       - byte[] Export(Scorecard scorecard)
       - Build iTextSharp Document:
         • Page: A4
         • Header table: PdfPTable(2) — title cell + completedAtIso cell
         • traineeId row
         • Scenario name: Font size 14 bold; Hindi name below in size 11
         • Doctrine badge: colored rectangle (green/red) + "DOCTRINE PASSED / FAILED" text
         • Metrics table: PdfPTable(4) — [Metric, Value, Benchmark, Pass/Fail] rows for:
           Time-to-Detection, ROE Compliance, Kinetic Efficiency, Agents Neutralised,
           Premature EW Penalty
         • Timeline sparkline: iterate timelineIndex + NDJSON (only t and threatState needed);
           draw a simple line chart using iTextSharp graphics primitives (LineTo/MoveTo)
           (GREEN=0, AMBER=1, RED=2; Y-axis maps threat level; X-axis is t in seconds)
         • Footer: Phrase("CONFIDENTIAL — For training use only · Air-gapped system")
       - Return PDF as byte[]
    3. Write byte[] to %AppData%\TRISHUL-SIM\sessions\<sessionId>.pdf
       + a button in ScorecardComputer (or expose method ExportPdf() for ui-ux to call)
    4. Hindi text: iTextSharp supports Unicode fonts — embed NotoSansDevanagari-Regular.ttf
       from Assets/Fonts/ (free, Apache 2.0 license — add to project)

DELIVERABLE:
  Assets/Plugins/itextsharp.dll  (iTextSharp 5.5.13)
  Assets/Fonts/NotoSansDevanagari-Regular.ttf
  Assets/Scripts/PDF/PdfExporter.cs
  %AppData%\TRISHUL-SIM\sessions\<sessionId>.pdf  (generated at runtime)

TEST:
  Run scenario to end. Call PdfExporter.Export(scorecard). Open generated PDF:
  - Title, traineeId, scenario name (English + Hindi) visible
  - Doctrine badge: red (stub metrics fail thresholds) or green
  - Metrics table: 5 rows with values and benchmarks
  - Timeline sparkline: visible line chart
  - Footer text present
  File size < 500 KB.

DEPENDS: AA-P1-A               UNBLOCKS: UX-P2-A (PDF export button becomes functional)

DEMO: "Judge gets a PDF in one click — one-click iTextSharp export." (Beat 6)
PERSONA: aar-engine builder   STATUS: [ ] todo
```

---

### AA-P2-B · Warden review — contract fidelity + timing verification [timebox: 2h]

```
CONTEXT:
  All AAR features complete. This card runs the contract fidelity check for aar-engine.
  
  Files to read before starting:
    • board/contracts/scorecard.md — 10-second timing guarantee; SCORECARD_PAYLOAD routing
    • board/contracts/telemetry.md — TelemetryRecord REQUIRED fields; session file lifecycle
    • board/contracts/websocket-ipc.md — SCORECARD_PAYLOAD delivery budget: ≤500 ms after
      AAR engine writes Scorecard; overall budget ≤10s from SCENARIO_END
  
  Steps:
    1. Run Punjab scenario 3×; measure: time from SCENARIO_END ts field to SCORECARD_PAYLOAD ts field
       Target: ≤10 s (ideally ≤8 s compute + 2 s WS delivery)
    2. Verify: all TelemetryRecord required fields are non-null in NDJSON (spot-check 5 random lines)
    3. Verify: SCORECARD_PAYLOAD payload contains full Scorecard object (not truncated)
    4. Verify: both screens receive SCORECARD_PAYLOAD (Instructor C2 Console + Trainee Console)
    5. Verify: sessions/manifest.json updated after each run
    6. Fix any violations; log in board/STATUS.md: "aar-engine Warden: PASSED — <date>"

DELIVERABLE:
  board/STATUS.md updated (AAR Warden pass)
  3 timing measurements logged

TEST:
  3 consecutive runs; all SCORECARD_PAYLOAD arrivals within 10 s of SCENARIO_END.
  No "SCORECARD_COMPUTE_FAILED" ERROR messages in Console.

DEPENDS: AA-P2-A, AA-P1-A, AA-P1-B     UNBLOCKS: unity-sim P2-D (Warden coordination)

DEMO: "Scorecard renders in ≤10 s — the PRD hard requirement." (Beat 5)
PERSONA: aar-engine builder             STATUS: [ ] todo
```

---

## P3 · Endgame (19–20 Oct 2026)

---

### AA-P3-A · Session manifest clean-up + partial-file guard [timebox: 1h]

```
CONTEXT:
  Feature freeze imminent. This card hardens the AAR engine against demo-day failures:
  disk full, unexpected crash mid-scenario.
  
  Files to read:
    • board/contracts/telemetry.md — error handling: "partial files flagged with partial:true"
    • board/contracts/scorecard.md — error shape: SCORECARD_COMPUTE_FAILED
  
  Steps:
    1. TelemetryLogger: wrap writer.WriteLine in try/catch; on IOException → set partialFlag=true,
       log warning, continue scenario (don't abort)
    2. ScorecardComputer: if NDJSON file size == 0 or read fails:
       Broadcast WS ERROR {code:"SCORECARD_COMPUTE_FAILED", message:"Session file corrupt or missing.", fatal:false}
    3. SessionManifest: set partial:true for this session entry
    4. On startup: scan sessions/manifest.json for any sessions missing .scorecard.json;
       log orphaned sessions to Console (don't re-compute — too risky mid-demo)
    5. Verify %AppData%\TRISHUL-SIM\sessions\ directory creation is idempotent
       (already Directory.CreateDirectory — confirm no race condition)

DELIVERABLE:
  TelemetryLogger.cs, ScorecardComputer.cs hardened
  SessionManifest: partial flag support

TEST:
  Simulate disk failure: temporarily write-protect sessions/ directory → run scenario.
  Console shows: "IOException during telemetry write — continuing in partial mode."
  After END: SCORECARD_COMPUTE_FAILED broadcast (or graceful partial Scorecard).
  Remove write-protection → next run works normally.

DEPENDS: AA-P2-B     UNBLOCKS: —

DEMO: "Demo-day insurance — no crash on disk error during live judging." (internal)
PERSONA: aar-engine builder     STATUS: [ ] todo
```

---

## Oracle Pass — Wow Moment Analysis

| Card | Demo beat | Oracle verdict |
|---|---|---|
| AA-P0-A | P0 checklist #5 (NDJSON grows) | ⭐ **never-cut** — no log = no Scorecard = no Beat 5 |
| AA-P0-B | P0 checklist #6 (Scorecard received) | ⭐ **never-cut** — P0 gate item; both screens need this |
| AA-P1-A | Beat 5 ⭐ (real metrics, pass/fail) | ⭐ **never-cut** — this IS the "10-second Scorecard" story |
| AA-P1-B | Beat 5 (Timeline Scrubber navigable) | ⭐ **never-cut** — judge asked "show me the moment he fired" |
| AA-P2-A | Beat 6 (PDF export one-click) | never-cut — PRD MUST item; MoD expects dossier |
| AA-P2-B | Warden gate | procedural — 2h is cheap; catches regressions |
| AA-P3-A | Demo-day resilience | high-value 1h — never cut for 1h payoff |

**P3 cut order (if endgame is tight):** AA-P3-A (cut — live dangerously for 1h) → AA-P2-B (compress to 30 min smoke) → AA-P2-A must NOT be cut (PDF is a MUST)
