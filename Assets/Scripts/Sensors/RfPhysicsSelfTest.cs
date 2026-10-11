// ============================================================
// RfPhysicsSelfTest.cs — Runnable Self-Test for SA-P0-B
// TRISHUL-SIM · swarm-ai track (Venu)
//
// Validates:
// 1. Exact mission card test (SA-P0-B):
//    - Play Punjab scenario; SCENARIO_TICK Console log shows sensorReadings.rfWaterfallPeakDbm
//      is a negative float that gets less negative (stronger) as agents approach.
//    - At 50m range: verifies exact Friis formula 23 - (20*log10(0.05) + 20*log10(5.825) + 92.45).
//    - radarDetectedCount increases as agents approach (from 2 decoys at 260m spawn to all 6 agents inside 230m/50m).
//    - dopplerPeakHz = 0.0f and acousticSplDb = 0.0f (P1 placeholders).
// 2. RfPhysics.ComputeFriisDbm mathematical accuracy across 915 MHz, 2.4 GHz, and 5.825 GHz.
// 3. RfPhysics.ComputeRadarReturnDbm two-way monostatic return & -90 dBm detection threshold.
// 4. SensorSuiteComputer (MonoBehaviour) filtering of non-emitting and neutralised agents.
// 5. SwarmManager registration of SensorSuiteComputer and SCENARIO_TICK injection.
// ============================================================

using System;
using System.Text;
using UnityEngine;
using TrishulSim.Sensors;
using TrishulSim.SwarmAI;
using TrishulSim.Mocks;

namespace TrishulSim.Sensors
{
    public static class RfPhysicsSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllTests();
                Console.WriteLine("\n[PASS] All SA-P0-B Friis RF propagation & SensorReadings tests passed.");
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
            Console.WriteLine("=== Running TRISHUL-SIM SA-P0-B Friis RF Model Self-Test ===");

