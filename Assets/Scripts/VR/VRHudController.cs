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
// When renderers are absent (standalone VR scene without UITK
// Cockpit), procedural fallback synthesis fills every panel.
//
// Threat indicator torus color follows MIL-STD-2525:
//   GREEN=#00B300, AMBER=#FFB300, RED=#CC0000
//
// URP + Legacy shader compatibility: all MaterialPropertyBlock
// calls set BOTH _BaseMap/_BaseColor (URP) and _MainTex/_Color
// (legacy) properties.
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

        // ── Sensor Renderers (found at runtime, may be null) ─────
        private UI.RfWaterfallRenderer rfRenderer;
        private UI.DopplerScopeRenderer dopplerRenderer;
        private UI.AcousticFftRenderer acousticRenderer;

        // ── MIL-STD-2525 Threat Colors ──────────────────────────
        public static readonly Color ThreatGreen = new Color(0f, 0.702f, 0f, 1f);      // #00B300
        public static readonly Color ThreatAmber = new Color(1f, 0.702f, 0f, 1f);       // #FFB300
        public static readonly Color ThreatRed   = new Color(0.8f, 0f, 0f, 1f);         // #CC0000

        // ── Procedural Acoustic FFT Texture ─────────────────────
        public const int AcousticTexWidth  = 256;
        public const int AcousticTexHeight = 128;
        public const int AcousticBandCount = 8;
        public const int BladeTipBandIndex = 2;  // ~220 Hz blade-tip harmonic
        public const float AcousticAmberThreshold = 40f;
        public const float AcousticRedThreshold   = 55f;
        public const float AcousticMaxDb          = 85f;

        private Texture2D acousticTex;
        private Color[] acousticPixels;

        // ── Procedural EW / SMASH Texture ───────────────────────
        public const int EwSmashTexWidth  = 256;
        public const int EwSmashTexHeight = 128;

        private Texture2D ewSmashTex;
        private Color[] ewSmashPixels;

        // ── Standalone RF Fallback Texture ───────────────────────
        private Texture2D rfFallbackTex;
        private Color[] rfFallbackRow;

        // ── Standalone Doppler Fallback Texture ─────────────────
        private Texture2D dopplerFallbackTex;
        private Color[] dopplerBgPixels;
        private Color[] dopplerRenderBuf;

        // ── State ────────────────────────────────────────────────
        private string currentThreatState = "GREEN";
        private bool isScenarioRunning = false;
        private bool isInitialized = false;
        private MaterialPropertyBlock threatMpb;
        private float lastAcousticDb = 30f;
        private bool lastBlackoutRisk = false;
        private bool lastEwEngaged = false;
        private float lastEwBandGHz = 5.825f;
        private float lastAzimuthDeg = 0f;
        private float lastElevationDeg = 0f;
        private float lastLeadAngleDeg = 0f;
        private bool lastKineticHit = false;

        // ── Public Accessors (for self-test) ─────────────────────
        public RenderTexture RtRfWaterfall      { get { return rtRfWaterfall; } }
        public RenderTexture RtDopplerScope     { get { return rtDopplerScope; } }
        public RenderTexture RtAcousticFft      { get { return rtAcousticFft; } }
        public RenderTexture RtEwSmashPanel     { get { return rtEwSmashPanel; } }
        public Renderer PanelRfRenderer         { get { return panelRfRenderer; } }
        public Renderer PanelDopplerRenderer    { get { return panelDopplerRenderer; } }
        public Renderer PanelAcousticRenderer   { get { return panelAcousticRenderer; } }
        public Renderer PanelEwSmashRenderer    { get { return panelEwSmashRenderer; } }
        public Renderer ThreatTorusRenderer     { get { return threatTorusRenderer; } }
        public string CurrentThreatState        { get { return currentThreatState; } }
        public bool IsScenarioRunning           { get { return isScenarioRunning; } }
        public bool IsInitialized               { get { return isInitialized; } }
        public Texture2D AcousticTex            { get { return acousticTex; } }
        public Texture2D EwSmashTex             { get { return ewSmashTex; } }

        // ══════════════════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            threatMpb = new MaterialPropertyBlock();
        }

        private void Start()
        {
            FindSensorRenderers();
            SubscribeToScenarioEvents();
            AllocateProceduralTextures();
            ValidateAndInitialize();
            Debug.Log("[VRHudController] Initialized. Panels ready for scenario data.");
        }

        private void OnDestroy()
        {
            UnsubscribeFromScenarioEvents();
            FreeProceduralTextures();
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
                Debug.Log("[VRHudController] RfWaterfallRenderer not found — using procedural RF fallback.");
            if (dopplerRenderer == null)
                Debug.Log("[VRHudController] DopplerScopeRenderer not found — using procedural Doppler fallback.");
        }

        private void AllocateProceduralTextures()
        {
            // Acoustic FFT — always procedural (UITK bars have no Texture2D)
            acousticTex = new Texture2D(AcousticTexWidth, AcousticTexHeight, TextureFormat.RGB24, false);
            acousticPixels = new Color[AcousticTexWidth * AcousticTexHeight];

            // EW/SMASH — always procedural (UITK controls have no Texture2D)
            ewSmashTex = new Texture2D(EwSmashTexWidth, EwSmashTexHeight, TextureFormat.RGB24, false);
            ewSmashPixels = new Color[EwSmashTexWidth * EwSmashTexHeight];

            // RF fallback — only used when rfRenderer is null
            rfFallbackTex = new Texture2D(256, 128, TextureFormat.RGB24, false);
            rfFallbackRow = new Color[256];

            // Doppler fallback — only used when dopplerRenderer is null
            dopplerFallbackTex = new Texture2D(200, 200, TextureFormat.RGB24, false);
            dopplerBgPixels = new Color[200 * 200];
            dopplerRenderBuf = new Color[200 * 200];
            GenerateDopplerBackground();
        }

        private void FreeProceduralTextures()
        {
            if (acousticTex != null) Destroy(acousticTex);
            if (ewSmashTex != null) Destroy(ewSmashTex);
            if (rfFallbackTex != null) Destroy(rfFallbackTex);
            if (dopplerFallbackTex != null) Destroy(dopplerFallbackTex);
        }

        private void ValidateAndInitialize()
        {
            bool valid = true;

            if (rtRfWaterfall == null)  { Debug.LogWarning("[VRHudController] RT_RfWaterfall not assigned."); valid = false; }
            if (rtDopplerScope == null) { Debug.LogWarning("[VRHudController] RT_DopplerScope not assigned."); valid = false; }
            if (rtAcousticFft == null)  { Debug.LogWarning("[VRHudController] RT_AcousticFft not assigned."); valid = false; }
            if (rtEwSmashPanel == null) { Debug.LogWarning("[VRHudController] RT_EwSmashPanel not assigned."); valid = false; }

            if (panelRfRenderer == null)      { Debug.LogWarning("[VRHudController] Panel_RF renderer not assigned."); valid = false; }
            if (panelDopplerRenderer == null)  { Debug.LogWarning("[VRHudController] Panel_Doppler renderer not assigned."); valid = false; }
            if (panelAcousticRenderer == null) { Debug.LogWarning("[VRHudController] Panel_Acoustic renderer not assigned."); valid = false; }
            if (panelEwSmashRenderer == null)  { Debug.LogWarning("[VRHudController] Panel_EwSmash renderer not assigned."); valid = false; }

            isInitialized = valid;

            if (isInitialized)
            {
                AssignRtToPanel(panelRfRenderer, rtRfWaterfall);
                AssignRtToPanel(panelDopplerRenderer, rtDopplerScope);
                AssignRtToPanel(panelAcousticRenderer, rtAcousticFft);
                AssignRtToPanel(panelEwSmashRenderer, rtEwSmashPanel);
                SetThreatTorusColor("GREEN");
            }
        }

        /// <summary>
        /// Sets BOTH URP (_BaseMap) and legacy (_MainTex) texture properties.
        /// </summary>
        private static void AssignRtToPanel(Renderer panelRenderer, RenderTexture rt)
        {
            if (panelRenderer == null || rt == null) return;

            var mpb = new MaterialPropertyBlock();
            panelRenderer.GetPropertyBlock(mpb);
            mpb.SetTexture("_BaseMap", rt);
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

            // Cache telemetry for procedural panels
            if (tick.sensorReadings != null)
                lastAcousticDb = tick.sensorReadings.acousticSplDb;
            if (tick.ewJammerState != null)
            {
                lastBlackoutRisk = tick.ewJammerState.friendlyBlackoutRisk;
                lastEwEngaged = tick.ewJammerState.engaged;
                lastEwBandGHz = tick.ewJammerState.bandGHz;
                lastAzimuthDeg = tick.ewJammerState.bearingDeg;
                lastElevationDeg = tick.ewJammerState.elevationDeg;
            }
            if (tick.traineeAction != null && tick.traineeAction.kinetic != null)
            {
                lastLeadAngleDeg = tick.traineeAction.kinetic.leadAngleDeg;
                lastKineticHit = tick.traineeAction.kinetic.hit;
            }

            // Blit all 4 panels
            BlitSensorToRenderTextures(tick);
        }

        private void HandleScenarioEnd(Scenarios.ScenarioEndPayload end)
        {
            isScenarioRunning = false;
            Debug.Log(string.Format("[VRHudController] SCENARIO_END: {0}", end.reason));
        }
