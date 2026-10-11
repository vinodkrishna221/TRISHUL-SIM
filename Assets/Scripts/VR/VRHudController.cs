// ============================================================
// VRHudController.cs — Floating 3D Sensor HUD Panel Controller
// TRISHUL-SIM · VG-P1-C · vr-gamepad track (Uday)
//
// Converts existing 2D sensor renderers into world-space 3D
// floating panels via RenderTextures. The "Iron Man helmet display".
//
// ADDITIVE change: existing 2D HUD path is unchanged.
// Reads from renderers' Texture2D output via Graphics.Blit into
// RenderTextures assigned to world-space Quad materials.
//
// Threat indicator torus color follows MIL-STD-2525:
//   GREEN=#00B300, AMBER=#FFB300, RED=#CC0000
// ============================================================

using System;
using UnityEngine;

namespace TrishulSim.VR
{
    /// <summary>
    /// MonoBehaviour on VRRig. Drives 4 floating HUD panels via RenderTextures
    /// and a threat indicator torus that changes color with threat state.
    /// Subscribes to ScenarioManager events identically to CockpitController.
    /// </summary>
    public class VRHudController : MonoBehaviour
    {
        // ── RenderTexture References ─────────────────────────────
        [Header("RenderTextures (assign in Inspector or auto-created by VRHudSetup)")]
        [SerializeField] private RenderTexture rtRfWaterfall;
        [SerializeField] private RenderTexture rtDopplerScope;
        [SerializeField] private RenderTexture rtAcousticFft;
        [SerializeField] private RenderTexture rtEwSmashPanel;

        // ── Panel Quad References ────────────────────────────────
        [Header("Panel GameObjects (assign in Inspector or auto-created by VRHudSetup)")]
        [SerializeField] private Renderer panelRfRenderer;
        [SerializeField] private Renderer panelDopplerRenderer;
        [SerializeField] private Renderer panelAcousticRenderer;
        [SerializeField] private Renderer panelEwSmashRenderer;

        // ── Threat Torus ─────────────────────────────────────────
        [Header("Threat Indicator")]
        [SerializeField] private Renderer threatTorusRenderer;

        // ── Sensor Renderers (found at runtime) ──────────────────
        private UI.RfWaterfallRenderer rfRenderer;
        private UI.DopplerScopeRenderer dopplerRenderer;
        private UI.AcousticFftRenderer acousticRenderer;

        // ── MIL-STD-2525 Threat Colors ──────────────────────────
        public static readonly Color ThreatGreen = new Color(0f, 0.702f, 0f, 1f);      // #00B300
        public static readonly Color ThreatAmber = new Color(1f, 0.702f, 0f, 1f);       // #FFB300
        public static readonly Color ThreatRed   = new Color(0.8f, 0f, 0f, 1f);         // #CC0000

        // ── State ────────────────────────────────────────────────
        private string currentThreatState = "GREEN";
        private bool isScenarioRunning = false;
        private bool isInitialized = false;
        private MaterialPropertyBlock threatMpb;

        // ── Public Accessors (for self-test) ─────────────────────
        public RenderTexture RtRfWaterfall      => rtRfWaterfall;
        public RenderTexture RtDopplerScope     => rtDopplerScope;
        public RenderTexture RtAcousticFft      => rtAcousticFft;
        public RenderTexture RtEwSmashPanel     => rtEwSmashPanel;
        public Renderer PanelRfRenderer         => panelRfRenderer;
        public Renderer PanelDopplerRenderer    => panelDopplerRenderer;
        public Renderer PanelAcousticRenderer   => panelAcousticRenderer;
        public Renderer PanelEwSmashRenderer    => panelEwSmashRenderer;
        public Renderer ThreatTorusRenderer     => threatTorusRenderer;
        public string CurrentThreatState        => currentThreatState;
        public bool IsScenarioRunning           => isScenarioRunning;
        public bool IsInitialized               => isInitialized;

        // ══════════════════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            threatMpb = new MaterialPropertyBlock();
        }

        private void Start()
        {
            // Find sensor renderers in scene (additive — these also serve the 2D HUD)
            FindSensorRenderers();

            // Subscribe to ScenarioManager events
            SubscribeToScenarioEvents();

            // Validate RT assignments
            ValidateAndInitialize();

            Debug.Log("[VRHudController] Initialized. Panels ready for scenario data.");
        }

        private void OnDestroy()
        {
            UnsubscribeFromScenarioEvents();
        }

        // ══════════════════════════════════════════════════════════
        // Initialization
        // ══════════════════════════════════════════════════════════

        private void FindSensorRenderers()
        {
#if UNITY_5_3_OR_NEWER
            rfRenderer = FindAnyObjectByType<UI.RfWaterfallRenderer>();
            dopplerRenderer = FindAnyObjectByType<UI.DopplerScopeRenderer>();
            acousticRenderer = FindAnyObjectByType<UI.AcousticFftRenderer>();
#endif
            if (rfRenderer == null)
                Debug.LogWarning("[VRHudController] RfWaterfallRenderer not found — RF panel will show static.");
            if (dopplerRenderer == null)
                Debug.LogWarning("[VRHudController] DopplerScopeRenderer not found — Doppler panel will show static.");
            if (acousticRenderer == null)
                Debug.LogWarning("[VRHudController] AcousticFftRenderer not found — Acoustic panel will show static.");
        }

