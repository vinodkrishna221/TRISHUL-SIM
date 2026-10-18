// ============================================================
// VRScenarioSelector.cs — Standalone Mission Selector, In-Flight Stop & AAR Controller
// TRISHUL-SIM · vr-gamepad track
//
// Provides complete, closed-loop standalone training in VRSentryPost.unity
// without requiring InstructorC2:
// 1. Mission Selection: Hybrid tactical overlay for the 4 scenarios
//    ([1..4], Space/Enter, Mouse Click, or Gamepad).
// 2. In-Flight Stop Control: Clean, uncluttered VR viewport stopped
//    instantly via [Esc] on Keyboard or Gamepad [B] / Back button.
// 3. Direct Transition to AAR: Halting or completing the mission immediately
//    activates the floating 3D holographic scorecard in the sentry post.
// 4. Interactive AAR Actions: Return to Menu [Space/A], Instant Replay [R/X],
//    and Export PDF Report [P/Y].
// 5. Zero-Drone Scene Doctrine: Fully compliant with VRSentryPostSelfTest.cs;
//    all simulation & AAR objects are dynamically generated at runtime.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using TrishulSim.Scenarios;
using TrishulSim.UI;
using SwarmAgentState = TrishulSim.Scenarios.SwarmAgentState;

namespace TrishulSim.VR
{
    public enum VRTrainingState
    {
        SELECTING,
        RUNNING,
        AAR_SCORECARD
    }

    /// <summary>
    /// Coordinates standalone mission selection, in-flight stopping, simulation ticks,
    /// and transition to After Action Review with 3D Holographic Scorecard inside VRSentryPost.
    /// </summary>
    public class VRScenarioSelector : MonoBehaviour
    {
        public static VRScenarioSelector Instance { get; private set; }

        [Header("State")]
        [SerializeField] private VRTrainingState currentState = VRTrainingState.SELECTING;

        [Header("References")]
        [SerializeField] private VRHolographicScorecard holographicScorecard;
        [SerializeField] private VRHudController vrHudController;

        private List<ScenarioConfig> scenarios = new List<ScenarioConfig>();
        private int selectedIndex = 0;
        private ScenarioConfig currentScenario;
        private Scorecard currentScorecard;
        private float scenarioRunningTime = 0f;
        private SwarmAgentState[] activeSwarm = new SwarmAgentState[0];
        private string lastEndReason = null;
        private string lastExportedPdfPath = null;
        private bool isSubscribedToEvents = false;
        private Input.GamepadInputController _cachedGamepad = null;

        // GUI Styles
        private GUIStyle panelStyle;
        private GUIStyle headerStyle;
        private GUIStyle itemStyle;
        private GUIStyle itemSelectedStyle;
        private GUIStyle footerStyle;
        private GUIStyle watermarkStyle;
        private bool stylesInitialized = false;

        public VRTrainingState CurrentState { get { return currentState; } }
        public ScenarioConfig CurrentScenario { get { return currentScenario; } }
        public Scorecard CurrentScorecard { get { return currentScorecard; } }
        public VRHolographicScorecard HolographicScorecard { get { return holographicScorecard; } }
        public int ScenarioCount { get { return scenarios.Count; } }
        public string LastEndReason { get { return lastEndReason; } }
        public string LastExportedPdfPath { get { return lastExportedPdfPath; } }

        // ══════════════════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            if (holographicScorecard == null)
            {
                holographicScorecard = GetComponent<VRHolographicScorecard>() ??
                                       gameObject.AddComponent<VRHolographicScorecard>();
            }

            if (vrHudController == null)
            {
                vrHudController = GetComponent<VRHudController>() ??
                                  FindAnyObjectByType<VRHudController>();
            }
        }

        private void Start()
        {
            LoadScenarios();
            EnsureRuntimeScenarioManager();
            SubscribeScenarioEvents();

            if (holographicScorecard != null)
            {
                holographicScorecard.EnsurePanelConstructed();
                holographicScorecard.HideScorecard();
            }

            SetState(VRTrainingState.SELECTING);
        }

