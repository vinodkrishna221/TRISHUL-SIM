// ============================================================
// CockpitController.cs — Trainee Cockpit UIDocument Controller
// TRISHUL-SIM · UX-P0-A + UX-P0-B · ui-ux track (Rani)
//
// Binds TraineeCockpit.uxml elements. Reacts to scenario events:
// SCENARIO_START → show panes, set GREEN
// SCENARIO_TICK  → update threat indicator, forward sensor data
// SCENARIO_END   → show computing overlay
// SCORECARD_PAYLOAD → show scorecard panel
//
// Dependencies US-P0-A/B not merged — uses mock path.
//
// UX-P0-B: All label text uses LocaleToggle.GetString(key).
//          Subscribes to OnLocaleChanged for runtime rebind.
// ============================================================

using UnityEngine;
using UnityEngine.UIElements;

namespace TrishulSim.UI
{
    /// <summary>
    /// MonoBehaviour attached to the TraineeCockpit scene's UIDocument.
    /// Manages the Trainee HUD: threat indicator, sensor pane stubs,
    /// controls row visibility, scorecard overlay, and computing state.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CockpitController : MonoBehaviour
    {
        // ── UI Element References ──────────────────────────────
        private VisualElement root;
        private VisualElement threatIndicator;
        private Label threatLabel;

        // Sensor panes (stub boxes — replaced by renderers in UX-P1-A)
        private VisualElement rfWaterfallPane;
        private VisualElement rfContent;
        private VisualElement dopplerPane;
        private VisualElement dopplerContent;
        private Label dopplerReading;
        private VisualElement acousticPane;
        private VisualElement acousticContent;
        private Label acousticReading;

        // Controls and overlays
        private VisualElement controlsRow;
        private VisualElement scorecardPanel;
        private Label scorecardStubLabel;
        private VisualElement computingOverlay;

        // ── State ─────────────────────────────────────────────
        private bool scenarioRunning = false;
        private string currentThreatState = "GREEN";

        // ── Public Accessors for Tests / UI Validation ────────
        public bool ScenarioRunning { get { return scenarioRunning; } }
        public string CurrentThreatState { get { return currentThreatState; } }
        public VisualElement Root { get { return root; } }
        public VisualElement ThreatIndicator { get { return threatIndicator; } }
        public Label ThreatLabel { get { return threatLabel; } }
        public VisualElement ControlsRow { get { return controlsRow; } }
        public VisualElement ScorecardPanel { get { return scorecardPanel; } }
        public VisualElement ComputingOverlay { get { return computingOverlay; } }
        public Label DopplerReading { get { return dopplerReading; } }
        public Label AcousticReading { get { return acousticReading; } }

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
            // ── UX-P0-B: Subscribe to locale changes ──────────
            LocaleToggle.Instance.OnLocaleChanged += OnLocaleChangedCockpit;
            RefreshLocalizedLabels();

            // ── MOCK PATH (US-P0-A/B not merged) ────────────
            // Simulate SCENARIO_START for editor testing.
            // Remove this block once US-P0-B (ScenarioManager) is merged.
#if UNITY_EDITOR
            SimulateScenarioStart();
#endif
        }

        private void OnDestroy()
        {
            // ── UX-P0-B: Unsubscribe locale listener ─────────
            if (LocaleToggle.Instance != null)
                LocaleToggle.Instance.OnLocaleChanged -= OnLocaleChangedCockpit;
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

            threatIndicator    = root.Q<VisualElement>("threat-indicator");
            threatLabel        = root.Q<Label>("threat-label");

            rfWaterfallPane    = root.Q<VisualElement>("rf-waterfall-pane");
            rfContent          = root.Q<VisualElement>("rf-content");
            dopplerPane        = root.Q<VisualElement>("doppler-pane");
            dopplerContent     = root.Q<VisualElement>("doppler-content");
            dopplerReading     = root.Q<Label>("doppler-reading");
            acousticPane       = root.Q<VisualElement>("acoustic-pane");
            acousticContent    = root.Q<VisualElement>("acoustic-content");
            acousticReading    = root.Q<Label>("acoustic-reading");

            controlsRow        = root.Q<VisualElement>("controls-row");
            scorecardPanel     = root.Q<VisualElement>("scorecard-panel");
            scorecardStubLabel = root.Q<Label>("scorecard-stub-label");
            computingOverlay   = root.Q<VisualElement>("computing-overlay");
        }

