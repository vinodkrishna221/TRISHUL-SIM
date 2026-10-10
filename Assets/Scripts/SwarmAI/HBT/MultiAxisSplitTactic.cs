// ============================================================
// MultiAxisSplitTactic.cs — Multi-Axis Cardinal Split Tactic Tree
// TRISHUL-SIM · SA-P2-A · swarm-ai track (Uday)
//
// LoC Mountain Pass tactic: 1 decoy executes direct frontal approach,
// while 4 attackers immediately split into 4 cardinal compass vectors
// (North, East, South, West) simultaneously from spawn at 915 MHz.
// Contract: board/contracts/scenario-events.md, docs/SCHEMA.md Entity 1
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using TrishulSim.Sensors;

namespace TrishulSim.SwarmAI.HBT
{
    /// <summary>
    /// MULTI-AXIS_SPLIT tactic:
    /// - 1 decoy takes direct frontal approach (loud, visible, 915 MHz)
    /// - 4 attackers immediately split to 4 cardinal approach vectors simultaneously from spawn
    /// - 915 MHz only (low-tech): no Doppler evasion, narrow-band RF signal
    /// - Forces trainee to prioritize and engage across 360° cardinal axes
    /// </summary>
    public class MultiAxisSplitTactic : TacticTree
    {
        public override string Name
        {
            get { return "MULTI-AXIS_SPLIT"; }
        }

        private float _elapsedSeconds;
        private readonly Vector3[] _cardinalApproachVectors = new Vector3[]
        {
            new Vector3(0, 0, -1), // North approach vector (moving South towards origin)
            new Vector3(-1, 0, 0), // East approach vector (moving West towards origin)
            new Vector3(0, 0, 1),  // South approach vector (moving North towards origin)
            new Vector3(1, 0, 0)   // West approach vector (moving East towards origin)
        };

        private readonly float[] _cardinalSpawnAnglesDeg = new float[]
        {
            0.0f,   // North (0°)
            90.0f,  // East (90°)
            180.0f, // South (180°)
            270.0f  // West (270°)
        };

        public MultiAxisSplitTactic() : base()
        {
            _elapsedSeconds = 0f;
        }

        public override void Initialize(SwarmProfile profile, int seed, List<SwarmAgentState> agents, Vector3 targetOrigin)
        {
            base.Initialize(profile, seed, agents, targetOrigin);
            _elapsedSeconds = 0f;

            // Immediately split from spawn
            SetState(TacticState.SPLIT);

            SetupMultiAxisFormation();
        }

        /// <summary>
        /// Positions 1 decoy along direct frontal approach,
        /// and positions 4 attackers at the 4 cardinal compass axes closing inward toward origin.
        /// </summary>
        public void SetupMultiAxisFormation(float spawnDistanceM = 300.0f)
        {
            if (Agents == null || Agents.Count == 0)
                return;

            float baseAlt = Profile != null ? Profile.altitudeM : 45.0f;
            float freq = (Profile != null && Profile.rfFreqGHz != null && Profile.rfFreqGHz.Length > 0)
                ? Profile.rfFreqGHz[0]
                : 0.915f;

            int attackerIdx = 0;

            for (int i = 0; i < Agents.Count; i++)
            {
                SwarmAgentState agent = Agents[i];
                if (agent == null)
                    continue;

                agent.altitudeM = baseAlt;
                agent.rfFreqGHz = freq;
                agent.rfEmitting = true;

                if (agent.isDecoy)
                {
                    // 1 Decoy: Direct frontal approach (loud, high RCS 0.08, direct line of sight)
                    agent.positionWorld = new Vector3(TargetOrigin.x, baseAlt, TargetOrigin.z + spawnDistanceM);
                    agent.velocityMs = new Vector3(0, 0, -1) * (Profile != null ? Profile.SpeedMs : (80f / 3.6f));
                    agent.headingDeg = 180.0f; // Heading South directly toward origin
                    agent.rcs = 0.08f;
                    agent.propellerRpm = 9800;
                    agent.rfPowerDbm = Profile != null ? Profile.rfPowerDbm : 20.0f;
                }
                else
                {
                    // 4 Attackers: split into 4 cardinal vectors simultaneously from spawn
                    int cardinalIdx = attackerIdx % 4;
                    float angleDeg = _cardinalSpawnAnglesDeg[cardinalIdx];
                    float rad = (float)(angleDeg * (Math.PI / 180.0));

                    // Spawn position on circle of radius spawnDistanceM at the cardinal compass points
                    float spawnX = TargetOrigin.x + (float)Math.Sin(rad) * spawnDistanceM;
                    float spawnZ = TargetOrigin.z + (float)Math.Cos(rad) * spawnDistanceM;

                    agent.positionWorld = new Vector3(spawnX, baseAlt, spawnZ);

                    Vector3 dir = _cardinalApproachVectors[cardinalIdx];
                    float speedMs = Profile != null ? Profile.SpeedMs : (80f / 3.6f);
                    agent.velocityMs = dir * speedMs;
                    agent.headingDeg = CalculateHeadingDeg(agent.velocityMs);
                    agent.rcs = 0.012f;
                    agent.propellerRpm = 11400; // No Doppler evasion: steady RPM
                    agent.rfPowerDbm = Profile != null ? Profile.rfPowerDbm : 20.0f;

                    attackerIdx++;
                }
            }
        }

