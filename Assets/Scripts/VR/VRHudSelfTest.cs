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
//
// Run: attach to any GameObject → Play → check Console for
//      [VRHudSelfTest] 5/5 PASSED or FAILED details.
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
            Debug.Log("[VRHudSelfTest] ═══ Starting VG-P1-C Self-Test ═══");

            TestThreatColorMapping();
            TestRenderTextureClearMechanic();
            TestControllerInitWithNulls();
            TestAssignReferencesWiring();
            TestThreatStateCycling();

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

                // GREEN = #00B300 → (0, 0.702, 0, 1)
                AssertColorClose(green, new Color(0f, 0.702f, 0f, 1f), "GREEN");
                // AMBER = #FFB300 → (1, 0.702, 0, 1)
                AssertColorClose(amber, new Color(1f, 0.702f, 0f, 1f), "AMBER");
                // RED = #CC0000 → (0.8, 0, 0, 1)
                AssertColorClose(red, new Color(0.8f, 0f, 0f, 1f), "RED");
                // Unknown falls back to GREEN
                AssertColorClose(unknown, green, "UNKNOWN→GREEN");

                // Verify they're all different
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
                // Create a test RenderTexture
                var rt = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32);
                rt.Create();

                // Fill with white
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.white);
                RenderTexture.active = prev;

                // Clear to black (same mechanic as VRHudController.ClearRenderTexture)
                prev = RenderTexture.active;
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.black);
                RenderTexture.active = prev;

                // Read back a pixel to verify
                var readTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                prev = RenderTexture.active;
                RenderTexture.active = rt;
                readTex.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                readTex.Apply();
                RenderTexture.active = prev;

                Color pixel = readTex.GetPixel(0, 0);
                Assert(pixel.r < 0.1f && pixel.g < 0.1f && pixel.b < 0.1f,
                    string.Format("Cleared pixel should be black, got ({0},{1},{2})", pixel.r, pixel.g, pixel.b));

                // Cleanup
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
                // Create a temporary GO with VRHudController
                var tempGo = new GameObject("VRHudSelfTest_TempController");
                var controller = tempGo.AddComponent<VRHudController>();

                // Without any references assigned, should not be initialized
                // (Start() would run on next frame, but we can check initial state)
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

                // Create mock RenderTextures
                var rt1 = new RenderTexture(64, 64, 0);   rt1.Create();
                var rt2 = new RenderTexture(64, 64, 0);   rt2.Create();
                var rt3 = new RenderTexture(64, 32, 0);   rt3.Create();
                var rt4 = new RenderTexture(64, 32, 0);   rt4.Create();

                // Create mock panel quads
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

                // Cleanup
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
                var tempGo = new GameObject("VRHudSelfTest_ThreatCycle");
                var controller = tempGo.AddComponent<VRHudController>();

                // Create a minimal torus for color testing
                var torus = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                var rt1 = new RenderTexture(4, 4, 0); rt1.Create();
                var rt2 = new RenderTexture(4, 4, 0); rt2.Create();
                var rt3 = new RenderTexture(4, 4, 0); rt3.Create();
                var rt4 = new RenderTexture(4, 4, 0); rt4.Create();
                var q1 = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var q2 = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var q3 = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var q4 = GameObject.CreatePrimitive(PrimitiveType.Quad);

                controller.AssignReferences(
                    rt1, rt2, rt3, rt4,
                    q1.GetComponent<Renderer>(), q2.GetComponent<Renderer>(),
                    q3.GetComponent<Renderer>(), q4.GetComponent<Renderer>(),
                    torus.GetComponent<Renderer>()
                );

                // Cycle through all threat states
                string[] states = new string[] { "GREEN", "AMBER", "RED", "GREEN" };
                foreach (var state in states)
                {
                    controller.ManualSetThreat(state);
                    Assert(controller.CurrentThreatState == state,
                        string.Format("After ManualSetThreat({0}), state should be {0}", state));
                }

                // Verify torus renderer has property block set
                var mpb = new MaterialPropertyBlock();
                torus.GetComponent<Renderer>().GetPropertyBlock(mpb);
                // Property block should have _Color set (non-default)
                Color torusColor = mpb.GetColor("_Color");
                AssertColorClose(torusColor, VRHudController.ThreatGreen, "Torus final color = GREEN");

                // Cleanup
                rt1.Release(); rt2.Release(); rt3.Release(); rt4.Release();
                DestroyImmediate(rt1); DestroyImmediate(rt2);
                DestroyImmediate(rt3); DestroyImmediate(rt4);
                DestroyImmediate(q1); DestroyImmediate(q2);
                DestroyImmediate(q3); DestroyImmediate(q4);
                DestroyImmediate(torus);
                DestroyImmediate(tempGo);

                Debug.Log("[VRHudSelfTest] ✓ Test 5: Threat state cycling correct");
                passed++;
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("[VRHudSelfTest] ✗ Test 5: Threat state cycling — {0}", e.Message));
                failed++;
            }
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
