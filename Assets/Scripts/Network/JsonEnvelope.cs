using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#if !UNITY_5_3_OR_NEWER
namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HeaderAttribute : Attribute
    {
        public string header;
        public HeaderAttribute(string header) { this.header = header; }
    }

    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float r, float g, float b, float a = 1f)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static Color green { get { return new Color(0f, 0.702f, 0f, 1f); } }
        public static Color gray { get { return new Color(0.5f, 0.5f, 0.5f, 1f); } }
        public static Color grey { get { return new Color(0.5f, 0.5f, 0.5f, 1f); } }
        public static Color yellow { get { return new Color(1f, 0.702f, 0f, 1f); } }

        public static bool operator ==(Color a, Color b)
        {
            return Math.Abs(a.r - b.r) < 0.001f && Math.Abs(a.g - b.g) < 0.001f && Math.Abs(a.b - b.b) < 0.001f && Math.Abs(a.a - b.a) < 0.001f;
        }

        public static bool operator !=(Color a, Color b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (obj is Color)
            {
                Color other = (Color)obj;
                return this == other;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return r.GetHashCode() ^ g.GetHashCode() ^ b.GetHashCode() ^ a.GetHashCode();
        }
    }

    public static class Debug
    {
        static Debug()
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        }

        public static event Action<string> OnLog;
        public static event Action<string> OnLogWarning;
        public static event Action<string> OnLogError;

        public static void Log(object message)
        {
            string text = message != null ? message.ToString() : "null";
            Console.WriteLine(text);
            Action<string> handler = OnLog;
            if (handler != null) handler(text);
        }

        public static void LogWarning(object message)
        {
            string text = message != null ? message.ToString() : "null";
            Console.WriteLine("[WARN] " + text);
            Action<string> handler = OnLogWarning;
            if (handler != null) handler(text);
        }

        public static void LogError(object message)
        {
            string text = message != null ? message.ToString() : "null";
            Console.Error.WriteLine("[ERROR] " + text);
            Action<string> handler = OnLogError;
            if (handler != null) handler(text);
        }
    }

    public class MonoBehaviour
    {
        public bool enabled = true;
    }

    namespace UI
    {
        public class Text : MonoBehaviour
        {
            public string text = string.Empty;
            public Color color = Color.grey;
        }
    }
}
#endif

namespace TrishulSim.Network
{
    /// <summary>
    /// Wire envelope for TRISHUL-SIM localhost WebSocket IPC (board/contracts/websocket-ipc.md v1).
    /// Zero-reflection parser/serializer for IL2CPP compatibility (board/FIT-AUDIT.md).
    /// </summary>
    [Serializable]
    public struct JsonEnvelope
    {
        public const int CurrentVersion = 1;
        public const int MaxMessageBytes = 64 * 1024; // 64 KB per websocket-ipc.md

        // Message registry constants (board/contracts/websocket-ipc.md)
        public const string SCENARIO_START = "SCENARIO_START";
        public const string SCENARIO_TICK = "SCENARIO_TICK";
        public const string SCENARIO_END = "SCENARIO_END";
        public const string SCORECARD_PAYLOAD = "SCORECARD_PAYLOAD";
        public const string CLIENT_READY = "CLIENT_READY";
        public const string CLIENT_HEARTBEAT = "CLIENT_HEARTBEAT";
        public const string SERVER_HEARTBEAT_ACK = "SERVER_HEARTBEAT_ACK";
        public const string ERROR = "ERROR";

        // Error code constants (board/contracts/websocket-ipc.md §2)
        public const string ERR_SCENARIO_NOT_FOUND = "SCENARIO_NOT_FOUND";
        public const string ERR_VERSION_MISMATCH = "VERSION_MISMATCH";
        public const string ERR_PAYLOAD_INVALID = "PAYLOAD_INVALID";
        public const string ERR_SCENARIO_ALREADY_RUNNING = "SCENARIO_ALREADY_RUNNING";

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public string type;
        public int v;
        public long ts;
        public string payload;

        public static long NowUnixMs()
        {
            return (long)(DateTime.UtcNow - UnixEpoch).TotalMilliseconds;
        }

        public static bool IsKnownMessageType(string messageType)
        {
            switch (messageType)
            {
                case SCENARIO_START:
                case SCENARIO_TICK:
                case SCENARIO_END:
                case SCORECARD_PAYLOAD:
                case CLIENT_READY:
                case CLIENT_HEARTBEAT:
                case SERVER_HEARTBEAT_ACK:
                case ERROR:
                    return true;
                default:
                    return false;
            }
        }

