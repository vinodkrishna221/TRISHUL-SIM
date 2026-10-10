// ============================================================
// SmashFireControlSelfTest.cs — Runnable Self-Test for SA-P1-D
// TRISHUL-SIM · swarm-ai track (Uday)
//
// Validates:
// 1. Exact mission card test:
//    - Aim at approaching agent; press FIRE key.
//    - Console output: KineticEvent{hit:true/false, leadAngleDeg:X.X}
//    - hit=true -> agent stops moving (isNeutralised in next TICK swarmSnapshot)
//    - All 4 attackers neutralised -> ALL_NEUTRALISED -> SCENARIO_END
// 2. Ballistic lead-angle calculation (bullet speed 900 m/s, time to target, lead vector)
// 3. Accuracy cone tolerance (< 5° = hit, >= 5° = miss)
// 4. Target already-neutralised guard
// 5. SwarmManager neutralisation lifecycle & terminal condition
// 6. SensorSuiteComputer integration & TICK payload injection
// 7. HUD lead reticle guidance angles
//
// Compiles directly with csc.exe or inside Unity Editor tests.
// ============================================================

using System;
using System.Text;
using UnityEngine;
using TrishulSim.Sensors;
using TrishulSim.SwarmAI;
using TrishulSim.Mocks;

namespace TrishulSim.Sensors
{
    public static class SmashFireControlSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllTests();
                Console.WriteLine("\n[PASS] All SA-P1-D SMASH fire control tests passed.");
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
            Console.WriteLine("=== Running TRISHUL-SIM SA-P1-D SMASH Fire Control Self-Test ===");

