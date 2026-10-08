// ============================================================
// EwDeconflictionSelfTest.cs — Runnable Self-Test for SA-P1-C
// TRISHUL-SIM · swarm-ai track (Uday)
//
// Validates:
// 1. Exact mission card tests:
//    - band=0.915, bearing=90°: friendlyBlackoutRisk=true
//    - band=5.825, any bearing: friendlyBlackoutRisk=false
//    - band=0.915, bearing=180°: friendlyBlackoutRisk=false
// 2. Out-of-band rejection (2.4 GHz, 5.825 GHz)
// 3. In-band VHF/UHF detection (< 0.5 GHz)
// 4. Inactive friendly VHF guard (friendlyVhfActive=false)
// 5. Disengaged jammer guard (engaged=false)
// 6. Half-power beamwidth cone boundaries (60°–120° azimuth)
// 7. Degree wrap-around and negative bearing normalization
// 8. SensorSuiteComputer tick update & payload injection integration
//
// Compiles directly with csc.exe or inside Unity Editor tests.
// ============================================================

using System;
using System.Text;

namespace TrishulSim.Sensors
{
    public static class EwDeconflictionSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllTests();
                Console.WriteLine("\n[PASS] All SA-P1-C EW deconfliction tests passed.");
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
            Console.WriteLine("=== Running TRISHUL-SIM SA-P1-C EW Deconfliction Self-Test ===");

