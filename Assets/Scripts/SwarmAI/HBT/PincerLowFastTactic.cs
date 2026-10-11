// ============================================================
// PincerLowFastTactic.cs — Pincer Low-Fast Tactical Behavior Tree
// TRISHUL-SIM · SA-P1-B-i / SA-P1-B-ii / SA-P2-A · swarm-ai track (Uday)
//
// Punjab Border demo tactic: 2 decoys oscillate and peel,
// 4 attackers execute dual-axis pincer (0° vs 180°) with RF silence and low skim.
// Contract: board/contracts/scenario-events.md, docs/PRD.md §3 Beat 4
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using TrishulSim.Sensors;

namespace TrishulSim.SwarmAI.HBT
{
    public class PincerLowFastTactic : TacticTree
    {
        private const float Deg2Rad = (float)(Math.PI / 180.0);
        private const float Rad2Deg = (float)(180.0 / Math.PI);

        public override string Name
        {
            get { return "PINCER_LOW_FAST"; }
        }

        private float _elapsedSeconds;
        private float _splitTimeSeconds = -1f;
        private bool _splitTriggered;

        public PincerLowFastTactic() : base()
        {
            _elapsedSeconds = 0f;
            _splitTriggered = false;
        }

        public override void Initialize(SwarmProfile profile, int seed, List<SwarmAgentState> agents, Vector3 targetOrigin)
        {
            base.Initialize(profile, seed, agents, targetOrigin);
            _elapsedSeconds = 0f;
            _splitTimeSeconds = -1f;
            _splitTriggered = false;
        }

        public override void Tick(float deltaTime, long currentScenarioTimeMs, EWJammerState ewJammer, Vector3 traineePos)
        {
            if (ExclusionBreached)
                return;

            _elapsedSeconds += deltaTime;
            float speedMs = Profile != null ? Profile.SpeedMs : (120f / 3.6f);
            float baseAlt = Profile != null ? Profile.altitudeM : 8.0f;

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

            // Step 1: Evaluate transition triggers
            if (!_splitTriggered)
            {
                float minAttackerDist = float.MaxValue;
                for (int i = 0; i < Agents.Count; i++)
                {
                    SwarmAgentState agent = Agents[i];
                    if (agent != null && !agent.isDecoy && !agent.isNeutralised)
                    {
                        float dist = HorizontalDistanceToOrigin(agent.positionWorld);
                        if (dist < minAttackerDist)
                            minAttackerDist = dist;
                    }
                }

                // SPLIT_TRIGGER: closest attacker < 150m
                if (minAttackerDist <= 150.0f)
                {
                    _splitTriggered = true;
                    _splitTimeSeconds = _elapsedSeconds;
                    SetState(TacticState.SPLIT);
                }
            }
            else if (CurrentState == TacticState.SPLIT && (_elapsedSeconds - _splitTimeSeconds >= 1.0f))
            {
                SetState(TacticState.PINCER);
            }

            // Step 2: Per-agent kinematic and state updates
            int attackerIndex = 0;
            int decoyIndex = 0;

            for (int i = 0; i < Agents.Count; i++)
            {
                SwarmAgentState agent = Agents[i];
                if (agent == null || agent.isNeutralised)
                    continue;

                if (agent.isDecoy)
                {
                    UpdateDecoy(agent, decoyIndex, deltaTime, baseAlt, speedMs);
                    decoyIndex++;
                }
                else
                {
                    UpdateAttacker(agent, attackerIndex, deltaTime, baseAlt, speedMs, ewJammer, currentScenarioTimeMs);
                    attackerIndex++;
                }
            }

            // Step 3: Check inner exclusion zone (20m radius)
            CheckExclusionZone(currentScenarioTimeMs);
        }

        private void UpdateDecoy(SwarmAgentState agent, int decoyIdx, float dt, float baseAlt, float speedMs)
        {
            if (!_splitTriggered)
            {
                // APPROACH: fly towards origin at higher altitude with sinusoidal lateral oscillation
                agent.altitudeM = baseAlt + 5.0f;
                float oscSide = (decoyIdx % 2 == 0) ? 1.0f : -1.0f;
                float lateralOffset = (float)Math.Sin(_elapsedSeconds * (2.0 * Math.PI / 8.0)) * 15.0f * oscSide;

                Vector3 toOrigin = (TargetOrigin - agent.positionWorld);
                toOrigin.y = 0f;
                Vector3 forward = toOrigin.sqrMagnitude > 0.001f ? (toOrigin * (1f / (float)Math.Sqrt(toOrigin.sqrMagnitude))) : new Vector3(0, 0, -1);
                Vector3 right = new Vector3(forward.z, 0, -forward.x);

                agent.velocityMs = (forward * (speedMs * 0.85f)) + (right * (lateralOffset * 0.2f));
                agent.positionWorld.x += agent.velocityMs.x * dt;
                agent.positionWorld.y = agent.altitudeM;
                agent.positionWorld.z += agent.velocityMs.z * dt;
                agent.headingDeg = CalculateHeadingDeg(agent.velocityMs);
                agent.rfEmitting = true;
                agent.propellerRpm = 9800;
            }
            else
            {
                // SPLIT / PINCER: peel away at 45° angle, climb to 20m
                agent.altitudeM = MoveTowards(agent.altitudeM, 20.0f, dt * 5.0f);
                float peelAngle = (decoyIdx % 2 == 0) ? 45.0f : -45.0f;
                float rad = peelAngle * Deg2Rad;

                Vector3 peelDir = new Vector3((float)Math.Sin(rad), 0, -(float)Math.Cos(rad));
                float mag = (float)Math.Sqrt(peelDir.sqrMagnitude);
                if (mag > 0.0001f) peelDir = peelDir * (1f / mag);

                agent.velocityMs = peelDir * (speedMs * 0.7f);
                agent.positionWorld.x += agent.velocityMs.x * dt;
                agent.positionWorld.y = agent.altitudeM;
                agent.positionWorld.z += agent.velocityMs.z * dt;
                agent.headingDeg = CalculateHeadingDeg(agent.velocityMs);
                agent.rfEmitting = true;
                agent.propellerRpm = 9800;
            }
        }

