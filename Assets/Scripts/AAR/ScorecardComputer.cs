// ============================================================
// ScorecardComputer.cs — Tactical Performance Scorecard Computer
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Implements board/contracts/scorecard.md v1, websocket-ipc.md v1,
// docs/SCHEMA.md Entity 4 & 5, and missions/sohail-mission.md card AA-P0-B.
//
// In P0: Stub Scorecard compute with all metrics=0.0 / false, doctrinePassed=false.
// Establishes the complete pipeline:
// 1. SCENARIO_END triggers ComputeScorecard()
// 2. Writes sessions/<sessionId>.scorecard.json
// 3. Updates sessions/manifest.json
// 4. Broadcasts SCORECARD_PAYLOAD via WsClient.SendAsync (Client -> Server -> both screens)
// 5. Guarantees delivery in <= 10s of SCENARIO_END
// ============================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TrishulSim.Network;
using TrishulSim.Scenarios;
using UnityEngine;

namespace TrishulSim.AAR
{
    /// <summary>
    /// ScorecardMetrics matching board/contracts/scorecard.md v1 and docs/SCHEMA.md Entity 4.
    /// In P0: metrics are stubs (all zeros/false). Real metrics computed in AA-P1-A.
    /// </summary>
    [Serializable]
    public class ScorecardMetrics
    {
        public long timeToDetectionMs;       // ms from t=0 to first non-GREEN threatState; 0 if never detected
        public float classificationAccuracy; // 0.0–1.0; v1: hardcoded to 0.0 (metric requires v2 UserClassificationEvent)
        public float roeCompliance;          // 1.0 = clean; 0.0 = >=1 friendlyBlackoutRisk event
        public int kineticRounds;            // integer count of KineticEvents
        public int kineticHits;              // integer count of KineticEvents where hit == true
        public float kineticEfficiency;      // kineticHits / kineticRounds; 0.0 if kineticRounds == 0
        public int agentsNeutralised;        // count of agents where isNeutralised == true in finalSwarmSnapshot
        public int agentsMissed;             // attackerCount - agentsNeutralised (from ScenarioConfig)
        public bool prematureEwPenalty;      // true if EW engaged while threatState == "GREEN"

        public ScorecardMetrics Clone()
        {
            return new ScorecardMetrics
            {
                timeToDetectionMs = timeToDetectionMs,
                classificationAccuracy = classificationAccuracy,
                roeCompliance = roeCompliance,
                kineticRounds = kineticRounds,
                kineticHits = kineticHits,
                kineticEfficiency = kineticEfficiency,
                agentsNeutralised = agentsNeutralised,
                agentsMissed = agentsMissed,
                prematureEwPenalty = prematureEwPenalty
            };
        }

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder(256);
            sb.Append("{\"timeToDetectionMs\":");
            sb.Append(timeToDetectionMs.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"classificationAccuracy\":");
            sb.Append(ScenarioConfig.FormatFloat(classificationAccuracy));
            sb.Append(",\"roeCompliance\":");
            sb.Append(ScenarioConfig.FormatFloat(roeCompliance));
            sb.Append(",\"kineticRounds\":");
            sb.Append(kineticRounds.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"kineticHits\":");
            sb.Append(kineticHits.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"kineticEfficiency\":");
            sb.Append(ScenarioConfig.FormatFloat(kineticEfficiency));
            sb.Append(",\"agentsNeutralised\":");
            sb.Append(agentsNeutralised.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"agentsMissed\":");
            sb.Append(agentsMissed.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"prematureEwPenalty\":");
            sb.Append(prematureEwPenalty ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        public static bool TryParse(string json, out ScorecardMetrics metrics, out string error)
        {
            metrics = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "ScorecardMetrics JSON is null or empty.";
                return false;
            }

            Dictionary<string, JsonToken> fields;
            if (!JsonParser.TryParseObject(json, out fields, out error))
                return false;

            long ttd;
            if (!JsonParser.TryGetRequiredLong(fields, "timeToDetectionMs", out ttd, out error))
                return false;

            float classAcc;
            if (!JsonParser.TryGetRequiredFloat(fields, "classificationAccuracy", out classAcc, out error))
                return false;

            float roe;
            if (!JsonParser.TryGetRequiredFloat(fields, "roeCompliance", out roe, out error))
                return false;

            int rounds;
            if (!JsonParser.TryGetRequiredInt(fields, "kineticRounds", out rounds, out error))
                return false;

            int hits;
            if (!JsonParser.TryGetRequiredInt(fields, "kineticHits", out hits, out error))
                return false;

            float eff;
            if (!JsonParser.TryGetRequiredFloat(fields, "kineticEfficiency", out eff, out error))
                return false;

            int neutralised;
            if (!JsonParser.TryGetRequiredInt(fields, "agentsNeutralised", out neutralised, out error))
                return false;

            int missed;
            if (!JsonParser.TryGetRequiredInt(fields, "agentsMissed", out missed, out error))
                return false;

            bool premature;
            if (!JsonParser.TryGetRequiredBool(fields, "prematureEwPenalty", out premature, out error))
                return false;

            metrics = new ScorecardMetrics
            {
                timeToDetectionMs = ttd,
                classificationAccuracy = classAcc,
                roeCompliance = roe,
                kineticRounds = rounds,
                kineticHits = hits,
                kineticEfficiency = eff,
                agentsNeutralised = neutralised,
                agentsMissed = missed,
                prematureEwPenalty = premature
            };
            return true;
        }