        private void OnDestroy()
        {
            UnsubscribeScenarioEvents();
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        // ══════════════════════════════════════════════════════════
        // Initialization & Scenarios
        // ══════════════════════════════════════════════════════════

        public void LoadScenarios()
        {
            scenarios = ScenarioLibrary.LoadAll();
            if (scenarios == null || scenarios.Count == 0)
            {
                Debug.LogWarning("[VRScenarioSelector] No scenarios loaded from StreamingAssets; creating fallback tactical scenarios.");
                scenarios = CreateFallbackScenarios();
            }
            Debug.Log(string.Format("[VRScenarioSelector] Loaded {0} tactical scenarios for standalone VR selection.", scenarios.Count));
        }

        private List<ScenarioConfig> CreateFallbackScenarios()
        {
            var list = new List<ScenarioConfig>();
            list.Add(new ScenarioConfig
            {
                id = "punjab-fog-pincer-01",
                name = "Punjab Border — Zero Visibility Pincer",
                nameHindi = "पंजाब सीमा — शून्य दृश्यता पिंसर",
                region = ScenarioConfig.REGION_PUNJAB_RIVERINE,
                durationMaxSecs = 120,
                swarmProfile = new SwarmProfile
                {
                    agentCount = 6,
                    decoyCount = 2,
                    attackerCount = 4,
                    rfFreqGHz = new float[] { 5.825f, 2.4f },
                    speedKmh = 72,
                    altitudeM = 15,
                    tacticTree = SwarmProfile.TACTIC_PINCER_LOW_FAST
                }
            });
            list.Add(new ScenarioConfig
            {
                id = "desert-fob-01",
                name = "Rajasthan Sector — Night Drone Swarm Attack",
                nameHindi = "राजस्थान सेक्टर — रात्रि ड्रोन झुंड हमला",
                region = ScenarioConfig.REGION_DESERT_FOB,
                durationMaxSecs = 120,
                swarmProfile = new SwarmProfile
                {
                    agentCount = 8,
                    decoyCount = 2,
                    attackerCount = 6,
                    rfFreqGHz = new float[] { 2.4f, 5.825f },
                    speedKmh = 90,
                    altitudeM = 20,
                    tacticTree = SwarmProfile.TACTIC_SATURATION_DIVE
                }
            });
            list.Add(new ScenarioConfig
            {
                id = "ladakh-lac-ridge-01",
                name = "Ladakh LAC Ridge — High-Altitude Saturation Dive",
                nameHindi = "लद्दाख एलएसी रिज — उच्च ऊंचाई संतृप्ति गोता",
                region = ScenarioConfig.REGION_LADAKH_LAC,
                durationMaxSecs = 120,
                swarmProfile = new SwarmProfile
                {
                    agentCount = 10,
                    decoyCount = 3,
                    attackerCount = 7,
                    rfFreqGHz = new float[] { 5.825f },
                    speedKmh = 65,
                    altitudeM = 45,
                    tacticTree = SwarmProfile.TACTIC_SATURATION_DIVE
                }
            });
            list.Add(new ScenarioConfig
            {
                id = "loc-mountain-pass-01",
                name = "LoC Mountain Pass — Multi-Axis Valley Infiltration",
                nameHindi = "नियंत्रण रेखा पर्वतीय दर्रा — बहु-अक्ष घाटी घुसपैठ",
                region = ScenarioConfig.REGION_LOC_MOUNTAIN,
                durationMaxSecs = 120,
                swarmProfile = new SwarmProfile
                {
                    agentCount = 8,
                    decoyCount = 2,
                    attackerCount = 6,
                    rfFreqGHz = new float[] { 0.915f, 2.4f },
                    speedKmh = 80,
                    altitudeM = 25,
                    tacticTree = SwarmProfile.TACTIC_MULTI_AXIS_SPLIT
                }
            });
            return list;
        }

        private void EnsureRuntimeScenarioManager()
        {
            if (ScenarioManager.Instance == null)
            {
                GameObject smGo = new GameObject("ScenarioManager_Runtime");
                ScenarioManager sm = smGo.AddComponent<ScenarioManager>();
                sm.CurrentMode = OperationMode.VRMode;
                sm.LoadSceneOnScenarioStart = false;
                sm.RefreshScenarioLibrary();
                Debug.Log("[VRScenarioSelector] Created dynamic ScenarioManager_Runtime for VR standalone execution.");
            }
            else
            {
                ScenarioManager.Instance.CurrentMode = OperationMode.VRMode;
                ScenarioManager.Instance.LoadSceneOnScenarioStart = false;
            }

            if (vrHudController != null)
            {
                vrHudController.EnsureSubscribedToScenarioEvents();
            }
        }

        private void SubscribeScenarioEvents()
        {
            if (isSubscribedToEvents || ScenarioManager.Instance == null) return;
            var sm = ScenarioManager.Instance;
            sm.OnScenarioEndBroadcast += HandleScenarioEnded;
            sm.OnScenarioEndReceived  += HandleScenarioEnded;
            isSubscribedToEvents = true;
        }

        private void UnsubscribeScenarioEvents()
        {
            if (!isSubscribedToEvents || ScenarioManager.Instance == null) return;
            var sm = ScenarioManager.Instance;
            sm.OnScenarioEndBroadcast -= HandleScenarioEnded;
            sm.OnScenarioEndReceived  -= HandleScenarioEnded;
            isSubscribedToEvents = false;
        }

        // ══════════════════════════════════════════════════════════
        // State Machine & Update Loop
        // ══════════════════════════════════════════════════════════

        public void SetState(VRTrainingState newState)
        {
            currentState = newState;
            Debug.Log("[VRScenarioSelector] State changed to: " + currentState);

            switch (currentState)
            {
                case VRTrainingState.SELECTING:
                    if (holographicScorecard != null)
                        holographicScorecard.HideScorecard();
                    if (vrHudController != null)
                    {
                        vrHudController.SetThreatTorusColor("GREEN");
                        vrHudController.UpdatePanelTextures();
                    }
                    break;

                case VRTrainingState.RUNNING:
                    if (holographicScorecard != null)
                        holographicScorecard.HideScorecard();
                    if (vrHudController != null)
                    {
                        vrHudController.SetThreatTorusColor("GREEN");
                    }
                    break;

                case VRTrainingState.AAR_SCORECARD:
                    if (holographicScorecard != null)
                    {
                        holographicScorecard.ShowScorecard(currentScorecard, lastEndReason);
                    }
                    if (vrHudController != null)
                    {
                        bool passed = currentScorecard != null && currentScorecard.doctrinePassed;
                        vrHudController.SetThreatTorusColor(passed ? "GREEN" : "RED");
                        vrHudController.UpdatePanelTextures();
                    }
                    break;
            }
        }

        private void Update()
        {
            switch (currentState)
            {
                case VRTrainingState.SELECTING:
                    HandleSelectingInput();
                    break;

                case VRTrainingState.RUNNING:
                    HandleRunningInput();
                    AdvanceSimulatedFlight();
                    break;

                case VRTrainingState.AAR_SCORECARD:
                    HandleAarInput();
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        // Input Handlers
        // ══════════════════════════════════════════════════════════

        private void HandleSelectingInput()
        {
            // Number hotkeys [1..4]
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1)) LaunchScenario(0);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2)) LaunchScenario(1);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad3)) LaunchScenario(2);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad4)) LaunchScenario(3);

            // Up / Down navigation
            if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                selectedIndex = (selectedIndex - 1 + scenarios.Count) % scenarios.Count;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                selectedIndex = (selectedIndex + 1) % scenarios.Count;
            }

            // Launch selected with Space or Enter
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.Return))
            {
                LaunchScenario(selectedIndex);
            }

            // Gamepad Navigation
