// ============================================================
// C2StationController.cs — Instructor C2 Station UIDocument Controller
// TRISHUL-SIM · UX-P0-A · ui-ux track (Rani)
//
// Binds InstructorC2.uxml elements. Reacts to WS connection events
// and scenario lifecycle. Dependencies US-P0-A/B not merged —
// uses mock path: stub ConnectedCount=1, mock SCENARIO_START.
// ============================================================

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
        public Label ScorecardStubLabel { get { return scorecardStubLabel; } }

        // ══════════════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc != null)
            {
                root = uiDoc.rootVisualElement;
                BindElements();
            }
        }

        private void Start()
        {
            PopulateScenarioDropdown();
            RegisterCallbacks();

            // ── MOCK PATH (US-P0-A not merged) ──────────────
            // Stub: simulate connected state for editor testing.
            // Remove this block once US-P0-A (WsServer) is merged.
#if UNITY_EDITOR
            SimulateConnection();
#endif
        }

        private void OnDestroy()
        {
            UnregisterCallbacks();
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

            appTitle           = root.Q<Label>("app-title");
            linkStatusIcon     = root.Q<VisualElement>("link-status-icon");
            linkStatusLabel    = root.Q<Label>("link-status-label");
            scenarioDropdown   = root.Q<DropdownField>("scenario-dropdown");
            traineeIdField     = root.Q<TextField>("trainee-id");
            launchBtn          = root.Q<Button>("launch-btn");
            abortBtn           = root.Q<Button>("abort-btn");
            langToggle         = root.Q<Button>("lang-toggle");
            scorecardMirror    = root.Q<VisualElement>("scorecard-mirror");
            scorecardStubLabel = root.Q<Label>("scorecard-stub-label");
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
            // ── MOCK: Hard-coded scenarios (US-P0-B not merged) ──
            // Replace with ScenarioLibrary.LoadAll() once available.
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

            if (scenarioDropdown != null)
                scenarioDropdown.RegisterValueChangedCallback(OnScenarioChanged);
        }

        public void UnregisterCallbacks()
        {
            if (launchBtn != null) launchBtn.clicked -= OnLaunchClicked;
            if (abortBtn != null) abortBtn.clicked -= OnAbortClicked;
            if (langToggle != null) langToggle.clicked -= OnLangToggleClicked;

            if (scenarioDropdown != null)
                scenarioDropdown.UnregisterValueChangedCallback(OnScenarioChanged);
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
        /// MOCK: logs to console until ScenarioManager is available.
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

            // TODO: Replace with ScenarioManager.LaunchScenario(selectedScenarioId)
            // once US-P0-B is merged.
        }

        /// <summary>
        /// Abort the running scenario.
        /// MOCK: logs to console until ScenarioManager is available.
        /// </summary>
        public void OnAbortClicked()
        {
            Debug.Log("[C2] ABORT requested by Instructor.");

            // TODO: Replace with ScenarioManager.BroadcastScenarioEnd("INSTRUCTOR_ABORT")
            // once US-P0-B is merged.
        }

        /// <summary>
        /// Toggle language (placeholder — full implementation in UX-P0-B).
        /// </summary>
        public void OnLangToggleClicked()
        {
            Debug.Log("[C2] Language toggle clicked. Full implementation in UX-P0-B.");
            // TODO: Wire to LocaleToggle.ToggleLanguage() in UX-P0-B.
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
                linkStatusLabel.text = "● LAN LINK CONFIRMED";
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
                linkStatusLabel.text = "✕ LINK LOST";
            }
            Debug.LogWarning("[C2] Trainee disconnected — status RED.");
        }

        // ══════════════════════════════════════════════════════
        // Scorecard Mirror
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Called when SCORECARD_PAYLOAD is received via WebSocket.
        /// Shows the scorecard mirror panel on the C2 screen.
        /// Stub: displays sessionId text. Full renderer in UX-P1-D (Rajeswari).
        /// </summary>
        public void OnScorecardReceived(string sessionId)
        {
            if (scorecardMirror != null)
            {
                scorecardMirror.RemoveFromClassList("hidden");
            }
            if (scorecardStubLabel != null)
            {
                scorecardStubLabel.text = string.Format("Scorecard ready — Session: {0}", sessionId);
            }
            Debug.Log(string.Format("[C2] SCORECARD_PAYLOAD received for session {0}.", sessionId));
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
