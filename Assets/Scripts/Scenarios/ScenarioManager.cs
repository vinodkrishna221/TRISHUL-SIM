using System;
using System.Collections;
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
    /// Trainee Cockpit scene transition + 1s ACK handling (US-P0-B), and the 10 Hz
    /// SCENARIO_TICK + SCENARIO_END simulation loop (US-P0-C).
    /// Implements board/contracts/scenario-events.md v1 (SCENARIO_START, SCENARIO_TICK, SCENARIO_END).
    /// </summary>
    public class ScenarioManager : MonoBehaviour
    {
        public const string DefaultTraineeSceneName = "TraineeCockpit";
        public const int DefaultAckTimeoutMs = 1000;
        public const float DefaultTickIntervalSeconds = 0.1f;
        public const int DefaultTickStepMs = 100;
        public const float ExclusionZoneRadiusMeters = 20.0f;

        public static ScenarioManager Instance { get; private set; }

        [SerializeField] private WsServer server;
        [SerializeField] private WsClient client;
        [SerializeField] private Dropdown scenarioDropdown;
        [SerializeField] private Button launchButton;
        [SerializeField] private Button abortButton;
        [SerializeField] private GameObject traineeCockpitRoot;
        [SerializeField] private string traineeCockpitSceneName = DefaultTraineeSceneName;
        [SerializeField] private bool loadSceneOnScenarioStart = true;
        [SerializeField] private int ackTimeoutMs = DefaultAckTimeoutMs;
        [SerializeField] private float tickIntervalSeconds = DefaultTickIntervalSeconds;
        [SerializeField] private int tickStepMs = DefaultTickStepMs;
        [SerializeField] private bool autoStartTickLoop = true;

        private readonly List<ScenarioConfig> _scenarios = new List<ScenarioConfig>();
        private readonly object _tickStateLock = new object();
        private WsServer _subscribedServer;
        private WsClient _subscribedClient;
        private Button _subscribedButton;
        private Button _subscribedAbortButton;
        private Coroutine _tickCoroutine;
        private string _scenariosDirectoryOverride;
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
            SubscribeAbortButton();
        }

        public void OnDestroy()
        {
            StopTickLoop();
            UnsubscribeServer();
            UnsubscribeClient();
            UnsubscribeButton();
            UnsubscribeAbortButton();
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
                _injectedSwarmSnapshot = new SwarmAgentState[0];
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

            if (autoStartTickLoop)
            {
                StartTickLoop();
            }

            return sessionId;
        }

        public void StartTickLoop()
        {
            StopTickLoop();
            if (!_isServerScenarioRunning)
                return;

            _tickCoroutine = StartCoroutine(TickLoop());
        }

        public void StopTickLoop()
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

                StepScenarioTickAsync().GetAwaiter().GetResult();
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

            string terminalReason;
            if (EvaluateTerminalCondition(elapsedMs, snapshotCopy, CurrentScenario, out terminalReason))
            {
                await BroadcastScenarioEndAsync(terminalReason).ConfigureAwait(false);
            }

            return tickPayload;
        }

        public void AbortScenario()
        {
            BroadcastScenarioEnd(ScenarioEndPayload.REASON_INSTRUCTOR_ABORT);
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

            Debug.Log("[WsClient] SCENARIO_TICK received \u2014 sessionId: " + tickPayload.sessionId + ", t: " + tickPayload.t);

            Action<ScenarioTickPayload> handler = OnScenarioTickReceived;
            if (handler != null)
                handler(tickPayload);
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
        }
    }
}
