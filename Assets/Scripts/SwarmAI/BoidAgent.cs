// ============================================================
// BoidAgent.cs — Per-Agent Boid Component & SwarmAgentState Mirror
// TRISHUL-SIM · SA-P0-A · swarm-ai track (Venu)
//
// MonoBehaviour per swarm agent holding all Entity 2 SwarmAgentState fields,
// initializing decoy/attacker signatures from SwarmProfile, and driving
// the visual sphere representation (red = attacker, blue = decoy).
// Contract: board/contracts/scenario-events.md, docs/SCHEMA.md Entity 2
// ============================================================

using System;
using UnityEngine;
using TrishulSim.Sensors;

#if !UNITY_5_3_OR_NEWER
namespace UnityEngine
{
    [Serializable]
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

        public static Color red { get { return new Color(1f, 0f, 0f, 1f); } }
        public static Color blue { get { return new Color(0f, 0f, 1f, 1f); } }
        public static Color gray { get { return new Color(0.5f, 0.5f, 0.5f, 1f); } }
    }

    public static class Random
    {
        private static readonly object LockObj = new object();
        private static System.Random _rng = new System.Random(42819);

        public static void InitState(int seed)
        {
            lock (LockObj)
            {
                _rng = new System.Random(seed);
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

        public static int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;
            lock (LockObj)
            {
                return _rng.Next(minInclusive, maxExclusive);
            }
        }
    }

    public class Transform
    {
        public Vector3 position = Vector3.zero;
        public Vector3 localScale = new Vector3(1f, 1f, 1f);
    }

    public class GameObject
    {
        public string name = string.Empty;
        public bool activeSelf = true;
        public Transform transform = new Transform();

        public GameObject() { }
        public GameObject(string name) { this.name = name ?? string.Empty; }

        public void SetActive(bool active)
        {
            activeSelf = active;
        }
    }

    public class MonoBehaviour
    {
        private GameObject _gameObject;
        public GameObject gameObject
        {
            get
            {
                if (_gameObject == null)
                    _gameObject = new GameObject(GetType().Name);
                return _gameObject;
            }
            set { _gameObject = value; }
        }

        public Transform transform
        {
            get { return gameObject.transform; }
        }

        public bool enabled = true;

        public static void Destroy(object obj)
        {
            GameObject go = obj as GameObject;
            if (go != null)
            {
                go.activeSelf = false;
                return;
            }
            MonoBehaviour mb = obj as MonoBehaviour;
            if (mb != null && mb._gameObject != null)
            {
                mb._gameObject.activeSelf = false;
            }
        }
    }
}
#endif

namespace TrishulSim.SwarmAI
{
    /// <summary>
    /// MonoBehaviour per swarm agent (SA-P0-A Step 1).
    /// Holds all required Entity 2 SwarmAgentState fields and manages
    /// decoy (blue sphere, RCS 0.08, 9800 RPM) vs attacker (red sphere, RCS 0.012, 11400 RPM) state.
    /// </summary>
    public class BoidAgent : MonoBehaviour
    {
        // ── Doctrinal Signatures from SwarmProfile (SA-P0-A Step 1) ──────
        public const float DECOY_RCS_M2 = 0.08f;
        public const float ATTACKER_RCS_M2 = 0.012f;
        public const int DECOY_PROPELLER_RPM = 9800;
        public const int ATTACKER_PROPELLER_RPM = 11400;

        public static readonly Color AttackerTintColor = new Color(0.95f, 0.15f, 0.15f, 1.0f); // Red = Attacker
        public static readonly Color DecoyTintColor = new Color(0.15f, 0.45f, 0.95f, 1.0f);    // Blue = Decoy
        public static readonly Color NeutralisedTintColor = new Color(0.35f, 0.35f, 0.35f, 1.0f);

