using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TrishulSim.Network;

#if !UNITY_5_3_OR_NEWER
namespace UnityEngine
{
    public static class Application
    {
        private static string _streamingAssetsPath;

        public static string streamingAssetsPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_streamingAssetsPath))
                    return _streamingAssetsPath;
                return Path.Combine(Directory.GetCurrentDirectory(), "Assets", "StreamingAssets");
            }
            set { _streamingAssetsPath = value; }
        }
    }

    public static class Random
    {
        private static readonly object LockObj = new object();
        private static System.Random _rng = new System.Random();

        public static void InitState(int seed)
        {
            lock (LockObj)
            {
                _rng = new System.Random(seed);
            }
        }

        public static int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;
            lock (LockObj)
            {
                return _rng.Next(minInclusive, maxExclusive);
            }
        }

        public static float Range(float minInclusive, float maxInclusive)
        {
            if (maxInclusive <= minInclusive)
                return minInclusive;
            lock (LockObj)
            {
                return (float)(minInclusive + (_rng.NextDouble() * (maxInclusive - minInclusive)));
            }
        }
    }

    public class GameObject
    {
        public string name = string.Empty;
        public bool activeSelf = true;

        public GameObject() { }
        public GameObject(string name) { this.name = name ?? string.Empty; }

        public void SetActive(bool value)
        {
            activeSelf = value;
        }
    }

    public class AsyncOperation
    {
        public bool isDone = true;
        public float progress = 1f;
    }

    namespace Events
    {
        public delegate void UnityAction();
        public delegate void UnityAction<T0>(T0 arg0);

        public class UnityEvent
        {
            private readonly List<UnityAction> _listeners = new List<UnityAction>();

            public void AddListener(UnityAction call)
            {
                if (call != null)
                    _listeners.Add(call);
            }

            public void RemoveListener(UnityAction call)
            {
                if (call != null)
                    _listeners.Remove(call);
            }

            public void RemoveAllListeners()
            {
                _listeners.Clear();
            }

            public void Invoke()
            {
                UnityAction[] snapshot = _listeners.ToArray();
                for (int i = 0; i < snapshot.Length; i++)
                {
                    snapshot[i]();
                }
            }
        }

        public class UnityEvent<T0>
        {
            private readonly List<UnityAction<T0>> _listeners = new List<UnityAction<T0>>();

            public void AddListener(UnityAction<T0> call)
            {
                if (call != null)
                    _listeners.Add(call);
            }

            public void RemoveListener(UnityAction<T0> call)
            {
                if (call != null)
                    _listeners.Remove(call);
            }

            public void RemoveAllListeners()
            {
                _listeners.Clear();
            }

            public void Invoke(T0 arg0)
            {
                UnityAction<T0>[] snapshot = _listeners.ToArray();
                for (int i = 0; i < snapshot.Length; i++)
                {
                    snapshot[i](arg0);
                }
            }
        }
    }

    namespace SceneManagement
    {
        public static class SceneManager
        {
            public static string LastLoadedSceneName { get; private set; }
            public static int LoadCount { get; private set; }
            public static event Action<string> OnSceneLoaded;

            public static void ResetTestState()
            {
                LastLoadedSceneName = null;
                LoadCount = 0;
            }

            public static AsyncOperation LoadSceneAsync(string sceneName)
            {
                LastLoadedSceneName = sceneName;
                LoadCount++;
                Action<string> handler = OnSceneLoaded;
                if (handler != null)
                    handler(sceneName);
                return new AsyncOperation { isDone = true, progress = 1f };
            }
        }
    }

    namespace UI
    {
        public class Dropdown : MonoBehaviour
        {
            [Serializable]
            public class OptionData
            {
                public string text;
                public OptionData() { }
                public OptionData(string text) { this.text = text; }
            }

            public class DropdownEvent : UnityEngine.Events.UnityEvent<int> { }

            public List<OptionData> options = new List<OptionData>();
            private int _value;
            public DropdownEvent onValueChanged = new DropdownEvent();
            public Text captionText;

            public int value
            {
                get { return _value; }
                set
                {
                    _value = value;
                    RefreshShownValue();
                    if (onValueChanged != null)
                        onValueChanged.Invoke(_value);
                }
            }

            public void ClearOptions()
            {
                options.Clear();
                _value = 0;
                RefreshShownValue();
            }

            public void AddOptions(List<OptionData> optionList)
            {
                if (optionList != null)
                    options.AddRange(optionList);
                RefreshShownValue();
            }

            public void AddOptions(List<string> optionList)
            {
                if (optionList != null)
                {
                    for (int i = 0; i < optionList.Count; i++)
                        options.Add(new OptionData(optionList[i]));
                }
                RefreshShownValue();
            }

            public void RefreshShownValue()
            {
                if (captionText != null)
                {
                    if (options != null && _value >= 0 && _value < options.Count && options[_value] != null)
                        captionText.text = options[_value].text ?? string.Empty;
                    else
                        captionText.text = string.Empty;
                }
            }
        }

        public class Button : MonoBehaviour
        {
            public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { }

            public ButtonClickedEvent onClick = new ButtonClickedEvent();
            public bool interactable = true;
        }
    }
}
#endif

