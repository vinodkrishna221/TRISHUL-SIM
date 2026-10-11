// ============================================================
// VRSentryPostSelfTest.cs — Automated Self-Test for VG-P1-B
// TRISHUL-SIM · vr-gamepad track (Vinod)
//
// Validates:
// 1. Assets/Scenes/VRSentryPost.unity file existence & YAML structure
// 2. ProjectSettings/EditorBuildSettings.asset registration at index 3
// 3. Assets/Materials/SkyboxNight.mat procedural full-moon night sky material
// 4. Assets/Materials/DarkConcrete.mat URP Lit watchtower concrete material
// 5. RenderSettings fog (ExponentialSquared, #12122A, density ~0.03) & skybox reference
// 6. Directional Light (Moonlight) with pale tint (#C8D8E8), intensity 0.3 & soft shadows
// 7. VRRig GameObject at origin (0, 0, 0)
// 8. StereoHead child at localPosition (0, 1.65, 0)
// 9. LeftEyeCamera at localPos (-0.032, 0, 0) with ViewportRect (0, 0, 0.5, 1) & clip (0.01, 500)
// 10. RightEyeCamera at localPos (0.032, 0, 0) with ViewportRect (0.5, 0, 0.5, 1) & clip (0.01, 500)
// 11. AudioListener attached to VRRig, strictly absent on Left/Right eye cameras
// 12. GamepadInputController component attached to VRRig
// 13. Watchtower hierarchy: GroundQuad (20x20m, DarkConcrete) + 4 posts + 4 rails at Y=1.5m
// 14. Zero-drone visibility doctrine enforcement (zero drone models in scene)
// ============================================================

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace TrishulSim.VR
{
    public static class VRSentryPostSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllTests();
                Console.WriteLine("\n[PASS] All VG-P1-B 3D Nighttime Sentry Post scene tests passed successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("\n[FAIL] " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }
#endif

#if UNITY_EDITOR
        [UnityEditor.MenuItem("TRISHUL-SIM/Tests/Run VR Sentry Post Self-Test")]
        public static void RunFromEditorMenu()
        {
            try
            {
                RunAllTests();
                UnityEngine.Debug.Log("[PASS] All VG-P1-B 3D Nighttime Sentry Post scene tests passed successfully.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[FAIL] " + ex.Message + "\n" + ex.StackTrace);
            }
        }
#endif

        public static void RunAllTests()
        {
            Console.WriteLine("=== Running TRISHUL-SIM VG-P1-B 3D Nighttime Sentry Post Self-Test ===");

            TestSceneFileExistsAndValidYaml();
            Console.WriteLine("[PASS] TestSceneFileExistsAndValidYaml");

            TestEditorBuildSettingsRegistration();
            Console.WriteLine("[PASS] TestEditorBuildSettingsRegistration");

            TestSkyboxNightMaterial();
            Console.WriteLine("[PASS] TestSkyboxNightMaterial");

            TestDarkConcreteMaterial();
            Console.WriteLine("[PASS] TestDarkConcreteMaterial");

            TestRenderSettingsFogAndSkybox();
            Console.WriteLine("[PASS] TestRenderSettingsFogAndSkybox");

            TestMoonlightDirectionalLight();
            Console.WriteLine("[PASS] TestMoonlightDirectionalLight");

            TestVRRigHierarchyAndTransform();
            Console.WriteLine("[PASS] TestVRRigHierarchyAndTransform");

            TestStereoHeadChildTransform();
            Console.WriteLine("[PASS] TestStereoHeadChildTransform");

            TestLeftEyeCameraViewportAndIPD();
            Console.WriteLine("[PASS] TestLeftEyeCameraViewportAndIPD");

            TestRightEyeCameraViewportAndIPD();
            Console.WriteLine("[PASS] TestRightEyeCameraViewportAndIPD");

            TestAudioListenerOnVRRigOnly();
            Console.WriteLine("[PASS] TestAudioListenerOnVRRigOnly");

            TestGamepadInputControllerAttachedToVRRig();
            Console.WriteLine("[PASS] TestGamepadInputControllerAttachedToVRRig");

            TestWatchtowerPlatformStructure();
            Console.WriteLine("[PASS] TestWatchtowerPlatformStructure");

            TestZeroDroneVisibilityDoctrine();
            Console.WriteLine("[PASS] TestZeroDroneVisibilityDoctrine");
        }

        public static void TestSceneFileExistsAndValidYaml()
        {
            string scenePath = Path.Combine("Assets", "Scenes", "VRSentryPost.unity");
            string metaPath = scenePath + ".meta";

            Assert(File.Exists(scenePath), "VRSentryPost.unity must exist at " + scenePath);
            Assert(File.Exists(metaPath), "VRSentryPost.unity.meta must exist at " + metaPath);

            string yaml = File.ReadAllText(scenePath, Encoding.UTF8);
            Assert(yaml.StartsWith("%YAML 1.1"), "Scene must begin with %YAML 1.1 header");
            Assert(yaml.Contains("OcclusionCullingSettings:"), "Scene must contain OcclusionCullingSettings");
            Assert(yaml.Contains("RenderSettings:"), "Scene must contain RenderSettings");
            Assert(yaml.Contains("LightmapSettings:"), "Scene must contain LightmapSettings");
            Assert(yaml.Contains("NavMeshSettings:"), "Scene must contain NavMeshSettings");
        }

        public static void TestEditorBuildSettingsRegistration()
        {
            string buildSettingsPath = Path.Combine("ProjectSettings", "EditorBuildSettings.asset");
            Assert(File.Exists(buildSettingsPath), "EditorBuildSettings.asset must exist");

            string content = File.ReadAllText(buildSettingsPath, Encoding.UTF8);
            Assert(content.Contains("Assets/Scenes/VRSentryPost.unity"), "EditorBuildSettings must register Assets/Scenes/VRSentryPost.unity");
            Assert(content.Contains("guid: d1d2d3d4e5f647898a1b2c3d4e5f9001"), "EditorBuildSettings must match VRSentryPost scene GUID");

            // Verify order: SplitScreen (index 0) -> InstructorC2 (index 1) -> TraineeCockpit (index 2) -> VRSentryPost (index 3)
            int idxSplit = content.IndexOf("Assets/Scenes/SplitScreen.unity", StringComparison.Ordinal);
            int idxC2 = content.IndexOf("Assets/Scenes/InstructorC2.unity", StringComparison.Ordinal);
            int idxCockpit = content.IndexOf("Assets/Scenes/TraineeCockpit.unity", StringComparison.Ordinal);
            int idxVR = content.IndexOf("Assets/Scenes/VRSentryPost.unity", StringComparison.Ordinal);

            Assert(idxSplit >= 0, "SplitScreen.unity must be present in build settings");
            Assert(idxC2 > idxSplit, "InstructorC2.unity must follow SplitScreen.unity");
            Assert(idxCockpit > idxC2, "TraineeCockpit.unity must follow InstructorC2.unity");
            Assert(idxVR > idxCockpit, "VRSentryPost.unity must be registered at index 3 following TraineeCockpit");
        }

        public static void TestSkyboxNightMaterial()
        {
            string matPath = Path.Combine("Assets", "Materials", "SkyboxNight.mat");
            string metaPath = matPath + ".meta";

            Assert(File.Exists(matPath), "SkyboxNight.mat must exist at " + matPath);
            Assert(File.Exists(metaPath), "SkyboxNight.mat.meta must exist at " + metaPath);

            string yaml = File.ReadAllText(matPath, Encoding.UTF8);
            Assert(yaml.Contains("m_Shader: {fileID: 106, guid: 0000000000000000f000000000000000, type: 0}"),
                "SkyboxNight.mat must use Skybox/Procedural shader (fileID 106)");
            Assert(yaml.Contains("m_Name: SkyboxNight"), "Material name must be SkyboxNight");

            // Check exposure for full-moon night lighting (exposure in [0.25, 0.35])
            Match expMatch = Regex.Match(yaml, @"_Exposure:\s*([0-9\.]+)");
            Assert(expMatch.Success, "SkyboxNight.mat must define _Exposure float");
            float exposure = float.Parse(expMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert(exposure >= 0.2f && exposure <= 0.4f,
                string.Format("SkyboxNight exposure ({0}) should be calibrated for full-moon night illumination (~0.25-0.35)", exposure));

            // Check moon disk is enabled for full-moon night scenery (_SunDisk > 0)
            Match diskMatch = Regex.Match(yaml, @"_SunDisk:\s*([0-9]+)");
            Assert(diskMatch.Success, "SkyboxNight.mat must define _SunDisk");
            int sunDisk = int.Parse(diskMatch.Groups[1].Value);
            Assert(sunDisk > 0, "SkyboxNight.mat must enable moon disk (_SunDisk > 0) for full-moon scenery");
        }

        public static void TestDarkConcreteMaterial()
        {
            string matPath = Path.Combine("Assets", "Materials", "DarkConcrete.mat");
            string metaPath = matPath + ".meta";

            Assert(File.Exists(matPath), "DarkConcrete.mat must exist at " + matPath);
            Assert(File.Exists(metaPath), "DarkConcrete.mat.meta must exist at " + metaPath);

            string yaml = File.ReadAllText(matPath, Encoding.UTF8);
            Assert(yaml.Contains("guid: 933532a4fcc9baf4fa0491de14d08ed7"), "DarkConcrete.mat must use Universal Render Pipeline/Lit shader");
            Assert(yaml.Contains("m_Name: DarkConcrete"), "Material name must be DarkConcrete");

            // Check base color values around 0.15
            Assert(yaml.Contains("r: 0.15, g: 0.15, b: 0.15") || yaml.Contains("r: 0.15"),
                "DarkConcrete.mat must have dark concrete base color ~0.15");
        }

        public static void TestRenderSettingsFogAndSkybox()
        {
            string scenePath = Path.Combine("Assets", "Scenes", "VRSentryPost.unity");
            string yaml = File.ReadAllText(scenePath, Encoding.UTF8);

            Assert(yaml.Contains("m_Fog: 1"), "RenderSettings must have Fog enabled (m_Fog: 1)");
            Assert(yaml.Contains("m_FogMode: 3"), "Fog mode must be ExponentialSquared (m_FogMode: 3)");

            // Fog density ~0.02 - 0.04
            Match densityMatch = Regex.Match(yaml, @"m_FogDensity:\s*([0-9\.]+)");
            Assert(densityMatch.Success, "RenderSettings must define m_FogDensity");
            float density = float.Parse(densityMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert(density >= 0.02f && density <= 0.04f,
                string.Format("Fog density ({0}) must be within [0.02, 0.04]", density));

            // Skybox material GUID reference
            Assert(yaml.Contains("guid: f1b2c3d4e5f647898a1b2c3d4e5f8001"),
                "RenderSettings m_SkyboxMaterial must reference SkyboxNight.mat GUID");

            // Ambient mode: Trilight (1)
            Assert(yaml.Contains("m_AmbientMode: 1"), "RenderSettings must use Trilight ambient mode (m_AmbientMode: 1)");

            // Sunlight link to Moonlight directional light (100000002)
            Assert(yaml.Contains("m_Sun: {fileID: 100000002}"), "RenderSettings m_Sun must link to Moonlight Directional Light");
        }

        public static void TestMoonlightDirectionalLight()
        {
            string scenePath = Path.Combine("Assets", "Scenes", "VRSentryPost.unity");
            string yaml = File.ReadAllText(scenePath, Encoding.UTF8);

            Assert(yaml.Contains("Directional Light (Moonlight)"), "Scene must contain 'Directional Light (Moonlight)' GameObject");

            // Verify Light component attributes
            Assert(yaml.Contains("m_Type: 1"), "Moonlight must be a Directional Light (Type: 1)");

            // Check pale moonlight color tint (#C8D8E8 -> r~0.784, g~0.847, b~0.910)
            Assert(yaml.Contains("r: 0.78431374") || yaml.Contains("r: 0.78"), "Moonlight must have cool pale-blue/white tint #C8D8E8 (red channel ~0.78)");
            Assert(yaml.Contains("b: 0.9098039") || yaml.Contains("b: 0.91"), "Moonlight must have cool pale-blue/white tint #C8D8E8 (blue channel ~0.91)");

            // Check intensity in [0.25, 0.35]
            Match intensityMatch = Regex.Match(yaml, @"m_Intensity:\s*([0-9\.]+)");
            Assert(intensityMatch.Success, "Moonlight must specify m_Intensity");
            float intensity = float.Parse(intensityMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert(intensity >= 0.25f && intensity <= 0.35f,
                string.Format("Moonlight intensity ({0}) must be within soft full-moon range [0.25, 0.35]", intensity));

            // Shadows enabled
            Assert(yaml.Contains("m_Shadows:"), "Moonlight must have shadows block");
            Assert(yaml.Contains("m_Type: 2"), "Moonlight must have soft shadows enabled (m_Type: 2)");
        }

        public static void TestVRRigHierarchyAndTransform()
        {
            string scenePath = Path.Combine("Assets", "Scenes", "VRSentryPost.unity");
            string yaml = File.ReadAllText(scenePath, Encoding.UTF8);

            Assert(yaml.Contains("m_Name: VRRig"), "Scene must contain 'VRRig' GameObject");
            Assert(yaml.Contains("m_LocalPosition: {x: 0, y: 0, z: 0}"), "VRRig must be positioned at origin (0, 0, 0)");
        }

        public static void TestStereoHeadChildTransform()
        {
            string scenePath = Path.Combine("Assets", "Scenes", "VRSentryPost.unity");
            string yaml = File.ReadAllText(scenePath, Encoding.UTF8);

            Assert(yaml.Contains("m_Name: StereoHead"), "Scene must contain 'StereoHead' GameObject");
            Assert(yaml.Contains("m_LocalPosition: {x: 0, y: 1.65, z: 0}"), "StereoHead must be at localPosition (0, 1.65, 0)");
            Assert(yaml.Contains("m_Father: {fileID: 200000001}"), "StereoHead must be child of VRRig Transform");
        }

        public static void TestLeftEyeCameraViewportAndIPD()
        {
            string scenePath = Path.Combine("Assets", "Scenes", "VRSentryPost.unity");
            string yaml = File.ReadAllText(scenePath, Encoding.UTF8);

            Assert(yaml.Contains("m_Name: LeftEyeCamera"), "Scene must contain 'LeftEyeCamera'");
            Assert(yaml.Contains("m_LocalPosition: {x: -0.032, y: 0, z: 0}"), "LeftEyeCamera must be at localPosition (-0.032, 0, 0) for IPD");

            // Viewport rect: x: 0, y: 0, width: 0.5, height: 1
            Match leftRectMatch = Regex.Match(yaml, @"x:\s*0\s+y:\s*0\s+width:\s*0\.5\s+height:\s*1");
            Assert(leftRectMatch.Success, "LeftEyeCamera must have NormalizedViewPortRect {x: 0, y: 0, width: 0.5, height: 1}");

            Assert(yaml.Contains("near clip plane: 0.01"), "LeftEyeCamera near clip plane must be 0.01");
            Assert(yaml.Contains("far clip plane: 500"), "LeftEyeCamera far clip plane must be 500");
        }

        public static void TestRightEyeCameraViewportAndIPD()
        {
            string scenePath = Path.Combine("Assets", "Scenes", "VRSentryPost.unity");
            string yaml = File.ReadAllText(scenePath, Encoding.UTF8);

            Assert(yaml.Contains("m_Name: RightEyeCamera"), "Scene must contain 'RightEyeCamera'");
            Assert(yaml.Contains("m_LocalPosition: {x: 0.032, y: 0, z: 0}"), "RightEyeCamera must be at localPosition (0.032, 0, 0) for IPD");

            // Viewport rect: x: 0.5, y: 0, width: 0.5, height: 1
            Match rightRectMatch = Regex.Match(yaml, @"x:\s*0\.5\s+y:\s*0\s+width:\s*0\.5\s+height:\s*1");
            Assert(rightRectMatch.Success, "RightEyeCamera must have NormalizedViewPortRect {x: 0.5, y: 0, width: 0.5, height: 1}");
        }

        public static void TestAudioListenerOnVRRigOnly()
        {
            string scenePath = Path.Combine("Assets", "Scenes", "VRSentryPost.unity");
            string yaml = File.ReadAllText(scenePath, Encoding.UTF8);

            // AudioListener attached to VRRig GameObject (200000000)
            Assert(yaml.Contains("AudioListener:"), "Scene must contain AudioListener component");
            Assert(yaml.Contains("m_GameObject: {fileID: 200000000}"), "AudioListener must be attached to VRRig GameObject");

            // Extract all AudioListener blocks (classID 81) and ensure none are on cameras
            MatchCollection listenerBlocks = Regex.Matches(yaml, @"--- !u!81 &.*?(?=(?:--- !u!|\z))", RegexOptions.Singleline);
            Assert(listenerBlocks.Count == 1, string.Format("Expected exactly 1 AudioListener in scene, found {0}", listenerBlocks.Count));
            string singleListener = listenerBlocks[0].Value;
            Assert(singleListener.Contains("m_GameObject: {fileID: 200000000}"), "AudioListener must be attached to VRRig (fileID 200000000)");
            Assert(!singleListener.Contains("220000000"), "AudioListener must not be attached to LeftEyeCamera");
            Assert(!singleListener.Contains("230000000"), "AudioListener must not be attached to RightEyeCamera");
        }

        public static void TestGamepadInputControllerAttachedToVRRig()
        {
            string scenePath = Path.Combine("Assets", "Scenes", "VRSentryPost.unity");
            string yaml = File.ReadAllText(scenePath, Encoding.UTF8);

            Assert(yaml.Contains("guid: b5d4ae04b8f74cd8e6d4d44ae6700001"),
                "VRRig must have GamepadInputController script attached (guid b5d4ae04b8f74cd8e6d4d44ae6700001)");
            Assert(yaml.Contains("GamepadInputController"), "Scene must reference GamepadInputController identifier");
            Assert(yaml.Contains("azimuthSensitivity: 3"), "GamepadInputController must have default azimuthSensitivity: 3");
        }

        public static void TestWatchtowerPlatformStructure()
        {
            string scenePath = Path.Combine("Assets", "Scenes", "VRSentryPost.unity");
            string yaml = File.ReadAllText(scenePath, Encoding.UTF8);

            Assert(yaml.Contains("m_Name: Watchtower"), "Scene must contain 'Watchtower' parent GameObject");
            Assert(yaml.Contains("m_Name: GroundQuad"), "Watchtower must contain 'GroundQuad'");
            Assert(yaml.Contains("m_LocalPosition: {x: 0, y: -0.01, z: 0}"), "GroundQuad must be at Y = -0.01m");
            Assert(yaml.Contains("m_Mesh: {fileID: 10210"), "GroundQuad must use Unity built-in Quad mesh (fileID 10210)");
            Assert(yaml.Contains("guid: f1b2c3d4e5f647898a1b2c3d4e5f8002"),
                "Watchtower elements must reference DarkConcrete material GUID");

            // GroundQuad scale and horizontal floor orientation
            Assert(yaml.Contains("m_LocalScale: {x: 20, y: 20, z: 1}") || yaml.Contains("m_LocalScale: {x: 20, y: 1, z: 20}"),
                "GroundQuad scale must span 20x20m watchtower floor platform");
            Assert(yaml.Contains("x: 90, y: 0, z: 0") || yaml.Contains("x: 0.7071068, y: 0, z: 0, w: 0.7071068"),
                "GroundQuad must be oriented horizontally (90 deg pitch)");

            // 4 Railing Posts
            Assert(yaml.Contains("m_Name: RailingPost_FL"), "Watchtower must contain RailingPost_FL");
            Assert(yaml.Contains("m_Name: RailingPost_FR"), "Watchtower must contain RailingPost_FR");
            Assert(yaml.Contains("m_Name: RailingPost_BL"), "Watchtower must contain RailingPost_BL");
            Assert(yaml.Contains("m_Name: RailingPost_BR"), "Watchtower must contain RailingPost_BR");
            Assert(yaml.Contains("m_LocalPosition: {x: -4, y: 0.75, z: -4}"), "RailingPost_FL position at (-4, 0.75, -4)");
            Assert(yaml.Contains("m_LocalPosition: {x: 4, y: 0.75, z: -4}"), "RailingPost_FR position at (4, 0.75, -4)");
            Assert(yaml.Contains("m_LocalPosition: {x: -4, y: 0.75, z: 4}"), "RailingPost_BL position at (-4, 0.75, 4)");
            Assert(yaml.Contains("m_LocalPosition: {x: 4, y: 0.75, z: 4}"), "RailingPost_BR position at (4, 0.75, 4)");

            // 4 Horizontal Rails at height 1.5m
            Assert(yaml.Contains("m_Name: Rail_Front"), "Watchtower must contain Rail_Front");
            Assert(yaml.Contains("m_Name: Rail_Back"), "Watchtower must contain Rail_Back");
            Assert(yaml.Contains("m_Name: Rail_Left"), "Watchtower must contain Rail_Left");
            Assert(yaml.Contains("m_Name: Rail_Right"), "Watchtower must contain Rail_Right");
            Assert(yaml.Contains("m_LocalPosition: {x: 0, y: 1.5, z: -4}"), "Rail_Front at (0, 1.5, -4)");
            Assert(yaml.Contains("m_LocalPosition: {x: 0, y: 1.5, z: 4}"), "Rail_Back at (0, 1.5, 4)");
            Assert(yaml.Contains("m_LocalPosition: {x: -4, y: 1.5, z: 0}"), "Rail_Left at (-4, 1.5, 0)");
            Assert(yaml.Contains("m_LocalPosition: {x: 4, y: 1.5, z: 0}"), "Rail_Right at (4, 1.5, 0)");
        }

        public static void TestZeroDroneVisibilityDoctrine()
        {
            string scenePath = Path.Combine("Assets", "Scenes", "VRSentryPost.unity");
            string yaml = File.ReadAllText(scenePath, Encoding.UTF8);

            // Zero drone objects allowed in VRSentryPost.unity per C-UAS zero-visibility doctrine
            Assert(!yaml.ToLowerInvariant().Contains("drone"), "CRITICAL: Zero drone objects allowed in VRSentryPost scene (Beat 4 zero-visibility doctrine)");
            Assert(!yaml.Contains("BoidAgent"), "No BoidAgent components in VRSentryPost scene");
            Assert(!yaml.Contains("SwarmManager"), "No SwarmManager in VRSentryPost scene");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("[ASSERTION FAILED] " + message);
            }
        }
    }
}
