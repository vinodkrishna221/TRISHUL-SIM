// ============================================================
// GamepadInputSelfTest.cs — Runnable Self-Test for VG-P1-A
// TRISHUL-SIM · vr-gamepad track (Vinod)
//
// Validates:
// 1. TrishulInputActions.inputactions file structure & JSON schema:
//    - Action map "Trainee" exists
//    - All 7 actions exist (JammerAzimuth, JammerElevation, FireSMASH, ToggleJammer, BandUp, BandDown, Recalibrate)
//    - Gamepad & Keyboard bindings mapped according to spec
// 2. Azimuth accumulation, sensitivity & [0, 359°] wrap math
// 3. Elevation stepping (±2°/step) & [-90°, +90°] clamp limits
// 4. Band cycle transitions (2.4 -> 5.825 -> 0.915 GHz)
// 5. SMASH 2000 kinetic trigger dispatch & EW engagement toggle
// 6. Recalibrate event firing for GyroReceiver (VG-P1-D integration)
// 7. TraineeCockpitRoot.prefab & SplitScreen.unity component wiring
// ============================================================

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TrishulSim.UI;
using UnityEngine.UIElements;

namespace TrishulSim.Input
{
    public static class GamepadInputSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllTests();
                Console.WriteLine("\n[PASS] All VG-P1-A Gamepad Input Action tests passed successfully.");
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

        public static void RunAllTests()
        {
            Console.WriteLine("=== Running TRISHUL-SIM VG-P1-A Gamepad Input Actions Self-Test ===");

            TestInputActionsFileAndBindings();
            Console.WriteLine("[PASS] TestInputActionsFileAndBindings");

            TestAzimuthAccumulationAndWrapping();
            Console.WriteLine("[PASS] TestAzimuthAccumulationAndWrapping");

            TestElevationSteppingAndClamping();
            Console.WriteLine("[PASS] TestElevationSteppingAndClamping");

            TestBandCycleProgression();
            Console.WriteLine("[PASS] TestBandCycleProgression");

            TestJammerToggleAndSmashTrigger();
            Console.WriteLine("[PASS] TestJammerToggleAndSmashTrigger");

            TestRecalibrateEventDispatch();
            Console.WriteLine("[PASS] TestRecalibrateEventDispatch");

            TestPrefabAndSceneWireIntegrity();
            Console.WriteLine("[PASS] TestPrefabAndSceneWireIntegrity");

            TestCockpitStatePreservationAndSync();
            Console.WriteLine("[PASS] TestCockpitStatePreservationAndSync");

            TestAzimuthBoundaryModuloPush();
            Console.WriteLine("[PASS] TestAzimuthBoundaryModuloPush");

            TestCanonicalJsonControlSchemes();
            Console.WriteLine("[PASS] TestCanonicalJsonControlSchemes");

            TestNullSafetyWithoutCockpit();
            Console.WriteLine("[PASS] TestNullSafetyWithoutCockpit");
        }

