# Contract: telemetry
> version: 1 · 2026-10-04
> OWNER: aar-engine (writes TelemetryRecord to disk and assembles Scorecard)
> CONSUMERS: aar-engine (self — reads back for Scorecard compute) · ui-ux (Timeline Scrubber reads session file) · aar-engine PDF exporter

This contract defines the shape of the 10 Hz log entry written by the AAR Engine.
One `TelemetryRecord` line is appended to `%AppData%\TRISHUL-SIM\sessions\<sessionId>.ndjson` every 100 ms during a running scenario.
The file is newline-delimited JSON (NDJSON): each line is a valid, self-contained JSON object.

---

## TelemetryRecord  (one NDJSON line per 100 ms tick)

```
TelemetryRecord {
  t:               number            // REQUIRED — scenario-elapsed milliseconds, always ≥ 0; increments by ~100 each line
  sessionId:       string            // REQUIRED — "sess-20261004-143201"; matches SCENARIO_START.sessionId
  scenarioId:      string            // REQUIRED — "punjab-fog-pincer-01"
  traineeAction:   TraineeAction     // REQUIRED — see below
  swarmSnapshot:   SwarmAgentState[] // REQUIRED — full swarm state at this tick (from scenario-events.md)
  sensorReadings:  SensorReadings    // REQUIRED — see below
  threatState:     string            // REQUIRED — "GREEN" | "AMBER" | "RED"
  friendlyVhfActive: boolean         // REQUIRED — true if friendly VHF radios are active in sim zone
  ewJammerState:   EWJammerState     // REQUIRED — see below
}
```

### TraineeAction
```
TraineeAction {
  antennaBearingDeg:   number         // 0.0–359.9 — trainee's virtual antenna azimuth
  antennaElevationDeg: number         // -90.0–+90.0 — antenna elevation
  ewEngaged:           boolean        // true = EW jammer currently active
  ewBandGHz:           number | null  // active jam frequency; null if ewEngaged == false
  kinetic:             KineticEvent | null  // non-null only on the tick a shot is fired
}
```

### KineticEvent
```
KineticEvent {
  firedAtMs:    number    // scenario-elapsed ms when trigger pulled — same as parent TelemetryRecord.t
  targetAgentId: string   // "agent-3" — agent the SMASH 2000 reticle was locked to
  hit:          boolean   // true = lead-angle within accuracy cone AND agent not yet neutralised
  leadAngleDeg: number    // computed lead angle in degrees, e.g. 3.2
}
```

### SensorReadings
```
SensorReadings {
  rfWaterfallPeakGHz:  number   // highest-power detected RF frequency, e.g. 5.825
  rfWaterfallPeakDbm:  number   // received signal strength at peak, e.g. -62.3
  dopplerPeakHz:       number   // strongest Doppler return (blade-tip harmonic), e.g. 220.4
  acousticSplDb:       number   // acoustic sound pressure level in dB, e.g. 54.2
  radarDetectedCount:  number   // integer count of radar returns above threshold, e.g. 4
}
```

### EWJammerState
```
EWJammerState {
  engaged:               boolean   // true = jammer currently active
  bearingDeg:            number    // 0.0–359.9 — jammer azimuth
  elevationDeg:          number    // -90.0–+90.0 — jammer elevation
  bandGHz:               number    // jam band centre frequency
  friendlyBlackoutRisk:  boolean   // true = jammer azimuth overlaps friendly VHF antenna pattern; triggers ROE violation
}
```

---

## Full example (one NDJSON line, pretty-printed for readability — actual file is one line)

```json
{
  "t": 45200,
  "sessionId": "sess-20261004-143201",
  "scenarioId": "punjab-fog-pincer-01",
  "traineeAction": {
    "antennaBearingDeg": 247.0,
    "antennaElevationDeg": -5.0,
    "ewEngaged": true,
    "ewBandGHz": 5.825,
    "kinetic": null
  },
  "swarmSnapshot": [
    {
      "agentId": "agent-3",
      "isDecoy": false,
      "positionWorld": { "x": 142.3, "y": 8.3, "z": -87.1 },
      "velocityMs": { "x": -28.4, "y": -0.2, "z": 12.1 },
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
  ],
  "sensorReadings": {
    "rfWaterfallPeakGHz": 5.825,
    "rfWaterfallPeakDbm": -62.3,
    "dopplerPeakHz": 220.4,
    "acousticSplDb": 54.2,
    "radarDetectedCount": 4
  },
  "threatState": "RED",
  "friendlyVhfActive": false,
  "ewJammerState": {
    "engaged": true,
    "bearingDeg": 247.0,
    "elevationDeg": -5.0,
    "bandGHz": 5.825,
    "friendlyBlackoutRisk": false
  }
}
```

---

## Session file lifecycle

| Event | File action |
|---|---|
| `SCENARIO_START` received | Create `sessions/<sessionId>.ndjson` (truncate if exists) |
| Each `SCENARIO_TICK` (100 ms) | Append one `TelemetryRecord` line + `\n` |
| `SCENARIO_END` received | Flush and close file handle |
| Scorecard computed | Write `sessions/<sessionId>.scorecard.json` (see scorecard.md) |
| SessionManifest updated | Append entry to `sessions/manifest.json` |

**Error handling:** If disk write fails mid-scenario, `aar-engine` logs to Unity Console; scenario continues; Scorecard may be partial. Partial files are flagged with `"partial": true` in SessionManifest.

---

## Derived metrics (AAR Engine computes from the NDJSON after SCENARIO_END)

These are NOT stored in TelemetryRecord but computed by reading the file:

| Metric | Algorithm |
|---|---|
| `timeToDetectionMs` | `t` of first record where `threatState != "GREEN"` |
| `classificationAccuracy` | (correct agent-type identifications by trainee) / total agents — requires trainee classification events (future: separate UserClassificationEvent in v2) |
| `roeCompliance` | 1.0 if zero records have `ewJammerState.friendlyBlackoutRisk == true`; else 0.0 |
| `kineticRounds` | count of records where `traineeAction.kinetic != null` |
| `kineticHits` | count of records where `traineeAction.kinetic.hit == true` |
| `prematureEwPenalty` | true if any record has `ewEngaged == true` AND `threatState == "GREEN"` |

---

## Changelog
- v1 · 2026-10-04 · initial — Orion / Atlas