        private void UpdateAttacker(SwarmAgentState agent, int attIdx, float dt, float baseAlt, float speedMs, EWJammerState ewJammer, long currentMs)
        {
            float dist = HorizontalDistanceToOrigin(agent.positionWorld);

            if (!_splitTriggered)
            {
                // APPROACH: move low in formation
                agent.altitudeM = baseAlt;
                Vector3 toOrigin = (TargetOrigin - agent.positionWorld);
                toOrigin.y = 0f;
                float toOriginMag = (float)Math.Sqrt(toOrigin.sqrMagnitude);
                Vector3 dir = toOriginMag > 0.001f ? (toOrigin * (1f / toOriginMag)) : new Vector3(0, 0, -1);

                agent.velocityMs = dir * speedMs;
                agent.positionWorld.x += agent.velocityMs.x * dt;
                agent.positionWorld.y = agent.altitudeM;
                agent.positionWorld.z += agent.velocityMs.z * dt;
                agent.headingDeg = CalculateHeadingDeg(agent.velocityMs);
                agent.rfEmitting = true;
                agent.propellerRpm = 11400;
            }
            else
            {
                // SPLIT / PINCER: Attackers split into 2 pairs (Pair A: North/0°, Pair B: South/180°)
                bool isPairA = (attIdx % 2 == 0);
                Vector3 targetVector;

                if (isPairA)
                {
                    // Approach from North (towards -Z)
                    targetVector = new Vector3(0, 0, -1);
                }
                else
                {
                    // Approach from South (towards +Z)
                    targetVector = new Vector3(0, 0, 1);
                }

                // EW Evasion: silent for 5 seconds after split
                if (_elapsedSeconds - _splitTimeSeconds < 5.0f)
                {
                    agent.rfEmitting = false;
                }
                else
                {
                    agent.rfEmitting = true;
                }

                // Altitude profile: at < 50m, skim low at 2m
                if (dist <= 50.0f)
                {
                    agent.altitudeM = MoveTowards(agent.altitudeM, 2.0f, dt * 6.0f);
                    SetState(TacticState.DIVE_ATTACK);
                }
                else
                {
                    agent.altitudeM = baseAlt;
                }

                // Dynamic EW evasion check
                if (ewJammer != null && ewJammer.engaged)
                {
                    float jammerBearing = ewJammer.bearingDeg;
                    float agentBearing = CalculateHeadingDeg(agent.positionWorld - TargetOrigin);
                    float diff = Math.Abs(DeltaAngle(jammerBearing, agentBearing));

                    if (diff <= 15.0f)
                    {
                        // Steer ±30° off heading, suppress RF for frequency hop
                        float steerOffset = (attIdx % 2 == 0) ? 30.0f : -30.0f;
                        float curAngle = (float)(Math.Atan2(targetVector.x, targetVector.z) * Rad2Deg);
                        float newAngle = (curAngle + steerOffset) * Deg2Rad;
                        targetVector = new Vector3((float)Math.Sin(newAngle), 0, (float)Math.Cos(newAngle));
                        float tvMag = (float)Math.Sqrt(targetVector.sqrMagnitude);
                        if (tvMag > 0.0001f) targetVector = targetVector * (1f / tvMag);
                        agent.rfEmitting = false;
                    }
                }

                agent.velocityMs = targetVector * speedMs;
                agent.positionWorld.x += agent.velocityMs.x * dt;
                agent.positionWorld.y = agent.altitudeM;
                agent.positionWorld.z += agent.velocityMs.z * dt;
                agent.headingDeg = CalculateHeadingDeg(agent.velocityMs);
                agent.propellerRpm = 11400;
            }
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
                return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        private static float DeltaAngle(float current, float target)
        {
            float num = (target - current) % 360f;
            if (num < -180f) num += 360f;
            if (num > 180f) num -= 360f;
            return num;
        }
    }
}