            TestExactMissionCardPunjabApproach();
            TestFriisFormulaMathematics();
            TestMonostaticRadarReturnMathematics();
            TestSensorSuiteComputerFilteringAndPlaceholders();
            TestSwarmManagerRegistrationAndTickInjection();
        }

        /// <summary>
        /// Exact test from missions/venu-mission.md & plans/swarm-ai-phase-plan.md:
        /// Play Punjab scenario. SCENARIO_TICK Console: sensorReadings.rfWaterfallPeakDbm is a negative
        /// float that gets less negative as agents approach. At 50m: verify Friis value.
        /// radarDetectedCount increases as agents approach.
        /// </summary>
        public static void TestExactMissionCardPunjabApproach()
        {
            Console.WriteLine("\n-- Test 1: Exact Mission Card Punjab Scenario Approach (10 Hz TICK) --");

            SwarmManager manager = new SwarmManager();
            SensorSuiteComputer computer = new SensorSuiteComputer();
            manager.RegisterSensorSuiteComputer(computer);
            manager.AntennaPosition = Vector3.zero;

            SwarmProfile punjabProfile = SwarmMock.GetPunjabProfile();
            manager.OnScenarioStart(punjabProfile, 42819, "punjab-fog-pincer-01", Vector3.zero);

            SensorReadings initialReadings = null;
            manager.InjectSwarmSnapshot(null, sr => { initialReadings = sr; });

            Assert(initialReadings != null, "Initial SensorReadings must not be null");
            Assert(initialReadings.rfWaterfallPeakDbm < 0.0f,
                "Initial rfWaterfallPeakDbm must be a negative float, got " + initialReadings.rfWaterfallPeakDbm);
            Assert(Math.Abs(initialReadings.rfWaterfallPeakGHz - 5.825f) < 0.001f,
                "Initial rfWaterfallPeakGHz must be 5.825 GHz, got " + initialReadings.rfWaterfallPeakGHz);
            Assert(Math.Abs(initialReadings.dopplerPeakHz - 0.0f) < 0.0001f,
                "dopplerPeakHz must be 0.0f (P1 placeholder)");
            Assert(Math.Abs(initialReadings.acousticSplDb - 0.0f) < 0.0001f,
                "acousticSplDb must be 0.0f (P1 placeholder)");

            int initialRadarCount = initialReadings.radarDetectedCount;
            float prevPeakDbm = initialReadings.rfWaterfallPeakDbm;

            Console.WriteLine(string.Format(
                "[SCENARIO_TICK t=0ms] rfWaterfallPeakGHz={0:F3} GHz | rfWaterfallPeakDbm={1:F2} dBm | radarDetectedCount={2} | dopplerPeakHz={3:F1} | acousticSplDb={4:F1}",
                initialReadings.rfWaterfallPeakGHz,
                initialReadings.rfWaterfallPeakDbm,
                initialReadings.radarDetectedCount,
                initialReadings.dopplerPeakHz,
                initialReadings.acousticSplDb));

            // Step scenario forward in 100 ms ticks as swarm approaches from ~260m to ~50m
            // At 120 km/h (33.33 m/s), 65 ticks (6.5s) covers ~216m -> reaches ~44m from origin
            int maxRadarCountObserved = initialRadarCount;
            SensorReadings readingsAt50m = null;
            float closestDistTo50m = float.MaxValue;

            for (int tick = 1; tick <= 65; tick++)
            {
                long tMs = tick * 100L;
                manager.StepFlocking(0.1f);

                SensorReadings tickReadings = null;
                SwarmAgentState[] snap = manager.InjectSwarmSnapshot(null, sr => { tickReadings = sr; });

                Assert(tickReadings != null, "Tick " + tick + " SensorReadings must not be null");
                Assert(tickReadings.rfWaterfallPeakDbm < 0.0f, "rfWaterfallPeakDbm must remain a negative float");
                Assert(tickReadings.rfWaterfallPeakDbm > prevPeakDbm,
                    string.Format("Tick {0}: rfWaterfallPeakDbm must get less negative as agents approach (prev={1:F3}, curr={2:F3})",
                        tick, prevPeakDbm, tickReadings.rfWaterfallPeakDbm));

                prevPeakDbm = tickReadings.rfWaterfallPeakDbm;
                if (tickReadings.radarDetectedCount > maxRadarCountObserved)
                {
                    maxRadarCountObserved = tickReadings.radarDetectedCount;
                }

                // Track closest approach to 50m range
                float minAgentDist = float.MaxValue;
                for (int i = 0; i < snap.Length; i++)
                {
                    float d = Vector3.Distance(snap[i].positionWorld, Vector3.zero);
                    if (d < minAgentDist)
                        minAgentDist = d;
                }

                if (Math.Abs(minAgentDist - 50.0f) < closestDistTo50m)
                {
                    closestDistTo50m = Math.Abs(minAgentDist - 50.0f);
                    readingsAt50m = tickReadings;
                }

                if (tick == 10 || tick == 30 || tick == 50 || tick == 63)
                {
                    Console.WriteLine(string.Format(
                        "[SCENARIO_TICK t={0}ms | range≈{1:F1}m] rfWaterfallPeakGHz={2:F3} GHz | rfWaterfallPeakDbm={3:F2} dBm | radarDetectedCount={4}",
                        tMs,
                        minAgentDist,
                        tickReadings.rfWaterfallPeakGHz,
                        tickReadings.rfWaterfallPeakDbm,
                        tickReadings.radarDetectedCount));
                }
            }

            // Verify radarDetectedCount increased from initial spawn (2 decoys) to all 6 agents as they approached
            Assert(initialRadarCount == 2,
                "At ~260m spawn, expected 2 high-RCS decoys detected above -90 dBm, got " + initialRadarCount);
            Assert(maxRadarCountObserved == 6,
                "As agents approach inside 230m/50m, radarDetectedCount must increase to all 6 agents, got " + maxRadarCountObserved);
            Assert(readingsAt50m != null && readingsAt50m.radarDetectedCount == 6,
                "At ~50m range, all 6 agents must be radar-detected");

            // Exact 50m Friis evaluation check
            float exact50mDbm = RfPhysics.ComputeFriisDbm(23.0f, 5.825f, 50.0f);
            float expectedFspl50m = (20.0f * (float)Math.Log10(0.05)) + (20.0f * (float)Math.Log10(5.825)) + 92.45f;
            float expected50mDbm = 23.0f - expectedFspl50m;
            Assert(Math.Abs(exact50mDbm - expected50mDbm) < 0.001f,
                string.Format("At 50m, ComputeFriisDbm must equal {0:F2} dBm, got {1:F2} dBm", expected50mDbm, exact50mDbm));

            Console.WriteLine(string.Format(
                "50m verification: exact Friis = {0:F2} dBm (FSPL={1:F2} dB); radarDetectedCount increased {2} -> {3}.",
                exact50mDbm, expectedFspl50m, initialRadarCount, maxRadarCountObserved));
            Console.WriteLine("[PASS] Exact Mission Card Punjab Scenario Approach PASSED");
        }

        /// <summary>
        /// Validates RfPhysics.ComputeFriisDbm across bands and distances.
        /// </summary>
        public static void TestFriisFormulaMathematics()
        {
            Console.WriteLine("\n-- Test 2: RfPhysics.ComputeFriisDbm Formula Verification --");

            // 1. Distance monotonicity: closer distance = stronger (less negative) received power
            float dbm300m = RfPhysics.ComputeFriisDbm(23.0f, 5.825f, 300.0f);
            float dbm150m = RfPhysics.ComputeFriisDbm(23.0f, 5.825f, 150.0f);
            float dbm50m = RfPhysics.ComputeFriisDbm(23.0f, 5.825f, 50.0f);

            Assert(dbm300m < dbm150m && dbm150m < dbm50m && dbm50m < 0.0f,
                "Received power must be negative and increase monotonically as distance decreases");

            // Halving distance (300m -> 150m) must increase received power by ~6.02 dB
            float halfDistGain = dbm150m - dbm300m;
            Assert(Math.Abs(halfDistGain - 6.0206f) < 0.01f,
                "Halving distance must increase Friis received power by 6.02 dB, got " + halfDistGain);

            // 2. Frequency dependence: lower frequency has lower path loss at equal distance & txPower
            float dbm915Mhz = RfPhysics.ComputeFriisDbm(23.0f, 0.915f, 100.0f);
            float dbm24Ghz = RfPhysics.ComputeFriisDbm(23.0f, 2.4f, 100.0f);
            float dbm58Ghz = RfPhysics.ComputeFriisDbm(23.0f, 5.825f, 100.0f);

            Assert(dbm915Mhz > dbm24Ghz && dbm24Ghz > dbm58Ghz,
                "Lower frequency must experience less free-space path loss at identical range");

            // 3. Zero/negative distance singularity clamp
            float dbmZeroDist = RfPhysics.ComputeFriisDbm(23.0f, 5.825f, 0.0f);
            Assert(!float.IsNaN(dbmZeroDist) && !float.IsInfinity(dbmZeroDist),
                "Zero distance must be clamped without NaN or Infinity");

            Console.WriteLine("[PASS] RfPhysics.ComputeFriisDbm Formula Verification PASSED");
        }

        /// <summary>
        /// Validates RfPhysics.ComputeRadarReturnDbm two-way loss and RCS scaling.
        /// </summary>
        public static void TestMonostaticRadarReturnMathematics()
        {
            Console.WriteLine("\n-- Test 3: RfPhysics.ComputeRadarReturnDbm Two-Way Return & RCS --");

            // Decoy (RCS = 0.08 m²) vs Attacker (RCS = 0.012 m²) at same frequency and distance (150m)
            float decoyReturn = RfPhysics.ComputeRadarReturnDbm(23.0f, 5.825f, 150.0f, 0.08f);
            float attackerReturn = RfPhysics.ComputeRadarReturnDbm(23.0f, 5.825f, 150.0f, 0.012f);

            // 10 * log10(0.08 / 0.012) = 8.239 dB stronger return for decoy
            float rcsDiffDb = decoyReturn - attackerReturn;
            Assert(Math.Abs(rcsDiffDb - 8.239f) < 0.02f,
                "Decoy (0.08 m²) must have 8.24 dB stronger radar return than attacker (0.012 m²), got " + rcsDiffDb);

            // Both must exceed -90 dBm at 150m
            Assert(decoyReturn > -90.0f && attackerReturn > -90.0f,
                "Both decoy and attacker must be detectable (> -90 dBm) at 150m");

            // Beyond 500m (e.g. 600m), attacker return must be below -90 dBm
            float farAttackerReturn = RfPhysics.ComputeRadarReturnDbm(23.0f, 5.825f, 600.0f, 0.012f);
            Assert(farAttackerReturn < -90.0f,
                "Attacker at 600m must fall below -90 dBm radar threshold, got " + farAttackerReturn);

            // Non-positive RCS guard
            float zeroRcsReturn = RfPhysics.ComputeRadarReturnDbm(23.0f, 5.825f, 50.0f, 0.0f);
            Assert(zeroRcsReturn <= -900.0f, "Zero RCS must return floor value below threshold");

            Console.WriteLine("[PASS] RfPhysics.ComputeRadarReturnDbm Verification PASSED");
        }

        /// <summary>
        /// Validates SensorSuiteComputer MonoBehaviour inheritance, silent/neutralised filtering, and P1 placeholders.
        /// </summary>
        public static void TestSensorSuiteComputerFilteringAndPlaceholders()
        {
            Console.WriteLine("\n-- Test 4: SensorSuiteComputer Filtering & P1 Placeholders --");

            SensorSuiteComputer computer = new SensorSuiteComputer();
            Assert(computer is MonoBehaviour, "SensorSuiteComputer must inherit from MonoBehaviour");

            SwarmAgentState silentAgent = new SwarmAgentState
            {
                agentId = "agent-silent",
                positionWorld = new Vector3(0f, 8f, 60f),
                rfEmitting = false,
                rfFreqGHz = 5.825f,
                rfPowerDbm = 23.0f,
                rcs = 0.012f,
                isNeutralised = false
            };

            SwarmAgentState neutralisedAgent = new SwarmAgentState
            {
                agentId = "agent-down",
                positionWorld = new Vector3(0f, 8f, 30f),
                rfEmitting = true,
                rfFreqGHz = 5.825f,
                rfPowerDbm = 23.0f,
                rcs = 0.08f,
                isNeutralised = true
            };

            // Silent non-neutralised agent: no RF peak (-90 dBm noise floor), but still radar-detected (radarDetectedCount = 1)!
            SensorReadings r1 = computer.ComputeSensorReadings(new SwarmAgentState[] { silentAgent, neutralisedAgent }, Vector3.zero);
            Assert(Math.Abs(r1.rfWaterfallPeakDbm - (-90.0f)) < 0.001f,
                "When no active agent is emitting RF, rfWaterfallPeakDbm must be -90.0 dBm noise floor");
            Assert(Math.Abs(r1.rfWaterfallPeakGHz - 0.0f) < 0.001f,
                "When no active agent is emitting RF, rfWaterfallPeakGHz must be 0.0 GHz");
            Assert(r1.radarDetectedCount == 1,
                "Silent non-neutralised agent at 60m must still be counted by radar (neutralised agent excluded)");
            Assert(r1.dopplerPeakHz == 0.0f && r1.acousticSplDb == 0.0f,
                "dopplerPeakHz and acousticSplDb must be 0.0f placeholders in SA-P0-B");

            Console.WriteLine("[PASS] SensorSuiteComputer Filtering & P1 Placeholders PASSED");
        }

        /// <summary>
        /// Validates SwarmManager registration of SensorSuiteComputer and HandleWsMessage(SCENARIO_TICK) injection.
        /// </summary>
        public static void TestSwarmManagerRegistrationAndTickInjection()
        {
            Console.WriteLine("\n-- Test 5: SwarmManager Registration & SCENARIO_TICK Injection --");

            SwarmManager manager = new SwarmManager();
            SensorSuiteComputer customComputer = new SensorSuiteComputer();
            manager.RegisterSensorSuiteComputer(customComputer);
            Assert(object.ReferenceEquals(manager.SensorSuiteComputer, customComputer),
                "RegisterSensorSuiteComputer must bind the provided SensorSuiteComputer instance");

            string startJson = "{\"sessionId\":\"sess-20261011-123000\",\"scenarioId\":\"punjab-fog-pincer-01\",\"seed\":42819}";
            manager.HandleWsMessage("SCENARIO_START", startJson);

            SensorReadings injectedReadings = null;
            manager.OnSensorReadingsInjected += sr => { injectedReadings = sr; };

            manager.HandleWsMessage("SCENARIO_TICK", "{}");
            Assert(injectedReadings != null, "HandleWsMessage(SCENARIO_TICK) must compute and inject SensorReadings");
            Assert(injectedReadings.rfWaterfallPeakDbm < 0.0f && injectedReadings.rfWaterfallPeakDbm > -90.0f,
                "Injected rfWaterfallPeakDbm must be live Friis value above noise floor");
            Assert(Math.Abs(injectedReadings.rfWaterfallPeakGHz - 5.825f) < 0.001f,
                "Injected rfWaterfallPeakGHz must be 5.825 GHz");

            Console.WriteLine("[PASS] SwarmManager Registration & SCENARIO_TICK Injection PASSED");
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
