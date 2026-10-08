using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace TrishulSim.Scenarios
{
    /// <summary>
    /// Scenario Library loader (US-P0-B).
    /// Reads all *.json scenario configs from Assets/StreamingAssets/scenarios/
    /// using Application.streamingAssetsPath and regenerates a procedural seed each run.
    /// </summary>
    public class ScenarioLibrary
    {
        public const string DefaultScenarioId = "punjab-fog-pincer-01";
        public const int MinProceduralSeed = 10000;
        public const int MaxProceduralSeed = 99999;

        public static string GetDefaultScenariosDirectory()
        {
            string streamingPath = Application.streamingAssetsPath;
            if (!string.IsNullOrWhiteSpace(streamingPath))
            {
                string candidate = Path.Combine(streamingPath, "scenarios");
                if (Directory.Exists(candidate))
                    return candidate;
            }

            string repoFallback = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "StreamingAssets", "scenarios");
            if (Directory.Exists(repoFallback))
                return repoFallback;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
            {
                string appDataCandidate = Path.Combine(appData, "TRISHUL-SIM", "scenarios");
                if (Directory.Exists(appDataCandidate))
                    return appDataCandidate;
            }

            return Path.Combine(streamingPath ?? string.Empty, "scenarios");
        }

        public static int GenerateProceduralSeed()
        {
            return UnityEngine.Random.Range(MinProceduralSeed, MaxProceduralSeed);
        }

        public static List<ScenarioConfig> LoadAll()
        {
            return LoadAll(null, true);
        }

        public static List<ScenarioConfig> LoadAll(string scenariosDirectory, bool regenerateSeeds = true)
        {
            string dir = string.IsNullOrWhiteSpace(scenariosDirectory)
                ? GetDefaultScenariosDirectory()
                : scenariosDirectory;

            List<ScenarioConfig> results = new List<ScenarioConfig>();
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Debug.LogWarning("[ScenarioLibrary] Scenarios directory not found: " + (dir ?? "null"));
                return results;
            }

            string[] files = Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                try
                {
                    string rawJson = File.ReadAllText(filePath, Encoding.UTF8);
                    ScenarioConfig config;
                    string parseError;
                    if (!ScenarioConfig.TryParse(rawJson, out config, out parseError))
                    {
                        Debug.LogWarning("[ScenarioLibrary] Skipping invalid scenario JSON '" + Path.GetFileName(filePath) + "': " + parseError);
                        continue;
                    }

                    if (regenerateSeeds)
                    {
                        config.seed = GenerateProceduralSeed();
                    }

                    results.Add(config);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[ScenarioLibrary] Failed reading scenario file '" + Path.GetFileName(filePath) + "': " + ex.Message);
                }
            }

            // Ensure punjab-fog-pincer-01 is index 0 for Beat 1 default selection, then alphabetical by id.
            results.Sort(CompareScenarioOrder);
            return results;
        }

        public static ScenarioConfig FindById(string scenarioId, string scenariosDirectory = null, bool regenerateSeed = false)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
                return null;

            List<ScenarioConfig> all = LoadAll(scenariosDirectory, regenerateSeed);
            for (int i = 0; i < all.Count; i++)
            {
                if (string.Equals(all[i].id, scenarioId, StringComparison.Ordinal))
                    return all[i];
            }
            return null;
        }

        public static bool Exists(string scenarioId, string scenariosDirectory = null)
        {
            return FindById(scenarioId, scenariosDirectory, false) != null;
        }

        private static int CompareScenarioOrder(ScenarioConfig a, ScenarioConfig b)
        {
            if (ReferenceEquals(a, b))
                return 0;
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            bool aIsPrimary = string.Equals(a.id, DefaultScenarioId, StringComparison.Ordinal);
            bool bIsPrimary = string.Equals(b.id, DefaultScenarioId, StringComparison.Ordinal);
            if (aIsPrimary && !bIsPrimary)
                return -1;
            if (!aIsPrimary && bIsPrimary)
                return 1;

            return string.Compare(a.id, b.id, StringComparison.Ordinal);
        }
    }
}
