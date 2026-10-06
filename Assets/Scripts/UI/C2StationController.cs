// UX-P0-A  ·  C2StationController.cs
// UITK controller for the Instructor C2 Station scene.
//
// Dependencies (from mission mock path):
//   WsServer, ScenarioManager, ScenarioLibrary  — stubbed below with #if TRISHUL_MOCK
//   when US-P0-A + US-P0-B land, remove the MOCK stubs and reference the real classes.
//
// OWNED BY: Rani (ui-ux track)
// DO NOT TOUCH: Assets/Scripts/Network/, Assets/Scripts/SwarmAI/, Assets/Scripts/AAR/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TRISHUL.UI
{
    /// <summary>
    /// Drives the Instructor C2 Station UITK document.
    /// Binds to InstructorC2.uxml via UIDocument component on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class C2StationController : MonoBehaviour
    {
        // ── Cached element references ─────────────────────────────────────
        private VisualElement _root;
        private VisualElement _statusIcon;
        private Label         _statusLabel;
        private DropdownField _scenarioDropdown;
        private TextField     _traineeId;
        private Button        _launchBtn;
        private Button        _abortBtn;
        private Button        _langToggle;
        private VisualElement _scorecardMirror;
        private Label         _scorecardStubLabel;

        // ── State ─────────────────────────────────────────────────────────
        private string _selectedScenarioId = string.Empty;

        // ─────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            BindElements();
        }

        private void Start()
        {
            PopulateScenarioDropdown();
            SubscribeToWsEvents();

#if TRISHUL_MOCK
            // Mock path: simulate Trainee connected immediately in Editor
            // Remove when US-P0-A (WsServer) is merged.
            SimulateTraineeConnected();
#endif
        }

        private void OnDestroy()
        {
            UnsubscribeFromWsEvents();
        }

        // ── Element binding ───────────────────────────────────────────────
        private void BindElements()
        {
            _statusIcon         = _root.Q<VisualElement>("link-status-icon");
            _statusLabel        = _root.Q<Label>("link-status-label");
            _scenarioDropdown   = _root.Q<DropdownField>("scenario-dropdown");
            _traineeId          = _root.Q<TextField>("trainee-id");
            _launchBtn          = _root.Q<Button>("launch-btn");
            _abortBtn           = _root.Q<Button>("abort-btn");
            _langToggle         = _root.Q<Button>("lang-toggle");
            _scorecardMirror    = _root.Q<VisualElement>("scorecard-mirror");
            _scorecardStubLabel = _root.Q<Label>("scorecard-stub-label");

            // Button callbacks
            _launchBtn.clicked  += OnLaunchClicked;
            _abortBtn.clicked   += OnAbortClicked;
            _langToggle.clicked += OnLangToggleClicked;

            // Dropdown callback
            _scenarioDropdown.RegisterValueChangedCallback(evt =>
            {
                _selectedScenarioId = evt.newValue;
            });
        }

        // ── Scenario dropdown ─────────────────────────────────────────────
        private void PopulateScenarioDropdown()
        {
#if TRISHUL_MOCK
            // Mock path: hard-coded scenario list until ScenarioLibrary (US-P0-B) lands.
            var mockScenarios = new List<string>
            {
                "Punjab Border \u2014 Zero Visibility Pincer",
                "Desert FOB \u2014 Dawn Saturation",
                "Coastal Estuary \u2014 Sea-Skimmer Swarm",
                "Mountain Pass \u2014 High-Alt Loiter"
            };
            _scenarioDropdown.choices = mockScenarios;
            if (mockScenarios.Count > 0)
            {
                _scenarioDropdown.value  = mockScenarios[0];
                _selectedScenarioId     = mockScenarios[0];
            }
#else
            // Real path: ScenarioLibrary.LoadAll() from US-P0-B.
            // var scenarios = ScenarioLibrary.LoadAll();
            // _scenarioDropdown.choices = scenarios.ConvertAll(s => s.name);
            // if (scenarios.Count > 0) { _scenarioDropdown.value = scenarios[0].name; }
            Debug.LogWarning("[C2StationController] ScenarioLibrary not yet available. Build US-P0-B.");
#endif
        }

        // ── WebSocket event wiring ────────────────────────────────────────
        private void SubscribeToWsEvents()
        {
#if TRISHUL_MOCK
            // No-op in mock mode.
#else
            // Real path (after US-P0-A lands):
            // WsServer.Instance.OnClientConnected    += OnTraineeConnected;
            // WsServer.Instance.OnClientDisconnected += OnTraineeDisconnected;
            // WsClient.Instance.OnMessageReceived    += OnWsMessageReceived;
#endif
        }

        private void UnsubscribeFromWsEvents()
        {
#if !TRISHUL_MOCK
            // WsServer.Instance.OnClientConnected    -= OnTraineeConnected;
            // WsServer.Instance.OnClientDisconnected -= OnTraineeDisconnected;
            // WsClient.Instance.OnMessageReceived    -= OnWsMessageReceived;
#endif
        }

        // ── Status icon helpers ───────────────────────────────────────────
        public void OnTraineeConnected()
        {
            SetStatusClass("status-connected");
            _statusLabel.text = "\u25CF LAN LINK CONFIRMED";
        }

        public void OnTraineeDisconnected()
        {
            SetStatusClass("status-lost");
            _statusLabel.text = "\u25CB Link lost\u2026";
        }

        private void SetStatusClass(string cls)
        {
            _statusIcon.RemoveFromClassList("status-waiting");
            _statusIcon.RemoveFromClassList("status-connected");
            _statusIcon.RemoveFromClassList("status-lost");
            _statusIcon.AddToClassList(cls);
        }

        // ── Button handlers ───────────────────────────────────────────────
        private void OnLaunchClicked()
        {
#if TRISHUL_MOCK
            Debug.Log($"[C2Station MOCK] LaunchScenario: {_selectedScenarioId}");
            // Directly invoke mock scenario start on CockpitController for editor testing.
            var cockpit = FindObjectOfType<CockpitController>();
            cockpit?.OnScenarioStart_Mock();
#else
            // ScenarioManager.Instance.LaunchScenario(_selectedScenarioId);
#endif
        }

        private void OnAbortClicked()
        {
#if TRISHUL_MOCK
            Debug.Log("[C2Station MOCK] ABORT pressed.");
#else
            // ScenarioManager.Instance.BroadcastScenarioEnd("INSTRUCTOR_ABORT");
#endif
        }

        private void OnLangToggleClicked()
        {
            // Wired to LocaleToggle in UX-P0-B.
            var localeToggle = FindObjectOfType<LocaleToggle>();
            if (localeToggle != null)
                localeToggle.ToggleLanguage();
            else
                Debug.Log("[C2Station] LocaleToggle not yet present (UX-P0-B).");
        }

        // ── Scorecard mirror (stub for UX-P1-D) ──────────────────────────
        public void ShowScorecardMirror(string sessionId)
        {
            _scorecardMirror.RemoveFromClassList("hidden");
            _scorecardStubLabel.text = $"Scorecard received \u2014 session: {sessionId}";
        }

        // ── WS message dispatcher (real path, active when US-P0-A lands) ──
        // private void OnWsMessageReceived(string json)
        // {
        //     var envelope = JsonUtility.FromJson<WsEnvelope>(json);
        //     switch (envelope.type)
        //     {
        //         case "SCORECARD_PAYLOAD":
        //             var payload = JsonUtility.FromJson<ScorecardPayload>(json);
        //             ShowScorecardMirror(payload.payload.scorecard.sessionId);
        //             break;
        //     }
        // }

        // ── Mock helpers ──────────────────────────────────────────────────
#if TRISHUL_MOCK
        [ContextMenu("Mock: Simulate Trainee Connected")]
        public void SimulateTraineeConnected()
        {
            OnTraineeConnected();
        }
#endif
    }
}