        public static void TestInputActionsFileAndBindings()
        {
            string actionsPath = Path.Combine("Assets", "Input", "TrishulInputActions.inputactions");
            string metaPath = Path.Combine("Assets", "Input", "TrishulInputActions.inputactions.meta");

            Assert(File.Exists(actionsPath), "TrishulInputActions.inputactions must exist at " + actionsPath);
            Assert(File.Exists(metaPath), "TrishulInputActions.inputactions.meta must exist at " + metaPath);

            string json = File.ReadAllText(actionsPath, Encoding.UTF8);

            // Assert map
            Assert(json.Contains("\"name\": \"Trainee\""), "Action map 'Trainee' must exist in TrishulInputActions.inputactions");

            // Assert 7 actions
            string[] requiredActions = new string[]
            {
                "JammerAzimuth",
                "JammerElevation",
                "FireSMASH",
                "ToggleJammer",
                "BandUp",
                "BandDown",
                "Recalibrate"
            };

            foreach (var act in requiredActions)
            {
                Assert(json.Contains("\"name\": \"" + act + "\""), "Action '" + act + "' must exist in inputactions file");
            }

            // Assert required bindings
            Assert(json.Contains("<Gamepad>/leftStick/x"), "Binding <Gamepad>/leftStick/x must exist");
            Assert(json.Contains("<Gamepad>/dpad/y"), "Binding <Gamepad>/dpad/y must exist");
            Assert(json.Contains("<Gamepad>/rightTrigger"), "Binding <Gamepad>/rightTrigger must exist");
            Assert(json.Contains("<Gamepad>/buttonSouth"), "Binding <Gamepad>/buttonSouth must exist");
            Assert(json.Contains("<Gamepad>/buttonNorth"), "Binding <Gamepad>/buttonNorth must exist");
            Assert(json.Contains("<Gamepad>/buttonWest"), "Binding <Gamepad>/buttonWest must exist");
            Assert(json.Contains("<Gamepad>/select"), "Binding <Gamepad>/select must exist");

            // Assert keyboard fallbacks
            Assert(json.Contains("<Keyboard>/a"), "Keyboard fallback <Keyboard>/a must exist");
            Assert(json.Contains("<Keyboard>/d"), "Keyboard fallback <Keyboard>/d must exist");
            Assert(json.Contains("<Keyboard>/w"), "Keyboard fallback <Keyboard>/w must exist");
            Assert(json.Contains("<Keyboard>/s"), "Keyboard fallback <Keyboard>/s must exist");
            Assert(json.Contains("<Keyboard>/space"), "Keyboard fallback <Keyboard>/space must exist");
            Assert(json.Contains("<Keyboard>/j"), "Keyboard fallback <Keyboard>/j must exist");
            Assert(json.Contains("<Keyboard>/equals"), "Keyboard fallback <Keyboard>/equals must exist");
            Assert(json.Contains("<Keyboard>/minus"), "Keyboard fallback <Keyboard>/minus must exist");
            Assert(json.Contains("<Keyboard>/r"), "Keyboard fallback <Keyboard>/r must exist");
        }

        public static void TestAzimuthAccumulationAndWrapping()
        {
            var controller = new GamepadInputController();
            controller.AzimuthSensitivity = 3.0f;

            // 1. Initial azimuth is 0
            Assert(controller.CurrentAzimuth == 0f, "Initial azimuth must be 0°");

            // 2. Deadzone: deflection <= 0.05 has no effect
            controller.ApplyAzimuthDelta(0.04f);
            Assert(controller.CurrentAzimuth == 0f, "Deadzone deflection should not change azimuth");

            // 3. Full right deflection (+1.0) -> +3.0°
            controller.ApplyAzimuthDelta(1.0f);
            Assert(Math.Abs(controller.CurrentAzimuth - 3.0f) < 0.001f, "Full right deflection must add 3°");

            // 4. Repeat 10 times -> 33°
            for (int i = 0; i < 10; i++)
            {
                controller.ApplyAzimuthDelta(1.0f);
            }
            Assert(Math.Abs(controller.CurrentAzimuth - 33.0f) < 0.001f, "Accumulated azimuth must be 33°");

            // 5. Wrap over 360° -> 357° + 3° = 0°
            var wrapController = new GamepadInputController();
            wrapController.AzimuthSensitivity = 3.0f;
            for (int i = 0; i < 119; i++)
            {
                wrapController.ApplyAzimuthDelta(1.0f);
            }
            Assert(Math.Abs(wrapController.CurrentAzimuth - 357.0f) < 0.001f, "Azimuth at 119 steps should be 357°");
            wrapController.ApplyAzimuthDelta(1.0f);
            Assert(Math.Abs(wrapController.CurrentAzimuth - 0.0f) < 0.001f, "357° + 3° must wrap to 0°");
            wrapController.ApplyAzimuthDelta(1.0f);
            Assert(Math.Abs(wrapController.CurrentAzimuth - 3.0f) < 0.001f, "0° + 3° must advance to 3°");

            // 6. Wrap below 0° -> 0° - 3° = 357°
            var revController = new GamepadInputController();
            revController.AzimuthSensitivity = 3.0f;
            revController.ApplyAzimuthDelta(-1.0f);
            Assert(Math.Abs(revController.CurrentAzimuth - 357.0f) < 0.001f, "0° - 3° must wrap to 357°");
        }

