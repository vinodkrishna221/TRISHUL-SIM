// ============================================================
// VRModeSelfTest.cs — Self-Test Suite for VG-P1-E
// TRISHUL-SIM · vr-gamepad track (Uday)
//
// Validates:
// 1. ScenarioManager OperationMode.VRMode transitions & events
// 2. VRModeController dual-display routing & fallback:
//    - Display 2 available (spacedesk active) -> targetDisplay = 1
//    - Display 2 absent (laptop-only) -> targetDisplay = 0 (stereo fallback)
//    - C2 Camera -> targetDisplay = 0 (laptop display)
//    - Camera Viewport Rects: Left [0, 0, 0.5, 1], Right [0.5, 0, 0.5, 1]
//    - VR exit -> targetDisplay reset to 0
// 3. Gyro query integration & mock path handling:
//    - Default mock path: offline (false) until VG-P1-D lands
//    - Live status reporting when active
// 4. C2StationController UI binding & status badge:
//    - "VR MODE: OFF"
//    - "VR MODE: ON ● GYRO: LIVE" (status-connected)
//    - "VR MODE: ON ○ GYRO: OFFLINE" (status-waiting)
//    - [VR MODE] / [VR ACTIVE] button toggle
//    - Null-safety when VR elements omitted in legacy mock roots
// 5. UXML & USS integrity in both Assets/UI and Assets/Resources/UI
// 6. docs/vr-setup-guide.md presence and step completeness
//
// Compiles directly with csc.exe or inside Unity Editor tests.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;
using UnityEngine.SceneManagement;
using TrishulSim.Scenarios;
#if UNITY_5_3_OR_NEWER
using TrishulSim.UI;
#endif

namespace TrishulSim.VR
{
    public class VRModeSelfTest : MonoBehaviour
    {
        private static int passed = 0;
        private static int failed = 0;
        private static int total = 0;

#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllTests();
                Console.WriteLine(string.Format("\n[PASS] All VG-P1-E VR Mode & Spacedesk Mirror tests passed successfully! ({0}/{1} suites)", passed, total));
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(string.Format("\n[FAIL] VG-P1-E test failed: {0}", ex.Message));
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }
#endif

        private void Start()
        {
            Debug.Log("[VRModeSelfTest] ═══ Starting VG-P1-E Self-Test Suite ═══");
            RunAllTests();
            Debug.Log(string.Format("[VRModeSelfTest] ═══ Results: {0}/{1} PASSED, {2} FAILED ═══", passed, total, failed));
            if (failed == 0)
                Debug.Log("[VRModeSelfTest] ✅ ALL TESTS PASSED");
            else
                Debug.LogError(string.Format("[VRModeSelfTest] ❌ {0} TEST(S) FAILED", failed));
        }

        public static void RunAllTests()
        {
            passed = 0;
            failed = 0;
            total = 0;

            TestOperationModeEnumAndTransitions();
            TestVRModeControllerCameraRoutingWithSpacedesk();
            TestVRModeControllerLaptopFallbackWithoutPhone();
            TestVRModeControllerExitRestoresDesktop();
            TestGyroStatusQueryIntegration();
#if UNITY_5_3_OR_NEWER
            TestC2StationControllerVrModeToggleAndBadges();
            TestC2StationControllerNullSafety();
#else
            TestC2StationControllerIntegrationContract();
#endif
            TestUxmlAndUssIntegrity();
            TestVrSetupGuideDocumentation();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception("Assertion Failed: " + message);
        }