#endif

        // ══════════════════════════════════════════════════════════
        // RenderTexture Blitting — ALL 4 PANELS
        // ══════════════════════════════════════════════════════════

#if UNITY_5_3_OR_NEWER
        private void BlitSensorToRenderTextures(Scenarios.ScenarioTickPayload tick)
        {
            // ── RF Waterfall ─────────────────────────────────────
            if (rfRenderer != null && rfRenderer.IsInitialized && rfRenderer.Texture != null && rtRfWaterfall != null)
            {
                Graphics.Blit(rfRenderer.Texture, rtRfWaterfall);
            }
            else if (rtRfWaterfall != null && tick.sensorReadings != null)
            {
                RenderRfFallback(tick.sensorReadings.rfWaterfallPeakGHz, tick.sensorReadings.rfWaterfallPeakDbm);
                Graphics.Blit(rfFallbackTex, rtRfWaterfall);
            }

            // ── Doppler Scope ────────────────────────────────────
            if (dopplerRenderer != null && dopplerRenderer.IsInitialized && dopplerRenderer.Texture != null && rtDopplerScope != null)
            {
                Graphics.Blit(dopplerRenderer.Texture, rtDopplerScope);
            }
            else if (rtDopplerScope != null && tick.swarmSnapshot != null)
            {
                RenderDopplerFallback(tick.swarmSnapshot, tick.sensorReadings);
                Graphics.Blit(dopplerFallbackTex, rtDopplerScope);
            }

            // ── Acoustic FFT (always procedural) ─────────────────
            if (rtAcousticFft != null)
            {
                RenderAcousticFft(lastAcousticDb);
                Graphics.Blit(acousticTex, rtAcousticFft);
            }

            // ── EW / SMASH 2000 (always procedural) ──────────────
            if (rtEwSmashPanel != null)
            {
                RenderEwSmashPanel();
                Graphics.Blit(ewSmashTex, rtEwSmashPanel);
            }
        }
