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
    public class SwarmManager : MonoBehaviour
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
        private List<BoidAgent> _boidAgents;
        private bool _allNeutralised;
        private long? _allNeutralisedAtMs;
        private TacticTree _currentTactic;
        private SwarmProfile _currentProfile;
        private string _currentScenarioId;
        private int _currentSeed;
        private SensorSuiteComputer _sensorSuiteComputer;
        private Vector3 _antennaPosition = Vector3.zero;
        private SensorReadings _lastSensorReadings;

        public GameObject swarmAgentPrefab;
        public event Action<SwarmAgentState[]> OnSwarmSnapshotInjected;
        public event Action<SensorReadings> OnSensorReadingsInjected;

        public IReadOnlyList<BoidAgent> BoidAgents
        {
            get { return _boidAgents; }
        }

        public SensorSuiteComputer SensorSuiteComputer
        {
            get { return _sensorSuiteComputer; }
        }

        public Vector3 AntennaPosition
        {
            get { return _antennaPosition; }
            set { _antennaPosition = value; }
        }

        public SensorReadings LastSensorReadings
        {
            get { return _lastSensorReadings; }
        }

        /// <summary>
        /// SA-P0-B Step 3: Registers a SensorSuiteComputer instance with SwarmManager to compute
        /// SensorReadings after each GetSwarmSnapshot() call.
        /// </summary>
        public void RegisterSensorSuiteComputer(SensorSuiteComputer computer)
        {
            _sensorSuiteComputer = computer ?? new SensorSuiteComputer();
            if (!string.IsNullOrEmpty(_currentScenarioId))
            {
                _sensorSuiteComputer.ConfigureScenario(_currentScenarioId);
            }
        }

        public int CurrentSeed
        {
            get { return _currentSeed; }
        }

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

        private BoidFlockController _flockController;
        public BoidFlockController FlockController
        {
            get { return _flockController; }
        }

        public SwarmManager()
        {
            _agents = new List<SwarmAgentState>();
            _boidAgents = new List<BoidAgent>();
            _flockController = new BoidFlockController();
            _sensorSuiteComputer = new SensorSuiteComputer();
            _antennaPosition = Vector3.zero;
            _lastSensorReadings = new SensorReadings();
            _allNeutralised = false;
            _allNeutralisedAtMs = null;
            _currentTactic = null;
            _currentProfile = null;
            _currentScenarioId = string.Empty;
            _currentSeed = 42819;
        }

        /// <summary>
        /// Initialises swarm from an array of agent states (e.g. Punjab fixture) and creates/binds BoidAgent components.
        /// </summary>
        public void LoadAgents(SwarmAgentState[] agentArray)
        {
            DestroySpawnedBoidAgents();
            _agents.Clear();
            _allNeutralised = false;
            _allNeutralisedAtMs = null;

            if (agentArray != null)
            {
                for (int i = 0; i < agentArray.Length; i++)
                {
                    SwarmAgentState state = agentArray[i];
                    if (state != null)
                    {
                        _agents.Add(state);
                        BoidAgent boid = CreateBoidAgentInstance(i, state.isDecoy);
                        boid.BindState(state);
                        _boidAgents.Add(boid);
                    }
                }
            }
        }

        /// <summary>
        /// SA-P0-A Step 3: Seeds UnityEngine.Random.InitState(seed), spawns agentCount BoidAgent instances
        /// (first decoyCount marked as decoys with blue sphere tint, remaining as attackers with red sphere tint)
        /// in coherent formation, and binds the scenario tactic tree.
        /// SA-P1-A Step 4: Passes WeatherPreset.temperature to SensorSuiteComputer for ISO 9613-1 correction.
        /// </summary>
        public void OnScenarioStart(SwarmProfile profile, int seed, string scenarioId = "punjab-fog-pincer-01", Vector3? targetOrigin = null, float? temperatureCelsius = null)
        {
            OnScenarioEnd();

            _currentScenarioId = string.IsNullOrEmpty(scenarioId) ? "punjab-fog-pincer-01" : scenarioId;
            _currentProfile = profile != null ? profile.Clone() : SwarmMock.GetPunjabProfile();
            _currentSeed = seed;
            Vector3 origin = targetOrigin.HasValue ? targetOrigin.Value : Vector3.zero;

            // SA-P0-A Step 3: Seed procedural RNG deterministically
            UnityEngine.Random.InitState(seed);

            int totalAgents = _currentProfile.agentCount > 0 ? _currentProfile.agentCount : 6;
            int decoyCount = _currentProfile.decoyCount >= 0 ? _currentProfile.decoyCount : 2;
            float baseAlt = _currentProfile.altitudeM > 0f ? _currentProfile.altitudeM : 8.0f;
            float speedMs = _currentProfile.SpeedMs > 0f ? _currentProfile.SpeedMs : (120.0f / 3.6f);

            // Spawn coherent formation around approach corridor (~260m out, within 20m neighbour radius so Boids cohere)
            float baseRangeM = 260.0f;
            float formationSpacingM = 3.2f;
            float halfWidth = ((totalAgents - 1) * formationSpacingM) * 0.5f;

            for (int i = 0; i < totalAgents; i++)
            {
                bool isDecoy = i < decoyCount;
                float jitterX = UnityEngine.Random.Range(-0.8f, 0.8f);
                float jitterZ = UnityEngine.Random.Range(-1.2f, 1.2f);
                float jitterY = UnityEngine.Random.Range(-0.4f, 0.4f);

                float spawnX = (i * formationSpacingM) - halfWidth + jitterX;
                float spawnY = (isDecoy ? (baseAlt + 2.0f) : baseAlt) + jitterY;
                float spawnZ = baseRangeM + (isDecoy ? -4.0f : 0.0f) + jitterZ;

                Vector3 spawnPos = new Vector3(spawnX, spawnY, spawnZ);
                Vector3 toTarget = new Vector3(origin.x - spawnPos.x, 0.0f, origin.z - spawnPos.z);
                Vector3 dir = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : new Vector3(0f, 0f, -1f);
                Vector3 initialVel = dir * speedMs;

                SwarmAgentState state = new SwarmAgentState();
                BoidAgent boid = CreateBoidAgentInstance(i, isDecoy);
                boid.InitializeFromProfile(i, isDecoy, _currentProfile, spawnPos, initialVel, state);

                _agents.Add(state);
                _boidAgents.Add(boid);
            }

            string tacticName = !string.IsNullOrEmpty(_currentProfile.tacticTree)
                ? _currentProfile.tacticTree
                : "PINCER_LOW_FAST";
            _currentTactic = TacticTreeFactory.Create(tacticName);
            _currentTactic.Initialize(_currentProfile, seed, _agents, origin);

            if (_sensorSuiteComputer == null)
            {
                _sensorSuiteComputer = new SensorSuiteComputer();
            }
            _sensorSuiteComputer.ConfigureScenario(_currentScenarioId, null, temperatureCelsius);
            _lastSensorReadings = _sensorSuiteComputer.ComputeSensorReadings(_agents.ToArray(), _antennaPosition);
        }

        /// <summary>
        /// SA-P0-A Step 3 & 4: Overload accepting SCENARIO_START JSON payload or ScenarioConfig JSON string.
        /// Parses seed, scenarioId, WeatherPreset.temperature, and SwarmProfile, then spawns the seeded Boid flock.
        /// </summary>
        public void OnScenarioStart(string scenarioStartOrConfigJson, int seedOverride = -1)
        {
            string scenarioId = ExtractJsonString(scenarioStartOrConfigJson, "scenarioId");
            if (string.IsNullOrEmpty(scenarioId))
            {
                scenarioId = ExtractJsonString(scenarioStartOrConfigJson, "id");
            }
            if (string.IsNullOrEmpty(scenarioId))
            {
                scenarioId = "punjab-fog-pincer-01";
            }

            int parsedSeed = seedOverride > 0 ? seedOverride : ExtractJsonInt(scenarioStartOrConfigJson, "seed", 42819);
            SwarmProfile profile = ParseProfileFromJson(scenarioStartOrConfigJson);
            if (profile == null)
            {
                profile = SwarmMock.GetPunjabProfile();
            }

            float parsedTemp = ExtractJsonFloat(scenarioStartOrConfigJson ?? string.Empty, "temperature", float.NaN);
            float? tempOverride = !float.IsNaN(parsedTemp) ? (float?)parsedTemp : null;

            OnScenarioStart(profile, parsedSeed, scenarioId, Vector3.zero, tempOverride);
        }

        /// <summary>
        /// SA-P0-A Step 3: Destroys all spawned BoidAgent instances, halts active HBT, and clears swarm state.
        /// </summary>
        public void OnScenarioEnd()
        {
            DestroySpawnedBoidAgents();
            _agents.Clear();
            _currentTactic = null;
            _allNeutralised = false;
            _allNeutralisedAtMs = null;
            _lastSensorReadings = new SensorReadings();
        }

        private BoidAgent CreateBoidAgentInstance(int index, bool isDecoy)
        {
            string objName = "SwarmAgentVisual_" + index + (isDecoy ? "_Decoy" : "_Attacker");
#if UNITY_5_3_OR_NEWER
            GameObject go = null;
            if (swarmAgentPrefab != null)
            {
                go = UnityEngine.Object.Instantiate(swarmAgentPrefab);
                go.name = objName;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = objName;
                go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            }
            BoidAgent agent = go.GetComponent<BoidAgent>();
            if (agent == null)
            {
                agent = go.AddComponent<BoidAgent>();
            }
            return agent;
#else
            GameObject go = new GameObject(objName);
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            BoidAgent agent = new BoidAgent();
            agent.gameObject = go;
            return agent;
#endif
        }

        private void DestroySpawnedBoidAgents()
        {
            if (_boidAgents == null)
                return;

            for (int i = 0; i < _boidAgents.Count; i++)
            {
                BoidAgent boid = _boidAgents[i];
                if (boid != null)
                {
                    MonoBehaviour.Destroy(boid.gameObject);
                }
            }
            _boidAgents.Clear();
        }

        private void SyncBoidAgentsFromStates()
        {
            if (_boidAgents == null || _agents == null)
                return;

            int count = Math.Min(_boidAgents.Count, _agents.Count);
            for (int i = 0; i < count; i++)
            {
                if (_boidAgents[i] != null && _agents[i] != null)
                {
                    _boidAgents[i].SyncFromState(_agents[i]);
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
            _currentSeed = seed;
            Vector3 origin = targetOrigin.HasValue ? targetOrigin.Value : Vector3.zero;

            SwarmProfile profile = null;
            SwarmAgentState[] agents = null;
            float? parsedTempOverride = null;

            // Check if scenario JSON exists on disk in StreamingAssets
            string jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "StreamingAssets", "scenarios", _currentScenarioId + ".json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(jsonPath);
                    profile = ParseProfileFromJson(jsonContent);
                    float parsedTemp = ExtractJsonFloat(jsonContent, "temperature", float.NaN);
                    if (!float.IsNaN(parsedTemp))
                    {
                        parsedTempOverride = parsedTemp;
                    }
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

            if (_sensorSuiteComputer == null)
            {
                _sensorSuiteComputer = new SensorSuiteComputer();
            }
            _sensorSuiteComputer.ConfigureScenario(_currentScenarioId, null, parsedTempOverride);

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

            SyncBoidAgentsFromStates();

            // Continuously verify ALL_NEUTRALISED state
            if (!_allNeutralised && CheckAllAttackersNeutralised())
            {
                _allNeutralised = true;
                _allNeutralisedAtMs = currentScenarioTimeMs;
            }
        }

        /// <summary>
        /// Explicitly steps classical Reynolds Boid flocking dynamics on all active agents.
        /// </summary>
        public void StepFlocking(float deltaTime)
        {
            if (_flockController != null && _agents != null && _agents.Count > 0)
            {
                _flockController.Step(_agents, _currentProfile, deltaTime);
                SyncBoidAgentsFromStates();
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
        /// SA-P0-A Step 3 & 4 / SA-P0-B Step 3: Called by ScenarioManager / TickLoop before each SCENARIO_TICK broadcast.
        /// Returns current SwarmAgentState[], computes SensorReadings via registered SensorSuiteComputer,
        /// and invokes registered snapshot and sensor consumer callbacks.
        /// </summary>
        public SwarmAgentState[] InjectSwarmSnapshot(
            Action<SwarmAgentState[]> targetInjector = null,
            Action<SensorReadings> sensorInjector = null)
        {
            SwarmAgentState[] snapshot = GetSwarmSnapshot();
            if (targetInjector != null)
            {
                targetInjector(snapshot);
            }
            if (OnSwarmSnapshotInjected != null)
            {
                OnSwarmSnapshotInjected(snapshot);
            }

            if (_sensorSuiteComputer == null)
            {
                _sensorSuiteComputer = new SensorSuiteComputer();
            }
            _lastSensorReadings = _sensorSuiteComputer.ComputeSensorReadings(snapshot, _antennaPosition);
            if (sensorInjector != null)
            {
                sensorInjector(_lastSensorReadings);
            }
            if (OnSensorReadingsInjected != null)
            {
                OnSensorReadingsInjected(_lastSensorReadings);
            }

            return snapshot;
        }

        /// <summary>
        /// SA-P0-B Step 3: Computes and injects SensorReadings for the current swarm snapshot into SCENARIO_TICK.
        /// </summary>
        public SensorReadings InjectSensorReadings(Action<SensorReadings> sensorInjector = null)
        {
            SwarmAgentState[] snapshot = GetSwarmSnapshot();
            if (_sensorSuiteComputer == null)
            {
                _sensorSuiteComputer = new SensorSuiteComputer();
            }
            _lastSensorReadings = _sensorSuiteComputer.ComputeSensorReadings(snapshot, _antennaPosition);
            if (sensorInjector != null)
            {
                sensorInjector(_lastSensorReadings);
            }
            if (OnSensorReadingsInjected != null)
            {
                OnSensorReadingsInjected(_lastSensorReadings);
            }
            return _lastSensorReadings;
        }

        /// <summary>
        /// SA-P0-A Step 4: Handles WebSocket IPC messages (SCENARIO_START, SCENARIO_TICK, SCENARIO_END).
        /// </summary>
        public void HandleWsMessage(string messageType, string payloadJson = null)
        {
            if (string.IsNullOrEmpty(messageType))
                return;

            if (string.Equals(messageType, "SCENARIO_START", StringComparison.OrdinalIgnoreCase))
            {
                OnScenarioStart(payloadJson ?? string.Empty);
            }
            else if (string.Equals(messageType, "SCENARIO_END", StringComparison.OrdinalIgnoreCase))
            {
                OnScenarioEnd();
            }
            else if (string.Equals(messageType, "SCENARIO_TICK", StringComparison.OrdinalIgnoreCase))
            {
                StepFlocking(0.1f);
                InjectSwarmSnapshot();
            }
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
                SyncBoidAgentsFromStates();

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