        private static string FindRepoRoot()
        {
            string current = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(Path.Combine(current, "Assets")) &&
                    File.Exists(Path.Combine(current, "AGENTS.md")))
                {
                    return current;
                }
                current = Path.GetDirectoryName(current);
            }
            return Directory.GetCurrentDirectory();
        }

        // ── Test 1: ScenarioManager OperationMode.VRMode Enum & Transitions ──

        public static void TestOperationModeEnumAndTransitions()
        {
            total++;
            try
            {
                Assert((int)OperationMode.TwoLaptop == 0, "TwoLaptop must be 0");
                Assert((int)OperationMode.SingleLaptopSplit == 1, "SingleLaptopSplit must be 1");
                Assert((int)OperationMode.VRMode == 2, "VRMode must be 2");

                var sm = new ScenarioManager();
                Assert(sm.CurrentMode == OperationMode.TwoLaptop, "Default mode must be TwoLaptop");
                Assert(ScenarioManager.ModeVrText == "VR Mode (spacedesk)", "ModeVrText constant mismatch");
                Assert(ScenarioManager.DefaultVrSentrySceneName == "VRSentryPost", "DefaultVrSentrySceneName mismatch");

                OperationMode firedMode = (OperationMode)(-1);
                int eventCount = 0;
                sm.OnOperationModeChanged += mode =>
                {
                    firedMode = mode;
                    eventCount++;
                };

                // Transition to VRMode
                sm.SetMode(OperationMode.VRMode);
                Assert(sm.CurrentMode == OperationMode.VRMode, "CurrentMode must be VRMode");
                Assert(eventCount == 1, "OnOperationModeChanged should have fired once");
                Assert(firedMode == OperationMode.VRMode, "Fired mode must be VRMode");
                Assert(SceneLoader.LastRequestedScene == "VRSentryPost", "VRSentryPost scene must be requested");

                // Switch back to TwoLaptop
                sm.SetMode(OperationMode.TwoLaptop);
                Assert(sm.CurrentMode == OperationMode.TwoLaptop, "CurrentMode must be TwoLaptop");
                Assert(eventCount == 2, "OnOperationModeChanged should have fired twice");
                Assert(firedMode == OperationMode.TwoLaptop, "Fired mode must be TwoLaptop");
                Assert(SceneLoader.LastRequestedScene == "InstructorC2", "InstructorC2 scene must be requested");

                // Test SwitchToVRMode helper
                sm.SwitchToVRMode();
                Assert(sm.CurrentMode == OperationMode.VRMode, "SwitchToVRMode must set mode to VRMode");
                Assert(eventCount == 3, "OnOperationModeChanged should have fired three times");

                Console.WriteLine("[PASS] Test 1: OperationMode.VRMode enum and transitions");
                passed++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] Test 1: " + ex.Message);
                failed++;
                throw;
            }
        }

        // ── Test 2: VRModeController Dual-Display Routing with Spacedesk (Display 2) ──

        public static void TestVRModeControllerCameraRoutingWithSpacedesk()
        {
            total++;
            try
            {
                var ctrl = new VRModeController();
                var leftCam = new Camera();
                var rightCam = new Camera();
                var c2Cam = new Camera();

                ctrl.LeftEyeCamera = leftCam;
                ctrl.RightEyeCamera = rightCam;
                ctrl.C2Camera = c2Cam;

                // Simulate Spacedesk secondary display active (Display 2 available)
                VRModeController.ForceDisplay2Available = true;
                Assert(ctrl.IsDisplay2Connected, "IsDisplay2Connected must be true when forced true");

                ctrl.EnterVRMode(transitionScene: false);
                Assert(ctrl.IsVRActive, "IsVRActive must be true after EnterVRMode");

                // Verify VR cameras target Display 2 (targetDisplay = 1)
                Assert(leftCam.targetDisplay == 1, "LeftEyeCamera targetDisplay must be 1 (Display 2)");
                Assert(rightCam.targetDisplay == 1, "RightEyeCamera targetDisplay must be 1 (Display 2)");

                // Verify viewport rects are split-stereo
                Assert(Math.Abs(leftCam.rect.x - 0.0f) < 0.001f, "LeftEyeCamera rect.x must be 0.0");
                Assert(Math.Abs(leftCam.rect.width - 0.5f) < 0.001f, "LeftEyeCamera rect.width must be 0.5");
                Assert(Math.Abs(rightCam.rect.x - 0.5f) < 0.001f, "RightEyeCamera rect.x must be 0.5");
                Assert(Math.Abs(rightCam.rect.width - 0.5f) < 0.001f, "RightEyeCamera rect.width must be 0.5");

                // Verify C2 camera stays on Display 1 (targetDisplay = 0)
                Assert(c2Cam.targetDisplay == 0, "C2 Camera targetDisplay must remain 0 (laptop display)");

                VRModeController.ForceDisplay2Available = null;
                Console.WriteLine("[PASS] Test 2: Camera routing with spacedesk (Display 2)");
                passed++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] Test 2: " + ex.Message);
                failed++;
                throw;
            }
        }

        // ── Test 3: VRModeController Laptop-only Fallback (Display 1) ──

        public static void TestVRModeControllerLaptopFallbackWithoutPhone()
        {
            total++;
            try
            {
                var ctrl = new VRModeController();
                var leftCam = new Camera();
                var rightCam = new Camera();
                var c2Cam = new Camera();

                ctrl.LeftEyeCamera = leftCam;
                ctrl.RightEyeCamera = rightCam;
                ctrl.C2Camera = c2Cam;

                // Simulate no phone connected (Display 2 absent)
                VRModeController.ForceDisplay2Available = false;
                Assert(!ctrl.IsDisplay2Connected, "IsDisplay2Connected must be false when forced false");

                ctrl.EnterVRMode(transitionScene: false);
                Assert(ctrl.IsVRActive, "IsVRActive must be true after EnterVRMode");

                // Verify VR cameras fallback to Display 1 (targetDisplay = 0) in split-stereo
                Assert(leftCam.targetDisplay == 0, "LeftEyeCamera targetDisplay must fallback to 0");
                Assert(rightCam.targetDisplay == 0, "RightEyeCamera targetDisplay must fallback to 0");

                Assert(Math.Abs(leftCam.rect.x - 0.0f) < 0.001f, "LeftEyeCamera rect.x must be 0.0");
                Assert(Math.Abs(leftCam.rect.width - 0.5f) < 0.001f, "LeftEyeCamera rect.width must be 0.5");
                Assert(Math.Abs(rightCam.rect.x - 0.5f) < 0.001f, "RightEyeCamera rect.x must be 0.5");
                Assert(Math.Abs(rightCam.rect.width - 0.5f) < 0.001f, "RightEyeCamera rect.width must be 0.5");

                Assert(c2Cam.targetDisplay == 0, "C2 Camera targetDisplay must remain 0");

                VRModeController.ForceDisplay2Available = null;
                Console.WriteLine("[PASS] Test 3: Laptop fallback without phone (Display 1 stereo)");
                passed++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] Test 3: " + ex.Message);
                failed++;
                throw;
            }
        }

        // ── Test 4: VRModeController Exit Restores Desktop ──

        public static void TestVRModeControllerExitRestoresDesktop()
        {
            total++;
            try
            {
                var ctrl = new VRModeController();
                var leftCam = new Camera { targetDisplay = 1 };
                var rightCam = new Camera { targetDisplay = 1 };
                ctrl.LeftEyeCamera = leftCam;
                ctrl.RightEyeCamera = rightCam;

                ctrl.ExitVRMode(transitionScene: false);
                Assert(!ctrl.IsVRActive, "IsVRActive must be false after ExitVRMode");
                Assert(leftCam.targetDisplay == 0, "LeftEyeCamera must reset to display 0");
                Assert(rightCam.targetDisplay == 0, "RightEyeCamera must reset to display 0");

                Console.WriteLine("[PASS] Test 4: Exit VR mode restores desktop configuration");
                passed++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] Test 4: " + ex.Message);
                failed++;
                throw;
            }
        }

        // ── Test 5: Gyro Status Query Integration ──

        public static void TestGyroStatusQueryIntegration()
        {
            total++;
            try
            {
                var ctrl = new VRModeController();

                // Mock path default: offline
                VRModeController.ForceGyroConnected = null;
                Assert(!ctrl.IsGyroConnected, "Default mock path must report IsGyroConnected == false");

                // Live status simulation
                VRModeController.ForceGyroConnected = true;
                Assert(ctrl.IsGyroConnected, "IsGyroConnected must report true when live");

                VRModeController.ForceGyroConnected = false;
                Assert(!ctrl.IsGyroConnected, "IsGyroConnected must report false when offline");

                VRModeController.ForceGyroConnected = null;
                Console.WriteLine("[PASS] Test 5: Gyro status query integration and mock path");
                passed++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] Test 5: " + ex.Message);
                failed++;
                throw;
            }
        }

        // ── Test 6: C2StationController VR Mode Toggle & Status Badges ──

