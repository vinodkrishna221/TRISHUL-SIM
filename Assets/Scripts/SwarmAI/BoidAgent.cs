// ============================================================
// BoidAgent.cs — Per-Agent Boid Component, SwarmAgentState Mirror & EW Evasion
// TRISHUL-SIM · SA-P0-A / SA-P1-B-ii · swarm-ai track (Venu)
//
// MonoBehaviour per swarm agent holding all Entity 2 SwarmAgentState fields,
// initializing decoy/attacker signatures from SwarmProfile, executing dynamic
// EW evasion (seed-alternated ±30° heading steer + 3s RF silence + frequency hop),
// and driving the visual sphere representation (red = attacker, blue = decoy).
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
}
#endif

namespace TrishulSim.SwarmAI
{
    /// <summary>
    /// MonoBehaviour per swarm agent (SA-P0-A Step 1 & SA-P1-B-ii Step 2).
    /// Holds all required Entity 2 SwarmAgentState fields, manages decoy vs attacker state,
    /// and executes dynamic EW evasion (CheckEvasion: ±30° steer, 3s RF silence, frequency-hop).
    /// </summary>
    public class BoidAgent : MonoBehaviour
    {
        // ── Doctrinal Signatures from SwarmProfile (SA-P0-A Step 1) ──────
        public const float DECOY_RCS_M2 = 0.08f;
        public const float ATTACKER_RCS_M2 = 0.012f;
        public const int DECOY_PROPELLER_RPM = 9800;
        public const int ATTACKER_PROPELLER_RPM = 11400;

        // ── SA-P1-B-ii EW Evasion Constants ──────────────────────────────
        public const float EW_EVASION_CONE_HALF_ANGLE_DEG = 15.0f;
        public const float EW_EVASION_STEER_OFFSET_DEG = 30.0f;
        public const float EW_EVASION_SILENCE_DURATION_SEC = 3.0f;

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

        // ── Seed, Visual & EW Evasion Backing State ──────────────────────
        public int agentSeed = 42819;
        private SwarmAgentState _linkedState;
        private Color _visualColor = AttackerTintColor;
        private float _ewSilentTimerSec = 0f;
        private int _evasionHopCount = 0;
        private float[] _availableFreqGHz = new float[] { 5.825f, 2.4f };

        public event Action<BoidAgent, long> OnAgentNeutralised;

        public Color VisualColor
        {
            get { return _visualColor; }
        }

        public SwarmAgentState LinkedState
        {
            get { return _linkedState; }
        }

        public float EwSilentRemainingSec
        {
            get { return _ewSilentTimerSec; }
        }

