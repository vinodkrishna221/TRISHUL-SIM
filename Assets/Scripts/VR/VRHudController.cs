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
        public bool LastBlackoutRisk            { get { return lastBlackoutRisk; } }
        public bool LastEwEngaged               { get { return lastEwEngaged; } }
        public bool LastKineticHit              { get { return lastKineticHit; } }
        public Texture2D AcousticTex            { get { return acousticTex; } }
        public Texture2D EwSmashTex             { get { return ewSmashTex; } }

        // ══════════════════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            threatMpb = new MaterialPropertyBlock();
            AdjustPanelLayout();
            AdjustThreatTorusPosition();
        }

        private void AdjustPanelLayout()
        {
            Vector3 eyePos = new Vector3(0f, 1.65f, 0f);

            // Center: Micro-Doppler Radar
            if (panelDopplerRenderer != null)
            {
                panelDopplerRenderer.transform.localPosition = new Vector3(0f, 1.75f, 2.4f);
                panelDopplerRenderer.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
                panelDopplerRenderer.transform.localEulerAngles = Vector3.zero;

                Transform lbl = panelDopplerRenderer.transform.Find("Panel_Doppler_Label");
                if (lbl != null)
                {
                    lbl.localPosition = new Vector3(0f, 0.82f, 0f);
                    lbl.localScale = new Vector3(0.33f, 0.33f, 1f);
                }
            }

            // Left: RF Waterfall (angled inward ~33 deg directly facing operator eye level, 2:1 aspect ratio)
            if (panelRfRenderer != null)
            {
                panelRfRenderer.transform.localPosition = new Vector3(-1.42f, 1.68f, 2.20f);
                panelRfRenderer.transform.localScale = new Vector3(1.4f, 0.70f, 1f);
                OrientPanelToOperator(panelRfRenderer.transform, eyePos);

                Transform lbl = panelRfRenderer.transform.Find("Panel_RF_Label");
                if (lbl != null)
                {
                    lbl.localPosition = new Vector3(0f, 0.55f, 0f);
                    lbl.localScale = new Vector3(0.5f / 1.4f, 0.5f / 0.70f, 1f);
                }
            }

            // Right: Acoustic Sonar (angled inward ~33 deg directly facing operator eye level, 2:1 aspect ratio)
            if (panelAcousticRenderer != null)
            {
                panelAcousticRenderer.transform.localPosition = new Vector3(1.42f, 1.68f, 2.20f);
                panelAcousticRenderer.transform.localScale = new Vector3(1.4f, 0.70f, 1f);
                OrientPanelToOperator(panelAcousticRenderer.transform, eyePos);

                Transform lbl = panelAcousticRenderer.transform.Find("Panel_Acoustic_Label");
                if (lbl != null)
                {
                    lbl.localPosition = new Vector3(0f, 0.55f, 0f);
                    lbl.localScale = new Vector3(0.5f / 1.4f, 0.5f / 0.70f, 1f);
                }
            }

            // Bottom: EW / SMASH 2000 Console (tilted up 22 deg)
            if (panelEwSmashRenderer != null)
            {
                panelEwSmashRenderer.transform.localPosition = new Vector3(0f, 0.80f, 2.05f);
                panelEwSmashRenderer.transform.localScale = new Vector3(1.7f, 0.75f, 1f);
                panelEwSmashRenderer.transform.localEulerAngles = new Vector3(22f, 0f, 0f);

                Transform lbl = panelEwSmashRenderer.transform.Find("Panel_EwSmash_Label");
                if (lbl != null)
                {
                    lbl.localPosition = new Vector3(0f, 0.44f, 0f);
                    lbl.localScale = new Vector3(0.29f, 0.66f, 1f);
                }
            }
        }

        private static void OrientPanelToOperator(Transform panelTransform, Vector3 operatorPos)
        {
            if (panelTransform == null) return;
            // In Unity Quads, the visible front face points along -Z (backwards).
            // Setting LookRotation to (panel - operator) aligns +Z away from operator,
            // making the quad's visible face (-Z) point directly at the operator.
            Vector3 lookAway = panelTransform.localPosition - operatorPos;
            if (lookAway.sqrMagnitude > 0.001f)
            {
                panelTransform.localRotation = Quaternion.LookRotation(lookAway, Vector3.up);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                AdjustPanelLayout();
            }
        }

        [ContextMenu("Apply Optimal Panel Layout")]
        public void ApplyOptimalPanelLayoutMenu()
        {
            AdjustPanelLayout();
            AdjustThreatTorusPosition();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[VRHudController] Optimal wraparound panel layout applied.");
        }
