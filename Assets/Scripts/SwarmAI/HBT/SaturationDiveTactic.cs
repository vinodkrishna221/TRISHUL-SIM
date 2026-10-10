// ============================================================
// SaturationDiveTactic.cs — High-Altitude Saturation Dive Tactic Tree
// TRISHUL-SIM · SA-P2-A · swarm-ai track (Uday)
//
// Ladakh LAC Ridge / Desert FOB tactic: 8 agents approach in a broad front,
// simultaneously dive to 1m altitude at 100m range, and execute
// random RF frequency hopping every 4 seconds.
// Contract: board/contracts/scenario-events.md, docs/SCHEMA.md Entity 1
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using TrishulSim.Sensors;

namespace TrishulSim.SwarmAI.HBT
{
    /// <summary>
    /// SATURATION_DIVE tactic:
    /// - All agents approach simultaneously in a broad front (no split; overwhelming sensor count)
    /// - At 100m range to origin: all agents dive from cruising altitude to 1m altitude simultaneously
    /// - Dynamic EW evasion: random frequency hopping every 4 seconds
    /// </summary>
    public class SaturationDiveTactic : TacticTree
    {
        public override string Name
        {
            get { return "SATURATION_DIVE"; }
        }

        private float _elapsedSeconds;
        private float _hopTimer;
        private int _currentFreqIndex;
        private bool _diveTriggered;
        private float _diveTriggerRangeM = 100.0f;
        private float _diveTargetAltitudeM = 1.0f;
        private System.Random _rng;

        public bool DiveTriggered
        {
            get { return _diveTriggered; }
        }

        public float DiveTriggerRangeM
        {
            get { return _diveTriggerRangeM; }
            set { _diveTriggerRangeM = value; }
        }

        public float DiveTargetAltitudeM
        {
            get { return _diveTargetAltitudeM; }
            set { _diveTargetAltitudeM = value; }
        }

        public SaturationDiveTactic() : base()
        {
            _elapsedSeconds = 0f;
            _hopTimer = 0f;
            _currentFreqIndex = 0;
            _diveTriggered = false;
            _rng = new System.Random(1337);
        }

        public override void Initialize(SwarmProfile profile, int seed, List<SwarmAgentState> agents, Vector3 targetOrigin)
        {
            base.Initialize(profile, seed, agents, targetOrigin);
            _elapsedSeconds = 0f;
            _hopTimer = 0f;
            _currentFreqIndex = 0;
            _diveTriggered = false;
            _rng = new System.Random(seed != 0 ? seed : 51204);

            // Establish initial broad front formation positions if needed
            SetupBroadFrontFormation();
        }

        /// <summary>
        /// Sets up or verifies broad-front lateral spacing for agents.
        /// Spans across X-axis perpendicular to Z-approach vector.
        /// </summary>
        public void SetupBroadFrontFormation(float lateralSpacingM = 15.0f, float spawnDistanceM = 300.0f)
        {
            if (Agents == null || Agents.Count == 0)
                return;

            int count = Agents.Count;
            float totalWidth = (count - 1) * lateralSpacingM;
            float startX = -totalWidth * 0.5f;
            float baseAlt = Profile != null ? Profile.altitudeM : 25.0f;
            if (baseAlt > 500.0f) baseAlt = 25.0f; // High-altitude terrain elevation (e.g. Ladakh 3800m ASL) normalised to local sim AGL (25m)

            for (int i = 0; i < count; i++)
            {
                SwarmAgentState agent = Agents[i];
                if (agent == null)
                    continue;

                // If agent is at origin or uninitialized, lay out across broad front
                if (agent.positionWorld.sqrMagnitude < 1.0f)
                {
                    float x = startX + (i * lateralSpacingM);
                    float z = TargetOrigin.z + spawnDistanceM;
                    agent.positionWorld = new Vector3(x, baseAlt, z);
                }

                agent.altitudeM = baseAlt;
                agent.positionWorld.y = baseAlt;
                agent.rfEmitting = true;
                agent.propellerRpm = agent.isDecoy ? 9800 : 11400;

                // Initial frequency assignment from profile
                if (Profile != null && Profile.rfFreqGHz != null && Profile.rfFreqGHz.Length > 0)
                {
                    agent.rfFreqGHz = Profile.rfFreqGHz[i % Profile.rfFreqGHz.Length];
                }
            }
        }

