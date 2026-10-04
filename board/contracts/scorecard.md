# Contract: scorecard
> version: 1 · 2026-10-04
> OWNER: aar-engine (computes Scorecard; emits SCORECARD_PAYLOAD via WebSocket)
> CONSUMERS: ui-ux (renders Tactical Performance Scorecard on both screens + Timeline Scrubber) · aar-engine (PDF export) · unity-sim (routes SCORECARD_PAYLOAD to both WS endpoints)

The AAR Engine computes the Scorecard from the completed NDJSON session file within 10 seconds of `SCENARIO_END`. It then:
1. Writes `sessions/<sessionId>.scorecard.json` to disk
2. Broadcasts `SCORECARD_PAYLOAD` via WebSocket so both screens update simultaneously

---

## Scorecard  (data object + disk file `<sessionId>.scorecard.json`)

```
Scorecard {
  sessionId:          string      // "sess-20261004-143201"
  scenarioId:         string      // "punjab-fog-pincer-01"
  scenarioName:       string      // "Punjab Border — Zero Visibility Pincer"
  scenarioNameHindi:  string      // "पंजाब सीमा — शून्य दृश्यता पिंसर"
  traineeId:          string      // "havildar-rajan" (from login screen; "" if skipped)
  completedAtIso:     string      // ISO-8601 with offset "2026-10-04T14:33:01+05:30"
  durationMs:         number      // total scenario run time in ms
  doctrinePassed:     boolean     // true = ALL doctrinal thresholds met

  metrics:            ScorecardMetrics
  doctrineBenchmarks: DoctrineBenchmarks
  timelineIndex:      number[]    // sorted array of `t` values present in NDJSON (built at compute time)
  sessionFilePath:    string      // absolute local Windows path to .ndjson (for Timeline Scrubber seek)
  partial:            boolean     // true if session file was incomplete (disk write failure mid-run)
  version:            number      // schema version, currently 1
}

ScorecardMetrics {
  timeToDetectionMs:      number   // ms from t=0 to first non-GREEN threatState; 0 if never detected
  classificationAccuracy: number   // 0.0–1.0; v1: hardcoded to 0.0 (metric requires v2 UserClassificationEvent)
  roeCompliance:          number   // 1.0 = clean; 0.0 = ≥1 friendlyBlackoutRisk event
  kineticRounds:          number   // integer count of KineticEvents
  kineticHits:            number   // integer count of KineticEvents where hit == true
  kineticEfficiency:      number   // kineticHits / kineticRounds; 0.0 if kineticRounds == 0
  agentsNeutralised:      number   // count of agents where isNeutralised == true in finalSwarmSnapshot
  agentsMissed:           number   // attackerCount − agentsNeutralised (from ScenarioConfig)
  prematureEwPenalty:     boolean  // true if EW engaged while threatState == "GREEN"
}

DoctrineBenchmarks {
  timeToDetectionThresholdMs: number   // 15000 ms (doctrinal 15-second detection window)
  roeMinCompliance:           number   // 1.0 (zero ROE violations — non-negotiable)
  kineticMinEfficiency:       number   // 0.5 (≥50% hit rate)
}
```

---

## Full example (Scorecard JSON file, pretty-printed)

```json
{
  "sessionId": "sess-20261004-143201",
  "scenarioId": "punjab-fog-pincer-01",
  "scenarioName": "Punjab Border — Zero Visibility Pincer",
  "scenarioNameHindi": "पंजाब सीमा — शून्य दृश्यता पिंसर",
  "traineeId": "havildar-rajan",
  "completedAtIso": "2026-10-04T14:33:01+05:30",
  "durationMs": 55000,
  "doctrinePassed": true,
  "metrics": {
    "timeToDetectionMs": 12300,
    "classificationAccuracy": 0.0,
    "roeCompliance": 1.0,
    "kineticRounds": 5,
    "kineticHits": 4,
    "kineticEfficiency": 0.8,
    "agentsNeutralised": 4,
    "agentsMissed": 0,
    "prematureEwPenalty": false
  },
  "doctrineBenchmarks": {
    "timeToDetectionThresholdMs": 15000,
    "roeMinCompliance": 1.0,
    "kineticMinEfficiency": 0.5
  },
  "timelineIndex": [0, 100, 200, 300, 400, 500, 12300, 45200, 45300, 55000],
  "sessionFilePath": "C:\\Users\\Rajan\\AppData\\Roaming\\TRISHUL-SIM\\sessions\\sess-20261004-143201.ndjson",
  "partial": false,
  "version": 1
}
```

