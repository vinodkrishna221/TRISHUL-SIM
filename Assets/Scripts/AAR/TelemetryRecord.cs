// ============================================================
// TelemetryRecord.cs — 10 Hz Telemetry Record Entity Contract
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Implements board/contracts/telemetry.md v1 and docs/SCHEMA.md Entity 3.
// Represents one NDJSON line written every 100 ms during a running scenario.
// ============================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TrishulSim.Network;
using TrishulSim.Scenarios;
using UnityEngine;

namespace TrishulSim.AAR
{
    /// <summary>
    /// TelemetryRecord matching board/contracts/telemetry.md v1 and docs/SCHEMA.md Entity 3.
    /// One record is appended to %AppData%\TRISHUL-SIM\sessions\&lt;sessionId&gt;.ndjson every 100 ms (10 Hz).
    /// </summary>
    [Serializable]
    public class TelemetryRecord
    {
        public long t; // REQUIRED — scenario-elapsed milliseconds, always >= 0; increments by ~100 each line
        public string sessionId = string.Empty; // REQUIRED — "sess-20261004-143201"; matches SCENARIO_START.sessionId
        public string scenarioId = string.Empty; // REQUIRED — "punjab-fog-pincer-01"
        public TraineeAction traineeAction; // REQUIRED — antenna orientation, EW engagement, kinetic shots
        public SwarmAgentState[] swarmSnapshot; // REQUIRED — full swarm state at this tick
        public SensorReadings sensorReadings; // REQUIRED — synthetic sensor readings
        public string threatState = ScenarioTickPayload.THREAT_GREEN; // REQUIRED — "GREEN" | "AMBER" | "RED"
        public bool friendlyVhfActive; // REQUIRED — true if friendly VHF radios are active in sim zone
        public EWJammerState ewJammerState; // REQUIRED — current EW jammer state

        // Nested subclass aliases for full contract and namespace compatibility
        [Serializable]
        public class TraineeActionRef : TraineeAction { }
        [Serializable]
        public class KineticEventRef : KineticEvent { }
        [Serializable]
        public class SensorReadingsRef : SensorReadings { }
        [Serializable]
        public class EWJammerStateRef : EWJammerState { }
        [Serializable]
        public class SwarmAgentStateRef : SwarmAgentState { }

        public TelemetryRecord()
        {
            t = 0;
            sessionId = string.Empty;
            scenarioId = string.Empty;
            traineeAction = new TraineeAction();
            swarmSnapshot = new SwarmAgentState[0];
            sensorReadings = new SensorReadings();
            threatState = ScenarioTickPayload.THREAT_GREEN;
            friendlyVhfActive = false;
            ewJammerState = new EWJammerState();
        }

        public TelemetryRecord(
            long t,
            string sessionId,
            string scenarioId,
            TraineeAction traineeAction,
            SwarmAgentState[] swarmSnapshot,
            SensorReadings sensorReadings,
            string threatState,
            bool friendlyVhfActive,
            EWJammerState ewJammerState)
        {
            this.t = t;
            this.sessionId = sessionId ?? string.Empty;
            this.scenarioId = scenarioId ?? string.Empty;
            this.traineeAction = traineeAction ?? new TraineeAction();
            this.swarmSnapshot = swarmSnapshot ?? new SwarmAgentState[0];
            this.sensorReadings = sensorReadings ?? new SensorReadings();
            this.threatState = ScenarioTickPayload.IsValidThreatState(threatState) ? threatState : ScenarioTickPayload.THREAT_GREEN;
            this.friendlyVhfActive = friendlyVhfActive;
            this.ewJammerState = ewJammerState ?? new EWJammerState();
        }

        public TelemetryRecord Clone()
        {
            return new TelemetryRecord
            {
                t = t,
                sessionId = sessionId,
                scenarioId = scenarioId,
                traineeAction = traineeAction != null ? traineeAction.Clone() : new TraineeAction(),
                swarmSnapshot = SwarmAgentState.CloneArray(swarmSnapshot),
                sensorReadings = sensorReadings != null ? sensorReadings.Clone() : new SensorReadings(),
                threatState = threatState,
                friendlyVhfActive = friendlyVhfActive,
                ewJammerState = ewJammerState != null ? ewJammerState.Clone() : new EWJammerState()
            };
        }

        /// <summary>
        /// Factory method to construct a TelemetryRecord from a SCENARIO_TICK payload and active scenarioId.
        /// </summary>
        public static TelemetryRecord FromTickPayload(ScenarioTickPayload tick, string scenarioId)
        {
            if (tick == null)
                return null;

            return new TelemetryRecord
            {
                t = tick.t,
                sessionId = tick.sessionId ?? string.Empty,
                scenarioId = scenarioId ?? string.Empty,
                traineeAction = tick.traineeAction != null ? tick.traineeAction.Clone() : new TraineeAction(),
                swarmSnapshot = SwarmAgentState.CloneArray(tick.swarmSnapshot),
                sensorReadings = tick.sensorReadings != null ? tick.sensorReadings.Clone() : new SensorReadings(),
                threatState = ScenarioTickPayload.IsValidThreatState(tick.threatState) ? tick.threatState : ScenarioTickPayload.THREAT_GREEN,
                friendlyVhfActive = tick.friendlyVhfActive,
                ewJammerState = tick.ewJammerState != null ? tick.ewJammerState.Clone() : new EWJammerState()
            };
        }

