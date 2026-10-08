// ============================================================
// UiShellSelfTest.cs — Runnable Self-Test for UX-P0-A (UITK Shell)
// TRISHUL-SIM · ui-ux track (Rani)
//
// Validates:
// 1. InstructorC2.uxml & TraineeCockpit.uxml schema, tags, and element IDs
// 2. InstructorC2.uss & TraineeCockpit.uss MIL-STD-2525 colors and layout
// 3. C2StationController lifecycle, mock connection, scenario dropdown
// 4. CockpitController lifecycle, threat indicator cycling, sensor readings
// 5. 1920x1080 resolution constraints & responsive flexbox rules
//
// Compiles directly with csc.exe or inside Unity Editor tests.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

#if !UNITY_5_3_OR_NEWER
namespace UnityEngine
{
    public static class Resources
    {
        public static T Load<T>(string path) where T : class { return null; }
    }

    public class Object { }
    
    public class GameObject : Object
    {
        public GameObject(string name) { }
        public T AddComponent<T>() where T : Component, new() { return new T(); }
        public T GetComponent<T>() where T : Component { return null; }
    }

    public class Component : Object
    {
        public GameObject gameObject { get { return new GameObject("mock"); } }
        public T GetComponent<T>() where T : Component { return null; }
        public T AddComponent<T>() where T : Component, new() { return new T(); }
    }

    public class MonoBehaviour : Component
    {
        public static void DontDestroyOnLoad(Object target) { }
        public static void Destroy(Object obj) { }
    }

