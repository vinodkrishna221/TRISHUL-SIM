// ============================================================
// PincerApproachSelfTest.cs — Runnable Self-Test for SA-P1-B-i
// TRISHUL-SIM · swarm-ai track (Venu)
//
// Validates:
// 1. Exact mission card test (SA-P1-B-i):
//    - Punjab scenario: 6 agents spawn ~300m out, flock toward origin (Vector3.zero).
//    - Decoys (2) fly at higher altitude (altitudeM + 5m = 13m) and oscillate laterally.
//    - Attackers (4) stay low at altitudeM (8m) and maintain coherent formation.
//    - All agents running APPROACH; radarDetectedCount = 6 visible during approach.
//    - When lead attacker hits 150m mark, Console logs "PINCER SPLIT TRIGGERED" and
//      state transitions to SPLIT with lowered Boids cohesion weight.
// 2. TacticTree abstract base & TacticState enum {APPROACH, SPLIT, PINCER, DIVE, NEUTRALISED}
//    plus TargetPosition = Vector3.zero property.
// 3. Seed-driven sinusoidal lateral oscillation for decoys (15m amplitude, 8s period).
// 4. SwarmManager registration of PincerLowFastTactic when tacticTree == "PINCER_LOW_FAST"
//    and BoidFlockController weight synchronization.
// ============================================================

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using TrishulSim.Sensors;
using TrishulSim.SwarmAI;
using TrishulSim.SwarmAI.HBT;
using TrishulSim.Mocks;