#if UNITY_5_3_OR_NEWER
        public static void TestC2StationControllerVrModeToggleAndBadges()
        {
            total++;
            try
            {
                var controller = new C2StationController();

                var mockRoot = new VisualElement { name = "c2-root" };
                var vrModeBtn = new Button { name = "vr-mode-btn", text = "VR MODE" };
                var vrStatusBadge = new VisualElement { name = "vr-status-badge" };
                var vrStatusIcon = new VisualElement { name = "vr-status-icon" };
                var vrStatusLabel = new Label { name = "vr-status-label", text = "VR MODE: OFF" };

                mockRoot.Add(vrModeBtn);
                mockRoot.Add(vrStatusBadge);
                mockRoot.Add(vrStatusIcon);
                mockRoot.Add(vrStatusLabel);

                controller.BindElements(mockRoot);
                controller.RegisterCallbacks();

                Assert(controller.VrModeBtn != null, "VrModeBtn must be bound");
                Assert(controller.VrStatusBadge != null, "VrStatusBadge must be bound");
                Assert(controller.VrStatusIcon != null, "VrStatusIcon must be bound");
                Assert(controller.VrStatusLabel != null, "VrStatusLabel must be bound");

                // Initial state: VR mode OFF
                controller.UpdateVrStatusBadge(vrActive: false, gyroLive: false);
                Assert(!controller.IsVrModeActive, "IsVrModeActive must be false initially");
                Assert(vrStatusLabel.text == "VR MODE: OFF", "Label must be 'VR MODE: OFF'");
                Assert(vrStatusIcon.ClassListContains("status-waiting"), "Icon must have status-waiting");
                Assert(vrModeBtn.text == "VR MODE", "Button text must be 'VR MODE'");
                Assert(!vrModeBtn.ClassListContains("btn-vr-active"), "Button must not have btn-vr-active");

                // State 2: VR Mode ON, Gyro OFFLINE
                controller.UpdateVrStatusBadge(vrActive: true, gyroLive: false);
                Assert(controller.IsVrModeActive, "IsVrModeActive must be true");
                Assert(!controller.IsGyroLive, "IsGyroLive must be false");
                Assert(vrStatusLabel.text.Contains("VR MODE: ON") && vrStatusLabel.text.Contains("OFFLINE"),
                    "Label must contain 'VR MODE: ON' and 'OFFLINE', found: " + vrStatusLabel.text);
                Assert(vrStatusIcon.ClassListContains("status-waiting"), "Offline gyro must show status-waiting");
                Assert(vrModeBtn.text == "VR ACTIVE", "Button text must be 'VR ACTIVE'");
                Assert(vrModeBtn.ClassListContains("btn-vr-active"), "Button must have btn-vr-active");

                // State 3: VR Mode ON, Gyro LIVE
                controller.UpdateVrStatusBadge(vrActive: true, gyroLive: true);
                Assert(controller.IsVrModeActive, "IsVrModeActive must be true");
                Assert(controller.IsGyroLive, "IsGyroLive must be true");
                Assert(vrStatusLabel.text.Contains("VR MODE: ON") && vrStatusLabel.text.Contains("LIVE"),
                    "Label must contain 'VR MODE: ON' and 'LIVE', found: " + vrStatusLabel.text);
                Assert(vrStatusIcon.ClassListContains("status-connected"), "Live gyro must show status-connected");
                Assert(vrModeBtn.text == "VR ACTIVE", "Button text must be 'VR ACTIVE'");

                // Toggle click simulation
                vrModeBtn.SimulateClick();
                Assert(!controller.IsVrModeActive, "SimulateClick on VR ACTIVE should toggle to OFF");
                Assert(vrStatusLabel.text == "VR MODE: OFF", "Status label must return to OFF");

                vrModeBtn.SimulateClick();
                Assert(controller.IsVrModeActive, "SimulateClick on VR MODE should toggle to ON");

                controller.UnregisterCallbacks();
                Console.WriteLine("[PASS] Test 6: C2StationController VR Mode toggle and status badges");
                passed++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] Test 6: " + ex.Message);
                failed++;
                throw;
            }
        }

        // ── Test 7: C2StationController Null Safety (Legacy Mock Root) ──

        public static void TestC2StationControllerNullSafety()
        {
            total++;
            try
            {
                var controller = new C2StationController();
                var emptyRoot = new VisualElement { name = "empty-root" };

                // Must bind cleanly without throwing NullReferenceException when VR elements are absent
                controller.BindElements(emptyRoot);
                controller.RegisterCallbacks();
                controller.OnVrModeClicked();
                controller.UpdateVrStatusBadge(true, true);
                controller.UpdateVrStatusBadge(false, false);
                controller.UnregisterCallbacks();

                Assert(controller.VrModeBtn == null, "VrModeBtn should be null in empty root");
                Assert(controller.VrStatusBadge == null, "VrStatusBadge should be null in empty root");

                Console.WriteLine("[PASS] Test 7: C2StationController null-safety for omitted VR elements");
                passed++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] Test 7: " + ex.Message);
                failed++;
                throw;
            }
        }