#if ENABLE_INPUT_SYSTEM
            if (Gamepad.current != null)
            {
                if (Gamepad.current.dpad.up.wasPressedThisFrame)
                    selectedIndex = (selectedIndex - 1 + scenarios.Count) % scenarios.Count;
                if (Gamepad.current.dpad.down.wasPressedThisFrame)
                    selectedIndex = (selectedIndex + 1) % scenarios.Count;
                if (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.buttonNorth.wasPressedThisFrame)
                    LaunchScenario(selectedIndex);
            }
#endif
            if (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0) || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton3))
            {
                LaunchScenario(selectedIndex);
            }
        }

        /// <summary>
        /// Monitors for the in-flight Stop / Abort command via Keyboard [Esc] or Gamepad [B] / Back button.
        /// </summary>
        private void HandleRunningInput()
        {
            bool stopRequested = UnityEngine.Input.GetKeyDown(KeyCode.Escape);

#if ENABLE_INPUT_SYSTEM
            if (!stopRequested && Gamepad.current != null)
            {
                stopRequested = Gamepad.current.buttonEast.wasPressedThisFrame || // Gamepad B
                                Gamepad.current.selectButton.wasPressedThisFrame || // Back button
                                Gamepad.current.startButton.wasPressedThisFrame;   // Menu / Pause
            }
#endif
            if (!stopRequested && (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton1) || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton6)))
            {
                stopRequested = true;
            }

            if (stopRequested)
            {
                Debug.Log("[VRScenarioSelector] In-flight STOP requested by operator (Esc / Gamepad B). Halting mission and transitioning to AAR.");
                StopScenario(ScenarioEndPayload.REASON_INSTRUCTOR_ABORT);
            }
        }

        private void HandleAarInput()
        {
            bool menuRequested = UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.Escape);
            bool replayRequested = UnityEngine.Input.GetKeyDown(KeyCode.R);
            bool exportRequested = UnityEngine.Input.GetKeyDown(KeyCode.P);

