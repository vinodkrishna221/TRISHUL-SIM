using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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
    /// Trainee Cockpit scene transition + 1s ACK handling (US-P0-B), and the 10 Hz
    /// SCENARIO_TICK + SCENARIO_END simulation loop (US-P0-C).
    /// Implements board/contracts/scenario-events.md v1 (SCENARIO_START, SCENARIO_TICK, SCENARIO_END).
    /// </summary>
    public enum OperationMode
    {
        TwoLaptop = 0,
        SingleLaptopSplit = 1,
        VRMode = 2
    }

    /// <summary>
    /// Manages Scenario Library selection, SCENARIO_START broadcast from Instructor C2,
    /// Trainee Cockpit scene transition + 1s ACK handling (US-P0-B), the 10 Hz
    /// SCENARIO_TICK + SCENARIO_END simulation loop (US-P0-C), and the Single-Laptop
    /// split-screen fallback mode (US-P1-A).
    /// Implements board/contracts/scenario-events.md v1 (SCENARIO_START, SCENARIO_TICK, SCENARIO_END)
    /// and board/contracts/scorecard.md v1 (SCORECARD_PAYLOAD).
    /// </summary>
    public class ScenarioManager : MonoBehaviour
    {
        public const string DefaultTraineeSceneName = "TraineeCockpit";
        public const string DefaultTwoLaptopSceneName = "InstructorC2";
        public const string DefaultSplitScreenSceneName = "SplitScreen";
        public const string DefaultVrSentrySceneName = "VRSentryPost";
        public const string ModeTwoLaptopText = "Two-Laptop";
        public const string ModeSplitScreenText = "Single-Laptop (Split)";
        public const string ModeVrText = "VR Mode (spacedesk)";
        public const int DefaultAckTimeoutMs = 1000;
        public const float DefaultTickIntervalSeconds = 0.1f;
        public const int DefaultTickStepMs = 100;
        public const float ExclusionZoneRadiusMeters = 20.0f;
        public const string DefaultXrReadyStatus = "XR-Ready: Yes (WebXR scaffold)";

        public static ScenarioManager Instance { get; private set; }

        [SerializeField] private WsServer server;
        [SerializeField] private WsClient client;
        [SerializeField] private Dropdown scenarioDropdown;
        [SerializeField] private Dropdown modeDropdown;
        [SerializeField] private Button launchButton;
        [SerializeField] private Button abortButton;
        [SerializeField] private Text systemInfoText;
        [SerializeField] private GameObject traineeCockpitRoot;
        [SerializeField] private string traineeCockpitSceneName = DefaultTraineeSceneName;
        [SerializeField] private string twoLaptopSceneName = DefaultTwoLaptopSceneName;
        [SerializeField] private string splitScreenSceneName = DefaultSplitScreenSceneName;
        [SerializeField] private string vrSentrySceneName = DefaultVrSentrySceneName;
        [SerializeField] private OperationMode currentMode = OperationMode.TwoLaptop;
        [SerializeField] private bool loadSceneOnScenarioStart = true;
        [SerializeField] private int ackTimeoutMs = DefaultAckTimeoutMs;
        [SerializeField] private float tickIntervalSeconds = DefaultTickIntervalSeconds;
        [SerializeField] private int tickStepMs = DefaultTickStepMs;
        [SerializeField] private bool autoStartTickLoop = true;
        [SerializeField] private bool autoLogTelemetryToDisk = true;
        [SerializeField] private bool spawnDefaultSwarmIfEmpty = false;
        [SerializeField] private string telemetryDirectoryOverride;
        [SerializeField] private string xrReadyStatus = DefaultXrReadyStatus;

        private readonly List<ScenarioConfig> _scenarios = new List<ScenarioConfig>();
        private readonly object _tickStateLock = new object();
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
        private readonly object _mainThreadQueueLock = new object();
        private int _mainThreadId;
        private WsServer _subscribedServer;
        private WsClient _subscribedClient;
        private Dropdown _subscribedModeDropdown;
        private Button _subscribedButton;
        private Button _subscribedAbortButton;
        private Coroutine _tickCoroutine;
        private string _scenariosDirectoryOverride;
        private string _sessionTelemetryFilePath;
        private volatile bool _isServerScenarioRunning;
        private volatile bool _isClientScenarioRunning;
        private volatile bool _lastScenarioStartAcked;
        private string _pendingAckSessionId;
        private int _launchGeneration;
        private long _currentElapsedMs;
        private int _tickCount;
        private SwarmAgentState[] _injectedSwarmSnapshot = new SwarmAgentState[0];
        private SensorReadings _injectedSensorReadings = new SensorReadings();
        private string _injectedThreatState = ScenarioTickPayload.THREAT_GREEN;
        private TraineeAction _injectedTraineeAction = new TraineeAction();
        private EWJammerState _injectedEwJammerState = new EWJammerState();
        private bool _injectedFriendlyVhfActive;

        public event Action<ScenarioStartPayload> OnScenarioStartBroadcast;
        public event Action<ScenarioStartPayload> OnScenarioStartReceived;
        public event Action<ScenarioTickPayload> OnScenarioTickBroadcast;
        public event Action<ScenarioTickPayload> OnScenarioTickReceived;
        public event Action<ScenarioEndPayload> OnScenarioEndBroadcast;
        public event Action<ScenarioEndPayload> OnScenarioEndReceived;
        public event Action<OperationMode> OnOperationModeChanged;
        public event Action<OperationMode> OnModeChanged
        {
            add { OnOperationModeChanged += value; }
            remove { OnOperationModeChanged -= value; }
        }
        public event Action<JsonEnvelope> OnScorecardReceived;

        public JsonEnvelope LastScorecardEnvelope { get; private set; }
        public string LastTelemetrySessionFilePath { get; private set; }

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

        public Button AbortButton
        {
            get { return abortButton; }
            set
            {
                if (!ReferenceEquals(abortButton, value))
                {
                    UnsubscribeAbortButton();
                    abortButton = value;
                    SubscribeAbortButton();
                }
            }
        }

        public GameObject TraineeCockpitRoot
        {
            get { return traineeCockpitRoot; }
            set { traineeCockpitRoot = value; }
        }

        public Dropdown ModeDropdown
        {
            get { return modeDropdown; }
            set
            {
                if (!ReferenceEquals(modeDropdown, value))
                {
                    UnsubscribeModeDropdown();
                    modeDropdown = value;
                    SubscribeModeDropdown();
                }
            }
        }

        public OperationMode CurrentMode
        {
            get { return currentMode; }
            set { SetOperationMode(value, notifyListeners: true); }
        }

        public string TwoLaptopSceneName
        {
            get { return twoLaptopSceneName; }
            set { twoLaptopSceneName = string.IsNullOrWhiteSpace(value) ? DefaultTwoLaptopSceneName : value; }
        }

        public string SplitScreenSceneName
        {
            get { return splitScreenSceneName; }
            set { splitScreenSceneName = string.IsNullOrWhiteSpace(value) ? DefaultSplitScreenSceneName : value; }
        }

        public string VrSentrySceneName
        {
            get { return vrSentrySceneName; }
            set { vrSentrySceneName = string.IsNullOrWhiteSpace(value) ? DefaultVrSentrySceneName : value; }
        }

        public bool AutoLogTelemetryToDisk
        {
            get { return autoLogTelemetryToDisk; }
            set { autoLogTelemetryToDisk = value; }
        }

        public string TelemetryDirectoryOverride
        {
            get { return telemetryDirectoryOverride; }
            set { telemetryDirectoryOverride = value; }
        }

        public bool SpawnDefaultSwarmIfEmpty
        {
            get { return spawnDefaultSwarmIfEmpty; }
            set { spawnDefaultSwarmIfEmpty = value; }
        }

        public Text SystemInfoText
        {
            get { return systemInfoText; }
            set
            {
                systemInfoText = value;
                UpdateSystemInfoText();
            }
        }

        public string XrReadyStatus
        {
            get { return string.IsNullOrEmpty(xrReadyStatus) ? DefaultXrReadyStatus : xrReadyStatus; }
            set
            {
                xrReadyStatus = value;
                UpdateSystemInfoText();
            }
        }

        public void UpdateSystemInfoText()
        {
            if (systemInfoText != null)
            {
                systemInfoText.text = XrReadyStatus;
            }
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

        public float TickIntervalSeconds
        {
            get { return tickIntervalSeconds; }
            set { tickIntervalSeconds = Math.Max(0.001f, value); }
        }

        public int TickStepMs
        {
            get { return tickStepMs; }
            set { tickStepMs = Math.Max(1, value); }
        }

        public bool AutoStartTickLoop
        {
            get { return autoStartTickLoop; }
            set { autoStartTickLoop = value; }
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

        public long CurrentElapsedMs
        {
            get { lock (_tickStateLock) { return _currentElapsedMs; } }
        }

        public int TickCount
        {
            get { lock (_tickStateLock) { return _tickCount; } }
        }

        public string LastScenarioEndReason { get; private set; }
        public ScenarioTickPayload LastTickPayload { get; private set; }
        public ScenarioEndPayload LastEndPayload { get; private set; }

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

#if UNITY_5_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnforcePerformanceAndFramerateCaps()
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureEventSystemAfterSceneLoad()
        {
            EnsureEventSystem();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => EnsureEventSystem();
        }

        public static void EnsureEventSystem()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (existing == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                UnityEngine.Object.DontDestroyOnLoad(esGo);
                existing = esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }

            var sim = existing.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (sim != null)
            {
                sim.enabled = false;
                UnityEngine.Object.Destroy(sim);
            }

            var inputModule = existing.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = existing.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                Debug.Log("[ScenarioManager] Attached InputSystemUIInputModule to EventSystem for Unity New Input System.");
            }
        }

        public class SafeBaseInput : UnityEngine.EventSystems.BaseInput
        {
            public override bool GetButtonDown(string buttonName)
            {
                try { return UnityEngine.Input.GetButtonDown(buttonName); }
                catch (System.InvalidOperationException) { return false; }
            }

            public override bool GetMouseButtonDown(int button)
            {
                try { return UnityEngine.Input.GetMouseButtonDown(button); }
                catch (System.InvalidOperationException)
                {
                    var mouse = UnityEngine.InputSystem.Mouse.current;
                    if (mouse != null)
                    {
                        if (button == 0) return mouse.leftButton.wasPressedThisFrame;
                        if (button == 1) return mouse.rightButton.wasPressedThisFrame;
                        if (button == 2) return mouse.middleButton.wasPressedThisFrame;
                    }
                    return false;
                }
            }

            public override bool GetMouseButtonUp(int button)
            {
                try { return UnityEngine.Input.GetMouseButtonUp(button); }
                catch (System.InvalidOperationException)
                {
                    var mouse = UnityEngine.InputSystem.Mouse.current;
                    if (mouse != null)
                    {
                        if (button == 0) return mouse.leftButton.wasReleasedThisFrame;
                        if (button == 1) return mouse.rightButton.wasReleasedThisFrame;
                        if (button == 2) return mouse.middleButton.wasReleasedThisFrame;
                    }
                    return false;
                }
            }

            public override bool GetMouseButton(int button)
            {
                try { return UnityEngine.Input.GetMouseButton(button); }
                catch (System.InvalidOperationException)
                {
                    var mouse = UnityEngine.InputSystem.Mouse.current;
                    if (mouse != null)
                    {
                        if (button == 0) return mouse.leftButton.isPressed;
                        if (button == 1) return mouse.rightButton.isPressed;
                        if (button == 2) return mouse.middleButton.isPressed;
                    }
                    return false;
                }
            }

            public override Vector2 mousePosition
            {
                get
                {
                    try { return UnityEngine.Input.mousePosition; }
                    catch (System.InvalidOperationException)
                    {
                        var mouse = UnityEngine.InputSystem.Mouse.current;
                        if (mouse != null)
                            return mouse.position.ReadValue();
                        return Vector2.zero;
                    }
                }
            }

            public override Vector2 mouseScrollDelta
            {
                get
                {
                    try { return UnityEngine.Input.mouseScrollDelta; }
                    catch (System.InvalidOperationException)
                    {
                        var mouse = UnityEngine.InputSystem.Mouse.current;
                        if (mouse != null)
                            return mouse.scroll.ReadValue();
                        return Vector2.zero;
                    }
                }
            }

            public override float GetAxisRaw(string axisName)
            {
                try { return UnityEngine.Input.GetAxisRaw(axisName); }
                catch (System.InvalidOperationException) { return 0f; }
            }

            public override bool touchSupported
            {
                get
                {
                    try { return UnityEngine.Input.touchSupported; }
                    catch (System.InvalidOperationException) { return false; }
                }
            }

            public override int touchCount
            {
                get
                {
                    try { return UnityEngine.Input.touchCount; }
                    catch (System.InvalidOperationException) { return 0; }
                }
            }
        }
#endif

        public void EnqueueMainThread(Action action)
        {
            if (action == null)
                return;

#if !UNITY_5_3_OR_NEWER
            action();
            return;
#else
            if (_mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                action();
                return;
            }

            lock (_mainThreadQueueLock)
            {
                _mainThreadQueue.Enqueue(action);
            }
#endif
        }

        public void Update()
        {
            DrainMainThreadQueue();
        }

        public int DrainMainThreadQueue()
        {
            int executed = 0;
            while (true)
            {
                Action next = null;
                lock (_mainThreadQueueLock)
                {
                    if (_mainThreadQueue.Count > 0)
                        next = _mainThreadQueue.Dequeue();
                }

                if (next == null)
                    break;

                executed++;
                try
                {
                    next();
                }
                catch (Exception ex)
                {
                    Debug.LogError("[ScenarioManager] Main thread queue error: " + ex.Message);
                }
            }
            return executed;
        }

        private Text _swarmTelemetryText;
        private GameObject _scorecardPanel;
        private Text _scorecardText;

        private void LocateSplitScreenUI()
        {
#if UNITY_5_3_OR_NEWER
            if (_swarmTelemetryText == null)
            {
                GameObject hudGo = GameObject.Find("SwarmTelemetryHUD");
                if (hudGo != null)
                    _swarmTelemetryText = hudGo.GetComponent<Text>();
            }

            if (_scorecardPanel == null)
            {
                GameObject scGo = GameObject.Find("CockpitScorecardPanel");
                if (scGo != null)
                {
                    _scorecardPanel = scGo;
                    _scorecardText = scGo.GetComponent<Text>() ?? scGo.GetComponentInChildren<Text>(true);
                }
            }

            GameObject connGo = GameObject.Find("ConnectionStatusText");
            if (connGo != null && connGo.GetComponent<ConnectionStatusUI>() == null)
            {
                ConnectionStatusUI csUI = connGo.AddComponent<ConnectionStatusUI>();
                csUI.StatusText = connGo.GetComponent<Text>();
                csUI.Server = Server;
            }

            EnsureButtonsInteractable();
#endif
        }

        public void EnsureButtonsInteractable()
        {
#if UNITY_5_3_OR_NEWER
            if (launchButton == null)
            {
                GameObject launchGo = GameObject.Find("LaunchButton");
                if (launchGo != null)
                    launchButton = launchGo.GetComponent<Button>();
            }
            if (abortButton == null)
            {
                GameObject abortGo = GameObject.Find("AbortButton");
                if (abortGo != null)
                    abortButton = abortGo.GetComponent<Button>();
            }

            if (launchButton != null)
            {
                var img = launchButton.GetComponent<Image>();
                if (img == null)
                {
                    img = launchButton.gameObject.AddComponent<Image>();
                    img.color = new Color(0.18f, 0.45f, 0.28f, 0.95f); // MIL-STD 2525 Dark Green
                }
                img.raycastTarget = true;
                launchButton.targetGraphic = img;
                launchButton.interactable = true;
            }

            if (abortButton != null)
            {
                var img = abortButton.GetComponent<Image>();
                if (img == null)
                {
                    img = abortButton.gameObject.AddComponent<Image>();
                    img.color = new Color(0.55f, 0.18f, 0.18f, 0.95f); // Crimson Red
                }
                img.raycastTarget = true;
                abortButton.targetGraphic = img;
                abortButton.interactable = true;
            }

            SubscribeButton();
            SubscribeAbortButton();
#endif
        }

        private void UpdateTelemetryHUD(ScenarioTickPayload tick)
        {
#if UNITY_5_3_OR_NEWER
            EnqueueMainThread(() =>
            {
                if (_swarmTelemetryText == null)
                    LocateSplitScreenUI();

                if (_swarmTelemetryText != null && tick != null)
                {
                    int activeCount = 0;
                    int neutralisedCount = 0;
                    if (tick.swarmSnapshot != null)
                    {
                        for (int i = 0; i < tick.swarmSnapshot.Length; i++)
                        {
                            if (tick.swarmSnapshot[i].isNeutralised)
                                neutralisedCount++;
                            else
                                activeCount++;
                        }
                    }

                    string ewStatus = (tick.ewJammerState != null && tick.ewJammerState.engaged) ? "ENGAGED" : "STANDBY";
                    _swarmTelemetryText.text = string.Format(
                        "T: {0:F1}s | THREAT: {1} | SWARM: {2} ACTIVE ({3} NEUT)\nRF: {4:F3} GHz | DOPPLER: {5:F0} Hz | EW: {6}",
                        tick.t / 1000f,
                        tick.threatState,
                        activeCount,
                        neutralisedCount,
                        tick.sensorReadings != null ? tick.sensorReadings.rfWaterfallPeakGHz : 5.8f,
                        tick.sensorReadings != null ? tick.sensorReadings.dopplerPeakHz : 1140f,
                        ewStatus
                    );
                }
            });
#endif
        }

        private void UpdateScorecardHUD(ScenarioEndPayload endPayload)
        {
#if UNITY_5_3_OR_NEWER
            EnqueueMainThread(() =>
            {
                if (_scorecardPanel == null)
                    LocateSplitScreenUI();

                if (_scorecardPanel != null)
                    _scorecardPanel.SetActive(true);

                if (_scorecardText != null && endPayload != null)
                {
                    _scorecardText.text = string.Format(
                        "=== AFTER ACTION REVIEW SCORECARD ===\n" +
                        "Session: {0}\n" +
                        "Reason: {1}\n" +
                        "Duration: {2:F1}s\n" +
                        "Status: MISSION CONCLUDED",
                        endPayload.sessionId,
                        endPayload.reason,
                        endPayload.t / 1000f
                    );
                }
            });
#endif
        }

        public void Awake()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            if (Instance == null || (Server != null && Instance.Server == null))
                Instance = this;

#if UNITY_5_3_OR_NEWER
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;
            EnsureEventSystem();
            LocateSplitScreenUI();
#endif
        }

        public void Start()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            if (Instance == null || (Server != null && Instance.Server == null))
                Instance = this;

