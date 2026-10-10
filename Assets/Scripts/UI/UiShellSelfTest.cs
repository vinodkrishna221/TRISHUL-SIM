// ============================================================
// UiShellSelfTest.cs — Runnable Self-Test for UX-P0-A & UX-P1-A
// TRISHUL-SIM · ui-ux track (Rani)
//
// Validates:
// 1. InstructorC2.uxml & TraineeCockpit.uxml schema and elements
// 2. InstructorC2.uss & TraineeCockpit.uss MIL-STD-2525 colors & layout
// 3. C2StationController lifecycle, mock connection, scenario dropdown
// 4. CockpitController lifecycle, threat indicator cycling
// 5. RfWaterfallRenderer: 256x128 spectrogram, false-color dBm mapping, scrolling
// 6. DopplerScopeRenderer: 200x200 circular PPI radar, range rings, 6 swarm blips
// 7. AcousticFftRenderer: 8-band audio spectrum, blade-tip 220 Hz spike, amber/red thresholds
// 8. End-to-end CockpitController sensor suite integration on SCENARIO_TICK
// 9. 1920x1080 resolution scalability and responsive flexbox rules
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
    public struct Vector2
    {
        public float x;
        public float y;
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y); } }
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y + z * z); } }
        public Vector3 normalized { get { float mag = magnitude; return mag > 1e-5f ? new Vector3(x / mag, y / mag, z / mag) : new Vector3(0, 0, 0); } }
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator *(Vector3 a, float d) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static Vector3 operator *(float d, Vector3 a) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static float Dot(Vector3 lhs, Vector3 rhs) { return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z; }
    }

    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float r, float g, float b, float a = 1.0f)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static Color black { get { return new Color(0, 0, 0, 1); } }
        public static Color white { get { return new Color(1, 1, 1, 1); } }
        public static Color red { get { return new Color(1, 0, 0, 1); } }
        public static Color green { get { return new Color(0, 1, 0, 1); } }
        public static Color cyan { get { return new Color(0, 1, 1, 1); } }
        public static Color yellow { get { return new Color(1, 1, 0, 1); } }
        public static Color clear { get { return new Color(0, 0, 0, 0); } }

        public static Color Lerp(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(
                a.r + (b.r - a.r) * t,
                a.g + (b.g - a.g) * t,
                a.b + (b.b - a.b) * t,
                a.a + (b.a - a.a) * t
            );
        }

        public static bool operator ==(Color a, Color b)
        {
            return Math.Abs(a.r - b.r) < 0.005f &&
                   Math.Abs(a.g - b.g) < 0.005f &&
                   Math.Abs(a.b - b.b) < 0.005f &&
                   Math.Abs(a.a - b.a) < 0.005f;
        }

        public static bool operator !=(Color a, Color b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Color)) return false;
            return this == (Color)obj;
        }

        public override int GetHashCode()
        {
            return r.GetHashCode() ^ g.GetHashCode() ^ b.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("RGBA({0:F2},{1:F2},{2:F2},{3:F2})", r, g, b, a);
        }
    }

    public enum TextureFormat
    {
        RGB24,
        RGBA32
    }

    public class Texture2D
    {
        public int width { get; private set; }
        public int height { get; private set; }
        private Color[] pixelBuffer;

        public Texture2D(int width, int height, TextureFormat format = TextureFormat.RGBA32, bool mipChain = false)
        {
            this.width = width;
            this.height = height;
            pixelBuffer = new Color[width * height];
        }

        public void SetPixels(Color[] colors)
        {
            Array.Copy(colors, pixelBuffer, Math.Min(colors.Length, pixelBuffer.Length));
        }

        public void SetPixels(int x, int y, int blockWidth, int blockHeight, Color[] colors)
        {
            for (int r = 0; r < blockHeight; r++)
            {
                int targetIndex = (y + r) * width + x;
                int sourceIndex = r * blockWidth;
                int count = Math.Min(blockWidth, width - x);
                if (count > 0 && targetIndex >= 0 && (targetIndex + count) <= pixelBuffer.Length)
                {
                    Array.Copy(colors, sourceIndex, pixelBuffer, targetIndex, count);
                }
            }
        }

        public Color[] GetPixels(int x, int y, int blockWidth, int blockHeight)
        {
            Color[] result = new Color[blockWidth * blockHeight];
            for (int r = 0; r < blockHeight; r++)
            {
                int srcIndex = (y + r) * width + x;
                int dstIndex = r * blockWidth;
                int count = Math.Min(blockWidth, width - x);
                if (count > 0 && srcIndex >= 0 && (srcIndex + count) <= pixelBuffer.Length)
                {
                    Array.Copy(pixelBuffer, srcIndex, result, dstIndex, count);
                }
            }
            return result;
        }

        public void SetPixel(int x, int y, Color color)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                pixelBuffer[y * width + x] = color;
            }
        }

        public Color GetPixel(int x, int y)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                return pixelBuffer[y * width + x];
            }
            return Color.black;
        }

        public void Apply() { }
    }

    public static class Mathf
    {
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float Rad2Deg = (float)(180.0 / Math.PI);

        public static float Clamp01(float val)
        {
            if (val < 0f) return 0f;
            if (val > 1f) return 1f;
            return val;
        }

        public static float Clamp(float val, float min, float max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        public static int Clamp(int val, int min, int max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        public static int RoundToInt(float val)
        {
            return (int)Math.Round(val);
        }

        public static float Sqrt(float val)
        {
            return (float)Math.Sqrt(val);
        }

        public static float Abs(float val)
        {
            return Math.Abs(val);
        }

        public static float Sin(float rad)
        {
            return (float)Math.Sin(rad);
        }

        public static float Cos(float rad)
        {
            return (float)Math.Cos(rad);
        }

        public static float Atan2(float y, float x)
        {
            return (float)Math.Atan2(y, x);
        }

        public static float Exp(float power)
        {
            return (float)Math.Exp(power);
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Clamp01(t);
        }

        public static float Min(float a, float b)
        {
            return Math.Min(a, b);
        }

        public static float Max(float a, float b)
        {
            return Math.Max(a, b);
        }

        public static int Min(int a, int b)
        {
            return Math.Min(a, b);
        }

        public static int Max(int a, int b)
        {
            return Math.Max(a, b);
        }

        public static float DeltaAngle(float current, float target)
        {
            float num = Mathf.Repeat(target - current, 360f);
            if (num > 180f) num -= 360f;
            return num;
        }

        public static float Repeat(float t, float length)
        {
            return (float)(t - Math.Floor(t / length) * length);
        }

        public static float Asin(float val)
        {
            return (float)Math.Asin(Clamp(val, -1f, 1f));
        }

        public static float Dot(Vector3 lhs, Vector3 rhs)
        {
            return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;
        }
    }

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

    public static class GUIUtility
    {
        public static string systemCopyBuffer { get; set; }
    }

    public enum KeyCode { T }

    public static class Input
    {
        public static bool GetKeyDown(KeyCode key) { return false; }
    }
}

namespace UnityEngine.UIElements
{
    public class UIDocument : MonoBehaviour
    {
        public VisualElement rootVisualElement { get; set; }
    }

    public enum LengthUnit { Pixel, Percent }
    public enum FlexDirection { Column, ColumnReverse, Row, RowReverse }
    public enum Justify { FlexStart, Center, FlexEnd, SpaceBetween, SpaceAround }
    public enum Align { Auto, FlexStart, Center, FlexEnd, Stretch }

    public struct Length
    {
        public float value;
        public LengthUnit unit;
        public bool isPercent;

        public Length(float value, LengthUnit unit = LengthUnit.Pixel)
        {
            this.value = value;
            this.unit = unit;
            this.isPercent = (unit == LengthUnit.Percent);
        }

        public static Length Percent(float val)
        {
            return new Length(val, LengthUnit.Percent);
        }

        public static implicit operator Length(float val)
        {
            return new Length(val, LengthUnit.Pixel);
        }

        public static implicit operator Length(int val)
        {
            return new Length(val, LengthUnit.Pixel);
        }
    }

    public class StyleBackground
    {
        public Texture2D texture { get; set; }
        public StyleBackground(Texture2D tex) { this.texture = tex; }
    }

    public class IStyle
    {
        public Length left { get; set; }
        public Length top { get; set; }
        public Length width { get; set; }
        public Length height { get; set; }
        public StyleBackground backgroundImage { get; set; }
        public FlexDirection flexDirection { get; set; }
        public Justify justifyContent { get; set; }
        public Align alignItems { get; set; }
        public Length flexBasis { get; set; }
        public float flexGrow { get; set; }
        public Length paddingLeft { get; set; }
        public Length paddingRight { get; set; }
        public Length paddingTop { get; set; }
        public Length paddingBottom { get; set; }
        public Length marginRight { get; set; }
        public Color backgroundColor { get; set; }
        public Length borderTopLeftRadius { get; set; }
        public Length borderTopRightRadius { get; set; }
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

        public DropdownField()
        {
            choices = new List<string>();
        }

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

    public class SliderInt : VisualElement
    {
        public int lowValue { get; set; }
        public int highValue { get; set; }
        public int value { get; set; }
        public string label { get; set; }
        public event Action<ChangeEvent<int>> ValueChanged;

        public void RegisterValueChangedCallback(Action<ChangeEvent<int>> callback)
        {
            ValueChanged += callback;
        }
        public void UnregisterValueChangedCallback(Action<ChangeEvent<int>> callback)
        {
            ValueChanged -= callback;
        }
        public void SimulateChange(int newVal)
        {
            int old = value;
            value = newVal;
            if (ValueChanged != null)
                ValueChanged(new ChangeEvent<int> { previousValue = old, newValue = newVal });
        }
    }

    public class Toggle : VisualElement
    {
        public bool value { get; set; }
        public string label { get; set; }
        public event Action<ChangeEvent<bool>> ValueChanged;

        public void RegisterValueChangedCallback(Action<ChangeEvent<bool>> callback)
        {
            ValueChanged += callback;
        }
        public void UnregisterValueChangedCallback(Action<ChangeEvent<bool>> callback)
        {
            ValueChanged -= callback;
        }
        public void SimulateChange(bool newVal)
        {
            bool old = value;
            value = newVal;
            if (ValueChanged != null)
                ValueChanged(new ChangeEvent<bool> { previousValue = old, newValue = newVal });
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
                Console.WriteLine("[PASS] All UX-P0-A, UX-P1-A & UX-P1-B tests passed successfully!");
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
            TestRfWaterfallRenderer();
            TestDopplerScopeRenderer();
            TestAcousticFftRenderer();
            TestCockpitControllerSensorIntegration();
            TestEwJammerController();
            TestSmashReticleController();
            TestCockpitControllerEwAndSmashIntegration();
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

            // EW Jammer & SMASH 2000 UXML Elements (UX-P1-B)
            Assert(cockpitContent.Contains("name=\"ew-panel\""), "TraineeCockpit must contain ew-panel");
            Assert(cockpitContent.Contains("name=\"ew-azimuth\""), "TraineeCockpit must contain ew-azimuth");
            Assert(cockpitContent.Contains("name=\"ew-elevation\""), "TraineeCockpit must contain ew-elevation");
            Assert(cockpitContent.Contains("name=\"ew-band\""), "TraineeCockpit must contain ew-band");
            Assert(cockpitContent.Contains("name=\"ew-engage\""), "TraineeCockpit must contain ew-engage");
            Assert(cockpitContent.Contains("name=\"ew-roe-warning\""), "TraineeCockpit must contain ew-roe-warning");

            Assert(cockpitContent.Contains("name=\"smash-panel\""), "TraineeCockpit must contain smash-panel");
            Assert(cockpitContent.Contains("name=\"smash-lock-label\""), "TraineeCockpit must contain smash-lock-label");
            Assert(cockpitContent.Contains("name=\"smash-lead-label\""), "TraineeCockpit must contain smash-lead-label");
            Assert(cockpitContent.Contains("name=\"fire-btn\""), "TraineeCockpit must contain fire-btn");
            Assert(cockpitContent.Contains("name=\"smash-feedback-label\""), "TraineeCockpit must contain smash-feedback-label");
            Assert(cockpitContent.Contains("name=\"smash-reticle-overlay\""), "TraineeCockpit must contain smash-reticle-overlay");
            Assert(cockpitContent.Contains("name=\"smash-crosshair\""), "TraineeCockpit must contain smash-crosshair");

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

            Assert(c2Uss.Contains("#00B300"), "InstructorC2.uss must define MIL-STD-2525 GREEN #00B300");
            Assert(c2Uss.Contains("#FFB300"), "InstructorC2.uss must define MIL-STD-2525 AMBER #FFB300");
            Assert(c2Uss.Contains("#CC0000"), "InstructorC2.uss must define MIL-STD-2525 RED #CC0000");

            Assert(cockpitUss.Contains("#00B300"), "TraineeCockpit.uss must define MIL-STD-2525 GREEN #00B300");
            Assert(cockpitUss.Contains("#FFB300"), "TraineeCockpit.uss must define MIL-STD-2525 AMBER #FFB300");
            Assert(cockpitUss.Contains("#CC0000"), "TraineeCockpit.uss must define MIL-STD-2525 RED #CC0000");

            Assert(c2Uss.Contains("#0D0D0D"), "InstructorC2.uss must use #0D0D0D dark background");
            Assert(cockpitUss.Contains("#0D0D0D"), "TraineeCockpit.uss must use #0D0D0D dark background");

            Assert(c2Uss.Contains("display: none"), "InstructorC2.uss must contain .hidden display: none rule");
            Assert(cockpitUss.Contains("display: none"), "TraineeCockpit.uss must contain .hidden display: none rule");

            // EW & SMASH USS Styles (UX-P1-B)
            Assert(cockpitUss.Contains(".roe-warning"), "TraineeCockpit.uss must style .roe-warning");
            Assert(cockpitUss.Contains(".fire-btn"), "TraineeCockpit.uss must style .fire-btn");
            Assert(cockpitUss.Contains(".smash-reticle-overlay"), "TraineeCockpit.uss must style .smash-reticle-overlay");
            Assert(cockpitUss.Contains(".reticle-ring"), "TraineeCockpit.uss must style .reticle-ring");
            Assert(cockpitUss.Contains(".feedback-hit"), "TraineeCockpit.uss must style .feedback-hit");
            Assert(cockpitUss.Contains(".feedback-miss"), "TraineeCockpit.uss must style .feedback-miss");
        }

        public static void TestC2StationControllerLifecycleAndEvents()
        {
            var controller = new C2StationController();

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

            controller.BindElements(mockRoot);
            controller.PopulateScenarioDropdown();
            controller.RegisterCallbacks();

            Assert(dropdown.choices != null && dropdown.choices.Count == 4, "Dropdown must contain 4 scenarios");
            Assert(dropdown.choices[0] == "Punjab Border — Zero Visibility Pincer", "First scenario must be Punjab Border");
            Assert(controller.SelectedScenarioId == "punjab-fog-pincer-01", "Initial selected scenario must be punjab-fog-pincer-01");

            Assert(!controller.IsConnected, "Initially not connected");
            controller.OnClientConnected();
            Assert(controller.IsConnected, "Must be connected after OnClientConnected");
            Assert(statusIcon.ClassListContains("status-connected"), "Status icon must have status-connected class");
            Assert(!statusIcon.ClassListContains("status-waiting"), "Status icon must not have status-waiting class");
            Assert(statusLabel.text.Contains("LAN LINK CONFIRMED"), "Status label must confirm LAN link");

            dropdown.SimulateChange("Rajasthan Desert FOB — Saturation Dive");
            Assert(controller.SelectedScenarioId == "rajasthan-desert-fob-01", "Selected scenario ID must update on dropdown change");

            controller.OnClientDisconnected();
            Assert(!controller.IsConnected, "Must not be connected after OnClientDisconnected");
            Assert(statusIcon.ClassListContains("status-lost"), "Status icon must have status-lost class");
            Assert(statusLabel.text.Contains("LINK LOST"), "Status label must indicate link lost");

            controller.OnScorecardReceived("sess-test-20261005");
            Assert(!scorecardMirror.ClassListContains("hidden"), "Scorecard mirror must unhide on SCORECARD_PAYLOAD");
            Assert(scorecardStub.text.Contains("sess-test-20261005"), "Scorecard stub must display sessionId");

            controller.UnregisterCallbacks();
        }

        public static void TestCockpitControllerLifecycleAndEvents()
        {
            var controller = new CockpitController();

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

            controller.BindElements(mockRoot);

            controller.OnScenarioStart("punjab-fog-pincer-01", "Punjab Border — Zero Visibility Pincer");
            Assert(controller.ScenarioRunning, "Scenario must be running after OnScenarioStart");
            Assert(!controlsRow.ClassListContains("hidden"), "Controls row must unhide on SCENARIO_START");
            Assert(controller.CurrentThreatState == "GREEN", "Initial threat state must be GREEN");
            Assert(threatIndicator.ClassListContains("threat-green"), "Threat indicator must have threat-green class");
            Assert(threatLabel.text == "GREEN", "Threat label must display GREEN");

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

            controller.OnScenarioEnd("ALL_NEUTRALISED");
            Assert(!controller.ScenarioRunning, "ScenarioRunning must be false after OnScenarioEnd");
            Assert(!computingOverlay.ClassListContains("hidden"), "Computing overlay must unhide on SCENARIO_END");

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

            Assert(c2Uss.Contains("flex-grow: 1"), "InstructorC2 root must have flex-grow: 1 for full viewport scaling");
            Assert(cockpitUss.Contains("flex-grow: 1"), "TraineeCockpit root must have flex-grow: 1 for full viewport scaling");

            Assert(cockpitUss.Contains("flex-direction: row"), "TraineeCockpit sensor row must use horizontal flex-direction");
            Assert(cockpitUss.Contains("flex-basis: 0"), "TraineeCockpit sensor panes must distribute space evenly (flex-basis: 0)");

            Assert(!Regex.IsMatch(c2Uss, @"\.c2-root\s*\{[^}]*width:\s*\d+px"), "c2-root must not have fixed width in px");
            Assert(!Regex.IsMatch(cockpitUss, @"\.cockpit-root\s*\{[^}]*width:\s*\d+px"), "cockpit-root must not have fixed width in px");
        }

        public static void TestRfWaterfallRenderer()
        {
            var renderer = new RfWaterfallRenderer();
            var container = new VisualElement { name = "rf-content" };

            renderer.Initialize(container);
            Assert(renderer.IsInitialized, "RfWaterfallRenderer must be initialized");
            Assert(renderer.Texture != null, "Texture must be allocated");
            Assert(renderer.Texture.width == 256, "Waterfall texture width must be 256");
            Assert(renderer.Texture.height == 128, "Waterfall texture height must be 128");

            // Test color mapping thresholds
            Color cNoise = RfWaterfallRenderer.MapDbmToColor(-90f);
            Assert(cNoise == Color.black, "-90 dBm must map to Color.black");

            Color cCyan = RfWaterfallRenderer.MapDbmToColor(-70f);
            Assert(cCyan == Color.cyan, "-70 dBm must map to Color.cyan");

            Color cYellow = RfWaterfallRenderer.MapDbmToColor(-50f);
            Assert(cYellow == Color.yellow, "-50 dBm must map to Color.yellow");

            Color cRed = RfWaterfallRenderer.MapDbmToColor(-30f);
            Assert(cRed == Color.red, "-30 dBm must map to Color.red");

            // Test incursion peak at 5.825 GHz
            var sr = new SensorReadings(5.825f, -40.0f, 220.4f, 54.2f, 6);
            renderer.UpdateWaterfall(sr);

            // 5.825 GHz out of 6.0 GHz maps to approx pixel X = 248
            int expectedX = Mathf.RoundToInt((5.825f / 6.0f) * 255);
            Color peakPixel = renderer.Texture.GetPixel(expectedX, 0);
            Assert(peakPixel.r > 0.5f, "Pixel at 5.825 GHz must show strong signal intensity (red/yellow)");

            // Test scrolling: update again and check that row 0 moves to row 1
            renderer.UpdateWaterfall(new SensorReadings(0f, -90f, 0f, 30f, 0));
            Color scrolledPixel = renderer.Texture.GetPixel(expectedX, 1);
            Assert(scrolledPixel.r > 0.5f, "Previous peak row must scroll up to Y = 1");
        }

        public static void TestDopplerScopeRenderer()
        {
            var renderer = new DopplerScopeRenderer();
            var container = new VisualElement { name = "doppler-content" };
            var label = new Label("Doppler: --- Hz");

            renderer.Initialize(container, label);
            Assert(renderer.IsInitialized, "DopplerScopeRenderer must be initialized");
            Assert(renderer.Texture != null, "Doppler texture must be allocated");
            Assert(renderer.Texture.width == 200, "Scope texture width must be 200");
            Assert(renderer.Texture.height == 200, "Scope texture height must be 200");

            // Check center graticule crosshair pixel is non-black
            Color centerColor = renderer.Texture.GetPixel(100, 100);
            Assert(centerColor != Color.black, "Scope center must have radar graticule or grid");

            // Update with 6-agent swarm
            var swarm = SensorMock.GetPunjabSwarmFixture();
            var sr = SensorMock.GetPunjabFixture();
            renderer.UpdateScope(sr, swarm);

            Assert(label.text == "Doppler: 220.4 Hz", "Doppler label must reflect 220.4 Hz");
        }

        public static void TestAcousticFftRenderer()
        {
            var renderer = new AcousticFftRenderer();
            var container = new VisualElement { name = "acoustic-content" };
            var label = new Label("SPL: --- dB");

            renderer.Initialize(container, label);
            Assert(renderer.IsInitialized, "AcousticFftRenderer must be initialized");
            Assert(renderer.BarElements != null, "BarElements array must not be null");
            Assert(renderer.BarElements.Length == 8, "Must generate exactly 8 FFT bar columns");

            // Test quiet baseline (< 40 dB)
            var quietSr = new SensorReadings(0f, -90f, 0f, 32.0f, 0);
            renderer.UpdateAcoustic(quietSr);
            Assert(label.text == "SPL: 32.0 dB", "Acoustic label must format SPL correctly");
            var bladeBar = renderer.BarElements[AcousticFftRenderer.BladeTipBandIndex];
            Assert(bladeBar.style.height.value <= 15.0f, "Quiet blade bar height must be <= 15% (noise floor)");

            // Test Amber threat level (40–55 dB)
            var amberSr = new SensorReadings(5.825f, -62.3f, 220.4f, 48.0f, 6);
            renderer.UpdateAcoustic(amberSr);
            Assert(label.text == "SPL: 48.0 dB", "Acoustic label must show 48.0 dB");
            Assert(bladeBar.style.height.value >= 25.0f && bladeBar.style.height.value <= 65.0f, "Amber bar height must be between 25% and 65%");
            Assert(bladeBar.style.backgroundColor.r == 1.0f && bladeBar.style.backgroundColor.g > 0.6f, "Amber bar must use #FFB300");

            // Test Red alert (> 55 dB)
            var redSr = new SensorReadings(5.825f, -40.0f, 220.4f, 65.0f, 6);
            renderer.UpdateAcoustic(redSr);
            Assert(label.text == "SPL: 65.0 dB", "Acoustic label must show 65.0 dB");
            Assert(bladeBar.style.height.value > 65.0f, "Red alert bar height must be > 65%");
            Assert(bladeBar.style.backgroundColor.r >= 0.75f && bladeBar.style.backgroundColor.g < 0.1f, "Red alert bar must use #CC0000");
        }

        public static void TestCockpitControllerSensorIntegration()
        {
            var controller = new CockpitController();
            var mockRoot = new VisualElement { name = "cockpit-root" };

            var threatIndicator = new VisualElement { name = "threat-indicator" };
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
            var scorecardPanel = new VisualElement { name = "scorecard-panel" };
            var scorecardStubLabel = new Label { name = "scorecard-stub-label", text = "Scorecard received" };
            var computingOverlay = new VisualElement { name = "computing-overlay" };

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

            controller.BindElements(mockRoot);

            Assert(controller.RfRenderer != null && controller.RfRenderer.IsInitialized, "RfRenderer must be initialized");
            Assert(controller.DopplerRenderer != null && controller.DopplerRenderer.IsInitialized, "DopplerRenderer must be initialized");
            Assert(controller.AcousticRenderer != null && controller.AcousticRenderer.IsInitialized, "AcousticRenderer must be initialized");

            controller.OnScenarioStart("punjab-fog-pincer-01", "Punjab Border — Zero Visibility Pincer");
            Assert(controller.ScenarioRunning, "Scenario must be running");

            // Tick with Punjab incursion telemetry
            var punjabReadings = SensorMock.GetPunjabFixture();
            var punjabSwarm = SensorMock.GetPunjabSwarmFixture();
            controller.OnScenarioTick("RED", punjabReadings, punjabSwarm);

            Assert(controller.CurrentThreatState == "RED", "Threat state must be RED");
            Assert(dopplerReading.text == "Doppler: 220.4 Hz", "Doppler label must update");
            Assert(acousticReading.text == "SPL: 54.2 dB", "Acoustic label must update");
        }

        public static void TestEwJammerController()
        {
            var controller = new EwJammerController();
            var mockContainer = new VisualElement { name = "controls-row" };

            var ewPanel = new VisualElement { name = "ew-panel" };
            var sliderAzimuth = new SliderInt { name = "ew-azimuth", lowValue = 0, highValue = 359, value = 0 };
            var sliderElevation = new SliderInt { name = "ew-elevation", lowValue = -90, highValue = 90, value = 0 };
            var dropdownBand = new DropdownField { name = "ew-band" };
            var toggleEngage = new Toggle { name = "ew-engage", value = false };
            var labelRoeWarning = new Label { name = "ew-roe-warning", text = "⚠ ROE VIOLATION — FRIENDLY BLACKOUT" };
            labelRoeWarning.AddToClassList("hidden");

            ewPanel.Add(sliderAzimuth);
            ewPanel.Add(sliderElevation);
            ewPanel.Add(dropdownBand);
            ewPanel.Add(toggleEngage);
            ewPanel.Add(labelRoeWarning);
            mockContainer.Add(ewPanel);

            controller.Initialize(mockContainer);
            Assert(controller.IsInitialized, "EwJammerController must be initialized");
            Assert(controller.SliderAzimuth != null, "SliderAzimuth must be bound");
            Assert(controller.DropdownBand != null, "DropdownBand must be bound");
            Assert(controller.DropdownBand.choices.Count == 3, "Must have 3 jam frequency bands");

            // Test setting Azimuth & Elevation
            controller.SetAzimuth(90);
            Assert(controller.AzimuthDeg == 90, "Azimuth must be 90°");
            Assert(sliderAzimuth.value == 90, "SliderAzimuth value must update");

            controller.SetElevation(-5);
            Assert(controller.ElevationDeg == -5, "Elevation must be -5°");
            Assert(sliderElevation.value == -5, "SliderElevation value must update");

            // Test ROE Violation on 90° azimuth + 0.915 GHz band
            controller.SetBand("0.915 GHz");
            Assert(controller.FriendlyBlackoutRisk, "Azimuth 90° + Band 0.915 GHz must trigger ROE blackout risk");
            Assert(!labelRoeWarning.ClassListContains("hidden"), "ROE warning label must be visible (hidden class removed)");

            // Test clearing ROE Warning on 247° attack axis + 5.825 GHz band
            controller.SetAzimuth(247);
            controller.SetBand("5.825 GHz");
            Assert(!controller.FriendlyBlackoutRisk, "Azimuth 247° + Band 5.825 GHz must clear ROE blackout risk");
            Assert(labelRoeWarning.ClassListContains("hidden"), "ROE warning label must be hidden");

            // Test Engage Toggle and BuildTraineeAction
            controller.SetEngaged(true);
            Assert(controller.IsEngaged, "EW must be engaged");
            var action = controller.BuildTraineeAction();
            Assert(action.antennaBearingDeg == 247f, "Action antennaBearingDeg must be 247");
            Assert(action.antennaElevationDeg == -5f, "Action antennaElevationDeg must be -5");
            Assert(action.ewEngaged == true, "Action ewEngaged must be true");
            Assert(action.ewBandGHz.HasValue && Math.Abs(action.ewBandGHz.Value - 5.825f) < 0.05f, "Action ewBandGHz must be 5.825");
        }

        public static void TestSmashReticleController()
        {
            var controller = new SmashReticleController();
            var mockContainer = new VisualElement { name = "controls-row" };
            var mockOverlay = new VisualElement { name = "smash-reticle-overlay" };

            var smashPanel = new VisualElement { name = "smash-panel" };
            var lockLabel = new Label { name = "smash-lock-label", text = "STANDBY" };
            var leadLabel = new Label { name = "smash-lead-label", text = "Lead: ---°" };
            var fireBtn = new Button { name = "fire-btn", text = "FIRE — 7.62mm" };
            var feedbackLabel = new Label { name = "smash-feedback-label", text = "" };
            feedbackLabel.AddToClassList("hidden");

            smashPanel.Add(lockLabel);
            smashPanel.Add(leadLabel);
            smashPanel.Add(fireBtn);
            smashPanel.Add(feedbackLabel);
            mockContainer.Add(smashPanel);

            var crosshair = new VisualElement { name = "smash-crosshair" };
            var leadVector = new VisualElement { name = "smash-lead-vector" };
            var targetTag = new Label { name = "smash-target-tag", text = "" };
            mockOverlay.Add(crosshair);
            mockOverlay.Add(leadVector);
            mockOverlay.Add(targetTag);

            controller.Initialize(mockContainer, mockOverlay);
            Assert(controller.IsInitialized, "SmashReticleController must be initialized");

            var swarm = SensorMock.GetPunjabSwarmFixture();

            // Test target lock with antenna bearing aimed at attack axis (247°)
            var actionLocked = new TraineeAction(247.0f, -5.0f, true, 5.825f);
            controller.UpdateReticle(swarm, actionLocked);

            Assert(controller.IsLocked, "Reticle must lock when antenna bearing matches agent bearing within 10°");
            Assert(controller.LockedAgent != null, "LockedAgent must not be null");
            Assert(lockLabel.text.StartsWith("LOCKED:"), "Lock label must show LOCKED: agent-X");
            Assert(controller.CurrentLeadAngleDeg > 0.5f && controller.CurrentLeadAngleDeg < 10.0f, "Lead angle must be non-zero and realistic");
            Assert(leadLabel.text.StartsWith("Lead:"), "Lead label must format lead angle");

            // Test firing when locked -> Hit feedback
            var hitEvent = controller.TriggerFire(45200);
            Assert(hitEvent != null, "TriggerFire must return KineticEvent");
            Assert(hitEvent.hit == true, "KineticEvent hit must be true for locked target with lead <= 5.0°");
            Assert(hitEvent.targetAgentId == controller.LockedAgent.agentId, "KineticEvent targetAgentId must match locked agent");
            Assert(hitEvent.firedAtMs == 45200, "KineticEvent firedAtMs must match timestamp");
            Assert(feedbackLabel.text.StartsWith("HIT:"), "Feedback label must show HIT");
            Assert(!feedbackLabel.ClassListContains("hidden"), "Feedback label must be visible");

            // Test consuming kinetic trigger (1-tick lifetime)
            var consumed = controller.ConsumeKineticTrigger();
            Assert(consumed != null && consumed.hit, "ConsumeKineticTrigger must return event on first call");
            var consumedAgain = controller.ConsumeKineticTrigger();
            Assert(consumedAgain == null, "ConsumeKineticTrigger must return null on subsequent call (single-tick contract)");

            // Test unlocking when aimed away (45°) -> Miss feedback
            var actionUnlocked = new TraineeAction(45.0f, 0f, false, null);
            controller.UpdateReticle(swarm, actionUnlocked);
            Assert(!controller.IsLocked, "Reticle must unlock when aimed away from swarm");
            Assert(lockLabel.text == "STANDBY", "Lock label must show STANDBY");

            var missEvent = controller.TriggerFire(45300);
            Assert(missEvent.hit == false, "KineticEvent hit must be false when unlocked");
            Assert(feedbackLabel.text.Contains("MISS"), "Feedback label must show MISS");
        }

        public static void TestCockpitControllerEwAndSmashIntegration()
        {
            var controller = new CockpitController();
            var mockRoot = new VisualElement { name = "cockpit-root" };

            var threatIndicator = new VisualElement { name = "threat-indicator" };
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

            var ewPanel = new VisualElement { name = "ew-panel" };
            var sliderAzimuth = new SliderInt { name = "ew-azimuth", lowValue = 0, highValue = 359, value = 0 };
            var sliderElevation = new SliderInt { name = "ew-elevation", lowValue = -90, highValue = 90, value = 0 };
            var dropdownBand = new DropdownField { name = "ew-band" };
            var toggleEngage = new Toggle { name = "ew-engage", value = false };
            var labelRoeWarning = new Label { name = "ew-roe-warning", text = "⚠ ROE VIOLATION — FRIENDLY BLACKOUT" };
            labelRoeWarning.AddToClassList("hidden");
            ewPanel.Add(sliderAzimuth);
            ewPanel.Add(sliderElevation);
            ewPanel.Add(dropdownBand);
            ewPanel.Add(toggleEngage);
            ewPanel.Add(labelRoeWarning);
            controlsRow.Add(ewPanel);

            var smashPanel = new VisualElement { name = "smash-panel" };
            var lockLabel = new Label { name = "smash-lock-label", text = "STANDBY" };
            var leadLabel = new Label { name = "smash-lead-label", text = "Lead: ---°" };
            var fireBtn = new Button { name = "fire-btn", text = "FIRE — 7.62mm" };
            var feedbackLabel = new Label { name = "smash-feedback-label", text = "" };
            feedbackLabel.AddToClassList("hidden");
            smashPanel.Add(lockLabel);
            smashPanel.Add(leadLabel);
            smashPanel.Add(fireBtn);
            smashPanel.Add(feedbackLabel);
            controlsRow.Add(smashPanel);

            var reticleOverlay = new VisualElement { name = "smash-reticle-overlay" };
            var crosshair = new VisualElement { name = "smash-crosshair" };
            var leadVector = new VisualElement { name = "smash-lead-vector" };
            var targetTag = new Label { name = "smash-target-tag", text = "" };
            reticleOverlay.Add(crosshair);
            reticleOverlay.Add(leadVector);
            reticleOverlay.Add(targetTag);
            dopplerContent.Add(reticleOverlay);

            var scorecardPanel = new VisualElement { name = "scorecard-panel" };
            var scorecardStubLabel = new Label { name = "scorecard-stub-label", text = "Scorecard received" };
            var computingOverlay = new VisualElement { name = "computing-overlay" };

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

            controller.BindElements(mockRoot);

            Assert(controller.EwController != null && controller.EwController.IsInitialized, "EwController must be initialized");
            Assert(controller.SmashController != null && controller.SmashController.IsInitialized, "SmashController must be initialized");

            // Start Scenario
            controller.OnScenarioStart("punjab-fog-pincer-01", "Punjab Border — Zero Visibility Pincer");
            Assert(!controlsRow.ClassListContains("hidden"), "Controls row must be un-hidden on SCENARIO_START");

            // Set EW controls to Beat 4 parameters (247° azimuth, -5° elevation, 5.825 GHz, engaged)
            controller.SetEwParameters(247, -5, 5.825f, true);

            var punjabReadings = SensorMock.GetPunjabFixture();
            var punjabSwarm = SensorMock.GetPunjabSwarmFixture();

            // Tick 1: Threat state RED, Swarm updated, EW active on attack axis
            controller.OnScenarioTick("RED", punjabReadings, punjabSwarm, friendlyVhfActive: false, elapsedMs: 45000);

            Assert(controller.CurrentEwState.engaged == true, "EW must be engaged");
            Assert(controller.CurrentEwState.friendlyBlackoutRisk == false, "Attack axis on 5.825 GHz must have zero blackout risk");
            Assert(controller.CurrentTraineeAction.antennaBearingDeg == 247f, "TraineeAction antenna bearing must be 247°");
            Assert(controller.CurrentTraineeAction.ewEngaged == true, "TraineeAction ewEngaged must be true");
            Assert(controller.SmashController.IsLocked, "SMASH reticle must lock on Punjab swarm agent");

            // Trainee pulls trigger on SMASH 2000
            var firedEv = controller.TriggerFireKinetic();
            Assert(firedEv != null && firedEv.hit, "Kinetic shot must hit locked target");

            // Tick 2: TraineeAction includes kinetic hit event
            controller.OnScenarioTick("RED", punjabReadings, punjabSwarm, friendlyVhfActive: false, elapsedMs: 45100);
            Assert(controller.CurrentTraineeAction.kinetic != null, "TraineeAction.kinetic must contain KineticEvent on firing tick");
            Assert(controller.CurrentTraineeAction.kinetic.hit == true, "Kinetic event hit must be true");

            // Tick 3: TraineeAction.kinetic resets to null (single-tick event)
            controller.OnScenarioTick("RED", punjabReadings, punjabSwarm, friendlyVhfActive: false, elapsedMs: 45200);
            Assert(controller.CurrentTraineeAction.kinetic == null, "TraineeAction.kinetic must be null on subsequent ticks");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message);
        }
    }
}