        public int EvasionHopCount
        {
            get { return _evasionHopCount; }
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
            SwarmAgentState linkedState = null,
            int seed = 42819)
        {
            agentId = "agent-" + index;
            isDecoy = decoy;
            agentSeed = seed;

            float baseAlt = profile != null ? profile.altitudeM : 8.0f;
            altitudeM = spawnPosition.y > 0.001f ? spawnPosition.y : baseAlt;
            positionWorld = new Vector3(spawnPosition.x, altitudeM, spawnPosition.z);
            velocityMs = initialVelocity;
            headingDeg = ComputeHeadingDeg(velocityMs);

            rfEmitting = true;
            rfPowerDbm = profile != null ? profile.rfPowerDbm : 23.0f;

            if (profile != null && profile.rfFreqGHz != null && profile.rfFreqGHz.Length > 0)
            {
                _availableFreqGHz = (float[])profile.rfFreqGHz.Clone();
                rfFreqGHz = _availableFreqGHz[0];
            }
            else
            {
                _availableFreqGHz = new float[] { 5.825f, 2.4f };
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
            _evasionHopCount = 0;

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
        /// SA-P1-B-ii Step 3: Neutralises this agent, stopping movement, zeroing propellerRpm,
        /// suppressing RF emission, recording neutralisedAtMs, and notifying SwarmManager.
        /// </summary>
        public void Neutralise(long currentTimeMs)
        {
            isNeutralised = true;
            neutralisedAtMs = currentTimeMs;
            velocityMs = Vector3.zero;
            propellerRpm = 0;
            rfEmitting = false;
            _ewSilentTimerSec = 0f;
            _visualColor = NeutralisedTintColor;

            if (_linkedState != null)
            {
                SyncToState(_linkedState);
            }
            UpdateVisualTransformAndTint();

            if (OnAgentNeutralised != null)
            {
                OnAgentNeutralised(this, currentTimeMs);
            }
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
                velocityMs = Vector3.zero;
                propellerRpm = 0;
                rfEmitting = false;
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
        /// SA-P1-B-ii Step 2: Evaluates dynamic EW jammer evasion on this agent.
        /// If ewJammerState.engaged == true AND bearingDeg is within 15° of agent bearing:
        /// steers ±30° off current heading (seed-alternated), sets rfEmitting = false for 3s,
        /// and performs frequency-hop across available RF bands.
        /// </summary>
        public bool CheckEvasion(EWJammerState ew)
        {
            return CheckEvasion(ew, Vector3.zero, 0.1f, null);
        }

        /// <summary>
        /// Evaluates dynamic EW jammer evasion with configurable origin, deltaTime, and optional seed override.
        /// </summary>
        public bool CheckEvasion(EWJammerState ew, Vector3 originPos, float deltaTime = 0.1f, int? seedOverride = null)
        {
            if (isNeutralised)
            {
                velocityMs = Vector3.zero;
                propellerRpm = 0;
                rfEmitting = false;
                if (_linkedState != null)
                {
                    SyncToState(_linkedState);
                }
                return false;
            }

            if (_ewSilentTimerSec > 0f)
            {
                _ewSilentTimerSec -= deltaTime;
                if (_ewSilentTimerSec <= 0f)
                {
                    _ewSilentTimerSec = 0f;
                    rfEmitting = true;
                }
                else
                {
                    rfEmitting = false;
                }
            }

            if (ew == null || !ew.engaged)
            {
                if (_linkedState != null)
                {
                    SyncToState(_linkedState);
                }
                return false;
            }

            Vector3 fromOrigin = positionWorld - originPos;
            float agentBearing = ComputeHeadingDeg(fromOrigin);
            float angularDiff = EwDeconfliction.ComputeAngularDistance(ew.bearingDeg, agentBearing);

            if (angularDiff <= EW_EVASION_CONE_HALF_ANGLE_DEG)
            {
                int seedVal = seedOverride.HasValue ? seedOverride.Value : agentSeed;
                int agentIdx = ParseAgentIndex(agentId);
                bool steerRight = (((seedVal + agentIdx + _evasionHopCount) & 1) == 0);
                float steerSign = steerRight ? 1.0f : -1.0f;
                float steerOffsetDeg = EW_EVASION_STEER_OFFSET_DEG * steerSign;
                _evasionHopCount++;

                float horizSpeed = (float)Math.Sqrt((velocityMs.x * velocityMs.x) + (velocityMs.z * velocityMs.z));
                if (horizSpeed < 0.001f)
                {
                    horizSpeed = 120.0f / 3.6f;
                }

                float currentHeading = (velocityMs.sqrMagnitude > 0.0001f)
                    ? ComputeHeadingDeg(velocityMs)
                    : headingDeg;
                float newHeadingDeg = NormalizeAngle360(currentHeading + steerOffsetDeg);
                float newHeadingRad = newHeadingDeg * ((float)Math.PI / 180.0f);

                float vx = (float)Math.Sin(newHeadingRad) * horizSpeed;
                float vz = (float)Math.Cos(newHeadingRad) * horizSpeed;
                velocityMs = new Vector3(vx, velocityMs.y, vz);
                headingDeg = newHeadingDeg;

                // Suppress RF for 3s and frequency-hop (SA-P1-B-ii Step 2)
                rfEmitting = false;
                _ewSilentTimerSec = EW_EVASION_SILENCE_DURATION_SEC;
                HopFrequency();

                if (_linkedState != null)
                {
                    SyncToState(_linkedState);
                }
                return true;
            }

            if (_linkedState != null)
            {
                SyncToState(_linkedState);
            }
            return false;
        }

        private void HopFrequency()
        {
            if (_availableFreqGHz != null && _availableFreqGHz.Length > 1)
            {
                for (int i = 0; i < _availableFreqGHz.Length; i++)
                {
                    if (Math.Abs(_availableFreqGHz[i] - rfFreqGHz) > 0.01f)
                    {
                        rfFreqGHz = _availableFreqGHz[i];
                        return;
                    }
                }
            }

            // Fallback dual-band hop between 5.825 GHz and 2.4 GHz
            rfFreqGHz = (Math.Abs(rfFreqGHz - 5.825f) < 0.1f) ? 2.4f : 5.825f;
        }

        private static int ParseAgentIndex(string id)
        {
            if (string.IsNullOrEmpty(id))
                return 0;
            int dash = id.LastIndexOf('-');
            if (dash >= 0 && dash + 1 < id.Length)
            {
                int parsed;
                if (int.TryParse(id.Substring(dash + 1), out parsed))
                    return parsed;
            }
            return Math.Abs(id.GetHashCode()) & 0x7FFF;
        }

        private static float NormalizeAngle360(float deg)
        {
            float mod = deg % 360.0f;
            if (mod < 0.0f)
                mod += 360.0f;
            return mod;
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