        public static ScorecardMetrics Parse(string json)
        {
            ScorecardMetrics metrics;
            string error;
            if (!TryParse(json, out metrics, out error))
                throw new FormatException(error);
            return metrics;
        }
    }

    /// <summary>
    /// DoctrineBenchmarks matching board/contracts/scorecard.md v1 and docs/SCHEMA.md Entity 4.
    /// Doctrinal pass/fail thresholds enforced by TRISHUL-SIM AAR Engine.
    /// </summary>
    [Serializable]
    public class DoctrineBenchmarks
    {
        public long timeToDetectionThresholdMs = 15000;   // 15000 ms (doctrinal 15-second detection window)
        public float roeMinCompliance = 1.0f;             // 1.0 (zero ROE violations — non-negotiable)
        public float kineticMinEfficiency = 0.5f;         // 0.5 (>=50% hit rate)

        public DoctrineBenchmarks Clone()
        {
            return new DoctrineBenchmarks
            {
                timeToDetectionThresholdMs = timeToDetectionThresholdMs,
                roeMinCompliance = roeMinCompliance,
                kineticMinEfficiency = kineticMinEfficiency
            };
        }

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder(128);
            sb.Append("{\"timeToDetectionThresholdMs\":");
            sb.Append(timeToDetectionThresholdMs.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"roeMinCompliance\":");
            sb.Append(ScenarioConfig.FormatFloat(roeMinCompliance));
            sb.Append(",\"kineticMinEfficiency\":");
            sb.Append(ScenarioConfig.FormatFloat(kineticMinEfficiency));
            sb.Append('}');
            return sb.ToString();
        }

        public static bool TryParse(string json, out DoctrineBenchmarks benchmarks, out string error)
        {
            benchmarks = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "DoctrineBenchmarks JSON is null or empty.";
                return false;
            }

            Dictionary<string, JsonToken> fields;
            if (!JsonParser.TryParseObject(json, out fields, out error))
                return false;

            long ttdThresh;
            if (!JsonParser.TryGetRequiredLong(fields, "timeToDetectionThresholdMs", out ttdThresh, out error))
                return false;

            float minRoe;
            if (!JsonParser.TryGetRequiredFloat(fields, "roeMinCompliance", out minRoe, out error))
                return false;

            float minEff;
            if (!JsonParser.TryGetRequiredFloat(fields, "kineticMinEfficiency", out minEff, out error))
                return false;