#endif

        /// <summary>
        /// Overload for manual/test blit without tick payload.
        /// </summary>
        private void BlitSensorToRenderTextures()
        {
            if (rtAcousticFft != null)
            {
                RenderAcousticFft(lastAcousticDb);
                Graphics.Blit(acousticTex, rtAcousticFft);
            }
            if (rtEwSmashPanel != null)
            {
                RenderEwSmashPanel();
                Graphics.Blit(ewSmashTex, rtEwSmashPanel);
            }
        }

        // ══════════════════════════════════════════════════════════
        // Procedural Acoustic FFT Panel
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Renders an 8-band FFT bar chart into acousticTex matching
        /// AcousticFftRenderer doctrine: Band 2 = blade-tip harmonic,
        /// green < 40 dB, amber 40-55 dB, red > 55 dB.
        /// </summary>
        public void RenderAcousticFft(float acousticSplDb)
        {
            if (acousticTex == null || acousticPixels == null) return;

            lastAcousticDb = acousticSplDb;
            Color bgColor = new Color(0.04f, 0.05f, 0.04f, 1f);

            // Clear to background
            for (int i = 0; i < acousticPixels.Length; i++)
                acousticPixels[i] = bgColor;

            int bandWidth = AcousticTexWidth / AcousticBandCount;
            int gap = 4;

            for (int band = 0; band < AcousticBandCount; band++)
            {
                // Compute per-band SPL: blade-tip band gets full signal, others roll off
                float bandSpl;
                if (band == BladeTipBandIndex)
                {
                    bandSpl = acousticSplDb;
                }
                else
                {
                    float distToDominant = Mathf.Abs(band - BladeTipBandIndex);
                    float rollOff = 12f * distToDominant;
                    bandSpl = Mathf.Max(30f, acousticSplDb - rollOff);
                }

                // Compute bar height and color
                float heightNorm;
                Color barColor;

                if (bandSpl < AcousticAmberThreshold)
                {
                    heightNorm = Mathf.Clamp(bandSpl / AcousticAmberThreshold * 0.15f, 0.04f, 0.15f);
                    barColor = new Color(0f, 1f, 0.53f, 1f); // #00FF88 soft green
                }
                else if (bandSpl <= AcousticRedThreshold)
                {
                    float t = (bandSpl - AcousticAmberThreshold) / (AcousticRedThreshold - AcousticAmberThreshold);
                    heightNorm = Mathf.Lerp(0.25f, 0.65f, t);
                    barColor = new Color(1f, 0.702f, 0f, 1f); // #FFB300 amber
                }
                else
                {
                    float t = Mathf.Clamp01((bandSpl - AcousticRedThreshold) / (AcousticMaxDb - AcousticRedThreshold));
                    heightNorm = Mathf.Lerp(0.70f, 0.98f, t);
                    barColor = new Color(0.8f, 0f, 0f, 1f); // #CC0000 red
                }

                int barHeight = Mathf.RoundToInt(heightNorm * AcousticTexHeight);
                int x0 = band * bandWidth + gap / 2;
                int x1 = (band + 1) * bandWidth - gap / 2;

                for (int y = 0; y < barHeight; y++)
                {
                    for (int x = x0; x < x1 && x < AcousticTexWidth; x++)
                    {
                        acousticPixels[y * AcousticTexWidth + x] = barColor;
                    }
                }
            }

            acousticTex.SetPixels(acousticPixels);
            acousticTex.Apply();
        }

        // ══════════════════════════════════════════════════════════
        // Procedural EW / SMASH 2000 Panel
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Renders EW jammer status + SMASH 2000 reticle crosshair into ewSmashTex.
        /// Shows: band, azimuth, elevation, engaged state, ROE blackout warning,
        /// and a crosshair with lead-angle offset.
        /// </summary>
        public void RenderEwSmashPanel()
        {
            if (ewSmashTex == null || ewSmashPixels == null) return;

            Color bgColor = new Color(0.03f, 0.04f, 0.03f, 1f);
            Color textGreen = new Color(0f, 0.85f, 0.15f, 1f);
            Color roeRed = new Color(0.9f, 0.1f, 0.1f, 1f);
            Color engagedAmber = new Color(1f, 0.702f, 0f, 1f);
            Color crosshairColor = new Color(0f, 1f, 0f, 1f);
            Color hitGreen = new Color(0f, 1f, 0.4f, 1f);

            // Clear background
            for (int i = 0; i < ewSmashPixels.Length; i++)
                ewSmashPixels[i] = bgColor;

            int W = EwSmashTexWidth;
            int H = EwSmashTexHeight;

            // ── Left half: EW Jammer status ──────────────────────
            // Engaged indicator bar (top-left)
            Color engageColor = lastEwEngaged ? engagedAmber : textGreen;
            DrawFilledRect(ewSmashPixels, W, 4, H - 16, 60, 10, engageColor);

            // Band indicator — colored bar at different X positions per band
            int bandBarX;
            if (Mathf.Abs(lastEwBandGHz - 0.915f) < 0.05f)
                bandBarX = 4;
            else if (Mathf.Abs(lastEwBandGHz - 2.4f) < 0.05f)
                bandBarX = 28;
            else
                bandBarX = 52;
            DrawFilledRect(ewSmashPixels, W, bandBarX, H - 32, 20, 8, textGreen);

            // Azimuth indicator — horizontal line at height proportional to 0-359
            int azLine = Mathf.Clamp(Mathf.RoundToInt(lastAzimuthDeg / 359f * (H / 2 - 10)), 0, H / 2 - 10);
            DrawHLine(ewSmashPixels, W, 4, azLine + 4, 60, textGreen);

            // Elevation indicator — small bar
            int elLine = Mathf.Clamp(Mathf.RoundToInt((lastElevationDeg + 90f) / 180f * (H / 2 - 10)), 0, H / 2 - 10);
            DrawHLine(ewSmashPixels, W, 70, elLine + 4, 40, textGreen);

            // ROE Blackout Warning — prominent red block
            if (lastBlackoutRisk)
            {
                DrawFilledRect(ewSmashPixels, W, 4, H / 2 + 4, W / 2 - 8, 20, roeRed);
                // Flash pattern: diagonal stripes
                for (int y = H / 2 + 4; y < H / 2 + 24 && y < H; y++)
                {
                    for (int x = 4; x < W / 2 - 4; x++)
                    {
                        if ((x + y) % 8 < 4)
                            ewSmashPixels[y * W + x] = bgColor;
                    }
                }
            }

            // ── Right half: SMASH 2000 crosshair ────────────────
            int cx = W * 3 / 4;
            int cy = H / 2;
            int crossLen = 20;

            // Crosshair lines
            DrawVLine(ewSmashPixels, W, cx, cy - crossLen, crossLen * 2, crosshairColor);
            DrawHLine(ewSmashPixels, W, cx - crossLen, cy, crossLen * 2, crosshairColor);

            // Lead angle offset indicator — small dot offset from center
            float leadRad = lastLeadAngleDeg * Mathf.Deg2Rad;
            int leadX = cx + Mathf.RoundToInt(Mathf.Sin(leadRad) * 12f);
            int leadY = cy + Mathf.RoundToInt(Mathf.Cos(leadRad) * 12f);
            Color dotColor = lastKineticHit ? hitGreen : roeRed;
            DrawFilledRect(ewSmashPixels, W, leadX - 2, leadY - 2, 5, 5, dotColor);

            // Circle approximation around crosshair (octagon)
            int r = 16;
            DrawCircleApprox(ewSmashPixels, W, H, cx, cy, r, crosshairColor);

            ewSmashTex.SetPixels(ewSmashPixels);
            ewSmashTex.Apply();
        }

        // ══════════════════════════════════════════════════════════
        // Standalone RF Waterfall Fallback
        // ══════════════════════════════════════════════════════════

        private void RenderRfFallback(float peakGHz, float peakDbm)
        {
            if (rfFallbackTex == null) return;
            int W = 256;
            int H = 128;

            // Shift rows up
            Color[] oldRows = rfFallbackTex.GetPixels(0, 0, W, H - 1);
            rfFallbackTex.SetPixels(0, 1, W, H - 1, oldRows);

            // Generate bottom row
            for (int x = 0; x < W; x++)
            {
                float jitter = -90f + (float)(Math.Sin(x * 0.15) * 1.5);
                rfFallbackRow[x] = UI.RfWaterfallRenderer.MapDbmToColor(jitter);
            }

            if (peakGHz > 0.01f && peakDbm > -90f)
            {
                float normX = peakGHz / 6f;
                int center = Mathf.Clamp(Mathf.RoundToInt(normX * (W - 1)), 0, W - 1);
                for (int off = -4; off <= 4; off++)
                {
                    int x = center + off;
                    if (x >= 0 && x < W)
                    {
                        float att = Mathf.Exp(-0.5f * off * off / 2f);
                        float pwr = Mathf.Lerp(-90f, peakDbm, att);
                        rfFallbackRow[x] = UI.RfWaterfallRenderer.MapDbmToColor(pwr);
                    }
                }
            }

            rfFallbackTex.SetPixels(0, 0, W, 1, rfFallbackRow);
            rfFallbackTex.Apply();
        }

        // ══════════════════════════════════════════════════════════
        // Standalone Doppler Scope Fallback
        // ══════════════════════════════════════════════════════════

        private void GenerateDopplerBackground()
        {
            int S = 200;
            float center = S / 2f;
            float maxR = center - 2f;
            Color bg = new Color(0.04f, 0.05f, 0.04f, 1f);
            Color ring = new Color(0f, 0.35f, 0f, 1f);
            Color axis = new Color(0f, 0.22f, 0f, 1f);

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > maxR) { dopplerBgPixels[y * S + x] = Color.black; continue; }
                    float norm = dist / maxR;
                    bool isRing = Mathf.Abs(norm - 0.25f) < 0.015f || Mathf.Abs(norm - 0.5f) < 0.015f || Mathf.Abs(norm - 0.75f) < 0.015f;
                    bool isAxis = Mathf.Abs(dx) < 0.8f || Mathf.Abs(dy) < 0.8f;
                    dopplerBgPixels[y * S + x] = isRing ? ring : (isAxis ? axis : bg);
                }
            }
        }