        // ══════════════════════════════════════════════════════
        // Scenario Event Handlers
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Called when SCENARIO_START is received.
        /// Contract: scenario-events.md v1 — consumer reaction:
        ///   "hide lobby screen; show Sensor Suite panes; set threat state to GREEN"
        /// Wire to WsClient.OnMessageReceived → type == "SCENARIO_START"
        /// once US-P0-A is merged.
        /// </summary>
        public void OnScenarioStart(string scenarioId, string scenarioName)
        {
            scenarioRunning = true;
            Debug.Log(string.Format("[Cockpit] SCENARIO_START: {0} — {1}", scenarioId, scenarioName));

            // Show controls row (EW + SMASH controls added in UX-P1-B)
            if (controlsRow != null)
                controlsRow.RemoveFromClassList("hidden");

            // Set threat indicator to GREEN
            SetThreatState("GREEN");

            // Hide scorecard panel and computing overlay
            if (scorecardPanel != null)
                scorecardPanel.AddToClassList("hidden");
            if (computingOverlay != null)
                computingOverlay.AddToClassList("hidden");
        }

        /// <summary>
        /// Called on each SCENARIO_TICK (10 Hz).
        /// Contract: scenario-events.md v1 — consumer reaction:
        ///   "redraws RF Waterfall, Doppler scope, Acoustic FFT, threat-state indicator"
        /// </summary>
        /// <param name="threatState">"GREEN" | "AMBER" | "RED"</param>
        /// <param name="rfPeakGHz">RF waterfall peak frequency in GHz</param>
        /// <param name="rfPeakDbm">RF waterfall peak power in dBm</param>
        /// <param name="dopplerHz">Micro-Doppler peak frequency in Hz</param>
        /// <param name="acousticDb">Acoustic SPL in dB</param>
        /// <param name="radarCount">Number of radar returns above threshold</param>
        public void OnScenarioTick(
            string threatState,
            float rfPeakGHz, float rfPeakDbm,
            float dopplerHz, float acousticDb,
            int radarCount)
        {
            if (!scenarioRunning) return;

            // Update threat indicator
            SetThreatState(threatState);

            // Forward sensor data to renderers (stub: update reading labels)
            // Replace with actual renderer calls in UX-P1-A:
            //   RfWaterfallRenderer.UpdateWaterfall(sensorReadings)
            //   DopplerScopeRenderer.UpdateScope(sensorReadings, swarm)
            //   AcousticFftRenderer.UpdateAcoustic(sensorReadings)
            if (dopplerReading != null)
                dopplerReading.text = LocaleToggle.Instance.GetFormatted("DOPPLER_READING", dopplerHz);
            if (acousticReading != null)
                acousticReading.text = LocaleToggle.Instance.GetFormatted("ACOUSTIC_READING", acousticDb);

            // Log sensor values (stub — removed when renderers are wired)
            Debug.Log(string.Format("[Cockpit][TICK] threat={0} RF={1}GHz/{2}dBm Doppler={3}Hz Acoustic={4}dB radar={5}",
                threatState, rfPeakGHz, rfPeakDbm, dopplerHz, acousticDb, radarCount));
        }

        /// <summary>
        /// Called when SCENARIO_END is received.
        /// Contract: scenario-events.md v1 — consumer reaction:
        ///   "freeze sensor panes; show 'Computing Scorecard…' overlay"
        /// </summary>
        public void OnScenarioEnd(string reason)
        {
            scenarioRunning = false;
            Debug.Log(string.Format("[Cockpit] SCENARIO_END: reason={0}", reason));

            // Show computing overlay
            if (computingOverlay != null)
                computingOverlay.RemoveFromClassList("hidden");
        }

        /// <summary>
        /// Called when SCORECARD_PAYLOAD is received.
        /// Contract: scorecard.md v1 — consumer reaction:
        ///   Show scorecard panel, hide computing overlay.
        /// Stub: shows sessionId text. Full renderer in UX-P1-D (Rajeswari).
        /// </summary>
        public void OnScorecardReceived(string sessionId)
        {
            Debug.Log(string.Format("[Cockpit] SCORECARD_PAYLOAD received for session {0}.", sessionId));

            // Hide computing overlay
            if (computingOverlay != null)
                computingOverlay.AddToClassList("hidden");

            // Show scorecard panel (stub — full render in UX-P1-D)
            if (scorecardPanel != null)
                scorecardPanel.RemoveFromClassList("hidden");
            if (scorecardStubLabel != null)
                scorecardStubLabel.text = LocaleToggle.Instance.GetFormatted("SCORECARD_RECEIVED", sessionId);
        }