        public static void TestElevationSteppingAndClamping()
        {
            var controller = new GamepadInputController();

            Assert(controller.CurrentElevation == 0, "Initial elevation must be 0°");

            // 1. Step up (+1) -> +2°
            controller.ApplyElevationStep(1.0f);
            Assert(controller.CurrentElevation == 2, "Elevation step up must add +2°");

            // 2. Step down (-1) -> back to 0°
            controller.ApplyElevationStep(-1.0f);
            Assert(controller.CurrentElevation == 0, "Elevation step down must subtract -2°");

            // 3. Clamping at +90° maximum
            for (int i = 0; i < 50; i++)
            {
                controller.ApplyElevationStep(1.0f);
            }
            Assert(controller.CurrentElevation == 90, "Elevation must clamp at +90° max");

            // Step up again must stay 90°
            controller.ApplyElevationStep(1.0f);
            Assert(controller.CurrentElevation == 90, "Elevation cannot exceed +90°");

            // 4. Clamping at -90° minimum
            for (int i = 0; i < 100; i++)
            {
                controller.ApplyElevationStep(-1.0f);
            }
            Assert(controller.CurrentElevation == -90, "Elevation must clamp at -90° min");

            // Step down again must stay -90°
            controller.ApplyElevationStep(-1.0f);
            Assert(controller.CurrentElevation == -90, "Elevation cannot go below -90°");
        }

        public static void TestBandCycleProgression()
        {
            var controller = new GamepadInputController();

            // Default band: 5.825 GHz (index 1)
            Assert(Math.Abs(controller.CurrentBandGHz - 5.825f) < 0.01f, "Default band must be 5.825 GHz");
            Assert(controller.CurrentBandIndex == 1, "Default band index must be 1");

            // 1. BandUp (Y / ButtonNorth): cycle 2.4 -> 5.825 -> 0.915 GHz
            // Next from 5.825 is 0.915 GHz (index 2)
            controller.StepBand(1);
            Assert(Math.Abs(controller.CurrentBandGHz - 0.915f) < 0.01f, "BandUp step 1 must be 0.915 GHz");
            Assert(controller.CurrentBandIndex == 2, "Band index must be 2");

            // Next from 0.915 is 2.4 GHz (index 0)
            controller.StepBand(1);
            Assert(Math.Abs(controller.CurrentBandGHz - 2.4f) < 0.01f, "BandUp step 2 must cycle to 2.4 GHz");
            Assert(controller.CurrentBandIndex == 0, "Band index must be 0");

            // Next from 2.4 is 5.825 GHz (index 1)
            controller.StepBand(1);
            Assert(Math.Abs(controller.CurrentBandGHz - 5.825f) < 0.01f, "BandUp step 3 must cycle to 5.825 GHz");
            Assert(controller.CurrentBandIndex == 1, "Band index must be 1");

            // 2. BandDown (X / ButtonWest): reverse cycle
            // Previous from 5.825 is 2.4 GHz (index 0)
            controller.StepBand(-1);
            Assert(Math.Abs(controller.CurrentBandGHz - 2.4f) < 0.01f, "BandDown step 1 must be 2.4 GHz");
            Assert(controller.CurrentBandIndex == 0, "Band index must be 0");

            // Previous from 2.4 is 0.915 GHz (index 2)
            controller.StepBand(-1);
            Assert(Math.Abs(controller.CurrentBandGHz - 0.915f) < 0.01f, "BandDown step 2 must cycle to 0.915 GHz");
            Assert(controller.CurrentBandIndex == 2, "Band index must be 2");
        }

        public static void TestJammerToggleAndSmashTrigger()
        {
            var controller = new GamepadInputController();

            // 1. Toggle jammer
            Assert(controller.CurrentEngaged == false, "Initial jammer engaged must be false");
            controller.ToggleJammer();
            Assert(controller.CurrentEngaged == true, "First ToggleJammer must set engaged to true");
            controller.ToggleJammer();
            Assert(controller.CurrentEngaged == false, "Second ToggleJammer must set engaged to false");

            // 2. Test TriggerFire with mock/live CockpitController
            var cockpit = new CockpitController();
            controller.Cockpit = cockpit;

            // TriggerFire should not throw
            try
            {
                controller.TriggerFire();
            }
            catch (Exception ex)
            {
                Assert(false, "TriggerFire should not throw: " + ex.Message);
            }
        }