#if UNITY_5_3_OR_NEWER
        private void RenderDopplerFallback(Scenarios.SwarmAgentState[] swarm, Scenarios.SensorReadings sr)
        {
            if (dopplerFallbackTex == null) return;
            int S = 200;
            float center = S / 2f;
            float maxR = center - 4f;

            Array.Copy(dopplerBgPixels, dopplerRenderBuf, dopplerBgPixels.Length);

            if (swarm != null)
            {
                for (int i = 0; i < swarm.Length; i++)
                {
                    var agent = swarm[i];
                    if (agent == null || agent.isNeutralised) continue;
                    float range = Mathf.Sqrt(agent.positionWorld.x * agent.positionWorld.x + agent.positionWorld.z * agent.positionWorld.z);
                    if (range > 500f) continue;
                    float normRange = Mathf.Clamp01(range / 500f);
                    float pixDist = normRange * maxR;
                    float bearRad = Mathf.Atan2(agent.positionWorld.x, agent.positionWorld.z);
                    int px = Mathf.RoundToInt(center + pixDist * Mathf.Sin(bearRad));
                    int py = Mathf.RoundToInt(center + pixDist * Mathf.Cos(bearRad));
                    int blipSz = agent.isDecoy ? 5 : 3;
                    Color c = agent.isDecoy ? new Color(0.2f, 1f, 0.4f, 1f) : new Color(0f, 0.85f, 0.15f, 1f);
                    int half = blipSz / 2;
                    for (int dy = -half; dy <= half; dy++)
                        for (int dx = -half; dx <= half; dx++)
                        {
                            int bx = px + dx, by = py + dy;
                            if (bx >= 0 && bx < S && by >= 0 && by < S)
                                dopplerRenderBuf[by * S + bx] = c;
                        }
                }
            }

            dopplerFallbackTex.SetPixels(dopplerRenderBuf);
            dopplerFallbackTex.Apply();
        }
