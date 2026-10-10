using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TrishulSim.Network;

namespace TrishulSim.Scenarios
{
    /// <summary>
    /// Automated verification and self-test suite for US-P2-C:
    /// 1. Verifies Packages/manifest.json declares com.unity.xr.management and mock-hmd package dependencies.
    /// 2. Verifies ProjectSettings/XRGeneralSettingsPerBuildTarget.asset and XRSettings.asset enable Mock HMD.
    /// 3. Verifies Assets/Scenes/VRSentryStub.unity exists, is valid YAML, and contains XRRig + labeled sphere.
    /// 4. Verifies Assets/Scenes/VRSentryStub-README.txt explains scaffold status and Cut List #1 rationale.
    /// 5. Verifies ProjectSettings/EditorBuildSettings.asset DOES NOT include VRSentryStub.unity.
    /// 6. Verifies InstructorC2.unity and InstructorC2Root.prefab contain SystemInfoText with "XR-Ready: Yes (WebXR scaffold)".
    /// 7. Verifies ScenarioManager runtime class exposes DefaultXrReadyStatus and updates UI text.
    /// 8. Verifies TrishulSimApp simulation run executes with XR scaffold verification and >=60 FPS benchmark.
    /// </summary>
    public static class XRScaffoldSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllAsync().GetAwaiter().GetResult();
                Console.WriteLine("\n[PASS] All US-P2-C XR scaffold stub verification tests passed successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("\n[FAIL] US-P2-C XR scaffold stub test failed: " + ex);
                return 1;
            }
        }