            benchmarks = new DoctrineBenchmarks
            {
                timeToDetectionThresholdMs = ttdThresh,
                roeMinCompliance = minRoe,
                kineticMinEfficiency = minEff
            };
            return true;
        }

        public static DoctrineBenchmarks Parse(string json)
        {
            DoctrineBenchmarks benchmarks;
            string error;
            if (!TryParse(json, out benchmarks, out error))
                throw new FormatException(error);
            return benchmarks;
        }
    }

    /// <summary>
    /// Scorecard data object matching board/contracts/scorecard.md v1 and docs/SCHEMA.md Entity 4.
    /// Written to sessions/&lt;sessionId&gt;.scorecard.json and transmitted inside SCORECARD_PAYLOAD envelope.
    /// </summary>
    [Serializable]
    public class Scorecard
    {
        public const int CurrentVersion = 1;
        public const string DefaultTraineeId = "havildar-rajan";

        public string sessionId = string.Empty;
        public string scenarioId = string.Empty;
        public string scenarioName = string.Empty;
        public string scenarioNameHindi = string.Empty;
        public string traineeId = DefaultTraineeId;
        public string completedAtIso = string.Empty;
        public long durationMs;
        public bool doctrinePassed;

        public ScorecardMetrics metrics = new ScorecardMetrics();
        public DoctrineBenchmarks doctrineBenchmarks = new DoctrineBenchmarks();
        public int[] timelineIndex = new int[0];
        public string sessionFilePath = string.Empty;
        public bool partial;
        public int version = CurrentVersion;

        public Scorecard Clone()
        {
            int[] idxCopy = null;
            if (timelineIndex != null)
            {
                idxCopy = new int[timelineIndex.Length];
                Array.Copy(timelineIndex, idxCopy, timelineIndex.Length);
            }

            return new Scorecard
            {
                sessionId = sessionId,
                scenarioId = scenarioId,
                scenarioName = scenarioName,
                scenarioNameHindi = scenarioNameHindi,
                traineeId = traineeId,
                completedAtIso = completedAtIso,
                durationMs = durationMs,
                doctrinePassed = doctrinePassed,
                metrics = metrics != null ? metrics.Clone() : new ScorecardMetrics(),
                doctrineBenchmarks = doctrineBenchmarks != null ? doctrineBenchmarks.Clone() : new DoctrineBenchmarks(),
                timelineIndex = idxCopy ?? new int[0],
                sessionFilePath = sessionFilePath,
                partial = partial,
                version = version
            };
        }

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder(1024);
            sb.Append("{\"sessionId\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(sessionId ?? string.Empty));
            sb.Append("\",\"scenarioId\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(scenarioId ?? string.Empty));
            sb.Append("\",\"scenarioName\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(scenarioName ?? string.Empty));
            sb.Append("\",\"scenarioNameHindi\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(scenarioNameHindi ?? string.Empty));
            sb.Append("\",\"traineeId\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(traineeId ?? string.Empty));
            sb.Append("\",\"completedAtIso\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(completedAtIso ?? string.Empty));
            sb.Append("\",\"durationMs\":");
            sb.Append(durationMs.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"doctrinePassed\":");
            sb.Append(doctrinePassed ? "true" : "false");
            sb.Append(",\"metrics\":");
            sb.Append((metrics ?? new ScorecardMetrics()).ToJson());
            sb.Append(",\"doctrineBenchmarks\":");
            sb.Append((doctrineBenchmarks ?? new DoctrineBenchmarks()).ToJson());
            sb.Append(",\"timelineIndex\":[");
            if (timelineIndex != null)
            {
                for (int i = 0; i < timelineIndex.Length; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    sb.Append(timelineIndex[i].ToString(CultureInfo.InvariantCulture));
                }
            }
            sb.Append("],\"sessionFilePath\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(sessionFilePath ?? string.Empty));
            sb.Append("\",\"partial\":");
            sb.Append(partial ? "true" : "false");
            sb.Append(",\"version\":");
            sb.Append(version.ToString(CultureInfo.InvariantCulture));
            sb.Append('}');
            return sb.ToString();
        }

        public static bool TryParse(string json, out Scorecard scorecard, out string error)
        {
            scorecard = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Scorecard JSON is null or empty.";
                return false;
            }

            Dictionary<string, JsonToken> fields;
            if (!JsonParser.TryParseObject(json, out fields, out error))
                return false;

            string sId;
            if (!JsonParser.TryGetRequiredString(fields, "sessionId", out sId, out error))
                return false;

            string scId;
            if (!JsonParser.TryGetRequiredString(fields, "scenarioId", out scId, out error))
                return false;

            string nameVal;
            if (!JsonParser.TryGetRequiredString(fields, "scenarioName", out nameVal, out error))
                return false;

            string nameHiVal;
            if (!JsonParser.TryGetRequiredString(fields, "scenarioNameHindi", out nameHiVal, out error))
                return false;

            string traineeVal = DefaultTraineeId;
            if (fields.ContainsKey("traineeId"))
            {
                if (!JsonParser.TryGetRequiredString(fields, "traineeId", out traineeVal, out error))
                    return false;
            }

            string completedIso;
            if (!JsonParser.TryGetRequiredString(fields, "completedAtIso", out completedIso, out error))
                return false;

            long durMs;
            if (!JsonParser.TryGetRequiredLong(fields, "durationMs", out durMs, out error))
                return false;

            bool passed;
            if (!JsonParser.TryGetRequiredBool(fields, "doctrinePassed", out passed, out error))
                return false;

            string metricsRaw;
            if (!JsonParser.TryGetRequiredObjectRaw(fields, "metrics", out metricsRaw, out error))
                return false;
            ScorecardMetrics metricsObj;
            if (!ScorecardMetrics.TryParse(metricsRaw, out metricsObj, out error))
                return false;

            string benchRaw;
            if (!JsonParser.TryGetRequiredObjectRaw(fields, "doctrineBenchmarks", out benchRaw, out error))
                return false;
            DoctrineBenchmarks benchObj;
            if (!DoctrineBenchmarks.TryParse(benchRaw, out benchObj, out error))
                return false;

            int[] timelineIdx = new int[0];
            if (fields.ContainsKey("timelineIndex"))
            {
                if (!TryGetIntArray(fields, "timelineIndex", out timelineIdx, out error))
                    return false;
            }

            string sessionPath;
            if (!JsonParser.TryGetRequiredString(fields, "sessionFilePath", out sessionPath, out error))
                return false;

            bool partVal = false;
            if (fields.ContainsKey("partial"))
            {
                if (!JsonParser.TryGetRequiredBool(fields, "partial", out partVal, out error))
                    return false;
            }

            int verVal = CurrentVersion;
            if (fields.ContainsKey("version"))
            {
                if (!JsonParser.TryGetRequiredInt(fields, "version", out verVal, out error))
                    return false;
            }

            scorecard = new Scorecard
            {
                sessionId = sId,
                scenarioId = scId,
                scenarioName = nameVal,
                scenarioNameHindi = nameHiVal,
                traineeId = traineeVal,
                completedAtIso = completedIso,
                durationMs = durMs,
                doctrinePassed = passed,
                metrics = metricsObj,
                doctrineBenchmarks = benchObj,
                timelineIndex = timelineIdx,
                sessionFilePath = sessionPath,
                partial = partVal,
                version = verVal
            };
            return true;
        }

        public static Scorecard Parse(string json)
        {
            Scorecard scorecard;
            string error;
            if (!TryParse(json, out scorecard, out error))
                throw new FormatException(error);
            return scorecard;
        }

        private static bool TryGetIntArray(Dictionary<string, JsonToken> fields, string key, out int[] values, out string error)
        {
            values = null;
            error = null;
            JsonToken tok;
            if (!fields.TryGetValue(key, out tok) || tok.Kind != JsonTokenKind.Array || tok.ArrayElements == null)
            {
                error = "Missing or invalid array field '" + key + "'.";
                return false;
            }

            int[] arr = new int[tok.ArrayElements.Count];
            for (int i = 0; i < tok.ArrayElements.Count; i++)
            {
                JsonToken elem = tok.ArrayElements[i];
                int val;
                if (elem.Kind != JsonTokenKind.Number || !int.TryParse(elem.RawText, NumberStyles.Integer, CultureInfo.InvariantCulture, out val))
                {
                    error = "Invalid integer element at index " + i + " in array '" + key + "'.";
                    return false;
                }
                arr[i] = val;
            }
            values = arr;
            return true;
        }
    }

    /// <summary>
    /// MonoBehaviour component coordinating AAR Scorecard computation and WebSocket delivery.
    /// Runs on Trainee Cockpit scene (same scene as TelemetryLogger).
    /// </summary>
    public class ScorecardComputer : MonoBehaviour
    {
        public static ScorecardComputer Instance { get; private set; }

        [SerializeField] private WsClient wsClient;
        [SerializeField] private TelemetryLogger telemetryLogger;
        [SerializeField] private string sessionsDirectoryOverride;
        [SerializeField] private bool autoSubscribeToClient = true;

        private WsClient _subscribedClient;
        private ScenarioStartPayload _lastStartPayload;
        private Scorecard _lastComputedScorecard;

        public event Action<Scorecard> OnScorecardComputed;
        public event Action<Scorecard> OnScorecardBroadcast;

        public string SessionsDirectoryOverride
        {
            get { return sessionsDirectoryOverride; }
            set { sessionsDirectoryOverride = value; }
        }

        public WsClient Client
        {
            get { return wsClient != null ? wsClient : WsClient.Instance; }
            set { wsClient = value; }
        }

        public TelemetryLogger Logger
        {
            get { return telemetryLogger != null ? telemetryLogger : TelemetryLogger.Instance; }
            set { telemetryLogger = value; }
        }

        public ScenarioStartPayload LastStartPayload
        {
            get { return _lastStartPayload; }
            set { _lastStartPayload = value; }
        }

        public Scorecard LastComputedScorecard
        {
            get { return _lastComputedScorecard; }
        }

        public void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        public void Start()
        {
            if (Instance == null)
                Instance = this;

            if (telemetryLogger == null)
            {
#if UNITY_5_3_OR_NEWER
                telemetryLogger = FindObjectOfType<TelemetryLogger>();
#endif
            }

            if (autoSubscribeToClient)
            {
                SubscribeToClient();
            }
        }

        public void OnEnable()
        {
            if (autoSubscribeToClient)
            {
                SubscribeToClient();
            }
        }

        public void OnDisable()
        {
            UnsubscribeFromClient();
        }

        public void OnDestroy()
        {
            UnsubscribeFromClient();
            if (ReferenceEquals(Instance, this))
                Instance = null;
        }

        public void SubscribeToClient(WsClient clientToSubscribe = null)
        {
            WsClient target = clientToSubscribe != null ? clientToSubscribe : Client;
            if (target == null)
                return;

            if (ReferenceEquals(_subscribedClient, target))
                return;

            UnsubscribeFromClient();
            _subscribedClient = target;
            _subscribedClient.OnMessageReceived += HandleMessageReceived;
            Debug.Log("[ScorecardComputer] Subscribed to WsClient.OnMessageReceived");
        }

        public void UnsubscribeFromClient()
        {
            if (_subscribedClient != null)
            {
                _subscribedClient.OnMessageReceived -= HandleMessageReceived;
                _subscribedClient = null;
            }
        }

        public void HandleMessageReceived(JsonEnvelope envelope)
        {
            if (string.IsNullOrEmpty(envelope.type))
                return;

            if (string.Equals(envelope.type, JsonEnvelope.SCENARIO_START, StringComparison.Ordinal))
            {
                ScenarioStartPayload startPayload;
                string error;
                if (ScenarioStartPayload.TryParse(envelope.payload, out startPayload, out error))
                {
                    _lastStartPayload = startPayload;
                }
                else
                {
                    Debug.LogWarning("[ScorecardComputer] Malformed SCENARIO_START payload: " + error);
                }
            }
            else if (string.Equals(envelope.type, JsonEnvelope.SCENARIO_END, StringComparison.Ordinal))
            {
                ScenarioEndPayload endPayload;
                string error;
                if (ScenarioEndPayload.TryParse(envelope.payload, out endPayload, out error))
                {
                    string scId = _lastStartPayload != null ? _lastStartPayload.scenarioId : null;
                    ComputeScorecard(endPayload.sessionId, scId, endPayload.reason, endPayload.t, endPayload.finalSwarmSnapshot);
                }
                else
                {
                    Debug.LogWarning("[ScorecardComputer] Malformed SCENARIO_END payload: " + error);
                    string sId = _lastStartPayload != null ? _lastStartPayload.sessionId : (Logger != null ? Logger.CurrentSessionId : "unknown-session");
                    string scId = _lastStartPayload != null ? _lastStartPayload.scenarioId : (Logger != null ? Logger.CurrentScenarioId : "punjab-fog-pincer-01");
                    ComputeScorecard(sId, scId, "END_RECEIVED", 0, null);
                }
            }
        }

        /// <summary>
        /// Resolves the absolute directory path where session files (.ndjson, .scorecard.json, manifest.json) are stored.
        /// Defaults to %AppData%\TRISHUL-SIM\sessions\.
        /// </summary>
        public string GetSessionsDirectory()
        {
            if (!string.IsNullOrWhiteSpace(sessionsDirectoryOverride))
                return sessionsDirectoryOverride;

            if (Logger != null && !string.IsNullOrWhiteSpace(Logger.SessionsDirectoryOverride))
                return Logger.SessionsDirectoryOverride;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
                appData = Directory.GetCurrentDirectory();

            return Path.Combine(appData, "TRISHUL-SIM", "sessions");
        }

        /// <summary>
        /// Computes stub Scorecard on SCENARIO_END, writes to disk, updates manifest, and broadcasts SCORECARD_PAYLOAD.
        /// In P0: all metrics are zeros/false, doctrinePassed = false, timelineIndex = [].
        /// </summary>
        public Scorecard ComputeScorecard(
            string sessionId,
            string scenarioId = null,
            string endReason = null,
            long durationMs = 0,
            SwarmAgentState[] finalSwarmSnapshot = null)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                sessionId = _lastStartPayload != null ? _lastStartPayload.sessionId : (Logger != null ? Logger.CurrentSessionId : "sess-stub-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            }

            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                scenarioId = _lastStartPayload != null ? _lastStartPayload.scenarioId : (Logger != null ? Logger.CurrentScenarioId : "punjab-fog-pincer-01");
            }

            string scenarioName = "Punjab Border — Zero Visibility Pincer";
            string scenarioNameHindi = "पंजाब सीमा — शून्य दृश्यता पिंसर"; // fallback

            if (_lastStartPayload != null && _lastStartPayload.scenarioConfig != null)
            {
                if (!string.IsNullOrWhiteSpace(_lastStartPayload.scenarioConfig.name))
                    scenarioName = _lastStartPayload.scenarioConfig.name;
                if (!string.IsNullOrWhiteSpace(_lastStartPayload.scenarioConfig.nameHindi))
                    scenarioNameHindi = _lastStartPayload.scenarioConfig.nameHindi;
            }
            else
            {
                // Try looking up in ScenarioConfig defaults
                if (string.Equals(scenarioId, "punjab-fog-pincer-01", StringComparison.Ordinal))
                {
                    scenarioName = "Punjab Border \u2014 Zero Visibility Pincer";
                    scenarioNameHindi = "\u092A\u0902\u091C\u093E\u092C \u0938\u0940\u092E\u093E \u2014 \u0936\u0942\u0928\u094D\u092F \u0926\u0943\u0936\u094D\u092F\u0924\u093E \u092A\u093F\u0902\u0938\u0930";
                }
            }

            string dir = GetSessionsDirectory();
            try { Directory.CreateDirectory(dir); } catch { }

            string expectedNdjsonPath = (Logger != null && !string.IsNullOrWhiteSpace(Logger.CurrentSessionFilePath) && string.Equals(Logger.CurrentSessionId, sessionId, StringComparison.Ordinal) && File.Exists(Logger.CurrentSessionFilePath))
                ? Logger.CurrentSessionFilePath
                : Path.Combine(dir, sessionId + ".ndjson");

            string completedIso = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

            // Read NDJSON session records if file exists on disk
            List<TelemetryRecord> records = new List<TelemetryRecord>();
            bool fileReadAttempted = false;
            bool fileReadError = false;

            if (File.Exists(expectedNdjsonPath))
            {
                fileReadAttempted = true;
                try
                {
                    using (FileStream fs = new FileStream(expectedNdjsonPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader reader = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line))
                                continue;

                            TelemetryRecord rec;
                            string err;
                            if (TelemetryRecord.TryParse(line, out rec, out err))
                            {
                                records.Add(rec);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[ScorecardComputer] Error reading NDJSON file: " + ex.Message);
                    fileReadError = true;
                }
            }

            // Resolve scenario configuration for attackerCount benchmarks
            ScenarioConfig config = (_lastStartPayload != null && _lastStartPayload.scenarioConfig != null)
                ? _lastStartPayload.scenarioConfig
                : null;

            if (config == null)
            {
                config = new ScenarioConfig { id = scenarioId ?? "punjab-fog-pincer-01" };
            }

            DoctrineBenchmarks benchmarks = new DoctrineBenchmarks
            {
                timeToDetectionThresholdMs = 15000,
                roeMinCompliance = 1.0f,
                kineticMinEfficiency = 0.5f
            };

            // Compute real ScorecardMetrics via DecisionTreeScorer (AA-P1-A)
            ScorecardMetrics metrics = DecisionTreeScorer.Score(records, finalSwarmSnapshot, config, benchmarks);

            bool threatDetected = metrics.timeToDetectionMs > 0 ||
                (records.Count > 0 && !string.Equals(records[0].threatState, ScenarioTickPayload.THREAT_GREEN, StringComparison.OrdinalIgnoreCase));
            bool doctrinePassed = DecisionTreeScorer.EvaluateDoctrine(metrics, benchmarks, threatDetected);

            long actualDurationMs = durationMs;
            if (actualDurationMs <= 0 && records.Count > 0)
            {
                actualDurationMs = records[records.Count - 1].t;
            }

            bool isPartial = fileReadError || (!fileReadAttempted && (Logger != null && Logger.TotalTicksWritten > 0));

            // Compute timelineIndex for Timeline Scrubber data engine (AA-P1-B)
            int[] timelineIdx = null;
            if (File.Exists(expectedNdjsonPath))
            {
                try
                {
                    timelineIdx = TimelineIndexer.BuildIndex(expectedNdjsonPath);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[ScorecardComputer] Failed to build timeline index from file: " + ex.Message);
                }
            }

            if ((timelineIdx == null || timelineIdx.Length == 0) && records != null && records.Count > 0)
            {
                timelineIdx = TimelineIndexer.BuildIndexFromRecords(records);
            }

            if (timelineIdx == null)
            {
                timelineIdx = new int[0];
            }

            Scorecard scorecard = new Scorecard
            {
                sessionId = sessionId,
                scenarioId = scenarioId,
                scenarioName = scenarioName,
                scenarioNameHindi = scenarioNameHindi,
                traineeId = Scorecard.DefaultTraineeId,
                completedAtIso = completedIso,
                durationMs = Math.Max(0L, actualDurationMs),
                doctrinePassed = doctrinePassed,
                metrics = metrics,
                doctrineBenchmarks = benchmarks,
                timelineIndex = timelineIdx,
                sessionFilePath = expectedNdjsonPath,
                partial = isPartial,
                version = Scorecard.CurrentVersion
            };

            // 1. Write sessions/<sessionId>.scorecard.json
            string scorecardPath = Path.Combine(dir, sessionId + ".scorecard.json");
            try
            {
                File.WriteAllText(scorecardPath, scorecard.ToJson(), Encoding.UTF8);
                Debug.Log("[ScorecardComputer] Wrote scorecard JSON: " + scorecardPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ScorecardComputer] Failed writing scorecard file: " + ex.Message);
            }

            // 2. Update sessions/manifest.json
            string manifestPath = Path.Combine(dir, "manifest.json");
            try
            {
                SessionManifest manifest = SessionManifest.LoadOrCreate(manifestPath);
                SessionSummary summary = new SessionSummary(
                    scorecard.sessionId,
                    scorecard.scenarioId,
                    scorecard.completedAtIso,
                    scorecard.doctrinePassed,
                    scorecardPath,
                    scorecard.partial);
                manifest.AddOrUpdate(summary);
                manifest.Save(manifestPath);
                Debug.Log("[ScorecardComputer] Updated manifest at: " + manifestPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ScorecardComputer] Failed updating manifest: " + ex.Message);
            }

            _lastComputedScorecard = scorecard;

            if (TimelinePlaybackController.Instance != null)
            {
                try
                {
                    TimelinePlaybackController.Instance.Initialize(scorecard);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[ScorecardComputer] Failed to initialize TimelinePlaybackController: " + ex.Message);
                }
            }

            Action<Scorecard> compHandler = OnScorecardComputed;
            if (compHandler != null)
                compHandler(scorecard);

            // 3. Broadcast SCORECARD_PAYLOAD via WsClient (Client -> Server -> broadcast)
            BroadcastScorecard(scorecard);

            return scorecard;
        }

        /// <summary>
        /// Broadcasts SCORECARD_PAYLOAD envelope to the WebSocket server for redistribution.
        /// Envelope payload contains { "scorecard": &lt;Scorecard JSON&gt; }.
        /// </summary>
        public void BroadcastScorecard(Scorecard scorecard)
        {
            if (scorecard == null)
                return;

            string scorecardJson = scorecard.ToJson();
            string payloadJson = "{\"scorecard\":" + scorecardJson + "}";
            JsonEnvelope envelope = JsonEnvelope.Create(JsonEnvelope.SCORECARD_PAYLOAD, payloadJson);

            WsClient client = Client;
            if (client != null && client.IsConnected)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await client.SendAsync(envelope.ToJson()).ConfigureAwait(false);
                        Debug.Log("[AAREngine] SCORECARD_PAYLOAD sent");
                        string metricsLabel = (scorecard.metrics != null && (scorecard.metrics.timeToDetectionMs > 0 || scorecard.metrics.kineticRounds > 0)) ? "real" : "stub";
                        Debug.Log("[AAREngine] Scorecard broadcast — sessionId: " + scorecard.sessionId + ", metrics: " + metricsLabel);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[ScorecardComputer] Broadcast transport error: " + ex.Message);
                    }
                });
            }
            else
            {
                // In offline/test mode: still log required contract markers
                Debug.Log("[AAREngine] SCORECARD_PAYLOAD sent");
                string metricsLabel = (scorecard.metrics != null && (scorecard.metrics.timeToDetectionMs > 0 || scorecard.metrics.kineticRounds > 0)) ? "real" : "stub";
                Debug.Log("[AAREngine] Scorecard broadcast — sessionId: " + scorecard.sessionId + ", metrics: " + metricsLabel);
            }

            Action<Scorecard> broadHandler = OnScorecardBroadcast;
            if (broadHandler != null)
                broadHandler(scorecard);
        }
    }
}