    public class TextAsset : Object
    {
        public string text { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RequireComponent : Attribute
    {
        public Type RequiredType { get; private set; }
        public RequireComponent(Type requiredType) { RequiredType = requiredType; }
    }

    public static class Debug
    {
        public static void Log(string msg) { }
        public static void LogWarning(string msg) { }
        public static void LogError(string msg) { }
    }

    public enum KeyCode { T }

    public static class Input
    {
        public static bool GetKeyDown(KeyCode key) { return false; }
    }
    public static class Mathf
    {
        public static float Clamp01(float val) { return val < 0f ? 0f : (val > 1f ? 1f : val); }
        public static float Clamp(float val, float min, float max) { return val < min ? min : (val > max ? max : val); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static int RoundToInt(float val) { return (int)Math.Round(val); }
    }
}

namespace UnityEngine.UIElements
{
    public class UIDocument : MonoBehaviour
    {
        public VisualElement rootVisualElement { get; set; }
    }

    public enum LengthUnit { Pixel, Percent }

    public struct Length
    {
        public float value;
        public LengthUnit unit;
        public Length(float value, LengthUnit unit = LengthUnit.Pixel) { this.value = value; this.unit = unit; }
    }

    public class IStyle
    {
        public Length left { get; set; }
        public Length top { get; set; }
        public Length width { get; set; }
        public Length height { get; set; }
    }

    public class VisualElement
    {
        public string name { get; set; }
        private readonly List<string> classes = new List<string>();
        private readonly List<VisualElement> children = new List<VisualElement>();
        public IStyle style { get; set; }

        public VisualElement() { style = new IStyle(); }

        public int childCount { get { return children.Count; } }

        public void Add(VisualElement child) { if (child != null) children.Add(child); }
        public void Clear() { children.Clear(); }
        public List<VisualElement> Children() { return new List<VisualElement>(children); }
        public void AddToClassList(string className)
        {
            if (!classes.Contains(className)) classes.Add(className);
        }
        public void RemoveFromClassList(string className)
        {
            classes.Remove(className);
        }
        public bool ClassListContains(string className)
        {
            return classes.Contains(className);
        }

        public T Q<T>(string elementName = null) where T : VisualElement
        {
            foreach (var child in children)
            {
                if (elementName == null || child.name == elementName)
                {
                    var casted = child as T;
                    if (casted != null) return casted;
                }
                var nested = child.Q<T>(elementName);
                if (nested != null) return nested;
            }
            return null;
        }
    }

    public class Label : VisualElement
    {
        public string text { get; set; }
        public Label(string text = "") { this.text = text; }
    }

    public class Button : VisualElement
    {
        public event Action clicked;
        public string text { get; set; }
        public void SimulateClick() { if (clicked != null) clicked(); }
    }

    public class TextField : VisualElement
    {
        public string value { get; set; }
        public string label { get; set; }
    }

    public class DropdownField : VisualElement
    {
        public List<string> choices { get; set; }
        public int index { get; set; }
        public string value { get; set; }
        public string label { get; set; }
        public event Action<ChangeEvent<string>> ValueChanged;

        public void RegisterValueChangedCallback(Action<ChangeEvent<string>> callback)
        {
            ValueChanged += callback;
        }
        public void UnregisterValueChangedCallback(Action<ChangeEvent<string>> callback)
        {
            ValueChanged -= callback;
        }
        public void SimulateChange(string newVal)
        {
            string old = value;
            value = newVal;
            if (choices != null) index = choices.IndexOf(newVal);
            if (ValueChanged != null)
                ValueChanged(new ChangeEvent<string> { previousValue = old, newValue = newVal });
        }
    }

    public class Slider : VisualElement
    {
        public float lowValue { get; set; }
        public float highValue { get; set; }
        public float value { get; set; }
        public event Action<ChangeEvent<float>> ValueChanged;

        public void RegisterValueChangedCallback(Action<ChangeEvent<float>> callback)
        {
            ValueChanged += callback;
        }
        public void UnregisterValueChangedCallback(Action<ChangeEvent<float>> callback)
        {
            ValueChanged -= callback;
        }
        public void SimulateChange(float newVal)
        {
            float old = value;
            value = newVal;
            if (ValueChanged != null)
                ValueChanged(new ChangeEvent<float> { previousValue = old, newValue = newVal });
        }
    }

    public class ChangeEvent<T>
    {
        public T previousValue { get; set; }
        public T newValue { get; set; }
    }
}
#endif

namespace TrishulSim.UI
{
    using UnityEngine;
    using UnityEngine.UIElements;

    public static class UiShellSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAll();
                Console.WriteLine("[PASS] All UX-P0-A UITK Shell tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }
#endif

        public static void RunAll()
        {
            TestUxmlFilesAndElementIntegrity();
            TestUssStylesheetsAndMilStd2525Palette();
            TestC2StationControllerLifecycleAndEvents();
            TestCockpitControllerLifecycleAndEvents();
            TestResolutionScalability1920x1080();
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

        public static void TestUxmlFilesAndElementIntegrity()
        {
            string root = FindRepoRoot();
            string c2UxmlPath = Path.Combine(root, "Assets", "UI", "InstructorC2.uxml");
            string cockpitUxmlPath = Path.Combine(root, "Assets", "UI", "TraineeCockpit.uxml");

            Assert(File.Exists(c2UxmlPath), "Assets/UI/InstructorC2.uxml must exist");
            Assert(File.Exists(cockpitUxmlPath), "Assets/UI/TraineeCockpit.uxml must exist");

            // Validate XML structure for InstructorC2
            XmlDocument c2Xml = new XmlDocument();
            c2Xml.Load(c2UxmlPath);
            Assert(c2Xml.DocumentElement != null, "InstructorC2.uxml must be valid XML");
            Assert(c2Xml.DocumentElement.Name.EndsWith("UXML"), "InstructorC2.uxml root must be UXML");

            string c2Content = File.ReadAllText(c2UxmlPath);
            Assert(c2Content.Contains("name=\"app-title\""), "InstructorC2 must contain app-title");
            Assert(c2Content.Contains("name=\"link-status-icon\""), "InstructorC2 must contain link-status-icon");
            Assert(c2Content.Contains("name=\"link-status-label\""), "InstructorC2 must contain link-status-label");
            Assert(c2Content.Contains("name=\"scenario-dropdown\""), "InstructorC2 must contain scenario-dropdown");
            Assert(c2Content.Contains("name=\"trainee-id\""), "InstructorC2 must contain trainee-id");
            Assert(c2Content.Contains("name=\"launch-btn\""), "InstructorC2 must contain launch-btn");
            Assert(c2Content.Contains("name=\"abort-btn\""), "InstructorC2 must contain abort-btn");
            Assert(c2Content.Contains("name=\"lang-toggle\""), "InstructorC2 must contain lang-toggle");
            Assert(c2Content.Contains("name=\"scorecard-mirror\""), "InstructorC2 must contain scorecard-mirror");
            Assert(c2Content.Contains("name=\"scorecard-stub-label\""), "InstructorC2 must contain scorecard-stub-label");

            // Validate XML structure for TraineeCockpit
            XmlDocument cockpitXml = new XmlDocument();
            cockpitXml.Load(cockpitUxmlPath);
            Assert(cockpitXml.DocumentElement != null, "TraineeCockpit.uxml must be valid XML");

            string cockpitContent = File.ReadAllText(cockpitUxmlPath);
            Assert(cockpitContent.Contains("name=\"threat-indicator\""), "TraineeCockpit must contain threat-indicator");
            Assert(cockpitContent.Contains("name=\"threat-label\""), "TraineeCockpit must contain threat-label");
            Assert(cockpitContent.Contains("name=\"rf-waterfall-pane\""), "TraineeCockpit must contain rf-waterfall-pane");
            Assert(cockpitContent.Contains("name=\"rf-content\""), "TraineeCockpit must contain rf-content");
            Assert(cockpitContent.Contains("name=\"doppler-pane\""), "TraineeCockpit must contain doppler-pane");
            Assert(cockpitContent.Contains("name=\"doppler-content\""), "TraineeCockpit must contain doppler-content");
            Assert(cockpitContent.Contains("name=\"doppler-reading\""), "TraineeCockpit must contain doppler-reading");
            Assert(cockpitContent.Contains("name=\"acoustic-pane\""), "TraineeCockpit must contain acoustic-pane");
            Assert(cockpitContent.Contains("name=\"acoustic-content\""), "TraineeCockpit must contain acoustic-content");
            Assert(cockpitContent.Contains("name=\"acoustic-reading\""), "TraineeCockpit must contain acoustic-reading");
            Assert(cockpitContent.Contains("name=\"controls-row\""), "TraineeCockpit must contain controls-row");
            Assert(cockpitContent.Contains("name=\"scorecard-panel\""), "TraineeCockpit must contain scorecard-panel");
            Assert(cockpitContent.Contains("name=\"computing-overlay\""), "TraineeCockpit must contain computing-overlay");

            // Verify RF frequency markers
            Assert(cockpitContent.Contains("915 MHz"), "TraineeCockpit must have 915 MHz marker");
            Assert(cockpitContent.Contains("2.4 GHz"), "TraineeCockpit must have 2.4 GHz marker");
            Assert(cockpitContent.Contains("5.8 GHz"), "TraineeCockpit must have 5.8 GHz marker");
        }

        public static void TestUssStylesheetsAndMilStd2525Palette()
        {
            string root = FindRepoRoot();
            string c2UssPath = Path.Combine(root, "Assets", "UI", "InstructorC2.uss");
            string cockpitUssPath = Path.Combine(root, "Assets", "UI", "TraineeCockpit.uss");

            Assert(File.Exists(c2UssPath), "Assets/UI/InstructorC2.uss must exist");
            Assert(File.Exists(cockpitUssPath), "Assets/UI/TraineeCockpit.uss must exist");

            string c2Uss = File.ReadAllText(c2UssPath);
            string cockpitUss = File.ReadAllText(cockpitUssPath);

            // MIL-STD-2525 Threat State Hex Colors: GREEN #00B300, AMBER #FFB300, RED #CC0000
            Assert(c2Uss.Contains("#00B300"), "InstructorC2.uss must define MIL-STD-2525 GREEN #00B300");
            Assert(c2Uss.Contains("#FFB300"), "InstructorC2.uss must define MIL-STD-2525 AMBER #FFB300");
            Assert(c2Uss.Contains("#CC0000"), "InstructorC2.uss must define MIL-STD-2525 RED #CC0000");

            Assert(cockpitUss.Contains("#00B300"), "TraineeCockpit.uss must define MIL-STD-2525 GREEN #00B300");
            Assert(cockpitUss.Contains("#FFB300"), "TraineeCockpit.uss must define MIL-STD-2525 AMBER #FFB300");
            Assert(cockpitUss.Contains("#CC0000"), "TraineeCockpit.uss must define MIL-STD-2525 RED #CC0000");

            // Dark theme backgrounds
            Assert(c2Uss.Contains("#0D0D0D"), "InstructorC2.uss must use #0D0D0D dark background");
            Assert(cockpitUss.Contains("#0D0D0D"), "TraineeCockpit.uss must use #0D0D0D dark background");

            // Display none for hidden utility
            Assert(c2Uss.Contains("display: none"), "InstructorC2.uss must contain .hidden display: none rule");
            Assert(cockpitUss.Contains("display: none"), "TraineeCockpit.uss must contain .hidden display: none rule");
        }

        public static void TestC2StationControllerLifecycleAndEvents()
        {
            var controller = new C2StationController();

            // Construct mock hierarchy matching InstructorC2.uxml
            var mockRoot = new VisualElement { name = "c2-root" };
            var appTitle = new Label { name = "app-title", text = "TRISHUL-SIM · C2 STATION" };
            var statusIcon = new VisualElement { name = "link-status-icon" };
            statusIcon.AddToClassList("status-waiting");
            var statusLabel = new Label { name = "link-status-label", text = "○ Waiting for Trainee..." };
            var dropdown = new DropdownField { name = "scenario-dropdown" };
            var traineeId = new TextField { name = "trainee-id", value = "TRAINEE-01" };
            var launchBtn = new Button { name = "launch-btn", text = "LAUNCH SCENARIO" };
            var abortBtn = new Button { name = "abort-btn", text = "ABORT" };
            var langToggle = new Button { name = "lang-toggle", text = "हिंदी" };
            var scorecardMirror = new VisualElement { name = "scorecard-mirror" };
            scorecardMirror.AddToClassList("hidden");
            var scorecardStub = new Label { name = "scorecard-stub-label", text = "Awaiting data" };

            mockRoot.Add(appTitle);
            mockRoot.Add(statusIcon);
            mockRoot.Add(statusLabel);
            mockRoot.Add(dropdown);
            mockRoot.Add(traineeId);
            mockRoot.Add(launchBtn);
            mockRoot.Add(abortBtn);
            mockRoot.Add(langToggle);
            mockRoot.Add(scorecardMirror);
            mockRoot.Add(scorecardStub);

            // Bind elements
            controller.BindElements(mockRoot);
            controller.PopulateScenarioDropdown();
            controller.RegisterCallbacks();

            // Verify scenario dropdown choices
            Assert(dropdown.choices != null && dropdown.choices.Count == 4, "Dropdown must contain 4 scenarios");
            Assert(dropdown.choices[0] == "Punjab Border — Zero Visibility Pincer", "First scenario must be Punjab Border");
            Assert(controller.SelectedScenarioId == "punjab-fog-pincer-01", "Initial selected scenario must be punjab-fog-pincer-01");

            // Verify connection event
            Assert(!controller.IsConnected, "Initially not connected");
            controller.OnClientConnected();
            Assert(controller.IsConnected, "Must be connected after OnClientConnected");
            Assert(statusIcon.ClassListContains("status-connected"), "Status icon must have status-connected class");
            Assert(!statusIcon.ClassListContains("status-waiting"), "Status icon must not have status-waiting class");
            Assert(statusLabel.text.Contains("LAN LINK CONFIRMED"), "Status label must confirm LAN link");

            // Verify scenario change
            dropdown.SimulateChange("Rajasthan Desert FOB — Saturation Dive");
            Assert(controller.SelectedScenarioId == "rajasthan-desert-fob-01", "Selected scenario ID must update on dropdown change");

            // Verify disconnect event
            controller.OnClientDisconnected();
            Assert(!controller.IsConnected, "Must not be connected after OnClientDisconnected");
            Assert(statusIcon.ClassListContains("status-lost"), "Status icon must have status-lost class");
            Assert(statusLabel.text.Contains("LINK LOST"), "Status label must indicate link lost");

            // Verify scorecard mirror
            controller.OnScorecardReceived("sess-test-20261005");
            Assert(!scorecardMirror.ClassListContains("hidden"), "Scorecard mirror must unhide on SCORECARD_PAYLOAD");
            Assert(scorecardStub.text.Contains("sess-test-20261005"), "Scorecard stub must display sessionId");

            // Verify defensive cleanup
            controller.UnregisterCallbacks();
        }

        public static void TestCockpitControllerLifecycleAndEvents()
        {
            var controller = new CockpitController();

            // Construct mock hierarchy matching TraineeCockpit.uxml
            var mockRoot = new VisualElement { name = "cockpit-root" };
            var threatIndicator = new VisualElement { name = "threat-indicator" };
            threatIndicator.AddToClassList("threat-green");
            var threatLabel = new Label { name = "threat-label", text = "GREEN" };

            var rfWaterfallPane = new VisualElement { name = "rf-waterfall-pane" };
            var rfContent = new VisualElement { name = "rf-content" };
            var dopplerPane = new VisualElement { name = "doppler-pane" };
            var dopplerContent = new VisualElement { name = "doppler-content" };
            var dopplerReading = new Label { name = "doppler-reading", text = "Doppler: --- Hz" };
            var acousticPane = new VisualElement { name = "acoustic-pane" };
            var acousticContent = new VisualElement { name = "acoustic-content" };
            var acousticReading = new Label { name = "acoustic-reading", text = "SPL: --- dB" };

            var controlsRow = new VisualElement { name = "controls-row" };
            controlsRow.AddToClassList("hidden");
            var scorecardPanel = new VisualElement { name = "scorecard-panel" };
            scorecardPanel.AddToClassList("hidden");
            var scorecardStubLabel = new Label { name = "scorecard-stub-label", text = "Scorecard received" };
            var computingOverlay = new VisualElement { name = "computing-overlay" };
            computingOverlay.AddToClassList("hidden");

            mockRoot.Add(threatIndicator);
            mockRoot.Add(threatLabel);
            mockRoot.Add(rfWaterfallPane);
            mockRoot.Add(rfContent);
            mockRoot.Add(dopplerPane);
            mockRoot.Add(dopplerContent);
            mockRoot.Add(dopplerReading);
            mockRoot.Add(acousticPane);
            mockRoot.Add(acousticContent);
            mockRoot.Add(acousticReading);
            mockRoot.Add(controlsRow);
            mockRoot.Add(scorecardPanel);
            mockRoot.Add(scorecardStubLabel);
            mockRoot.Add(computingOverlay);

            // Bind elements
            controller.BindElements(mockRoot);

            // Test SCENARIO_START
            controller.OnScenarioStart("punjab-fog-pincer-01", "Punjab Border — Zero Visibility Pincer");
            Assert(controller.ScenarioRunning, "Scenario must be running after OnScenarioStart");
            Assert(!controlsRow.ClassListContains("hidden"), "Controls row must unhide on SCENARIO_START");
            Assert(controller.CurrentThreatState == "GREEN", "Initial threat state must be GREEN");
            Assert(threatIndicator.ClassListContains("threat-green"), "Threat indicator must have threat-green class");
            Assert(threatLabel.text == "GREEN", "Threat label must display GREEN");

            // Test SCENARIO_TICK threat state transition to AMBER
            controller.OnScenarioTick(
                threatState: "AMBER",
                rfPeakGHz: 5.825f,
                rfPeakDbm: -62.3f,
                dopplerHz: 180.5f,
                acousticDb: 48.0f,
                radarCount: 2
            );
            Assert(controller.CurrentThreatState == "AMBER", "Threat state must transition to AMBER");
            Assert(threatIndicator.ClassListContains("threat-amber"), "Threat indicator must have threat-amber class");
            Assert(!threatIndicator.ClassListContains("threat-green"), "Threat indicator must remove threat-green class");
            Assert(threatLabel.text == "AMBER", "Threat label must display AMBER");
            Assert(dopplerReading.text == "Doppler: 180.5 Hz", "Doppler reading must format correctly");
            Assert(acousticReading.text == "SPL: 48.0 dB", "Acoustic reading must format correctly");

            // Test SCENARIO_TICK threat state transition to RED
            controller.OnScenarioTick(
                threatState: "RED",
                rfPeakGHz: 5.825f,
                rfPeakDbm: -45.0f,
                dopplerHz: 220.4f,
                acousticDb: 65.5f,
                radarCount: 5
            );
            Assert(controller.CurrentThreatState == "RED", "Threat state must transition to RED");
            Assert(threatIndicator.ClassListContains("threat-red"), "Threat indicator must have threat-red class");
            Assert(!threatIndicator.ClassListContains("threat-amber"), "Threat indicator must remove threat-amber class");
            Assert(threatLabel.text == "RED", "Threat label must display RED");

            // Test SCENARIO_END
            controller.OnScenarioEnd("ALL_NEUTRALISED");
            Assert(!controller.ScenarioRunning, "ScenarioRunning must be false after OnScenarioEnd");
            Assert(!computingOverlay.ClassListContains("hidden"), "Computing overlay must unhide on SCENARIO_END");

            // Test SCORECARD_PAYLOAD
            controller.OnScorecardReceived("sess-test-20261005");
            Assert(computingOverlay.ClassListContains("hidden"), "Computing overlay must hide on SCORECARD_PAYLOAD");
            Assert(!scorecardPanel.ClassListContains("hidden"), "Scorecard panel must unhide on SCORECARD_PAYLOAD");
            Assert(scorecardStubLabel.text.Contains("sess-test-20261005"), "Scorecard label must contain sessionId");
        }

        public static void TestResolutionScalability1920x1080()
        {
            string root = FindRepoRoot();
            string c2Uss = File.ReadAllText(Path.Combine(root, "Assets", "UI", "InstructorC2.uss"));
            string cockpitUss = File.ReadAllText(Path.Combine(root, "Assets", "UI", "TraineeCockpit.uss"));

            // Check root element flex-grow
            Assert(c2Uss.Contains("flex-grow: 1"), "InstructorC2 root must have flex-grow: 1 for full viewport scaling");
            Assert(cockpitUss.Contains("flex-grow: 1"), "TraineeCockpit root must have flex-grow: 1 for full viewport scaling");

            // Check sensor row layout
            Assert(cockpitUss.Contains("flex-direction: row"), "TraineeCockpit sensor row must use horizontal flex-direction");
            Assert(cockpitUss.Contains("flex-basis: 0"), "TraineeCockpit sensor panes must distribute space evenly (flex-basis: 0)");

            // Verify no fixed absolute pixel widths on root containers that would cause clipping at 1920x1080
            Assert(!Regex.IsMatch(c2Uss, @"\.c2-root\s*\{[^}]*width:\s*\d+px"), "c2-root must not have fixed width in px");
            Assert(!Regex.IsMatch(cockpitUss, @"\.cockpit-root\s*\{[^}]*width:\s*\d+px"), "cockpit-root must not have fixed width in px");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message);
        }
    }
}
