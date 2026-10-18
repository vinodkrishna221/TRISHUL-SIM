// ============================================================
// VRHolographicScorecard.cs — Floating 3D Holographic AAR Scorecard
// TRISHUL-SIM · vr-gamepad track
//
// Implements a floating 3D holographic scorecard panel inside
// the VRSentryPost watchtower at comfortable eye height (Y=1.75m, Z=2.1m).
// Keeps head tracking and VR immersion active during After Action Review.
//
// Features:
// 1. Procedural 512x384 military tactical UI texture
// 2. MIL-STD-2525 Pass/Fail Badge (Emissive Green #00B300 / Red #CC0000)
// 3. 4 Core Doctrinal Benchmarks: Time to Detection, ROE Compliance,
//    Kinetic Accuracy, Premature Jamming Penalty
// 4. Session telemetry & termination reason readout
// 5. Tactical action prompts: Return to Menu [Space/A], Instant Replay [R/X],
//    and Export PDF [P/Y] with live export status
// 6. Dual URP (_BaseMap) and legacy (_MainTex) shader property support
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using TrishulSim.UI;

namespace TrishulSim.VR
{
    /// <summary>
    /// World-space 3D holographic scorecard panel for After Action Review in VR Sentry Post.
    /// Created dynamically at runtime to preserve zero-drone scene YAML doctrine.
    /// </summary>
    public class VRHolographicScorecard : MonoBehaviour
    {
        public const int TexWidth = 512;
        public const int TexHeight = 384;

        // Position directly in front of the sentry view inside the watchtower
        public static readonly Vector3 DefaultLocalPos = new Vector3(0f, 1.75f, 2.1f);
        public static readonly Vector3 DefaultLocalScale = new Vector3(1.75f, 1.31f, 1f);

        [Header("Runtime References")]
        [SerializeField] private GameObject panelObject;
        [SerializeField] private Renderer panelRenderer;

        private Texture2D scorecardTex;
        private Color[] scorecardPixels;
        private MaterialPropertyBlock mpb;
        private bool isVisible = false;
        private Scorecard currentScorecard;
        private string terminalReason;
        private string exportStatusMessage = null;

