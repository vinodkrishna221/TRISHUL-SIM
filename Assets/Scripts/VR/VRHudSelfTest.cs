// ============================================================
// VRHudSelfTest.cs — Self-Test for VG-P1-C · Floating 3D Sensor HUD
// TRISHUL-SIM · vr-gamepad track (Uday)
//
// Validates:
//   1. VRHudController threat color mapping (GREEN/AMBER/RED → MIL-STD-2525)
//   2. RenderTexture clear/blit mechanics
//   3. Panel reference assignment and initialization
//   4. AssignReferences wiring path
//   5. Threat state cycling
//   6. Acoustic FFT procedural rendering (bar colors at 30/45/60 dB)
//   7. EW/SMASH procedural rendering (jammer + blackout risk)
//   8. Dual URP+legacy MaterialPropertyBlock properties
//
// Run: attach to any GameObject → Play → check Console for
//      [VRHudSelfTest] 8/8 PASSED or FAILED details.
// ============================================================

using System;
using UnityEngine;

namespace TrishulSim.VR
{
    /// <summary>
    /// Structural self-test for VRHudController — validates all code paths
    /// without requiring VRSentryPost scene or ScenarioManager.
    /// </summary>
    public class VRHudSelfTest : MonoBehaviour
    {
        private int passed = 0;
        private int failed = 0;
        private int total = 0;

        private void Start()
        {
            Debug.Log("[VRHudSelfTest] ═══ Starting VG-P1-C Self-Test (8 suites) ═══");

            TestThreatColorMapping();
            TestRenderTextureClearMechanic();
            TestControllerInitWithNulls();
            TestAssignReferencesWiring();
            TestThreatStateCycling();
            TestAcousticFftProceduralRendering();
            TestEwSmashProceduralRendering();
            TestDualMaterialPropertyBlock();

            Debug.Log(string.Format(
                "[VRHudSelfTest] ═══ Results: {0}/{1} PASSED, {2} FAILED ═══",
                passed, total, failed));

            if (failed == 0)
                Debug.Log("[VRHudSelfTest] ✅ ALL TESTS PASSED");
            else
                Debug.LogError(string.Format("[VRHudSelfTest] ❌ {0} TEST(S) FAILED", failed));
        }

        // ── Test 1: MIL-STD-2525 Threat Color Mapping ───────────

        private void TestThreatColorMapping()
        {
            total++;
            try
            {
                Color green = VRHudController.GetThreatColor("GREEN");
                Color amber = VRHudController.GetThreatColor("AMBER");
                Color red   = VRHudController.GetThreatColor("RED");
                Color unknown = VRHudController.GetThreatColor("UNKNOWN");

                AssertColorClose(green, new Color(0f, 0.702f, 0f, 1f), "GREEN");
                AssertColorClose(amber, new Color(1f, 0.702f, 0f, 1f), "AMBER");
                AssertColorClose(red, new Color(0.8f, 0f, 0f, 1f), "RED");
                AssertColorClose(unknown, green, "UNKNOWN→GREEN");

                Assert(green != amber, "GREEN != AMBER");
                Assert(amber != red, "AMBER != RED");
                Assert(green != red, "GREEN != RED");

                Debug.Log("[VRHudSelfTest] ✓ Test 1: Threat color mapping correct");
                passed++;
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("[VRHudSelfTest] ✗ Test 1: Threat color mapping — {0}", e.Message));
                failed++;
            }
        }

        // ── Test 2: RenderTexture Clear Mechanic ─────────────────

        private void TestRenderTextureClearMechanic()
        {
            total++;
            try
            {
                var rt = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32);
                rt.Create();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.white);
                RenderTexture.active = prev;

