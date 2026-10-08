// ============================================================
// TelemetryLogger.cs — 10 Hz NDJSON Telemetry Logger
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Implements board/contracts/telemetry.md v1, scenario-events.md v1,
// and missions/sohail-mission.md card AA-P0-A.
// Runs on Trainee Cockpit station, subscribes to WsClient.OnMessageReceived,
// writes one TelemetryRecord per 100 ms tick to:
// %AppData%\TRISHUL-SIM\sessions\<sessionId>.ndjson
// ============================================================

using System;
using System.IO;
using System.Text;
using TrishulSim.Network;
using TrishulSim.Scenarios;
using UnityEngine;

namespace TrishulSim.AAR
{
    /// <summary>
    /// 10 Hz TelemetryLogger (NDJSON) matching board/contracts/telemetry.md v1.
    /// Subscribes to WsClient.OnMessageReceived on the Trainee Cockpit station.
    /// On SCENARIO_START: creates sessions/&lt;sessionId&gt;.ndjson.
    /// On SCENARIO_TICK: appends one TelemetryRecord line and flushes every 1s (10 ticks).
    /// On SCENARIO_END: flushes and closes stream writer.
    /// </summary>
    public class TelemetryLogger : MonoBehaviour
    {
        public const string DefaultSessionsSubdirectory = "TRISHUL-SIM/sessions";
        public const int DefaultFlushIntervalTicks = 10; // flush every 1s (10 ticks at 10 Hz)

        public static TelemetryLogger Instance { get; private set; }

        [SerializeField] private WsClient wsClient;
        [SerializeField] private TraineeActionInputStub inputStub;
        [SerializeField] private string sessionsDirectoryOverride;
        [SerializeField] private bool autoFlushEvery10Ticks = true;
        [SerializeField] private bool autoSubscribeToClient = true;

        private readonly object _writeLock = new object();
        private StreamWriter _writer;
        private FileStream _fileStream;
        private WsClient _subscribedClient;
        private int _tickCount;
        private bool _isSessionActive;

        public event Action<string, string> OnSessionStarted; // (sessionId, filePath)
        public event Action<TelemetryRecord> OnRecordLogged;
        public event Action<string, int> OnSessionClosed;     // (filePath, ticksCount)

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

        public TraineeActionInputStub InputStub
        {
            get { return inputStub; }
            set { inputStub = value; }
        }

        public string CurrentSessionId { get; private set; }
        public string CurrentScenarioId { get; private set; }
        public string CurrentSessionFilePath { get; private set; }
        public int TotalTicksWritten { get { return _tickCount; } }
        public bool IsLogging { get { return _isSessionActive && _writer != null; } }