namespace TrishulSim.SwarmAI
{
    public static class PincerApproachSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllTests();
                Console.WriteLine("\n[PASS] All SA-P1-B-i HBT PINCER_LOW_FAST approach & split-trigger tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("\n[FAIL] " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }
#endif

        public static void RunAllTests()
        {
            Console.WriteLine("=== Running TRISHUL-SIM SA-P1-B-i HBT PINCER_LOW_FAST Approach Self-Test ===");

            TestExactMissionCardPunjabApproachAndSplitTrigger();
            TestTacticTreeBaseAndTacticStateEnum();
            TestDecoySinusoidalOscillationAndSeedDeterminism();
            TestSwarmManagerPincerRegistrationAndBoidsWeights();
        }

        /// <summary>
        /// Exact test from missions/venu-mission.md & plans/swarm-ai-phase-plan.md:
        /// Punjab scenario: 6 agents spawn ~300m out, flock toward origin. Decoys visible at higher
        /// altitude, oscillating laterally. When lead attacker hits 150m mark, Console logs
        /// "PINCER SPLIT TRIGGERED". All agents running APPROACH; radarDetectedCount = 6 visible.
        /// </summary>
        public static void TestExactMissionCardPunjabApproachAndSplitTrigger()
        {
            Console.WriteLine("\n-- Test 1: Exact Mission Card Punjab Approach (~300m -> 150m SPLIT_TRIGGER) --");

            SwarmManager manager = new SwarmManager();
            SensorSuiteComputer sensorComputer = new SensorSuiteComputer();
            manager.RegisterSensorSuiteComputer(sensorComputer);

            SwarmProfile punjabProfile = SwarmMock.GetPunjabProfile(); // 6 agents (2 decoys, 4 attackers), 120 km/h, alt 8m
            int seed = 42819;

            manager.OnScenarioStart(punjabProfile, seed, "punjab-fog-pincer-01", Vector3.zero);

            Assert(manager.CurrentTactic is PincerLowFastTactic,
                "SwarmManager.CurrentTactic must be PincerLowFastTactic for PINCER_LOW_FAST");
            PincerLowFastTactic tactic = (PincerLowFastTactic)manager.CurrentTactic;

            Assert(tactic.CurrentState == TacticState.APPROACH,
                "Initial tactic state must be TacticState.APPROACH");
            Assert(Vector3.Distance(tactic.TargetPosition, Vector3.zero) < 0.0001f,
                "TargetPosition must be Vector3.zero (defended origin)");

            SwarmAgentState[] initialSnap = manager.GetSwarmSnapshot();
            Assert(initialSnap.Length == 6, "Punjab scenario must spawn 6 agents");

            // Verify spawn ~300m out and initial decoy/attacker altitudes
            int decoyCount = 0;
            int attackerCount = 0;
            float[] initialDecoyX = new float[2];

            for (int i = 0; i < initialSnap.Length; i++)
            {
                SwarmAgentState a = initialSnap[i];
                float distToOrigin = Vector3.Distance(new Vector3(a.positionWorld.x, 0f, a.positionWorld.z), Vector3.zero);
                Assert(distToOrigin >= 290.0f && distToOrigin <= 310.0f,
                    string.Format("Agent {0} must spawn ~300m out, got {1:F2}m", a.agentId, distToOrigin));

                if (a.isDecoy)
                {
                    Assert(Math.Abs(a.altitudeM - (punjabProfile.altitudeM + 5.0f)) < 0.01f,
                        string.Format("Decoy {0} must be at altitudeM + 5m (13.0m), got {1:F2}m", a.agentId, a.altitudeM));
                    initialDecoyX[decoyCount] = a.positionWorld.x;
                    decoyCount++;
                }
                else
                {
                    Assert(Math.Abs(a.altitudeM - punjabProfile.altitudeM) < 0.01f,
                        string.Format("Attacker {0} must be at altitudeM (8.0m), got {1:F2}m", a.agentId, a.altitudeM));
                    attackerCount++;
                }
            }

            Assert(decoyCount == 2 && attackerCount == 4, "Must have 2 decoys and 4 attackers");

            string capturedTriggerLog = null;
            tactic.OnLogMessage += msg => { capturedTriggerLog = msg; };

            bool observedRadar6InApproach = false;
            float maxDecoyLateralExcursionM = 0.0f;
            int splitTick = -1;
            float leadAttackerDistAtSplit = -1.0f;

            // Step at 10 Hz (dt = 0.1s) from ~300m until SPLIT_TRIGGER fires at <= 150m (~4.5s = 45 ticks)
            const float dt = 0.1f;
            for (int tick = 1; tick <= 60; tick++)
            {
                long tMs = tick * 100L;
                bool wasSplitBeforeTick = tactic.SplitTriggered;

                manager.Tick(dt, tMs, null, Vector3.zero);
                SensorReadings readings = manager.InjectSensorReadings();
                SwarmAgentState[] snap = manager.GetSwarmSnapshot();

                // Track decoy lateral oscillation amplitude relative to initial X
                float d0Excursion = Math.Abs(snap[0].positionWorld.x - initialDecoyX[0]);
                if (d0Excursion > maxDecoyLateralExcursionM)
                {
                    maxDecoyLateralExcursionM = d0Excursion;
                }

                if (!tactic.SplitTriggered)
                {
                    Assert(tactic.CurrentState == TacticState.APPROACH,
                        "Before lead attacker reaches 150m, state must remain APPROACH");

                    // Verify decoys remain at altitudeM + 5m (13m) and attackers at altitudeM (8m)
                    for (int i = 0; i < snap.Length; i++)
                    {
                        if (snap[i].isDecoy)
                        {
                            Assert(Math.Abs(snap[i].altitudeM - 13.0f) < 0.01f,
                                "During APPROACH, decoy altitude must remain altitudeM + 5m (13m)");
                        }
                        else
                        {
                            Assert(Math.Abs(snap[i].altitudeM - 8.0f) < 0.01f,
                                "During APPROACH, attacker altitude must remain altitudeM (8m)");
                        }
                    }

                    if (readings.radarDetectedCount == 6)
                    {
                        observedRadar6InApproach = true;
                    }
                }
                else if (!wasSplitBeforeTick)
                {
                    splitTick = tick;
                    leadAttackerDistAtSplit = tactic.SplitTriggeredDistanceM;
                    Console.WriteLine(string.Format(
                        "[Tick {0} (t={1}ms)] Lead attacker reached {2:F2}m (<= 150m) -> state={3}, log=\"{4}\"",
                        tick, tMs, leadAttackerDistAtSplit, tactic.CurrentState, capturedTriggerLog));
                    break;
                }
            }

            Assert(observedRadar6InApproach,
                "All 6 agents must be radar-detected (radarDetectedCount == 6) during APPROACH before split");
            Assert(maxDecoyLateralExcursionM > 5.0f,
                "Decoys must oscillate laterally during APPROACH (observed excursion=" + maxDecoyLateralExcursionM + "m)");
            Assert(tactic.SplitTriggered && splitTick > 0,
                "SPLIT_TRIGGER must fire when lead attacker reaches <= 150m");
            Assert(leadAttackerDistAtSplit <= 150.0f && leadAttackerDistAtSplit > 140.0f,
                "SPLIT_TRIGGER must fire right as lead attacker crosses 150m mark, got " + leadAttackerDistAtSplit);
            Assert(tactic.CurrentState == TacticState.SPLIT && tactic.CurrentState == TacticState.SPLIT_TRIGGER,
                "State must transition to TacticState.SPLIT on trigger");
            Assert(capturedTriggerLog == "PINCER SPLIT TRIGGERED" && tactic.LastLogMessage == "PINCER SPLIT TRIGGERED",
                "Console/log must emit exact string \"PINCER SPLIT TRIGGERED\"");

            Console.WriteLine("[PASS] Exact Mission Card Punjab Approach & SPLIT_TRIGGER PASSED");
        }

        /// <summary>
        /// Validates TacticTree abstract base class and TacticState enum values required by SA-P1-B-i Step 2.
        /// </summary>
        public static void TestTacticTreeBaseAndTacticStateEnum()
        {
            Console.WriteLine("\n-- Test 2: TacticTree Base Class & TacticState Enum --");

            // Verify all required enum states exist and are distinct/compatible
            TacticState sApproach = TacticState.APPROACH;
            TacticState sSplit = TacticState.SPLIT;
            TacticState sPincer = TacticState.PINCER;
            TacticState sDive = TacticState.DIVE;
            TacticState sNeutralised = TacticState.NEUTRALISED;

            Assert(sApproach != sSplit && sSplit != sPincer && sPincer != sDive && sDive != sNeutralised,
                "TacticState enum {APPROACH, SPLIT, PINCER, DIVE, NEUTRALISED} must have distinct states");
            Assert(TacticState.DIVE == TacticState.DIVE_ATTACK,
                "TacticState.DIVE and TacticState.DIVE_ATTACK must be compatible aliases");
            Assert(TacticState.SPLIT == TacticState.SPLIT_TRIGGER,
                "TacticState.SPLIT and TacticState.SPLIT_TRIGGER must be compatible aliases");

            PincerLowFastTactic tactic = new PincerLowFastTactic();
            Assert(tactic is TacticTree, "PincerLowFastTactic must inherit from TacticTree");
            Assert(tactic.Name == "PINCER_LOW_FAST", "PincerLowFastTactic.Name must be PINCER_LOW_FAST");
            Assert(Vector3.Distance(tactic.TargetPosition, Vector3.zero) < 0.0001f,
                "Default TargetPosition must be Vector3.zero");

            Console.WriteLine("[PASS] TacticTree Base Class & TacticState Enum PASSED");
        }

        /// <summary>
        /// Validates seed-driven sinusoidal lateral oscillation for decoys (15m amplitude, 8s period).
        /// </summary>
        public static void TestDecoySinusoidalOscillationAndSeedDeterminism()
        {
            Console.WriteLine("\n-- Test 3: Seed-Driven Decoy Sinusoidal Oscillation (15m Amplitude, 8s Period) --");

            SwarmProfile profile = SwarmMock.GetPunjabProfile();
            PincerLowFastTactic tacticSeedA = new PincerLowFastTactic();
            tacticSeedA.Initialize(profile, 42819, new List<SwarmAgentState>(), Vector3.zero);

            float minOffset = float.MaxValue;
            float maxOffset = float.MinValue;

            // Sample one full 8-second period in 0.05s increments
            for (int step = 0; step <= 160; step++)
            {
                float tSec = step * 0.05f;
                float off0 = tacticSeedA.ComputeDecoyLateralOffsetM(0, tSec);
                float off1 = tacticSeedA.ComputeDecoyLateralOffsetM(1, tSec);

                if (off0 < minOffset) minOffset = off0;
                if (off0 > maxOffset) maxOffset = off0;

                // Decoys 0 and 1 must diverge oppositely (equal magnitude, opposite sign)
                Assert(Math.Abs(off0 + off1) < 0.001f,
                    "Decoy 0 and Decoy 1 lateral oscillation offsets must have opposite signs");
            }

            Assert(Math.Abs(maxOffset - 15.0f) < 0.05f && Math.Abs(minOffset - (-15.0f)) < 0.05f,
                string.Format("Decoy lateral oscillation amplitude must be 15.0m (got min={0:F2}m, max={1:F2}m)",
                    minOffset, maxOffset));

            // Verify exact 8.0s period: offset(t) == offset(t + 8.0s)
            float tSample = 1.37f;
            float valAtT = tacticSeedA.ComputeDecoyLateralOffsetM(0, tSample);
            float valAtTPlus8 = tacticSeedA.ComputeDecoyLateralOffsetM(0, tSample + 8.0f);
            Assert(Math.Abs(valAtT - valAtTPlus8) < 0.001f,
                "Decoy lateral oscillation period must be exactly 8.0 seconds");

            // Verify seed-driven variation: different seed produces different initial phase/offset
            PincerLowFastTactic tacticSeedB = new PincerLowFastTactic();
            tacticSeedB.Initialize(profile, 91024, new List<SwarmAgentState>(), Vector3.zero);
            float seedAInitial = tacticSeedA.ComputeDecoyLateralOffsetM(0, 0.0f);
            float seedBInitial = tacticSeedB.ComputeDecoyLateralOffsetM(0, 0.0f);
            Assert(Math.Abs(seedAInitial - seedBInitial) > 0.1f,
                "Different scenario seeds must produce distinct decoy oscillation phases");

            Console.WriteLine(string.Format(
                "Decoy oscillation verified: amplitude=[{0:F2}m, +{1:F2}m], period=8.0s, seedA(0s)={2:F2}m vs seedB(0s)={3:F2}m",
                minOffset, maxOffset, seedAInitial, seedBInitial));
            Console.WriteLine("[PASS] Seed-Driven Decoy Sinusoidal Oscillation PASSED");
        }

        /// <summary>
        /// Validates SwarmManager registration of PincerLowFastTactic and BoidFlockController weight modulation.
        /// </summary>
        public static void TestSwarmManagerPincerRegistrationAndBoidsWeights()
        {
            Console.WriteLine("\n-- Test 4: SwarmManager PincerLowFastTactic Registration & Boid Weights --");

            SwarmManager manager = new SwarmManager();
            SwarmProfile profile = SwarmMock.GetPunjabProfile();
            Assert(profile.tacticTree == "PINCER_LOW_FAST", "Punjab profile tacticTree must be PINCER_LOW_FAST");

            manager.OnScenarioStart(profile, 42819, "punjab-fog-pincer-01", Vector3.zero);
            PincerLowFastTactic tactic = manager.CurrentTactic as PincerLowFastTactic;
            Assert(tactic != null, "SwarmManager must register PincerLowFastTactic when tacticTree == PINCER_LOW_FAST");
            Assert(ReferenceEquals(tactic.FlockController, manager.FlockController),
                "SwarmManager must bind its BoidFlockController to PincerLowFastTactic");

            // In APPROACH: alignment weight is high (1.6), cohesion weight is normal (0.8)
            Assert(Math.Abs(manager.FlockController.AlignmentWeight - PincerLowFastTactic.ApproachAlignmentWeight) < 0.001f,
                "In APPROACH, FlockController.AlignmentWeight must be high (1.6)");
            Assert(Math.Abs(manager.FlockController.CohesionWeight - PincerLowFastTactic.ApproachCohesionWeight) < 0.001f,
                "In APPROACH, FlockController.CohesionWeight must be 0.8");

            // Move lead attacker inside 150m to trigger SPLIT and verify cohesion weight drops
            SwarmAgentState[] snap = manager.GetSwarmSnapshot();
            for (int i = 0; i < snap.Length; i++)
            {
                if (!snap[i].isDecoy)
                {
                    snap[i].positionWorld = new Vector3(snap[i].positionWorld.x, snap[i].altitudeM, 148.0f);
                    break;
                }
            }

            manager.Tick(0.1f, 4500, null, Vector3.zero);

            Assert(tactic.CurrentState == TacticState.SPLIT,
                "When lead attacker is at 148m (< 150m), state must be SPLIT");
            Assert(Math.Abs(manager.FlockController.CohesionWeight - PincerLowFastTactic.PincerCohesionWeight) < 0.001f,
                "On SPLIT, FlockController.CohesionWeight must drop to PincerCohesionWeight (0.05) for independent motion");

            Console.WriteLine(string.Format(
                "Boid weights verified: APPROACH(Align={0:F2}, Coh={1:F2}) -> SPLIT(Coh={2:F2})",
                PincerLowFastTactic.ApproachAlignmentWeight,
                PincerLowFastTactic.ApproachCohesionWeight,
                manager.FlockController.CohesionWeight));
            Console.WriteLine("[PASS] SwarmManager PincerLowFastTactic Registration & Boid Weights PASSED");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Assertion failed: " + message);
            }
        }
    }
}