#else
        // ── Test 6: C2StationController VR Integration Contract (Non-Unity) ──

        public static void TestC2StationControllerIntegrationContract()
        {
            total++;
            try
            {
                string root = FindRepoRoot();
                string c2Path = Path.Combine(root, "Assets", "Scripts", "UI", "C2StationController.cs");
                Assert(File.Exists(c2Path), "C2StationController.cs must exist at " + c2Path);

                string content = File.ReadAllText(c2Path);
                Assert(content.Contains("vr-mode-btn"), "C2StationController must bind vr-mode-btn");
                Assert(content.Contains("vr-status-badge"), "C2StationController must bind vr-status-badge");
                Assert(content.Contains("vr-status-icon"), "C2StationController must bind vr-status-icon");
                Assert(content.Contains("vr-status-label"), "C2StationController must bind vr-status-label");
                Assert(content.Contains("VR MODE: ON \\u25cf GYRO: LIVE") || content.Contains("LIVE"), "C2StationController must format live gyro status");
                Assert(content.Contains("VR MODE: ON \\u25cb GYRO: OFFLINE") || content.Contains("OFFLINE"), "C2StationController must format offline gyro status");
                Assert(content.Contains("VR MODE: OFF"), "C2StationController must format VR off status");
                Assert(content.Contains("OnVrModeClicked"), "C2StationController must declare OnVrModeClicked");
                Assert(content.Contains("OperationMode.VRMode"), "C2StationController must reference OperationMode.VRMode");

                Console.WriteLine("[PASS] Test 6: C2StationController VR Mode integration contract verified");
                passed++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] Test 6: " + ex.Message);
                failed++;
                throw;
            }
        }