        public static JsonEnvelope Create(string type, string payloadJson = "{}", int version = CurrentVersion, long? timestampMs = null)
        {
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Envelope type must not be empty.", "type");

            string normalizedPayload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson.Trim();
            if (!normalizedPayload.StartsWith("{", StringComparison.Ordinal) || !normalizedPayload.EndsWith("}", StringComparison.Ordinal))
                throw new ArgumentException("Envelope payload must be a JSON object.", "payloadJson");

            return new JsonEnvelope
            {
                type = type,
                v = version,
                ts = timestampMs ?? NowUnixMs(),
                payload = normalizedPayload
            };
        }

        public static JsonEnvelope CreateClientReady(string clientRole = "TRAINEE_COCKPIT", string buildVersion = "0.1.0", long? ts = null)
        {
            string payloadJson = "{\"clientRole\":\"" + EscapeJsonString(clientRole ?? string.Empty) +
                                 "\",\"buildVersion\":\"" + EscapeJsonString(buildVersion ?? string.Empty) + "\"}";
            return Create(CLIENT_READY, payloadJson, CurrentVersion, ts);
        }

        public static JsonEnvelope CreateClientHeartbeat(long? ts = null)
        {
            return Create(CLIENT_HEARTBEAT, "{}", CurrentVersion, ts);
        }

        public static JsonEnvelope CreateServerHeartbeatAck(long? serverTimeMs = null)
        {
            long now = serverTimeMs ?? NowUnixMs();
            string payloadJson = "{\"serverTimeMs\":" + now.ToString(CultureInfo.InvariantCulture) + "}";
            return Create(SERVER_HEARTBEAT_ACK, payloadJson, CurrentVersion, now);
        }

        public static JsonEnvelope CreateError(string code, string message, bool fatal, long? ts = null)
        {
            string payloadJson = "{\"code\":\"" + EscapeJsonString(code ?? ERR_PAYLOAD_INVALID) +
                                 "\",\"message\":\"" + EscapeJsonString(message ?? string.Empty) +
                                 "\",\"fatal\":" + (fatal ? "true" : "false") + "}";
            return Create(ERROR, payloadJson, CurrentVersion, ts);
        }

        public string ToJson()
        {
            string normalizedPayload = string.IsNullOrWhiteSpace(payload) ? "{}" : payload.Trim();
            StringBuilder sb = new StringBuilder(normalizedPayload.Length + 64);
            sb.Append("{\"type\":\"");
            sb.Append(EscapeJsonString(type ?? string.Empty));
            sb.Append("\",\"v\":");
            sb.Append(v.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"ts\":");
            sb.Append(ts.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"payload\":");
            sb.Append(normalizedPayload);
            sb.Append('}');
            return sb.ToString();
        }

        public static JsonEnvelope Parse(string json)
        {
            JsonEnvelope env;
            string error;
            if (!TryParse(json, out env, out error))
                throw new FormatException(error);
            return env;
        }

        public static bool TryParse(string json, out JsonEnvelope envelope, out string error)
        {
            envelope = default(JsonEnvelope);
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Envelope JSON is null or empty.";
                return false;
            }

            int byteCount = Encoding.UTF8.GetByteCount(json);
            if (byteCount > MaxMessageBytes)
            {
                error = "Envelope exceeds max message size of 64 KB (" + byteCount + " bytes).";
                return false;
            }

            Dictionary<string, RawJsonValue> fields;
            if (!TryParseJsonObjectFields(json, 0, json.Length, out fields, out error))
                return false;

            RawJsonValue typeVal;
            if (!fields.TryGetValue("type", out typeVal) || typeVal.Kind != JsonValueKind.String || string.IsNullOrWhiteSpace(typeVal.StringValue))
            {
                error = "Envelope missing required non-empty string field 'type'.";
                return false;
            }

            RawJsonValue vVal;
            int version;
            if (!fields.TryGetValue("v", out vVal) || vVal.Kind != JsonValueKind.Number ||
                !TryParseIntegralInt(vVal.RawText, out version))
            {
                error = "Envelope missing required integer field 'v'.";
                return false;
            }

            RawJsonValue tsVal;
            long timestamp;
            if (!fields.TryGetValue("ts", out tsVal) || tsVal.Kind != JsonValueKind.Number ||
                !TryParseIntegralLong(tsVal.RawText, out timestamp) || timestamp < 0)
            {
                error = "Envelope missing required non-negative integer field 'ts'.";
                return false;
            }

