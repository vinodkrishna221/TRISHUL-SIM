// UX-P0-A  ·  CockpitController.cs
// UITK controller for the Trainee Cockpit scene.
//
// Reacts to SCENARIO_START / SCENARIO_TICK / SCENARIO_END / SCORECARD_PAYLOAD
// from the WebSocket IPC (scenario-events.md contract v1).
//
// Mock path active until US-P0-A + US-P0-B merge:
//   OnScenarioStart_Mock(), OnScenarioTick_Mock() called from C2StationController
//   or Unity Editor ContextMenu for isolated editor testing.
//
// Sensor rendering stubs (grey boxes) are replaced in UX-P1-A by:
//   RfWaterfallRenderer, DopplerScopeRenderer, AcousticFftRenderer
//
// OWNED BY: Rani (ui-ux track)
// DO NOT TOUCH: Assets/Scripts/Network/, Assets/Scripts/SwarmAI/, Assets/Scripts/AAR/

using UnityEngine;
using UnityEngine.UIElements;

namespace TRISHUL.UI
{
    /// <summary>
    /// Drives the Trainee Cockpit UITK document.
    /// Binds to TraineeCockpit.uxml via UIDocument on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CockpitController : MonoBehaviour
    {
        // ── Sensor renderer references (wired in UX-P1-A) ─────────────────
        [Header("Sensor Renderers (wired in UX-P1-A)")]
        [SerializeField] private RfWaterfallRenderer  _rfRenderer;     // stub null until UX-P1-A
        [SerializeField] private DopplerScopeRenderer _dopplerRenderer;
        [SerializeField] private AcousticFftRenderer  _acousticRenderer;

        // ── Cached element references ─────────────────────────────────────
        private VisualElement _root;
        private VisualElement _threatIndicator;
        private Label         _threatLabel;
        private VisualElement _controlsRow;
        private VisualElement _scorecardPanel;
        private Label         _scorecardOverlayLabel;
        private Label         _dopplerHzLabel;
        private Label         _acousticSplLabel;

        // ── State ─────────────────────────────────────────────────────────
        private bool _scenarioRunning = false;

        // ─────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            BindElements();
        }

        private void Start()
        {
            SubscribeToWsEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromWsEvents();
        }

        // ── Element binding ───────────────────────────────────────────────
        private void BindElements()
        {
            _threatIndicator      = _root.Q<VisualElement>("threat-indicator");
            _threatLabel          = _root.Q<Label>("threat-label");
            _controlsRow          = _root.Q<VisualElement>("controls-row");
            _scorecardPanel       = _root.Q<VisualElement>("scorecard-panel");
            _scorecardOverlayLabel= _root.Q<Label>("scorecard-overlay-label");
            _dopplerHzLabel       = _root.Q<Label>("doppler-hz-label");
            _acousticSplLabel     = _root.Q<Label>("acoustic-spl-label");
        }

        // ── WebSocket event subscription ──────────────────────────────────
        private void SubscribeToWsEvents()
        {
#if TRISHUL_MOCK
            // No-op in mock mode — methods called directly.
#else
            // Real path (after US-P0-A lands):
            // WsClient.Instance.OnMessageReceived += OnWsMessageReceived;
#endif
        }

        private void UnsubscribeFromWsEvents()
        {
#if !TRISHUL_MOCK
            // WsClient.Instance.OnMessageReceived -= OnWsMessageReceived;
#endif
        }

        // ── SCENARIO_START handler ────────────────────────────────────────
        public void OnScenarioStart(string sessionId, string scenarioId)
        {
            _scenarioRunning = true;

            // Show EW + SMASH controls row (populated in UX-P1-B)
            _controlsRow.RemoveFromClassList("hidden");

            // Reset to GREEN threat state
            SetThreatState("GREEN");

            // Hide scorecard in case of replay
            _scorecardPanel.AddToClassList("hidden");

            Debug.Log($"[Cockpit] SCENARIO_START received — session={sessionId}, scenario={scenarioId}");
        }

        // ── SCENARIO_TICK handler ─────────────────────────────────────────
        /// <summary>
        /// Called every 100 ms by the WS message pump.
        /// In UX-P0-A: updates threat indicator + logs sensor values.
        /// In UX-P1-A: calls actual sensor renderers.
        /// </summary>
        public void OnScenarioTick(
            string    threatState,
            float     rfPeakGHz,
            float     rfPeakDbm,
            float     dopplerHz,
            float     acousticSpl,
            int       radarCount)
        {
            if (!_scenarioRunning) return;

            // Update threat indicator
            SetThreatState(threatState);

            // Update sensor readout labels (stub — real renderers in UX-P1-A)
            if (_dopplerHzLabel != null)
                _dopplerHzLabel.text = $"Doppler: {dopplerHz:F1} Hz";
            if (_acousticSplLabel != null)
                _acousticSplLabel.text = $"SPL: {acousticSpl:F1} dB";

            // Forward to sensor renderers (null-guarded — assigned in UX-P1-A)
            _rfRenderer?.UpdateWaterfall(rfPeakGHz, rfPeakDbm);
            _dopplerRenderer?.UpdateScope(dopplerHz, radarCount);
            _acousticRenderer?.UpdateAcoustic(acousticSpl);
        }