            TestExactCardRequirements();
            TestOutOfBandSafety();
            TestVhfUhfBandRisk();
            TestFriendlyVhfActiveCondition();
            TestDisengagedJammerSafety();
            TestBeamwidthBoundaries();
            TestAngularWrapAround();
            TestSensorSuiteComputerIntegration();
        }

        /// <summary>
        /// Exact tests specified in uday-mission.md and plans/swarm-ai-phase-plan.md.
        /// </summary>
        public static void TestExactCardRequirements()
        {
            Console.WriteLine("\n-- Test 1: Exact Mission Card Test Cases --");

            // 1. band=0.915, bearing=90° -> friendlyBlackoutRisk=true
            EWJammerState jammer1 = new EWJammerState
            {
                engaged = true,
                bearingDeg = 90.0f,
                elevationDeg = 0.0f,
                bandGHz = 0.915f,
                friendlyBlackoutRisk = false
            };
            bool risk1 = EwDeconfliction.ComputeBlackoutRisk(jammer1, friendlyVhfActive: true);
            Console.WriteLine("band=0.915, bearing=90°: friendlyBlackoutRisk=" + risk1.ToString().ToLower());
            Assert(risk1 == true, "Expected friendlyBlackoutRisk=true for band=0.915 at bearing=90°");

            // 2. band=5.825, any bearing -> false
            float[] testBearings = new float[] { 90.0f, 0.0f, 180.0f, 247.0f, 315.0f };
            foreach (float b in testBearings)
            {
                EWJammerState jammer2 = new EWJammerState
                {
                    engaged = true,
                    bearingDeg = b,
                    elevationDeg = 0.0f,
                    bandGHz = 5.825f,
                    friendlyBlackoutRisk = false
                };
                bool risk2 = EwDeconfliction.ComputeBlackoutRisk(jammer2, friendlyVhfActive: true);
                Console.WriteLine(string.Format("band=5.825, bearing={0}°: friendlyBlackoutRisk={1}", b, risk2.ToString().ToLower()));
                Assert(risk2 == false, "Expected friendlyBlackoutRisk=false for band=5.825 at bearing=" + b);
            }

            // 3. band=0.915, bearing=180° -> false (out of beam)
            EWJammerState jammer3 = new EWJammerState
            {
                engaged = true,
                bearingDeg = 180.0f,
                elevationDeg = 0.0f,
                bandGHz = 0.915f,
                friendlyBlackoutRisk = false
            };
            bool risk3 = EwDeconfliction.ComputeBlackoutRisk(jammer3, friendlyVhfActive: true);
            Console.WriteLine("band=0.915, bearing=180°: friendlyBlackoutRisk=" + risk3.ToString().ToLower() + " (out of beam)");
            Assert(risk3 == false, "Expected friendlyBlackoutRisk=false for band=0.915 at bearing=180°");

            Console.WriteLine("[PASS] Exact Mission Card Test Cases PASSED");
        }

        /// <summary>
        /// Validates that 2.4 GHz and 5.825 GHz are always out-of-band and never risk blackout.
        /// </summary>
        public static void TestOutOfBandSafety()
        {
            Console.WriteLine("\n-- Test 2: Out-Of-Band Microwave Safety --");

            // 2.4 GHz aimed directly at friendly VHF antenna
            EWJammerState jammer24 = new EWJammerState(true, 90.0f, 0.0f, 2.4f);
            bool risk24 = EwDeconfliction.ComputeBlackoutRisk(jammer24, true);
            Assert(risk24 == false, "2.4 GHz must not trigger blackout risk");

            // 5.825 GHz aimed at Beat 4 attack vector (247°)
            EWJammerState jammer58 = new EWJammerState(true, 247.0f, -5.0f, 5.825f);
            bool risk58 = EwDeconfliction.ComputeBlackoutRisk(jammer58, true);
            Assert(risk58 == false, "5.825 GHz on 247° attack axis must not trigger blackout risk");

            Console.WriteLine("[PASS] Out-of-band safety PASSED");
        }

        /// <summary>
        /// Validates that VHF/UHF frequencies (< 0.5 GHz) trigger blackout risk when beamed into friendly sector.
        /// </summary>
        public static void TestVhfUhfBandRisk()
        {
            Console.WriteLine("\n-- Test 3: VHF/UHF Low-Frequency Risk --");

            // 150 MHz VHF tactical radio frequency aimed towards friendly post
            EWJammerState jammerVhf = new EWJammerState(true, 85.0f, 0.0f, 0.150f);
            bool riskVhf = EwDeconfliction.ComputeBlackoutRisk(jammerVhf, true);
            Assert(riskVhf == true, "VHF 150 MHz aimed at 85° must trigger blackout risk");

            // 433 MHz UHF frequency aimed away from friendly post
            EWJammerState jammerUhfAway = new EWJammerState(true, 270.0f, 0.0f, 0.433f);
            bool riskUhfAway = EwDeconfliction.ComputeBlackoutRisk(jammerUhfAway, true);
            Assert(riskUhfAway == false, "UHF 433 MHz aimed at 270° must be safe (out of beam)");

            Console.WriteLine("[PASS] VHF/UHF band risk PASSED");
        }

        /// <summary>
        /// Validates that friendlyBlackoutRisk is false when friendly VHF radios are not active in sim zone.
        /// </summary>
        public static void TestFriendlyVhfActiveCondition()
        {
            Console.WriteLine("\n-- Test 4: friendlyVhfActive Condition --");

            // Band 0.915, bearing 90° with friendlyVhfActive=false
            EWJammerState jammer = new EWJammerState(true, 90.0f, 0.0f, 0.915f);
            bool risk = EwDeconfliction.ComputeBlackoutRisk(jammer, friendlyVhfActive: false);
            Assert(risk == false, "If friendly VHF is inactive, friendlyBlackoutRisk must be false");

            Console.WriteLine("[PASS] friendlyVhfActive condition PASSED");
        }

        /// <summary>
        /// Validates that disengaged jammer never triggers blackout risk.
        /// </summary>
        public static void TestDisengagedJammerSafety()
        {
            Console.WriteLine("\n-- Test 5: Disengaged Jammer Guard --");

            // Jammer set to 0.915 GHz and bearing 90°, but engaged=false
            EWJammerState jammer = new EWJammerState(false, 90.0f, 0.0f, 0.915f);
            bool risk = EwDeconfliction.ComputeBlackoutRisk(jammer, true);
            Assert(risk == false, "Disengaged jammer must not trigger blackout risk");

            // Null jammer check
            bool riskNull = EwDeconfliction.ComputeBlackoutRisk(null, true);
            Assert(riskNull == false, "Null jammer must return false without exception");

            Console.WriteLine("[PASS] Disengaged jammer safety PASSED");
        }

        /// <summary>
        /// Validates 60° beamwidth cone (±30° from 90°: 60° to 120°).
        /// </summary>
        public static void TestBeamwidthBoundaries()
        {
            Console.WriteLine("\n-- Test 6: Half-Power Beamwidth Cone Boundaries --");

            // Exactly at 60° (diff = 30° -> not < 30°)
            bool risk60 = EwDeconfliction.ComputeBlackoutRisk(true, 60.0f, 0.915f, true);
            Assert(risk60 == false, "At 60° boundary (diff=30°), risk must be false");

            // Inside at 61° (diff = 29° < 30°)
            bool risk61 = EwDeconfliction.ComputeBlackoutRisk(true, 61.0f, 0.915f, true);
            Assert(risk61 == true, "At 61° inside cone (diff=29°), risk must be true");

            // Inside at 119° (diff = 29° < 30°)
            bool risk119 = EwDeconfliction.ComputeBlackoutRisk(true, 119.0f, 0.915f, true);
            Assert(risk119 == true, "At 119° inside cone (diff=29°), risk must be true");

            // Exactly at 120° (diff = 30° -> not < 30°)
            bool risk120 = EwDeconfliction.ComputeBlackoutRisk(true, 120.0f, 0.915f, true);
            Assert(risk120 == false, "At 120° boundary (diff=30°), risk must be false");

            Console.WriteLine("[PASS] Beamwidth boundaries PASSED");
        }

        /// <summary>
        /// Validates modular angular distance and degree normalization.
        /// </summary>
        public static void TestAngularWrapAround()
        {
            Console.WriteLine("\n-- Test 7: Angular Wrap-Around & Normalization --");

            // Bearing 450° is 90°
            bool risk450 = EwDeconfliction.ComputeBlackoutRisk(true, 450.0f, 0.915f, true);
            Assert(risk450 == true, "Bearing 450° (90° mod 360) must trigger blackout risk");

            // Bearing -270° is 90°
            bool riskNeg270 = EwDeconfliction.ComputeBlackoutRisk(true, -270.0f, 0.915f, true);
            Assert(riskNeg270 == true, "Bearing -270° (90° normalized) must trigger blackout risk");

            // Bearing 355° vs 5° has angular distance of 10°
            float dist = EwDeconfliction.ComputeAngularDistance(355.0f, 5.0f);
            Assert(Math.Abs(dist - 10.0f) < 0.001f, "Angular distance between 355° and 5° must be 10°");

            Console.WriteLine("[PASS] Angular wrap-around PASSED");
        }

        /// <summary>
        /// Validates SensorSuiteComputer lifecycle, tick update, and payload injection.
        /// </summary>
        public static void TestSensorSuiteComputerIntegration()
        {
            Console.WriteLine("\n-- Test 8: SensorSuiteComputer Integration --");

            SensorSuiteComputer computer = new SensorSuiteComputer();
            computer.ConfigureScenario("punjab-fog-pincer-01");
            Assert(computer.friendlyVhfActive == true, "Punjab scenario must have friendlyVhfActive=true");

            // Tick 1: Trainee mistakenly engages 915 MHz towards friendly post (90°)
            EWJammerState wrongJammer = new EWJammerState(true, 90.0f, 0.0f, 0.915f);
            computer.UpdateTick(wrongJammer);
            Assert(wrongJammer.friendlyBlackoutRisk == true, "Tick update must set friendlyBlackoutRisk=true");

            // Tick 2: Trainee corrects aim to 247° attack axis on 5.825 GHz (Beat 4 doctrinal action)
            EWJammerState correctJammer = new EWJammerState(true, 247.0f, -5.0f, 5.825f);
            computer.UpdateTick(correctJammer);
            Assert(correctJammer.friendlyBlackoutRisk == false, "Doctrinal 5.825 GHz at 247° must clear friendlyBlackoutRisk to false");

            // Tick injection test
            EWJammerState tickJammer = new EWJammerState(true, 90.0f, 0.0f, 0.915f);
            bool tickVhfActive;
            computer.InjectTickPayload(tickJammer, out tickVhfActive);
            Assert(tickVhfActive == true, "Injected friendlyVhfActive must be true");
            Assert(tickJammer.friendlyBlackoutRisk == true, "Injected jammer must have friendlyBlackoutRisk=true");

            Console.WriteLine("[PASS] SensorSuiteComputer integration PASSED");
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
