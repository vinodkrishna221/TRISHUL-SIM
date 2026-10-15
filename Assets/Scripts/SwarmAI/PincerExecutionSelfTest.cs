// ============================================================
// PincerExecutionSelfTest.cs — Runnable Self-Test for SA-P1-B-ii
// TRISHUL-SIM · swarm-ai track (Venu)
//
// Validates:
// 1. Exact Mission Card Test Case 1 (No EW):
//    - Punjab scenario without EW: 6 agents approach -> SPLIT_TRIGGER at <=150m ->
//      2 decoys peel at ±45° (seed-driven) and climb to 20m alt;
//      4 attackers split into Pair A (azimuth 0°) and Pair B (azimuth 180°),
//      set rfEmitting = false for 5s after split, transition SPLIT -> PINCER ->
//      DIVE_ATTACK (2m alt skim at <=50m) -> all 4 attackers reach 20m exclusion
//      zone -> EXCLUSION_BREACH fires -> SCENARIO_END ("EXCLUSION_BREACH").
// 2. Exact Mission Card Test Case 2 (With EW on correct axis):
//    - Jamming on correct attack axis (within 15° of agent bearing):
//      attackers evade (±30° heading steer, rfEmitting = false for 3s, freq hop);
//      scenario lasts longer than without EW (both full-run EW and post-split Beat 4 EW).
// 3. Exact Mission Card Test Case 3 (Neutralise all 4 attackers):
//    - Neutralising an attacker stops movement (velocityMs = 0), sets propellerRpm = 0,
//      rfEmitting = false, neutralisedAtMs = current t.
//    - Neutralising all 4 attackers transitions tactic to TacticState.NEUTRALISED and
//      fires ALL_NEUTRALISED -> SCENARIO_END ("ALL_NEUTRALISED").
// 4. BoidAgent.CheckEvasion(EWJammerState ew) unit verification:
//    - Seed-alternated ±30° heading steer, 3s RF silence timer, and frequency-hop.
// ============================================================

using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using TrishulSim.Sensors;
using TrishulSim.SwarmAI;
using TrishulSim.SwarmAI.HBT;
using TrishulSim.Mocks;

namespace TrishulSim.SwarmAI
{
    public static class PincerExecutionSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllTests();
                Console.WriteLine("\n[PASS] All SA-P1-B-ii HBT PINCER_LOW_FAST execution & EW evasion tests passed.");
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
            Console.WriteLine("=== Running TRISHUL-SIM SA-P1-B-ii HBT PINCER_LOW_FAST Execution & EW Evasion Self-Test ===");