        // ── SCENARIO_END handler ──────────────────────────────────────────
        public void OnScenarioEnd(string reason)
        {
            _scenarioRunning = false;
            _scorecardPanel.RemoveFromClassList("hidden");
            _scorecardOverlayLabel.text = "Computing Scorecard\u2026";
            Debug.Log($"[Cockpit] SCENARIO_END — reason={reason}");
        }

        // ── SCORECARD_PAYLOAD handler ─────────────────────────────────────
        public void OnScorecardReceived(string sessionId)
        {
            // Stub: show session ID — full renderer in UX-P1-D.
            _scorecardOverlayLabel.text = $"Scorecard ready \u2014 session: {sessionId}";
        }

        // ── Threat state helper ───────────────────────────────────────────
        private void SetThreatState(string state)
        {
            _threatIndicator.RemoveFromClassList("threat-green");
            _threatIndicator.RemoveFromClassList("threat-amber");
            _threatIndicator.RemoveFromClassList("threat-red");

            switch (state)
            {
                case "AMBER":
                    _threatIndicator.AddToClassList("threat-amber");
                    _threatLabel.text = "AMBER";
                    break;
                case "RED":
                    _threatIndicator.AddToClassList("threat-red");
                    _threatLabel.text = "RED";
                    break;
                default: // GREEN
                    _threatIndicator.AddToClassList("threat-green");
                    _threatLabel.text = "GREEN";
                    break;
            }
        }

        // ── WS message dispatcher (real path) ─────────────────────────────
        // private void OnWsMessageReceived(string json)
        // {
        //     var envelope = JsonUtility.FromJson<WsEnvelope>(json);
        //     switch (envelope.type)
        //     {
        //         case "SCENARIO_START":
        //             var start = JsonUtility.FromJson<ScenarioStartMsg>(json);
        //             OnScenarioStart(start.payload.sessionId, start.payload.scenarioId);
        //             break;
        //         case "SCENARIO_TICK":
        //             var tick = JsonUtility.FromJson<ScenarioTickMsg>(json);
        //             OnScenarioTick(
        //                 tick.payload.threatState,
        //                 tick.payload.sensorReadings.rfWaterfallPeakGHz,
        //                 tick.payload.sensorReadings.rfWaterfallPeakDbm,
        //                 tick.payload.sensorReadings.dopplerPeakHz,
        //                 tick.payload.sensorReadings.acousticSplDb,
        //                 tick.payload.sensorReadings.radarDetectedCount
        //             );
        //             break;
        //         case "SCENARIO_END":
        //             var end = JsonUtility.FromJson<ScenarioEndMsg>(json);
        //             OnScenarioEnd(end.payload.reason);
        //             break;
        //         case "SCORECARD_PAYLOAD":
        //             var sc = JsonUtility.FromJson<ScorecardPayloadMsg>(json);
        //             OnScorecardReceived(sc.payload.scorecard.sessionId);
        //             var c2 = FindObjectOfType<C2StationController>();
        //             c2?.ShowScorecardMirror(sc.payload.scorecard.sessionId);
        //             break;
        //     }
        // }

        // ── Mock helpers ──────────────────────────────────────────────────
#if TRISHUL_MOCK
        [ContextMenu("Mock: Simulate SCENARIO_START")]
        public void OnScenarioStart_Mock()
        {
            OnScenarioStart("sess-MOCK-0001", "punjab-fog-pincer-01");
        }

        [ContextMenu("Mock: Simulate SCENARIO_TICK (RED)")]
        public void OnScenarioTick_MockRed()
        {
            // SensorMock values from mission brief
            OnScenarioTick("RED",
                rfPeakGHz:    5.825f,
                rfPeakDbm:    -62.3f,
                dopplerHz:    220.4f,
                acousticSpl:  54.2f,
                radarCount:   4);
        }

        [ContextMenu("Mock: Simulate SCENARIO_END")]
        public void OnScenarioEnd_Mock()
        {
            OnScenarioEnd("ALL_NEUTRALISED");
        }
#endif
    }

    // ── Stub type stubs for renderers (removed once UX-P1-A lands) ─────
    // These placeholders let this file compile without the P1-A renderers present.
#if !TRISHUL_P1A_READY
    public class RfWaterfallRenderer  : MonoBehaviour
    {
        public void UpdateWaterfall(float peakGHz, float peakDbm) { }
    }
    public class DopplerScopeRenderer : MonoBehaviour
    {
        public void UpdateScope(float dopplerHz, int radarCount) { }
    }
    public class AcousticFftRenderer  : MonoBehaviour
    {
        public void UpdateAcoustic(float splDb) { }
    }
#endif

    // Stub for LocaleToggle — replaced by UX-P0-B
#if !TRISHUL_P0B_READY
    public class LocaleToggle : MonoBehaviour
    {
        public void ToggleLanguage() { Debug.Log("[LocaleToggle stub] UX-P0-B not yet merged."); }
    }
#endif
}