        public override void Tick(float deltaTime, long currentScenarioTimeMs, EWJammerState ewJammer, Vector3 traineePos)
        {
            if (ExclusionBreached)
                return;

            _elapsedSeconds += deltaTime;
            _hopTimer += deltaTime;

            float speedMs = Profile != null ? Profile.SpeedMs : (100f / 3.6f);
            float cruisingAlt = Profile != null ? Profile.altitudeM : 25.0f;
            if (cruisingAlt > 500.0f) cruisingAlt = 25.0f; // High-altitude terrain elevation normalised to local sim AGL (25m)

            // Check terminal state: all attackers neutralised
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

            // Step 1: Dynamic EW Evasion — Random Frequency Hopping Every 4 Seconds
            if (_hopTimer >= 4.0f)
            {
                _hopTimer = 0f;
                ExecuteFrequencyHop();
            }

            // Step 2: Evaluate Dive Trigger condition (closest active agent <= 100m horizontal distance to origin)
            if (!_diveTriggered)
            {
                float minRangeToOrigin = float.MaxValue;
                for (int i = 0; i < Agents.Count; i++)
                {
                    SwarmAgentState agent = Agents[i];
                    if (agent != null && !agent.isNeutralised)
                    {
                        float d = HorizontalDistanceToOrigin(agent.positionWorld);
                        if (d < minRangeToOrigin)
                            minRangeToOrigin = d;
                    }
                }

                if (minRangeToOrigin <= _diveTriggerRangeM)
                {
                    _diveTriggered = true;
                    SetState(TacticState.DIVE_ATTACK);
                }
            }

            // Step 3: Kinematic update across the broad front
            for (int i = 0; i < Agents.Count; i++)
            {
                SwarmAgentState agent = Agents[i];
                if (agent == null || agent.isNeutralised)
                    continue;

                // Broad front approach vector: advance directly toward TargetOrigin along Z-axis
                Vector3 toOrigin = (TargetOrigin - agent.positionWorld);
                toOrigin.y = 0f;
                float toOriginMag = (float)Math.Sqrt(toOrigin.sqrMagnitude);

                // In broad front, forward component dominates towards origin
                Vector3 forwardDir;
                if (toOriginMag > 0.001f)
                {
                    forwardDir = toOrigin * (1f / toOriginMag);
                }
                else
                {
                    forwardDir = new Vector3(0, 0, -1);
                }

                // Altitude update: dive simultaneously if triggered, else stay at cruising altitude
                if (_diveTriggered)
                {
                    // Simultaneous dive to 1m altitude at high descent rate (e.g. 15 m/s descent)
                    agent.altitudeM = MoveTowards(agent.altitudeM, _diveTargetAltitudeM, deltaTime * 12.0f);
                }
                else
                {
                    agent.altitudeM = cruisingAlt;
                }

                agent.velocityMs = forwardDir * speedMs;
                agent.positionWorld.x += agent.velocityMs.x * deltaTime;
                agent.positionWorld.y = agent.altitudeM;
                agent.positionWorld.z += agent.velocityMs.z * deltaTime;

                agent.headingDeg = CalculateHeadingDeg(agent.velocityMs);
                agent.propellerRpm = agent.isDecoy ? 9800 : 11400;
            }

            // Step 4: Check inner exclusion zone (20m radius)
            CheckExclusionZone(currentScenarioTimeMs);
        }

        /// <summary>
        /// Executes random RF frequency hop across active emitting agents.
        /// Rule: Random frequency hop every 4s.
        /// </summary>
        private void ExecuteFrequencyHop()
        {
            if (Profile == null || Profile.rfFreqGHz == null || Profile.rfFreqGHz.Length == 0)
                return;

            float[] availableFreqs = Profile.rfFreqGHz;
            if (availableFreqs.Length > 1)
            {
                // Hop to a different frequency in the profile bands
                _currentFreqIndex = (_currentFreqIndex + 1 + _rng.Next(availableFreqs.Length - 1)) % availableFreqs.Length;
            }

            float newFreq = availableFreqs[_currentFreqIndex];

            for (int i = 0; i < Agents.Count; i++)
            {
                SwarmAgentState agent = Agents[i];
                if (agent != null && !agent.isNeutralised && agent.rfEmitting)
                {
                    agent.rfFreqGHz = newFreq;
                }
            }
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
                return target;
            return current + Math.Sign(target - current) * maxDelta;
        }
    }
}