        // ── Tactical Color Palette ────────────────────────────────
        private static readonly Color BgColor = new Color(0.02f, 0.04f, 0.07f, 0.98f);
        private static readonly Color CyanBorder = new Color(0f, 0.85f, 0.95f, 1f);
        private static readonly Color HeaderBg = new Color(0.04f, 0.12f, 0.18f, 1f);
        private static readonly Color TextBright = new Color(0f, 0.95f, 0.95f, 1f);
        private static readonly Color TextDim = new Color(0f, 0.60f, 0.70f, 1f);
        private static readonly Color TextWhite = new Color(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color TextYellow = new Color(1f, 0.90f, 0.20f, 1f);

        // MIL-STD-2525
        public static readonly Color PassGreen = new Color(0f, 0.702f, 0f, 1f);       // #00B300
        public static readonly Color FailRed   = new Color(0.80f, 0f, 0f, 1f);        // #CC0000
        public static readonly Color PassBadgeBg = new Color(0f, 0.40f, 0.08f, 1f);
        public static readonly Color FailBadgeBg = new Color(0.50f, 0.05f, 0.05f, 1f);

        public bool IsVisible { get { return isVisible; } }
        public GameObject PanelObject { get { return panelObject; } }
        public Renderer PanelRenderer { get { return panelRenderer; } }
        public Texture2D ScorecardTexture { get { return scorecardTex; } }
        public Scorecard CurrentScorecard { get { return currentScorecard; } }

        private void Awake()
        {
            EnsurePanelConstructed();
        }

        private void OnDestroy()
        {
            if (scorecardTex != null)
            {
                Destroy(scorecardTex);
                scorecardTex = null;
            }
            if (panelObject != null)
            {
                Destroy(panelObject);
                panelObject = null;
            }
        }

        /// <summary>
        /// Constructs the 3D Quad and procedural texture dynamically in world space.
        /// </summary>
        public void EnsurePanelConstructed()
        {
            if (scorecardTex == null)
            {
                scorecardTex = new Texture2D(TexWidth, TexHeight, TextureFormat.RGB24, false);
                scorecardTex.name = "Tex_HolographicScorecard";
                scorecardTex.wrapMode = TextureWrapMode.Clamp;
                scorecardTex.filterMode = FilterMode.Bilinear;
                scorecardPixels = new Color[TexWidth * TexHeight];
            }

            if (panelObject == null)
            {
                Transform existing = transform.Find("FloatingHolographicScorecard");
                if (existing != null)
                {
                    panelObject = existing.gameObject;
                }
                else
                {
                    panelObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    panelObject.name = "FloatingHolographicScorecard";
                    panelObject.transform.SetParent(this.transform, false);
                    panelObject.transform.localPosition = DefaultLocalPos;
                    panelObject.transform.localRotation = Quaternion.identity;
                    panelObject.transform.localScale = DefaultLocalScale;

                    // Remove collider so it does not block raycasts/look
                    Collider col = panelObject.GetComponent<Collider>();
                    if (col != null)
                    {
#if UNITY_EDITOR
                        if (!Application.isPlaying) DestroyImmediate(col);
                        else Destroy(col);
#else
                        Destroy(col);
#endif
                    }
                }

                panelRenderer = panelObject.GetComponent<Renderer>();
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                                Shader.Find("Unlit/Texture") ??
                                Shader.Find("Sprites/Default");
                if (shader != null && panelRenderer != null)
                {
                    Material mat = new Material(shader);
                    mat.name = "Mat_HolographicScorecard";
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                    panelRenderer.sharedMaterial = mat;
                }
            }

            if (mpb == null)
            {
                mpb = new MaterialPropertyBlock();
            }

            if (panelObject != null)
            {
                panelObject.SetActive(isVisible);
            }
        }

        /// <summary>
        /// Activates and renders the holographic scorecard board in front of the trainee.
        /// </summary>
        public void ShowScorecard(Scorecard sc, string reason = null)
        {
            EnsurePanelConstructed();
            currentScorecard = sc ?? ScorecardMock.GetPunjabFixture();
            terminalReason = reason;
            exportStatusMessage = null;
            isVisible = true;

            RenderScorecardTexture();
            ApplyTextureToRenderer();

            if (panelObject != null)
            {
                panelObject.SetActive(true);
            }
        }

        /// <summary>
        /// Updates the scorecard display with PDF export confirmation.
        /// </summary>
        public void SetPdfExportedStatus(string filePath)
        {
            string fileName = string.IsNullOrEmpty(filePath) ? "dossier.pdf" : System.IO.Path.GetFileName(filePath);
            exportStatusMessage = ">> DOSSIER EXPORTED: " + fileName + " <<";
            if (isVisible)
            {
                RenderScorecardTexture();
                ApplyTextureToRenderer();
            }
        }

        /// <summary>
        /// Hides the 3D scorecard board.
        /// </summary>
        public void HideScorecard()
        {
            isVisible = false;
            exportStatusMessage = null;
            if (panelObject != null)
            {
                panelObject.SetActive(false);
            }
        }

        /// <summary>
        /// Binds the procedural texture to the quad renderer with dual URP & legacy properties.
        /// </summary>
        private void ApplyTextureToRenderer()
        {
            if (panelRenderer == null || scorecardTex == null) return;

            if (panelRenderer.sharedMaterial != null)
            {
                panelRenderer.sharedMaterial.mainTexture = scorecardTex;
                if (panelRenderer.sharedMaterial.HasProperty("_BaseMap"))
                    panelRenderer.sharedMaterial.SetTexture("_BaseMap", scorecardTex);
                if (panelRenderer.sharedMaterial.HasProperty("_BaseColor"))
                    panelRenderer.sharedMaterial.SetColor("_BaseColor", Color.white);
                if (panelRenderer.sharedMaterial.HasProperty("_Color"))
                    panelRenderer.sharedMaterial.SetColor("_Color", Color.white);
            }

            if (mpb == null) mpb = new MaterialPropertyBlock();
            panelRenderer.GetPropertyBlock(mpb);
            mpb.SetTexture("_BaseMap", scorecardTex);
            mpb.SetTexture("_MainTex", scorecardTex);
            mpb.SetColor("_BaseColor", Color.white);
            mpb.SetColor("_Color", Color.white);
            panelRenderer.SetPropertyBlock(mpb);
        }

        /// <summary>
        /// Renders the complete military tactical After Action Review scorecard.
        /// </summary>
        public void RenderScorecardTexture()
        {
            if (scorecardTex == null || scorecardPixels == null) return;
            int W = TexWidth;
            int H = TexHeight;

            // 1. Clear background to dark military navy
            for (int i = 0; i < scorecardPixels.Length; i++)
            {
                scorecardPixels[i] = BgColor;
            }

            // 2. Corner tactical brackets and boundary frame
            DrawCornerBracket(scorecardPixels, W, H, 6, 6, 1, 1, 24, CyanBorder);
            DrawCornerBracket(scorecardPixels, W, H, W - 7, 6, -1, 1, 24, CyanBorder);
            DrawCornerBracket(scorecardPixels, W, H, 6, H - 7, 1, -1, 24, CyanBorder);
            DrawCornerBracket(scorecardPixels, W, H, W - 7, H - 7, -1, -1, 24, CyanBorder);
            DrawRectOutline(scorecardPixels, W, H, 4, 4, W - 8, H - 8, new Color(0f, 0.40f, 0.50f, 0.6f));

            // 3. Top Header Bar
            DrawFilledRect(scorecardPixels, W, 8, H - 42, W - 16, 32, HeaderBg);
            DrawHLine(scorecardPixels, W, 8, H - 43, W - 16, CyanBorder);
            DrawText(scorecardPixels, W, H, 52, H - 28, "TRISHUL-SIM // AFTER ACTION REVIEW", CyanBorder, 2);

            // 4. Session & Scenario Subtitle
            string scName = (currentScorecard != null && !string.IsNullOrEmpty(currentScorecard.scenarioName))
                ? currentScorecard.scenarioName
                : "PUNJAB BORDER - ZERO VISIBILITY PINCER";
            string sessId = (currentScorecard != null && !string.IsNullOrEmpty(currentScorecard.sessionId))
                ? currentScorecard.sessionId
                : "SESS-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            float durationSec = (currentScorecard != null && currentScorecard.durationMs > 0)
                ? currentScorecard.durationMs / 1000f
                : 55.0f;

            DrawText(scorecardPixels, W, H, 16, H - 60, "MISSION: " + scName.ToUpperInvariant(), TextWhite);
            DrawText(scorecardPixels, W, H, 16, H - 74, string.Format("SESSION: {0}  |  FLIGHT TIME: {1:F1}s", sessId, durationSec), TextDim);

            // 5. Prominent MIL-STD-2525 Pass/Fail Badge
            bool passed = currentScorecard != null ? currentScorecard.doctrinePassed : true;
            int badgeY = H - 128;
            int badgeH = 40;
            int badgeW = 340;
            int badgeX = (W - badgeW) / 2;

            Color badgeBg = passed ? PassBadgeBg : FailBadgeBg;
            Color badgeBorder = passed ? PassGreen : FailRed;
            DrawFilledRect(scorecardPixels, W, badgeX, badgeY, badgeW, badgeH, badgeBg);
            DrawRectOutline(scorecardPixels, W, H, badgeX, badgeY, badgeW, badgeH, badgeBorder);
            DrawRectOutline(scorecardPixels, W, H, badgeX - 2, badgeY - 2, badgeW + 4, badgeH + 4, badgeBorder);

            string badgeText = passed ? "*** DOCTRINE: PASSED ***" : "*** DOCTRINE: FAILED ***";
            Color badgeTextColor = passed ? new Color(0.2f, 1f, 0.4f, 1f) : new Color(1f, 0.3f, 0.3f, 1f);
            DrawText(scorecardPixels, W, H, badgeX + 26, badgeY + 13, badgeText, badgeTextColor, 2);

            // 6. Terminal Reason Banner
            string reason = !string.IsNullOrEmpty(terminalReason)
                ? terminalReason
                : (passed ? "TARGETS NEUTRALISED" : "MISSION HALTED");
            string reasonDisplay = "TERMINATION: " + reason.ToUpperInvariant();
            if (reason.Contains("ABORT"))
            {
                reasonDisplay = "TERMINATION: IN-FLIGHT STOP TRIGGERED [ESC / GAMEPAD B]";
            }
            DrawText(scorecardPixels, W, H, 16, H - 146, reasonDisplay, TextYellow);

            // 7. Core Benchmarks Table
            var m = currentScorecard != null ? currentScorecard.metrics : null;
            var b = currentScorecard != null ? currentScorecard.doctrineBenchmarks : null;

            int ttdMs = m != null ? m.timeToDetectionMs : 12300;
            int ttdThreshold = b != null && b.timeToDetectionThresholdMs > 0 ? b.timeToDetectionThresholdMs : 15000;
            bool ttdPass = ttdMs <= ttdThreshold;

            float roe = m != null ? m.roeCompliance : 1.0f;
            bool roePass = roe >= 1.0f;

            float kinEff = m != null ? m.kineticEfficiency : 0.75f;
            int kinHits = m != null ? m.kineticHits : 3;
            int kinRounds = m != null ? m.kineticRounds : 4;
            bool kinPass = kinEff >= 0.5f;

            bool prematureEw = m != null ? m.prematureEwPenalty : false;

            int tableY = H - 162;
            DrawFilledRect(scorecardPixels, W, 12, tableY - 96, W - 24, 96, new Color(0.015f, 0.03f, 0.05f, 0.9f));
            DrawRectOutline(scorecardPixels, W, H, 12, tableY - 96, W - 24, 96, new Color(0f, 0.35f, 0.45f, 1f));

            // Metric 1: Time to Detection
            DrawText(scorecardPixels, W, H, 20, tableY - 18, "1. TIME TO DETECTION", TextWhite);
            string ttdVal = string.Format("{0:F1}s (LIMIT: <={1:F0}s)", ttdMs / 1000f, ttdThreshold / 1000f);
            DrawText(scorecardPixels, W, H, 240, tableY - 18, ttdVal, TextDim);
            DrawText(scorecardPixels, W, H, 440, tableY - 18, ttdPass ? "[PASS]" : "[FAIL]", ttdPass ? PassGreen : FailRed);

            // Metric 2: ROE Compliance
            DrawText(scorecardPixels, W, H, 20, tableY - 40, "2. ROE COMPLIANCE", TextWhite);
            string roeVal = string.Format("{0:P0} (ZERO VHF INTERFERENCE)", roe);
            DrawText(scorecardPixels, W, H, 240, tableY - 40, roeVal, TextDim);
            DrawText(scorecardPixels, W, H, 440, tableY - 40, roePass ? "[PASS]" : "[FAIL]", roePass ? PassGreen : FailRed);

            // Metric 3: Kinetic Accuracy
            DrawText(scorecardPixels, W, H, 20, tableY - 62, "3. KINETIC ACCURACY", TextWhite);
            string kinVal = string.Format("{0:P0} (SMASH 2000: {1}/{2} HITS)", kinEff, kinHits, kinRounds);
            DrawText(scorecardPixels, W, H, 240, tableY - 62, kinVal, TextDim);
            DrawText(scorecardPixels, W, H, 440, tableY - 62, kinPass ? "[PASS]" : "[FAIL]", kinPass ? PassGreen : FailRed);

            // Metric 4: Premature Jamming Penalty
            DrawText(scorecardPixels, W, H, 20, tableY - 84, "4. PREMATURE EW", TextWhite);
            string ewVal = prematureEw ? "PENALTY APPLIED" : "0.0s (CLEAN DISCIPLINE)";
            DrawText(scorecardPixels, W, H, 240, tableY - 84, ewVal, TextDim);
            DrawText(scorecardPixels, W, H, 440, tableY - 84, !prematureEw ? "[PASS]" : "[PENALTY]", !prematureEw ? PassGreen : FailRed);

            // 8. PDF Export Notice / Status
            if (!string.IsNullOrEmpty(exportStatusMessage))
            {
                DrawFilledRect(scorecardPixels, W, 12, 64, W - 24, 20, new Color(0f, 0.35f, 0.40f, 1f));
                DrawText(scorecardPixels, W, H, 32, 70, exportStatusMessage, new Color(0.2f, 1f, 0.9f, 1f));
            }
            else
            {
                DrawText(scorecardPixels, W, H, 20, 70, "TACTICAL PERFORMANCE DOSSIER: READY FOR ONE-CLICK EXPORT", TextDim);
            }

            // 9. Interactive Action Footer
            DrawFilledRect(scorecardPixels, W, 8, 8, W - 16, 48, HeaderBg);
            DrawHLine(scorecardPixels, W, 8, 56, W - 16, CyanBorder);

            DrawText(scorecardPixels, W, H, 16, 36, "[SPACE / GAMEPAD A] RETURN TO MISSIONS", TextBright);
            DrawText(scorecardPixels, W, H, 276, 36, "[R / GAMEPAD X] INSTANT REPLAY", TextBright);
            DrawText(scorecardPixels, W, H, 16, 18, "[P / GAMEPAD Y] EXPORT OFFICIAL PDF DOSSIER", new Color(0.2f, 1f, 0.4f, 1f));
            DrawText(scorecardPixels, W, H, 310, 18, "HEAD TRACKING ACTIVE", TextDim);

            scorecardTex.SetPixels(scorecardPixels);
            scorecardTex.Apply();
        }

        // ══════════════════════════════════════════════════════════
        // Pixel Drawing Primitives & Glyph Engine
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

        private static void DrawCornerBracket(Color[] buf, int bufW, int bufH, int cx, int cy, int dirX, int dirY, int len, Color c)
        {
            int hx = dirX > 0 ? cx : cx - len + 1;
            DrawHLine(buf, bufW, hx, cy, len, c);
            int vy = dirY > 0 ? cy : cy - len + 1;
            DrawVLine(buf, bufW, cx, vy, len, c);
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

        private static readonly byte[][] GlyphTable = InitGlyphs();

        private static byte[][] InitGlyphs()
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
            table['='] = new byte[] { 0x00, 0x1F, 0x00, 0x1F, 0x00, 0x00, 0x00 };
            table['|'] = new byte[] { 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 };
            table['*'] = new byte[] { 0x00, 0x15, 0x0E, 0x1F, 0x0E, 0x15, 0x00 };
            table['#'] = new byte[] { 0x0A, 0x0A, 0x1F, 0x0A, 0x1F, 0x0A, 0x0A };
            table['!'] = new byte[] { 0x04, 0x04, 0x04, 0x04, 0x00, 0x04, 0x04 };
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
    }
}