#endif

        private void AdjustThreatTorusPosition()
        {
            if (threatTorusRenderer != null)
            {
                var mf = threatTorusRenderer.GetComponent<MeshFilter>();
                if (mf != null && (mf.sharedMesh == null || mf.sharedMesh.name.Contains("Cylinder")))
                {
                    mf.sharedMesh = CreateRingMesh(0.485f, 0.525f, 64);
                }
                threatTorusRenderer.transform.localRotation = Quaternion.identity;
                threatTorusRenderer.transform.localScale = Vector3.one;
                threatTorusRenderer.transform.localPosition = new Vector3(0f, 0f, -0.005f);
            }
        }

        public static Mesh CreateRingMesh(float innerRadius, float outerRadius, int segments = 64)
        {
            Mesh mesh = new Mesh();
            mesh.name = "ThreatRingMesh";
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[i * 2] = new Vector3(innerRadius * cos, innerRadius * sin, 0f);
                vertices[i * 2 + 1] = new Vector3(outerRadius * cos, outerRadius * sin, 0f);

                uv[i * 2] = new Vector2((float)i / segments, 0f);
                uv[i * 2 + 1] = new Vector2((float)i / segments, 1f);

                if (i < segments)
                {
                    int baseIdx = i * 2;
                    int triIdx = i * 6;

                    triangles[triIdx + 0] = baseIdx;
                    triangles[triIdx + 1] = baseIdx + 1;
                    triangles[triIdx + 2] = baseIdx + 2;

                    triangles[triIdx + 3] = baseIdx + 2;
                    triangles[triIdx + 4] = baseIdx + 1;
                    triangles[triIdx + 5] = baseIdx + 3;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Start()
        {
            FindSensorRenderers();
            SubscribeToScenarioEvents();
            AllocateProceduralTextures();
            ValidateAndInitialize();
            EnsureScenarioSelectorAttached();
            Debug.Log("[VRHudController] Initialized. Panels ready for scenario data.");
        }

        public void EnsureScenarioSelectorAttached()
        {
#if UNITY_5_3_OR_NEWER
            if (GetComponent<VRScenarioSelector>() == null)
            {
                gameObject.AddComponent<VRScenarioSelector>();
            }
#endif
        }

        public void EnsureSubscribedToScenarioEvents()
        {
#if UNITY_5_3_OR_NEWER
            UnsubscribeFromScenarioEvents();
            SubscribeToScenarioEvents();
#endif
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
            dopplerFallbackTex = new Texture2D(256, 256, TextureFormat.RGB24, false);
            dopplerBgPixels = new Color[256 * 256];
            dopplerRenderBuf = new Color[256 * 256];
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

                // Render immediate baseline frames so panels are immediately active
                try
                {
                    if (acousticTex != null && rtAcousticFft != null)
                    {
                        RenderAcousticFft(30f);
                        Graphics.Blit(acousticTex, rtAcousticFft);
                    }

                    if (ewSmashTex != null && rtEwSmashPanel != null)
                    {
                        RenderEwSmashPanel();
                        Graphics.Blit(ewSmashTex, rtEwSmashPanel);
                    }

                    if (rfFallbackTex != null && rtRfWaterfall != null)
                    {
                        RenderRfFallback(0f, -90f);
                        Graphics.Blit(rfFallbackTex, rtRfWaterfall);
                    }

#if UNITY_5_3_OR_NEWER
                    if (dopplerFallbackTex != null && rtDopplerScope != null)
                    {
                        RenderDopplerFallback(null, null);
                        Graphics.Blit(dopplerFallbackTex, rtDopplerScope);
                    }
#endif
                }
                catch (System.Exception)
                {
                    // Safe edit-mode fallback when Graphics.Blit target is not yet GPU-bound
                }
                finally
                {
                    RenderTexture.active = null;
                }
                UpdatePanelTextures();
            }
        }

        private void Update()
        {
            if (!isInitialized) return;

            // When running standalone (no live scenario tick), render active radar grid,
            // scrolling RF waterfall, acoustic bars, and sync with gamepad controller
            if (!isScenarioRunning)
            {
#if UNITY_5_3_OR_NEWER
                var gamepad = GetComponent<Input.GamepadInputController>() ??
                              FindAnyObjectByType<Input.GamepadInputController>();
                if (gamepad != null)
                {
                    lastAzimuthDeg = gamepad.CurrentAzimuth;
                    lastElevationDeg = gamepad.CurrentElevation;
                    lastEwEngaged = gamepad.CurrentEngaged;
                    lastEwBandGHz = gamepad.CurrentBandGHz;
                }
#endif

                RenderRfFallback(0f, -90f);
                if (rtRfWaterfall != null) Graphics.Blit(rfFallbackTex, rtRfWaterfall);

#if UNITY_5_3_OR_NEWER
                RenderDopplerFallback(null, null);
                if (rtDopplerScope != null) Graphics.Blit(dopplerFallbackTex, rtDopplerScope);
#endif

                RenderAcousticFft(lastAcousticDb);
                if (rtAcousticFft != null) Graphics.Blit(acousticTex, rtAcousticFft);

                RenderEwSmashPanel();
                if (rtEwSmashPanel != null) Graphics.Blit(ewSmashTex, rtEwSmashPanel);

                RenderTexture.active = null;
                UpdatePanelTextures();
            }
        }

        private Transform cachedVrRig;

        private void LateUpdate()
        {
            // If VRHudPanels is placed at root level, align position and rotation to VRRig
            if (transform.parent == null)
            {
                if (cachedVrRig == null)
                {
#if UNITY_5_3_OR_NEWER
                    var rig = GameObject.Find("VRRig");
                    if (rig != null) cachedVrRig = rig.transform;
#endif
                }
                if (cachedVrRig != null)
                {
                    transform.position = cachedVrRig.position;
                    transform.rotation = cachedVrRig.rotation;
                }
            }
        }

        /// <summary>
        /// Binds live Texture2Ds directly to panel renderers to ensure 100% reliable rendering
        /// across all render pipelines (URP, DX12, standalone, mobile) without depending on Graphics.Blit.
        /// </summary>
        public void UpdatePanelTextures()
        {
            Texture activeRf = (rfRenderer != null && rfRenderer.IsInitialized && rfRenderer.Texture != null)
                ? (Texture)rfRenderer.Texture
                : rfFallbackTex;
            AssignTextureToRenderer(panelRfRenderer, activeRf, rtRfWaterfall);

            Texture activeDoppler = (dopplerRenderer != null && dopplerRenderer.IsInitialized && dopplerRenderer.Texture != null)
                ? (Texture)dopplerRenderer.Texture
                : dopplerFallbackTex;
            AssignTextureToRenderer(panelDopplerRenderer, activeDoppler, rtDopplerScope);

            AssignTextureToRenderer(panelAcousticRenderer, acousticTex, rtAcousticFft);
            AssignTextureToRenderer(panelEwSmashRenderer, ewSmashTex, rtEwSmashPanel);
        }

        private static void AssignTextureToRenderer(Renderer panelRenderer, Texture activeTex, RenderTexture rtFallback)
        {
            if (panelRenderer == null) return;
            Texture target = activeTex != null ? activeTex : (Texture)rtFallback;
            if (target == null) return;

            if (panelRenderer.sharedMaterial != null)
            {
                panelRenderer.sharedMaterial.mainTexture = target;
                if (panelRenderer.sharedMaterial.HasProperty("_BaseMap"))
                    panelRenderer.sharedMaterial.SetTexture("_BaseMap", target);
            }

            var mpb = new MaterialPropertyBlock();
            panelRenderer.GetPropertyBlock(mpb);
            mpb.SetTexture("_BaseMap", target);
            mpb.SetTexture("_MainTex", target);
            panelRenderer.SetPropertyBlock(mpb);
        }

        /// <summary>
        /// Sets BOTH URP (_BaseMap) and legacy (_MainTex) texture properties on both material and MPB.
        /// </summary>
        private static void AssignRtToPanel(Renderer panelRenderer, RenderTexture rt)
        {
            AssignTextureToRenderer(panelRenderer, null, rt);
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
            RenderTexture.active = null;
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
            RenderTexture.active = null;
        }

        // ══════════════════════════════════════════════════════════
        // Procedural Acoustic FFT Panel
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Renders an 8-band FFT bar chart into acousticTex matching
        /// AcousticFftRenderer doctrine: Band 2 = blade-tip harmonic,
        /// green < 40 dB, amber 40-55 dB, red > 55 dB.
        /// In self-test mode, text uses electric blue to keep bar-height measurement unaffected.
        /// </summary>
        public void RenderAcousticFft(float acousticSplDb, bool inSelfTest = false)
        {
            if (acousticTex == null || acousticPixels == null) return;

            lastAcousticDb = acousticSplDb;
            Color bgColor = new Color(0.02f, 0.035f, 0.02f, 1f);
            Color brightGreen = new Color(0f, 0.95f, 0.35f, 1f);
            Color dimGreen = new Color(0f, 0.50f, 0.18f, 1f);
            Color testBlue = new Color(0.08f, 0.08f, 0.95f, 1f);

            Color headerColor = inSelfTest ? testBlue : brightGreen;
            Color scaleColor = inSelfTest ? testBlue : dimGreen;

            // Clear to background
            for (int i = 0; i < acousticPixels.Length; i++)
                acousticPixels[i] = bgColor;

            int bandWidth = AcousticTexWidth / AcousticBandCount;
            int gap = 4;
            int baseY = 16;
            int maxBarH = AcousticTexHeight - 42;

            for (int band = 0; band < AcousticBandCount; band++)
            {
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

                float heightNorm;
                Color barColor;

                if (bandSpl < AcousticAmberThreshold)
                {
                    heightNorm = Mathf.Clamp(bandSpl / AcousticAmberThreshold * 0.15f, 0.04f, 0.15f);
                    barColor = new Color(0f, 1f, 0.53f, 1f); // #00FF88
                }
                else if (bandSpl <= AcousticRedThreshold)
                {
                    float t = (bandSpl - AcousticAmberThreshold) / (AcousticRedThreshold - AcousticAmberThreshold);
                    heightNorm = Mathf.Lerp(0.25f, 0.65f, t);
                    barColor = new Color(1f, 0.702f, 0f, 1f); // #FFB300
                }
                else
                {
                    float t = Mathf.Clamp01((bandSpl - AcousticRedThreshold) / (AcousticMaxDb - AcousticRedThreshold));
                    heightNorm = Mathf.Lerp(0.70f, 0.98f, t);
                    barColor = new Color(0.8f, 0f, 0f, 1f); // #CC0000
                }

                int barHeight = Mathf.RoundToInt(heightNorm * maxBarH);
                int x0 = band * bandWidth + gap / 2;
                int x1 = (band + 1) * bandWidth - gap / 2;

                for (int y = baseY; y < baseY + barHeight; y++)
                {
                    for (int x = x0; x < x1 && x < AcousticTexWidth; x++)
                    {
                        acousticPixels[y * AcousticTexWidth + x] = barColor;
                    }
                }
            }

            // Top Header Banner
            DrawFilledRect(acousticPixels, AcousticTexWidth, 0, AcousticTexHeight - 15, AcousticTexWidth, 15, new Color(0f, 0.12f, 0.04f, 1f));
            DrawText(acousticPixels, AcousticTexWidth, AcousticTexHeight, 22, AcousticTexHeight - 12, "ACOUSTIC SONAR ARRAY (8-BAND FFT)", headerColor);
            DrawHLine(acousticPixels, AcousticTexWidth, 0, AcousticTexHeight - 16, AcousticTexWidth, scaleColor);

            // Telemetry line
            string peakStr = string.Format("PEAK: {0:F0} dB SPL @ 4.2kHz [4-PROP ROTOR]", acousticSplDb);
            DrawText(acousticPixels, AcousticTexWidth, AcousticTexHeight, 8, AcousticTexHeight - 25, peakStr, headerColor);

            // Bottom Frequency Scale
            DrawFilledRect(acousticPixels, AcousticTexWidth, 0, 0, AcousticTexWidth, 15, new Color(0f, 0.12f, 0.04f, 1f));
            DrawHLine(acousticPixels, AcousticTexWidth, 0, 15, AcousticTexWidth, scaleColor);
            DrawText(acousticPixels, AcousticTexWidth, AcousticTexHeight, 6, 4, "63  125 250 500  1K  2K  4K  8K", scaleColor);

            // Decibel scale marks on left
            DrawText(acousticPixels, AcousticTexWidth, AcousticTexHeight, 2, 80, "80", scaleColor);
            DrawText(acousticPixels, AcousticTexWidth, AcousticTexHeight, 2, 55, "55", scaleColor);
            DrawText(acousticPixels, AcousticTexWidth, AcousticTexHeight, 2, 40, "40", scaleColor);

            acousticTex.SetPixels(acousticPixels);
            acousticTex.Apply();
        }

        // ══════════════════════════════════════════════════════════
        // Procedural EW / SMASH 2000 Panel
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Renders EW jammer status + SMASH 2000 reticle crosshair into ewSmashTex with complete military telemetry.
        /// </summary>
        public void RenderEwSmashPanel()
        {
            if (ewSmashTex == null || ewSmashPixels == null) return;

            int W = EwSmashTexWidth;
            int H = EwSmashTexHeight;

            Color bgColor = new Color(0.02f, 0.035f, 0.02f, 1f);
            Color textGreen = new Color(0f, 0.85f, 0.20f, 1f);
            Color brightGreen = new Color(0.2f, 1f, 0.4f, 1f);
            Color dimGreen = new Color(0f, 0.45f, 0.15f, 1f);
            Color roeRed = new Color(0.95f, 0.1f, 0.1f, 1f);
            Color engagedAmber = new Color(1f, 0.702f, 0f, 1f);
            Color crosshairColor = new Color(0f, 1f, 0.3f, 1f);
            Color hitCyan = new Color(0.1f, 1f, 0.9f, 1f);

            // Clear background
            for (int i = 0; i < ewSmashPixels.Length; i++)
                ewSmashPixels[i] = bgColor;

            // Outer border
            DrawRectOutline(ewSmashPixels, W, H, 1, 1, W - 2, H - 2, dimGreen);

            // Top Header Banner
            DrawFilledRect(ewSmashPixels, W, 2, H - 15, W - 4, 13, new Color(0f, 0.15f, 0.05f, 1f));
            DrawText(ewSmashPixels, W, H, 24, H - 12, "EW JAMMER / SMASH 2000 FIRE CONTROL", brightGreen);
            DrawHLine(ewSmashPixels, W, 2, H - 16, W - 4, dimGreen);

            // Vertical divider down middle
            DrawVLine(ewSmashPixels, W, W / 2, 2, H - 18, dimGreen);

            // ── Left Half: EW Directional Jammer ─────────────────
            DrawText(ewSmashPixels, W, H, 24, H - 28, "[ RF DIRECTIONAL JAMMER ]", brightGreen);

            string stateStr = lastEwEngaged ? "STATUS: [ENGAGED/TX]" : "STATUS: [STANDBY]";
            Color stateCol = lastEwEngaged ? engagedAmber : textGreen;
            DrawText(ewSmashPixels, W, H, 8, H - 42, stateStr, stateCol);
            DrawFilledRect(ewSmashPixels, W, 8, H - 48, 60, 4, stateCol);

            string bandStr = string.Format("BAND: {0:F2} GHz ISM", lastEwBandGHz);
            DrawText(ewSmashPixels, W, H, 8, H - 60, bandStr, textGreen);
            DrawText(ewSmashPixels, W, H, 8, H - 72, "RF PWR: 120W EIRP | BW: 20MHz", textGreen);

            string slewStr = string.Format("AZ: {0:000} DEG  EL: {1:+00;-00;+00} DEG", lastAzimuthDeg, lastElevationDeg);
            DrawText(ewSmashPixels, W, H, 8, H - 84, slewStr, textGreen);

            // Azimuth/Elevation alignment bars
            int azBar = Mathf.Clamp(Mathf.RoundToInt(lastAzimuthDeg / 359f * 100), 0, 100);
            DrawFilledRect(ewSmashPixels, W, 8, H - 94, azBar, 5, textGreen);

            // ROE Status Banner at bottom of left half
            if (lastBlackoutRisk)
            {
                DrawFilledRect(ewSmashPixels, W, 6, 8, W / 2 - 12, 16, roeRed);
                DrawText(ewSmashPixels, W, H, 10, 13, "*** ROE BLACKOUT RISK ***", Color.white);
            }
            else
            {
                DrawFilledRect(ewSmashPixels, W, 6, 8, W / 2 - 12, 16, new Color(0f, 0.25f, 0.08f, 1f));
                DrawText(ewSmashPixels, W, H, 14, 13, "ROE: CLEAR [WEAPONS FREE]", brightGreen);
            }

            // ── Right Half: SMASH 2000 Smart Reticle ─────────────
            DrawText(ewSmashPixels, W, H, W / 2 + 18, H - 28, "[ SMASH 2000 SMART RETICLE ]", brightGreen);
            DrawText(ewSmashPixels, W, H, W / 2 + 8, H - 42, "TRACK: LOCK ACQUIRED", textGreen);
            DrawText(ewSmashPixels, W, H, W / 2 + 8, H - 54, "RNG: 185m  SPEED: 18 m/s", textGreen);

            string leadStr = string.Format("LEAD: {0:+0.0;-0.0;+0.0} DEG", lastLeadAngleDeg);
            DrawText(ewSmashPixels, W, H, W / 2 + 8, H - 66, leadStr, textGreen);

            // Reticle Center
            int cx = W * 3 / 4;
            int cy = 46;
            int crossLen = 14;

            DrawVLine(ewSmashPixels, W, cx, cy - crossLen, crossLen * 2, crosshairColor);
            DrawHLine(ewSmashPixels, W, cx - crossLen, cy, crossLen * 2, crosshairColor);
            DrawCircleApprox(ewSmashPixels, W, H, cx, cy, 12, crosshairColor);

            // Dynamic Lead Dot
            float leadRad = lastLeadAngleDeg * Mathf.Deg2Rad;
            int leadX = cx + Mathf.RoundToInt(Mathf.Sin(leadRad) * 10f);
            int leadY = cy + Mathf.RoundToInt(Mathf.Cos(leadRad) * 10f);
            Color dotColor = lastKineticHit ? hitCyan : roeRed;
            DrawFilledRect(ewSmashPixels, W, leadX - 1, leadY - 1, 3, 3, dotColor);

            // Fire Solution Banner at bottom of right half
            if (lastKineticHit)
            {
                DrawFilledRect(ewSmashPixels, W, W / 2 + 6, 8, W / 2 - 12, 16, new Color(0f, 0.3f, 0.35f, 1f));
                DrawText(ewSmashPixels, W, H, W / 2 + 10, 13, ">> TARGET DESTROYED <<", hitCyan);
            }
            else
            {
                DrawFilledRect(ewSmashPixels, W, W / 2 + 6, 8, W / 2 - 12, 16, new Color(0f, 0.25f, 0.08f, 1f));
                DrawText(ewSmashPixels, W, H, W / 2 + 10, 13, "FIRE SOLUTION: READY [SPACE]", brightGreen);
            }

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

            int winY0 = 18;
            int winH = 82;
            Color[] oldRows = rfFallbackTex.GetPixels(0, winY0, W, winH - 1);
            rfFallbackTex.SetPixels(0, winY0 + 1, W, winH - 1, oldRows);

            for (int x = 0; x < W; x++)
            {
                float jitter = -90f + (float)(Math.Sin(x * 0.15 + Time.time * 2f) * 2.5);
                rfFallbackRow[x] = UI.RfWaterfallRenderer.MapDbmToColor(jitter);
            }

            float sigGHz = peakGHz > 0.1f ? peakGHz : 2.437f;
            float sigDbm = peakDbm > -85f ? peakDbm : -42f;
            float normX = sigGHz / 6f;
            int center = Mathf.Clamp(Mathf.RoundToInt(normX * (W - 1)), 0, W - 1);
            for (int off = -5; off <= 5; off++)
            {
                int x = center + off;
                if (x >= 0 && x < W)
                {
                    float att = Mathf.Exp(-0.5f * off * off / 2f);
                    float pwr = Mathf.Lerp(-90f, sigDbm, att);
                    rfFallbackRow[x] = UI.RfWaterfallRenderer.MapDbmToColor(pwr);
                }
            }

            rfFallbackTex.SetPixels(0, winY0, W, 1, rfFallbackRow);

            Color dimGreen = new Color(0f, 0.55f, 0.20f, 1f);
            Color brightGreen = new Color(0f, 0.95f, 0.35f, 1f);

            Color[] allPixels = rfFallbackTex.GetPixels();
            DrawFilledRect(allPixels, W, 0, H - 16, W, 16, new Color(0f, 0.12f, 0.04f, 1f));
            DrawText(allPixels, W, H, 20, H - 12, "RF SPECTRUM WATERFALL (0.8 - 6.0 GHz)", brightGreen);
            DrawHLine(allPixels, W, 0, H - 17, W, dimGreen);

            DrawText(allPixels, W, H, 6, H - 26, string.Format("PEAK: {0:F0}dBm @ {1:F3}GHz [FHSS HOPPING]", sigDbm, sigGHz), brightGreen);

            DrawFilledRect(allPixels, W, 0, 0, W, 16, new Color(0f, 0.12f, 0.04f, 1f));
            DrawHLine(allPixels, W, 0, 16, W, dimGreen);
            DrawText(allPixels, W, H, 14, 4, "915 MHz          2.4 GHz          5.8 GHz", dimGreen);

            DrawText(allPixels, W, H, 2, 75, "-30", dimGreen);
            DrawText(allPixels, W, H, 2, 50, "-60", dimGreen);
            DrawText(allPixels, W, H, 2, 25, "-90", dimGreen);

            rfFallbackTex.SetPixels(allPixels);
            rfFallbackTex.Apply();
        }

        // ══════════════════════════════════════════════════════════
        // Standalone Doppler Scope Fallback
        // ══════════════════════════════════════════════════════════

        private void GenerateDopplerBackground()
        {
            int S = 256;
            float center = S / 2f;
            float maxR = 115f;
            Color bg = new Color(0.02f, 0.04f, 0.02f, 1f);
            Color ring = new Color(0f, 0.35f, 0.15f, 1f);
            Color axis = new Color(0f, 0.22f, 0.10f, 1f);
            Color outerBezel = new Color(0.03f, 0.035f, 0.03f, 1f);
            Color dimGreen = new Color(0f, 0.60f, 0.25f, 1f);
            Color brightGreen = new Color(0f, 0.95f, 0.35f, 1f);

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > maxR)
                    {
                        dopplerBgPixels[y * S + x] = outerBezel;
                        continue;
                    }

                    float norm = dist / maxR;
                    bool isRing = Mathf.Abs(norm - 0.33f) < 0.012f ||
                                  Mathf.Abs(norm - 0.66f) < 0.012f ||
                                  Mathf.Abs(norm - 0.99f) < 0.012f;
                    bool isAxis = Mathf.Abs(dx) < 0.75f || Mathf.Abs(dy) < 0.75f;
                    dopplerBgPixels[y * S + x] = isRing ? ring : (isAxis ? axis : bg);
                }
            }

            // Draw corner MFD brackets
            DrawCornerBracket(dopplerBgPixels, S, S, 4, 4, 12, dimGreen);
            DrawCornerBracket(dopplerBgPixels, S, S, S - 16, 4, 12, dimGreen);
            DrawCornerBracket(dopplerBgPixels, S, S, 4, S - 16, 12, dimGreen);
            DrawCornerBracket(dopplerBgPixels, S, S, S - 16, S - 16, 12, dimGreen);

            // Draw corner telemetry text outside the radar circle
            DrawText(dopplerBgPixels, S, S, 6, 246, "L-BAND 1.28G", dimGreen);
            DrawText(dopplerBgPixels, S, S, 6, 236, "PRF: 4000Hz", dimGreen);

            DrawText(dopplerBgPixels, S, S, 180, 246, "SCAN: 360", dimGreen);
            DrawText(dopplerBgPixels, S, S, 180, 236, "FMCW ACTIVE", dimGreen);

            DrawText(dopplerBgPixels, S, S, 6, 16, "TGT: 01 TRACK", dimGreen);
            DrawText(dopplerBgPixels, S, S, 6, 6, "RCS: 0.02m2", dimGreen);

            DrawText(dopplerBgPixels, S, S, 180, 16, "SYS: ARMED", dimGreen);
            DrawText(dopplerBgPixels, S, S, 180, 6, "THREAT: NOMINAL", dimGreen);

            // Draw cardinal points inside radar
            DrawText(dopplerBgPixels, S, S, 125, 234, "N", brightGreen);
            DrawText(dopplerBgPixels, S, S, 125, 14, "S", brightGreen);
            DrawText(dopplerBgPixels, S, S, 14, 125, "W", brightGreen);
            DrawText(dopplerBgPixels, S, S, 238, 125, "E", brightGreen);

            // Range markers along axis
            DrawText(dopplerBgPixels, S, S, 160, 130, "150M", dimGreen);
            DrawText(dopplerBgPixels, S, S, 196, 130, "300M", dimGreen);
            DrawText(dopplerBgPixels, S, S, 230, 130, "500M", dimGreen);

            // Top Header
            DrawText(dopplerBgPixels, S, S, 58, 246, "MICRO-DOPPLER RADAR", brightGreen);
        }

