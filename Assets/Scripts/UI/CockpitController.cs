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
        private ScorecardPanelController scorecardController;

        // ── EW/SMASH Renderers & Controllers (UX-P1-A/B) ────────
        private RfWaterfallRenderer rfWaterfallRenderer;
        private DopplerScopeRenderer dopplerScopeRenderer;
        private AcousticFftRenderer acousticFftRenderer;
        private EwJammerController ewJammerController;
        private SmashReticleController smashReticleController;
        
        private EWJammerState currentEwState = new EWJammerState();
        private TraineeAction currentTraineeAction = new TraineeAction();
        private SwarmAgentState[] lastSwarmSnapshot = null;
        private long scenarioElapsedMs = 0;

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
        public ScorecardPanelController ScorecardController { get { return scorecardController; } }
        public VisualElement ComputingOverlay { get { return computingOverlay; } }
        public Label DopplerReading { get { return dopplerReading; } }
        public Label AcousticReading { get { return acousticReading; } }
        public RfWaterfallRenderer RfRenderer { get { return rfWaterfallRenderer; } }
        public DopplerScopeRenderer DopplerRenderer { get { return dopplerScopeRenderer; } }
        public AcousticFftRenderer AcousticRenderer { get { return acousticFftRenderer; } }
        public EwJammerController EwController { get { return ewJammerController; } }
        public SmashReticleController SmashController { get { return smashReticleController; } }
        public TraineeAction CurrentTraineeAction { get { return currentTraineeAction; } }
        public EWJammerState CurrentEwState { get { return currentEwState; } }

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

            if (scorecardPanel != null)
            {
                var scorecardRoot = scorecardPanel.Q<VisualElement>("scorecard-root") ?? scorecardPanel;
                if (scorecardController == null)
                {
                    scorecardController = gameObject.GetComponent<ScorecardPanelController>();
                    if (scorecardController == null)
                        scorecardController = gameObject.AddComponent<ScorecardPanelController>();
                }
                scorecardController.BindElements(scorecardRoot);
            }

            InitializeRenderers();
            InitializeControllers();
        }

        private void InitializeRenderers()
        {
            if (rfWaterfallRenderer == null)
            {
                rfWaterfallRenderer = GetComponent<RfWaterfallRenderer>();
                if (rfWaterfallRenderer == null) rfWaterfallRenderer = new RfWaterfallRenderer();
            }
            if (rfContent != null) rfWaterfallRenderer.Initialize(rfContent);

            if (dopplerScopeRenderer == null)
            {
                dopplerScopeRenderer = GetComponent<DopplerScopeRenderer>();
                if (dopplerScopeRenderer == null) dopplerScopeRenderer = new DopplerScopeRenderer();
            }
            if (dopplerContent != null) dopplerScopeRenderer.Initialize(dopplerContent, dopplerReading);

            if (acousticFftRenderer == null)
            {
                acousticFftRenderer = GetComponent<AcousticFftRenderer>();
                if (acousticFftRenderer == null) acousticFftRenderer = new AcousticFftRenderer();
            }
            if (acousticContent != null) acousticFftRenderer.Initialize(acousticContent, acousticReading);
        }

        private void InitializeControllers()
        {
            if (ewJammerController == null)
            {
                ewJammerController = GetComponent<EwJammerController>();
                if (ewJammerController == null) ewJammerController = new EwJammerController();
            }
            if (controlsRow != null) ewJammerController.Initialize(controlsRow);

            if (smashReticleController == null)
            {
                smashReticleController = GetComponent<SmashReticleController>();
                if (smashReticleController == null) smashReticleController = new SmashReticleController();
            }
            if (controlsRow != null)
            {
                var reticleOverlay = root != null ? root.Q<VisualElement>("smash-reticle-overlay") : null;
                smashReticleController.Initialize(controlsRow, reticleOverlay);
            }
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
            scenarioElapsedMs = 0;
            Debug.Log(string.Format("[Cockpit] SCENARIO_START: {0} — {1}", scenarioId, scenarioName));

            if (controlsRow != null)
                controlsRow.RemoveFromClassList("hidden");

            if (ewJammerController != null) ewJammerController.ResetState();
            if (smashReticleController != null) smashReticleController.ResetState();

            currentTraineeAction = new TraineeAction();
            currentEwState = new EWJammerState();

            SetThreatState("GREEN");

            if (scorecardPanel != null)
                scorecardPanel.AddToClassList("hidden");
            if (computingOverlay != null)
                computingOverlay.AddToClassList("hidden");

            var baseline = SensorMock.GetBaselineFixture();
            UpdateSensors(baseline, null);
        }

        /// <summary>
        /// Primary tick entry point taking explicit sensor telemetry values.
        /// Contract: scenario-events.md v1 (SCENARIO_TICK).
        /// </summary>
        public void OnScenarioTick(
            string threatState,
            float rfPeakGHz, float rfPeakDbm,
            float dopplerHz, float acousticDb,
            int radarCount,
            bool friendlyVhfActive = false,
            long elapsedMs = 0)
        {
            if (!scenarioRunning) return;

            scenarioElapsedMs = elapsedMs > 0 ? elapsedMs : scenarioElapsedMs + 100;
            SetThreatState(threatState);

            // 1. Process EW and SMASH controls
            if (ewJammerController != null)
            {
                ewJammerController.OnScenarioTick(threatState, friendlyVhfActive);
                currentEwState = ewJammerController.CurrentState;
            }

            KineticEvent kineticEv = null;
            if (smashReticleController != null)
            {
                kineticEv = smashReticleController.ConsumeKineticTrigger();
            }

            if (ewJammerController != null)
            {
                currentTraineeAction = ewJammerController.BuildTraineeAction(kineticEv);
            }
            else
            {
                currentTraineeAction = new TraineeAction(0f, 0f, false, null, kineticEv);
            }

            if (smashReticleController != null)
            {
                smashReticleController.UpdateReticle(lastSwarmSnapshot, currentTraineeAction);
            }

            // 2. Update sensor suite
            var sr = new SensorReadings(rfPeakGHz, rfPeakDbm, dopplerHz, acousticDb, radarCount);
            UpdateSensors(sr, lastSwarmSnapshot);

            if (dopplerReading != null && LocaleToggle.Instance != null)
                dopplerReading.text = LocaleToggle.Instance.GetFormatted("DOPPLER_READING", dopplerHz);
            if (acousticReading != null && LocaleToggle.Instance != null)
                acousticReading.text = LocaleToggle.Instance.GetFormatted("ACOUSTIC_READING", acousticDb);
        }

        /// <summary>
        /// Overload taking full contract SensorReadings and SwarmAgentState snapshots.
        /// </summary>
        public void OnScenarioTick(string threatState, SensorReadings sr, SwarmAgentState[] swarm = null, bool friendlyVhfActive = false, long elapsedMs = 0)
        {
            if (!scenarioRunning) return;

            scenarioElapsedMs = elapsedMs > 0 ? elapsedMs : scenarioElapsedMs + 100;
            SetThreatState(threatState);
            if (swarm != null)
            {
                lastSwarmSnapshot = swarm;
            }

            // 1. Process EW and SMASH controls
            if (ewJammerController != null)
            {
                ewJammerController.OnScenarioTick(threatState, friendlyVhfActive);
                currentEwState = ewJammerController.CurrentState;
            }

            KineticEvent kineticEv = null;
            if (smashReticleController != null)
            {
                kineticEv = smashReticleController.ConsumeKineticTrigger();
            }

            if (ewJammerController != null)
            {
                currentTraineeAction = ewJammerController.BuildTraineeAction(kineticEv);
            }
            else
            {
                currentTraineeAction = new TraineeAction(0f, 0f, false, null, kineticEv);
            }

            if (smashReticleController != null)
            {
                smashReticleController.UpdateReticle(swarm ?? lastSwarmSnapshot, currentTraineeAction);
            }

            // 2. Update sensor suite
            UpdateSensors(sr, swarm ?? lastSwarmSnapshot);
        }

        /// <summary>
        /// Helper to trigger SMASH 2000 kinetic firing at current scenario timestamp.
        /// </summary>
        public KineticEvent TriggerFireKinetic()
        {
            if (smashReticleController != null)
            {
                return smashReticleController.TriggerFire(scenarioElapsedMs);
            }
            return null;
        }

        /// <summary>
        /// Helper to configure EW Jammer controls.
        /// </summary>
        public void SetEwParameters(int azimuth, int elevation, float bandGHz, bool engaged)
        {
            if (ewJammerController != null)
            {
                ewJammerController.SetAzimuth(azimuth);
                ewJammerController.SetElevation(elevation);
                ewJammerController.SetBandGHz(bandGHz);
                ewJammerController.SetEngaged(engaged);
                currentEwState = ewJammerController.CurrentState;
            }
        }

        /// <summary>
        /// Dispatches sensor telemetry to all three live renderers.
        /// </summary>
        private void UpdateSensors(SensorReadings sr, SwarmAgentState[] swarm)
        {
            if (sr == null) return;

            // 1. Update RF Waterfall
            if (rfWaterfallRenderer != null && rfWaterfallRenderer.IsInitialized)
            {
                rfWaterfallRenderer.UpdateWaterfall(sr);
            }

            // 2. Update Doppler Scope
            if (dopplerScopeRenderer != null && dopplerScopeRenderer.IsInitialized)
            {
                dopplerScopeRenderer.UpdateScope(sr, swarm);
            }

            // 3. Update Acoustic FFT
            if (acousticFftRenderer != null && acousticFftRenderer.IsInitialized)
            {
                acousticFftRenderer.UpdateAcoustic(sr);
            }
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
        /// Called when SCORECARD_PAYLOAD is received with a full Scorecard object.
        /// Contract: scorecard.md v1 — consumer reaction:
        ///   Show scorecard panel, hide computing overlay, bind Scorecard.
        /// </summary>
        public void OnScorecardReceived(Scorecard scorecard)
        {
            if (scorecard == null) return;
            Debug.Log(string.Format("[Cockpit] SCORECARD_PAYLOAD received for session {0}.", scorecard.sessionId));

            // Hide computing overlay
            if (computingOverlay != null)
                computingOverlay.AddToClassList("hidden");

            // Show scorecard panel
            if (scorecardPanel != null)
                scorecardPanel.RemoveFromClassList("hidden");

            if (scorecardStubLabel != null)
                scorecardStubLabel.text = LocaleToggle.Instance.GetFormatted("SCORECARD_RECEIVED", scorecard.sessionId);

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