        private void ValidateAndInitialize()
        {
            bool valid = true;

            if (rtRfWaterfall == null)    { Debug.LogWarning("[VRHudController] RT_RfWaterfall not assigned."); valid = false; }
            if (rtDopplerScope == null)   { Debug.LogWarning("[VRHudController] RT_DopplerScope not assigned."); valid = false; }
            if (rtAcousticFft == null)    { Debug.LogWarning("[VRHudController] RT_AcousticFft not assigned."); valid = false; }
            if (rtEwSmashPanel == null)   { Debug.LogWarning("[VRHudController] RT_EwSmashPanel not assigned."); valid = false; }

            if (panelRfRenderer == null)       { Debug.LogWarning("[VRHudController] Panel_RF renderer not assigned."); valid = false; }
            if (panelDopplerRenderer == null)   { Debug.LogWarning("[VRHudController] Panel_Doppler renderer not assigned."); valid = false; }
            if (panelAcousticRenderer == null)  { Debug.LogWarning("[VRHudController] Panel_Acoustic renderer not assigned."); valid = false; }
            if (panelEwSmashRenderer == null)   { Debug.LogWarning("[VRHudController] Panel_EwSmash renderer not assigned."); valid = false; }

            isInitialized = valid;

            if (isInitialized)
            {
                // Assign RenderTextures to panel materials
                AssignRtToPanel(panelRfRenderer, rtRfWaterfall);
                AssignRtToPanel(panelDopplerRenderer, rtDopplerScope);
                AssignRtToPanel(panelAcousticRenderer, rtAcousticFft);
                AssignRtToPanel(panelEwSmashRenderer, rtEwSmashPanel);

                // Set initial threat torus color
                SetThreatTorusColor("GREEN");
            }
        }

        private void AssignRtToPanel(Renderer panelRenderer, RenderTexture rt)
        {
            if (panelRenderer == null || rt == null) return;

            var mpb = new MaterialPropertyBlock();
            panelRenderer.GetPropertyBlock(mpb);
            mpb.SetTexture("_MainTex", rt);
            panelRenderer.SetPropertyBlock(mpb);
        }

        // ══════════════════════════════════════════════════════════
        // Scenario Event Subscription
        // ══════════════════════════════════════════════════════════

        private void SubscribeToScenarioEvents()
        {
#if UNITY_5_3_OR_NEWER
            if (Scenarios.ScenarioManager.Instance == null) return;

            var sm = Scenarios.ScenarioManager.Instance;
            sm.OnScenarioStartBroadcast += HandleScenarioStart;
            sm.OnScenarioStartReceived  += HandleScenarioStart;
            sm.OnScenarioTickBroadcast  += HandleScenarioTick;
            sm.OnScenarioTickReceived   += HandleScenarioTick;
            sm.OnScenarioEndBroadcast   += HandleScenarioEnd;
            sm.OnScenarioEndReceived    += HandleScenarioEnd;
#endif
        }

        private void UnsubscribeFromScenarioEvents()
        {
#if UNITY_5_3_OR_NEWER
            if (Scenarios.ScenarioManager.Instance == null) return;

            var sm = Scenarios.ScenarioManager.Instance;
            sm.OnScenarioStartBroadcast -= HandleScenarioStart;
            sm.OnScenarioStartReceived  -= HandleScenarioStart;
            sm.OnScenarioTickBroadcast  -= HandleScenarioTick;
            sm.OnScenarioTickReceived   -= HandleScenarioTick;
            sm.OnScenarioEndBroadcast   -= HandleScenarioEnd;
            sm.OnScenarioEndReceived    -= HandleScenarioEnd;
#endif
        }

        // ══════════════════════════════════════════════════════════
        // Event Handlers
        // ══════════════════════════════════════════════════════════

#if UNITY_5_3_OR_NEWER
        private void HandleScenarioStart(Scenarios.ScenarioStartPayload start)
        {
            isScenarioRunning = true;
            currentThreatState = "GREEN";
            SetThreatTorusColor("GREEN");

            Debug.Log(string.Format("[VRHudController] SCENARIO_START: {0}", start.scenarioId));

            // Clear RenderTextures to black
            ClearAllRenderTextures();
        }

        private void HandleScenarioTick(Scenarios.ScenarioTickPayload tick)
        {
            if (!isScenarioRunning || !isInitialized) return;

            // Update threat state
            if (!string.IsNullOrEmpty(tick.threatState) && tick.threatState != currentThreatState)
            {
                currentThreatState = tick.threatState;
                SetThreatTorusColor(currentThreatState);
            }

            // Blit sensor textures to RenderTextures
            BlitSensorToRenderTextures();
        }

