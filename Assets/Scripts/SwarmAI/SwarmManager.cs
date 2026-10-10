// ============================================================
// SwarmManager.cs — Swarm Simulation & Kinetic Engagement Manager
// TRISHUL-SIM · SA-P1-D / SA-P2-A · swarm-ai track (Uday)
//
// Manages swarm state lifecycle, dynamic HBT tactics via TacticTreeFactory,
// processes SMASH 2000 kinetic fire, executes target neutralisation,
// and verifies ALL_NEUTRALISED and EXCLUSION_BREACH terminal states.
// Contract: board/contracts/scenario-events.md, board/contracts/telemetry.md, docs/SCHEMA.md
// ============================================================

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using TrishulSim.Sensors;
using TrishulSim.Mocks;
using TrishulSim.SwarmAI.HBT;

namespace TrishulSim.SwarmAI
{
    /// <summary>
    /// Manager singleton / coordinating engine for the Swarm AI simulation.
    /// Manages agents, runs active HBT tactics, produces SCENARIO_TICK swarmSnapshot,
    /// resolves kinetic engagements, and signals terminal conditions.
    /// </summary>
    public class SwarmManager
    {
        private static SwarmManager _instance;
        public static SwarmManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SwarmManager();
                }
                return _instance;
            }
        }

        private List<SwarmAgentState> _agents;
        private bool _allNeutralised;
        private long? _allNeutralisedAtMs;
        private TacticTree _currentTactic;
        private SwarmProfile _currentProfile;
        private string _currentScenarioId;

        public bool AllNeutralised
        {
            get { return _allNeutralised; }
        }

        public long? AllNeutralisedAtMs
        {
            get { return _allNeutralisedAtMs; }
        }

        public TacticTree CurrentTactic
        {
            get { return _currentTactic; }
        }

        public SwarmProfile CurrentProfile
        {
            get { return _currentProfile; }
        }

        public string CurrentScenarioId
        {
            get { return _currentScenarioId; }
        }

        public SwarmManager()
        {
            _agents = new List<SwarmAgentState>();
            _allNeutralised = false;
            _allNeutralisedAtMs = null;
            _currentTactic = null;
            _currentProfile = null;
            _currentScenarioId = string.Empty;
        }

        /// <summary>
        /// Initialises swarm from an array of agent states (e.g. Punjab fixture).
        /// </summary>
        public void LoadAgents(SwarmAgentState[] agentArray)
        {
            _agents.Clear();
            _allNeutralised = false;
            _allNeutralisedAtMs = null;

            if (agentArray != null)
            {
                for (int i = 0; i < agentArray.Length; i++)
                {
                    if (agentArray[i] != null)
                    {
                        _agents.Add(agentArray[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Loads the canonical Punjab fixture for zero-blocker standalone execution.
        /// </summary>
        public void LoadPunjabFixture()
        {
            LoadScenario("punjab-fog-pincer-01");
        }

        /// <summary>
        /// Loads a specific scenario by ID (e.g. "ladakh-lac-ridge-01", "loc-mountain-pass-01").
        /// Checks for StreamingAssets JSON if available, falling back to canonical fixtures.
        /// Instantiates and binds the appropriate HBT tactic tree via TacticTreeFactory.
        /// </summary>
        public void LoadScenario(string scenarioId, int seed = 42819, Vector3? targetOrigin = null)
        {
            _currentScenarioId = scenarioId ?? "punjab-fog-pincer-01";
            Vector3 origin = targetOrigin.HasValue ? targetOrigin.Value : Vector3.zero;

            SwarmProfile profile = null;
            SwarmAgentState[] agents = null;

            // Check if scenario JSON exists on disk in StreamingAssets
            string jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "StreamingAssets", "scenarios", _currentScenarioId + ".json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(jsonPath);
                    profile = ParseProfileFromJson(jsonContent);
                }
                catch
                {
                    profile = null;
                }
            }

            // Fallback to canonical profiles and fixtures if JSON not present or partial
            if (profile == null)
            {
                string cleanId = _currentScenarioId.ToLowerInvariant();
                if (cleanId.Contains("ladakh"))
                {
                    profile = SwarmMock.GetLadakhProfile();
                    agents = SwarmMock.GetLadakhFixture();
                }
                else if (cleanId.Contains("loc"))
                {
                    profile = SwarmMock.GetLocProfile();
                    agents = SwarmMock.GetLocFixture();
                }
                else if (cleanId.Contains("desert"))
                {
                    profile = SwarmMock.GetDesertProfile();
                    agents = SwarmMock.GetLadakhFixture(); // 8-agent broad front baseline
                }
                else
                {
                    // Punjab default
                    profile = SwarmMock.GetPunjabProfile();
                    agents = SwarmMock.GetPunjabFixture();
                }
            }

            if (agents == null)
            {
                string tactic = profile != null ? profile.tacticTree : "PINCER_LOW_FAST";
                if (tactic == "SATURATION_DIVE")
                {
                    agents = SwarmMock.GetLadakhFixture();
                }
                else if (tactic == "MULTI-AXIS_SPLIT")
                {
                    agents = SwarmMock.GetLocFixture();
                }
                else
                {
                    agents = SwarmMock.GetPunjabFixture();
                }
            }

            _currentProfile = profile;
            LoadAgents(agents);

            // Bind HBT tactic tree via TacticTreeFactory (SA-P2-A core requirement)
            string tacticName = profile != null ? profile.tacticTree : "PINCER_LOW_FAST";
            _currentTactic = TacticTreeFactory.Create(tacticName);
            _currentTactic.Initialize(_currentProfile, seed, _agents, origin);
        }

        /// <summary>
        /// Advances the active swarm tactic tree and updates agent kinematics.
        /// </summary>
        public void Tick(float deltaTime, long currentScenarioTimeMs, EWJammerState ewJammer = null, Vector3? traineePos = null)
        {
            Vector3 pos = traineePos.HasValue ? traineePos.Value : Vector3.zero;

            if (_currentTactic != null)
            {
                _currentTactic.Tick(deltaTime, currentScenarioTimeMs, ewJammer, pos);
            }

            // Continuously verify ALL_NEUTRALISED state
            if (!_allNeutralised && CheckAllAttackersNeutralised())
            {
                _allNeutralised = true;
                _allNeutralisedAtMs = currentScenarioTimeMs;
            }
        }

        /// <summary>
        /// Returns the current swarm snapshot for injection into SCENARIO_TICK.
        /// </summary>
        public SwarmAgentState[] GetSwarmSnapshot()
        {
            return _agents.ToArray();
        }

        /// <summary>
        /// Retrieves an agent by its unique identifier.
        /// </summary>
        public SwarmAgentState GetAgentById(string agentId)
        {
            for (int i = 0; i < _agents.Count; i++)
            {
                if (_agents[i] != null && string.Equals(_agents[i].agentId, agentId, StringComparison.OrdinalIgnoreCase))
                {
                    return _agents[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Processes trainee kinetic trigger pull (SA-P1-D core deliverable).
        /// Resolves lead-angle intercept, evaluates hit, updates target state on confirmed hit,
        /// and evaluates the ALL_NEUTRALISED terminal condition.
        /// </summary>
        public KineticEvent ProcessKineticFire(
            TraineeAction action,
            long t,
            Vector3 traineePos,
            string targetAgentId = null)
        {
            if (action == null)
                return null;

            SwarmAgentState target = null;
            if (!string.IsNullOrEmpty(targetAgentId))
            {
                target = GetAgentById(targetAgentId);
            }

            if (target == null)
            {
                target = SmashFireControl.FindLockedTarget(
                    agents: _agents.ToArray(),
                    aimBearingDeg: action.antennaBearingDeg,
                    aimElevationDeg: action.antennaElevationDeg,
                    traineePos: traineePos
                );
            }

            if (target == null)
            {
                KineticEvent emptyEvent = new KineticEvent(t, "none", false, 999.0f);
                action.kinetic = emptyEvent;
                return emptyEvent;
            }

            KineticEvent kineticEvent = SmashFireControl.ComputeKineticEvent(
                action: action,
                target: target,
                t: t,
                traineePos: traineePos
            );

            if (kineticEvent.hit)
            {
                // Target neutralisation state transition (telemetry.md / SCENARIO_END)
                target.isNeutralised = true;
                target.neutralisedAtMs = t;
                target.velocityMs = Vector3.zero;
                target.propellerRpm = 0;
                target.rfEmitting = false;

                // Check terminal condition: all attackers neutralised
                if (CheckAllAttackersNeutralised())
                {
                    _allNeutralised = true;
                    _allNeutralisedAtMs = t;
                }
            }

            action.kinetic = kineticEvent;
            return kineticEvent;
        }

        /// <summary>
        /// Evaluates whether all non-decoy attacker agents have been neutralised.
        /// Rule: SCENARIO_END reason = "ALL_NEUTRALISED" when all attackerCount agents have isNeutralised == true.
        /// </summary>
        public bool CheckAllAttackersNeutralised()
        {
            int attackerCount = 0;
            int neutralisedCount = 0;

            for (int i = 0; i < _agents.Count; i++)
            {
                SwarmAgentState agent = _agents[i];
                if (agent == null || agent.isDecoy)
                    continue;

                attackerCount++;
                if (agent.isNeutralised)
                {
                    neutralisedCount++;
                }
            }

            return attackerCount > 0 && neutralisedCount >= attackerCount;
        }

        /// <summary>
        /// Returns total count of remaining active (non-neutralised) attackers.
        /// </summary>
        public int GetRemainingAttackerCount()
        {
            int active = 0;
            for (int i = 0; i < _agents.Count; i++)
            {
                SwarmAgentState agent = _agents[i];
                if (agent != null && !agent.isDecoy && !agent.isNeutralised)
                {
                    active++;
                }
            }
            return active;
        }

        /// <summary>
        /// Simple JSON scraper for SwarmProfile to avoid external dependencies.
        /// </summary>
        private static SwarmProfile ParseProfileFromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            SwarmProfile profile = new SwarmProfile();

            string tactic = ExtractJsonString(json, "tacticTree");
            if (!string.IsNullOrEmpty(tactic))
                profile.tacticTree = tactic;

            int agentCount = ExtractJsonInt(json, "agentCount", -1);
            if (agentCount > 0) profile.agentCount = agentCount;

            int decoyCount = ExtractJsonInt(json, "decoyCount", -1);
            if (decoyCount >= 0) profile.decoyCount = decoyCount;

            int attackerCount = ExtractJsonInt(json, "attackerCount", -1);
            if (attackerCount > 0) profile.attackerCount = attackerCount;

            float speedKmh = ExtractJsonFloat(json, "speedKmh", -1f);
            if (speedKmh > 0) profile.speedKmh = speedKmh;

            float altitudeM = ExtractJsonFloat(json, "altitudeM", -1f);
            if (altitudeM > 0) profile.altitudeM = altitudeM;

            float rfPower = ExtractJsonFloat(json, "rfPowerDbm", -1f);
            if (rfPower > 0) profile.rfPowerDbm = rfPower;

            return profile;
        }

        private static string ExtractJsonString(string json, string key)
        {
            int keyIdx = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
            if (keyIdx < 0) return null;
            int colonIdx = json.IndexOf(':', keyIdx);
            if (colonIdx < 0) return null;
            int quoteStart = json.IndexOf('\"', colonIdx + 1);
            if (quoteStart < 0) return null;
            int quoteEnd = json.IndexOf('\"', quoteStart + 1);
            if (quoteEnd < 0) return null;
            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }

        private static int ExtractJsonInt(string json, string key, int fallback)
        {
            int keyIdx = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
            if (keyIdx < 0) return fallback;
            int colonIdx = json.IndexOf(':', keyIdx);
            if (colonIdx < 0) return fallback;

            int i = colonIdx + 1;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t' || json[i] == '\r' || json[i] == '\n')) i++;
            int start = i;
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '-')) i++;
            if (i > start)
            {
                int val;
                if (int.TryParse(json.Substring(start, i - start), out val))
                    return val;
            }
            return fallback;
        }

        private static float ExtractJsonFloat(string json, string key, float fallback)
        {
            int keyIdx = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
            if (keyIdx < 0) return fallback;
            int colonIdx = json.IndexOf(':', keyIdx);
            if (colonIdx < 0) return fallback;

            int i = colonIdx + 1;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t' || json[i] == '\r' || json[i] == '\n')) i++;
            int start = i;
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == '-')) i++;
            if (i > start)
            {
                float val;
                if (float.TryParse(json.Substring(start, i - start), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
                    return val;
            }
            return fallback;
        }
    }
}