#if UNITY_5_3_OR_NEWER
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;
            EnsureEventSystem();
            LocateSplitScreenUI();
#endif

            RefreshScenarioLibrary();
            UpdateSystemInfoText();
            SubscribeServer();
            SubscribeClient();
            SubscribeButton();
            SubscribeAbortButton();
            SubscribeModeDropdown();
        }

        public void OnDestroy()
        {
            StopTickLoop();
            UnsubscribeServer();
            UnsubscribeClient();
            UnsubscribeButton();
            UnsubscribeAbortButton();
            UnsubscribeModeDropdown();
            if (ReferenceEquals(Instance, this))
                Instance = null;
        }

        public static string FormatScenarioOption(ScenarioConfig config)
        {
            if (config == null)
                return string.Empty;
            if (string.IsNullOrWhiteSpace(config.nameHindi))
                return config.name ?? string.Empty;
            return (config.name ?? string.Empty) + " / " + config.nameHindi;
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
                    optionNames.Add(FormatScenarioOption(_scenarios[i]));
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
                ScenarioConfig cfg = _scenarios[i];
                if (string.Equals(cfg.name, scenarioName, StringComparison.Ordinal) ||
                    string.Equals(cfg.id, scenarioName, StringComparison.Ordinal) ||
                    string.Equals(cfg.nameHindi, scenarioName, StringComparison.Ordinal) ||
                    string.Equals(FormatScenarioOption(cfg), scenarioName, StringComparison.Ordinal))
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
            StopTickLoop();
            _isServerScenarioRunning = false;
            _isClientScenarioRunning = false;
            _pendingAckSessionId = null;
            lock (_tickStateLock)
            {
                _currentElapsedMs = 0;
                _tickCount = 0;
                _injectedSwarmSnapshot = new SwarmAgentState[0];
                _injectedSensorReadings = new SensorReadings();
                _injectedThreatState = ScenarioTickPayload.THREAT_GREEN;
                _injectedTraineeAction = new TraineeAction();
                _injectedEwJammerState = new EWJammerState();
                _injectedFriendlyVhfActive = false;
            }
            Interlocked.Increment(ref _launchGeneration);
        }

        /// <summary>
        /// Stub interface for swarm-ai to inject current agent states for the next SCENARIO_TICK (US-P0-C).
        /// </summary>
        public void InjectSwarmSnapshot(SwarmAgentState[] s)
        {
            lock (_tickStateLock)
            {
                _injectedSwarmSnapshot = SwarmAgentState.CloneArray(s);
            }
        }

        public void InjectSensorReadings(SensorReadings readings)
        {
            lock (_tickStateLock)
            {
                _injectedSensorReadings = readings != null ? readings.Clone() : new SensorReadings();
            }
        }

        public void InjectThreatState(string threatState)
        {
            lock (_tickStateLock)
            {
                _injectedThreatState = ScenarioTickPayload.IsValidThreatState(threatState)
                    ? threatState
                    : ScenarioTickPayload.THREAT_GREEN;
            }
        }

        public void InjectTraineeAction(TraineeAction action)
        {
            lock (_tickStateLock)
            {
                _injectedTraineeAction = action != null ? action.Clone() : new TraineeAction();
            }
        }

        public void InjectEWJammerState(EWJammerState state)
        {
            lock (_tickStateLock)
            {
                _injectedEwJammerState = state != null ? state.Clone() : new EWJammerState();
            }
        }

        public void InjectFriendlyVhfActive(bool active)
        {
            lock (_tickStateLock)
            {
                _injectedFriendlyVhfActive = active;
            }
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

            Task _ = LaunchScenarioAsync(selected);
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

            StopTickLoop();
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
            LastScenarioEndReason = null;
            lock (_tickStateLock)
            {
                _currentElapsedMs = 0;
                _tickCount = 0;
                if (currentMode == OperationMode.SingleLaptopSplit || spawnDefaultSwarmIfEmpty)
                {
                    _injectedSwarmSnapshot = CreateDefaultBoidSwarm(launchConfig);
                }
                else
                {
                    _injectedSwarmSnapshot = new SwarmAgentState[0];
                }
                _injectedSensorReadings = new SensorReadings();
                _injectedThreatState = ScenarioTickPayload.THREAT_GREEN;
                _injectedTraineeAction = new TraineeAction();
                _injectedEwJammerState = new EWJammerState();
                _injectedFriendlyVhfActive = false;
            }
            _isServerScenarioRunning = true;
            _lastScenarioStartAcked = false;
            _pendingAckSessionId = sessionId;
            int generation = Interlocked.Increment(ref _launchGeneration);

            if (autoLogTelemetryToDisk)
            {
                InitSessionTelemetryFile(sessionId);
            }

#if UNITY_5_3_OR_NEWER
            EnqueueMainThread(() =>
            {
                if (_scorecardPanel != null)
                    _scorecardPanel.SetActive(false);
                if (_swarmTelemetryText != null)
                    _swarmTelemetryText.text = "LAUNCHING: " + launchConfig.name + " (" + (launchConfig.swarmProfile != null ? launchConfig.swarmProfile.agentCount : 6) + " AGENTS)...";
            });
#endif

            WsServer activeServer = Server;
            string swarmSummary = launchConfig.swarmProfile != null
                ? "tactic=" + launchConfig.swarmProfile.tacticTree + ", agents=" + launchConfig.swarmProfile.agentCount +
                  " (decoys=" + launchConfig.swarmProfile.decoyCount + ", attackers=" + launchConfig.swarmProfile.attackerCount +
                  "), speed=" + launchConfig.swarmProfile.speedKmh + "km/h, alt=" + launchConfig.swarmProfile.altitudeM + "m"
                : "none";

            if (activeServer != null && activeServer.IsRunning)
            {
                await activeServer.BroadcastAsync(envelope.ToJson()).ConfigureAwait(false);
                Debug.Log("[ScenarioManager] SCENARIO_START broadcast \u2014 sessionId: " + sessionId +
                          ", scenarioId: " + launchConfig.id + ", seed: " + launchConfig.seed +
                          ", swarmProfile: " + swarmSummary);
                Task _ = MonitorAckTimeoutAsync(sessionId, generation, ackTimeoutMs);
            }
            else
            {
                Debug.LogWarning("[ScenarioManager] WsServer is not running; SCENARIO_START prepared for sessionId: " + sessionId +
                                 ", swarmProfile: " + swarmSummary);
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

            if (autoStartTickLoop)
            {
                StartTickLoop();
            }

            return sessionId;
        }

        public void StartTickLoop()
        {
            EnqueueMainThread(() =>
            {
                StopTickLoopInternal();
                if (!_isServerScenarioRunning)
                    return;

                _tickCoroutine = StartCoroutine(TickLoop());
            });
        }

        public void StopTickLoop()
        {
            EnqueueMainThread(StopTickLoopInternal);
        }

        private void StopTickLoopInternal()
        {
            Coroutine running = _tickCoroutine;
            _tickCoroutine = null;
            if (running != null)
            {
                StopCoroutine(running);
            }
        }

        /// <summary>
        /// 10 Hz simulation heartbeat coroutine (US-P0-C).
        /// Runs every 0.1 s (WaitForSeconds), increments t by 100 ms, broadcasts SCENARIO_TICK,
        /// and fires SCENARIO_END when terminal conditions are met.
        /// </summary>
        public IEnumerator TickLoop()
        {
            int generation = _launchGeneration;
            while (_isServerScenarioRunning && _launchGeneration == generation)
            {
                yield return new WaitForSeconds(tickIntervalSeconds);
                if (!_isServerScenarioRunning || _launchGeneration != generation)
                    yield break;

                Task<ScenarioTickPayload> tickTask = StepScenarioTickAsync();
                while (!tickTask.IsCompleted)
                {
                    yield return null;
                }
            }
        }

        public ScenarioTickPayload StepScenarioTick()
        {
            return StepScenarioTickAsync().GetAwaiter().GetResult();
        }

        public async Task<ScenarioTickPayload> StepScenarioTickAsync()
        {
            if (!_isServerScenarioRunning || string.IsNullOrWhiteSpace(CurrentSessionId))
                return null;

            ScenarioTickPayload tickPayload;
            long elapsedMs;
            SwarmAgentState[] snapshotCopy;

            lock (_tickStateLock)
            {
                _currentElapsedMs += tickStepMs;
                _tickCount++;
                elapsedMs = _currentElapsedMs;
                snapshotCopy = SwarmAgentState.CloneArray(_injectedSwarmSnapshot);

                tickPayload = new ScenarioTickPayload
                {
                    sessionId = CurrentSessionId,
                    t = elapsedMs,
                    swarmSnapshot = snapshotCopy,
                    sensorReadings = _injectedSensorReadings != null ? _injectedSensorReadings.Clone() : new SensorReadings(),
                    threatState = _injectedThreatState ?? ScenarioTickPayload.THREAT_GREEN,
                    traineeAction = _injectedTraineeAction != null ? _injectedTraineeAction.Clone() : new TraineeAction(),
                    ewJammerState = _injectedEwJammerState != null ? _injectedEwJammerState.Clone() : new EWJammerState(),
                    friendlyVhfActive = _injectedFriendlyVhfActive
                };
            }

            JsonEnvelope tickEnvelope = ScenarioTickPayload.CreateEnvelope(tickPayload);
            LastTickPayload = tickPayload;

            if (autoLogTelemetryToDisk && !string.IsNullOrWhiteSpace(_sessionTelemetryFilePath))
            {
                AppendTelemetryRecord(tickPayload);
            }

            WsServer activeServer = Server;
            if (activeServer != null && activeServer.IsRunning)
            {
                await activeServer.BroadcastAsync(tickEnvelope.ToJson()).ConfigureAwait(false);
            }

            Debug.Log("[ScenarioManager] SCENARIO_TICK broadcast \u2014 sessionId: " + tickPayload.sessionId +
                      ", t: " + tickPayload.t + ", agents: " + tickPayload.swarmSnapshot.Length +
                      ", threatState: " + tickPayload.threatState);

            Action<ScenarioTickPayload> tickHandler = OnScenarioTickBroadcast;
            if (tickHandler != null)
                tickHandler(tickPayload);

#if UNITY_5_3_OR_NEWER
            UpdateTelemetryHUD(tickPayload);
#endif

            string terminalReason;
            if (EvaluateTerminalCondition(elapsedMs, snapshotCopy, CurrentScenario, out terminalReason))
            {
                await BroadcastScenarioEndAsync(terminalReason).ConfigureAwait(false);
            }

            return tickPayload;
        }

        public void AbortScenario()
        {
            Task _ = BroadcastScenarioEndAsync(ScenarioEndPayload.REASON_INSTRUCTOR_ABORT);
        }

        public void BroadcastScenarioEnd(string reason)
        {
            BroadcastScenarioEndAsync(reason).GetAwaiter().GetResult();
        }

        public async Task<ScenarioEndPayload> BroadcastScenarioEndAsync(string reason)
        {
            if (!_isServerScenarioRunning || string.IsNullOrWhiteSpace(CurrentSessionId))
                return null;

            _isServerScenarioRunning = false;
            _pendingAckSessionId = null;
            _sessionTelemetryFilePath = null;
            StopTickLoop();

            string resolvedReason = ScenarioEndPayload.IsValidReason(reason)
                ? reason
                : ScenarioEndPayload.REASON_INSTRUCTOR_ABORT;

            long finalElapsedMs;
            SwarmAgentState[] finalSnapshot;
            lock (_tickStateLock)
            {
                finalElapsedMs = _currentElapsedMs;
                finalSnapshot = SwarmAgentState.CloneArray(_injectedSwarmSnapshot);
            }

            ScenarioEndPayload endPayload = new ScenarioEndPayload
            {
                sessionId = CurrentSessionId,
                t = finalElapsedMs,
                reason = resolvedReason,
                finalSwarmSnapshot = finalSnapshot
            };

            JsonEnvelope endEnvelope = ScenarioEndPayload.CreateEnvelope(
                endPayload.sessionId,
                endPayload.t,
                endPayload.reason,
                endPayload.finalSwarmSnapshot);

            LastScenarioEndReason = resolvedReason;
            LastEndPayload = endPayload;

            WsServer activeServer = Server;
            if (activeServer != null && activeServer.IsRunning)
            {
                await activeServer.BroadcastAsync(endEnvelope.ToJson()).ConfigureAwait(false);
            }

            Debug.Log("[ScenarioManager] SCENARIO_END broadcast \u2014 sessionId: " + endPayload.sessionId +
                      ", t: " + endPayload.t + ", reason: " + endPayload.reason);

            Action<ScenarioEndPayload> endHandler = OnScenarioEndBroadcast;
            if (endHandler != null)
                endHandler(endPayload);

#if UNITY_5_3_OR_NEWER
            UpdateScorecardHUD(endPayload);
#endif

            return endPayload;
        }

        private static bool EvaluateTerminalCondition(
            long elapsedMs,
            SwarmAgentState[] snapshot,
            ScenarioConfig config,
            out string reason)
        {
            reason = null;

            if (snapshot != null && snapshot.Length > 0)
            {
                int attackerTotal = 0;
                int attackerNeutralised = 0;
                float radiusSq = ExclusionZoneRadiusMeters * ExclusionZoneRadiusMeters;

                for (int i = 0; i < snapshot.Length; i++)
                {
                    SwarmAgentState agent = snapshot[i];
                    if (agent == null || agent.isDecoy)
                        continue;

                    attackerTotal++;
                    if (agent.isNeutralised)
                    {
                        attackerNeutralised++;
                    }
                    else
                    {
                        float distSq = (agent.positionWorld.x * agent.positionWorld.x) +
                                       (agent.positionWorld.y * agent.positionWorld.y) +
                                       (agent.positionWorld.z * agent.positionWorld.z);
                        if (distSq <= radiusSq)
                        {
                            reason = ScenarioEndPayload.REASON_EXCLUSION_BREACH;
                            return true;
                        }
                    }
                }

                int expectedAttackers = (config != null && config.swarmProfile != null && config.swarmProfile.attackerCount > 0)
                    ? config.swarmProfile.attackerCount
                    : attackerTotal;

                if (attackerTotal > 0 && attackerNeutralised >= expectedAttackers && attackerNeutralised == attackerTotal)
                {
                    reason = ScenarioEndPayload.REASON_ALL_NEUTRALISED;
                    return true;
                }
            }

            long durationLimitMs = (config != null && config.durationMaxSecs > 0 ? config.durationMaxSecs : 120) * 1000L;
            if (elapsedMs >= durationLimitMs)
            {
                reason = ScenarioEndPayload.REASON_TIME_LIMIT;
                return true;
            }

            return false;
        }

#if !UNITY_5_3_OR_NEWER
        public Coroutine StartCoroutine(IEnumerator routine)
        {
            if (routine == null)
                return null;

            CancellationTokenSource cts = new CancellationTokenSource();
            Coroutine handle = new Coroutine { Cts = cts };
            handle.RunnerTask = RunCoroutineInternalAsync(routine, cts.Token);
            return handle;
        }

        public void StopCoroutine(Coroutine coroutine)
        {
            if (coroutine == null || coroutine.Cts == null)
                return;
            try { coroutine.Cts.Cancel(); } catch { }
        }

        private static async Task RunCoroutineInternalAsync(IEnumerator routine, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && routine.MoveNext())
                {
                    object yielded = routine.Current;
                    WaitForSeconds wfs = yielded as WaitForSeconds;
                    if (wfs != null)
                    {
                        int delayMs = Math.Max(1, (int)Math.Round(wfs.Seconds * 1000.0f));
                        await Task.Delay(delayMs, token).ConfigureAwait(false);
                    }
                    else
                    {
                        await Task.Yield();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError("[ScenarioManager] Coroutine exception: " + ex);
            }
        }
#endif

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

        private void SubscribeAbortButton()
        {
            if (ReferenceEquals(_subscribedAbortButton, abortButton))
                return;

            UnsubscribeAbortButton();
            if (abortButton != null && abortButton.onClick != null)
            {
                abortButton.onClick.AddListener(AbortScenario);
                _subscribedAbortButton = abortButton;
            }
        }

        private void UnsubscribeAbortButton()
        {
            if (_subscribedAbortButton != null && _subscribedAbortButton.onClick != null)
            {
                _subscribedAbortButton.onClick.RemoveListener(AbortScenario);
                _subscribedAbortButton = null;
            }
        }

        private void HandleServerMessageReceived(JsonEnvelope envelope)
        {
            if (string.IsNullOrEmpty(envelope.type))
                return;

            if (string.Equals(envelope.type, JsonEnvelope.SCORECARD_PAYLOAD, StringComparison.Ordinal))
            {
                LastScorecardEnvelope = envelope;
                Action<JsonEnvelope> scHandler = OnScorecardReceived;
                if (scHandler != null)
                    scHandler(envelope);
            }

            if (_pendingAckSessionId != null)
            {
                if (string.Equals(envelope.type, JsonEnvelope.CLIENT_HEARTBEAT, StringComparison.Ordinal) ||
                    string.Equals(envelope.type, JsonEnvelope.CLIENT_READY, StringComparison.Ordinal))
                {
                    _lastScenarioStartAcked = true;
                }
            }
        }

        private void HandleClientMessageReceived(JsonEnvelope envelope)
        {
            if (string.IsNullOrEmpty(envelope.type))
                return;

            if (string.Equals(envelope.type, JsonEnvelope.SCORECARD_PAYLOAD, StringComparison.Ordinal))
            {
                LastScorecardEnvelope = envelope;
                Action<JsonEnvelope> scHandler = OnScorecardReceived;
                if (scHandler != null)
                    scHandler(envelope);
                return;
            }

            if (string.Equals(envelope.type, JsonEnvelope.SCENARIO_START, StringComparison.Ordinal))
            {
                HandleClientScenarioStart(envelope);
                return;
            }

            if (string.Equals(envelope.type, JsonEnvelope.SCENARIO_TICK, StringComparison.Ordinal))
            {
                HandleClientScenarioTick(envelope);
                return;
            }

            if (string.Equals(envelope.type, JsonEnvelope.SCENARIO_END, StringComparison.Ordinal))
            {
                HandleClientScenarioEnd(envelope);
            }
        }

        private void HandleClientScenarioStart(JsonEnvelope envelope)
        {
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
            LastScenarioEndReason = null;
            lock (_tickStateLock)
            {
                _currentElapsedMs = 0;
                _tickCount = 0;
            }
            _isClientScenarioRunning = true;

            if (autoLogTelemetryToDisk && Server == null)
            {
                InitSessionTelemetryFile(startPayload.sessionId);
            }

            string clientSwarmSummary = (startPayload.scenarioConfig != null && startPayload.scenarioConfig.swarmProfile != null)
                ? "tactic=" + startPayload.scenarioConfig.swarmProfile.tacticTree + ", agents=" + startPayload.scenarioConfig.swarmProfile.agentCount +
                  " (decoys=" + startPayload.scenarioConfig.swarmProfile.decoyCount + ", attackers=" + startPayload.scenarioConfig.swarmProfile.attackerCount +
                  "), speed=" + startPayload.scenarioConfig.swarmProfile.speedKmh + "km/h, alt=" + startPayload.scenarioConfig.swarmProfile.altitudeM + "m"
                : "none";

            Debug.Log("[WsClient] SCENARIO_START received \u2014 sessionId: " + startPayload.sessionId +
                      ", scenarioId: " + startPayload.scenarioId +
                      ", swarmProfile: " + clientSwarmSummary);

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

        private void HandleClientScenarioTick(JsonEnvelope envelope)
        {
            WsClient activeClient = Client;
            ScenarioTickPayload tickPayload;
            string parseError;
            if (!ScenarioTickPayload.TryParse(envelope.payload, out tickPayload, out parseError))
            {
                Debug.LogWarning("[ScenarioManager] Invalid SCENARIO_TICK payload: " + parseError);
                if (activeClient != null && activeClient.IsConnected)
                {
                    JsonEnvelope errEnv = JsonEnvelope.CreateError(JsonEnvelope.ERR_PAYLOAD_INVALID, parseError, false);
                    Task _ = activeClient.SendEnvelopeAsync(errEnv);
                }
                return;
            }

            lock (_tickStateLock)
            {
                _currentElapsedMs = tickPayload.t;
                _tickCount++;
            }
            LastTickPayload = tickPayload;

            if (autoLogTelemetryToDisk && Server == null && !string.IsNullOrWhiteSpace(_sessionTelemetryFilePath))
            {
                AppendTelemetryRecord(tickPayload);
            }

            Debug.Log("[WsClient] SCENARIO_TICK received \u2014 sessionId: " + tickPayload.sessionId + ", t: " + tickPayload.t);

            Action<ScenarioTickPayload> handler = OnScenarioTickReceived;
            if (handler != null)
                handler(tickPayload);

#if UNITY_5_3_OR_NEWER
            UpdateTelemetryHUD(tickPayload);
#endif
        }

        private void HandleClientScenarioEnd(JsonEnvelope envelope)
        {
            WsClient activeClient = Client;
            ScenarioEndPayload endPayload;
            string parseError;
            if (!ScenarioEndPayload.TryParse(envelope.payload, out endPayload, out parseError))
            {
                Debug.LogWarning("[ScenarioManager] Invalid SCENARIO_END payload: " + parseError);
                if (activeClient != null && activeClient.IsConnected)
                {
                    JsonEnvelope errEnv = JsonEnvelope.CreateError(JsonEnvelope.ERR_PAYLOAD_INVALID, parseError, false);
                    Task _ = activeClient.SendEnvelopeAsync(errEnv);
                }
                return;
            }

            _isClientScenarioRunning = false;
            _sessionTelemetryFilePath = null;
            lock (_tickStateLock)
            {
                _currentElapsedMs = endPayload.t;
            }
            LastScenarioEndReason = endPayload.reason;
            LastEndPayload = endPayload;

            Debug.Log("[WsClient] SCENARIO_END received \u2014 sessionId: " + endPayload.sessionId +
                      ", t: " + endPayload.t + ", reason: " + endPayload.reason);

            Action<ScenarioEndPayload> handler = OnScenarioEndReceived;
            if (handler != null)
                handler(endPayload);

#if UNITY_5_3_OR_NEWER
            UpdateScorecardHUD(endPayload);
#endif
        }

        public void SetOperationMode(OperationMode mode, bool notifyListeners = true)
        {
            if (currentMode == mode)
                return;

            currentMode = mode;
            if (modeDropdown != null && modeDropdown.value != (int)mode)
            {
                modeDropdown.value = (int)mode;
                modeDropdown.RefreshShownValue();
            }

            Action<OperationMode> handler = OnOperationModeChanged;
            if (handler != null)
                handler(mode);

            if (notifyListeners)
            {
                string targetScene = null;
                if (mode == OperationMode.SingleLaptopSplit)
                    targetScene = splitScreenSceneName;
                else if (mode == OperationMode.VRMode)
                    targetScene = vrSentrySceneName;
                else
                    targetScene = twoLaptopSceneName;

                if (!string.IsNullOrWhiteSpace(targetScene))
                {
                    string modeName = mode == OperationMode.SingleLaptopSplit ? ModeSplitScreenText :
                                      (mode == OperationMode.VRMode ? ModeVrText : ModeTwoLaptopText);
                    Debug.Log("[ScenarioManager] Switching operation mode to " +
                              modeName +
                              " — loading scene: " + targetScene);
                    SceneLoader.LoadAsync(targetScene);
                }
            }
        }

        public void SwitchToSplitScreen()
        {
            SetOperationMode(OperationMode.SingleLaptopSplit, notifyListeners: true);
        }

        public void SwitchToTwoLaptop()
        {
            SetOperationMode(OperationMode.TwoLaptop, notifyListeners: true);
        }

        public void SwitchToVRMode()
        {
            SetOperationMode(OperationMode.VRMode, notifyListeners: true);
        }

        public void SetMode(OperationMode mode)
        {
            SetOperationMode(mode, notifyListeners: true);
        }

        private void SubscribeModeDropdown()
        {
            if (modeDropdown == null)
                return;

            if (modeDropdown.options == null || modeDropdown.options.Count < 2)
            {
                modeDropdown.ClearOptions();
                modeDropdown.AddOptions(new List<string> { ModeTwoLaptopText, ModeSplitScreenText });
            }

            modeDropdown.value = (int)currentMode;
            modeDropdown.RefreshShownValue();

            if (ReferenceEquals(_subscribedModeDropdown, modeDropdown))
                return;

            UnsubscribeModeDropdown();
            modeDropdown.onValueChanged.AddListener(HandleModeDropdownValueChanged);
            _subscribedModeDropdown = modeDropdown;
        }

        private void UnsubscribeModeDropdown()
        {
            if (_subscribedModeDropdown != null)
            {
                _subscribedModeDropdown.onValueChanged.RemoveListener(HandleModeDropdownValueChanged);
                _subscribedModeDropdown = null;
            }
        }

        private void HandleModeDropdownValueChanged(int index)
        {
            OperationMode mode = (OperationMode)index;
            if (mode != currentMode)
            {
                SetOperationMode(mode, notifyListeners: true);
            }
        }

        public static SwarmAgentState[] CreateDefaultBoidSwarm(ScenarioConfig config = null)
        {
            int totalAgents = 6;
            int decoys = 2;
            float freq1 = 5.825f;
            float freq2 = 2.4f;

            if (config != null && config.swarmProfile != null)
            {
                if (config.swarmProfile.agentCount > 0)
                    totalAgents = config.swarmProfile.agentCount;
                decoys = config.swarmProfile.decoyCount;
                if (config.swarmProfile.rfFreqGHz != null && config.swarmProfile.rfFreqGHz.Length > 0)
                {
                    freq1 = config.swarmProfile.rfFreqGHz[0];
                    if (config.swarmProfile.rfFreqGHz.Length > 1)
                        freq2 = config.swarmProfile.rfFreqGHz[1];
                }
            }

            SwarmAgentState[] swarm = new SwarmAgentState[totalAgents];
            for (int i = 0; i < totalAgents; i++)
            {
                bool isDecoy = i < decoys;
                swarm[i] = new SwarmAgentState
                {
                    agentId = "agent-" + i,
                    isDecoy = isDecoy,
                    positionWorld = new Vector3(140.0f + (i * 12.0f), 8.0f + (i * 0.4f), -85.0f + (i * 10.0f)),
                    velocityMs = new Vector3(-28.0f, 0.0f, 12.0f),
                    headingDeg = 247.0f,
                    altitudeM = 8.0f + (i * 0.4f),
                    rfEmitting = true,
                    rfFreqGHz = isDecoy ? freq2 : freq1,
                    rfPowerDbm = isDecoy ? 20.0f : 23.0f,
                    propellerRpm = isDecoy ? 9800f : 11400f,
                    rcs = isDecoy ? 0.08f : 0.012f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                };
            }
            return swarm;
        }

        public async Task SendScorecardPayloadAsync(string scorecardJson)
        {
            JsonEnvelope env = JsonEnvelope.Create(JsonEnvelope.SCORECARD_PAYLOAD, scorecardJson);
            WsClient activeClient = Client;
            if (activeClient != null && activeClient.IsConnected)
            {
                await activeClient.SendEnvelopeAsync(env).ConfigureAwait(false);
            }
            else
            {
                WsServer activeServer = Server;
                if (activeServer != null && activeServer.IsRunning)
                {
                    await activeServer.BroadcastAsync(env.ToJson()).ConfigureAwait(false);
                }
            }
        }

        public string GetTelemetryDirectory()
        {
            if (!string.IsNullOrWhiteSpace(telemetryDirectoryOverride))
                return telemetryDirectoryOverride;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(appData))
                appData = Directory.GetCurrentDirectory();
            return Path.Combine(appData, "TRISHUL-SIM", "sessions");
        }

        private void InitSessionTelemetryFile(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return;

            try
            {
                string dir = GetTelemetryDirectory();
                Directory.CreateDirectory(dir);
                _sessionTelemetryFilePath = Path.Combine(dir, sessionId + ".ndjson");
                using (FileStream fs = new FileStream(_sessionTelemetryFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                {
                }
                LastTelemetrySessionFilePath = _sessionTelemetryFilePath;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ScenarioManager] Could not initialize telemetry file: " + ex.Message);
            }
        }

        private void AppendTelemetryRecord(ScenarioTickPayload tick)
        {
            if (tick == null || string.IsNullOrWhiteSpace(_sessionTelemetryFilePath))
                return;

            try
            {
                using (FileStream fs = new FileStream(_sessionTelemetryFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    sw.WriteLine(tick.ToJson());
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ScenarioManager] Could not append telemetry record: " + ex.Message);
            }
        }
    }
}
