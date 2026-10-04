# SCHEMA — TRISHUL-SIM
> Blueprint date: 2026-10-04 · Owner: Atlas / Orion · Store: local filesystem NDJSON + in-memory Unity C# objects
> PRD vocabulary is verbatim. No database server. No migrations. Backward compat by field-addition only.

---

## Storage topology

| Location | Format | Written by | Read by |
|---|---|---|---|
| `%AppData%\TRISHUL-SIM\sessions\<session-id>.ndjson` | Newline-delimited JSON | `aar-engine` | `aar-engine` (Scorecard), `ui-ux` (Renderer), PDF exporter |
| `%AppData%\TRISHUL-SIM\scenarios\*.json` | JSON | shipped in installer | `unity-sim` (Scenario Library) |
| In-memory (Unity C# objects) | C# structs/classes | all tracks | all tracks (via contracts) |
| WebSocket wire | JSON strings | `unity-sim` (WS server/client) | all tracks (via contracts) |

---

## Entity 1 · Scenario

Owned by: `unity-sim`. Loaded from JSON file in Scenario Library.

```
ScenarioConfig {
  id:              string          // "punjab-fog-pincer-01"
  name:            string          // "Punjab Border — Zero Visibility Pincer"
  nameHindi:       string          // "पंजाब सीमा — शून्य दृश्यता पिंसर"
  region:          string          // "PUNJAB_RIVERINE" | "LADAKH_LAC" | "LOC_MOUNTAIN" | "DESERT_FOB"
  weatherPreset:   WeatherPreset   // see type below
  swarmProfile:    SwarmProfile    // see type below
  durationMaxSecs: number          // 120
  seed:            number          // procedural RNG seed — changes each run
  version:         number          // 1
}

WeatherPreset {
  visibility:      number          // metres, e.g. 15.0 (fog)
  windSpeedKmh:    number          // 12.0
  windBearingDeg:  number          // 270.0
  temperature:     number          // Celsius, affects acoustic attenuation
  isNight:         boolean         // true
}

SwarmProfile {
  agentCount:      number          // 6
  decoyCount:      number          // 2
  attackerCount:   number          // 4
  tacticTree:      string          // "PINCER_LOW_FAST" | "SATURATION_DIVE" | "MULTI-AXIS_SPLIT"
  speedKmh:        number          // 120.0
  altitudeM:       number          // 8.0
  rfFreqGHz:       number[]        // [5.825, 2.4]
  rfPowerDbm:      number          // 23.0
}
```

**Example doc:**
```json
{
  "id": "punjab-fog-pincer-01",
  "name": "Punjab Border — Zero Visibility Pincer",
  "nameHindi": "पंजाब सीमा — शून्य दृश्यता पिंसर",
  "region": "PUNJAB_RIVERINE",
  "weatherPreset": { "visibility": 15.0, "windSpeedKmh": 12.0, "windBearingDeg": 270.0, "temperature": 4.0, "isNight": true },
  "swarmProfile": { "agentCount": 6, "decoyCount": 2, "attackerCount": 4, "tacticTree": "PINCER_LOW_FAST", "speedKmh": 120.0, "altitudeM": 8.0, "rfFreqGHz": [5.825, 2.4], "rfPowerDbm": 23.0 },
  "durationMaxSecs": 120,
  "seed": 42819,
  "version": 1
}
```

**Demo query:** Load all scenario configs matching `region == "PUNJAB_RIVERINE"` at startup → populate Scenario Library dropdown.

---

## Entity 2 · SwarmAgent (in-memory, per-tick)

Owned by: `swarm-ai`. Passed via contract `SwarmAgentState` to sensor suite each tick.

```
SwarmAgentState {
  agentId:         string          // "agent-0"
  isDecoy:         boolean         // false
  positionWorld:   Vector3         // Unity world coords {x, y, z} metres
  velocityMs:      Vector3         // {x, y, z} m/s
  headingDeg:      number          // 247.0
  altitudeM:       number          // 8.3
  rfEmitting:      boolean         // true (decoys emit; real attackers may go silent)
  rfFreqGHz:       number          // 5.825
  rfPowerDbm:      number          // 23.0
  propellerRpm:    number          // 11400   (drives acoustic + Doppler model)
  rcs:             number          // m² radar cross-section, e.g. 0.012 (small FPV)
  isNeutralised:   boolean         // false
  neutralisedAtMs: number | null   // scenario-elapsed ms
}
```

**Example doc:**
```json
{
  "agentId": "agent-3",
  "isDecoy": false,
  "positionWorld": {"x": 142.3, "y": 8.3, "z": -87.1},
  "velocityMs": {"x": -28.4, "y": -0.2, "z": 12.1},
  "headingDeg": 247.0,
  "altitudeM": 8.3,
  "rfEmitting": true,
  "rfFreqGHz": 5.825,
  "rfPowerDbm": 23.0,
  "propellerRpm": 11400,
  "rcs": 0.012,
  "isNeutralised": false,
  "neutralisedAtMs": null
}
```

---

## Entity 3 · TelemetryRecord (10 Hz NDJSON log entry)

Owned by: `aar-engine`. One line per 100 ms tick. Written to `<session-id>.ndjson`.

```
TelemetryRecord {
  t:               number          // scenario-elapsed milliseconds, e.g. 12300
  sessionId:       string          // "sess-20261004-143201"
  scenarioId:      string          // "punjab-fog-pincer-01"
  traineeAction:   TraineeAction   // see type below
  swarmSnapshot:   SwarmAgentState[] // full swarm state at this tick
  sensorReadings:  SensorReadings  // see type below
  threatState:     string          // "GREEN" | "AMBER" | "RED"
  friendlyVhfActive: boolean       // for ROE deconfliction check
  ewJammerState:   EWJammerState   // see type below
}

TraineeAction {
  antennaBearingDeg: number        // 247.0
  antennaElevationDeg: number      // -5.0
  ewEngaged:       boolean         // false
  ewBandGHz:       number | null   // null
  kinetic:         KineticEvent | null
}

KineticEvent {
  firedAtMs:       number          // 45210
  targetAgentId:   string          // "agent-3"
  hit:             boolean         // true
  leadAngleDeg:    number          // 3.2
}

SensorReadings {
  rfWaterfallPeakGHz:   number     // 5.825
  rfWaterfallPeakDbm:   number     // -62.3
  dopplerPeakHz:        number     // 220.4   (blade-tip harmonic)
  acousticSplDb:        number     // 54.2    (acoustic sound pressure level)
  radarDetectedCount:   number     // 4
}

EWJammerState {
  engaged:         boolean         // true
  bearingDeg:      number          // 247.0
  elevationDeg:    number          // -5.0
  bandGHz:         number          // 5.825
  friendlyBlackoutRisk: boolean    // false
}
```

**Example doc (one NDJSON line):**
```json
{"t":45200,"sessionId":"sess-20261004-143201","scenarioId":"punjab-fog-pincer-01","traineeAction":{"antennaBearingDeg":247.0,"antennaElevationDeg":-5.0,"ewEngaged":true,"ewBandGHz":5.825,"kinetic":null},"swarmSnapshot":[{"agentId":"agent-3","isDecoy":false,"positionWorld":{"x":142.3,"y":8.3,"z":-87.1},"velocityMs":{"x":-28.4,"y":-0.2,"z":12.1},"headingDeg":247.0,"altitudeM":8.3,"rfEmitting":true,"rfFreqGHz":5.825,"rfPowerDbm":23.0,"propellerRpm":11400,"rcs":0.012,"isNeutralised":false,"neutralisedAtMs":null}],"sensorReadings":{"rfWaterfallPeakGHz":5.825,"rfWaterfallPeakDbm":-62.3,"dopplerPeakHz":220.4,"acousticSplDb":54.2,"radarDetectedCount":4},"threatState":"RED","friendlyVhfActive":false,"ewJammerState":{"engaged":true,"bearingDeg":247.0,"elevationDeg":-5.0,"bandGHz":5.825,"friendlyBlackoutRisk":false}}
```

**Demo queries:**
- "Time-to-Detection" → first `TelemetryRecord` where `threatState != "GREEN"`, `t` field
- "First kinetic event" → first record where `traineeAction.kinetic != null`
- "ROE violation check" → any record where `ewJammerState.friendlyBlackoutRisk == true`
- "Replay scrub to T+45s" → seek NDJSON to lines where `t >= 45000 && t <= 46000`

**Index:** NDJSON is sequential; Timeline Scrubber builds an in-memory index on `t` at Scorecard load time (O(n) scan once, then O(log n) seek).

---

## Entity 4 · Scorecard

Owned by: `aar-engine`. Computed from full NDJSON session after SCENARIO_END.

```
Scorecard {
  sessionId:            string      // "sess-20261004-143201"
  scenarioId:           string      // "punjab-fog-pincer-01"
  traineeId:            string      // "havildar-rajan" (entered at login; optional)
  completedAtIso:       string      // ISO-8601 "2026-10-04T14:33:01+05:30"
  durationMs:           number      // total scenario elapsed ms
  doctrinePassed:       boolean     // true = all doctrinal thresholds met

  metrics: {
    timeToDetectionMs:      number  // ms from scenario start to first RED/AMBER
    classificationAccuracy: number  // 0.0–1.0 (correct agent type IDs / total)
    roeCompliance:          number  // 1.0 = zero friendly VHF blackout events
    kineticRounds:          number  // total rounds fired
    kineticHits:            number  // rounds that hit a valid target
    kineticEfficiency:      number  // hits / rounds, 0.0–1.0
    agentsNeutralised:      number  // count
    agentsMissed:           number  // attackers that reached exclusion zone
    prematureEwPenalty:     boolean // EW engaged before threat was AMBER
  }

  doctrineBenchmarks: {
    timeToDetectionThresholdMs: number   // 15000 (doctrinal 15s)
    roeMinCompliance:           number   // 1.0
    kineticMinEfficiency:       number   // 0.5
  }

  timelineIndex:        number[]    // sorted array of `t` values in session file (for scrubber)
  sessionFilePath:      string      // absolute local path to .ndjson
}
```

**Example doc:**
```json
{
  "sessionId": "sess-20261004-143201",
  "scenarioId": "punjab-fog-pincer-01",
  "traineeId": "havildar-rajan",
  "completedAtIso": "2026-10-04T14:33:01+05:30",
  "durationMs": 55000,
  "doctrinePassed": true,
  "metrics": {
    "timeToDetectionMs": 12300,
    "classificationAccuracy": 0.83,
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
  "timelineIndex": [0, 100, 200, 300],
  "sessionFilePath": "C:\\Users\\Rajan\\AppData\\Roaming\\TRISHUL-SIM\\sessions\\sess-20261004-143201.ndjson"
}
```

---

## Entity 5 · SessionManifest (one per session directory)

Lightweight index file `sessions\manifest.json` — enables Scenario Library replay and Campaign Mode scaffold.

```
SessionManifest {
  sessions: SessionSummary[]
}

SessionSummary {
  sessionId:       string
  scenarioId:      string
  completedAtIso:  string
  doctrinePassed:  boolean
  scoreFilePath:   string   // path to Scorecard JSON (written alongside .ndjson)
}
```