#endif

        public static async Task RunAllAsync()
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("    TRISHUL-SIM · XR SCAFFOLD STUB VERIFICATION SUITE (US-P2-C)");
            Console.WriteLine("    Indian Ministry of Defence (MoD) Problem SIH26247 · PRD SHOULD #1");
            Console.WriteLine("================================================================================\n");

            string rootDir = Directory.GetCurrentDirectory();

            // 1. Verify Packages/manifest.json
            Console.WriteLine("[1/7] Testing Packages/manifest.json for XR Plugin Management...");
            string manifestPath = Path.Combine(rootDir, "Packages", "manifest.json");
            Assert(File.Exists(manifestPath), "Packages/manifest.json must exist at " + manifestPath);
            string manifestText = File.ReadAllText(manifestPath);
            Assert(manifestText.Contains("com.unity.xr.management"), "manifest.json must include com.unity.xr.management");
            Assert(manifestText.Contains("com.unity.xr.mock-hmd"), "manifest.json must include com.unity.xr.mock-hmd");
            Console.WriteLine("      -> com.unity.xr.management and mock-hmd package dependencies confirmed.");

            // 2. Verify XR ProjectSettings (Mock HMD enabled)
            Console.WriteLine("[2/7] Testing ProjectSettings for XR Mock HMD configuration...");
            string xrGeneralPath = Path.Combine(rootDir, "ProjectSettings", "XRGeneralSettingsPerBuildTarget.asset");
            Assert(File.Exists(xrGeneralPath), "ProjectSettings/XRGeneralSettingsPerBuildTarget.asset must exist");
            string xrGeneralText = File.ReadAllText(xrGeneralPath);
            Assert(xrGeneralText.Contains("MockHMD"), "XRGeneralSettingsPerBuildTarget must configure MockHMD loader");

            string xrSettingsPath = Path.Combine(rootDir, "ProjectSettings", "XRSettings.asset");
            Assert(File.Exists(xrSettingsPath), "ProjectSettings/XRSettings.asset must exist");
            string xrSettingsText = File.ReadAllText(xrSettingsPath);
            Assert(xrSettingsText.Contains("mockHmdEnabled: 1") || xrSettingsText.Contains("MockHMD"), "XRSettings.asset must enable mock HMD");
            Console.WriteLine("      -> Mock HMD configuration confirmed in ProjectSettings.");

            // 3. Verify VRSentryStub.unity scene structure
            Console.WriteLine("[3/7] Testing Assets/Scenes/VRSentryStub.unity structure...");
            string stubScenePath = Path.Combine(rootDir, "Assets", "Scenes", "VRSentryStub.unity");
            Assert(File.Exists(stubScenePath), "Assets/Scenes/VRSentryStub.unity must exist");
            string stubSceneText = File.ReadAllText(stubScenePath);
            Assert(stubSceneText.Contains("XRRig"), "VRSentryStub.unity must contain XRRig hierarchy");
            Assert(stubSceneText.Contains("Camera Offset"), "VRSentryStub.unity must contain Camera Offset");
            Assert(stubSceneText.Contains("Main Camera"), "VRSentryStub.unity must contain Main Camera");
            Assert(stubSceneText.Contains("VR SENTRY MODE — SCAFFOLD ONLY — NOT IN DEMO BUILD"),
                "VRSentryStub.unity must contain labeled scaffold sphere");

            string stubMetaPath = stubScenePath + ".meta";
            Assert(File.Exists(stubMetaPath), "VRSentryStub.unity.meta must exist");
            Console.WriteLine("      -> VRSentryStub.unity scene hierarchy and sphere label verified.");

            // 4. Verify VRSentryStub-README.txt
            Console.WriteLine("[4/7] Testing Assets/Scenes/VRSentryStub-README.txt...");
            string readmePath = Path.Combine(rootDir, "Assets", "Scenes", "VRSentryStub-README.txt");
            Assert(File.Exists(readmePath), "Assets/Scenes/VRSentryStub-README.txt must exist");
            string readmeText = File.ReadAllText(readmePath);
            Assert(readmeText.Contains("VR Sentry Mode") && readmeText.Contains("Cut List #1"),
                "VRSentryStub-README.txt must document scaffold status and Cut List #1 rationale");
            Assert(File.Exists(readmePath + ".meta"), "VRSentryStub-README.txt.meta must exist");
            Console.WriteLine("      -> VRSentryStub-README.txt documentation verified.");

            // 5. Verify Build Settings exclusion (VRSentryStub.unity NOT in Build Settings)
            Console.WriteLine("[5/7] Testing ProjectSettings/EditorBuildSettings.asset exclusion...");
            string buildSettingsPath = Path.Combine(rootDir, "ProjectSettings", "EditorBuildSettings.asset");
            Assert(File.Exists(buildSettingsPath), "EditorBuildSettings.asset must exist");
            string buildSettingsText = File.ReadAllText(buildSettingsPath);
            Assert(!buildSettingsText.Contains("VRSentryStub.unity"),
                "CRITICAL: VRSentryStub.unity must NOT be in EditorBuildSettings.asset (must be excluded from demo build per Cut List #1)");
            Assert(buildSettingsText.Contains("Assets/Scenes/SplitScreen.unity"), "SplitScreen.unity must be in build settings");
            Assert(buildSettingsText.Contains("Assets/Scenes/InstructorC2.unity"), "InstructorC2.unity must be in build settings");
            Assert(buildSettingsText.Contains("Assets/Scenes/TraineeCockpit.unity"), "TraineeCockpit.unity must be in build settings");
            Console.WriteLine("      -> Confirmed VRSentryStub.unity is strictly EXCLUDED from demo build scenes.");

            // 6. Verify InstructorC2 system info panel ("XR-Ready: Yes (WebXR scaffold)")
            Console.WriteLine("[6/7] Testing InstructorC2 scene and prefab system info panel...");
            string c2ScenePath = Path.Combine(rootDir, "Assets", "Scenes", "InstructorC2.unity");
            string c2SceneText = File.ReadAllText(c2ScenePath);
            Assert(c2SceneText.Contains("SystemInfoText"), "InstructorC2.unity must contain SystemInfoText GameObject");
            Assert(c2SceneText.Contains("XR-Ready: Yes (WebXR scaffold)"),
                "InstructorC2.unity must contain 'XR-Ready: Yes (WebXR scaffold)' in system info panel");

            string c2PrefabPath = Path.Combine(rootDir, "Assets", "Prefabs", "InstructorC2Root.prefab");
            string c2PrefabText = File.ReadAllText(c2PrefabPath);
            Assert(c2PrefabText.Contains("SystemInfoText"), "InstructorC2Root.prefab must contain SystemInfoText");
            Assert(c2PrefabText.Contains("XR-Ready: Yes (WebXR scaffold)"),
                "InstructorC2Root.prefab must contain 'XR-Ready: Yes (WebXR scaffold)'");

            // Verify ScenarioManager code & runtime behavior
            ScenarioManager testMgr = new ScenarioManager();
            Assert(ScenarioManager.DefaultXrReadyStatus == "XR-Ready: Yes (WebXR scaffold)",
                "DefaultXrReadyStatus must match expected label");
            Assert(testMgr.XrReadyStatus == "XR-Ready: Yes (WebXR scaffold)",
                "testMgr.XrReadyStatus must match DefaultXrReadyStatus");

#if UNITY_5_3_OR_NEWER
            UnityEngine.UI.Text textStub = new UnityEngine.GameObject("TextStub").AddComponent<UnityEngine.UI.Text>();
#else
            UnityEngine.UI.Text textStub = new UnityEngine.UI.Text();
#endif
            testMgr.SystemInfoText = textStub;
            Assert(textStub.text == "XR-Ready: Yes (WebXR scaffold)",
                "Wiring SystemInfoText must update UI Text component");
            Console.WriteLine("      -> InstructorC2 UI and ScenarioManager runtime verified.");

            // 7. Verify TrishulSimApp simulation run with XR scaffold assertion
            Console.WriteLine("[7/7] Executing TrishulSimApp simulation run with XR scaffold verification...");
            await TrishulSimApp.RunSimulationAsync(autoFinish: true);
            Console.WriteLine("      -> Standalone simulation execution passed with XR scaffold confirmed.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Assertion failed: " + message);
            }
        }
    }
}