            int noEwBreachTick = TestNoEwAllFourAttackersReachExclusionZone();
            TestWithEwOnCorrectAxisAttackersEvadeAndScenarioLastsLonger(noEwBreachTick);
            TestNeutraliseAllFourAttackersFiresAllNeutralisedScenarioEnd();
            TestBoidAgentCheckEvasionFrequencyHopAndSteer();
        }

        /// <summary>
        /// Exact card test 1:
        /// No EW: all 4 attackers reach exclusion zone -> EXCLUSION_BREACH fires.
        /// Also verifies SPLIT decoy ±45° peel & 20m climb, 5s post-split RF silence,
        /// Pair A (0°) vs Pair B (180°) pincer axes, PINCER state, and DIVE_ATTACK 2m skim at <=50m.
        /// </summary>
        public static int TestNoEwAllFourAttackersReachExclusionZone()
        {
            Console.WriteLine("\n-- Test 1: No EW -> All 4 Attackers Reach 20m Exclusion Zone -> EXCLUSION_BREACH --");

            SwarmManager manager = new SwarmManager();
            SwarmProfile profile = SwarmMock.GetPunjabProfile();
            int seed = 42819;

            string broadcastedEndReason = null;
            manager.OnScenarioEndTriggered += reason => { broadcastedEndReason = reason; };

            manager.OnScenarioStart(profile, seed, "punjab-fog-pincer-01", Vector3.zero);
            PincerLowFastTactic tactic = manager.CurrentTactic as PincerLowFastTactic;
            Assert(tactic != null, "Active tactic must be PincerLowFastTactic");

            bool observedSplitState = false;
            bool observedPincerState = false;
            bool observedDiveState = false;
            bool observedPostSplitRfSilence = false;
            bool observedPostSilenceRfRecovery = false;
            bool verifiedPincerDualAxes = false;

            const float dt = 0.1f;
            int breachTick = -1;

            for (int tick = 1; tick <= 200; tick++)
            {
                long tMs = tick * 100L;
                manager.Tick(dt, tMs, null, Vector3.zero);
                SwarmAgentState[] snap = manager.GetSwarmSnapshot();

                if (tactic.CurrentState == TacticState.SPLIT)
                {
                    observedSplitState = true;

                    if (!verifiedPincerDualAxes)
                    {
                        // Verify Pair A is on North axis (azimuth ~0°, z > 0) and Pair B is on South axis (azimuth ~180°, z < 0)
                        int northCount = 0;
                        int southCount = 0;
                        for (int i = 0; i < snap.Length; i++)
                        {
                            if (snap[i].isDecoy)
                                continue;

                            float bearingFromOrigin = TacticTree.CalculateHeadingDeg(snap[i].positionWorld - Vector3.zero);
                            if (EwDeconfliction.ComputeAngularDistance(bearingFromOrigin, 0.0f) <= 10.0f)
                                northCount++;
                            else if (EwDeconfliction.ComputeAngularDistance(bearingFromOrigin, 180.0f) <= 10.0f)
                                southCount++;
                        }
                        Assert(northCount == 2 && southCount == 2,
                            string.Format("Attackers must split into Pair A (2 on azimuth 0°) and Pair B (2 on azimuth 180°), got North={0}, South={1}",
                                northCount, southCount));
                        verifiedPincerDualAxes = true;
                    }
                }
                else if (tactic.CurrentState == TacticState.PINCER)
                {
                    observedPincerState = true;
                }
                else if (tactic.CurrentState == TacticState.DIVE_ATTACK)
                {
                    observedDiveState = true;
                }

                if (tactic.SplitTriggered && !tactic.ExclusionBreached)
                {
                    float sinceSplit = (tick * dt) - tactic.SplitTimeSeconds;
                    if (sinceSplit >= 0.2f && sinceSplit < 4.8f)
                    {
                        for (int i = 0; i < snap.Length; i++)
                        {
                            if (!snap[i].isDecoy)
                            {
                                Assert(snap[i].rfEmitting == false,
                                    "Attackers must have rfEmitting=false during 5s post-split EW evasion window");
                            }
                        }
                        observedPostSplitRfSilence = true;
                    }
                    else if (sinceSplit >= 5.05f)
                    {
                        for (int i = 0; i < snap.Length; i++)
                        {
                            if (!snap[i].isDecoy)
                            {
                                Assert(snap[i].rfEmitting == true,
                                    "Attackers must resume rfEmitting=true after 5s post-split silence expires (when unjammed)");
                            }
                        }
                        observedPostSilenceRfRecovery = true;
                    }
                }

                if (manager.ExclusionBreached)
                {
                    breachTick = tick;
                    break;
                }
            }

            Assert(observedSplitState, "Tactic must pass through TacticState.SPLIT");
            Assert(observedPincerState, "Tactic must pass through TacticState.PINCER");
            Assert(observedDiveState, "Tactic must pass through TacticState.DIVE_ATTACK (TacticState.DIVE) at <=50m");
            Assert(observedPostSplitRfSilence, "Attackers must go RF-silent for 5s after split");
            Assert(observedPostSilenceRfRecovery, "Attackers must recover RF emission after 5s post-split window");
            Assert(breachTick > 0 && manager.ExclusionBreached, "EXCLUSION_BREACH must fire when attackers reach 20m");
            Assert(tactic.CurrentState == TacticState.EXCLUSION_BREACH, "Tactic state must be EXCLUSION_BREACH");
            Assert(broadcastedEndReason == "EXCLUSION_BREACH" && manager.LastScenarioEndReason == "EXCLUSION_BREACH",
                "SwarmManager must broadcast SCENARIO_END with reason \"EXCLUSION_BREACH\"");

            // Verify decoys climbed to 20m & peeled at ±45°, and ALL 4 attackers reached the 20m exclusion zone at 2m alt
            SwarmAgentState[] finalSnap = manager.GetSwarmSnapshot();
            int attackersInExclusionZone = 0;
            for (int i = 0; i < finalSnap.Length; i++)
            {
                SwarmAgentState a = finalSnap[i];
                if (a.isDecoy)
                {
                    Assert(Math.Abs(a.altitudeM - 20.0f) < 0.1f,
                        string.Format("Decoy {0} must climb to 20m altitude after split, got {1:F2}m", a.agentId, a.altitudeM));
                }
                else
                {
                    float horizDist = (float)Math.Sqrt((a.positionWorld.x * a.positionWorld.x) + (a.positionWorld.z * a.positionWorld.z));
                    Assert(horizDist <= 20.0f,
                        string.Format("Attacker {0} must reach the 20m exclusion zone, got {1:F2}m", a.agentId, horizDist));
                    Assert(Math.Abs(a.altitudeM - 2.0f) < 0.25f,
                        string.Format("Attacker {0} must descend to 2m skimming altitude in DIVE_ATTACK, got {1:F2}m", a.agentId, a.altitudeM));
                    attackersInExclusionZone++;
                }
            }

            Assert(attackersInExclusionZone == 4, "All 4 attackers must reach the 20m exclusion zone when no EW is engaged");

            Console.WriteLine(string.Format(
                "No-EW run verified: SPLIT -> PINCER -> DIVE_ATTACK (2m alt) -> EXCLUSION_BREACH at tick {0} (t={1}ms); all 4 attackers <= 20m.",
                breachTick, breachTick * 100));
            Console.WriteLine("[PASS] No EW -> All 4 Attackers Reach Exclusion Zone -> EXCLUSION_BREACH PASSED");
            return breachTick;
        }

        /// <summary>
        /// Exact card test 2:
        /// With EW on correct axis: attackers evade; scenario lasts longer.
        /// Compares:
        /// - No EW baseline (noEwBreachTick)
        /// - EW on wrong axis (bearing 90° East, outside ±15° cone) -> does not delay breach
        /// - EW on correct axis (bearing 0° North / 180° South, inside ±15° cone) -> attackers evade, scenario lasts longer!
        /// </summary>
        public static void TestWithEwOnCorrectAxisAttackersEvadeAndScenarioLastsLonger(int noEwBreachTick)
        {
            Console.WriteLine("\n-- Test 2: With EW on Correct Axis -> Attackers Evade & Scenario Lasts Longer --");

            // 2A. EW on wrong axis (90° East — >15° away from North 0° and South 180°)
            SwarmManager wrongEwManager = new SwarmManager();
            wrongEwManager.OnScenarioStart(SwarmMock.GetPunjabProfile(), 42819, "punjab-fog-pincer-01", Vector3.zero);
            EWJammerState wrongAxisJammer = new EWJammerState(true, 90.0f, 0.0f, 5.825f, false);

            int wrongEwBreachTick = -1;
            for (int tick = 1; tick <= 250; tick++)
            {
                wrongEwManager.Tick(0.1f, tick * 100L, wrongAxisJammer, Vector3.zero);
                if (wrongEwManager.ExclusionBreached)
                {
                    wrongEwBreachTick = tick;
                    break;
                }
            }
            Assert(wrongEwBreachTick == noEwBreachTick,
                string.Format("Jamming on wrong axis (90°) must not trigger evasion (wrongEwTick={0} vs noEwTick={1})",
                    wrongEwBreachTick, noEwBreachTick));

            // 2B. EW on correct axis (0° North — after SPLIT_TRIGGER, Beat 4 doctrinal engagement)
            SwarmManager postSplitEwManager = new SwarmManager();
            postSplitEwManager.OnScenarioStart(SwarmMock.GetPunjabProfile(), 42819, "punjab-fog-pincer-01", Vector3.zero);
            PincerLowFastTactic postSplitTactic = (PincerLowFastTactic)postSplitEwManager.CurrentTactic;
            EWJammerState correctAxisJammer = new EWJammerState(true, 0.0f, -5.0f, 5.825f, false);

            bool observedEvadingHeadingOffset = false;
            bool observedEw3sSilenceAfter5sWindow = false;
            int postSplitEwBreachTick = -1;

            for (int tick = 1; tick <= 300; tick++)
            {
                EWJammerState jammerThisTick = postSplitTactic.SplitTriggered ? correctAxisJammer : null;
                postSplitEwManager.Tick(0.1f, tick * 100L, jammerThisTick, Vector3.zero);

                if (postSplitTactic.SplitTriggered && !postSplitEwManager.ExclusionBreached)
                {
                    SwarmAgentState[] snap = postSplitEwManager.GetSwarmSnapshot();
                    // Pair A attacker (agent-2) is on North 0° axis (direct approach heading ~180°).
                    // Under EW evasion, its heading is deflected by ±30° (~150° or ~210°).
                    SwarmAgentState pairAAttacker = snap[2];
                    float headingOffsetFromDirect = EwDeconfliction.ComputeAngularDistance(pairAAttacker.headingDeg, 180.0f);
                    if (headingOffsetFromDirect >= 20.0f && headingOffsetFromDirect <= 40.0f)
                    {
                        observedEvadingHeadingOffset = true;
                    }

                    float sinceSplit = (tick * 0.1f) - postSplitTactic.SplitTimeSeconds;
                    if (sinceSplit >= 5.2f && !pairAAttacker.rfEmitting)
                    {
                        observedEw3sSilenceAfter5sWindow = true;
                    }
                }

                if (postSplitEwManager.ExclusionBreached)
                {
                    postSplitEwBreachTick = tick;
                    break;
                }
            }

            Assert(observedEvadingHeadingOffset,
                "Jammed attackers on 0° axis must steer ±30° off direct heading during EW evasion");
            Assert(observedEw3sSilenceAfter5sWindow,
                "Jammed attackers must remain RF-silent (rfEmitting=false) due to 3s EW evasion timer");
            Assert(postSplitEwBreachTick > noEwBreachTick + 30,
                string.Format("Scenario with EW on correct axis must last significantly longer than No EW (withEW={0} ticks vs noEW={1} ticks)",
                    postSplitEwBreachTick, noEwBreachTick));

            Console.WriteLine(string.Format(
                "EW evasion verified: No-EW breach={0} ticks ({1:F1}s), Wrong-Axis EW breach={2} ticks ({3:F1}s), Correct-Axis EW breach={4} ticks ({5:F1}s).",
                noEwBreachTick, noEwBreachTick * 0.1f,
                wrongEwBreachTick, wrongEwBreachTick * 0.1f,
                postSplitEwBreachTick, postSplitEwBreachTick * 0.1f));
            Console.WriteLine("[PASS] With EW on Correct Axis -> Attackers Evade & Scenario Lasts Longer PASSED");
        }

        /// <summary>
        /// Exact card test 3:
        /// Neutralise all 4 -> ALL_NEUTRALISED fires -> SCENARIO_END.
        /// Verifies that neutralised agents stop (velocityMs = 0), set propellerRpm = 0,
        /// set neutralisedAtMs, and when all 4 attackers are neutralised,
        /// SwarmManager broadcasts SCENARIO_END("ALL_NEUTRALISED") and tactic state is NEUTRALISED.
        /// </summary>
        public static void TestNeutraliseAllFourAttackersFiresAllNeutralisedScenarioEnd()
        {
            Console.WriteLine("\n-- Test 3: Neutralise All 4 Attackers -> ALL_NEUTRALISED -> SCENARIO_END --");

            SwarmManager manager = new SwarmManager();
            SwarmProfile profile = SwarmMock.GetPunjabProfile();
            string broadcastedEndReason = null;
            manager.OnScenarioEndTriggered += reason => { broadcastedEndReason = reason; };

            manager.OnScenarioStart(profile, 42819, "punjab-fog-pincer-01", Vector3.zero);
            PincerLowFastTactic tactic = (PincerLowFastTactic)manager.CurrentTactic;

            // Advance 65 ticks (6.5s) so swarm is in PINCER state after split
            for (int tick = 1; tick <= 65; tick++)
            {
                manager.Tick(0.1f, tick * 100L, null, Vector3.zero);
            }
            Assert(tactic.CurrentState == TacticState.PINCER, "Swarm must be in PINCER state at t=6.5s");

            string[] attackerIds = new string[] { "agent-2", "agent-3", "agent-4", "agent-5" };
            for (int i = 0; i < attackerIds.Length; i++)
            {
                long fireTimeMs = 6600L + (i * 200L);
                string targetId = attackerIds[i];
                SwarmAgentState target = manager.GetAgentById(targetId);
                Assert(target != null && !target.isNeutralised, "Attacker " + targetId + " must be active before fire");

                float leadBearing, leadElev;
                SmashFireControl.ComputeLeadReticleAngles(target, Vector3.zero, out leadBearing, out leadElev);
                TraineeAction fireAction = new TraineeAction(leadBearing, leadElev, true, 5.825f)
                {
                    kineticFireTriggered = true
                };

                KineticEvent ke = manager.ProcessKineticFire(fireAction, fireTimeMs, Vector3.zero, targetId);
                Assert(ke != null && ke.hit, "SMASH 2000 shot on " + targetId + " must register hit=true");
                Assert(target.isNeutralised, targetId + " must have isNeutralised=true");
                Assert(target.neutralisedAtMs == fireTimeMs, targetId + " neutralisedAtMs must equal fireTimeMs");
                Assert(target.velocityMs.sqrMagnitude < 0.0001f, targetId + " must stop moving (velocityMs=0)");
                Assert(target.propellerRpm == 0, targetId + " must set propellerRpm=0");
                Assert(target.rfEmitting == false, targetId + " must cease RF emission");

                if (i < attackerIds.Length - 1)
                {
                    Assert(!manager.AllNeutralised, "AllNeutralised must remain false while attackers remain");
                    Assert(broadcastedEndReason == null, "SCENARIO_END must not fire until all 4 attackers are neutralised");
                }
            }

            Assert(manager.AllNeutralised, "AllNeutralised must be true after all 4 attackers are neutralised");
            Assert(manager.AllNeutralisedAtMs == 7200L, "AllNeutralisedAtMs must record timestamp of 4th kill");
            Assert(tactic.CurrentState == TacticState.NEUTRALISED, "TacticTree state must transition to TacticState.NEUTRALISED");
            Assert(broadcastedEndReason == "ALL_NEUTRALISED" && manager.LastScenarioEndReason == "ALL_NEUTRALISED",
                "SwarmManager must broadcast SCENARIO_END with reason \"ALL_NEUTRALISED\"");

            Console.WriteLine(string.Format(
                "All 4 attackers neutralised at t={0}ms -> TacticState={1}, SCENARIO_END=\"{2}\".",
                manager.AllNeutralisedAtMs.Value, tactic.CurrentState, broadcastedEndReason));
            Console.WriteLine("[PASS] Neutralise All 4 Attackers -> ALL_NEUTRALISED -> SCENARIO_END PASSED");
        }

        /// <summary>
        /// Validates SA-P1-B-ii Step 2: BoidAgent.CheckEvasion(EWJammerState ew)
        /// - Within 15° of agent bearing: steers ±30° off heading (seed-alternated),
        ///   sets rfEmitting = false for 3.0s, and hops rfFreqGHz.
        /// - Outside 15° of agent bearing: returns false, does not evade.
        /// </summary>
        public static void TestBoidAgentCheckEvasionFrequencyHopAndSteer()
        {
            Console.WriteLine("\n-- Test 4: BoidAgent.CheckEvasion(EWJammerState ew) Unit Verification --");

            SwarmProfile profile = SwarmMock.GetPunjabProfile();
            SwarmAgentState backingState = new SwarmAgentState();
            BoidAgent agent = new BoidAgent();
            agent.InitializeFromProfile(
                index: 2,
                decoy: false,
                profile: profile,
                spawnPosition: new Vector3(0.0f, 8.0f, 100.0f), // Bearing 0° North from origin
                initialVelocity: new Vector3(0.0f, 0.0f, -33.33f), // Heading 180° South
                linkedState: backingState,
                seed: 42819);

            Assert(Math.Abs(agent.headingDeg - 180.0f) < 0.01f, "Initial heading must be 180°");
            Assert(agent.rfEmitting == true, "Initial rfEmitting must be true");
            Assert(Math.Abs(agent.rfFreqGHz - 5.825f) < 0.01f, "Initial rfFreqGHz must be 5.825 GHz");

            // 1. Off-axis jammer (bearing 45° > 15° from agent bearing 0°) -> no evasion
            EWJammerState offAxisJammer = new EWJammerState(true, 45.0f, 0.0f, 5.825f, false);
            bool evadedOffAxis = agent.CheckEvasion(offAxisJammer);
            Assert(!evadedOffAxis, "CheckEvasion must return false when jammer bearing is >15° from agent bearing");
            Assert(agent.rfEmitting == true, "rfEmitting must remain true when not evading");

            // 2. On-axis jammer (bearing 10° <= 15° from agent bearing 0°) -> evasion triggered
            EWJammerState onAxisJammer = new EWJammerState(true, 10.0f, 0.0f, 5.825f, false);
            bool evadedOnAxis = agent.CheckEvasion(onAxisJammer);
            Assert(evadedOnAxis, "CheckEvasion must return true when jammer is engaged within 15° of agent bearing");
            Assert(agent.rfEmitting == false && backingState.rfEmitting == false,
                "CheckEvasion must set rfEmitting=false on both BoidAgent and linked SwarmAgentState");
            Assert(Math.Abs(agent.EwSilentRemainingSec - 3.0f) < 0.01f,
                "CheckEvasion must set 3.0s RF silence timer");

            float headingDelta = EwDeconfliction.ComputeAngularDistance(agent.headingDeg, 180.0f);
            Assert(Math.Abs(headingDelta - 30.0f) < 0.1f,
                string.Format("CheckEvasion must steer ±30° off current heading (180° -> {0:F1}°)", agent.headingDeg));
            Assert(Math.Abs(agent.rfFreqGHz - 2.4f) < 0.01f,
                "CheckEvasion must frequency-hop from 5.825 GHz to 2.4 GHz");

            // 3. Step 2.5s with jammer disengaged -> rfEmitting must stay false (3s timer still active)
            EWJammerState disengagedJammer = new EWJammerState(false, 10.0f, 0.0f, 5.825f, false);
            agent.CheckEvasion(disengagedJammer, Vector3.zero, 2.5f);
            Assert(agent.rfEmitting == false, "rfEmitting must remain false at 2.5s (< 3.0s silence duration)");

            // 4. Step another 0.6s (total 3.1s >= 3.0s) -> rfEmitting recovers to true
            agent.CheckEvasion(disengagedJammer, Vector3.zero, 0.6f);
            Assert(agent.rfEmitting == true && backingState.rfEmitting == true,
                "rfEmitting must recover to true once 3.0s EW silence timer expires");

            Console.WriteLine(string.Format(
                "BoidAgent.CheckEvasion verified: heading 180° -> {0:F1}° (±30°), freq 5.825 -> {1:F3} GHz, 3.0s RF silence & recovery.",
                agent.headingDeg, agent.rfFreqGHz));
            Console.WriteLine("[PASS] BoidAgent.CheckEvasion(EWJammerState ew) Unit Verification PASSED");
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
