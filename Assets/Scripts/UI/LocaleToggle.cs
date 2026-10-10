// ============================================================
// LocaleToggle.cs — Runtime Hindi/English Language Toggle
// TRISHUL-SIM · UX-P0-B · ui-ux track (Rani)
//
// Manages bilingual localization: loads CSV string tables,
// provides runtime toggle, and exposes GetString(key) for
// all UI controllers. Uses a lightweight CSV-based approach
// that works offline (air-gap requirement) without the Unity
// Localization package editor dependency.
//
// Architecture: docs/ARCHITECTURE.md §1 —
//   "Unity Localization package (CSV backend, bundled);
//    offline-capable; runtime language toggle"
//
// This implementation uses a pure-C# CSV parser so the
// localization system works in builds AND in test harnesses
// (UiShellSelfTest) without Unity Editor services.
// The Unity Localization package import (com.unity.localization)
// can be done later to add editor-time LocalizedString bindings;
// this runtime layer remains the single source of truth.
// ============================================================

using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TrishulSim.UI
{
    /// <summary>
    /// Supported UI locales. Matches Project Settings → Localization
    /// locale codes: en (English), hi (Hindi).
    /// </summary>
    public enum TrishulLocale
    {
        English,
        Hindi
    }

    /// <summary>
    /// Singleton-style localization manager. Loads CSV string tables
    /// from Assets/Localization/, provides key lookup, and fires
    /// an event on language change so controllers can rebind labels.
    ///
    /// Usage:
    ///   LocaleToggle.Instance.ToggleLanguage();
    ///   string label = LocaleToggle.Instance.GetString("LAUNCH_BTN");
    ///
    /// CSV format: Key,Value (first row is header, skipped).
    /// Values may contain {0}, {0:F1} etc. — caller uses string.Format.
    /// </summary>
    public class LocaleToggle : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────
        private static LocaleToggle _instance;
        public static LocaleToggle Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Auto-create if not in scene (e.g. test harness)
                    var go = new GameObject("[LocaleToggle]");
                    _instance = go.AddComponent<LocaleToggle>();
                    DontDestroyOnLoad(go);
#if !UNITY_5_3_OR_NEWER
                    _instance.LoadTables();
#endif
                }
                return _instance;
            }
        }

        // ── State ──────────────────────────────────────────────
        private TrishulLocale currentLocale = TrishulLocale.English;
        private Dictionary<string, string> enTable = new Dictionary<string, string>();
        private Dictionary<string, string> hiTable = new Dictionary<string, string>();
        private bool tablesLoaded = false;

        /// <summary>Current active locale.</summary>
        public TrishulLocale CurrentLocale { get { return currentLocale; } }

        /// <summary>True if Hindi is the active locale.</summary>
        public bool IsHindi { get { return currentLocale == TrishulLocale.Hindi; } }

        /// <summary>
        /// Fired when the locale changes. Controllers subscribe to
        /// rebind all label text. Arg = new locale.
        /// </summary>
        public event Action<TrishulLocale> OnLocaleChanged;

        // ── Public Accessors for Tests ─────────────────────────
        public int EnglishKeyCount { get { return enTable.Count; } }
        public int HindiKeyCount { get { return hiTable.Count; } }
        public bool TablesLoaded { get { return tablesLoaded; } }

        // ══════════════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadTables();
        }

        // ══════════════════════════════════════════════════════
        // CSV Loading
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Loads both CSV string tables from Resources or file path.
        /// Called automatically on Awake. Can be called manually
        /// for test injection.
        /// </summary>
        public void LoadTables()
        {
            enTable = LoadCsvTable("StringTable_en");
            hiTable = LoadCsvTable("StringTable_hi");
            tablesLoaded = enTable.Count > 0;
            Debug.Log(string.Format("[LocaleToggle] Loaded {0} EN keys, {1} HI keys.",
                enTable.Count, hiTable.Count));
        }

        /// <summary>
        /// Loads a CSV string table from a TextAsset in Resources/Localization/.
        /// Falls back to direct file read from Assets/Localization/ for editor.
        /// CSV format: Key,Value (header row skipped).
        /// </summary>
        private Dictionary<string, string> LoadCsvTable(string tableName)
        {
            var table = new Dictionary<string, string>();

            // Try Unity Resources first (works in builds if CSVs are in Resources/)
            TextAsset asset = Resources.Load<TextAsset>("Localization/" + tableName);
            string csvText = null;

            if (asset != null)
            {
                csvText = asset.text;
            }
            else
            {
                // Fallback: direct file read (editor / dev builds)
#if UNITY_5_3_OR_NEWER
                string basePath = Application.dataPath;
#else
                string basePath = "Assets";
#endif
                string path = Path.Combine(basePath, "Localization", tableName + ".csv");
                if (File.Exists(path))
                {
                    csvText = File.ReadAllText(path, Encoding.UTF8);
                }
                else
                {
                    Debug.LogWarning(string.Format(
                        "[LocaleToggle] CSV not found: {0}. Tried Resources and {1}",
                        tableName, path));
                    return table;
                }
            }

            ParseCsv(csvText, table);
            return table;
        }

        /// <summary>
        /// Injects pre-built tables for testing (bypasses file I/O).
        /// </summary>
        public void InjectTables(Dictionary<string, string> en, Dictionary<string, string> hi)
        {
            enTable = en ?? new Dictionary<string, string>();
            hiTable = hi ?? new Dictionary<string, string>();
            tablesLoaded = enTable.Count > 0;
        }

        /// <summary>
        /// Parses CSV text into a key-value dictionary.
        /// Handles quoted values with commas inside.
        /// Skips the header row (Key,Value).
        /// </summary>
        public static void ParseCsv(string csvText, Dictionary<string, string> table)
        {
            if (string.IsNullOrEmpty(csvText)) return;

            string[] lines = csvText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            bool headerSkipped = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (!headerSkipped)
                {
                    // Skip the header row "Key,Value"
                    if (line.StartsWith("Key", StringComparison.OrdinalIgnoreCase))
                    {
                        headerSkipped = true;
                        continue;
                    }
                    headerSkipped = true;
                }

                // Parse: Key,"Value" or Key,Value
                int commaIdx = line.IndexOf(',');
                if (commaIdx < 0) continue;

                string key = line.Substring(0, commaIdx).Trim();
                string val = line.Substring(commaIdx + 1).Trim();

                // Strip surrounding quotes if present
                if (val.Length >= 2 && val[0] == '"' && val[val.Length - 1] == '"')
                {
                    val = val.Substring(1, val.Length - 2);
                }

                if (!string.IsNullOrEmpty(key) && !table.ContainsKey(key))
                {
                    table[key] = val;
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // String Lookup
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Returns the localized string for the given key in the
        /// current locale. Falls back to English if the Hindi key
        /// is missing. Returns the key itself if not found at all.
        /// </summary>
        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";

            Dictionary<string, string> activeTable =
                currentLocale == TrishulLocale.Hindi ? hiTable : enTable;

            string val;
            if (activeTable.TryGetValue(key, out val))
                return val;

            // Fallback to English
            if (currentLocale == TrishulLocale.Hindi && enTable.TryGetValue(key, out val))
            {
                Debug.LogWarning(string.Format(
                    "[LocaleToggle] Hindi key missing, falling back to EN: {0}", key));
                return val;
            }

            // Key not found anywhere — return the key itself as a visible indicator
            Debug.LogWarning(string.Format("[LocaleToggle] Key not found: {0}", key));
            return key;
        }

        /// <summary>
        /// Convenience: GetString + string.Format with one argument.
        /// </summary>
        public string GetFormatted(string key, object arg0)
        {
            return string.Format(GetString(key), arg0);
        }

        /// <summary>
        /// Convenience: GetString + string.Format with two arguments.
        /// </summary>
        public string GetFormatted(string key, object arg0, object arg1)
        {
            return string.Format(GetString(key), arg0, arg1);
        }

        // ══════════════════════════════════════════════════════
        // Language Toggle
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Toggles between English and Hindi. Fires OnLocaleChanged
        /// so all subscribed controllers rebind their labels.
        /// </summary>
        public void ToggleLanguage()
        {
            currentLocale = currentLocale == TrishulLocale.English
                ? TrishulLocale.Hindi
                : TrishulLocale.English;

            Debug.Log(string.Format("[LocaleToggle] Locale changed to: {0}", currentLocale));

            if (OnLocaleChanged != null)
                OnLocaleChanged.Invoke(currentLocale);
        }

        /// <summary>
        /// Sets a specific locale. Fires OnLocaleChanged.
        /// </summary>
        public void SetLocale(TrishulLocale locale)
        {
            if (currentLocale == locale) return;
            currentLocale = locale;
            Debug.Log(string.Format("[LocaleToggle] Locale set to: {0}", currentLocale));

            if (OnLocaleChanged != null)
                OnLocaleChanged.Invoke(currentLocale);
        }
    }
}
