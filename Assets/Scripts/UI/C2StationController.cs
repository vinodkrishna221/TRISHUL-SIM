// ============================================================
// C2StationController.cs — Instructor C2 Station UIDocument Controller
// TRISHUL-SIM · UX-P0-A + UX-P0-B · ui-ux track (Rani)
//
// Binds InstructorC2.uxml elements. Reacts to WS connection events
// and scenario lifecycle. Dependencies US-P0-A/B not merged —
// uses mock path: stub ConnectedCount=1, mock SCENARIO_START.
//
// UX-P0-B: Wired LocaleToggle — all labels use GetString(key).
//          lang-toggle button calls LocaleToggle.ToggleLanguage().
//          OnLocaleChanged subscription rebinds all label text.
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace TrishulSim.UI
{
    /// <summary>
    /// MonoBehaviour attached to the InstructorC2 scene's UIDocument.
    /// Manages the C2 Station HUD: connection status, scenario launch,
    /// abort, language toggle, and scorecard mirror.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class C2StationController : MonoBehaviour
    {
        // ── UI Element References ──────────────────────────────
        private VisualElement root;
        private Label appTitle;
        private VisualElement linkStatusIcon;
        private Label linkStatusLabel;
        private DropdownField scenarioDropdown;
        private TextField traineeIdField;
        private Button launchBtn;
        private Button abortBtn;
        private Button langToggle;
        private VisualElement scorecardMirror;
        private Label scorecardStubLabel;
        private ScorecardPanelController scorecardController;

        // ── VG-P1-E: VR Mode & Gyro Status ─────────────────────
        private Button vrModeBtn;
        private VisualElement vrStatusBadge;
        private VisualElement vrStatusIcon;
        private Label vrStatusLabel;
        private bool isVrModeActive = false;
        private bool isGyroLive = false;
        public static Func<bool> GyroStatusQuery = null;

        // ── State ─────────────────────────────────────────────
        private bool isConnected = false;
        private string selectedScenarioId = "";
        private List<string> scenarioNames = new List<string>();
        private List<string> scenarioIds = new List<string>();

        // ── Public Accessors for Tests / UI Validation ────────
        public bool IsConnected { get { return isConnected; } }
        public string SelectedScenarioId { get { return selectedScenarioId; } }
        public VisualElement Root { get { return root; } }
        public Label LinkStatusLabel { get { return linkStatusLabel; } }
        public VisualElement LinkStatusIcon { get { return linkStatusIcon; } }
        public DropdownField ScenarioDropdown { get { return scenarioDropdown; } }
        public VisualElement ScorecardMirror { get { return scorecardMirror; } }
        public ScorecardPanelController ScorecardController { get { return scorecardController; } }
        public Label ScorecardStubLabel { get { return scorecardStubLabel; } }
        public Button VrModeBtn { get { return vrModeBtn; } }
        public VisualElement VrStatusBadge { get { return vrStatusBadge; } }
        public VisualElement VrStatusIcon { get { return vrStatusIcon; } }
        public Label VrStatusLabel { get { return vrStatusLabel; } }
        public bool IsVrModeActive { get { return isVrModeActive; } }
        public bool IsGyroLive { get { return isGyroLive; } }

        // ══════════════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc != null)
            {
#if UNITY_5_3_OR_NEWER
                if (uiDoc.panelSettings == null)
                {
#if UNITY_EDITOR
                    uiDoc.panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/DefaultPanelSettings.asset");
#endif
                    if (uiDoc.panelSettings == null)
                        uiDoc.panelSettings = Resources.Load<PanelSettings>("UI/DefaultPanelSettings");
                    if (uiDoc.panelSettings == null)
                    {
                        uiDoc.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                        uiDoc.panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                        uiDoc.panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                        uiDoc.panelSettings.match = 0.5f;
                    }
                }
                if (uiDoc.visualTreeAsset == null)
                {
#if UNITY_EDITOR
                    uiDoc.visualTreeAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/InstructorC2.uxml");
#endif
                    if (uiDoc.visualTreeAsset == null)
                        uiDoc.visualTreeAsset = Resources.Load<VisualTreeAsset>("UI/InstructorC2");
                }
#endif
                root = uiDoc.rootVisualElement;
#if UNITY_5_3_OR_NEWER
                if (root != null && root.childCount == 0 && uiDoc.visualTreeAsset != null)
                {
                    uiDoc.visualTreeAsset.CloneTree(root);
                }
#endif
                BindElements();
            }
        }

        private void OnEnable()
        {
            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc != null)
            {
                if (root == null || root.childCount == 0 || appTitle == null)
                {
                    root = uiDoc.rootVisualElement;
#if UNITY_5_3_OR_NEWER
                    if (root != null && root.childCount == 0 && uiDoc.visualTreeAsset != null)
                    {
                        uiDoc.visualTreeAsset.CloneTree(root);
                    }
#endif
                    BindElements();
                    PopulateScenarioDropdown();
                    RegisterCallbacks();
                }
            }
        }

        private void Start()
        {
            PopulateScenarioDropdown();
            RegisterCallbacks();

            // ── UX-P0-B: Subscribe to locale changes ──────────
            if (LocaleToggle.Instance != null)
            {
                LocaleToggle.Instance.OnLocaleChanged += OnLocaleChanged;
                RefreshLocalizedLabels();
            }

#if UNITY_5_3_OR_NEWER
            if (TrishulSim.Scenarios.ScenarioManager.Instance != null)
            {
                var sm = TrishulSim.Scenarios.ScenarioManager.Instance;
                sm.OnOperationModeChanged += OnOperationModeChanged;
                isVrModeActive = (sm.CurrentMode == TrishulSim.Scenarios.OperationMode.VRMode);
                UpdateVrStatusBadge();

                if (sm.CurrentMode == TrishulSim.Scenarios.OperationMode.SingleLaptopSplit || (sm.Server != null && sm.Server.ConnectedCount > 0))
                {
                    OnClientConnected();
                }

                if (sm.Server != null)
                {
                    sm.Server.OnClientConnected += () => OnClientConnected();
                    sm.Server.OnClientDisconnected += () =>
                    {
                        if (sm.Server.ConnectedCount == 0 && sm.CurrentMode != TrishulSim.Scenarios.OperationMode.SingleLaptopSplit)
                            OnClientDisconnected();
                    };
                }

                sm.OnScorecardReceived += (env) =>
                {
                    if (!string.IsNullOrEmpty(env.payload))
                    {
                        try
                        {
                            var sc = ScorecardMock.GetPunjabFixture();
                            sc.sessionId = sm.CurrentSessionId;
                            OnScorecardReceived(sc);
                        }
                        catch { }
                    }
                };
            }
            else
            {
#if UNITY_EDITOR
                SimulateConnection();
#endif
            }
#endif
        }

        private void Update()
        {
            if (isVrModeActive)
            {
                bool gyro = CheckGyroStatus();
                if (gyro != isGyroLive)
                {
                    UpdateVrStatusBadge(isVrModeActive, gyro);
                }
            }
        }

        private void OnDestroy()
        {
            UnregisterCallbacks();

            // ── UX-P0-B: Unsubscribe locale listener ─────────
            if (LocaleToggle.Instance != null)
                LocaleToggle.Instance.OnLocaleChanged -= OnLocaleChanged;

#if UNITY_5_3_OR_NEWER
            if (TrishulSim.Scenarios.ScenarioManager.Instance != null)
                TrishulSim.Scenarios.ScenarioManager.Instance.OnOperationModeChanged -= OnOperationModeChanged;
#endif
        }

        // ══════════════════════════════════════════════════════
        // UI Binding
        // ══════════════════════════════════════════════════════

        public void BindElements(VisualElement customRoot = null)
        {
            if (customRoot != null)
                root = customRoot;

            if (root == null)
                return;

#if UNITY_5_3_OR_NEWER
            bool isSplit = FindAnyObjectByType<CockpitController>() != null || 
                (TrishulSim.Scenarios.ScenarioManager.Instance != null && TrishulSim.Scenarios.ScenarioManager.Instance.CurrentMode == TrishulSim.Scenarios.OperationMode.SingleLaptopSplit);
            if (isSplit)
            {
                root.style.width = Length.Percent(50);
                root.style.height = Length.Percent(100);
                root.style.position = Position.Absolute;
                root.style.left = 0;
                root.style.top = 0;

                var container = root.Q<VisualElement>(className: "c2-root");
                if (container != null)
                {
                    container.style.width = Length.Percent(100);
                    container.style.height = Length.Percent(100);
                }
            }
#endif

            appTitle           = root.Q<Label>("app-title");
            linkStatusIcon     = root.Q<VisualElement>("link-status-icon");
            linkStatusLabel    = root.Q<Label>("link-status-label");
            scenarioDropdown   = root.Q<DropdownField>("scenario-dropdown");
            traineeIdField     = root.Q<TextField>("trainee-id");
            launchBtn          = root.Q<Button>("launch-btn");
            abortBtn           = root.Q<Button>("abort-btn");
            langToggle         = root.Q<Button>("lang-toggle");
            vrModeBtn          = root.Q<Button>("vr-mode-btn");
            vrStatusBadge      = root.Q<VisualElement>("vr-status-badge");
            vrStatusIcon       = root.Q<VisualElement>("vr-status-icon");
            vrStatusLabel      = root.Q<Label>("vr-status-label");
            scorecardMirror    = root.Q<VisualElement>("scorecard-mirror");
            scorecardStubLabel = root.Q<Label>("scorecard-stub-label");

            UpdateVrStatusBadge();

            if (scorecardMirror != null)
            {
                var scorecardRoot = scorecardMirror.Q<VisualElement>("scorecard-root") ?? scorecardMirror;
                if (scorecardController == null)
                {
                    scorecardController = gameObject.GetComponent<ScorecardPanelController>();
                    if (scorecardController == null)
                        scorecardController = gameObject.AddComponent<ScorecardPanelController>();
                }
                scorecardController.BindElements(scorecardRoot);
            }
        }

        // ══════════════════════════════════════════════════════
        // Scenario Dropdown
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Populates the scenario dropdown from available configs.
        /// MOCK PATH: hard-coded scenario list until US-P0-B
        /// (ScenarioLibrary.LoadAll()) is merged.
        /// </summary>
        public void PopulateScenarioDropdown()
        {
#if UNITY_5_3_OR_NEWER
            if (TrishulSim.Scenarios.ScenarioManager.Instance != null && TrishulSim.Scenarios.ScenarioManager.Instance.Scenarios != null && TrishulSim.Scenarios.ScenarioManager.Instance.Scenarios.Count > 0)
            {
                scenarioNames.Clear();
                scenarioIds.Clear();
                for (int i = 0; i < TrishulSim.Scenarios.ScenarioManager.Instance.Scenarios.Count; i++)
                {
                    var sc = TrishulSim.Scenarios.ScenarioManager.Instance.Scenarios[i];
                    scenarioNames.Add(sc.name);
                    scenarioIds.Add(sc.id);
                }
            }
            else
#endif
            {
                scenarioNames = new List<string>
                {
                    "Punjab Border — Zero Visibility Pincer",
                    "Rajasthan Desert FOB — Saturation Dive",
                    "Siachen Glacier Post — High-Alt Swarm",
                    "Andaman Coastal Battery — Maritime Multi-Axis"
                };
                scenarioIds = new List<string>
                {
                    "punjab-fog-pincer-01",
                    "rajasthan-desert-fob-01",
                    "siachen-glacier-01",
                    "andaman-coastal-01"
                };
            }

            if (scenarioDropdown != null)
            {
                scenarioDropdown.choices = scenarioNames;
                if (scenarioNames.Count > 0)
                {
                    scenarioDropdown.index = 0;
                    selectedScenarioId = scenarioIds[0];
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // Callbacks
        // ══════════════════════════════════════════════════════

        public void RegisterCallbacks()
        {
            if (launchBtn != null) launchBtn.clicked += OnLaunchClicked;
            if (abortBtn != null) abortBtn.clicked += OnAbortClicked;
            if (langToggle != null) langToggle.clicked += OnLangToggleClicked;
            if (vrModeBtn != null) vrModeBtn.clicked += OnVrModeClicked;

            if (scenarioDropdown != null)
                scenarioDropdown.RegisterValueChangedCallback(OnScenarioChanged);
        }

        public void UnregisterCallbacks()
        {
            if (launchBtn != null) launchBtn.clicked -= OnLaunchClicked;
            if (abortBtn != null) abortBtn.clicked -= OnAbortClicked;
            if (langToggle != null) langToggle.clicked -= OnLangToggleClicked;
            if (vrModeBtn != null) vrModeBtn.clicked -= OnVrModeClicked;

            if (scenarioDropdown != null)
                scenarioDropdown.UnregisterValueChangedCallback(OnScenarioChanged);
        }

#if UNITY_5_3_OR_NEWER
        private void OnOperationModeChanged(TrishulSim.Scenarios.OperationMode mode)
        {
            isVrModeActive = (mode == TrishulSim.Scenarios.OperationMode.VRMode);
            UpdateVrStatusBadge();
        }
#endif

        /// <summary>
        /// Toggles VR Mode on/off. When active, loads VRSentryPost on secondary display (phone via spacedesk)
        /// or laptop split-stereo fallback. When toggled off, returns to TwoLaptop mode.
        /// </summary>
        public void OnVrModeClicked()
        {
            Debug.Log("[C2] VR MODE toggle clicked.");
#if UNITY_5_3_OR_NEWER
            if (TrishulSim.Scenarios.ScenarioManager.Instance != null)
            {
                var sm = TrishulSim.Scenarios.ScenarioManager.Instance;
                if (sm.CurrentMode == TrishulSim.Scenarios.OperationMode.VRMode)
                {
                    sm.SetOperationMode(TrishulSim.Scenarios.OperationMode.TwoLaptop);
                }
                else
                {
                    sm.SetOperationMode(TrishulSim.Scenarios.OperationMode.VRMode);
                }
            }
            else
            {
                isVrModeActive = !isVrModeActive;
                UpdateVrStatusBadge();
            }
#else
            isVrModeActive = !isVrModeActive;
            UpdateVrStatusBadge();
#endif
        }

        /// <summary>
        /// Updates the C2 status badge for VR and Gyro tracking:
        /// "VR MODE: ON ● GYRO: LIVE" when connected
        /// "VR MODE: ON ○ GYRO: OFFLINE" when not
        /// "VR MODE: OFF" when VR mode is inactive
        /// </summary>
        public void UpdateVrStatusBadge()
        {
            bool gyro = CheckGyroStatus();
            UpdateVrStatusBadge(isVrModeActive, gyro);
        }

        public void UpdateVrStatusBadge(bool vrActive, bool gyroLive)
        {
            isVrModeActive = vrActive;
            isGyroLive = gyroLive;

            if (vrStatusBadge != null)
            {
                vrStatusBadge.RemoveFromClassList("hidden");
            }

            if (vrModeBtn != null)
            {
                if (vrActive)
                {
                    vrModeBtn.AddToClassList("btn-vr-active");
                    vrModeBtn.text = "VR ACTIVE";
                }
                else
                {
                    vrModeBtn.RemoveFromClassList("btn-vr-active");
                    vrModeBtn.text = "VR MODE";
                }
            }

            if (vrStatusLabel != null)
            {
                if (!vrActive)
                {
                    vrStatusLabel.text = "VR MODE: OFF";
                }
                else if (gyroLive)
                {
                    vrStatusLabel.text = "VR MODE: ON \u25cf GYRO: LIVE";
                }
                else
                {
                    vrStatusLabel.text = "VR MODE: ON \u25cb GYRO: OFFLINE";
                }
            }

            if (vrStatusIcon != null)
            {
                vrStatusIcon.RemoveFromClassList("status-connected");
                vrStatusIcon.RemoveFromClassList("status-waiting");
                vrStatusIcon.RemoveFromClassList("status-lost");

                if (!vrActive)
                {
                    vrStatusIcon.AddToClassList("status-waiting");
                }
                else if (gyroLive)
                {
                    vrStatusIcon.AddToClassList("status-connected");
                }
                else
                {
                    vrStatusIcon.AddToClassList("status-waiting");
                }
            }
        }

        private bool CheckGyroStatus()
        {
            if (GyroStatusQuery != null)
            {
                try { return GyroStatusQuery(); } catch { }
            }

            try
            {
                Type vrType = Type.GetType("TrishulSim.VR.VRModeController, Assembly-CSharp") ??
                              Type.GetType("TrishulSim.VR.VRModeController");
                if (vrType != null)
                {
                    var instanceProp = vrType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    object instance = instanceProp != null ? instanceProp.GetValue(null, null) : null;
                    if (instance != null)
                    {
                        var gyroProp = vrType.GetProperty("IsGyroConnected", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (gyroProp != null)
                            return (bool)gyroProp.GetValue(instance, null);
                    }
                }
            }
            catch { }
            return false;
        }

        public void OnScenarioChanged(ChangeEvent<string> evt)
        {
            if (evt == null) return;
            int idx = scenarioNames.IndexOf(evt.newValue);
            if (idx >= 0 && idx < scenarioIds.Count)
                selectedScenarioId = scenarioIds[idx];
        }

        /// <summary>
        /// Launch the selected scenario.
        /// </summary>
        public void OnLaunchClicked()
        {
            if (!isConnected)
            {
                Debug.LogWarning("[C2] Cannot launch — Trainee not connected.");
                return;
            }

            string traineeId = traineeIdField != null ? traineeIdField.value : "";
            Debug.Log(string.Format("[C2] LAUNCH: scenario={0}, trainee={1}", selectedScenarioId, traineeId));

#if UNITY_5_3_OR_NEWER
            if (TrishulSim.Scenarios.ScenarioManager.Instance != null)
            {
                TrishulSim.Scenarios.ScenarioConfig cfg = null;
                var scs = TrishulSim.Scenarios.ScenarioManager.Instance.Scenarios;
                if (scs != null)
                {
                    for (int i = 0; i < scs.Count; i++)
                    {
                        if (string.Equals(scs[i].id, selectedScenarioId, StringComparison.OrdinalIgnoreCase))
                        {
                            cfg = scs[i];
                            break;
                        }
                    }
                    if (cfg == null && scs.Count > 0)
                    {
                        int idx = scenarioDropdown != null ? scenarioDropdown.index : 0;
                        if (idx >= 0 && idx < scs.Count)
                            cfg = scs[idx];
                        else
                            cfg = scs[0];
                    }
                }
                if (cfg != null)
                {
                    TrishulSim.Scenarios.ScenarioManager.Instance.LaunchScenario(cfg);
                }
            }
#endif
        }

        /// <summary>
        /// Abort the running scenario.
        /// </summary>
        public void OnAbortClicked()
        {
            Debug.Log("[C2] ABORT requested by Instructor.");
#if UNITY_5_3_OR_NEWER
            if (TrishulSim.Scenarios.ScenarioManager.Instance != null)
            {
                TrishulSim.Scenarios.ScenarioManager.Instance.BroadcastScenarioEnd("INSTRUCTOR_ABORT");
            }
#endif
        }

        /// <summary>
        /// Toggle language via LocaleToggle (UX-P0-B).
        /// LocaleToggle fires OnLocaleChanged → RefreshLocalizedLabels().
        /// </summary>
        public void OnLangToggleClicked()
        {
            LocaleToggle.Instance.ToggleLanguage();
            // RefreshLocalizedLabels is called by the OnLocaleChanged handler
        }

        // ══════════════════════════════════════════════════════
        // Localization (UX-P0-B)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Called by LocaleToggle.OnLocaleChanged event.
        /// Rebinds all label text from the active string table.
        /// </summary>
        private void OnLocaleChanged(TrishulLocale newLocale)
        {
            RefreshLocalizedLabels();
        }

        /// <summary>
        /// Rebinds all UI labels from the active locale's string table.
        /// Called on Start and after every locale toggle.
        /// </summary>
        public void RefreshLocalizedLabels()
        {
            var loc = LocaleToggle.Instance;

            if (appTitle != null)
                appTitle.text = loc.GetString("APP_TITLE");

            if (linkStatusLabel != null)
            {
                // Preserve connection-state label text based on current state
                linkStatusLabel.text = isConnected
                    ? loc.GetString("LINK_CONNECTED")
                    : loc.GetString("LINK_WAITING");
            }

            if (launchBtn != null)
                launchBtn.text = loc.GetString("LAUNCH_BTN");

            if (abortBtn != null)
                abortBtn.text = loc.GetString("ABORT_BTN");

            if (langToggle != null)
            {
                // Show the OTHER language name as the toggle target
                langToggle.text = loc.IsHindi
                    ? loc.GetString("LANG_TOGGLE_TO_ENGLISH")
                    : loc.GetString("LANG_TOGGLE_TO_HINDI");
            }

            if (scenarioDropdown != null && scenarioDropdown.label != null)
                scenarioDropdown.label = loc.GetString("SCENARIO_LABEL");

            if (traineeIdField != null)
                traineeIdField.label = loc.GetString("TRAINEE_ID_LABEL");

            // Refresh scenario dropdown names for current locale
            RefreshScenarioNames();
        }

        /// <summary>
        /// Updates the scenario dropdown choices with localized names.
        /// </summary>
        private void RefreshScenarioNames()
        {
            if (scenarioDropdown == null) return;

            var loc = LocaleToggle.Instance;
            scenarioNames = new List<string>
            {
                loc.GetString("SCENARIO_PUNJAB"),
                loc.GetString("SCENARIO_RAJASTHAN"),
                loc.GetString("SCENARIO_SIACHEN"),
                loc.GetString("SCENARIO_ANDAMAN")
            };

            int currentIdx = scenarioDropdown.index;
            scenarioDropdown.choices = scenarioNames;
            if (currentIdx >= 0 && currentIdx < scenarioNames.Count)
                scenarioDropdown.index = currentIdx;
        }

        // ══════════════════════════════════════════════════════
        // Connection Status
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Called when a Trainee client connects.
        /// Wire to WsServer.OnClientConnected once US-P0-A is merged.
        /// </summary>
        public void OnClientConnected()
        {
            isConnected = true;
            if (linkStatusIcon != null)
            {
                linkStatusIcon.RemoveFromClassList("status-waiting");
                linkStatusIcon.RemoveFromClassList("status-lost");
                linkStatusIcon.AddToClassList("status-connected");
            }
            if (linkStatusLabel != null)
            {
                linkStatusLabel.text = LocaleToggle.Instance.GetString("LINK_CONNECTED");
            }
            Debug.Log("[C2] Trainee connected — status GREEN.");
        }

        /// <summary>
        /// Called when the Trainee client disconnects.
        /// Wire to WsServer.OnClientDisconnected once US-P0-A is merged.
        /// </summary>
        public void OnClientDisconnected()
        {
            isConnected = false;
            if (linkStatusIcon != null)
            {
                linkStatusIcon.RemoveFromClassList("status-connected");
                linkStatusIcon.RemoveFromClassList("status-waiting");
                linkStatusIcon.AddToClassList("status-lost");
            }
            if (linkStatusLabel != null)
            {
                linkStatusLabel.text = LocaleToggle.Instance.GetString("LINK_LOST");
            }
            Debug.LogWarning("[C2] Trainee disconnected — status RED.");
        }

        // ══════════════════════════════════════════════════════
        // Scorecard Mirror
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Called when SCORECARD_PAYLOAD is received via WebSocket with full Scorecard object.
        /// Shows the scorecard mirror panel on the C2 screen and binds the data.
        /// </summary>
        public void OnScorecardReceived(Scorecard scorecard)
        {
            if (scorecard == null) return;
            Debug.Log(string.Format("[C2] SCORECARD_PAYLOAD received for session {0}.", scorecard.sessionId));

            if (scorecardMirror != null)
            {
                scorecardMirror.RemoveFromClassList("hidden");
            }
            if (scorecardStubLabel != null)
            {
                scorecardStubLabel.text = LocaleToggle.Instance.GetFormatted("SCORECARD_RECEIVED", scorecard.sessionId);
            }
            if (scorecardController != null)
            {
                scorecardController.Bind(scorecard);
            }
        }

        /// <summary>
        /// Backward-compatible overload receiving sessionId string (or mock data).
        /// </summary>
        public void OnScorecardReceived(string sessionId)
        {
            var fixture = ScorecardMock.GetPunjabFixture();
            if (!string.IsNullOrEmpty(sessionId))
                fixture.sessionId = sessionId;
            OnScorecardReceived(fixture);
        }

        // ══════════════════════════════════════════════════════
        // Mock Path (Editor Only)
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        /// <summary>
        /// Simulates a connected Trainee for editor testing.
        /// Remove once US-P0-A (WsServer) is merged.
        /// </summary>
        private void SimulateConnection()
        {
            Debug.Log("[C2][MOCK] Simulating Trainee connection (US-P0-A not merged).");
            OnClientConnected();
        }
#endif
    }
}