        public void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        public void Start()
        {
            if (Instance == null)
                Instance = this;

            if (inputStub == null)
            {
#if UNITY_5_3_OR_NEWER
                inputStub = FindObjectOfType<TraineeActionInputStub>();
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
            EndSession("OBJECT_DESTROYED");
            if (ReferenceEquals(Instance, this))
                Instance = null;
        }

        public void OnApplicationQuit()
        {
            EndSession("APPLICATION_QUIT");
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
            Debug.Log("[TelemetryLogger] Subscribed to WsClient.OnMessageReceived");
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
            if (string.Equals(envelope.type, JsonEnvelope.SCENARIO_START, StringComparison.Ordinal))
            {
                ScenarioStartPayload startPayload;
                string error;
                if (ScenarioStartPayload.TryParse(envelope.payload, out startPayload, out error))
                {
                    StartSession(startPayload.sessionId, startPayload.scenarioId);
                }
                else
                {
                    Debug.LogWarning("[TelemetryLogger] Ignoring malformed SCENARIO_START payload: " + error);
                }
            }
            else if (string.Equals(envelope.type, JsonEnvelope.SCENARIO_TICK, StringComparison.Ordinal))
            {
                ScenarioTickPayload tickPayload;
                string error;
                if (ScenarioTickPayload.TryParse(envelope.payload, out tickPayload, out error))
                {
                    LogScenarioTick(tickPayload);
                }
                else
                {
                    Debug.LogWarning("[TelemetryLogger] Ignoring malformed SCENARIO_TICK payload: " + error);
                }
            }
            else if (string.Equals(envelope.type, JsonEnvelope.SCENARIO_END, StringComparison.Ordinal))
            {
                ScenarioEndPayload endPayload;
                string error;
                if (ScenarioEndPayload.TryParse(envelope.payload, out endPayload, out error))
                {
                    EndSession(endPayload.reason);
                }
                else
                {
                    EndSession("END_RECEIVED");
                }
            }
        }

        /// <summary>
        /// Resolves the absolute directory path where session NDJSON files are stored.
        /// Defaults to %AppData%\TRISHUL-SIM\sessions\.
        /// </summary>
        public string GetSessionsDirectory()
        {
            if (!string.IsNullOrWhiteSpace(sessionsDirectoryOverride))
                return sessionsDirectoryOverride;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
                appData = Directory.GetCurrentDirectory();

            return Path.Combine(appData, "TRISHUL-SIM", "sessions");
        }

        /// <summary>
        /// Begins a new telemetry logging session on SCENARIO_START.
        /// Creates sessions/&lt;sessionId&gt;.ndjson file with UTF-8 encoding.
        /// </summary>
        public void StartSession(string sessionId, string scenarioId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                sessionId = "sess-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);

            lock (_writeLock)
            {
                if (_writer != null)
                {
                    EndSession("NEW_SESSION_PREEMPT");
                }

                CurrentSessionId = sessionId;
                CurrentScenarioId = !string.IsNullOrWhiteSpace(scenarioId) ? scenarioId : "punjab-fog-pincer-01";
                _tickCount = 0;

                try
                {
                    string dir = GetSessionsDirectory();
                    Directory.CreateDirectory(dir);
                    CurrentSessionFilePath = Path.Combine(dir, sessionId + ".ndjson");

                    _fileStream = new FileStream(CurrentSessionFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    _writer = new StreamWriter(_fileStream, Encoding.UTF8) { AutoFlush = false };
                    _isSessionActive = true;

                    Debug.Log("[TelemetryLogger] Created session file: " + CurrentSessionFilePath);

                    Action<string, string> handler = OnSessionStarted;
                    if (handler != null)
                        handler(CurrentSessionId, CurrentSessionFilePath);
                }
                catch (Exception ex)
                {
                    _writer = null;
                    _fileStream = null;
                    _isSessionActive = false;
                    Debug.LogError("[TelemetryLogger] Failed to create session file: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Logs a single SCENARIO_TICK payload, merging active trainee input stub if present.
        /// </summary>
        public void LogScenarioTick(ScenarioTickPayload tick)
        {
            if (!_isSessionActive || _writer == null || tick == null)
                return;

            string scId = !string.IsNullOrEmpty(CurrentScenarioId) ? CurrentScenarioId : "punjab-fog-pincer-01";
            TelemetryRecord record = TelemetryRecord.FromTickPayload(tick, scId);

            if (inputStub != null && inputStub.CurrentTraineeAction != null)
            {
                record.traineeAction = inputStub.CurrentTraineeAction.Clone();
            }

            LogRecord(record);
        }

        /// <summary>
        /// Appends a TelemetryRecord line to the session NDJSON file.
        /// Batches disk flush every 10 ticks (1s) to keep disk write overhead low.
        /// </summary>
        public void LogRecord(TelemetryRecord record)
        {
            if (!_isSessionActive || _writer == null || record == null)
                return;

            lock (_writeLock)
            {
                if (_writer == null)
                    return;

                try
                {
                    string line = record.ToJson();
                    _writer.WriteLine(line);
                    _tickCount++;

                    if (autoFlushEvery10Ticks && (_tickCount % DefaultFlushIntervalTicks == 0))
                    {
                        _writer.Flush();
                    }

                    Action<TelemetryRecord> handler = OnRecordLogged;
                    if (handler != null)
                        handler(record);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[TelemetryLogger] IOException during telemetry write — continuing in partial mode: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Concludes telemetry logging on SCENARIO_END, flushes all remaining buffers,
        /// closes the file handle, and logs the session file path.
        /// </summary>
        public void EndSession(string reason = null)
        {
            lock (_writeLock)
            {
                if (_writer != null)
                {
                    try
                    {
                        _writer.Flush();
                        _writer.Close();
                        _writer.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[TelemetryLogger] Error closing telemetry file: " + ex.Message);
                    }
                    finally
                    {
                        _writer = null;
                        _fileStream = null;
                    }
                }

                _isSessionActive = false;
                string path = CurrentSessionFilePath;
                int count = _tickCount;

                Debug.Log("[TelemetryLogger] SCENARIO_END — session closed: " + path + " (ticks: " + count + ", reason: " + (reason ?? "NORMAL") + ")");

                Action<string, int> handler = OnSessionClosed;
                if (handler != null)
                    handler(path, count);
            }
        }
    }
}
