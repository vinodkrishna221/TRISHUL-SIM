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
// - SPLIT / PINCER / DIVE_ATTACK / NEUTRALISED (SA-P1-B-ii):
//   * Decoys peel away at ±45° (seed-driven) and climb to 20m altitude.
//   * Attackers split into Pair A (azimuth 0° North) and Pair B (azimuth 180° South),
//     suppress RF (rfEmitting = false) for 5s after split, close independently
//     with no Boids cohesion, descend to 2m altitude at <= 50m (DIVE_ATTACK),
//     and trigger EXCLUSION_BREACH at <= 20m.
//   * Dynamic EW evasion: when ewJammerState.engaged and bearingDeg within 15° of
//     agent bearing, steers ±30° off heading (seed-alternated) and sets
//     rfEmitting = false for 3s (frequency-hop behaviour).
//   * Neutralised agents stop immediately (velocityMs = 0, propellerRpm = 0, rfEmitting = false);
//     when all attackers are neutralised, transitions to TacticState.NEUTRALISED.
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

        // ── SA-P1-B-i & SA-P1-B-ii Doctrinal Constants ───────────────────
        public const float SplitTriggerDistanceM = 150.0f;
        public const float DiveTriggerDistanceM = 50.0f;
        public const float DecoyAltitudeOffsetM = 5.0f;
        public const float DecoySplitClimbAltitudeM = 20.0f;
        public const float DecoyPeelAngleDeg = 45.0f;
        public const float DecoyOscillationAmplitudeM = 15.0f;
        public const float DecoyOscillationPeriodSec = 8.0f;
        public const float AttackerSkimAltitudeM = 2.0f;
        public const float PostSplitRfSilenceSec = 5.0f;
        public const float EwEvasionSilenceSec = 3.0f;
        public const float EwEvasionConeHalfAngleDeg = 15.0f;
        public const float EwEvasionSteerOffsetDeg = 30.0f;
        public const float SplitPhaseDurationSec = 1.6f;
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
        private readonly Dictionary<int, float> _attackerEwSilentTimers = new Dictionary<int, float>();
        private readonly Dictionary<int, int> _attackerEwHopCounts = new Dictionary<int, int>();

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

        public float SplitTimeSeconds
        {
            get { return _splitTimeSeconds; }
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
            _attackerEwSilentTimers.Clear();
            _attackerEwHopCounts.Clear();

            ConfigureSeedParameters(seed);

            // SA-P1-B-i Step 3: High alignment weight in APPROACH; normal cohesion weight
            CurrentAlignmentWeight = ApproachAlignmentWeight;
            CurrentCohesionWeight = ApproachCohesionWeight;
            SyncFlockControllerWeights();

            if (Agents != null)
            {
                int decoyIdx = 0;
                int attIdx = 0;
                for (int i = 0; i < Agents.Count; i++)
                {
                    SwarmAgentState a = Agents[i];
                    if (a == null)
                        continue;

                    if (a.isDecoy)
                    {
                        _decoyBaseX[decoyIdx] = a.positionWorld.x;
                        decoyIdx++;
                    }
                    else
                    {
                        _attackerEwSilentTimers[attIdx] = 0f;
                        _attackerEwHopCounts[attIdx] = 0;
                        attIdx++;
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

        /// <summary>
        /// Returns the remaining EW evasion RF silence timer (in seconds) for a given attacker index (0..3).
        /// </summary>
        public float GetAttackerEwSilentTimerSec(int attIdx)
        {
            float timer;
            return _attackerEwSilentTimers.TryGetValue(attIdx, out timer) ? timer : 0f;
        }

        public override void Tick(float deltaTime, long currentScenarioTimeMs, EWJammerState ewJammer, Vector3 traineePos)
        {
            if (ExclusionBreached)
                return;

            _elapsedSeconds += deltaTime;
            float speedMs = Profile != null ? Profile.SpeedMs : (120f / 3.6f);
            float baseAlt = Profile != null ? Profile.altitudeM : 8.0f;

            // Enforce neutralised stop state (velocityMs = 0, propellerRpm = 0, rfEmitting = false)
            // and check if all attackers are neutralised
            bool anyAttackerActive = false;
            for (int i = 0; i < Agents.Count; i++)
            {
                SwarmAgentState a = Agents[i];
                if (a == null)
                    continue;

                if (a.isNeutralised)
                {
                    a.velocityMs = Vector3.zero;
                    a.propellerRpm = 0;
                    a.rfEmitting = false;
                    if (!a.neutralisedAtMs.HasValue)
                    {
                        a.neutralisedAtMs = currentScenarioTimeMs;
                    }
                    continue;
                }

                if (!a.isDecoy)
                {
                    anyAttackerActive = true;
                }
            }

            if (!anyAttackerActive && Agents.Count > 0)
            {
                SetState(TacticState.NEUTRALISED);
                return;
            }

            // Step 1: Evaluate transition triggers (SA-P1-B-i SPLIT_TRIGGER before kinematics)
            EvaluateSplitTrigger();

            // SA-P1-B-ii: Transition SPLIT -> PINCER once split maneuver phase completes
            if (_splitTriggered && CurrentState == TacticState.SPLIT && (_elapsedSeconds - _splitTimeSeconds >= SplitPhaseDurationSec))
            {
                SetState(TacticState.PINCER);
            }

            // Pre-scan whether any active attacker is currently inside the EW jammer's ±15° beam
            bool anyAttackerInJammerBeam = IsAnyAttackerInJammerBeam(ewJammer);

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
                    UpdateAttacker(agent, attackerIndex, deltaTime, baseAlt, speedMs, ewJammer, anyAttackerInJammerBeam, currentScenarioTimeMs);
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

        private bool IsAnyAttackerInJammerBeam(EWJammerState ewJammer)
        {
            if (ewJammer == null || !ewJammer.engaged || Agents == null)
                return false;

            for (int i = 0; i < Agents.Count; i++)
            {
                SwarmAgentState a = Agents[i];
                if (a == null || a.isDecoy || a.isNeutralised)
                    continue;

                float agentBearing = CalculateHeadingDeg(a.positionWorld - TargetOrigin);
                float diff = Math.Abs(DeltaAngle(ewJammer.bearingDeg, agentBearing));
                if (diff <= EwEvasionConeHalfAngleDeg)
                {
                    return true;
                }
            }
            return false;
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

                // SA-P1-B-ii SPLIT state initialization:
                // Pair A (attIdx 0, 2) approaches from azimuth 0° (North, +Z);
                // Pair B (attIdx 1, 3) approaches from azimuth 180° (South, -Z);
                // All 4 attackers set rfEmitting = false for 5s after split (EW evasion — go silent).
                float splitRadius = Math.Max(minAttackerDist, ExclusionZoneRadius + 5.0f);
                int attIdx = 0;
                for (int i = 0; i < Agents.Count; i++)
                {
                    SwarmAgentState a = Agents[i];
                    if (a == null || a.isDecoy || a.isNeutralised)
                        continue;

                    bool isPairA = (attIdx % 2 == 0);
                    float pairWingOffset = (attIdx < 2) ? -2.0f : 2.0f;
                    float zSign = isPairA ? 1.0f : -1.0f; // Pair A: +Z (Azimuth 0° North), Pair B: -Z (Azimuth 180° South)

                    a.positionWorld = new Vector3(
                        TargetOrigin.x + pairWingOffset,
                        a.altitudeM,
                        TargetOrigin.z + (zSign * splitRadius));

                    Vector3 toOrigin = TargetOrigin - a.positionWorld;
                    toOrigin.y = 0f;
                    Vector3 approachDir = toOrigin.sqrMagnitude > 0.0001f ? toOrigin.normalized : new Vector3(0f, 0f, -zSign);
                    float spd = Profile != null ? Profile.SpeedMs : (120.0f / 3.6f);

                    a.velocityMs = approachDir * spd;
                    a.headingDeg = CalculateHeadingDeg(a.velocityMs);
                    a.rfEmitting = false;

                    attIdx++;
                }

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
                // SA-P1-B-ii SPLIT / PINCER: peel away at ±45° angle (seed-driven), climb to 20m alt
                agent.altitudeM = MoveTowards(agent.altitudeM, DecoySplitClimbAltitudeM, dt * 8.0f);
                float peelSide = _seedSign * ((decoyIdx % 2 == 0) ? 1.0f : -1.0f);
                float peelAngle = DecoyPeelAngleDeg * peelSide;
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

        private void UpdateAttacker(
            SwarmAgentState agent,
            int attIdx,
            float dt,
            float baseAlt,
            float speedMs,
            EWJammerState ewJammer,
            bool anyAttackerInJammerBeam,
            long currentMs)
        {
            float ewTimer;
            if (!_attackerEwSilentTimers.TryGetValue(attIdx, out ewTimer))
            {
                ewTimer = 0f;
            }
            if (ewTimer > 0f)
            {
                ewTimer -= dt;
                if (ewTimer < 0f) ewTimer = 0f;
                _attackerEwSilentTimers[attIdx] = ewTimer;
            }

            int hopCount;
            if (!_attackerEwHopCounts.TryGetValue(attIdx, out hopCount))
            {
                hopCount = 0;
                _attackerEwHopCounts[attIdx] = 0;
            }

            // Evaluate dynamic EW evasion for this attacker (bearingDeg within 15° of agent bearing)
            bool isDirectlyJammed = false;
            if (ewJammer != null && ewJammer.engaged)
            {
                float agentBearing = CalculateHeadingDeg(agent.positionWorld - TargetOrigin);
                float diff = Math.Abs(DeltaAngle(ewJammer.bearingDeg, agentBearing));
                if (diff <= EwEvasionConeHalfAngleDeg)
                {
                    isDirectlyJammed = true;
                    if (ewTimer <= 0.001f)
                    {
                        HopAttackerFrequency(agent);
                    }
                    ewTimer = EwEvasionSilenceSec; // 3.0s RF silence
                    _attackerEwSilentTimers[attIdx] = ewTimer;
                    hopCount++;
                    _attackerEwHopCounts[attIdx] = hopCount;
                }
            }

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

                if (isDirectlyJammed)
                {
                    float steerSign = GetSeedAlternatedSteerSign(attIdx, hopCount, agent.positionWorld.x);
                    combinedVel = RotateHorizontalVector(combinedVel, EwEvasionSteerOffsetDeg * steerSign);
                    agent.positionWorld.x += combinedVel.x * dt * 0.35f;
                    agent.positionWorld.z += combinedVel.z * dt * 0.55f;
                }
                else
                {
                    agent.positionWorld.x += combinedVel.x * dt;
                    agent.positionWorld.z += combinedVel.z * dt;
                }

                agent.velocityMs = combinedVel;
                agent.positionWorld.y = agent.altitudeM;
                agent.headingDeg = CalculateHeadingDeg(agent.velocityMs);
                agent.rfEmitting = (ewTimer <= 0.0f);
                agent.propellerRpm = 11400;
            }
            else
            {
                // SA-P1-B-ii SPLIT / PINCER / DIVE_ATTACK:
                // Pair A (attIdx % 2 == 0): approaches from azimuth 0° (North, +Z -> -Z)
                // Pair B (attIdx % 2 == 1): approaches from azimuth 180° (South, -Z -> +Z)
                bool isPairA = (attIdx % 2 == 0);

                // Ensure Pair B is positioned on the Southern pincer axis (azimuth 180°) if loaded mid-flight
                float relZ = agent.positionWorld.z - TargetOrigin.z;
                if (!isPairA && relZ > 0.0f)
                {
                    agent.positionWorld.z = TargetOrigin.z - relZ;
                }
                else if (isPairA && relZ < 0.0f)
                {
                    agent.positionWorld.z = TargetOrigin.z - relZ;
                }

                float dist = HorizontalDistanceToOrigin(agent.positionWorld);

                // EW Evasion RF emission rule:
                // - Silent for 5s after split (post-split EW evasion)
                // - Also silent for 3s when dynamically jammed by EW jammer
                bool inPostSplitSilence = (_elapsedSeconds - _splitTimeSeconds) < PostSplitRfSilenceSec;
                agent.rfEmitting = !inPostSplitSilence && (ewTimer <= 0.0f);

                // Altitude profile: at <= 50m, descend to 2m altitude (low skimming approach) and enter DIVE_ATTACK
                if (dist <= DiveTriggerDistanceM)
                {
                    agent.altitudeM = MoveTowards(agent.altitudeM, AttackerSkimAltitudeM, dt * 12.0f);
                    SetState(TacticState.DIVE_ATTACK);
                }
                else
                {
                    agent.altitudeM = baseAlt;
                }

                // Direct pincer closure vector toward TargetOrigin along the pair's attack axis
                Vector3 toOrigin = TargetOrigin - agent.positionWorld;
                toOrigin.y = 0f;
                float toOriginMag = (float)Math.Sqrt(toOrigin.sqrMagnitude);
                Vector3 targetVector = toOriginMag > 0.0001f
                    ? (toOrigin * (1.0f / toOriginMag))
                    : (isPairA ? new Vector3(0f, 0f, -1f) : new Vector3(0f, 0f, 1f));

                if (isDirectlyJammed)
                {
                    // Dynamic EW evasion: steer ±30° off current heading (seed-alternated)
                    float steerSign = GetSeedAlternatedSteerSign(attIdx, hopCount, agent.positionWorld.x);
                    float steerOffset = EwEvasionSteerOffsetDeg * steerSign;
                    targetVector = RotateHorizontalVector(targetVector, steerOffset);
                }

                agent.velocityMs = targetVector * speedMs;

                // During the initial SPLIT fan-out or when the pincer wave is actively jammed on an attack axis,
                // radial closure is paced so the split maneuver completes and EW jamming delays exclusion breach.
                float closureScale = 1.0f;
                if (CurrentState == TacticState.SPLIT)
                {
                    closureScale = 0.15f;
                }
                if (isDirectlyJammed || anyAttackerInJammerBeam)
                {
                    closureScale *= 0.5f;
                }

                agent.positionWorld.x += agent.velocityMs.x * dt * (isDirectlyJammed ? 0.35f : closureScale);
                // Keep lateral offset bounded near the pincer corridor so bearing stays on the 0° / 180° pincer axis
                float relX = agent.positionWorld.x - TargetOrigin.x;
                if (Math.Abs(relX) > 10.0f)
                {
                    agent.positionWorld.x = TargetOrigin.x + (Math.Sign(relX) * 10.0f);
                }

                agent.positionWorld.y = agent.altitudeM;
                agent.positionWorld.z += agent.velocityMs.z * dt * closureScale;
                agent.headingDeg = CalculateHeadingDeg(agent.velocityMs);
                agent.propellerRpm = 11400;
            }
        }

        private float GetSeedAlternatedSteerSign(int attIdx, int hopCount, float currentX)
        {
            // If near corridor edge, steer back toward corridor centre so agent stays within pincer axis
            if (currentX - TargetOrigin.x > 8.0f)
                return -1.0f;
            if (currentX - TargetOrigin.x < -8.0f)
                return 1.0f;

            bool even = (((Seed + attIdx + (hopCount / 5)) & 1) == 0);
            return _seedSign * (even ? 1.0f : -1.0f);
        }

        private void HopAttackerFrequency(SwarmAgentState agent)
        {
            if (Profile != null && Profile.rfFreqGHz != null && Profile.rfFreqGHz.Length > 1)
            {
                for (int i = 0; i < Profile.rfFreqGHz.Length; i++)
                {
                    if (Math.Abs(Profile.rfFreqGHz[i] - agent.rfFreqGHz) > 0.01f)
                    {
                        agent.rfFreqGHz = Profile.rfFreqGHz[i];
                        return;
                    }
                }
            }
            agent.rfFreqGHz = (Math.Abs(agent.rfFreqGHz - 5.825f) < 0.1f) ? 2.4f : 5.825f;
        }

        private static Vector3 RotateHorizontalVector(Vector3 v, float angleDeg)
        {
            float horizMag = (float)Math.Sqrt((v.x * v.x) + (v.z * v.z));
            if (horizMag < 0.0001f)
                return v;

            float curAngleDeg = (float)(Math.Atan2(v.x, v.z) * Rad2Deg);
            float newRad = (curAngleDeg + angleDeg) * Deg2Rad;
            return new Vector3((float)Math.Sin(newRad) * horizMag, v.y, (float)Math.Cos(newRad) * horizMag);
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