#endif

        // ══════════════════════════════════════════════════════════
        // Pixel Drawing Helpers
        // ══════════════════════════════════════════════════════════

        private static void DrawFilledRect(Color[] buf, int bufW, int x0, int y0, int w, int h, Color c)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (x >= 0 && x < bufW && y >= 0 && y < buf.Length / bufW)
                        buf[y * bufW + x] = c;
        }

        private static void DrawHLine(Color[] buf, int bufW, int x0, int y, int len, Color c)
        {
            int H = buf.Length / bufW;
            if (y < 0 || y >= H) return;
            for (int x = x0; x < x0 + len && x < bufW; x++)
                if (x >= 0)
                    buf[y * bufW + x] = c;
        }

        private static void DrawVLine(Color[] buf, int bufW, int x, int y0, int len, Color c)
        {
            int H = buf.Length / bufW;
            if (x < 0 || x >= bufW) return;
            for (int y = y0; y < y0 + len && y < H; y++)
                if (y >= 0)
                    buf[y * bufW + x] = c;
        }

        private static void DrawCircleApprox(Color[] buf, int bufW, int bufH, int cx, int cy, int r, Color c)
        {
            for (int angle = 0; angle < 360; angle += 3)
            {
                float rad = angle * Mathf.Deg2Rad;
                int px = cx + Mathf.RoundToInt(Mathf.Cos(rad) * r);
                int py = cy + Mathf.RoundToInt(Mathf.Sin(rad) * r);
                if (px >= 0 && px < bufW && py >= 0 && py < bufH)
                    buf[py * bufW + px] = c;
            }
        }

        // ══════════════════════════════════════════════════════════
        // RT Clear
        // ══════════════════════════════════════════════════════════

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
        /// Sets BOTH URP (_BaseColor) and legacy (_Color) + _EmissionColor.
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
            threatMpb.SetColor("_BaseColor", color);
            threatMpb.SetColor("_Color", color);
            threatMpb.SetColor("_EmissionColor", color * 2.5f);
            threatTorusRenderer.SetPropertyBlock(threatMpb);
        }

        /// <summary>
        /// Gets the expected MIL-STD-2525 color for a given threat state string.
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
        // Manual Update Path (self-tests + editor)
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
        /// Manually render acoustic panel at a specific dB level.
        /// Used by self-tests.
        /// </summary>
        public void ManualRenderAcoustic(float db)
        {
            RenderAcousticFft(db);
            if (rtAcousticFft != null)
                Graphics.Blit(acousticTex, rtAcousticFft);
        }

        /// <summary>
        /// Manually render EW/SMASH panel with specific state.
        /// Used by self-tests.
        /// </summary>
        public void ManualRenderEwSmash(bool engaged, float bandGHz, float azimuth, float elevation, bool blackoutRisk, float leadAngle, bool hit)
        {
            lastEwEngaged = engaged;
            lastEwBandGHz = bandGHz;
            lastAzimuthDeg = azimuth;
            lastElevationDeg = elevation;
            lastBlackoutRisk = blackoutRisk;
            lastLeadAngleDeg = leadAngle;
            lastKineticHit = hit;
            RenderEwSmashPanel();
            if (rtEwSmashPanel != null)
                Graphics.Blit(ewSmashTex, rtEwSmashPanel);
        }

        /// <summary>
        /// Manually set threat state.
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

            if (acousticTex == null)
                AllocateProceduralTextures();

            ValidateAndInitialize();
        }

        /// <summary>
        /// Returns true if the MaterialPropertyBlock on the given renderer has
        /// both URP and legacy texture properties set. Used by self-tests.
        /// </summary>
        public static bool VerifyDualTextureProperties(Renderer r, string urpProp, string legacyProp)
        {
            if (r == null) return false;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            Texture baseMap = mpb.GetTexture(urpProp);
            Texture mainTex = mpb.GetTexture(legacyProp);
            return baseMap != null && mainTex != null;
        }

        /// <summary>
        /// Returns true if the MaterialPropertyBlock on the given renderer has
        /// both URP and legacy color properties set. Used by self-tests.
        /// </summary>
        public static bool VerifyDualColorProperties(Renderer r)
        {
            if (r == null) return false;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            Color baseColor = mpb.GetColor("_BaseColor");
            Color color = mpb.GetColor("_Color");
            return baseColor.a > 0f && color.a > 0f && baseColor == color;
        }
    }
}