        public override void Tick(float deltaTime, long currentScenarioTimeMs, EWJammerState ewJammer, Vector3 traineePos)
        {
            if (ExclusionBreached)
                return;

            _elapsedSeconds += deltaTime;

            float speedMs = Profile != null ? Profile.SpeedMs : (80f / 3.6f);
            float baseAlt = Profile != null ? Profile.altitudeM : 45.0f;

            // Check if all attackers neutralised
            bool anyAttackerActive = false;
            for (int i = 0; i < Agents.Count; i++)
            {
                if (Agents[i] != null && !Agents[i].isDecoy && !Agents[i].isNeutralised)
                {
                    anyAttackerActive = true;
                    break;
                }
            }

            if (!anyAttackerActive && Agents.Count > 0)
            {
                SetState(TacticState.NEUTRALISED);
                return;
            }

            int attackerIdx = 0;

            for (int i = 0; i < Agents.Count; i++)
            {
                SwarmAgentState agent = Agents[i];
                if (agent == null || agent.isNeutralised)
                    continue;

                if (agent.isDecoy)
                {
                    // Decoy: direct frontal approach towards origin
                    Vector3 toOrigin = (TargetOrigin - agent.positionWorld);
                    toOrigin.y = 0f;
                    float toOriginMag = (float)Math.Sqrt(toOrigin.sqrMagnitude);
                    Vector3 dir = toOriginMag > 0.001f ? (toOrigin * (1f / toOriginMag)) : new Vector3(0, 0, -1);

                    agent.velocityMs = dir * speedMs;
                    agent.positionWorld.x += agent.velocityMs.x * deltaTime;
                    agent.positionWorld.y = baseAlt;
                    agent.positionWorld.z += agent.velocityMs.z * deltaTime;
                    agent.headingDeg = CalculateHeadingDeg(agent.velocityMs);
                    agent.rfEmitting = true;
                }
                else
                {
                    // Attacker: advance along its cardinal approach vector directly towards origin
                    int cardinalIdx = attackerIdx % 4;
                    Vector3 cardinalDir = _cardinalApproachVectors[cardinalIdx];

                    // Steer directly toward origin
                    Vector3 toOrigin = (TargetOrigin - agent.positionWorld);
                    toOrigin.y = 0f;
                    float toOriginMag = (float)Math.Sqrt(toOrigin.sqrMagnitude);

                    Vector3 dir;
                    if (toOriginMag > 0.001f)
                    {
                        dir = toOrigin * (1f / toOriginMag);
                    }
                    else
                    {
                        dir = cardinalDir;
                    }

                    agent.velocityMs = dir * speedMs;
                    agent.positionWorld.x += agent.velocityMs.x * deltaTime;
                    agent.positionWorld.y = baseAlt;
                    agent.positionWorld.z += agent.velocityMs.z * deltaTime;
                    agent.headingDeg = CalculateHeadingDeg(agent.velocityMs);

                    // 915 MHz only, continuous emission, no Doppler evasion
                    agent.rfFreqGHz = 0.915f;
                    agent.rfEmitting = true;

                    attackerIdx++;
                }
            }

            // Check if any agent within attack dive range (30m)
            float minRange = float.MaxValue;
            for (int i = 0; i < Agents.Count; i++)
            {
                if (Agents[i] != null && !Agents[i].isDecoy && !Agents[i].isNeutralised)
                {
                    float d = HorizontalDistanceToOrigin(Agents[i].positionWorld);
                    if (d < minRange) minRange = d;
                }
            }

            if (minRange <= 30.0f && CurrentState == TacticState.SPLIT)
            {
                SetState(TacticState.DIVE_ATTACK);
            }

            // Check exclusion zone (20m radius)
            CheckExclusionZone(currentScenarioTimeMs);
        }

        /// <summary>
        /// Returns the cardinal compass direction name ("North", "East", "South", "West")
        /// for a given attacker index (0..3).
        /// </summary>
        public static string GetCompassDirectionName(int attackerIndex)
        {
            switch (attackerIndex % 4)
            {
                case 0: return "North";
                case 1: return "East";
                case 2: return "South";
                case 3: return "West";
                default: return "Unknown";
            }
        }
    }
}