namespace TrishulSim.Scenarios
{
    /// <summary>
    /// WeatherPreset matching docs/SCHEMA.md Entity 1.
    /// </summary>
    [Serializable]
    public class WeatherPreset
    {
        public float visibility = 15.0f;
        public float windSpeedKmh = 12.0f;
        public float windBearingDeg = 270.0f;
        public float temperature = 4.0f;
        public bool isNight = true;

        public WeatherPreset Clone()
        {
            return new WeatherPreset
            {
                visibility = visibility,
                windSpeedKmh = windSpeedKmh,
                windBearingDeg = windBearingDeg,
                temperature = temperature,
                isNight = isNight
            };
        }

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder(128);
            sb.Append("{\"visibility\":");
            sb.Append(ScenarioConfig.FormatFloat(visibility));
            sb.Append(",\"windSpeedKmh\":");
            sb.Append(ScenarioConfig.FormatFloat(windSpeedKmh));
            sb.Append(",\"windBearingDeg\":");
            sb.Append(ScenarioConfig.FormatFloat(windBearingDeg));
            sb.Append(",\"temperature\":");
            sb.Append(ScenarioConfig.FormatFloat(temperature));
            sb.Append(",\"isNight\":");
            sb.Append(isNight ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }
    }

    /// <summary>
    /// SwarmProfile matching docs/SCHEMA.md Entity 1 and board/contracts/scenario-events.md.
    /// </summary>
    [Serializable]
    public class SwarmProfile
    {
        public const string TACTIC_PINCER_LOW_FAST = "PINCER_LOW_FAST";
        public const string TACTIC_SATURATION_DIVE = "SATURATION_DIVE";
        public const string TACTIC_MULTI_AXIS_SPLIT = "MULTI-AXIS_SPLIT";

        public int agentCount = 6;
        public int decoyCount = 2;
        public int attackerCount = 4;
        public string tacticTree = TACTIC_PINCER_LOW_FAST;
        public float speedKmh = 120.0f;
        public float altitudeM = 8.0f;
        public float[] rfFreqGHz = new float[] { 5.825f, 2.4f };
        public float rfPowerDbm = 23.0f;

        public static bool IsValidTacticTree(string value)
        {
            return string.Equals(value, TACTIC_PINCER_LOW_FAST, StringComparison.Ordinal) ||
                   string.Equals(value, TACTIC_SATURATION_DIVE, StringComparison.Ordinal) ||
                   string.Equals(value, TACTIC_MULTI_AXIS_SPLIT, StringComparison.Ordinal);
        }

        public SwarmProfile Clone()
        {
            float[] freqsCopy = null;
            if (rfFreqGHz != null)
            {
                freqsCopy = new float[rfFreqGHz.Length];
                Array.Copy(rfFreqGHz, freqsCopy, rfFreqGHz.Length);
            }

            return new SwarmProfile
            {
                agentCount = agentCount,
                decoyCount = decoyCount,
                attackerCount = attackerCount,
                tacticTree = tacticTree,
                speedKmh = speedKmh,
                altitudeM = altitudeM,
                rfFreqGHz = freqsCopy,
                rfPowerDbm = rfPowerDbm
            };
        }

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder(256);
            sb.Append("{\"agentCount\":");
            sb.Append(agentCount.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"decoyCount\":");
            sb.Append(decoyCount.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"attackerCount\":");
            sb.Append(attackerCount.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"tacticTree\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(tacticTree ?? string.Empty));
            sb.Append("\",\"speedKmh\":");
            sb.Append(ScenarioConfig.FormatFloat(speedKmh));
            sb.Append(",\"altitudeM\":");
            sb.Append(ScenarioConfig.FormatFloat(altitudeM));
            sb.Append(",\"rfFreqGHz\":[");
            if (rfFreqGHz != null)
            {
                for (int i = 0; i < rfFreqGHz.Length; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    sb.Append(ScenarioConfig.FormatFloat(rfFreqGHz[i]));
                }
            }
            sb.Append("],\"rfPowerDbm\":");
            sb.Append(ScenarioConfig.FormatFloat(rfPowerDbm));
            sb.Append('}');
            return sb.ToString();
        }
    }

    /// <summary>
    /// ScenarioConfig C# model matching docs/SCHEMA.md Entity 1.
    /// Zero-reflection parser/serializer for IL2CPP compatibility.
    /// </summary>
    [Serializable]
    public class ScenarioConfig
    {
        public const int CurrentVersion = 1;

