using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using TrishulSim.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrishulSim.Scenarios
{
    /// <summary>
    /// Helper for asynchronous scene transitions on SCENARIO_START (US-P0-B).
    /// </summary>
    public static class SceneLoader
    {
        public static string LastRequestedScene { get; private set; }
        public static int LoadRequestCount { get; private set; }

        public static void ResetStats()
        {
            LastRequestedScene = null;
            LoadRequestCount = 0;
#if !UNITY_5_3_OR_NEWER
            SceneManager.ResetTestState();
#endif
        }

        public static AsyncOperation LoadAsync(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return null;

            LastRequestedScene = sceneName;
            LoadRequestCount++;
            Debug.Log("[SceneLoader] Loading scene async: " + sceneName);
#if UNITY_5_3_OR_NEWER
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && string.Equals(active.name, sceneName, StringComparison.Ordinal))
            {
                return null;
            }
#endif
            return SceneManager.LoadSceneAsync(sceneName);
        }
    }

    /// <summary>
    /// Manages Scenario Library selection, SCENARIO_START broadcast from Instructor C2,
    /// and Trainee Cockpit scene transition + 1s ACK handling (US-P0-B).
    /// Implements board/contracts/scenario-events.md v1 (SCENARIO_START).
    /// </summary>
    public class ScenarioManager : MonoBehaviour
    {
        public const string DefaultTraineeSceneName = "TraineeCockpit";
        public const int DefaultAckTimeoutMs = 1000;

        public static ScenarioManager Instance { get; private set; }

        [SerializeField] private WsServer server;
        [SerializeField] private WsClient client;
        [SerializeField] private Dropdown scenarioDropdown;
        [SerializeField] private Button launchButton;
        [SerializeField] private GameObject traineeCockpitRoot;
        [SerializeField] private string traineeCockpitSceneName = DefaultTraineeSceneName;
        [SerializeField] private bool loadSceneOnScenarioStart = true;
        [SerializeField] private int ackTimeoutMs = DefaultAckTimeoutMs;

        private readonly List<ScenarioConfig> _scenarios = new List<ScenarioConfig>();
        private WsServer _subscribedServer;
        private WsClient _subscribedClient;
        private Button _subscribedButton;
        private string _scenariosDirectoryOverride;
        private volatile bool _isServerScenarioRunning;
        private volatile bool _isClientScenarioRunning;
        private volatile bool _lastScenarioStartAcked;
        private string _pendingAckSessionId;
        private int _launchGeneration;

        public event Action<ScenarioStartPayload> OnScenarioStartBroadcast;
        public event Action<ScenarioStartPayload> OnScenarioStartReceived;

        public WsServer Server
        {
            get
            {
                if (server != null)
                    return server;
                if (client != null)
                    return null;
                return WsServer.Instance;
            }
            set
            {
                if (!ReferenceEquals(server, value))
                {
                    UnsubscribeServer();
                    server = value;
                    SubscribeServer();
                }
            }
        }

        public WsClient Client
        {
            get
            {
                if (client != null)
                    return client;
                if (server != null)
                    return null;
                return WsClient.Instance;
            }
            set
            {
                if (!ReferenceEquals(client, value))
                {
                    UnsubscribeClient();
                    client = value;
                    SubscribeClient();
                }
            }
        }

        public Dropdown ScenarioDropdown
        {
            get { return scenarioDropdown; }
            set { scenarioDropdown = value; }
        }

        public Button LaunchButton
        {
            get { return launchButton; }
            set
            {
                if (!ReferenceEquals(launchButton, value))
                {
                    UnsubscribeButton();
                    launchButton = value;
                    SubscribeButton();
                }
            }
        }

        public GameObject TraineeCockpitRoot
        {
            get { return traineeCockpitRoot; }
            set { traineeCockpitRoot = value; }
        }

        public string TraineeCockpitSceneName
        {
            get { return traineeCockpitSceneName; }
            set { traineeCockpitSceneName = string.IsNullOrWhiteSpace(value) ? DefaultTraineeSceneName : value; }
        }

        public bool LoadSceneOnScenarioStart
        {
            get { return loadSceneOnScenarioStart; }
            set { loadSceneOnScenarioStart = value; }
        }

        public int AckTimeoutMs
        {
            get { return ackTimeoutMs; }
            set { ackTimeoutMs = Math.Max(50, value); }
        }

        public string ScenariosDirectoryOverride
        {
            get { return _scenariosDirectoryOverride; }
            set { _scenariosDirectoryOverride = value; }
        }

        public IReadOnlyList<ScenarioConfig> Scenarios
        {
            get { return _scenarios; }
        }

        public ScenarioConfig SelectedScenario
        {
            get
            {
                if (_scenarios.Count == 0)
                    return null;

                int index = 0;
                if (scenarioDropdown != null)
                {
                    index = scenarioDropdown.value;
                    if (index < 0 || index >= _scenarios.Count)
                        index = 0;
                }
                return _scenarios[index];
            }
        }

        public string CurrentSessionId { get; private set; }
        public ScenarioConfig CurrentScenario { get; private set; }
        public long CurrentStartedAtMs { get; private set; }

        public bool IsScenarioRunning
        {
            get { return _isServerScenarioRunning || _isClientScenarioRunning; }
        }

        public bool LastScenarioStartAcked
        {
            get { return _lastScenarioStartAcked; }
        }

        public static string FormatSessionId(DateTime timestamp)
        {
            return "sess-" + timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
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

            RefreshScenarioLibrary();
            SubscribeServer();
            SubscribeClient();
            SubscribeButton();
        }

        public void OnDestroy()
        {
            UnsubscribeServer();
            UnsubscribeClient();
            UnsubscribeButton();
            if (ReferenceEquals(Instance, this))
                Instance = null;
        }

        public List<ScenarioConfig> RefreshScenarioLibrary()
        {
            List<ScenarioConfig> loaded = ScenarioLibrary.LoadAll(_scenariosDirectoryOverride, true);
            _scenarios.Clear();
            _scenarios.AddRange(loaded);

            if (scenarioDropdown != null)
            {
                scenarioDropdown.ClearOptions();
                List<string> optionNames = new List<string>(_scenarios.Count);
                for (int i = 0; i < _scenarios.Count; i++)
                {
                    optionNames.Add(_scenarios[i].name);
                }
                scenarioDropdown.AddOptions(optionNames);
                scenarioDropdown.value = 0;
                scenarioDropdown.RefreshShownValue();
            }

            return new List<ScenarioConfig>(_scenarios);
        }

        public bool SelectScenarioByName(string scenarioName)
        {
            if (string.IsNullOrWhiteSpace(scenarioName))
                return false;

            for (int i = 0; i < _scenarios.Count; i++)
            {
                if (string.Equals(_scenarios[i].name, scenarioName, StringComparison.Ordinal) ||
                    string.Equals(_scenarios[i].id, scenarioName, StringComparison.Ordinal))
                {
                    if (scenarioDropdown != null)
                        scenarioDropdown.value = i;
                    return true;
                }
            }
            return false;
        }

        public void ResetScenarioState()
        {
            _isServerScenarioRunning = false;
            _isClientScenarioRunning = false;
            _pendingAckSessionId = null;
            Interlocked.Increment(ref _launchGeneration);
        }

        public void LaunchSelectedScenario()
        {
            ScenarioConfig selected = SelectedScenario;
            if (selected == null)
            {
                RefreshScenarioLibrary();
                selected = SelectedScenario;
            }

            if (selected == null)
            {
                Debug.LogWarning("[ScenarioManager] Cannot launch scenario: ScenarioLibrary is empty.");
                return;
            }

            if (_launchGeneration > 0)
            {
                selected.seed = ScenarioLibrary.GenerateProceduralSeed();
            }

            LaunchScenario(selected);
        }

        public string LaunchScenario(ScenarioConfig config)
        {
            return LaunchScenarioAsync(config).GetAwaiter().GetResult();
        }

        public async Task<string> LaunchScenarioAsync(ScenarioConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[ScenarioManager] LaunchScenario called with null ScenarioConfig.");
                return null;
            }

            SubscribeServer();

            ScenarioConfig launchConfig = config.Clone();
            if (launchConfig.seed <= 0)
            {
                launchConfig.seed = ScenarioLibrary.GenerateProceduralSeed();
            }

            long startedAtMs = JsonEnvelope.NowUnixMs();
            string sessionId = FormatSessionId(DateTime.Now);
            JsonEnvelope envelope = ScenarioStartPayload.CreateEnvelope(sessionId, launchConfig, startedAtMs);

            CurrentSessionId = sessionId;
            CurrentScenario = launchConfig;
            CurrentStartedAtMs = startedAtMs;
            _isServerScenarioRunning = true;
            _lastScenarioStartAcked = false;
            _pendingAckSessionId = sessionId;
            int generation = Interlocked.Increment(ref _launchGeneration);

            WsServer activeServer = Server;
            if (activeServer != null && activeServer.IsRunning)
            {
                await activeServer.BroadcastAsync(envelope.ToJson()).ConfigureAwait(false);
                Debug.Log("[ScenarioManager] SCENARIO_START broadcast \u2014 sessionId: " + sessionId +
                          ", scenarioId: " + launchConfig.id + ", seed: " + launchConfig.seed);
                Task _ = MonitorAckTimeoutAsync(sessionId, generation, ackTimeoutMs);
            }
            else
            {
                Debug.LogWarning("[ScenarioManager] WsServer is not running; SCENARIO_START prepared for sessionId: " + sessionId);
            }

            ScenarioStartPayload payload = new ScenarioStartPayload
            {
                sessionId = sessionId,
                scenarioId = launchConfig.id,
                seed = launchConfig.seed,
                startedAtMs = startedAtMs,
                scenarioConfig = launchConfig
            };

            Action<ScenarioStartPayload> broadcastHandler = OnScenarioStartBroadcast;
            if (broadcastHandler != null)
                broadcastHandler(payload);

            return sessionId;
        }

        private async Task MonitorAckTimeoutAsync(string sessionId, int generation, int timeoutMs)
        {
            try
            {
                await Task.Delay(timeoutMs).ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            if (_launchGeneration != generation)
                return;

            if (!_lastScenarioStartAcked && string.Equals(_pendingAckSessionId, sessionId, StringComparison.Ordinal))
            {
                Debug.LogWarning("[ScenarioManager] Trainee Cockpit did not ACK SCENARIO_START within " +
                                 timeoutMs + "ms (sessionId: " + sessionId + ")");
            }
        }

        private void SubscribeServer()
        {
            WsServer target = Server;
            if (ReferenceEquals(_subscribedServer, target))
                return;

            UnsubscribeServer();
            if (target != null)
            {
                target.OnMessageReceived += HandleServerMessageReceived;
                _subscribedServer = target;
            }
        }

        private void UnsubscribeServer()
        {
            if (_subscribedServer != null)
            {
                _subscribedServer.OnMessageReceived -= HandleServerMessageReceived;
                _subscribedServer = null;
            }
        }

        private void SubscribeClient()
        {
            WsClient target = Client;
            if (ReferenceEquals(_subscribedClient, target))
                return;

            UnsubscribeClient();
            if (target != null)
            {
                target.OnMessageReceived += HandleClientMessageReceived;
                _subscribedClient = target;
            }
        }

        private void UnsubscribeClient()
        {
            if (_subscribedClient != null)
            {
                _subscribedClient.OnMessageReceived -= HandleClientMessageReceived;
                _subscribedClient = null;
            }
        }

        private void SubscribeButton()
        {
            if (ReferenceEquals(_subscribedButton, launchButton))
                return;

            UnsubscribeButton();
            if (launchButton != null && launchButton.onClick != null)
            {
                launchButton.onClick.AddListener(LaunchSelectedScenario);
                _subscribedButton = launchButton;
            }
        }

        private void UnsubscribeButton()
        {
            if (_subscribedButton != null && _subscribedButton.onClick != null)
            {
                _subscribedButton.onClick.RemoveListener(LaunchSelectedScenario);
                _subscribedButton = null;
            }
        }

        private void HandleServerMessageReceived(JsonEnvelope envelope)
        {
            if (_pendingAckSessionId == null)
                return;

            if (string.Equals(envelope.type, JsonEnvelope.CLIENT_HEARTBEAT, StringComparison.Ordinal) ||
                string.Equals(envelope.type, JsonEnvelope.CLIENT_READY, StringComparison.Ordinal))
            {
                _lastScenarioStartAcked = true;
            }
        }

        private void HandleClientMessageReceived(JsonEnvelope envelope)
        {
            if (!string.Equals(envelope.type, JsonEnvelope.SCENARIO_START, StringComparison.Ordinal))
                return;

            WsClient activeClient = Client;

            ScenarioStartPayload startPayload;
            string parseError;
            if (!ScenarioStartPayload.TryParse(envelope.payload, out startPayload, out parseError))
            {
                Debug.LogWarning("[ScenarioManager] Invalid SCENARIO_START payload: " + parseError);
                if (activeClient != null && activeClient.IsConnected)
                {
                    JsonEnvelope errEnv = JsonEnvelope.CreateError(JsonEnvelope.ERR_PAYLOAD_INVALID, parseError, false);
                    Task _ = activeClient.SendEnvelopeAsync(errEnv);
                }
                return;
            }

            // Per board/contracts/websocket-ipc.md §2: second SCENARIO_START received mid-run is ignored with non-fatal ERROR.
            if (_isClientScenarioRunning)
            {
                Debug.LogWarning("[ScenarioManager] Second SCENARIO_START received mid-run (ignored) for sessionId: " + startPayload.sessionId);
                if (activeClient != null && activeClient.IsConnected)
                {
                    JsonEnvelope errEnv = JsonEnvelope.CreateError(
                        JsonEnvelope.ERR_SCENARIO_ALREADY_RUNNING,
                        "Second SCENARIO_START received mid-run (ignored).",
                        false);
                    Task _ = activeClient.SendEnvelopeAsync(errEnv);
                }
                return;
            }

            // Per board/contracts/websocket-ipc.md §2: verify scenarioId exists on client.
            if (!ScenarioLibrary.Exists(startPayload.scenarioId, _scenariosDirectoryOverride))
            {
                string msg = "scenarioId '" + startPayload.scenarioId + "' in SCENARIO_START not present on client.";
                Debug.LogError("[ScenarioManager] " + msg);
                if (activeClient != null && activeClient.IsConnected)
                {
                    JsonEnvelope errEnv = JsonEnvelope.CreateError(JsonEnvelope.ERR_SCENARIO_NOT_FOUND, msg, true);
                    Task _ = activeClient.SendEnvelopeAsync(errEnv);
                }
                return;
            }

            CurrentSessionId = startPayload.sessionId;
            CurrentScenario = startPayload.scenarioConfig;
            CurrentStartedAtMs = startPayload.startedAtMs;
            _isClientScenarioRunning = true;

            Debug.Log("[WsClient] SCENARIO_START received \u2014 sessionId: " + startPayload.sessionId);

            // Immediate ACK within 1s per board/contracts/scenario-events.md using registered CLIENT_HEARTBEAT frame.
            if (activeClient != null && activeClient.IsConnected)
            {
                JsonEnvelope ackHeartbeat = JsonEnvelope.CreateClientHeartbeat();
                Task _ = activeClient.SendEnvelopeAsync(ackHeartbeat);
            }

            if (traineeCockpitRoot != null)
            {
                traineeCockpitRoot.SetActive(true);
            }

            if (loadSceneOnScenarioStart && !string.IsNullOrWhiteSpace(traineeCockpitSceneName))
            {
                SceneLoader.LoadAsync(traineeCockpitSceneName);
            }

            Action<ScenarioStartPayload> handler = OnScenarioStartReceived;
            if (handler != null)
                handler(startPayload);
        }
    }
}