        // ── Entity 2 SwarmAgentState Required Fields (docs/SCHEMA.md) ────
        public string agentId = "agent-0";
        public bool isDecoy = false;
        public Vector3 positionWorld = Vector3.zero;
        public Vector3 velocityMs = Vector3.zero;
        public float headingDeg = 0.0f;
        public float altitudeM = 8.0f;
        public bool rfEmitting = true;
        public float rfFreqGHz = 5.825f;
        public float rfPowerDbm = 23.0f;
        public int propellerRpm = ATTACKER_PROPELLER_RPM;
        public float rcs = ATTACKER_RCS_M2;
        public bool isNeutralised = false;
        [NonSerialized] public long? neutralisedAtMs = null;

        // ── Visual & Backing State ───────────────────────────────────────
        private SwarmAgentState _linkedState;
        private Color _visualColor = AttackerTintColor;
        private float _ewSilentTimerSec = 0f;

        public Color VisualColor
        {
            get { return _visualColor; }
        }

        public SwarmAgentState LinkedState
        {
            get { return _linkedState; }
        }

        /// <summary>
        /// Initializes this BoidAgent from a SwarmProfile and initial spawn kinematics (SA-P0-A Step 1).
        /// Decoys get rcs = 0.08 m², propellerRpm = 9800, blue visual sphere.
        /// Attackers get rcs = 0.012 m², propellerRpm = 11400, red visual sphere.
        /// </summary>
        public void InitializeFromProfile(
            int index,
            bool decoy,
            SwarmProfile profile,
            Vector3 spawnPosition,
            Vector3 initialVelocity,
            SwarmAgentState linkedState = null)
        {
            agentId = "agent-" + index;
            isDecoy = decoy;

            float baseAlt = profile != null ? profile.altitudeM : 8.0f;
            altitudeM = spawnPosition.y > 0.001f ? spawnPosition.y : baseAlt;
            positionWorld = new Vector3(spawnPosition.x, altitudeM, spawnPosition.z);
            velocityMs = initialVelocity;
            headingDeg = ComputeHeadingDeg(velocityMs);

            rfEmitting = true;
            rfPowerDbm = profile != null ? profile.rfPowerDbm : 23.0f;

            if (profile != null && profile.rfFreqGHz != null && profile.rfFreqGHz.Length > 0)
            {
                if (decoy && profile.rfFreqGHz.Length > 1)
                    rfFreqGHz = profile.rfFreqGHz[1];
                else
                    rfFreqGHz = profile.rfFreqGHz[0];
            }
            else
            {
                rfFreqGHz = decoy ? 2.4f : 5.825f;
            }

            // SA-P0-A Step 1 doctrinal signatures:
            // decoys -> rcs = 0.08, propellerRpm = 9800; attackers -> rcs = 0.012, rpm = 11400
            if (decoy)
            {
                rcs = DECOY_RCS_M2;
                propellerRpm = DECOY_PROPELLER_RPM;
                _visualColor = DecoyTintColor;
            }
            else
            {
                rcs = ATTACKER_RCS_M2;
                propellerRpm = ATTACKER_PROPELLER_RPM;
                _visualColor = AttackerTintColor;
            }

            isNeutralised = false;
            neutralisedAtMs = null;
            _ewSilentTimerSec = 0f;

            _linkedState = linkedState ?? new SwarmAgentState();
            SyncToState(_linkedState);
            UpdateVisualTransformAndTint();
        }

        /// <summary>
        /// Binds this BoidAgent to an existing SwarmAgentState and copies all fields.
        /// </summary>
        public void BindState(SwarmAgentState state)
        {
            if (state == null)
                return;

            _linkedState = state;
            SyncFromState(state);
        }

        /// <summary>
        /// Copies all 13 Entity 2 fields from this BoidAgent into a SwarmAgentState instance.
        /// </summary>
        public void SyncToState(SwarmAgentState target)
        {
            if (target == null)
                return;

            target.agentId = agentId;
            target.isDecoy = isDecoy;
            target.positionWorld = positionWorld;
            target.velocityMs = velocityMs;
            target.headingDeg = headingDeg;
            target.altitudeM = altitudeM;
            target.rfEmitting = rfEmitting;
            target.rfFreqGHz = rfFreqGHz;
            target.rfPowerDbm = rfPowerDbm;
            target.propellerRpm = propellerRpm;
            target.rcs = rcs;
            target.isNeutralised = isNeutralised;
            target.neutralisedAtMs = neutralisedAtMs;
        }