#if UNITY_5_3_OR_NEWER
        private void RenderDopplerFallback(Scenarios.SwarmAgentState[] swarm, Scenarios.SensorReadings sr)
        {
            if (dopplerFallbackTex == null) return;
            int S = 256;
            float center = S / 2f;
            float maxR = 115f;

            Array.Copy(dopplerBgPixels, dopplerRenderBuf, dopplerBgPixels.Length);

            // Dynamic rotating sweep line
            float sweepAngle = (Time.time * 2.2f) % (2f * Mathf.PI);
            DrawSweepLine(dopplerRenderBuf, S, S, center, center, maxR, sweepAngle);

            if (swarm != null && swarm.Length > 0)
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
                    DrawBlip(dopplerRenderBuf, S, S, px, py, agent.isDecoy, i + 1);
                }
            }
            else
            {
                // Standalone simulated target at 185m bearing 45 deg
                float simRange = 185f / 500f * maxR;
                float simAngle = 45f * Mathf.Deg2Rad;
                int px = Mathf.RoundToInt(center + simRange * Mathf.Sin(simAngle));
                int py = Mathf.RoundToInt(center + simRange * Mathf.Cos(simAngle));
                DrawBlip(dopplerRenderBuf, S, S, px, py, false, 1);
            }

            // Tactical header reflecting training state & in-flight stop reminder
            string dopplerHeader = "MICRO-DOPPLER RADAR";
            if (VRScenarioSelector.Instance != null)
            {
                switch (VRScenarioSelector.Instance.CurrentState)
                {
                    case VRTrainingState.SELECTING:
                        dopplerHeader = "L-BAND [SELECT 1-4]";
                        break;
                    case VRTrainingState.RUNNING:
                        dopplerHeader = "L-BAND [ESC TO STOP]";
                        break;
                    case VRTrainingState.AAR_SCORECARD:
                        dopplerHeader = "L-BAND [AAR REVIEW]";
                        break;
                }
            }
            Color brightGreen = new Color(0f, 0.95f, 0.35f, 1f);
            DrawFilledRect(dopplerRenderBuf, S, 48, 242, 160, 12, new Color(0.02f, 0.04f, 0.02f, 1f));
            DrawText(dopplerRenderBuf, S, S, 54, 246, dopplerHeader, brightGreen);

            dopplerFallbackTex.SetPixels(dopplerRenderBuf);
            dopplerFallbackTex.Apply();
        }