#endif

        // ── Test 8: UXML and USS Integrity ──

        public static void TestUxmlAndUssIntegrity()
        {
            total++;
            try
            {
                string root = FindRepoRoot();
                string[] uxmlPaths = new string[]
                {
                    Path.Combine(root, "Assets", "UI", "InstructorC2.uxml"),
                    Path.Combine(root, "Assets", "Resources", "UI", "InstructorC2.uxml")
                };

                foreach (var path in uxmlPaths)
                {
                    Assert(File.Exists(path), "File must exist: " + path);
                    XmlDocument xml = new XmlDocument();
                    xml.Load(path);
                    Assert(xml.DocumentElement != null, "UXML must be valid XML: " + path);

                    string content = File.ReadAllText(path);
                    Assert(content.Contains("name=\"vr-mode-btn\""), path + " must contain vr-mode-btn");
                    Assert(content.Contains("name=\"vr-status-badge\""), path + " must contain vr-status-badge");
                    Assert(content.Contains("name=\"vr-status-icon\""), path + " must contain vr-status-icon");
                    Assert(content.Contains("name=\"vr-status-label\""), path + " must contain vr-status-label");
                }

                string[] ussPaths = new string[]
                {
                    Path.Combine(root, "Assets", "UI", "InstructorC2.uss"),
                    Path.Combine(root, "Assets", "Resources", "UI", "InstructorC2.uss")
                };

                foreach (var path in ussPaths)
                {
                    Assert(File.Exists(path), "File must exist: " + path);
                    string content = File.ReadAllText(path);
                    Assert(content.Contains(".btn-vr-mode"), path + " must contain .btn-vr-mode");
                    Assert(content.Contains(".btn-vr-active"), path + " must contain .btn-vr-active");
                    Assert(content.Contains(".vr-status-badge"), path + " must contain .vr-status-badge");
                }

                Console.WriteLine("[PASS] Test 8: UXML and USS file integrity across UI and Resources/UI");
                passed++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] Test 8: " + ex.Message);
                failed++;
                throw;
            }
        }

        // ── Test 9: vr-setup-guide.md Documentation Completeness ──

        public static void TestVrSetupGuideDocumentation()
        {
            total++;
            try
            {
                string root = FindRepoRoot();
                string docPath = Path.Combine(root, "docs", "vr-setup-guide.md");

                Assert(File.Exists(docPath), "docs/vr-setup-guide.md must exist");
                string content = File.ReadAllText(docPath);

                Assert(content.Contains("Step 1"), "Setup guide must contain Step 1 (spacedesk driver)");
                Assert(content.Contains("Step 2"), "Setup guide must contain Step 2 (spacedesk app)");
                Assert(content.Contains("Step 3"), "Setup guide must contain Step 3 (WiFi hotspot)");
                Assert(content.Contains("Step 4"), "Setup guide must contain Step 4 (Phone connect)");
                Assert(content.Contains("Step 5"), "Setup guide must contain Step 5 (spacedesk display mirror)");
                Assert(content.Contains("Step 6"), "Setup guide must contain Step 6 (Gyro tracking)");
                Assert(content.Contains("VR MODE: ON ● GYRO: LIVE"), "Setup guide must document live status badge");
                Assert(content.Contains("Fail-Safe Fallbacks") || content.Contains("fallback"), "Setup guide must document fallbacks");

                Console.WriteLine("[PASS] Test 9: docs/vr-setup-guide.md documentation completeness");
                passed++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] Test 9: " + ex.Message);
                failed++;
                throw;
            }
        }
    }
}