        public static void TestRecalibrateEventDispatch()
        {
            var controller = new GamepadInputController();
            bool eventReceived = false;

            controller.OnRecalibrate += () =>
            {
                eventReceived = true;
            };

            controller.TriggerRecalibrate();
            Assert(eventReceived, "OnRecalibrate event must be fired when TriggerRecalibrate is called");
        }

        public static void TestPrefabAndSceneWireIntegrity()
        {
            // 1. TraineeCockpitRoot.prefab
            string prefabPath = Path.Combine("Assets", "Prefabs", "TraineeCockpitRoot.prefab");
            Assert(File.Exists(prefabPath), "TraineeCockpitRoot.prefab must exist");
            string prefabContent = File.ReadAllText(prefabPath, Encoding.UTF8);
            Assert(prefabContent.Contains("GamepadInputController"), "TraineeCockpitRoot.prefab must wire GamepadInputController");

            // 2. SplitScreen.unity scene
            string scenePath = Path.Combine("Assets", "Scenes", "SplitScreen.unity");
            Assert(File.Exists(scenePath), "SplitScreen.unity must exist");
            string sceneContent = File.ReadAllText(scenePath, Encoding.UTF8);
            Assert(sceneContent.Contains("GamepadInputController"), "SplitScreen.unity must wire GamepadInputController on TraineeCockpitRoot");
        }

        public static void TestCockpitStatePreservationAndSync()
        {
            var cockpit = new CockpitController();
            var mockRoot = new VisualElement { name = "cockpit-root" };
            var controlsRow = new VisualElement { name = "controls-row" };
            var ewPanel = new VisualElement { name = "ew-panel" };
            var sliderAz = new SliderInt { name = "ew-azimuth", lowValue = 0, highValue = 359, value = 0 };
            var sliderEl = new SliderInt { name = "ew-elevation", lowValue = -90, highValue = 90, value = 0 };
            var dropdownBand = new DropdownField { name = "ew-band" };
            var toggleEngage = new Toggle { name = "ew-engage", value = false };
            var labelRoeWarning = new Label { name = "ew-roe-warning", text = "" };

            ewPanel.Add(sliderAz);
            ewPanel.Add(sliderEl);
            ewPanel.Add(dropdownBand);
            ewPanel.Add(toggleEngage);
            ewPanel.Add(labelRoeWarning);
            controlsRow.Add(ewPanel);
            mockRoot.Add(controlsRow);

            cockpit.BindElements(mockRoot);
            Assert(cockpit.EwController != null, "EwController must be bound");

            // 1. External state set (e.g. sentry moves UI sliders)
            cockpit.SetEwParameters(247, 15, 2.4f, false);
            Assert(cockpit.EwController.AzimuthDeg == 247, "EwController azimuth must be 247°");
            Assert(cockpit.EwController.ElevationDeg == 15, "EwController elevation must be 15°");
            Assert(Math.Abs(cockpit.EwController.SelectedBandGHz - 2.4f) < 0.01f, "EwController band must be 2.4 GHz");

            var controller = new GamepadInputController();
            controller.Cockpit = cockpit;
            controller.SyncWithCockpitState();

            Assert(Math.Abs(controller.CurrentAzimuth - 247f) < 0.001f, "Controller must sync azimuth to 247°");
            Assert(controller.CurrentElevation == 15, "Controller must sync elevation to 15°");
            Assert(Math.Abs(controller.CurrentBandGHz - 2.4f) < 0.01f, "Controller must sync band to 2.4 GHz");
            Assert(!controller.CurrentEngaged, "Controller must sync engaged to false");

            // 2. Trainee presses ToggleJammer (A button)
            controller.ToggleJammer();
            Assert(controller.CurrentEngaged == true, "Trainee engaged jammer");
            Assert(cockpit.EwController.IsEngaged == true, "Cockpit EwController engaged must be true");
            Assert(cockpit.EwController.AzimuthDeg == 247, "Azimuth must be preserved at 247°, not reset to 0°");
            Assert(cockpit.EwController.ElevationDeg == 15, "Elevation must be preserved at 15°, not reset to 0°");

            // 3. Trainee presses BandUp (Y button)
            controller.StepBand(1);
            Assert(Math.Abs(cockpit.EwController.SelectedBandGHz - 5.825f) < 0.01f, "BandUp from 2.4 must cycle to 5.825 GHz");
            Assert(cockpit.EwController.AzimuthDeg == 247, "Azimuth must remain 247° after band step");
            Assert(cockpit.EwController.ElevationDeg == 15, "Elevation must remain 15° after band step");

            // 4. Trainee presses D-Pad Up (elevation +2°)
            controller.ApplyElevationStep(1.0f);
            Assert(cockpit.EwController.ElevationDeg == 17, "Elevation must step to 17°");
            Assert(cockpit.EwController.AzimuthDeg == 247, "Azimuth must remain 247° after elevation step");

            // 5. External change to azimuth via UI slider (e.g. to 310°)
            cockpit.SetEwParameters(310, 17, 5.825f, true);
            // Now stick nudges azimuth right (+1.0 -> +3°)
            controller.ApplyAzimuthDelta(1.0f);
            Assert(cockpit.EwController.AzimuthDeg == 313, "Azimuth must accumulate from 310° to 313°, not snap back to previous local angle");
        }

