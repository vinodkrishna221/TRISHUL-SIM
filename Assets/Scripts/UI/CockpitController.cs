// ============================================================
// CockpitController.cs — Trainee Cockpit UIDocument Controller
// TRISHUL-SIM · UX-P0-A + UX-P0-B + UX-P1-A · ui-ux track (Rani)
//
// Binds TraineeCockpit.uxml elements and connects the live sensor suite:
// 1. RF Waterfall Spectrogram (RfWaterfallRenderer)
// 2. Micro-Doppler Circular Radar PPI Scope (DopplerScopeRenderer)
// 3. Acoustic FFT Sonar Bar Equalizer (AcousticFftRenderer)
// 4. MIL-STD-2525 Threat State Indicator (GREEN / AMBER / RED)
// 5. Controls row, Scorecard panel, and Computing overlays.
//
// UX-P0-B: All label text uses LocaleToggle.GetString(key).
//          Subscribes to OnLocaleChanged for runtime rebind.
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrishulSim.UI
{
    /// <summary>
    /// MonoBehaviour attached to the TraineeCockpit scene's UIDocument.
    /// Manages HUD state, live sensor renderers, threat level, and scenario lifecycle.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CockpitController : MonoBehaviour
    {
        // ── UI Element References ──────────────────────────────
        private VisualElement root;
        private VisualElement threatIndicator;
        private Label threatLabel;

        // Sensor pane containers & readout labels
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

        // ── Live Sensor Renderers ─────────────────────────────
        private RfWaterfallRenderer rfWaterfallRenderer;
        private DopplerScopeRenderer dopplerScopeRenderer;
        private AcousticFftRenderer acousticFftRenderer;

        // ── EW & Fire-Control Controllers (UX-P1-B) ───────────
        private EwJammerController ewJammerController;
        private SmashReticleController smashReticleController;
        private TraineeAction currentTraineeAction = new TraineeAction();
        private EWJammerState currentEwState = new EWJammerState();

        // ── State ─────────────────────────────────────────────
        private bool scenarioRunning = false;
        private string currentThreatState = "GREEN";
        private SwarmAgentState[] lastSwarmSnapshot = null;
        private long scenarioElapsedMs = 0;

        // ── Public Accessors for Tests / UI Validation ────────
        public bool ScenarioRunning { get { return scenarioRunning; } }
        public string CurrentThreatState { get { return currentThreatState; } }
        public VisualElement Root { get { return root; } }
        public VisualElement ThreatIndicator { get { return threatIndicator; } }
        public Label ThreatLabel { get { return threatLabel; } }
        public VisualElement RfContent { get { return rfContent; } }
        public VisualElement DopplerContent { get { return dopplerContent; } }
        public VisualElement AcousticContent { get { return acousticContent; } }
        public Label DopplerReading { get { return dopplerReading; } }
        public Label AcousticReading { get { return acousticReading; } }
        public VisualElement ControlsRow { get { return controlsRow; } }
        public VisualElement ScorecardPanel { get { return scorecardPanel; } }
        public VisualElement ComputingOverlay { get { return computingOverlay; } }

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
            if (LocaleToggle.Instance != null)
            {
                LocaleToggle.Instance.OnLocaleChanged += OnLocaleChangedCockpit;
                RefreshLocalizedLabels();
            }

            // ── MOCK PATH (US-P0-A/B / SA-P0-B / SA-P1-A not merged) ──
            // Simulates SCENARIO_START and Punjab incursion tick for editor testing.
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
        // UI Binding & Renderer Initialisation
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

            InitializeRenderers();
            InitializeControllers();
        }

        /// <summary>
        /// Instantiates and initializes the 3 sensor suite renderers.
        /// </summary>
        private void InitializeRenderers()
        {
            // 1. RF Waterfall Renderer
            if (rfWaterfallRenderer == null)
            {
                rfWaterfallRenderer = GetComponent<RfWaterfallRenderer>();
                if (rfWaterfallRenderer == null)
                    rfWaterfallRenderer = new RfWaterfallRenderer();
            }
            if (rfContent != null)
            {
                rfWaterfallRenderer.Initialize(rfContent);
            }

            // 2. Micro-Doppler Scope Renderer
            if (dopplerScopeRenderer == null)
            {
                dopplerScopeRenderer = GetComponent<DopplerScopeRenderer>();
                if (dopplerScopeRenderer == null)
                    dopplerScopeRenderer = new DopplerScopeRenderer();
            }
            if (dopplerContent != null)
            {
                dopplerScopeRenderer.Initialize(dopplerContent, dopplerReading);
            }

            // 3. Acoustic FFT Sonar Renderer
            if (acousticFftRenderer == null)
            {
                acousticFftRenderer = GetComponent<AcousticFftRenderer>();
                if (acousticFftRenderer == null)
                    acousticFftRenderer = new AcousticFftRenderer();
            }
            if (acousticContent != null)
            {
                acousticFftRenderer.Initialize(acousticContent, acousticReading);
            }
        }

        /// <summary>
        /// Instantiates and initializes the EW Jammer and SMASH 2000 controllers (UX-P1-B).
        /// </summary>
        private void InitializeControllers()
        {
            // 1. EW Jammer Controller
            if (ewJammerController == null)
            {
                ewJammerController = GetComponent<EwJammerController>();
                if (ewJammerController == null)
                    ewJammerController = new EwJammerController();
            }
            if (controlsRow != null)
            {
                ewJammerController.Initialize(controlsRow);
            }

            // 2. SMASH 2000 Reticle Controller
            if (smashReticleController == null)
            {
                smashReticleController = GetComponent<SmashReticleController>();
                if (smashReticleController == null)
                    smashReticleController = new SmashReticleController();
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
        /// Called when SCENARIO_START is received from Instructor C2.
        /// Contract: scenario-events.md v1.
        /// </summary>
        public void OnScenarioStart(string scenarioId, string scenarioName)
        {
            scenarioRunning = true;
            scenarioElapsedMs = 0;
            Debug.Log(string.Format("[Cockpit] SCENARIO_START: {0} — {1}", scenarioId, scenarioName));

            // Show controls row (EW + SMASH controls)
            if (controlsRow != null)
                controlsRow.RemoveFromClassList("hidden");

            // Reset EW and SMASH controllers
            if (ewJammerController != null)
                ewJammerController.ResetState();
            if (smashReticleController != null)
                smashReticleController.ResetState();

            currentTraineeAction = new TraineeAction();
            currentEwState = new EWJammerState();

            // Set initial threat indicator to GREEN (peace/quiet)
            SetThreatState("GREEN");

            // Hide scorecard panel and computing overlay
            if (scorecardPanel != null)
                scorecardPanel.AddToClassList("hidden");
            if (computingOverlay != null)
                computingOverlay.AddToClassList("hidden");

            // Initial baseline update for sensors
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

            if (dopplerReading != null)
                dopplerReading.text = LocaleToggle.Instance != null
                    ? LocaleToggle.Instance.GetFormatted("DOPPLER_READING", dopplerHz)
                    : string.Format("Doppler: {0:F1} Hz", dopplerHz);
            if (acousticReading != null)
                acousticReading.text = LocaleToggle.Instance != null
                    ? LocaleToggle.Instance.GetFormatted("ACOUSTIC_READING", acousticDb)
                    : string.Format("SPL: {0:F1} dB", acousticDb);

            Debug.Log(string.Format("[Cockpit][TICK] threat={0} RF={1:F3}GHz/{2:F1}dBm Doppler={3:F1}Hz Acoustic={4:F1}dB radar={5} ewEngaged={6} roeRisk={7}",
                threatState, rfPeakGHz, rfPeakDbm, dopplerHz, acousticDb, radarCount, currentEwState.engaged, currentEwState.friendlyBlackoutRisk));
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
        /// Contract: scenario-events.md v1.
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
        /// Contract: scorecard.md v1.
        /// </summary>
        public void OnScorecardReceived(string sessionId)
        {
            Debug.Log(string.Format("[Cockpit] SCORECARD_PAYLOAD received for session {0}.", sessionId));

            if (computingOverlay != null)
                computingOverlay.AddToClassList("hidden");

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

            threatIndicator.RemoveFromClassList("threat-green");
            threatIndicator.RemoveFromClassList("threat-amber");
            threatIndicator.RemoveFromClassList("threat-red");

            var loc = LocaleToggle.Instance;

            switch (state)
            {
                case "GREEN":
                    threatIndicator.AddToClassList("threat-green");
                    if (threatLabel != null) threatLabel.text = loc != null ? loc.GetString("THREAT_GREEN") : "GREEN";
                    break;
                case "AMBER":
                    threatIndicator.AddToClassList("threat-amber");
                    if (threatLabel != null) threatLabel.text = loc != null ? loc.GetString("THREAT_AMBER") : "AMBER";
                    break;
                case "RED":
                    threatIndicator.AddToClassList("threat-red");
                    if (threatLabel != null) threatLabel.text = loc != null ? loc.GetString("THREAT_RED") : "RED";
                    break;
                default:
                    threatIndicator.AddToClassList("threat-green");
                    if (threatLabel != null) threatLabel.text = loc != null ? loc.GetString("THREAT_GREEN") : "GREEN";
                    Debug.LogWarning(string.Format("[Cockpit] Unknown threat state: {0}", state));
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // Mock Path (Editor Only)
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void SimulateScenarioStart()
        {
            Debug.Log("[Cockpit][MOCK] Simulating SCENARIO_START with Punjab Incursion sensors.");
            OnScenarioStart("punjab-fog-pincer-01", "Punjab Border — Zero Visibility Pincer");

            var punjabReadings = SensorMock.GetPunjabFixture();
            var punjabSwarm = SensorMock.GetPunjabSwarmFixture();
            OnScenarioTick("RED", punjabReadings, punjabSwarm);
        }

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
            catch (Exception)
            {
                // Degrade gracefully if new Input System is active
            }
        }
#endif
    }
}