---

## SCORECARD_PAYLOAD WebSocket message

Sent by `aar-engine` through the WS server (Client → Server → broadcast to all connected clients).
This is the only message where the Trainee Cockpit sends to the server for redistribution.

```
SCORECARD_PAYLOAD payload {
  scorecard: Scorecard    // full Scorecard object as above
}
```

**Envelope example:**
```json
{
  "type": "SCORECARD_PAYLOAD",
  "v": 1,
  "ts": 1759600086000,
  "payload": {
    "scorecard": { "sessionId": "sess-20261004-143201", "...": "..." }
  }
}
```

**Timing guarantee:** `SCORECARD_PAYLOAD` MUST be sent within **10 seconds** of `SCENARIO_END`. This is the PRD hard requirement (Beat 5). The compute budget is ~8 s (AAR Engine file read + score math); 2 s reserved for WS transmission.

**Server routing:** On receiving `SCORECARD_PAYLOAD`, the WS server immediately re-broadcasts to all connected clients (Instructor C2 screen also updates). No acknowledgement is expected.

---

## UI rendering contract (aar-engine → ui-ux)

`ui-ux` reads the `Scorecard` object and renders the **Tactical Performance Scorecard** panel. Required display elements:

| UI element | Source field | Pass/Fail threshold |
|---|---|---|
| Time-to-Detection | `metrics.timeToDetectionMs` | ≤ `doctrineBenchmarks.timeToDetectionThresholdMs` |
| Classification Accuracy | `metrics.classificationAccuracy` | display only (v1 always 0.0, label "N/A — v2") |
| ROE Compliance | `metrics.roeCompliance` | == `doctrineBenchmarks.roeMinCompliance` (must be 1.0) |
| Kinetic Efficiency | `metrics.kineticEfficiency` | ≥ `doctrineBenchmarks.kineticMinEfficiency` |
| Agents Neutralised | `metrics.agentsNeutralised` | display only |
| Doctrine Badge | `doctrinePassed` | green "DOCTRINE PASSED" / red "DOCTRINE FAILED" |
| Timeline Scrubber | `timelineIndex` + `sessionFilePath` | navigable; spatial overlay required |
| PDF Export button | entire `Scorecard` | one-click iTextSharp PDF |

**Bilingual:** `scenarioName` vs `scenarioNameHindi` swap on language toggle. Metric labels have matching Hindi strings in the localisation CSV.

**Threat-state colour coding:** use MIL-STD-2525 palette:
- GREEN `#00B300` · AMBER `#FFB300` · RED `#CC0000`

---

## PDF export shape

The exported PDF contains (in order):
1. Header: "TRISHUL-SIM · Tactical Performance Dossier" + `completedAtIso` + `traineeId`
2. Scenario name (English + Hindi)
3. Doctrine verdict badge (large, coloured)
4. Metrics table (all `ScorecardMetrics` fields, value vs benchmark)
5. Timeline mini-chart (sparkline of `threatState` transitions from `timelineIndex`)
6. Footer: "CONFIDENTIAL — For training use only · Air-gapped system"

---

## Error shape (aar-engine → any consumer)

If Scorecard compute fails (NDJSON unreadable, division by zero, etc.), `aar-engine` broadcasts a WS `ERROR` with:
```json
{ "code": "SCORECARD_COMPUTE_FAILED", "message": "Session file corrupt or missing.", "fatal": false }
```
`ui-ux` shows "Scorecard unavailable — check session file" and keeps the Timeline Scrubber disabled.

---

## Changelog
- v1 · 2026-10-04 · initial — Orion / Atlas
  - classificationAccuracy hardcoded to 0.0 in v1 (UserClassificationEvent deferred to v2)