        /// <summary>
        /// Serializes to a single-line JSON string matching board/contracts/telemetry.md NDJSON format.
        /// Zero-reflection implementation for IL2CPP compatibility.
        /// </summary>
        public string ToJson()
        {
            TraineeAction ta = traineeAction ?? new TraineeAction();
            SensorReadings sr = sensorReadings ?? new SensorReadings();
            EWJammerState ew = ewJammerState ?? new EWJammerState();
            string tsStr = ScenarioTickPayload.IsValidThreatState(threatState) ? threatState : ScenarioTickPayload.THREAT_GREEN;

            StringBuilder sb = new StringBuilder(512);
            sb.Append("{\"t\":");
            sb.Append(t.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"sessionId\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(sessionId ?? string.Empty));
            sb.Append("\",\"scenarioId\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(scenarioId ?? string.Empty));
            sb.Append("\",\"traineeAction\":");
            sb.Append(ta.ToJson());
            sb.Append(",\"swarmSnapshot\":[");
            if (swarmSnapshot != null)
            {
                for (int i = 0; i < swarmSnapshot.Length; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    SwarmAgentState agent = swarmSnapshot[i] ?? new SwarmAgentState();
                    sb.Append(agent.ToJson());
                }
            }
            sb.Append("],\"sensorReadings\":");
            sb.Append(sr.ToJson());
            sb.Append(",\"threatState\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(tsStr));
            sb.Append("\",\"friendlyVhfActive\":");
            sb.Append(friendlyVhfActive ? "true" : "false");
            sb.Append(",\"ewJammerState\":");
            sb.Append(ew.ToJson());
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Parses a single NDJSON line into a TelemetryRecord.
        /// </summary>
        public static bool TryParse(string json, out TelemetryRecord record, out string error)
        {
            record = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "TelemetryRecord JSON is null or empty.";
                return false;
            }

            Dictionary<string, JsonToken> fields;
            if (!JsonParser.TryParseObject(json, out fields, out error))
                return false;

            long tVal;
            if (!JsonParser.TryGetRequiredLong(fields, "t", out tVal, out error) || tVal < 0)
            {
                if (string.IsNullOrEmpty(error))
                    error = "TelemetryRecord field 't' must be >= 0.";
                return false;
            }

            string sessionIdVal;
            if (!JsonParser.TryGetRequiredString(fields, "sessionId", out sessionIdVal, out error))
                return false;

            string scenarioIdVal;
            if (!JsonParser.TryGetRequiredString(fields, "scenarioId", out scenarioIdVal, out error))
                return false;

            string taRaw;
            if (!JsonParser.TryGetRequiredObjectRaw(fields, "traineeAction", out taRaw, out error))
                return false;
            TraineeAction ta;
            if (!TraineeAction.TryParse(taRaw, out ta, out error))
                return false;

            string[] snapshotRaws;
            if (!JsonParser.TryGetRequiredObjectArrayRaw(fields, "swarmSnapshot", out snapshotRaws, out error))
                return false;
            SwarmAgentState[] snapshot = new SwarmAgentState[snapshotRaws.Length];
            for (int i = 0; i < snapshotRaws.Length; i++)
            {
                SwarmAgentState agent;
                if (!SwarmAgentState.TryParse(snapshotRaws[i], out agent, out error))
                    return false;
                snapshot[i] = agent;
            }

            string srRaw;
            if (!JsonParser.TryGetRequiredObjectRaw(fields, "sensorReadings", out srRaw, out error))
                return false;
            SensorReadings sr;
            if (!SensorReadings.TryParse(srRaw, out sr, out error))
                return false;

            string threatVal;
            if (!JsonParser.TryGetRequiredString(fields, "threatState", out threatVal, out error))
                return false;
            if (!ScenarioTickPayload.IsValidThreatState(threatVal))
            {
                error = "TelemetryRecord field 'threatState' has unsupported value '" + threatVal + "'.";
                return false;
            }

            bool vhfVal;
            if (!JsonParser.TryGetRequiredBool(fields, "friendlyVhfActive", out vhfVal, out error))
                return false;

            string ewRaw;
            if (!JsonParser.TryGetRequiredObjectRaw(fields, "ewJammerState", out ewRaw, out error))
                return false;
            EWJammerState ew;
            if (!EWJammerState.TryParse(ewRaw, out ew, out error))
                return false;

            record = new TelemetryRecord
            {
                t = tVal,
                sessionId = sessionIdVal,
                scenarioId = scenarioIdVal,
                traineeAction = ta,
                swarmSnapshot = snapshot,
                sensorReadings = sr,
                threatState = threatVal,
                friendlyVhfActive = vhfVal,
                ewJammerState = ew
            };
            return true;
        }

        public static TelemetryRecord Parse(string json)
        {
            TelemetryRecord record;
            string error;
            if (!TryParse(json, out record, out error))
                throw new FormatException(error);
            return record;
        }
    }
}
