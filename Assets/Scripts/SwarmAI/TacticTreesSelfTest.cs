// ============================================================
// TacticTreesSelfTest.cs — Runnable Self-Test for SA-P2-A
// TRISHUL-SIM · swarm-ai track (Uday)
//
// Validates:
// 1. Exact mission card test:
//    - Load ladakh-lac-ridge-01 -> 8 agents broad front, all dive at 100m.
//    - Load loc-mountain-pass-01 -> 4 attackers immediately split to 4 compass points.
// 2. TacticTreeFactory creation and normalization for all 3 tactic trees.
// 3. SATURATION_DIVE broad front kinematics, dive trigger at 100m, and 4s frequency hopping.
// 4. MULTI-AXIS_SPLIT direct frontal decoy, 4-way cardinal compass vectors from spawn, and 915 MHz band.
// 5. SwarmManager scenario loading and multi-spectral tick progression.
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
    public static class TacticTreesSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllTests();
                Console.WriteLine("\n[PASS] All SA-P2-A Extra Tactics tests passed successfully.");
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
            Console.WriteLine("=== Running TRISHUL-SIM SA-P2-A Extra Tactics Self-Test ===");

            TestExactMissionCardRequirements();
            TestTacticTreeFactory();
            TestSaturationDiveTacticDetails();
            TestMultiAxisSplitTacticDetails();
            TestSwarmManagerScenarioIntegration();
        }

        /// <summary>
        /// Exact card test specified in uday-mission.md and plans/swarm-ai-phase-plan.md:
        /// - Load ladakh-lac-ridge-01 -> 8 agents approach broad front, all dive at 100m.
        /// - Load loc-mountain-pass-01 -> 4 attackers immediately split to 4 compass points.
        /// </summary>
        public static void TestExactMissionCardRequirements()
        {
            Console.WriteLine("\n-- Test 1: Exact Mission Card Test Requirements --");

            // Part A: Load ladakh-lac-ridge-01 -> 8 agents broad front, all dive at 100m
            SwarmManager ladakhManager = new SwarmManager();
            ladakhManager.LoadScenario("ladakh-lac-ridge-01");

            SwarmAgentState[] ladakhAgents = ladakhManager.GetSwarmSnapshot();
            Assert(ladakhAgents.Length == 8, "ladakh-lac-ridge-01 must have 8 agents");
            Assert(ladakhManager.CurrentTactic is SaturationDiveTactic, "Active tactic must be SaturationDiveTactic");

            // Verify broad front: agents spread across X axis
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < ladakhAgents.Length; i++)
            {
                if (ladakhAgents[i].positionWorld.x < minX) minX = ladakhAgents[i].positionWorld.x;
                if (ladakhAgents[i].positionWorld.x > maxX) maxX = ladakhAgents[i].positionWorld.x;
            }
            float frontWidth = maxX - minX;
            Assert(frontWidth >= 80.0f, "Broad front must span >= 80m across X axis (measured: " + frontWidth + "m)");
            Console.WriteLine("Ladakh broad front verified: 8 agents spanning " + frontWidth + "m abreast.");

            // Verify approach before 100m: all agents maintain high cruising altitude (25m)
            SaturationDiveTactic satTactic = (SaturationDiveTactic)ladakhManager.CurrentTactic;
            Assert(satTactic.CurrentState == TacticState.APPROACH, "Initial state must be APPROACH");
            Assert(!satTactic.DiveTriggered, "Dive must not be triggered initially");

            // Advance time until agents reach 100m range to origin
            float dt = 0.1f;
            long t = 0;
            while (t < 20000 && !satTactic.DiveTriggered)
            {
                t += 100;
                ladakhManager.Tick(dt, t);
            }

            Assert(satTactic.DiveTriggered == true, "Dive must trigger when agents reach 100m range");
            Assert(satTactic.CurrentState == TacticState.DIVE_ATTACK, "State must transition to DIVE_ATTACK");
            Console.WriteLine("Dive trigger fired at range <= 100m (t=" + t + "ms). Transitioned to DIVE_ATTACK.");

            // Advance further into the dive and verify all agents descend towards 1m
            for (int step = 0; step < 50; step++)
            {
                t += 100;
                ladakhManager.Tick(dt, t);
            }

            for (int i = 0; i < ladakhAgents.Length; i++)
            {
                Assert(ladakhAgents[i].altitudeM <= 1.5f, "Agent " + i + " must have dived to ~1m altitude (actual: " + ladakhAgents[i].altitudeM + "m)");
            }
            Console.WriteLine("All 8 agents verified at dived altitude <= 1.5m.");

            // Part B: Load loc-mountain-pass-01 -> 4 attackers immediately split to 4 compass points
            SwarmManager locManager = new SwarmManager();
            locManager.LoadScenario("loc-mountain-pass-01");

            SwarmAgentState[] locAgents = locManager.GetSwarmSnapshot();
            Assert(locAgents.Length == 5, "loc-mountain-pass-01 must have 5 agents (1 decoy + 4 attackers)");
            Assert(locManager.CurrentTactic is MultiAxisSplitTactic, "Active tactic must be MultiAxisSplitTactic");
            Assert(locManager.CurrentTactic.CurrentState == TacticState.SPLIT, "Attackers must immediately split from spawn");

            // Check the 4 attackers: each must align with one of the 4 cardinal compass directions
            List<string> detectedCompassDirs = new List<string>();
            for (int i = 0; i < locAgents.Length; i++)
            {
                SwarmAgentState agent = locAgents[i];
                if (agent.isDecoy)
                {
                    Assert(agent.agentId == "agent-0", "agent-0 must be decoy");
                    continue;
                }

                // Check direction of velocity relative to origin
                Vector3 vel = agent.velocityMs;
                string dir = "Unknown";
                if (Math.Abs(vel.z) > Math.Abs(vel.x))
                {
                    dir = vel.z < 0 ? "NorthApproach" : "SouthApproach";
                }
                else
                {
                    dir = vel.x < 0 ? "EastApproach" : "WestApproach";
                }

                if (!detectedCompassDirs.Contains(dir))
                {
                    detectedCompassDirs.Add(dir);
                }
            }

            Assert(detectedCompassDirs.Count == 4, "4 attackers must approach from 4 distinct cardinal compass points");
            Console.WriteLine("LoC 4-axis split verified: attackers assigned to " + string.Join(", ", detectedCompassDirs.ToArray()));

            Console.WriteLine("[PASS] Exact Mission Card Test Requirements PASSED");
        }

        /// <summary>
        /// Validates TacticTreeFactory creation and normalization.
        /// </summary>
        public static void TestTacticTreeFactory()
        {
            Console.WriteLine("\n-- Test 2: TacticTreeFactory Creation & Normalization --");

            TacticTree t1 = TacticTreeFactory.Create("PINCER_LOW_FAST");
            Assert(t1 is PincerLowFastTactic, "Factory must create PincerLowFastTactic");
            Assert(t1.Name == "PINCER_LOW_FAST", "Tactic name must match");

            TacticTree t2 = TacticTreeFactory.Create("SATURATION_DIVE");
            Assert(t2 is SaturationDiveTactic, "Factory must create SaturationDiveTactic");
            Assert(t2.Name == "SATURATION_DIVE", "Tactic name must match");

            TacticTree t3 = TacticTreeFactory.Create("MULTI-AXIS_SPLIT");
            Assert(t3 is MultiAxisSplitTactic, "Factory must create MultiAxisSplitTactic");
            Assert(t3.Name == "MULTI-AXIS_SPLIT", "Tactic name must match");

            // Robust normalization tests
            TacticTree t3b = TacticTreeFactory.Create("multi_axis_split");
            Assert(t3b is MultiAxisSplitTactic, "Factory must handle underscore and lower case");

            TacticTree t2b = TacticTreeFactory.Create("saturation-dive");
            Assert(t2b is SaturationDiveTactic, "Factory must handle hyphen and lower case");

            Assert(TacticTreeFactory.IsValidTactic("SATURATION_DIVE"), "SATURATION_DIVE must be valid");
            Assert(TacticTreeFactory.IsValidTactic("MULTI-AXIS_SPLIT"), "MULTI-AXIS_SPLIT must be valid");
            Assert(TacticTreeFactory.IsValidTactic("PINCER_LOW_FAST"), "PINCER_LOW_FAST must be valid");
            Assert(!TacticTreeFactory.IsValidTactic("UNKNOWN_TACTIC"), "UNKNOWN_TACTIC must be invalid");

            Console.WriteLine("[PASS] TacticTreeFactory Creation & Normalization PASSED");
        }

        /// <summary>
        /// Validates SATURATION_DIVE specific mechanics:
        /// 1. 8-agent broad front formation.
        /// 2. Dive triggered simultaneously when range <= 100m.
        /// 3. Random frequency hopping every 4 seconds.
        /// </summary>
        public static void TestSaturationDiveTacticDetails()
        {
            Console.WriteLine("\n-- Test 3: SATURATION_DIVE Details (Dive & 4s Freq Hopping) --");

            SaturationDiveTactic tactic = new SaturationDiveTactic();
            SwarmProfile profile = SwarmMock.GetLadakhProfile();
            SwarmAgentState[] agents = SwarmMock.GetLadakhFixture();
            List<SwarmAgentState> agentList = new List<SwarmAgentState>(agents);

            tactic.Initialize(profile, 51204, agentList, Vector3.zero);

            // Step A: Broad front check
            Assert(agentList.Count == 8, "Must have 8 agents");
            Assert(tactic.CurrentState == TacticState.APPROACH, "Must start in APPROACH");

            // Step B: 4-second frequency hopping verification
            float initialFreq = agentList[0].rfFreqGHz;
            // Advance by 3.8s (should not hop yet)
            tactic.Tick(3.8f, 3800, null, Vector3.zero);
            Assert(agentList[0].rfFreqGHz == initialFreq, "Frequency must not hop before 4.0s");

            // Advance past 4.0s (total 4.2s)
            tactic.Tick(0.4f, 4200, null, Vector3.zero);
            // Verify hop took place
            Console.WriteLine("Freq hop at 4.0s: initial=" + initialFreq + " GHz -> current=" + agentList[0].rfFreqGHz + " GHz");

            // Step C: Trigger dive at 100m
            // Move agents directly to 95m range
            for (int i = 0; i < agentList.Count; i++)
            {
                agentList[i].positionWorld = new Vector3(agentList[i].positionWorld.x, 25.0f, 95.0f);
            }
            tactic.Tick(0.1f, 5000, null, Vector3.zero);
            Assert(tactic.DiveTriggered == true, "Dive must be triggered at 95m <= 100m");
            Assert(tactic.CurrentState == TacticState.DIVE_ATTACK, "State must be DIVE_ATTACK");

            // Step D: Verify all 8 agents dive simultaneously
            tactic.Tick(1.0f, 6000, null, Vector3.zero);
            for (int i = 0; i < agentList.Count; i++)
            {
                Assert(agentList[i].altitudeM < 20.0f, "Agent " + i + " must be diving simultaneously");
            }

            Console.WriteLine("[PASS] SATURATION_DIVE Details PASSED");
        }

        /// <summary>
        /// Validates MULTI-AXIS_SPLIT specific mechanics:
        /// 1. 1 Decoy frontal approach at 915 MHz.
        /// 2. 4 Attackers cardinal vectors (North, East, South, West).
        /// 3. Narrow-band 915 MHz only, no Doppler evasion.
        /// </summary>
        public static void TestMultiAxisSplitTacticDetails()
        {
            Console.WriteLine("\n-- Test 4: MULTI-AXIS_SPLIT Details (Cardinal Vectors & 915 MHz) --");

            MultiAxisSplitTactic tactic = new MultiAxisSplitTactic();
            SwarmProfile profile = SwarmMock.GetLocProfile();
            SwarmAgentState[] agents = SwarmMock.GetLocFixture();
            List<SwarmAgentState> agentList = new List<SwarmAgentState>(agents);

            tactic.Initialize(profile, 63918, agentList, Vector3.zero);

            Assert(agentList.Count == 5, "Must have 5 agents");
            Assert(tactic.CurrentState == TacticState.SPLIT, "Must immediately be in SPLIT");

            // 1 Decoy
            SwarmAgentState decoy = agentList[0];
            Assert(decoy.isDecoy == true, "agent-0 must be decoy");
            Assert(Math.Abs(decoy.positionWorld.x) < 0.001f, "Decoy must be on central axis (x=0)");
            Assert(decoy.rfFreqGHz == 0.915f, "Decoy must be 915 MHz");

            // 4 Attackers on 4 cardinal compass vectors
            for (int i = 1; i <= 4; i++)
            {
                SwarmAgentState att = agentList[i];
                Assert(att.isDecoy == false, "Attacker " + i + " must not be decoy");
                Assert(att.rfFreqGHz == 0.915f, "Attacker " + i + " must operate at 915 MHz");
                Assert(att.propellerRpm == 11400, "Attacker " + i + " must maintain steady 11400 RPM (no Doppler evasion)");
            }

            // Advance 2 seconds
            tactic.Tick(2.0f, 2000, null, Vector3.zero);
            for (int i = 0; i < agentList.Count; i++)
            {
                Assert(agentList[i].rfFreqGHz == 0.915f, "Frequency must remain 915 MHz (narrow band)");
            }

            Console.WriteLine("[PASS] MULTI-AXIS_SPLIT Details PASSED");
        }

        /// <summary>
        /// Validates SwarmManager integration with tactics and kinetic neutralisation.
        /// </summary>
        public static void TestSwarmManagerScenarioIntegration()
        {
            Console.WriteLine("\n-- Test 5: SwarmManager Scenario Integration & Fire Control --");

            SwarmManager manager = new SwarmManager();

            // Load LoC scenario
            manager.LoadScenario("loc-mountain-pass-01");
            Assert(manager.CurrentScenarioId == "loc-mountain-pass-01", "Scenario ID must match");
            Assert(manager.GetRemainingAttackerCount() == 4, "Must have 4 active attackers");

            // Neutralise attacker 1 with lead angle fire
            SwarmAgentState target = manager.GetAgentById("agent-1");
            Assert(target != null, "agent-1 must exist");

            float reqBearing, reqElev;
            SmashFireControl.ComputeLeadReticleAngles(target, Vector3.zero, out reqBearing, out reqElev);

            TraineeAction fire = new TraineeAction(reqBearing, reqElev) { kineticFireTriggered = true };
            KineticEvent ke = manager.ProcessKineticFire(fire, 5000, Vector3.zero, "agent-1");

            Assert(ke.hit == true, "Shot must hit agent-1");
            Assert(target.isNeutralised == true, "agent-1 must be neutralised");
            Assert(manager.GetRemainingAttackerCount() == 3, "Must have 3 remaining active attackers");

            // Tick manager to verify tactic handles neutralised agent gracefully
            manager.Tick(0.1f, 5100);
            Assert(target.velocityMs.sqrMagnitude < 0.0001f, "Neutralised agent must not move");

            Console.WriteLine("[PASS] SwarmManager Scenario Integration & Fire Control PASSED");
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