        /// <summary>
        /// Updates this BoidAgent's fields and visual transform from a SwarmAgentState instance.
        /// </summary>
        public void SyncFromState(SwarmAgentState source)
        {
            if (source == null)
                return;

            agentId = source.agentId;
            isDecoy = source.isDecoy;
            positionWorld = source.positionWorld;
            velocityMs = source.velocityMs;
            headingDeg = source.headingDeg;
            altitudeM = source.altitudeM;
            rfEmitting = source.rfEmitting;
            rfFreqGHz = source.rfFreqGHz;
            rfPowerDbm = source.rfPowerDbm;
            propellerRpm = source.propellerRpm;
            rcs = source.rcs;
            isNeutralised = source.isNeutralised;
            neutralisedAtMs = source.neutralisedAtMs;

            if (isNeutralised)
            {
                _visualColor = NeutralisedTintColor;
            }
            else
            {
                _visualColor = isDecoy ? DecoyTintColor : AttackerTintColor;
            }

            UpdateVisualTransformAndTint();
        }

        /// <summary>
        /// Returns a SwarmAgentState snapshot of this agent's current state.
        /// </summary>
        public SwarmAgentState ToSwarmAgentState()
        {
            if (_linkedState == null)
            {
                _linkedState = new SwarmAgentState();
            }
            SyncToState(_linkedState);
            return _linkedState;
        }

        /// <summary>
        /// Evaluates dynamic EW jammer evasion on this agent (SA-P1-B-ii hook).
        /// If jammer is engaged and within 15° of agent bearing, steers ±30° off heading and suppresses RF for 3s.
        /// </summary>
        public bool CheckEvasion(EWJammerState ew, Vector3 originPos = default(Vector3), float deltaTime = 0.1f)
        {
            if (isNeutralised)
                return false;

            if (_ewSilentTimerSec > 0f)
            {
                _ewSilentTimerSec -= deltaTime;
                if (_ewSilentTimerSec <= 0f)
                {
                    _ewSilentTimerSec = 0f;
                    rfEmitting = true;
                }
            }

            if (ew == null || !ew.engaged)
                return false;

            Vector3 fromOrigin = positionWorld - originPos;
            float agentBearing = ComputeHeadingDeg(fromOrigin);
            float angularDiff = EwDeconfliction.ComputeAngularDistance(ew.bearingDeg, agentBearing);

            if (angularDiff <= 15.0f)
            {
                float steerSign = (agentId != null && (agentId.GetHashCode() & 1) == 0) ? 1.0f : -1.0f;
                float steerRad = (30.0f * steerSign) * ((float)Math.PI / 180.0f);
                float cosA = (float)Math.Cos(steerRad);
                float sinA = (float)Math.Sin(steerRad);

                float vx = (velocityMs.x * cosA) + (velocityMs.z * sinA);
                float vz = (-velocityMs.x * sinA) + (velocityMs.z * cosA);
                velocityMs = new Vector3(vx, velocityMs.y, vz);
                headingDeg = ComputeHeadingDeg(velocityMs);
                rfEmitting = false;
                _ewSilentTimerSec = 3.0f;

                if (_linkedState != null)
                {
                    SyncToState(_linkedState);
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Synchronizes Unity Transform position and sphere renderer colour (red=attacker, blue=decoy).
        /// </summary>
        public void UpdateVisualTransformAndTint()
        {
            if (transform != null)
            {
                transform.position = positionWorld;
            }

#if UNITY_5_3_OR_NEWER
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = _visualColor;
            }
#endif
        }

        public static float ComputeHeadingDeg(Vector3 velocity)
        {
            if ((velocity.x * velocity.x) + (velocity.z * velocity.z) < 0.0001f)
                return 0.0f;

            float deg = (float)(Math.Atan2(velocity.x, velocity.z) * (180.0 / Math.PI));
            if (deg < 0.0f)
                deg += 360.0f;
            return deg;
        }
    }
}