        public static void TestAzimuthBoundaryModuloPush()
        {
            var cockpit = new CockpitController();
            var mockRoot = new VisualElement { name = "cockpit-root" };
            var controlsRow = new VisualElement { name = "controls-row" };
            var ewPanel = new VisualElement { name = "ew-panel" };
            var sliderAz = new SliderInt { name = "ew-azimuth", lowValue = 0, highValue = 359, value = 0 };
            var sliderEl = new SliderInt { name = "ew-elevation", lowValue = -90, highValue = 90, value = 0 };
            var dropdownBand = new DropdownField { name = "ew-band" };
            var toggleEngage = new Toggle { name = "ew-engage", value = false };
            var labelRoeWarning = new Label { name = "ew-roe-warning", text = "" };

            ewPanel.Add(sliderAz);
            ewPanel.Add(sliderEl);
            ewPanel.Add(dropdownBand);
            ewPanel.Add(toggleEngage);
            ewPanel.Add(labelRoeWarning);
            controlsRow.Add(ewPanel);
            mockRoot.Add(controlsRow);
            cockpit.BindElements(mockRoot);

            var controller = new GamepadInputController();
            controller.Cockpit = cockpit;

            // Step azimuth to exactly 359.7°
            // Initial is 0; +119 full steps of 3° = 357°
            for (int i = 0; i < 119; i++)
            {
                controller.ApplyAzimuthDelta(1.0f);
            }
            Assert(Math.Abs(controller.CurrentAzimuth - 357f) < 0.01f, "Azimuth at 357°");
            // Add 0.9 deflection: 0.9 * 3 = 2.7° -> 359.7°
            controller.ApplyAzimuthDelta(0.9f);
            Assert(controller.CurrentAzimuth > 359.5f && controller.CurrentAzimuth < 360f, "Current azimuth must be in [359.5, 360)");
            Assert(cockpit.EwController.AzimuthDeg == 0, "RoundToInt(359.7) wrapped via modulo must be 0, never 360");
        }

        public static void TestCanonicalJsonControlSchemes()
        {
            string json = GamepadInputController.CanonicalInputActionsJson;
            Assert(json.Contains("\"controlSchemes\""), "CanonicalInputActionsJson must define controlSchemes");
            Assert(json.Contains("\"name\": \"Gamepad\""), "CanonicalInputActionsJson must have Gamepad scheme");
            Assert(json.Contains("\"name\": \"Keyboard\""), "CanonicalInputActionsJson must have Keyboard scheme");
            Assert(json.Contains("<Gamepad>/leftStick/x"), "CanonicalInputActionsJson must bind leftStick/x");
            Assert(json.Contains("<Keyboard>/a"), "CanonicalInputActionsJson must bind Keyboard a");
        }

        public static void TestNullSafetyWithoutCockpit()
        {
            var controller = new GamepadInputController();
            controller.Cockpit = null;

            // Verify none of these throw NullReferenceException
            controller.ApplyAzimuthDelta(1.0f);
            controller.ApplyElevationStep(1.0f);
            controller.ApplyElevationStep(-1.0f);
            controller.ToggleJammer();
            controller.StepBand(1);
            controller.StepBand(-1);
            controller.TriggerFire();
            controller.TriggerRecalibrate();
            controller.PushStateToCockpit();
            controller.SyncWithCockpitState();
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