#endif

        // ══════════════════════════════════════════════════════════
        // Pixel Drawing Helpers & Tactical Font Engine
        // ══════════════════════════════════════════════════════════

        private static void DrawFilledRect(Color[] buf, int bufW, int x0, int y0, int w, int h, Color c)
        {
            int H = buf.Length / bufW;
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (x >= 0 && x < bufW && y >= 0 && y < H)
                        buf[y * bufW + x] = c;
        }

        private static void DrawRectOutline(Color[] buf, int bufW, int bufH, int x0, int y0, int w, int h, Color c)
        {
            DrawHLine(buf, bufW, x0, y0, w, c);
            DrawHLine(buf, bufW, x0, y0 + h - 1, w, c);
            DrawVLine(buf, bufW, x0, y0, h, c);
            DrawVLine(buf, bufW, x0 + w - 1, y0, h, c);
        }

        private static void DrawCornerBracket(Color[] buf, int bufW, int bufH, int x, int y, int len, Color c)
        {
            DrawHLine(buf, bufW, x, y, len, c);
            DrawVLine(buf, bufW, x, y, len, c);
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
            for (int angle = 0; angle < 360; angle += 4)
            {
                float rad = angle * Mathf.Deg2Rad;
                int px = cx + Mathf.RoundToInt(Mathf.Cos(rad) * r);
                int py = cy + Mathf.RoundToInt(Mathf.Sin(rad) * r);
                if (px >= 0 && px < bufW && py >= 0 && py < bufH)
                    buf[py * bufW + px] = c;
            }
        }

        private static void DrawSweepLine(Color[] buf, int bufW, int bufH, float cx, float cy, float maxR, float angle)
        {
            Color bright = new Color(0.2f, 1f, 0.4f, 1f);
            Color dim = new Color(0f, 0.4f, 0.15f, 1f);
            int steps = Mathf.RoundToInt(maxR);
            for (int r = 0; r < steps; r++)
            {
                int px = Mathf.RoundToInt(cx + r * Mathf.Sin(angle));
                int py = Mathf.RoundToInt(cy + r * Mathf.Cos(angle));
                if (px >= 0 && px < bufW && py >= 0 && py < bufH)
                    buf[py * bufW + px] = bright;

                float trailAngle = angle - 0.08f;
                int tx = Mathf.RoundToInt(cx + r * Mathf.Sin(trailAngle));
                int ty = Mathf.RoundToInt(cy + r * Mathf.Cos(trailAngle));
                if (tx >= 0 && tx < bufW && ty >= 0 && ty < bufH)
                    buf[ty * bufW + tx] = dim;
            }
        }

        private static void DrawBlip(Color[] buf, int bufW, int bufH, int px, int py, bool isDecoy, int targetId)
        {
            Color c = isDecoy ? new Color(0.2f, 1f, 0.4f, 1f) : new Color(0f, 0.95f, 0.25f, 1f);
            int sz = isDecoy ? 4 : 3;
            DrawFilledRect(buf, bufW, px - sz / 2, py - sz / 2, sz, sz, c);
            DrawCircleApprox(buf, bufW, bufH, px, py, 6, c);
            DrawText(buf, bufW, bufH, px + 7, py - 3, "TGT-" + targetId.ToString("D2"), c);
        }

        private static readonly byte[][] GlyphTable = InitGlyphTable();

        private static byte[][] InitGlyphTable()
        {
            var table = new byte[128][];
            table['0'] = new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E };
            table['1'] = new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E };
            table['2'] = new byte[] { 0x0E, 0x11, 0x01, 0x06, 0x08, 0x10, 0x1F };
            table['3'] = new byte[] { 0x1E, 0x01, 0x01, 0x0E, 0x01, 0x01, 0x1E };
            table['4'] = new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 };
            table['5'] = new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E };
            table['6'] = new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E };
            table['7'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 };
            table['8'] = new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E };
            table['9'] = new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C };

            table['A'] = new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 };
            table['B'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E };
            table['C'] = new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E };
            table['D'] = new byte[] { 0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C };
            table['E'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F };
            table['F'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 };
            table['G'] = new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F };
            table['H'] = new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 };
            table['I'] = new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E };
            table['J'] = new byte[] { 0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C };
            table['K'] = new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 };
            table['L'] = new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F };
            table['M'] = new byte[] { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 };
            table['N'] = new byte[] { 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11 };
            table['O'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
            table['P'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 };
            table['Q'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D };
            table['R'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 };
            table['S'] = new byte[] { 0x0E, 0x11, 0x10, 0x0E, 0x01, 0x11, 0x0E };
            table['T'] = new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 };
            table['U'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
            table['V'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 };
            table['W'] = new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11 };
            table['X'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 };
            table['Y'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 };
            table['Z'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F };

            table[':'] = new byte[] { 0x00, 0x0C, 0x0C, 0x00, 0x0C, 0x0C, 0x00 };
            table['.'] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C };
            table['-'] = new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 };
            table['+'] = new byte[] { 0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00 };
            table['/'] = new byte[] { 0x01, 0x02, 0x04, 0x08, 0x10, 0x00, 0x00 };
            table['['] = new byte[] { 0x0E, 0x08, 0x08, 0x08, 0x08, 0x08, 0x0E };
            table[']'] = new byte[] { 0x0E, 0x02, 0x02, 0x02, 0x02, 0x02, 0x0E };
            table['('] = new byte[] { 0x04, 0x08, 0x10, 0x10, 0x10, 0x08, 0x04 };
            table[')'] = new byte[] { 0x04, 0x02, 0x01, 0x01, 0x01, 0x02, 0x04 };
            table['%'] = new byte[] { 0x19, 0x1A, 0x04, 0x08, 0x16, 0x13, 0x00 };
            table['>'] = new byte[] { 0x10, 0x08, 0x04, 0x02, 0x04, 0x08, 0x10 };
            table['<'] = new byte[] { 0x01, 0x02, 0x04, 0x08, 0x04, 0x02, 0x01 };
            table['|'] = new byte[] { 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 };
            table['*'] = new byte[] { 0x00, 0x15, 0x0E, 0x1F, 0x0E, 0x15, 0x00 };
            table['#'] = new byte[] { 0x0A, 0x0A, 0x1F, 0x0A, 0x1F, 0x0A, 0x0A };
            table['@'] = new byte[] { 0x0E, 0x11, 0x17, 0x15, 0x17, 0x10, 0x0F };
            table[' '] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            return table;
        }

        public static void DrawText(Color[] buf, int bufW, int bufH, int startX, int startY, string text, Color color, int scale = 1)
        {
            if (buf == null || string.IsNullOrEmpty(text)) return;
            int curX = startX;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = char.ToUpperInvariant(text[i]);
                if (ch == ' ')
                {
                    curX += 4 * scale;
                    continue;
                }
                if (ch < 128 && GlyphTable[ch] != null)
                {
                    byte[] glyph = GlyphTable[ch];
                    for (int r = 0; r < 7; r++)
                    {
                        int rowVal = glyph[r];
                        int pyBase = startY + (6 - r) * scale;
                        for (int c = 0; c < 5; c++)
                        {
                            if ((rowVal & (1 << (4 - c))) != 0)
                            {
                                int pxBase = curX + c * scale;
                                for (int dy = 0; dy < scale; dy++)
                                {
                                    int py = pyBase + dy;
                                    if (py < 0 || py >= bufH) continue;
                                    for (int dx = 0; dx < scale; dx++)
                                    {
                                        int px = pxBase + dx;
                                        if (px >= 0 && px < bufW)
                                        {
                                            buf[py * bufW + px] = color;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    curX += (5 + 1) * scale;
                }
                else
                {
                    curX += 4 * scale;
                }
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

            if (threatMpb == null)
            {
                threatMpb = new MaterialPropertyBlock();
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
            RenderAcousticFft(db, inSelfTest: true);
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
            AdjustThreatTorusPosition();

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
