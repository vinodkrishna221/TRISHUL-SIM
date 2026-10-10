// ============================================================
// SessionManifest.cs — Session Manifest & Summary Model
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Implements docs/SCHEMA.md Entity 5 and board/contracts/telemetry.md.
// Represents sessions/manifest.json index file in %AppData%\TRISHUL-SIM\sessions\
// for session catalogue, scenario replay, and AAR reporting.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TrishulSim.Network;
using TrishulSim.Scenarios;
using UnityEngine;

namespace TrishulSim.AAR
{
    /// <summary>
    /// SessionSummary matching docs/SCHEMA.md Entity 5.
    /// Represents a single recorded scenario session in the manifest index.
    /// </summary>
    [Serializable]
    public class SessionSummary
    {
        public string sessionId = string.Empty;
        public string scenarioId = string.Empty;
        public string completedAtIso = string.Empty;
        public bool doctrinePassed;
        public string scoreFilePath = string.Empty;
        public bool partial;

        public SessionSummary()
        {
            sessionId = string.Empty;
            scenarioId = string.Empty;
            completedAtIso = string.Empty;
            doctrinePassed = false;
            scoreFilePath = string.Empty;
            partial = false;
        }

        public SessionSummary(
            string sessionId,
            string scenarioId,
            string completedAtIso,
            bool doctrinePassed,
            string scoreFilePath,
            bool partial = false)
        {
            this.sessionId = sessionId ?? string.Empty;
            this.scenarioId = scenarioId ?? string.Empty;
            this.completedAtIso = completedAtIso ?? string.Empty;
            this.doctrinePassed = doctrinePassed;
            this.scoreFilePath = scoreFilePath ?? string.Empty;
            this.partial = partial;
        }