            RawJsonValue payloadVal;
            if (!fields.TryGetValue("payload", out payloadVal) || payloadVal.Kind != JsonValueKind.Object)
            {
                error = "Envelope missing required object field 'payload'.";
                return false;
            }

            envelope = new JsonEnvelope
            {
                type = typeVal.StringValue,
                v = version,
                ts = timestamp,
                payload = payloadVal.RawText
            };
            return true;
        }

        public string GetPayloadString(string key)
        {
            if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrEmpty(key))
                return null;

            Dictionary<string, RawJsonValue> fields;
            string error;
            if (!TryParseJsonObjectFields(payload, 0, payload.Length, out fields, out error))
                return null;

            RawJsonValue val;
            if (fields.TryGetValue(key, out val) && val.Kind == JsonValueKind.String)
                return val.StringValue;
            return null;
        }

        public bool TryGetPayloadLong(string key, out long value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrEmpty(key))
                return false;

            Dictionary<string, RawJsonValue> fields;
            string error;
            if (!TryParseJsonObjectFields(payload, 0, payload.Length, out fields, out error))
                return false;

            RawJsonValue val;
            if (fields.TryGetValue(key, out val) && val.Kind == JsonValueKind.Number)
                return TryParseIntegralLong(val.RawText, out value);
            return false;
        }

        public bool TryGetPayloadBool(string key, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrEmpty(key))
                return false;

            Dictionary<string, RawJsonValue> fields;
            string error;
            if (!TryParseJsonObjectFields(payload, 0, payload.Length, out fields, out error))
                return false;

            RawJsonValue val;
            if (fields.TryGetValue(key, out val) && val.Kind == JsonValueKind.Boolean)
            {
                value = string.Equals(val.RawText, "true", StringComparison.Ordinal);
                return true;
            }
            return false;
        }

        public static string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            StringBuilder sb = new StringBuilder(input.Length + 8);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                switch (c)
                {
                    case '\"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        private enum JsonValueKind
        {
            String,
            Number,
            Object,
            Array,
            Boolean,
            Null
        }

        private struct RawJsonValue
        {
            public JsonValueKind Kind;
            public string RawText;
            public string StringValue;
        }

        private static bool TryParseJsonObjectFields(
            string text,
            int start,
            int end,
            out Dictionary<string, RawJsonValue> fields,
            out string error)
        {
            int idx = SkipWhitespace(text, start, end);
            if (!TryReadJsonObjectInternal(text, ref idx, end, out fields, out error))
                return false;

            idx = SkipWhitespace(text, idx, end);
            if (idx != end)
            {
                error = "Unexpected trailing characters after JSON object.";
                return false;
            }
            return true;
        }

        private static bool TryReadJsonObjectInternal(
            string text,
            ref int idx,
            int end,
            out Dictionary<string, RawJsonValue> fields,
            out string error)
        {
            fields = new Dictionary<string, RawJsonValue>(StringComparer.Ordinal);
            error = null;

            idx = SkipWhitespace(text, idx, end);
            if (idx >= end || text[idx] != '{')
            {
                error = "Expected '{' at start of JSON object.";
                return false;
            }
            idx++;

            idx = SkipWhitespace(text, idx, end);
            if (idx < end && text[idx] == '}')
            {
                idx++;
                return true;
            }

            while (idx < end)
            {
                idx = SkipWhitespace(text, idx, end);
                if (idx >= end || text[idx] != '\"')
                {
                    error = "Expected quoted property name in JSON object.";
                    return false;
                }

                string key;
                if (!TryReadJsonString(text, ref idx, end, out key, out error))
                    return false;

                idx = SkipWhitespace(text, idx, end);
                if (idx >= end || text[idx] != ':')
                {
                    error = "Expected ':' after property '" + key + "'.";
                    return false;
                }
                idx++;

                idx = SkipWhitespace(text, idx, end);
                RawJsonValue val;
                if (!TryReadJsonValue(text, ref idx, end, out val, out error))
                    return false;

                fields[key] = val;

                idx = SkipWhitespace(text, idx, end);
                if (idx >= end)
                {
                    error = "Unterminated JSON object.";
                    return false;
                }

                if (text[idx] == ',')
                {
                    idx++;
                    idx = SkipWhitespace(text, idx, end);
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

        private static bool TryValidateJsonArray(string text, ref int idx, int end, out string error)
        {
            error = null;
            idx = SkipWhitespace(text, idx, end);
            if (idx >= end || text[idx] != '[')
            {
                error = "Expected '[' at start of JSON array.";
                return false;
            }
            idx++;

            idx = SkipWhitespace(text, idx, end);
            if (idx < end && text[idx] == ']')
            {
                idx++;
                return true;
            }

            while (idx < end)
            {
                idx = SkipWhitespace(text, idx, end);
                RawJsonValue elem;
                if (!TryReadJsonValue(text, ref idx, end, out elem, out error))
                    return false;

                idx = SkipWhitespace(text, idx, end);
                if (idx >= end)
                {
                    error = "Unterminated JSON array.";
                    return false;
                }

                if (text[idx] == ',')
                {
                    idx++;
                    idx = SkipWhitespace(text, idx, end);
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

        private static bool TryReadJsonValue(string text, ref int idx, int end, out RawJsonValue value, out string error)
        {
            value = default(RawJsonValue);
            error = null;

            if (idx >= end)
            {
                error = "Unexpected end of JSON while reading value.";
                return false;
            }

            char c = text[idx];
            if (c == '\"')
            {
                int valStart = idx;
                string decoded;
                if (!TryReadJsonString(text, ref idx, end, out decoded, out error))
                    return false;
                value = new RawJsonValue
                {
                    Kind = JsonValueKind.String,
                    RawText = text.Substring(valStart, idx - valStart),
                    StringValue = decoded
                };
                return true;
            }

            if (c == '{')
            {
                int valStart = idx;
                Dictionary<string, RawJsonValue> nestedFields;
                if (!TryReadJsonObjectInternal(text, ref idx, end, out nestedFields, out error))
                    return false;
                value = new RawJsonValue
                {
                    Kind = JsonValueKind.Object,
                    RawText = text.Substring(valStart, idx - valStart),
                    StringValue = null
                };
                return true;
            }

            if (c == '[')
            {
                int valStart = idx;
                if (!TryValidateJsonArray(text, ref idx, end, out error))
                    return false;
                value = new RawJsonValue
                {
                    Kind = JsonValueKind.Array,
                    RawText = text.Substring(valStart, idx - valStart),
                    StringValue = null
                };
                return true;
            }

            if (MatchLiteral(text, ref idx, end, "true"))
            {
                value = new RawJsonValue { Kind = JsonValueKind.Boolean, RawText = "true" };
                return true;
            }

            if (MatchLiteral(text, ref idx, end, "false"))
            {
                value = new RawJsonValue { Kind = JsonValueKind.Boolean, RawText = "false" };
                return true;
            }

            if (MatchLiteral(text, ref idx, end, "null"))
            {
                value = new RawJsonValue { Kind = JsonValueKind.Null, RawText = "null" };
                return true;
            }

            if (c == '-' || (c >= '0' && c <= '9'))
            {
                int valStart = idx;
                if (!TryReadJsonNumber(text, ref idx, end, out error))
                    return false;
                value = new RawJsonValue
                {
                    Kind = JsonValueKind.Number,
                    RawText = text.Substring(valStart, idx - valStart)
                };
                return true;
            }

            error = "Unexpected character '" + c + "' in JSON value.";
            return false;
        }

        private static bool TryReadJsonString(string text, ref int idx, int end, out string result, out string error)
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
                                if (!IsHexDigit(text[idx + k]))
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

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static bool TryParseIntegralInt(string rawText, out int value)
        {
            if (int.TryParse(rawText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;

            double d;
            if (double.TryParse(rawText, NumberStyles.Float, CultureInfo.InvariantCulture, out d) &&
                !double.IsNaN(d) && !double.IsInfinity(d) &&
                Math.Floor(d) == d && d >= int.MinValue && d <= int.MaxValue)
            {
                value = (int)d;
                return true;
            }
            return false;
        }

        private static bool TryParseIntegralLong(string rawText, out long value)
        {
            if (long.TryParse(rawText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;

            decimal dec;
            if (decimal.TryParse(rawText, NumberStyles.Float, CultureInfo.InvariantCulture, out dec) &&
                decimal.Truncate(dec) == dec && dec >= long.MinValue && dec <= long.MaxValue)
            {
                value = (long)dec;
                return true;
            }
            return false;
        }

        private static bool TryReadJsonNumber(string text, ref int idx, int end, out string error)
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

            char firstDigit = text[idx++];
            if (firstDigit == '0')
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

            double parsed;
            string slice = text.Substring(start, idx - start);
            if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
                double.IsNaN(parsed) || double.IsInfinity(parsed))
            {
                error = "Invalid numeric literal '" + slice + "'.";
                return false;
            }
            return true;
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

        private static int SkipWhitespace(string text, int idx, int end)
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