                prev = RenderTexture.active;
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.black);
                RenderTexture.active = prev;

                var readTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                prev = RenderTexture.active;
                RenderTexture.active = rt;
                readTex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                readTex.Apply();
                RenderTexture.active = prev;

                Color pixel = readTex.GetPixel(0, 0);
                Assert(pixel.r < 0.1f && pixel.g < 0.1f && pixel.b < 0.1f,
                    string.Format("Cleared pixel should be black, got ({0},{1},{2})", pixel.r, pixel.g, pixel.b));

                rt.Release();
                DestroyImmediate(rt);
                DestroyImmediate(readTex);

                Debug.Log("[VRHudSelfTest] ✓ Test 2: RenderTexture clear mechanic works");
                passed++;
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("[VRHudSelfTest] ✗ Test 2: RenderTexture clear — {0}", e.Message));
                failed++;
            }
        }

        // ── Test 3: Controller Init With Null References ─────────

        private void TestControllerInitWithNulls()
        {
            total++;
            try
            {
                var tempGo = new GameObject("VRHudSelfTest_TempController");
                var controller = tempGo.AddComponent<VRHudController>();

                Assert(!controller.IsInitialized, "Controller should NOT be initialized without references");
                Assert(!controller.IsScenarioRunning, "Scenario should not be running initially");
                Assert(controller.CurrentThreatState == "GREEN", "Default threat state should be GREEN");

                DestroyImmediate(tempGo);

                Debug.Log("[VRHudSelfTest] ✓ Test 3: Controller handles null references gracefully");
                passed++;
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("[VRHudSelfTest] ✗ Test 3: Null reference handling — {0}", e.Message));
                failed++;
            }
        }

        // ── Test 4: AssignReferences Wiring ──────────────────────

        private void TestAssignReferencesWiring()
        {
            total++;
            try
            {
                var tempGo = new GameObject("VRHudSelfTest_WiringTest");
                var controller = tempGo.AddComponent<VRHudController>();

                var rt1 = new RenderTexture(64, 64, 0); rt1.Create();
                var rt2 = new RenderTexture(64, 64, 0); rt2.Create();
                var rt3 = new RenderTexture(64, 32, 0); rt3.Create();
                var rt4 = new RenderTexture(64, 32, 0); rt4.Create();

                var q1 = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var q2 = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var q3 = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var q4 = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var torus = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

                controller.AssignReferences(
                    rt1, rt2, rt3, rt4,
                    q1.GetComponent<Renderer>(), q2.GetComponent<Renderer>(),
                    q3.GetComponent<Renderer>(), q4.GetComponent<Renderer>(),
                    torus.GetComponent<Renderer>()
                );

                Assert(controller.IsInitialized, "Controller should be initialized after AssignReferences");
                Assert(controller.RtRfWaterfall == rt1, "RF RT should be assigned");
                Assert(controller.RtDopplerScope == rt2, "Doppler RT should be assigned");
                Assert(controller.RtAcousticFft == rt3, "Acoustic RT should be assigned");
                Assert(controller.RtEwSmashPanel == rt4, "EW/SMASH RT should be assigned");
                Assert(controller.PanelRfRenderer == q1.GetComponent<Renderer>(), "RF panel renderer assigned");
                Assert(controller.ThreatTorusRenderer == torus.GetComponent<Renderer>(), "Torus renderer assigned");

                rt1.Release(); rt2.Release(); rt3.Release(); rt4.Release();
                DestroyImmediate(rt1); DestroyImmediate(rt2);
                DestroyImmediate(rt3); DestroyImmediate(rt4);
                DestroyImmediate(q1); DestroyImmediate(q2);
                DestroyImmediate(q3); DestroyImmediate(q4);
                DestroyImmediate(torus);
                DestroyImmediate(tempGo);

                Debug.Log("[VRHudSelfTest] ✓ Test 4: AssignReferences wiring correct");
                passed++;
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("[VRHudSelfTest] ✗ Test 4: AssignReferences wiring — {0}", e.Message));
                failed++;
            }
        }

        // ── Test 5: Threat State Cycling ─────────────────────────

        private void TestThreatStateCycling()
        {
            total++;
            try
            {
                ControllerTestRig rig = CreateTestRig("ThreatCycle");

                string[] states = new string[] { "GREEN", "AMBER", "RED", "GREEN" };
                foreach (var state in states)
                {
                    rig.controller.ManualSetThreat(state);
                    Assert(rig.controller.CurrentThreatState == state,
                        string.Format("After ManualSetThreat({0}), state should be {0}", state));
                }

                var mpb = new MaterialPropertyBlock();
                rig.torus.GetComponent<Renderer>().GetPropertyBlock(mpb);
                Color torusColor = mpb.GetColor("_Color");
                AssertColorClose(torusColor, VRHudController.ThreatGreen, "Torus final _Color = GREEN");

                DestroyTestRig(rig);
                Debug.Log("[VRHudSelfTest] ✓ Test 5: Threat state cycling correct");
                passed++;
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("[VRHudSelfTest] ✗ Test 5: Threat state cycling — {0}", e.Message));
                failed++;
            }
        }

        // ── Test 6: Acoustic FFT Procedural Rendering ────────────

        private void TestAcousticFftProceduralRendering()
        {
            total++;
            try
            {
                ControllerTestRig rig = CreateTestRig("AcousticTest");

                // ── 30 dB (ambient, below 40 dB threshold → green, low bars) ──
                rig.controller.ManualRenderAcoustic(30f);
                Assert(rig.controller.AcousticTex != null, "AcousticTex should be allocated");

                Color[] pix30 = rig.controller.AcousticTex.GetPixels();
                Assert(HasNonBlackPixels(pix30), "30 dB: should produce non-black pixels");
                Assert(HasColor(pix30, IsGreenish), "30 dB: dominant color should be greenish (#00FF88)");
                float greenHeight30 = MeasureBarHeight(pix30, VRHudController.AcousticTexWidth, VRHudController.AcousticTexHeight);

                // ── 45 dB (mid, between 40-55 → amber, mid bars) ──
                rig.controller.ManualRenderAcoustic(45f);
                Color[] pix45 = rig.controller.AcousticTex.GetPixels();
                Assert(HasNonBlackPixels(pix45), "45 dB: should produce non-black pixels");
                Assert(HasColor(pix45, IsAmberish), "45 dB: should contain amber bars (#FFB300)");
                float amberHeight45 = MeasureBarHeight(pix45, VRHudController.AcousticTexWidth, VRHudController.AcousticTexHeight);
                Assert(amberHeight45 > greenHeight30, "45 dB bars should be taller than 30 dB bars");

                // ── 60 dB (high, above 55 → red, tall bars) ──
                rig.controller.ManualRenderAcoustic(60f);
                Color[] pix60 = rig.controller.AcousticTex.GetPixels();
                Assert(HasNonBlackPixels(pix60), "60 dB: should produce non-black pixels");
                Assert(HasColor(pix60, IsReddish), "60 dB: should contain red bars (#CC0000)");
                float redHeight60 = MeasureBarHeight(pix60, VRHudController.AcousticTexWidth, VRHudController.AcousticTexHeight);
                Assert(redHeight60 > amberHeight45, "60 dB bars should be taller than 45 dB bars");

                // ── Verify RT got the blit ──
                var rtReadTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rig.rts[2];
                rtReadTex.ReadPixels(new Rect(0, 0, 4, 4), 0, 0);
                rtReadTex.Apply();
                RenderTexture.active = prev;
                Color[] rtPixels = rtReadTex.GetPixels();
                Assert(HasNonBlackPixels(rtPixels), "rtAcousticFft should have non-black pixels after blit");
                DestroyImmediate(rtReadTex);

                DestroyTestRig(rig);
                Debug.Log("[VRHudSelfTest] ✓ Test 6: Acoustic FFT procedural rendering correct");
                passed++;
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("[VRHudSelfTest] ✗ Test 6: Acoustic FFT rendering — {0}", e.Message));
                failed++;
            }
        }

        // ── Test 7: EW/SMASH Procedural Rendering ────────────────

        private void TestEwSmashProceduralRendering()
        {
            total++;
            try
            {
                ControllerTestRig rig = CreateTestRig("EwSmashTest");

                // Normal state — no blackout risk
                rig.controller.ManualRenderEwSmash(
                    engaged: true, bandGHz: 5.825f,
                    azimuth: 45f, elevation: 10f,
                    blackoutRisk: false, leadAngle: 5f, hit: false);

                Assert(rig.controller.EwSmashTex != null, "EwSmashTex should be allocated");
                Color[] pixNormal = rig.controller.EwSmashTex.GetPixels();
                Assert(HasNonBlackPixels(pixNormal), "EW normal state: should produce non-black pixels");

                // Blackout risk state — should produce red warning
                rig.controller.ManualRenderEwSmash(
                    engaged: true, bandGHz: 2.4f,
                    azimuth: 180f, elevation: -5f,
                    blackoutRisk: true, leadAngle: 12f, hit: true);

                Color[] pixBlackout = rig.controller.EwSmashTex.GetPixels();
                Assert(HasNonBlackPixels(pixBlackout), "EW blackout: should produce non-black pixels");
                Assert(HasColor(pixBlackout, IsReddish), "EW blackout: should contain red ROE warning pixels");

                // Pixel content should differ between normal and blackout
                Assert(!PixelsIdentical(pixNormal, pixBlackout),
                    "Normal vs blackout renders should differ");

                // ── Verify RT got the blit ──
                var rtReadTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rig.rts[3];
                rtReadTex.ReadPixels(new Rect(0, 0, 4, 4), 0, 0);
                rtReadTex.Apply();
                RenderTexture.active = prev;
                Color[] rtPixels = rtReadTex.GetPixels();
                Assert(HasNonBlackPixels(rtPixels), "rtEwSmashPanel should have non-black pixels after blit");
                DestroyImmediate(rtReadTex);

                DestroyTestRig(rig);
                Debug.Log("[VRHudSelfTest] ✓ Test 7: EW/SMASH procedural rendering correct");
                passed++;
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("[VRHudSelfTest] ✗ Test 7: EW/SMASH rendering — {0}", e.Message));
                failed++;
            }
        }

        // ── Test 8: Dual URP+Legacy MaterialPropertyBlock ────────

        private void TestDualMaterialPropertyBlock()
        {
            total++;
            try
            {
                ControllerTestRig rig = CreateTestRig("DualMPB");

                // After AssignReferences + ValidateAndInitialize, panels should have
                // both _BaseMap and _MainTex set to the RenderTexture.
                Assert(VRHudController.VerifyDualTextureProperties(
                    rig.controller.PanelRfRenderer, "_BaseMap", "_MainTex"),
                    "RF panel should have both _BaseMap and _MainTex set");

                Assert(VRHudController.VerifyDualTextureProperties(
                    rig.controller.PanelDopplerRenderer, "_BaseMap", "_MainTex"),
                    "Doppler panel should have both _BaseMap and _MainTex set");

                Assert(VRHudController.VerifyDualTextureProperties(
                    rig.controller.PanelAcousticRenderer, "_BaseMap", "_MainTex"),
                    "Acoustic panel should have both _BaseMap and _MainTex set");

                Assert(VRHudController.VerifyDualTextureProperties(
                    rig.controller.PanelEwSmashRenderer, "_BaseMap", "_MainTex"),
                    "EW/SMASH panel should have both _BaseMap and _MainTex set");

                // Torus should have both _BaseColor and _Color set after SetThreatTorusColor
                rig.controller.ManualSetThreat("AMBER");
                Assert(VRHudController.VerifyDualColorProperties(
                    rig.controller.ThreatTorusRenderer),
                    "Threat torus should have both _BaseColor and _Color set");

                // Verify emission is also set
                var mpb = new MaterialPropertyBlock();
                rig.controller.ThreatTorusRenderer.GetPropertyBlock(mpb);
                Color emission = mpb.GetColor("_EmissionColor");
                Assert(emission.r > 0f || emission.g > 0f,
                    string.Format("Emission should be non-zero, got ({0},{1},{2})", emission.r, emission.g, emission.b));

                DestroyTestRig(rig);
                Debug.Log("[VRHudSelfTest] ✓ Test 8: Dual URP+legacy MaterialPropertyBlock correct");
                passed++;
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("[VRHudSelfTest] ✗ Test 8: Dual MaterialPropertyBlock — {0}", e.Message));
                failed++;
            }
        }

        // ══════════════════════════════════════════════════════════
        // Test Rig Factory — shared setup/teardown
        // ══════════════════════════════════════════════════════════

        private struct ControllerTestRig
        {
            public GameObject root;
            public VRHudController controller;
            public RenderTexture[] rts;
            public GameObject[] quads;
            public GameObject torus;
        }

        private ControllerTestRig CreateTestRig(string testName)
        {
            var rig = new ControllerTestRig();
            rig.root = new GameObject(string.Format("VRHudSelfTest_{0}", testName));
            rig.controller = rig.root.AddComponent<VRHudController>();

            rig.rts = new RenderTexture[4];
            rig.rts[0] = new RenderTexture(64, 64, 0); rig.rts[0].Create();
            rig.rts[1] = new RenderTexture(64, 64, 0); rig.rts[1].Create();
            rig.rts[2] = new RenderTexture(64, 32, 0); rig.rts[2].Create();
            rig.rts[3] = new RenderTexture(64, 32, 0); rig.rts[3].Create();

            rig.quads = new GameObject[4];
            for (int i = 0; i < 4; i++)
                rig.quads[i] = GameObject.CreatePrimitive(PrimitiveType.Quad);

            rig.torus = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

            rig.controller.AssignReferences(
                rig.rts[0], rig.rts[1], rig.rts[2], rig.rts[3],
                rig.quads[0].GetComponent<Renderer>(), rig.quads[1].GetComponent<Renderer>(),
                rig.quads[2].GetComponent<Renderer>(), rig.quads[3].GetComponent<Renderer>(),
                rig.torus.GetComponent<Renderer>()
            );

            return rig;
        }

        private void DestroyTestRig(ControllerTestRig rig)
        {
            for (int i = 0; i < 4; i++)
            {
                if (rig.rts[i] != null) { rig.rts[i].Release(); DestroyImmediate(rig.rts[i]); }
                if (rig.quads[i] != null) DestroyImmediate(rig.quads[i]);
            }
            if (rig.torus != null) DestroyImmediate(rig.torus);
            if (rig.root != null) DestroyImmediate(rig.root);
        }

        // ══════════════════════════════════════════════════════════
        // Pixel Analysis Helpers
        // ══════════════════════════════════════════════════════════

        private delegate bool ColorPredicate(Color c);

        private static bool HasNonBlackPixels(Color[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i].r > 0.05f || pixels[i].g > 0.05f || pixels[i].b > 0.05f)
                    return true;
            return false;
        }

        private static bool HasColor(Color[] pixels, ColorPredicate pred)
        {
            for (int i = 0; i < pixels.Length; i++)
                if (pred(pixels[i]))
                    return true;
            return false;
        }

        private static bool IsGreenish(Color c) { return c.g > 0.4f && c.r < 0.2f && c.b < c.g; }
        private static bool IsAmberish(Color c) { return c.r > 0.6f && c.g > 0.4f && c.b < 0.15f; }
        private static bool IsReddish(Color c)  { return c.r > 0.5f && c.g < 0.25f && c.b < 0.25f; }

        /// <summary>
        /// Measures the fraction of the tallest bar in the texture by finding the
        /// highest row that contains any non-background pixel.
        /// </summary>
        private static float MeasureBarHeight(Color[] pixels, int w, int h)
        {
            int topRow = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = pixels[y * w + x];
                    if (c.r > 0.05f || c.g > 0.05f || c.b > 0.05f)
                    {
                        // Check it's not just the dark background
                        if (c.r > 0.1f || c.g > 0.1f)
                        {
                            if (y > topRow) topRow = y;
                        }
                    }
                }
            }
            return (float)topRow / h;
        }

        private static bool PixelsIdentical(Color[] a, Color[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (Mathf.Abs(a[i].r - b[i].r) > 0.01f ||
                    Mathf.Abs(a[i].g - b[i].g) > 0.01f ||
                    Mathf.Abs(a[i].b - b[i].b) > 0.01f)
                    return false;
            }
            return true;
        }

        // ── Assertion Helpers ────────────────────────────────────

        private void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(string.Format("Assertion failed: {0}", message));
        }

        private void AssertColorClose(Color actual, Color expected, string label, float tolerance = 0.02f)
        {
            if (Mathf.Abs(actual.r - expected.r) > tolerance ||
                Mathf.Abs(actual.g - expected.g) > tolerance ||
                Mathf.Abs(actual.b - expected.b) > tolerance)
            {
                throw new Exception(string.Format(
                    "{0}: expected ({1:F3},{2:F3},{3:F3}) got ({4:F3},{5:F3},{6:F3})",
                    label, expected.r, expected.g, expected.b, actual.r, actual.g, actual.b));
            }
        }
    }
}