        public const string REGION_PUNJAB_RIVERINE = "PUNJAB_RIVERINE";
        public const string REGION_LADAKH_LAC = "LADAKH_LAC";
        public const string REGION_LOC_MOUNTAIN = "LOC_MOUNTAIN";
        public const string REGION_DESERT_FOB = "DESERT_FOB";

        public string id = "punjab-fog-pincer-01";
        public string name = "Punjab Border \u2014 Zero Visibility Pincer";
        public string nameHindi = "\u092A\u0902\u091C\u093E\u092C \u0938\u0940\u092E\u093E \u2014 \u0936\u0942\u0928\u094D\u092F \u0926\u0943\u0936\u094D\u092F\u0924\u093E \u092A\u093F\u0902\u0938\u0930";
        public string region = REGION_PUNJAB_RIVERINE;
        public WeatherPreset weatherPreset = new WeatherPreset();
        public SwarmProfile swarmProfile = new SwarmProfile();
        public int durationMaxSecs = 120;
        public int seed = 42819;
        public int version = CurrentVersion;

        public static bool IsValidRegion(string value)
        {
            return string.Equals(value, REGION_PUNJAB_RIVERINE, StringComparison.Ordinal) ||
                   string.Equals(value, REGION_LADAKH_LAC, StringComparison.Ordinal) ||
                   string.Equals(value, REGION_LOC_MOUNTAIN, StringComparison.Ordinal) ||
                   string.Equals(value, REGION_DESERT_FOB, StringComparison.Ordinal);
        }

        public ScenarioConfig Clone()
        {
            return new ScenarioConfig
            {
                id = id,
                name = name,
                nameHindi = nameHindi,
                region = region,
                weatherPreset = weatherPreset != null ? weatherPreset.Clone() : new WeatherPreset(),
                swarmProfile = swarmProfile != null ? swarmProfile.Clone() : new SwarmProfile(),
                durationMaxSecs = durationMaxSecs,
                seed = seed,
                version = version
            };
        }