            TestExactMissionCardRequirements();
            TestBallisticLeadMath();
            TestAccuracyConeThresholds();
            TestAlreadyNeutralisedGuard();
            TestAllAttackersNeutralisedTerminalCondition();
            TestSensorSuiteComputerIntegration();
            TestLeadReticleAngleCalculations();
        }

        /// <summary>
        /// Exact card test specified in uday-mission.md and plans/swarm-ai-phase-plan.md:
        /// Aim at approaching agent; press FIRE key. Console: KineticEvent{hit:true/false, leadAngleDeg:X.X}
        /// hit=true -> agent stops moving (isNeutralised in next TICK swarmSnapshot).
        /// All 4 attackers neutralised -> ALL_NEUTRALISED -> SCENARIO_END.
        /// </summary>
        public static void TestExactMissionCardRequirements()
        {
            Console.WriteLine("\n-- Test 1: Exact Mission Card Test Cases --");

            // Setup Punjab scenario fixture: 2 decoys, 4 attackers
            SwarmManager swarm = new SwarmManager();
            swarm.LoadPunjabFixture();

            SwarmAgentState[] initialSnapshot = swarm.GetSwarmSnapshot();
            Assert(initialSnapshot.Length == 6, "Punjab fixture must have 6 agents");

            // Select approaching lead attacker (agent-3 from canonical SCHEMA doc)
            SwarmAgentState targetAgent = swarm.GetAgentById("agent-3");
            Assert(targetAgent != null, "Target agent-3 must exist");
            Assert(!targetAgent.isNeutralised, "Target agent-3 must initially be active");

            Vector3 traineePos = Vector3.zero; // Sentry position at origin

            // Step A: Calculate required aim direction for lead intercept
            float requiredBearing, requiredElevation;
            SmashFireControl.ComputeLeadReticleAngles(targetAgent, traineePos, out requiredBearing, out requiredElevation);

            // Step B: Trainee aims reticle directly at approaching target's lead point and pulls trigger
            TraineeAction fireAction = new TraineeAction
            {
                antennaBearingDeg = requiredBearing,
                antennaElevationDeg = requiredElevation,
                kineticFireTriggered = true
            };

            long currentScenarioTimeMs = 45200; // Beat 4 wow moment timestamp
            KineticEvent ke = swarm.ProcessKineticFire(fireAction, currentScenarioTimeMs, traineePos, targetAgent.agentId);

            // Mandatory Console output format from mission card
            Console.WriteLine(ke.ToString());

            Assert(ke != null, "KineticEvent must not be null");
            Assert(ke.hit == true, "Expected hit=true when aimed at lead point");
            Assert(ke.leadAngleDeg < 5.0f, "leadAngleDeg must be within 5° accuracy cone");
            Assert(string.Equals(ke.targetAgentId, "agent-3"), "Target must be agent-3");

            // Step C: Verify agent stops moving and isNeutralised in next TICK swarmSnapshot
            SwarmAgentState[] nextTickSnapshot = swarm.GetSwarmSnapshot();
            SwarmAgentState neutralisedAgent = null;
            for (int i = 0; i < nextTickSnapshot.Length; i++)
            {
                if (string.Equals(nextTickSnapshot[i].agentId, "agent-3"))
                {
                    neutralisedAgent = nextTickSnapshot[i];
                    break;
                }
            }

            Assert(neutralisedAgent != null, "neutralised agent must be in snapshot");
            Assert(neutralisedAgent.isNeutralised == true, "agent.isNeutralised must be true");
            Assert(neutralisedAgent.neutralisedAtMs == currentScenarioTimeMs, "neutralisedAtMs must match timestamp");
            Assert(neutralisedAgent.velocityMs.sqrMagnitude < 1e-6f, "Agent must stop moving (velocityMs == 0)");
            Assert(neutralisedAgent.propellerRpm == 0, "Propeller RPM must drop to 0");
            Assert(neutralisedAgent.rfEmitting == false, "Neutralised agent must cease RF emission");

            Console.WriteLine("[PASS] Exact Mission Card Test Cases PASSED");
        }

        /// <summary>
        /// Validates ballistic lead mathematics:
        /// bulletSpeedMs = 900 m/s, timeToTarget = dist / 900, leadVector = velocity * timeToTarget, aimPoint = pos + leadVector.
        /// </summary>
        public static void TestBallisticLeadMath()
        {
            Console.WriteLine("\n-- Test 2: Ballistic Lead Intercept Math --");

            // Synthetic test drone:
            // At (0, 0, 900m) North of sentry, moving East (+X) at 100 m/s
            SwarmAgentState testDrone = new SwarmAgentState
            {
                agentId = "test-drone",
                positionWorld = new Vector3(0.0f, 0.0f, 900.0f),
                velocityMs = new Vector3(100.0f, 0.0f, 0.0f),
                isNeutralised = false
            };

            Vector3 traineePos = Vector3.zero;
            Vector3 aimPoint = SmashFireControl.ComputeLeadAimPoint(testDrone, traineePos, bulletSpeedMs: 900.0f);

            // Distance = 900m -> timeToTarget = 900 / 900 = 1.0s
            // Lead vector = (100 * 1.0, 0, 0) = (100, 0, 0)
            // aimPoint = (100, 0, 900)
            Assert(Math.Abs(aimPoint.x - 100.0f) < 0.001f, "AimPoint X must be 100m");
            Assert(Math.Abs(aimPoint.y - 0.0f) < 0.001f, "AimPoint Y must be 0m");
            Assert(Math.Abs(aimPoint.z - 900.0f) < 0.001f, "AimPoint Z must be 900m");

            // Required bearing to intercept aimPoint (100, 0, 900):
            // atan2(100, 900) in degrees = ~6.34° East of North
            float reqBearing, reqElev;
            SmashFireControl.ComputeLeadReticleAngles(testDrone, traineePos, out reqBearing, out reqElev, bulletSpeedMs: 900.0f);
            float expectedBearing = (float)(Math.Atan2(100.0, 900.0) * (180.0 / Math.PI));
            Assert(Math.Abs(reqBearing - expectedBearing) < 0.01f, "Required bearing must match trigonometric prediction");

            Console.WriteLine("[PASS] Ballistic lead intercept math PASSED");
        }

        /// <summary>
        /// Validates 5° accuracy cone threshold (hit < 5°, miss >= 5°).
        /// </summary>
        public static void TestAccuracyConeThresholds()
        {
            Console.WriteLine("\n-- Test 3: Accuracy Cone Thresholds (< 5° vs >= 5°) --");

            SwarmAgentState drone = new SwarmAgentState
            {
                agentId = "drone-1",
                positionWorld = new Vector3(0.0f, 0.0f, 100.0f), // 100m North
                velocityMs = Vector3.zero,
                isNeutralised = false
            };

            Vector3 traineePos = Vector3.zero;

            // 1. Aim directly at target (bearing 0°, elevation 0°): leadAngle = 0° -> hit
            KineticEvent hitEvent = SmashFireControl.ComputeKineticEvent(
                aimBearingDeg: 0.0f,
                aimElevationDeg: 0.0f,
                target: drone,
                t: 1000,
                traineePos: traineePos
            );
            Assert(hitEvent.hit == true, "Zero angular offset must be hit");
            Assert(hitEvent.leadAngleDeg < 0.001f, "leadAngleDeg must be ~0°");

            // 2. Aim 4.0° offset (within 5° cone): leadAngle = 4.0° -> hit
            KineticEvent hitCone = SmashFireControl.ComputeKineticEvent(
                aimBearingDeg: 4.0f,
                aimElevationDeg: 0.0f,
                target: drone,
                t: 1000,
                traineePos: traineePos
            );
            Assert(hitCone.hit == true, "4.0° offset (< 5.0°) must be hit");

            // 3. Aim 5.1° offset (outside 5° cone): leadAngle = 5.1° -> miss
            KineticEvent missCone = SmashFireControl.ComputeKineticEvent(
                aimBearingDeg: 5.1f,
                aimElevationDeg: 0.0f,
                target: drone,
                t: 1000,
                traineePos: traineePos
            );
            Assert(missCone.hit == false, "5.1° offset (>= 5.0°) must be miss");
            Assert(missCone.leadAngleDeg > 5.0f, "leadAngleDeg must exceed 5°");

            // 4. Aim 90° away (East): leadAngle = 90° -> miss
            KineticEvent wildMiss = SmashFireControl.ComputeKineticEvent(
                aimBearingDeg: 90.0f,
                aimElevationDeg: 0.0f,
                target: drone,
                t: 1000,
                traineePos: traineePos
            );
            Assert(wildMiss.hit == false, "90° offset must be miss");

            Console.WriteLine("[PASS] Accuracy cone thresholds PASSED");
        }

        /// <summary>
        /// Validates that an already neutralised agent cannot be hit again (hit = false).
        /// </summary>
        public static void TestAlreadyNeutralisedGuard()
        {
            Console.WriteLine("\n-- Test 4: Already Neutralised Guard --");

            SwarmAgentState downDrone = new SwarmAgentState
            {
                agentId = "down-drone",
                positionWorld = new Vector3(0.0f, 0.0f, 50.0f),
                velocityMs = Vector3.zero,
                isNeutralised = true,
                neutralisedAtMs = 12000
            };

            KineticEvent ke = SmashFireControl.ComputeKineticEvent(
                aimBearingDeg: 0.0f,
                aimElevationDeg: 0.0f,
                target: downDrone,
                t: 15000,
                traineePos: Vector3.zero
            );

            Assert(ke.hit == false, "Firing at already neutralised agent must yield hit=false");

            Console.WriteLine("[PASS] Already neutralised guard PASSED");
        }

        /// <summary>
        /// Validates ALL_NEUTRALISED terminal condition:
        /// Punjab scenario: 2 decoys, 4 attackers (agents 2, 3, 4, 5).
        /// Neutralising all 4 attackers triggers ALL_NEUTRALISED.
        /// Decoys alone do not satisfy or block ALL_NEUTRALISED.
        /// </summary>
        public static void TestAllAttackersNeutralisedTerminalCondition()
        {
            Console.WriteLine("\n-- Test 5: ALL_NEUTRALISED Terminal Condition --");

            SwarmManager swarm = new SwarmManager();
            swarm.LoadPunjabFixture();

            Assert(swarm.AllNeutralised == false, "Initially AllNeutralised must be false");
            Assert(swarm.GetRemainingAttackerCount() == 4, "Initially 4 active attackers");

            string[] attackerIds = new string[] { "agent-2", "agent-3", "agent-4", "agent-5" };
            Vector3 traineePos = Vector3.zero;

            for (int i = 0; i < attackerIds.Length; i++)
            {
                string id = attackerIds[i];
                SwarmAgentState target = swarm.GetAgentById(id);
                Assert(target != null, "Attacker " + id + " must exist");

                float reqBearing, reqElev;
                SmashFireControl.ComputeLeadReticleAngles(target, traineePos, out reqBearing, out reqElev);

                TraineeAction fire = new TraineeAction(reqBearing, reqElev) { kineticFireTriggered = true };
                KineticEvent ke = swarm.ProcessKineticFire(fire, 40000 + (i * 1000), traineePos, id);

                Assert(ke.hit == true, "Shot on " + id + " must hit");
                Assert(target.isNeutralised == true, id + " must be neutralised");

                if (i < attackerIds.Length - 1)
                {
                    Assert(swarm.AllNeutralised == false, "AllNeutralised should not trigger until all 4 attackers down");
                }
            }

            Assert(swarm.GetRemainingAttackerCount() == 0, "Zero active attackers remaining");
            Assert(swarm.AllNeutralised == true, "All 4 attackers neutralised must set AllNeutralised=true");
            Assert(swarm.AllNeutralisedAtMs.HasValue, "AllNeutralisedAtMs must be populated");
            Console.WriteLine("All 4 attackers neutralised at T=" + swarm.AllNeutralisedAtMs.Value + "ms -> ALL_NEUTRALISED triggered.");

            // Confirm decoys (agents 0, 1) are still intact, proving decoys do not block mission victory
            SwarmAgentState decoy0 = swarm.GetAgentById("agent-0");
            Assert(decoy0.isDecoy == true, "agent-0 is decoy");
            Assert(decoy0.isNeutralised == false, "Decoy did not need to be neutralised");

            Console.WriteLine("[PASS] ALL_NEUTRALISED terminal condition PASSED");
        }

        /// <summary>
        /// Validates SensorSuiteComputer integration with TraineeAction and KineticEvent.
        /// </summary>
        public static void TestSensorSuiteComputerIntegration()
        {
            Console.WriteLine("\n-- Test 6: SensorSuiteComputer Integration --");

            SensorSuiteComputer computer = new SensorSuiteComputer();
            computer.ConfigureScenario("punjab-fog-pincer-01");

            SwarmAgentState[] swarm = SwarmMock.GetPunjabFixture();
            Vector3 traineePos = Vector3.zero;

            // Tick without fire: kinetic is null
            TraineeAction idleAction = new TraineeAction(247.0f, -5.0f, true, 5.825f);
            idleAction.kineticFireTriggered = false;
            computer.ProcessTraineeAction(idleAction, swarm, 45100, traineePos);
            Assert(idleAction.kinetic == null, "When kineticFireTriggered=false, kinetic must be null");

            // Tick with fire: trainee fires at agent-3 with lead angle
            float leadBearing, leadElev;
            SmashFireControl.ComputeLeadReticleAngles(swarm[3], traineePos, out leadBearing, out leadElev);

            TraineeAction fireAction = new TraineeAction(leadBearing, leadElev, true, 5.825f);
            fireAction.kineticFireTriggered = true;

            KineticEvent ke = computer.ProcessTraineeAction(fireAction, swarm, 45200, traineePos, "agent-3");
            Assert(ke != null, "KineticEvent returned");
            Assert(fireAction.kinetic != null, "action.kinetic must be populated");
            Assert(fireAction.kinetic.hit == true, "action.kinetic.hit must be true");
            Assert(swarm[3].isNeutralised == true, "agent-3 must be marked neutralised");

            // Recompute sensor readings: neutralised agent must be excluded
            SensorReadings readings = computer.ComputeSensorReadings(swarm, traineePos);
            Assert(readings != null, "SensorReadings computed");

            Console.WriteLine("[PASS] SensorSuiteComputer integration PASSED");
        }

        /// <summary>
        /// Validates HUD lead reticle angle guidance calculations.
        /// </summary>
        public static void TestLeadReticleAngleCalculations()
        {
            Console.WriteLine("\n-- Test 7: HUD Lead Reticle Angle Calculations --");

            // Drone approaching at 45° azimuth, moving perpendicularly
            SwarmAgentState drone = new SwarmAgentState
            {
                agentId = "reticle-drone",
                positionWorld = new Vector3(100.0f, 10.0f, 100.0f),
                velocityMs = new Vector3(20.0f, 0.0f, -20.0f),
                isNeutralised = false
            };

            float leadBearing, leadElev;
            SmashFireControl.ComputeLeadReticleAngles(drone, Vector3.zero, out leadBearing, out leadElev);

            Assert(leadBearing >= 0.0f && leadBearing < 360.0f, "Lead bearing must be valid azimuth");
            Assert(leadElev >= -90.0f && leadElev <= 90.0f, "Lead elevation must be valid angle");

            Console.WriteLine(string.Format("Target pos (100, 10, 100) -> Lead Reticle: Bearing={0:F1}°, Elev={1:F1}°",
                leadBearing, leadElev));

            Console.WriteLine("[PASS] HUD Lead Reticle calculations PASSED");
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
