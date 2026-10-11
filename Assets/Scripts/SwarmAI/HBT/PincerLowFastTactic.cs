// ============================================================
// PincerLowFastTactic.cs — Pincer Low-Fast Tactical Behavior Tree
// TRISHUL-SIM · SA-P1-B-i / SA-P1-B-ii / SA-P2-A · swarm-ai track (Venu / Uday)
//
// Punjab Border demo tactic:
// - APPROACH (SA-P1-B-i): All 6 agents flock toward TargetPosition (Vector3.zero);
//   2 decoys at altitudeM + 5m with seed-driven sinusoidal lateral oscillation
//   (15m amplitude, 8s period); 4 attackers at altitudeM maintaining formation.
// - SPLIT_TRIGGER (SA-P1-B-i): Closest attacker < 150m to origin -> logs
//   "PINCER SPLIT TRIGGERED", lowers Boids cohesion weight, sets state = SPLIT.
// - SPLIT / PINCER / DIVE_ATTACK (SA-P1-B-ii): 2 decoys peel at ±45° & climb to 20m;
//   4 attackers execute dual-axis pincer (0° vs 180°) with RF silence, 2m low skim,
//   and dynamic EW evasion.
// Contract: board/contracts/scenario-events.md, docs/PRD.md §3 Beat 3 & Beat 4
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

        // ── SA-P1-B-i Doctrinal Constants ────────────────────────
        public const float SplitTriggerDistanceM = 150.0f;
        public const float DecoyAltitudeOffsetM = 5.0f;
        public const float DecoyOscillationAmplitudeM = 15.0f;
        public const float DecoyOscillationPeriodSec = 8.0f;
        public const float ApproachAlignmentWeight = 1.6f;
        public const float ApproachCohesionWeight = 0.8f;
        public const float PincerCohesionWeight = 0.05f;
        public const string SplitTriggerLogMessage = "PINCER SPLIT TRIGGERED";

        public override string Name
        {
            get { return "PINCER_LOW_FAST"; }
        }

        private float _elapsedSeconds;
        private float _splitTimeSeconds = -1f;
        private bool _splitTriggered;
        private float _splitTriggeredDistanceM = -1f;
        private float _seedPhaseRad;
        private float _seedSign = 1.0f;
        private readonly Dictionary<int, float> _decoyBaseX = new Dictionary<int, float>();

        public BoidFlockController FlockController { get; set; }

        public float CurrentAlignmentWeight { get; private set; }

        public float CurrentCohesionWeight { get; private set; }

        public bool SplitTriggered
        {
            get { return _splitTriggered; }
        }

        public float SplitTriggeredDistanceM
        {
            get { return _splitTriggeredDistanceM; }
        }

        public float SeedPhaseRad
        {
            get { return _seedPhaseRad; }
        }

        public string LastLogMessage { get; private set; }

        public event Action<string> OnLogMessage;

        public PincerLowFastTactic() : base()
        {
            _elapsedSeconds = 0f;
            _splitTriggered = false;
            _splitTriggeredDistanceM = -1f;
            CurrentAlignmentWeight = ApproachAlignmentWeight;
            CurrentCohesionWeight = ApproachCohesionWeight;
            ConfigureSeedParameters(42819);
        }

        public override void Initialize(SwarmProfile profile, int seed, List<SwarmAgentState> agents, Vector3 targetOrigin)
        {
            base.Initialize(profile, seed, agents, targetOrigin);
            _elapsedSeconds = 0f;
            _splitTimeSeconds = -1f;
            _splitTriggered = false;
            _splitTriggeredDistanceM = -1f;
            LastLogMessage = null;
            _decoyBaseX.Clear();

            ConfigureSeedParameters(seed);

            // SA-P1-B-i Step 3: High alignment weight in APPROACH; normal cohesion weight
            CurrentAlignmentWeight = ApproachAlignmentWeight;
            CurrentCohesionWeight = ApproachCohesionWeight;
            SyncFlockControllerWeights();

            if (Agents != null)
            {
                int decoyIdx = 0;
                for (int i = 0; i < Agents.Count; i++)
                {
                    SwarmAgentState a = Agents[i];
                    if (a != null && a.isDecoy)
                    {
                        _decoyBaseX[decoyIdx] = a.positionWorld.x;
                        decoyIdx++;
                    }
                }
            }
        }

        private void ConfigureSeedParameters(int seed)
        {
            long hash = Math.Abs((long)seed * 1103515245L + 12345L);
            _seedPhaseRad = (float)((hash % 360L) * (Math.PI / 180.0));
            _seedSign = ((seed & 1) == 0) ? 1.0f : -1.0f;
        }

        private void SyncFlockControllerWeights()
        {
            if (FlockController != null)
            {
                FlockController.AlignmentWeight = CurrentAlignmentWeight;
                FlockController.CohesionWeight = CurrentCohesionWeight;
            }
        }

        /// <summary>
        /// SA-P1-B-i Step 3: Computes the deterministic seed-driven sinusoidal lateral offset (in metres)
        /// for a decoy agent at a given elapsed scenario time:
        ///   amplitude = 15m, period = 8s, phase & side derived from Seed.
        /// </summary>
        public float ComputeDecoyLateralOffsetM(int decoyIdx, float elapsedSeconds)
        {
            float oscSide = _seedSign * ((decoyIdx % 2 == 0) ? 1.0f : -1.0f);
            double omega = (2.0 * Math.PI) / DecoyOscillationPeriodSec;
            double angle = (omega * elapsedSeconds) + _seedPhaseRad;
            return oscSide * DecoyOscillationAmplitudeM * (float)Math.Sin(angle);
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

            // Step 1: Evaluate transition triggers (SA-P1-B-i SPLIT_TRIGGER before kinematics, and also after kinematics)
            EvaluateSplitTrigger();

            if (_splitTriggered && CurrentState == TacticState.SPLIT && (_elapsedSeconds - _splitTimeSeconds >= 1.0f))
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

            // Re-evaluate SPLIT_TRIGGER immediately if an attacker crossed the 150m mark on this tick step
            if (!_splitTriggered)
            {
                EvaluateSplitTrigger();
            }

            // Step 3: Check inner exclusion zone (20m radius)
            CheckExclusionZone(currentScenarioTimeMs);
        }

        private void EvaluateSplitTrigger()
        {
            if (_splitTriggered || Agents == null)
                return;

            float minAttackerDist = float.MaxValue;
            for (int i = 0; i < Agents.Count; i++)
            {
                SwarmAgentState agent = Agents[i];
                if (agent != null && !agent.isDecoy && !agent.isNeutralised)
                {
                    float dist = HorizontalDistanceToOrigin(agent.positionWorld);
                    if (dist < minAttackerDist)
                    {
                        minAttackerDist = dist;
                    }
                }
            }

            // SA-P1-B-i SPLIT_TRIGGER: closest attacker distance to origin < 150m -> state = SPLIT
            if (minAttackerDist <= SplitTriggerDistanceM)
            {
                _splitTriggered = true;
                _splitTimeSeconds = _elapsedSeconds;
                _splitTriggeredDistanceM = minAttackerDist;

                // SA-P1-B-i Step 3: Lower Boids cohesion weight to allow independent pincer motion
                CurrentCohesionWeight = PincerCohesionWeight;
                SyncFlockControllerWeights();

                // SA-P1-B-i Step 3 & TEST: Log "PINCER SPLIT TRIGGERED" on trigger
                LastLogMessage = SplitTriggerLogMessage;
                Console.WriteLine(SplitTriggerLogMessage);
#if UNITY_5_3_OR_NEWER
                Debug.Log(SplitTriggerLogMessage);
#endif
                if (OnLogMessage != null)
                {
                    OnLogMessage(SplitTriggerLogMessage);
                }

                SetState(TacticState.SPLIT);
            }
        }

        private void UpdateDecoy(SwarmAgentState agent, int decoyIdx, float dt, float baseAlt, float speedMs)
        {
            if (!_splitTriggered)
            {
                // SA-P1-B-i APPROACH:
                // Decoys (2): higher altitude (altitudeM + 5m), sinusoidal lateral oscillation (15m amplitude, 8s period, seed-driven)
                agent.altitudeM = baseAlt + DecoyAltitudeOffsetM;

                float baseX;
                if (!_decoyBaseX.TryGetValue(decoyIdx, out baseX))
                {
                    baseX = agent.positionWorld.x;
                    _decoyBaseX[decoyIdx] = baseX;
                }

                float oscNow = ComputeDecoyLateralOffsetM(decoyIdx, _elapsedSeconds);
                float oscStart = ComputeDecoyLateralOffsetM(decoyIdx, 0.0f);
                float targetX = baseX + (oscNow - oscStart);

                float dzToOrigin = TargetPosition.z - agent.positionWorld.z;
                float forwardSignZ = dzToOrigin <= 0.0f ? -1.0f : 1.0f;
                float vz = forwardSignZ * speedMs;
                float nextZ = agent.positionWorld.z + (vz * dt);
                float vx = dt > 0.0001f ? ((targetX - agent.positionWorld.x) / dt) : 0.0f;

                agent.velocityMs = new Vector3(vx, 0.0f, vz);
                agent.positionWorld = new Vector3(targetX, agent.altitudeM, nextZ);
                agent.headingDeg = CalculateHeadingDeg(agent.velocityMs);
                agent.rfEmitting = true;
                agent.propellerRpm = 9800;
            }
            else
            {
                // SA-P1-B-ii SPLIT / PINCER: peel away at ±45° angle (seed-driven), climb to 20m
                agent.altitudeM = MoveTowards(agent.altitudeM, 20.0f, dt * 5.0f);
                float peelSide = _seedSign * ((decoyIdx % 2 == 0) ? 1.0f : -1.0f);
                float peelAngle = 45.0f * peelSide;
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
                // SA-P1-B-i APPROACH:
                // Attackers (4): stay at altitudeM, steer toward TargetPosition (Vector3.zero) as a Boid flock
                agent.altitudeM = baseAlt;

                Vector3 toTarget = TargetPosition - agent.positionWorld;
                toTarget.y = 0f;
                float toTargetMag = (float)Math.Sqrt(toTarget.sqrMagnitude);
                Vector3 goalDir = toTargetMag > 0.001f ? (toTarget * (1f / toTargetMag)) : new Vector3(0f, 0f, -1f);

                // Compute Boids separation/alignment/cohesion across active attackers to maintain formation
                Vector3 sepForce = Vector3.zero;
                Vector3 alignSum = Vector3.zero;
                Vector3 cohSum = Vector3.zero;
                int alignCount = 0;
                int cohCount = 0;

                for (int j = 0; j < Agents.Count; j++)
                {
                    SwarmAgentState other = Agents[j];
                    if (other == null || ReferenceEquals(other, agent) || other.isNeutralised || other.isDecoy)
                        continue;

                    Vector3 diff = agent.positionWorld - other.positionWorld;
                    diff.y = 0f;
                    float dSq = diff.sqrMagnitude;

                    if (dSq > 0.0001f && dSq < 25.0f) // Separation < 5m
                    {
                        float d = (float)Math.Sqrt(dSq);
                        sepForce = sepForce + (diff * ((1.0f - (d / 5.0f)) / d));
                    }
                    if (dSq < 400.0f) // Alignment & Cohesion within 20m
                    {
                        alignSum = alignSum + other.velocityMs;
                        alignCount++;
                        if (dSq >= 25.0f)
                        {
                            cohSum = cohSum + other.positionWorld;
                            cohCount++;
                        }
                    }
                }

                Vector3 desiredVel = goalDir * speedMs;
                Vector3 boidSteer = sepForce * BoidFlockController.DefaultSeparationWeight;
                if (alignCount > 0)
                {
                    Vector3 avgVel = alignSum * (1.0f / alignCount);
                    avgVel.y = 0f;
                    boidSteer = boidSteer + ((avgVel - desiredVel) * (CurrentAlignmentWeight * 0.15f));
                }
                if (cohCount > 0)
                {
                    Vector3 centroid = cohSum * (1.0f / cohCount);
                    Vector3 toCentroid = centroid - agent.positionWorld;
                    toCentroid.y = 0f;
                    boidSteer = boidSteer + (toCentroid * (CurrentCohesionWeight * 0.25f));
                }

                Vector3 combinedVel = desiredVel + (boidSteer * dt);
                combinedVel.y = 0f;
                float combinedSpeed = (float)Math.Sqrt(combinedVel.sqrMagnitude);
                if (combinedSpeed > 0.001f)
                {
                    combinedVel = combinedVel * (speedMs / combinedSpeed);
                }
                else
                {
                    combinedVel = goalDir * speedMs;
                }

                agent.velocityMs = combinedVel;
                agent.positionWorld.x += agent.velocityMs.x * dt;
                agent.positionWorld.y = agent.altitudeM;
                agent.positionWorld.z += agent.velocityMs.z * dt;
                agent.headingDeg = CalculateHeadingDeg(agent.velocityMs);
                agent.rfEmitting = true;
                agent.propellerRpm = 11400;
            }
            else
            {
                // SA-P1-B-ii SPLIT / PINCER: Attackers split into 2 pairs (Pair A: North/0°, Pair B: South/180°)
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