        public string ToJson()
        {
            WeatherPreset wp = weatherPreset ?? new WeatherPreset();
            SwarmProfile sp = swarmProfile ?? new SwarmProfile();

            StringBuilder sb = new StringBuilder(512);
            sb.Append("{\"id\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(id ?? string.Empty));
            sb.Append("\",\"name\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(name ?? string.Empty));
            sb.Append("\",\"nameHindi\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(nameHindi ?? string.Empty));
            sb.Append("\",\"region\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(region ?? string.Empty));
            sb.Append("\",\"weatherPreset\":");
            sb.Append(wp.ToJson());
            sb.Append(",\"swarmProfile\":");
            sb.Append(sp.ToJson());
            sb.Append(",\"durationMaxSecs\":");
            sb.Append(durationMaxSecs.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"seed\":");
            sb.Append(seed.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"version\":");
            sb.Append(version.ToString(CultureInfo.InvariantCulture));
            sb.Append('}');
            return sb.ToString();
        }

        public static ScenarioConfig Parse(string json)
        {
            ScenarioConfig config;
            string error;
            if (!TryParse(json, out config, out error))
                throw new FormatException(error);
            return config;
        }

        public static bool TryParse(string json, out ScenarioConfig config, out string error)
        {
            config = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "ScenarioConfig JSON is null or empty.";
                return false;
            }

            Dictionary<string, JsonToken> fields;
            if (!JsonParser.TryParseObject(json, out fields, out error))
                return false;

            string idVal;
            if (!JsonParser.TryGetRequiredString(fields, "id", out idVal, out error))
                return false;

            string nameVal;
            if (!JsonParser.TryGetRequiredString(fields, "name", out nameVal, out error))
                return false;

            string nameHindiVal;
            if (!JsonParser.TryGetRequiredString(fields, "nameHindi", out nameHindiVal, out error))
                return false;

            string regionVal;
            if (!JsonParser.TryGetRequiredString(fields, "region", out regionVal, out error))
                return false;
            if (!IsValidRegion(regionVal))
            {
                error = "ScenarioConfig field 'region' has unsupported value '" + regionVal + "'.";
                return false;
            }

            string wpRaw;
            if (!JsonParser.TryGetRequiredObjectRaw(fields, "weatherPreset", out wpRaw, out error))
                return false;
            WeatherPreset wp;
            if (!TryParseWeatherPreset(wpRaw, out wp, out error))
                return false;

            string spRaw;
            if (!JsonParser.TryGetRequiredObjectRaw(fields, "swarmProfile", out spRaw, out error))
                return false;
            SwarmProfile sp;
            if (!TryParseSwarmProfile(spRaw, out sp, out error))
                return false;

            int durationSecs;
            if (!JsonParser.TryGetRequiredInt(fields, "durationMaxSecs", out durationSecs, out error) || durationSecs <= 0)
            {
                if (string.IsNullOrEmpty(error))
                    error = "ScenarioConfig field 'durationMaxSecs' must be > 0.";
                return false;
            }

            int seedVal;
            if (!JsonParser.TryGetRequiredInt(fields, "seed", out seedVal, out error))
                return false;

            int versionVal;
            if (!JsonParser.TryGetRequiredInt(fields, "version", out versionVal, out error) || versionVal != CurrentVersion)
            {
                if (string.IsNullOrEmpty(error))
                    error = "ScenarioConfig field 'version' must be " + CurrentVersion + ".";
                return false;
            }

            config = new ScenarioConfig
            {
                id = idVal,
                name = nameVal,
                nameHindi = nameHindiVal,
                region = regionVal,
                weatherPreset = wp,
                swarmProfile = sp,
                durationMaxSecs = durationSecs,
                seed = seedVal,
                version = versionVal
            };
            return true;
        }

        private static bool TryParseWeatherPreset(string json, out WeatherPreset preset, out string error)
        {
            preset = null;
            Dictionary<string, JsonToken> fields;
            if (!JsonParser.TryParseObject(json, out fields, out error))
                return false;

            float visibility;
            if (!JsonParser.TryGetRequiredFloat(fields, "visibility", out visibility, out error) || visibility < 0f)
            {
                if (string.IsNullOrEmpty(error))
                    error = "WeatherPreset field 'visibility' must be >= 0.";
                return false;
            }

            float windSpeedKmh;
            if (!JsonParser.TryGetRequiredFloat(fields, "windSpeedKmh", out windSpeedKmh, out error) || windSpeedKmh < 0f)
            {
                if (string.IsNullOrEmpty(error))
                    error = "WeatherPreset field 'windSpeedKmh' must be >= 0.";
                return false;
            }

            float windBearingDeg;
            if (!JsonParser.TryGetRequiredFloat(fields, "windBearingDeg", out windBearingDeg, out error))
                return false;

            float temperature;
            if (!JsonParser.TryGetRequiredFloat(fields, "temperature", out temperature, out error))
                return false;

            bool isNight;
            if (!JsonParser.TryGetRequiredBool(fields, "isNight", out isNight, out error))
                return false;

            preset = new WeatherPreset
            {
                visibility = visibility,
                windSpeedKmh = windSpeedKmh,
                windBearingDeg = windBearingDeg,
                temperature = temperature,
                isNight = isNight
            };
            return true;
        }

        private static bool TryParseSwarmProfile(string json, out SwarmProfile profile, out string error)
        {
            profile = null;
            Dictionary<string, JsonToken> fields;
            if (!JsonParser.TryParseObject(json, out fields, out error))
                return false;

            int agentCount;
            if (!JsonParser.TryGetRequiredInt(fields, "agentCount", out agentCount, out error) || agentCount <= 0)
            {
                if (string.IsNullOrEmpty(error))
                    error = "SwarmProfile field 'agentCount' must be > 0.";
                return false;
            }

            int decoyCount;
            if (!JsonParser.TryGetRequiredInt(fields, "decoyCount", out decoyCount, out error) || decoyCount < 0)
            {
                if (string.IsNullOrEmpty(error))
                    error = "SwarmProfile field 'decoyCount' must be >= 0.";
                return false;
            }

            int attackerCount;
            if (!JsonParser.TryGetRequiredInt(fields, "attackerCount", out attackerCount, out error) || attackerCount <= 0)
            {
                if (string.IsNullOrEmpty(error))
                    error = "SwarmProfile field 'attackerCount' must be > 0.";
                return false;
            }

            if (decoyCount + attackerCount != agentCount)
            {
                error = "SwarmProfile 'decoyCount + attackerCount' must equal 'agentCount'.";
                return false;
            }

            string tacticTree;
            if (!JsonParser.TryGetRequiredString(fields, "tacticTree", out tacticTree, out error))
                return false;
            if (!SwarmProfile.IsValidTacticTree(tacticTree))
            {
                error = "SwarmProfile field 'tacticTree' has unsupported value '" + tacticTree + "'.";
                return false;
            }

            float speedKmh;
            if (!JsonParser.TryGetRequiredFloat(fields, "speedKmh", out speedKmh, out error) || speedKmh <= 0f)
            {
                if (string.IsNullOrEmpty(error))
                    error = "SwarmProfile field 'speedKmh' must be > 0.";
                return false;
            }

            float altitudeM;
            if (!JsonParser.TryGetRequiredFloat(fields, "altitudeM", out altitudeM, out error) || altitudeM < 0f)
            {
                if (string.IsNullOrEmpty(error))
                    error = "SwarmProfile field 'altitudeM' must be >= 0.";
                return false;
            }

            float[] rfFreqGHz;
            if (!JsonParser.TryGetRequiredFloatArray(fields, "rfFreqGHz", out rfFreqGHz, out error) || rfFreqGHz.Length == 0)
            {
                if (string.IsNullOrEmpty(error))
                    error = "SwarmProfile field 'rfFreqGHz' must be a non-empty float array.";
                return false;
            }

            float rfPowerDbm;
            if (!JsonParser.TryGetRequiredFloat(fields, "rfPowerDbm", out rfPowerDbm, out error))
                return false;

            profile = new SwarmProfile
            {
                agentCount = agentCount,
                decoyCount = decoyCount,
                attackerCount = attackerCount,
                tacticTree = tacticTree,
                speedKmh = speedKmh,
                altitudeM = altitudeM,
                rfFreqGHz = rfFreqGHz,
                rfPowerDbm = rfPowerDbm
            };
            return true;
        }

        internal static string FormatFloat(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return "0.0";
            string s = value.ToString("0.###", CultureInfo.InvariantCulture);
            if (s.IndexOf('.') < 0)
                s += ".0";
            return s;
        }
    }

    /// <summary>
    /// SCENARIO_START payload matching board/contracts/scenario-events.md v1.
    /// </summary>
    [Serializable]
    public class ScenarioStartPayload
    {
        public string sessionId;
        public string scenarioId;
        public int seed;
        public long startedAtMs;
        public ScenarioConfig scenarioConfig;

        public string ToJson()
        {
            ScenarioConfig cfg = scenarioConfig != null ? scenarioConfig.Clone() : new ScenarioConfig();
            if (seed != 0)
                cfg.seed = seed;
            StringBuilder sb = new StringBuilder(640);
            sb.Append("{\"sessionId\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(sessionId ?? string.Empty));
            sb.Append("\",\"scenarioId\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(scenarioId ?? string.Empty));
            sb.Append("\",\"seed\":");
            sb.Append(seed.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"startedAtMs\":");
            sb.Append(startedAtMs.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"scenarioConfig\":");
            sb.Append(cfg.ToJson());
            sb.Append('}');
            return sb.ToString();
        }

        public static JsonEnvelope CreateEnvelope(string sessionId, ScenarioConfig config, long? startedAtMs = null)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("sessionId must not be empty.", "sessionId");
            if (config == null)
                throw new ArgumentNullException("config");

            long ts = startedAtMs ?? JsonEnvelope.NowUnixMs();
            ScenarioConfig cloned = config.Clone();
            ScenarioStartPayload payload = new ScenarioStartPayload
            {
                sessionId = sessionId,
                scenarioId = cloned.id,
                seed = cloned.seed,
                startedAtMs = ts,
                scenarioConfig = cloned
            };

            return JsonEnvelope.Create(JsonEnvelope.SCENARIO_START, payload.ToJson(), JsonEnvelope.CurrentVersion, ts);
        }

        public static ScenarioStartPayload Parse(string payloadJson)
        {
            ScenarioStartPayload payload;
            string error;
            if (!TryParse(payloadJson, out payload, out error))
                throw new FormatException(error);
            return payload;
        }

        public static bool TryParse(string payloadJson, out ScenarioStartPayload payload, out string error)
        {
            payload = null;
            error = null;

            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                error = "SCENARIO_START payload is null or empty.";
                return false;
            }

            Dictionary<string, JsonToken> fields;
            if (!JsonParser.TryParseObject(payloadJson, out fields, out error))
                return false;

            string sessionIdVal;
            if (!JsonParser.TryGetRequiredString(fields, "sessionId", out sessionIdVal, out error))
                return false;

            string scenarioIdVal;
            if (!JsonParser.TryGetRequiredString(fields, "scenarioId", out scenarioIdVal, out error))
                return false;

            int seedVal;
            if (!JsonParser.TryGetRequiredInt(fields, "seed", out seedVal, out error))
                return false;

            long startedAtMsVal;
            if (!JsonParser.TryGetRequiredLong(fields, "startedAtMs", out startedAtMsVal, out error) || startedAtMsVal < 0)
            {
                if (string.IsNullOrEmpty(error))
                    error = "SCENARIO_START field 'startedAtMs' must be >= 0.";
                return false;
            }

            string configRaw;
            if (!JsonParser.TryGetRequiredObjectRaw(fields, "scenarioConfig", out configRaw, out error))
                return false;

            ScenarioConfig cfg;
            if (!ScenarioConfig.TryParse(configRaw, out cfg, out error))
                return false;

            if (!string.Equals(scenarioIdVal, cfg.id, StringComparison.Ordinal))
            {
                error = "SCENARIO_START 'scenarioId' (" + scenarioIdVal + ") does not match 'scenarioConfig.id' (" + cfg.id + ").";
                return false;
            }

            cfg.seed = seedVal;

            payload = new ScenarioStartPayload
            {
                sessionId = sessionIdVal,
                scenarioId = scenarioIdVal,
                seed = seedVal,
                startedAtMs = startedAtMsVal,
                scenarioConfig = cfg
            };
            return true;
        }
    }

    internal enum JsonTokenKind
    {
        String,
        Number,
        Object,
        Array,
        Boolean,
        Null
    }

    internal struct JsonToken
    {
        public JsonTokenKind Kind;
        public string RawText;
        public string StringValue;
        public List<JsonToken> ArrayElements;
    }

    internal static class JsonParser
    {
        public static bool TryParseObject(string json, out Dictionary<string, JsonToken> fields, out string error)
        {
            int idx = SkipWs(json, 0, json.Length);
            if (!TryReadObject(json, ref idx, json.Length, out fields, out error))
                return false;

            idx = SkipWs(json, idx, json.Length);
            if (idx != json.Length)
            {
                error = "Unexpected trailing characters after JSON object.";
                return false;
            }
            return true;
        }

        public static bool TryGetRequiredString(Dictionary<string, JsonToken> fields, string key, out string value, out string error)
        {
            value = null;
            error = null;
            JsonToken tok;
            if (!fields.TryGetValue(key, out tok) || tok.Kind != JsonTokenKind.String || string.IsNullOrWhiteSpace(tok.StringValue))
            {
                error = "Missing or empty required string field '" + key + "'.";
                return false;
            }
            value = tok.StringValue;
            return true;
        }

        public static bool TryGetRequiredObjectRaw(Dictionary<string, JsonToken> fields, string key, out string rawObject, out string error)
        {
            rawObject = null;
            error = null;
            JsonToken tok;
            if (!fields.TryGetValue(key, out tok) || tok.Kind != JsonTokenKind.Object || string.IsNullOrWhiteSpace(tok.RawText))
            {
                error = "Missing required object field '" + key + "'.";
                return false;
            }
            rawObject = tok.RawText;
            return true;
        }

        public static bool TryGetRequiredInt(Dictionary<string, JsonToken> fields, string key, out int value, out string error)
        {
            value = 0;
            error = null;
            JsonToken tok;
            if (!fields.TryGetValue(key, out tok) || tok.Kind != JsonTokenKind.Number || !TryParseIntegralInt(tok.RawText, out value))
            {
                error = "Missing or invalid integer field '" + key + "'.";
                return false;
            }
            return true;
        }

        public static bool TryGetRequiredLong(Dictionary<string, JsonToken> fields, string key, out long value, out string error)
        {
            value = 0;
            error = null;
            JsonToken tok;
            if (!fields.TryGetValue(key, out tok) || tok.Kind != JsonTokenKind.Number || !TryParseIntegralLong(tok.RawText, out value))
            {
                error = "Missing or invalid 64-bit integer field '" + key + "'.";
                return false;
            }
            return true;
        }

        public static bool TryGetRequiredFloat(Dictionary<string, JsonToken> fields, string key, out float value, out string error)
        {
            value = 0f;
            error = null;
            JsonToken tok;
            if (!fields.TryGetValue(key, out tok) || tok.Kind != JsonTokenKind.Number || !TryParseFloat(tok.RawText, out value))
            {
                error = "Missing or invalid number field '" + key + "'.";
                return false;
            }
            return true;
        }

        public static bool TryGetRequiredBool(Dictionary<string, JsonToken> fields, string key, out bool value, out string error)
        {
            value = false;
            error = null;
            JsonToken tok;
            if (!fields.TryGetValue(key, out tok) || tok.Kind != JsonTokenKind.Boolean)
            {
                error = "Missing or invalid boolean field '" + key + "'.";
                return false;
            }
            value = string.Equals(tok.RawText, "true", StringComparison.Ordinal);
            return true;
        }

        public static bool TryGetRequiredFloatArray(Dictionary<string, JsonToken> fields, string key, out float[] values, out string error)
        {
            values = null;
            error = null;
            JsonToken tok;
            if (!fields.TryGetValue(key, out tok) || tok.Kind != JsonTokenKind.Array || tok.ArrayElements == null)
            {
                error = "Missing or invalid array field '" + key + "'.";
                return false;
            }

            float[] arr = new float[tok.ArrayElements.Count];
            for (int i = 0; i < tok.ArrayElements.Count; i++)
            {
                JsonToken elem = tok.ArrayElements[i];
                float f;
                if (elem.Kind != JsonTokenKind.Number || !TryParseFloat(elem.RawText, out f))
                {
                    error = "Invalid numeric element at index " + i + " in array '" + key + "'.";
                    return false;
                }
                arr[i] = f;
            }
            values = arr;
            return true;
        }

        private static bool TryReadObject(string text, ref int idx, int end, out Dictionary<string, JsonToken> fields, out string error)
        {
            fields = new Dictionary<string, JsonToken>(StringComparer.Ordinal);
            error = null;

            idx = SkipWs(text, idx, end);
            if (idx >= end || text[idx] != '{')
            {
                error = "Expected '{' at start of JSON object.";
                return false;
            }
            idx++;

            idx = SkipWs(text, idx, end);
            if (idx < end && text[idx] == '}')
            {
                idx++;
                return true;
            }

            while (idx < end)
            {
                idx = SkipWs(text, idx, end);
                if (idx >= end || text[idx] != '\"')
                {
                    error = "Expected quoted property name in JSON object.";
                    return false;
                }

                string key;
                if (!TryReadString(text, ref idx, end, out key, out error))
                    return false;

                idx = SkipWs(text, idx, end);
                if (idx >= end || text[idx] != ':')
                {
                    error = "Expected ':' after property '" + key + "'.";
                    return false;
                }
                idx++;

                idx = SkipWs(text, idx, end);
                JsonToken val;
                if (!TryReadValue(text, ref idx, end, out val, out error))
                    return false;

                fields[key] = val;

                idx = SkipWs(text, idx, end);
                if (idx >= end)
                {
                    error = "Unterminated JSON object.";
                    return false;
                }

                if (text[idx] == ',')
                {
                    idx++;
                    idx = SkipWs(text, idx, end);
                    if (idx >= end || text[idx] == '}')
                    {
                        error = "Trailing comma in JSON object is not allowed.";
                        return false;
                    }
                    continue;
                }

                if (text[idx] == '}')
                {
                    idx++;
                    return true;
                }

                error = "Expected ',' or '}' in JSON object.";
                return false;
            }

            error = "Unterminated JSON object.";
            return false;
        }

        private static bool TryReadArray(string text, ref int idx, int end, out List<JsonToken> elements, out string error)
        {
            elements = new List<JsonToken>();
            error = null;

            idx = SkipWs(text, idx, end);
            if (idx >= end || text[idx] != '[')
            {
                error = "Expected '[' at start of JSON array.";
                return false;
            }
            idx++;

            idx = SkipWs(text, idx, end);
            if (idx < end && text[idx] == ']')
            {
                idx++;
                return true;
            }

            while (idx < end)
            {
                idx = SkipWs(text, idx, end);
                JsonToken elem;
                if (!TryReadValue(text, ref idx, end, out elem, out error))
                    return false;

                elements.Add(elem);

                idx = SkipWs(text, idx, end);
                if (idx >= end)
                {
                    error = "Unterminated JSON array.";
                    return false;
                }

                if (text[idx] == ',')
                {
                    idx++;
                    idx = SkipWs(text, idx, end);
                    if (idx >= end || text[idx] == ']')
                    {
                        error = "Trailing comma in JSON array is not allowed.";
                        return false;
                    }
                    continue;
                }

                if (text[idx] == ']')
                {
                    idx++;
                    return true;
                }

                error = "Expected ',' or ']' in JSON array.";
                return false;
            }

            error = "Unterminated JSON array.";
            return false;
        }

        private static bool TryReadValue(string text, ref int idx, int end, out JsonToken token, out string error)
        {
            token = default(JsonToken);
            error = null;

            if (idx >= end)
            {
                error = "Unexpected end of JSON value.";
                return false;
            }

            char c = text[idx];
            if (c == '\"')
            {
                int start = idx;
                string decoded;
                if (!TryReadString(text, ref idx, end, out decoded, out error))
                    return false;
                token = new JsonToken
                {
                    Kind = JsonTokenKind.String,
                    RawText = text.Substring(start, idx - start),
                    StringValue = decoded
                };
                return true;
            }

            if (c == '{')
            {
                int start = idx;
                Dictionary<string, JsonToken> nested;
                if (!TryReadObject(text, ref idx, end, out nested, out error))
                    return false;
                token = new JsonToken
                {
                    Kind = JsonTokenKind.Object,
                    RawText = text.Substring(start, idx - start)
                };
                return true;
            }

            if (c == '[')
            {
                int start = idx;
                List<JsonToken> arr;
                if (!TryReadArray(text, ref idx, end, out arr, out error))
                    return false;
                token = new JsonToken
                {
                    Kind = JsonTokenKind.Array,
                    RawText = text.Substring(start, idx - start),
                    ArrayElements = arr
                };
                return true;
            }

            if (MatchLiteral(text, ref idx, end, "true"))
            {
                token = new JsonToken { Kind = JsonTokenKind.Boolean, RawText = "true" };
                return true;
            }

            if (MatchLiteral(text, ref idx, end, "false"))
            {
                token = new JsonToken { Kind = JsonTokenKind.Boolean, RawText = "false" };
                return true;
            }

            if (MatchLiteral(text, ref idx, end, "null"))
            {
                token = new JsonToken { Kind = JsonTokenKind.Null, RawText = "null" };
                return true;
            }

            if (c == '-' || (c >= '0' && c <= '9'))
            {
                int start = idx;
                if (!TryReadNumber(text, ref idx, end, out error))
                    return false;
                token = new JsonToken
                {
                    Kind = JsonTokenKind.Number,
                    RawText = text.Substring(start, idx - start)
                };
                return true;
            }

            error = "Unexpected character '" + c + "' in JSON value.";
            return false;
        }

        private static bool TryReadString(string text, ref int idx, int end, out string result, out string error)
        {
            result = null;
            error = null;

            if (idx >= end || text[idx] != '\"')
            {
                error = "Expected '\"' at start of JSON string.";
                return false;
            }
            idx++;

            StringBuilder sb = new StringBuilder();
            while (idx < end)
            {
                char c = text[idx++];
                if (c == '\"')
                {
                    result = sb.ToString();
                    return true;
                }
                if (c == '\\')
                {
                    if (idx >= end)
                    {
                        error = "Unterminated escape sequence in JSON string.";
                        return false;
                    }
                    char esc = text[idx++];
                    switch (esc)
                    {
                        case '\"': sb.Append('\"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (idx + 4 > end)
                            {
                                error = "Invalid unicode escape in JSON string.";
                                return false;
                            }
                            for (int k = 0; k < 4; k++)
                            {
                                char h = text[idx + k];
                                bool isHex = (h >= '0' && h <= '9') || (h >= 'a' && h <= 'f') || (h >= 'A' && h <= 'F');
                                if (!isHex)
                                {
                                    error = "Invalid hex digits in unicode escape.";
                                    return false;
                                }
                            }
                            int codePoint = int.Parse(text.Substring(idx, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                            sb.Append((char)codePoint);
                            idx += 4;
                            break;
                        default:
                            error = "Invalid escape character '\\" + esc + "' in JSON string.";
                            return false;
                    }
                }
                else if (c < 0x20)
                {
                    error = "Unescaped control character in JSON string.";
                    return false;
                }
                else
                {
                    sb.Append(c);
                }
            }

            error = "Unterminated JSON string.";
            return false;
        }

        private static bool TryReadNumber(string text, ref int idx, int end, out string error)
        {
            error = null;
            int start = idx;
            if (text[idx] == '-')
                idx++;

            if (idx >= end || text[idx] < '0' || text[idx] > '9')
            {
                error = "Invalid number in JSON.";
                return false;
            }

            char first = text[idx++];
            if (first == '0')
            {
                if (idx < end && text[idx] >= '0' && text[idx] <= '9')
                {
                    error = "Leading zeros are not allowed in JSON numbers.";
                    return false;
                }
            }
            else
            {
                while (idx < end && text[idx] >= '0' && text[idx] <= '9')
                    idx++;
            }

            if (idx < end && text[idx] == '.')
            {
                idx++;
                if (idx >= end || text[idx] < '0' || text[idx] > '9')
                {
                    error = "Invalid fractional number in JSON.";
                    return false;
                }
                while (idx < end && text[idx] >= '0' && text[idx] <= '9')
                    idx++;
            }

            if (idx < end && (text[idx] == 'e' || text[idx] == 'E'))
            {
                idx++;
                if (idx < end && (text[idx] == '+' || text[idx] == '-'))
                    idx++;
                if (idx >= end || text[idx] < '0' || text[idx] > '9')
                {
                    error = "Invalid exponent in JSON number.";
                    return false;
                }
                while (idx < end && text[idx] >= '0' && text[idx] <= '9')
                    idx++;
            }

            double d;
            string slice = text.Substring(start, idx - start);
            if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ||
                double.IsNaN(d) || double.IsInfinity(d))
            {
                error = "Invalid numeric literal '" + slice + "'.";
                return false;
            }
            return true;
        }

        private static bool TryParseIntegralInt(string raw, out int value)
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;

            double d;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out d) &&
                !double.IsNaN(d) && !double.IsInfinity(d) &&
                Math.Floor(d) == d && d >= int.MinValue && d <= int.MaxValue)
            {
                value = (int)d;
                return true;
            }
            return false;
        }

        private static bool TryParseIntegralLong(string raw, out long value)
        {
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;

            decimal dec;
            if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out dec) &&
                decimal.Truncate(dec) == dec && dec >= long.MinValue && dec <= long.MaxValue)
            {
                value = (long)dec;
                return true;
            }
            return false;
        }

        private static bool TryParseFloat(string raw, out float value)
        {
            double d;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out d) &&
                !double.IsNaN(d) && !double.IsInfinity(d) &&
                d >= -float.MaxValue && d <= float.MaxValue)
            {
                value = (float)d;
                return true;
            }
            value = 0f;
            return false;
        }

        private static bool MatchLiteral(string text, ref int idx, int end, string literal)
        {
            if (idx + literal.Length > end)
                return false;
            for (int i = 0; i < literal.Length; i++)
            {
                if (text[idx + i] != literal[i])
                    return false;
            }
            idx += literal.Length;
            return true;
        }

        private static int SkipWs(string text, int idx, int end)
        {
            while (idx < end)
            {
                char c = text[idx];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    idx++;
                else
                    break;
            }
            return idx;
        }
    }
}