        // ══════════════════════════════════════════════════════
        // Localization (UX-P0-B)
        // ══════════════════════════════════════════════════════

        private void OnLocaleChangedCockpit(TrishulLocale newLocale)
        {
            RefreshLocalizedLabels();
        }

        public void RefreshLocalizedLabels()
        {
            var loc = LocaleToggle.Instance;

            // Re-apply threat state string based on current state
            SetThreatState(currentThreatState);

            // Re-apply static labels if they exist
            if (computingOverlay != null)
            {
                var lbl = computingOverlay.Q<Label>();
                if (lbl != null) lbl.text = loc.GetString("COMPUTING_SCORECARD");
            }
        }

        // ══════════════════════════════════════════════════════
        // Threat State Management
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Updates the threat indicator visual.
        /// MIL-STD-2525 colours: GREEN #00B300, AMBER #FFB300, RED #CC0000.
        /// </summary>
        public void SetThreatState(string state)
        {
            currentThreatState = state;

            if (threatIndicator == null)
                return;

            // Remove all threat classes
            threatIndicator.RemoveFromClassList("threat-green");
            threatIndicator.RemoveFromClassList("threat-amber");
            threatIndicator.RemoveFromClassList("threat-red");

            var loc = LocaleToggle.Instance;

            // Apply new threat class
            switch (state)
            {
                case "GREEN":
                    threatIndicator.AddToClassList("threat-green");
                    if (threatLabel != null) threatLabel.text = loc.GetString("THREAT_GREEN");
                    break;
                case "AMBER":
                    threatIndicator.AddToClassList("threat-amber");
                    if (threatLabel != null) threatLabel.text = loc.GetString("THREAT_AMBER");
                    break;
                case "RED":
                    threatIndicator.AddToClassList("threat-red");
                    if (threatLabel != null) threatLabel.text = loc.GetString("THREAT_RED");
                    break;
                default:
                    threatIndicator.AddToClassList("threat-green");
                    if (threatLabel != null) threatLabel.text = loc.GetString("THREAT_GREEN");
                    Debug.LogWarning(string.Format("[Cockpit] Unknown threat state: {0}", state));
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // Mock Path (Editor Only)
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        /// <summary>
        /// Simulates SCENARIO_START + one TICK for editor testing.
        /// Uses SensorMock values from mission: rfWaterfallPeakGHz=5.825,
        /// rfWaterfallPeakDbm=-62.3, dopplerPeakHz=220.4, acousticSplDb=54.2.
        /// Remove once US-P0-A/B are merged.
        /// </summary>
        private void SimulateScenarioStart()
        {
            Debug.Log("[Cockpit][MOCK] Simulating SCENARIO_START (US-P0-A/B not merged).");
            OnScenarioStart("punjab-fog-pincer-01", "Punjab Border — Zero Visibility Pincer");

            // Simulate one tick with mock sensor data
            OnScenarioTick(
                threatState: "GREEN",
                rfPeakGHz: 5.825f,
                rfPeakDbm: -62.3f,
                dopplerHz: 220.4f,
                acousticDb: 54.2f,
                radarCount: 4
            );
        }

        /// <summary>
        /// Editor helper: cycle threat states on keypress for visual testing.
        /// T key cycles GREEN → AMBER → RED → GREEN.
        /// Safely wrapped to prevent input system mismatch crashes.
        /// </summary>
        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.T))
                {
                    string next;
                    switch (currentThreatState)
                    {
                        case "GREEN": next = "AMBER"; break;
                        case "AMBER": next = "RED"; break;
                        case "RED": next = "GREEN"; break;
                        default: next = "GREEN"; break;
                    }
                    Debug.Log(string.Format("[Cockpit][MOCK] Cycling threat state → {0}", next));
                    SetThreatState(next);
                }
            }
            catch (System.Exception)
            {
                // Degrade gracefully if new Input System is active in editor
            }
        }
#endif
    }
}