        public SessionSummary Clone()
        {
            return new SessionSummary(sessionId, scenarioId, completedAtIso, doctrinePassed, scoreFilePath, partial);
        }

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder(256);
            sb.Append("{\"sessionId\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(sessionId ?? string.Empty));
            sb.Append("\",\"scenarioId\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(scenarioId ?? string.Empty));
            sb.Append("\",\"completedAtIso\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(completedAtIso ?? string.Empty));
            sb.Append("\",\"doctrinePassed\":");
            sb.Append(doctrinePassed ? "true" : "false");
            sb.Append(",\"scoreFilePath\":\"");
            sb.Append(JsonEnvelope.EscapeJsonString(scoreFilePath ?? string.Empty));
            sb.Append("\",\"partial\":");
            sb.Append(partial ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        public static bool TryParse(string json, out SessionSummary summary, out string error)
        {
            summary = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "SessionSummary JSON is null or empty.";
                return false;
            }

            Dictionary<string, JsonToken> fields;
            if (!JsonParser.TryParseObject(json, out fields, out error))
                return false;

            string sId;
            if (!JsonParser.TryGetRequiredString(fields, "sessionId", out sId, out error))
                return false;

            string scId;
            if (!JsonParser.TryGetRequiredString(fields, "scenarioId", out scId, out error))
                return false;

            string completedIso;
            if (!JsonParser.TryGetRequiredString(fields, "completedAtIso", out completedIso, out error))
                return false;

            bool passed;
            if (!JsonParser.TryGetRequiredBool(fields, "doctrinePassed", out passed, out error))
                return false;

            string scorePath;
            if (!JsonParser.TryGetRequiredString(fields, "scoreFilePath", out scorePath, out error))
                return false;

            bool partialVal = false;
            if (fields.ContainsKey("partial"))
            {
                if (!JsonParser.TryGetRequiredBool(fields, "partial", out partialVal, out error))
                    return false;
            }

            summary = new SessionSummary(sId, scId, completedIso, passed, scorePath, partialVal);
            return true;
        }

        public static SessionSummary Parse(string json)
        {
            SessionSummary summary;
            string error;
            if (!TryParse(json, out summary, out error))
                throw new FormatException(error);
            return summary;
        }
    }

    /// <summary>
    /// SessionManifest matching docs/SCHEMA.md Entity 5.
    /// Lightweight JSON index file maintained at %AppData%\TRISHUL-SIM\sessions\manifest.json.
    /// </summary>
    [Serializable]
    public class SessionManifest
    {
        public List<SessionSummary> sessions = new List<SessionSummary>();

        public SessionManifest()
        {
            sessions = new List<SessionSummary>();
        }

        public SessionManifest(IEnumerable<SessionSummary> initialSessions)
        {
            sessions = initialSessions != null ? new List<SessionSummary>(initialSessions) : new List<SessionSummary>();
        }

        public void AddOrUpdate(SessionSummary summary)
        {
            if (summary == null || string.IsNullOrWhiteSpace(summary.sessionId))
                return;

            int index = sessions.FindIndex(s => string.Equals(s.sessionId, summary.sessionId, StringComparison.Ordinal));
            if (index >= 0)
            {
                sessions[index] = summary;
            }
            else
            {
                sessions.Add(summary);
            }
        }

        public string ToJson(bool pretty = false)
        {
            StringBuilder sb = new StringBuilder(512);
            if (!pretty)
            {
                sb.Append("{\"sessions\":[");
                for (int i = 0; i < sessions.Count; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    sb.Append(sessions[i] != null ? sessions[i].ToJson() : "{}");
                }
                sb.Append("]}");
            }
            else
            {
                sb.AppendLine("{");
                sb.AppendLine("  \"sessions\": [");
                for (int i = 0; i < sessions.Count; i++)
                {
                    sb.Append("    ");
                    sb.Append(sessions[i] != null ? sessions[i].ToJson() : "{}");
                    if (i < sessions.Count - 1)
                        sb.Append(',');
                    sb.AppendLine();
                }
                sb.AppendLine("  ]");
                sb.Append('}');
            }
            return sb.ToString();
        }

        public static bool TryParse(string json, out SessionManifest manifest, out string error)
        {
            manifest = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                manifest = new SessionManifest();
                return true;
            }

            Dictionary<string, JsonToken> fields;
            if (!JsonParser.TryParseObject(json, out fields, out error))
                return false;

            manifest = new SessionManifest();
            if (fields.ContainsKey("sessions"))
            {
                string[] sessionRaws;
                if (!JsonParser.TryGetRequiredObjectArrayRaw(fields, "sessions", out sessionRaws, out error))
                    return false;

                for (int i = 0; i < sessionRaws.Length; i++)
                {
                    SessionSummary item;
                    if (!SessionSummary.TryParse(sessionRaws[i], out item, out error))
                        return false;
                    manifest.sessions.Add(item);
                }
            }

            return true;
        }

        public static SessionManifest Parse(string json)
        {
            SessionManifest manifest;
            string error;
            if (!TryParse(json, out manifest, out error))
                throw new FormatException(error);
            return manifest;
        }

        /// <summary>
        /// Reads manifest.json from disk if present, or creates a clean empty instance.
        /// </summary>
        public static SessionManifest LoadOrCreate(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            {
                return new SessionManifest();
            }

            try
            {
                string content = File.ReadAllText(manifestPath, Encoding.UTF8);
                SessionManifest manifest;
                string error;
                if (TryParse(content, out manifest, out error) && manifest != null)
                {
                    return manifest;
                }
                Debug.LogWarning("[SessionManifest] Manifest parse warning: " + error + " — creating fresh manifest.");
                return new SessionManifest();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SessionManifest] Failed to read manifest (" + ex.Message + ") — creating fresh manifest.");
                return new SessionManifest();
            }
        }

        /// <summary>
        /// Atomically saves manifest to disk as UTF-8 JSON.
        /// </summary>
        public void Save(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
                throw new ArgumentException("manifestPath must not be empty.", "manifestPath");

            string dir = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = ToJson(true);
            string tempPath = manifestPath + ".tmp";
            try
            {
                File.WriteAllText(tempPath, json, Encoding.UTF8);
                if (File.Exists(manifestPath))
                {
                    File.Delete(manifestPath);
                }
                File.Move(tempPath, manifestPath);
            }
            catch
            {
                // Fallback to direct write if file move fails
                File.WriteAllText(manifestPath, json, Encoding.UTF8);
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }
    }
}