        private void HandleScenarioEnd(Scenarios.ScenarioEndPayload end)
        {
            isScenarioRunning = false;
            Debug.Log(string.Format("[VRHudController] SCENARIO_END: {0}", end.reason));
        }
#endif

        // ══════════════════════════════════════════════════════════
        // RenderTexture Blitting
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Reads the current Texture2D frame from each sensor renderer and blits it
        /// into the corresponding RenderTexture for the world-space panel display.
        /// </summary>
        private void BlitSensorToRenderTextures()
        {
            // RF Waterfall: blit from renderer's Texture2D
            if (rfRenderer != null && rfRenderer.IsInitialized && rfRenderer.Texture != null && rtRfWaterfall != null)
            {
                Graphics.Blit(rfRenderer.Texture, rtRfWaterfall);
            }

            // Doppler Scope: blit from renderer's Texture2D
            if (dopplerRenderer != null && dopplerRenderer.IsInitialized && dopplerRenderer.Texture != null && rtDopplerScope != null)
            {
                Graphics.Blit(dopplerRenderer.Texture, rtDopplerScope);
            }

            // Acoustic FFT: This renderer uses UITK VisualElements (bars), not a Texture2D.
            // For VR panels, we generate a simplified texture representation.
            // The acoustic panel will show via a dedicated camera or procedural texture.
            // For now, leave as-is; the panel shows whatever was last blitted or cleared.

            // EW/SMASH Panel: similar to Acoustic — UITK-based controls.
            // These panels show status via dedicated rendering in a future pass.
        }

        /// <summary>
        /// Clears all RenderTextures to transparent black.
        /// </summary>
        private void ClearAllRenderTextures()
        {
            ClearRenderTexture(rtRfWaterfall);
            ClearRenderTexture(rtDopplerScope);
            ClearRenderTexture(rtAcousticFft);
            ClearRenderTexture(rtEwSmashPanel);
        }

        private void ClearRenderTexture(RenderTexture rt)
        {
            if (rt == null) return;

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prev;
        }

        // ══════════════════════════════════════════════════════════
        // Threat Torus Color Management
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Updates the threat indicator torus emissive color.
        /// MIL-STD-2525: GREEN=#00B300, AMBER=#FFB300, RED=#CC0000.
        /// </summary>
        public void SetThreatTorusColor(string threatState)
        {
            currentThreatState = threatState;

            if (threatTorusRenderer == null) return;

            Color color;
            switch (threatState)
            {
                case "GREEN": color = ThreatGreen; break;
                case "AMBER": color = ThreatAmber; break;
                case "RED":   color = ThreatRed;   break;
                default:
                    Debug.LogWarning(string.Format("[VRHudController] Unknown threat state: {0}", threatState));
                    color = ThreatGreen;
                    break;
            }

            threatTorusRenderer.GetPropertyBlock(threatMpb);
            threatMpb.SetColor("_Color", color);
            threatMpb.SetColor("_EmissionColor", color * 2.5f); // HDR emissive glow
            threatTorusRenderer.SetPropertyBlock(threatMpb);
        }

        /// <summary>
        /// Gets the expected MIL-STD-2525 color for a given threat state string.
        /// Used by self-tests.
        /// </summary>
        public static Color GetThreatColor(string state)
        {
            switch (state)
            {
                case "GREEN": return ThreatGreen;
                case "AMBER": return ThreatAmber;
                case "RED":   return ThreatRed;
                default:      return ThreatGreen;
            }
        }

        // ══════════════════════════════════════════════════════════
        // Manual Update Path (fallback for testing without ScenarioManager)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Manually trigger a blit cycle. Used by self-tests and editor scripts.
        /// </summary>
        public void ManualBlitUpdate()
        {
            if (!isInitialized) return;
            BlitSensorToRenderTextures();
        }

        /// <summary>
        /// Manually set threat state. Used by self-tests and editor scripts.
        /// </summary>
        public void ManualSetThreat(string state)
        {
            SetThreatTorusColor(state);
        }

        // ══════════════════════════════════════════════════════════
        // Runtime Assignment (for programmatic setup)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Assigns all references programmatically. Called by VRHudSetup editor script.
        /// </summary>
        public void AssignReferences(
            RenderTexture rf, RenderTexture doppler, RenderTexture acoustic, RenderTexture ewSmash,
            Renderer rfPanel, Renderer dopplerPanel, Renderer acousticPanel, Renderer ewSmashPanel,
            Renderer torus)
        {
            rtRfWaterfall = rf;
            rtDopplerScope = doppler;
            rtAcousticFft = acoustic;
            rtEwSmashPanel = ewSmash;

            panelRfRenderer = rfPanel;
            panelDopplerRenderer = dopplerPanel;
            panelAcousticRenderer = acousticPanel;
            panelEwSmashRenderer = ewSmashPanel;

            threatTorusRenderer = torus;

            ValidateAndInitialize();
        }
    }
}