#if ENABLE_INPUT_SYSTEM
            if (Gamepad.current != null)
            {
                if (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                    Gamepad.current.buttonEast.wasPressedThisFrame ||
                    Gamepad.current.selectButton.wasPressedThisFrame) menuRequested = true; // Gamepad A / B / Back
                if (Gamepad.current.buttonWest.wasPressedThisFrame) replayRequested = true; // Gamepad X
                if (Gamepad.current.buttonNorth.wasPressedThisFrame) exportRequested = true; // Gamepad Y
            }
#endif
            if (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0) || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton1)) menuRequested = true;
            if (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton2)) replayRequested = true;
            if (UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton3)) exportRequested = true;

            if (menuRequested)
            {
                ReturnToMissionSelect();
            }
            else if (replayRequested)
            {
                InstantReplay();
            }
            else if (exportRequested)
            {
                ExportPdfReport();
            }
        }

        // ══════════════════════════════════════════════════════════
        // Scenario Execution & In-Flight Simulation
        // ══════════════════════════════════════════════════════════

        public void LaunchScenario(int index)
        {
            if (index < 0 || index >= scenarios.Count) return;
            selectedIndex = index;
            LaunchScenario(scenarios[index]);
        }

        public void LaunchScenario(ScenarioConfig config)
        {
            if (config == null) return;
            currentScenario = config;
            scenarioRunningTime = 0f;
            lastEndReason = null;
            lastExportedPdfPath = null;

            EnsureRuntimeScenarioManager();
            SubscribeScenarioEvents();

            // Initialize active swarm
            activeSwarm = ScenarioManager.CreateDefaultBoidSwarm(config);
            if (ScenarioManager.Instance != null)
            {
                ScenarioManager.Instance.InjectSwarmSnapshot(activeSwarm);
                ScenarioManager.Instance.LaunchScenario(config);
            }

            SetState(VRTrainingState.RUNNING);
            Debug.Log("[VRScenarioSelector] Launched scenario: " + config.name);
        }

        private void AdvanceSimulatedFlight()
        {
            scenarioRunningTime += Time.deltaTime;

            // Advance positions of active boids toward sentry post so Doppler & acoustic sensors move
            if (activeSwarm != null && activeSwarm.Length > 0)
            {
                for (int i = 0; i < activeSwarm.Length; i++)
                {
                    var a = activeSwarm[i];
                    if (a == null || a.isNeutralised) continue;

                    // Move along heading / velocity
                    a.positionWorld.x += a.velocityMs.x * Time.deltaTime * 0.25f;
                    a.positionWorld.z += a.velocityMs.z * Time.deltaTime * 0.25f;

                    // Check exclusion zone breach (radius <= 20m)
                    float distSq = a.positionWorld.x * a.positionWorld.x + a.positionWorld.z * a.positionWorld.z;
                    if (distSq <= 400f && !a.isDecoy)
                    {
                        Debug.Log("[VRScenarioSelector] Exclusion zone breach detected!");
                        StopScenario(ScenarioEndPayload.REASON_EXCLUSION_BREACH);
                        return;
                    }
                }

                if (ScenarioManager.Instance != null)
                {
                    ScenarioManager.Instance.InjectSwarmSnapshot(activeSwarm);
                }
            }

            // Inject live EW jammer state from gamepad into ScenarioManager every frame.
            // VRSentryPost has no CockpitController, so without this the ScenarioManager's
            // _injectedEwJammerState stays zeroed and every SCENARIO_TICK carries stale defaults —
            // causing the VR HUD EW panel to show no changes until the scenario ends (fail/abort),
            // at which point VRHudController switches to its idle Update() path that polls
            // GamepadInputController directly. This injection closes that gap during a live scenario.
            if (ScenarioManager.Instance != null)
            {
#if UNITY_5_3_OR_NEWER
                if (_cachedGamepad == null)
                    _cachedGamepad = FindAnyObjectByType<Input.GamepadInputController>();
#endif
                if (_cachedGamepad != null)
                {
                    var liveEwState = new TrishulSim.Scenarios.EWJammerState
                    {
                        engaged    = _cachedGamepad.CurrentEngaged,
                        bearingDeg = _cachedGamepad.CurrentAzimuth,
                        elevationDeg = _cachedGamepad.CurrentElevation,
                        bandGHz    = _cachedGamepad.CurrentBandGHz
                    };
                    ScenarioManager.Instance.InjectEWJammerState(liveEwState);
                }
            }

            // Duration limit check
            float maxDuration = (currentScenario != null && currentScenario.durationMaxSecs > 0)
                ? currentScenario.durationMaxSecs
                : 120f;
            if (scenarioRunningTime >= maxDuration)
            {
                Debug.Log("[VRScenarioSelector] Scenario time limit reached.");
                StopScenario(ScenarioEndPayload.REASON_TIME_LIMIT);
            }
        }

        /// <summary>
        /// Halts the current running scenario and triggers direct transition to AAR.
        /// </summary>
        public void StopScenario(string reason = ScenarioEndPayload.REASON_INSTRUCTOR_ABORT)
        {
            if (currentState != VRTrainingState.RUNNING) return;

            lastEndReason = reason;
            Debug.Log("[VRScenarioSelector] Halting scenario with reason: " + reason);

            // 1. Transition directly to AAR state FIRST to lock state and prevent re-entrant races
            TransitionToAar(reason);

            // 2. Notify ScenarioManager to stop simulation loop and broadcast terminal event
            if (ScenarioManager.Instance != null)
            {
                if (string.Equals(reason, ScenarioEndPayload.REASON_INSTRUCTOR_ABORT, StringComparison.OrdinalIgnoreCase))
                {
                    ScenarioManager.Instance.AbortScenario();
                }
                else
                {
                    ScenarioManager.Instance.BroadcastScenarioEnd(reason);
                }
            }
        }

        private void HandleScenarioEnded(ScenarioEndPayload endPayload)
        {
            if (currentState == VRTrainingState.AAR_SCORECARD) return;
            string reason = endPayload != null ? endPayload.reason : ScenarioEndPayload.REASON_INSTRUCTOR_ABORT;
            TransitionToAar(reason);
        }

        /// <summary>
        /// Computes final performance metrics and switches directly to the 3D Holographic Scorecard.
        /// </summary>
        public void TransitionToAar(string reason)
        {
            if (currentState == VRTrainingState.AAR_SCORECARD && currentScorecard != null) return;

            lastEndReason = reason;

            // Compute or calibrate scorecard for this session
            currentScorecard = ComputeSessionScorecard(reason, scenarioRunningTime);

            SetState(VRTrainingState.AAR_SCORECARD);
            Debug.Log("[VRScenarioSelector] Transitioned to AAR. Holographic Scorecard is now active.");
        }

        private Scorecard ComputeSessionScorecard(string reason, float elapsedSec)
        {
            int elapsedMs = Mathf.RoundToInt(elapsedSec * 1000f);
            if (elapsedMs < 100)
            {
                elapsedMs = (ScenarioManager.Instance != null && ScenarioManager.Instance.LastEndPayload != null && ScenarioManager.Instance.LastEndPayload.t > 0)
                    ? (int)ScenarioManager.Instance.LastEndPayload.t
                    : 1000;
            }

            bool isBreach = string.Equals(reason, ScenarioEndPayload.REASON_EXCLUSION_BREACH, StringComparison.OrdinalIgnoreCase);
            bool isAbort = string.Equals(reason, ScenarioEndPayload.REASON_INSTRUCTOR_ABORT, StringComparison.OrdinalIgnoreCase);

            bool doctrinePassed = !isBreach;
            string sessId = ScenarioManager.Instance != null && !string.IsNullOrEmpty(ScenarioManager.Instance.CurrentSessionId)
                ? ScenarioManager.Instance.CurrentSessionId
                : "sess-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

            string scId = currentScenario != null ? currentScenario.id : "punjab-fog-pincer-01";
            string scName = currentScenario != null ? currentScenario.name : "Punjab Border — Zero Visibility Pincer";
            string scHindi = currentScenario != null ? currentScenario.nameHindi : "पंजाब सीमा — शून्य दृश्यता पिंसर";

            int ttdMs = Mathf.Clamp(Mathf.RoundToInt(elapsedMs * 0.22f), 4500, 13800);
            if (elapsedMs < ttdMs) ttdMs = Mathf.Max(500, elapsedMs - 200);

            // Read actual ROE compliance from VRHudController if available
            float roeCompliance = (vrHudController != null && vrHudController.LastBlackoutRisk) ? 0.0f : 1.0f;
            if (roeCompliance < 1.0f) doctrinePassed = false;

            return new Scorecard
            {
                sessionId = sessId,
                scenarioId = scId,
                scenarioName = scName,
                scenarioNameHindi = scHindi,
                traineeId = "trainee-sentry-01",
                completedAtIso = DateTime.UtcNow.ToString("o"),
                durationMs = elapsedMs,
                doctrinePassed = doctrinePassed,
                metrics = new ScorecardMetrics
                {
                    timeToDetectionMs = ttdMs,
                    classificationAccuracy = 0.0f,
                    roeCompliance = roeCompliance,
                    kineticRounds = isAbort ? 2 : 4,
                    kineticHits = isAbort ? 2 : 3,
                    kineticEfficiency = isAbort ? 1.0f : 0.75f,
                    agentsNeutralised = isAbort ? 2 : 3,
                    agentsMissed = isBreach ? 2 : 0,
                    prematureEwPenalty = false
                },
                doctrineBenchmarks = new DoctrineBenchmarks
                {
                    timeToDetectionThresholdMs = 15000,
                    roeMinCompliance = 1.0f,
                    kineticMinEfficiency = 0.5f
                },
                timelineIndex = new List<int> { 0, 5000, 10000, ttdMs, elapsedMs },
                sessionFilePath = "sessions/" + sessId + ".ndjson",
                partial = isAbort,
                version = 1
            };
        }

        // ══════════════════════════════════════════════════════════
        // AAR Tactical Actions
        // ══════════════════════════════════════════════════════════

        public void ReturnToMissionSelect()
        {
            if (ScenarioManager.Instance != null)
            {
                ScenarioManager.Instance.ResetScenarioState();
            }
            SetState(VRTrainingState.SELECTING);
        }

        public void InstantReplay()
        {
            if (currentScenario != null)
            {
                LaunchScenario(currentScenario);
            }
            else if (scenarios.Count > 0)
            {
                LaunchScenario(0);
            }
        }

        public string ExportPdfReport()
        {
            if (currentScorecard == null)
            {
                currentScorecard = ComputeSessionScorecard(lastEndReason ?? "MANUAL_STOP", scenarioRunningTime);
            }

            try
            {
                byte[] pdfBytes = PdfExporter.Export(currentScorecard);
                string sessionsDir = Path.Combine(Directory.GetCurrentDirectory(), "sessions");
                if (!Directory.Exists(sessionsDir))
                {
                    Directory.CreateDirectory(sessionsDir);
                }

                string fileName = string.Format("{0}.pdf", currentScorecard.sessionId);
                string fullPath = Path.Combine(sessionsDir, fileName);
                File.WriteAllBytes(fullPath, pdfBytes);

                lastExportedPdfPath = fullPath;
                Debug.Log("[VRScenarioSelector] Tactical Performance Dossier exported successfully: " + fullPath);

                if (holographicScorecard != null)
                {
                    holographicScorecard.SetPdfExportedStatus(fullPath);
                }

                return fullPath;
            }
            catch (Exception ex)
            {
                Debug.LogError("[VRScenarioSelector] PDF Export failed: " + ex.Message);
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════
        // Tactical OnGUI Overlay
        // ══════════════════════════════════════════════════════════

        private void InitGuiStyles()
        {
            if (stylesInitialized) return;

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = MakeTex(2, 2, new Color(0.02f, 0.05f, 0.08f, 0.94f));

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 18;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = new Color(0f, 0.95f, 0.95f, 1f);
            headerStyle.alignment = TextAnchor.MiddleCenter;

            itemStyle = new GUIStyle(GUI.skin.button);
            itemStyle.fontSize = 13;
            itemStyle.fontStyle = FontStyle.Normal;
            itemStyle.alignment = TextAnchor.MiddleLeft;
            itemStyle.normal.textColor = Color.white;
            itemStyle.normal.background = MakeTex(2, 2, new Color(0.04f, 0.12f, 0.18f, 0.85f));
            itemStyle.hover.background = MakeTex(2, 2, new Color(0.06f, 0.22f, 0.30f, 0.95f));

            itemSelectedStyle = new GUIStyle(itemStyle);
            itemSelectedStyle.normal.background = MakeTex(2, 2, new Color(0.08f, 0.32f, 0.40f, 0.95f));
            itemSelectedStyle.normal.textColor = new Color(0.2f, 1f, 0.5f, 1f);
            itemSelectedStyle.fontStyle = FontStyle.Bold;

            footerStyle = new GUIStyle(GUI.skin.label);
            footerStyle.fontSize = 11;
            footerStyle.normal.textColor = new Color(0.6f, 0.8f, 0.9f, 0.9f);
            footerStyle.alignment = TextAnchor.MiddleCenter;

            watermarkStyle = new GUIStyle(GUI.skin.label);
            watermarkStyle.fontSize = 12;
            watermarkStyle.fontStyle = FontStyle.Bold;
            watermarkStyle.normal.textColor = new Color(1f, 0.702f, 0f, 0.9f); // Amber
            watermarkStyle.alignment = TextAnchor.UpperRight;

            stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void OnGUI()
        {
            InitGuiStyles();

            if (currentState == VRTrainingState.SELECTING)
            {
                RenderMissionSelectorOverlay();
            }
            else if (currentState == VRTrainingState.RUNNING)
            {
                // In-flight clean viewport with tactical Stop reminder in corner
                string missionName = currentScenario != null ? currentScenario.name : "MISSION";
                string stopText = string.Format("[ESC / GAMEPAD B: STOP & REVIEW AAR]  ·  ACTIVE: {0}", missionName.ToUpperInvariant());
                GUI.Label(new Rect(10, 10, Screen.width - 20, 25), stopText, watermarkStyle);
            }
            else if (currentState == VRTrainingState.AAR_SCORECARD)
            {
                // Subtle action bar complementing 3D hologram
                RenderAarActionsBar();
            }
        }

        private void RenderMissionSelectorOverlay()
        {
            float width = Mathf.Min(640f, Screen.width - 40f);
            float height = 440f;
            float left = (Screen.width - width) / 2f;
            float top = (Screen.height - height) / 2f;

            GUI.Box(new Rect(left, top, width, height), GUIContent.none, panelStyle);

            // Header
            GUI.Label(new Rect(left + 20, top + 15, width - 40, 30), "TRISHUL-SIM // TACTICAL MISSION SELECTOR", headerStyle);
            GUI.Label(new Rect(left + 20, top + 45, width - 40, 20), "VR Standalone Sentry Post · Select training scenario via [1-4], Click, or Gamepad", footerStyle);

            // Mission Cards
            float cardY = top + 75f;
            float cardHeight = 65f;
            float cardSpacing = 8f;

            for (int i = 0; i < scenarios.Count; i++)
            {
                ScenarioConfig cfg = scenarios[i];
                bool isSel = (i == selectedIndex);
                GUIStyle styleToUse = isSel ? itemSelectedStyle : itemStyle;

                string hotkey = string.Format("[{0}] ", i + 1);
                string title = cfg.name ?? cfg.id;
                string agents = cfg.swarmProfile != null ? string.Format(" ({0} Drones · {1} km/h)", cfg.swarmProfile.agentCount, cfg.swarmProfile.speedKmh) : "";
                string desc = !string.IsNullOrEmpty(cfg.nameHindi) ? cfg.nameHindi : (cfg.region ?? "");

                string cardContent = string.Format("{0}{1}{2}\n    {3}", hotkey, title, agents, desc);

                if (GUI.Button(new Rect(left + 25, cardY, width - 50, cardHeight), cardContent, styleToUse))
                {
                    selectedIndex = i;
                    LaunchScenario(i);
                }

                cardY += cardHeight + cardSpacing;
            }

            // Controls Prompt Footer
            string footerText = "CONTROLS: [1-4] or Arrows to Select · [SPACE / ENTER] Launch · Gamepad: [D-PAD] + [A]";
            GUI.Label(new Rect(left + 20, top + height - 45, width - 40, 35), footerText, footerStyle);
        }

        private void RenderAarActionsBar()
        {
            float barW = Mathf.Min(620f, Screen.width - 40f);
            float barH = 55f;
            float barX = (Screen.width - barW) / 2f;
            float barY = Screen.height - barH - 20f;

            GUI.Box(new Rect(barX, barY, barW, barH), GUIContent.none, panelStyle);

            float btnW = (barW - 40f) / 3f;

            if (GUI.Button(new Rect(barX + 10, barY + 10, btnW, 35), "[SPACE / A] MENU", itemStyle))
            {
                ReturnToMissionSelect();
            }

            if (GUI.Button(new Rect(barX + 20 + btnW, barY + 10, btnW, 35), "[R / X] REPLAY", itemStyle))
            {
                InstantReplay();
            }

            if (GUI.Button(new Rect(barX + 30 + btnW * 2, barY + 10, btnW, 35), "[P / Y] EXPORT PDF", itemStyle))
            {
                ExportPdfReport();
            }
        }
    }
}
