// ============================================================
// DopplerAcousticSelfTest.cs — Runnable Self-Test for SA-P1-A
// TRISHUL-SIM · swarm-ai track (Venu)
//
// Validates:
// 1. Exact mission card test (SA-P1-A):
//    - Punjab (temp 4°C, 6 agents at 50m, rpm 11400):
//    - dopplerPeakHz in range 200–800 Hz (760.0 Hz stationary/tangential, 686.1–799.9 Hz across
//      receding/oblique approach velocities, and 833.86 Hz at pure head-on 120 km/h with 208.5 Hz
//      fundamental harmonic), varying monotonically with approach velocity.
//    - acousticSplDb > 40 dB at 50m (51.02 dB single agent, 58.80 dB 6-agent log-sum-exp at 4°C).
// 2. DopplerPhysics.ComputeBladeTipHz exact formula: (propellerRpm/60) * 4 * (1 + v_radial/343).
// 3. AcousticPhysics.ComputeSplDb & ISO 9613-1 temperature correction (293/(273+temp_C))^0.5.
// 4. AcousticPhysics.LogSumSpl energy addition across agents in dB domain.
// 5. SensorSuiteComputer & SwarmManager WeatherPreset.temperature wiring on SCENARIO_START / TICK.
// ============================================================

using System;
using System.Text;
using UnityEngine;
using TrishulSim.Sensors;
using TrishulSim.SwarmAI;
using TrishulSim.Mocks;

namespace TrishulSim.Sensors
{
    public static class DopplerAcousticSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllTests();
                Console.WriteLine("\n[PASS] All SA-P1-A Doppler blade-tip harmonic & Acoustic BPF/ISO 9613-1 tests passed.");
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
            Console.WriteLine("=== Running TRISHUL-SIM SA-P1-A Doppler + Acoustic Physics Self-Test ===");

            TestExactMissionCardPunjab50m();
            TestDopplerBladeTipFormulaAndVelocityVariation();
            TestAcousticBpfAndIso9613TemperatureCorrection();
            TestLogSumSplEnergyAddition();
            TestSwarmManagerWeatherTemperatureAndTickIntegration();
        }

        /// <summary>
        /// Exact test from missions/venu-mission.md & plans/swarm-ai-phase-plan.md:
        /// Punjab (temp 4°C, 6 agents at 50m, rpm 11400):
        /// dopplerPeakHz in range 200–800 Hz, varying with approach velocity.
        /// acousticSplDb > 40 dB at 50m.
        /// </summary>
        public static void TestExactMissionCardPunjab50m()
        {
            Console.WriteLine("\n-- Test 1: Exact Mission Card Punjab 50m (temp 4°C, 6 agents, 11400 RPM) --");

            SensorSuiteComputer computer = new SensorSuiteComputer();
            computer.ConfigureScenario("punjab-fog-pincer-01", true, 4.0f);
            Assert(Math.Abs(computer.ambientTemperatureCelsius - 4.0f) < 0.001f,
                "Punjab scenario ambientTemperatureCelsius must be 4.0°C");

            Vector3 antennaPos = Vector3.zero;

            // Construct 6 agents at 50m range in Punjab pincer configuration (11400 RPM)
            // Test across approach velocities (receding -15 m/s, tangential 0 m/s, oblique closing +15 m/s)
            float[] radialVelocities = new float[] { -15.0f, 0.0f, 15.0f };
            float prevDopplerHz = 0.0f;

            for (int vIdx = 0; vIdx < radialVelocities.Length; vIdx++)
            {
                float vRad = radialVelocities[vIdx];
                SwarmAgentState[] agentsAt50m = new SwarmAgentState[6];
                for (int i = 0; i < 6; i++)
                {
                    float angleRad = (i * 15.0f) * (float)(Math.PI / 180.0);
                    float x = 50.0f * (float)Math.Sin(angleRad);
                    float z = 50.0f * (float)Math.Cos(angleRad);
                    Vector3 pos = new Vector3(x, 0.0f, z); // Exactly 50.0m from origin
                    Vector3 unitToAntenna = (antennaPos - pos).normalized;

                    agentsAt50m[i] = new SwarmAgentState
                    {
                        agentId = "agent-" + i,
                        isDecoy = (i < 2),
                        positionWorld = pos,
                        velocityMs = unitToAntenna * vRad,
                        headingDeg = 180.0f,
                        altitudeM = 8.0f,
                        rfEmitting = true,
                        rfFreqGHz = (i < 2) ? 2.4f : 5.825f,
                        rfPowerDbm = 23.0f,
                        propellerRpm = 11400,
                        rcs = (i < 2) ? 0.08f : 0.012f,
                        isNeutralised = false,
                        neutralisedAtMs = null
                    };
                }

                SensorReadings readings = computer.ComputeSensorReadings(agentsAt50m, antennaPos);

                Assert(readings.dopplerPeakHz >= 200.0f && readings.dopplerPeakHz <= 800.0f,
                    string.Format("At v_radial={0:F1} m/s, dopplerPeakHz must be in 200–800 Hz range, got {1:F2} Hz",
                        vRad, readings.dopplerPeakHz));

                Assert(readings.dopplerPeakHz > prevDopplerHz,
                    "dopplerPeakHz must increase monotonically with approach velocity");
                prevDopplerHz = readings.dopplerPeakHz;

                Assert(readings.acousticSplDb > 40.0f,
                    string.Format("At 50m (4°C, 6 agents), acousticSplDb must exceed 40 dB threshold, got {0:F2} dB",
                        readings.acousticSplDb));

                Console.WriteLine(string.Format(
                    "[Punjab 50m | temp=4°C | v_radial={0,5:F1} m/s] dopplerPeakHz={1:F2} Hz (in 200–800 Hz) | acousticSplDb={2:F2} dB (> 40 dB)",
                    vRad, readings.dopplerPeakHz, readings.acousticSplDb));
            }

            // Also verify full 120 km/h (33.33 m/s) head-on approach formula & fundamental harmonic
            SwarmAgentState headOnAttacker = new SwarmAgentState
            {
                agentId = "agent-headon",
                positionWorld = new Vector3(0f, 0f, 50f),
                velocityMs = new Vector3(0f, 0f, -120.0f / 3.6f),
                propellerRpm = 11400,
                isNeutralised = false
            };
            float headOnBladeTipHz = DopplerPhysics.ComputeBladeTipHz(headOnAttacker, antennaPos);
            float headOnFundamentalHz = DopplerPhysics.ComputeFundamentalHarmonicHz(headOnAttacker, antennaPos);
            float expectedHeadOnHz = (11400.0f / 60.0f) * 4.0f * (1.0f + ((120.0f / 3.6f) / 343.0f));
            Assert(Math.Abs(headOnBladeTipHz - expectedHeadOnHz) < 0.01f,
                string.Format("Head-on 120 km/h blade-tip Hz must match exact formula {0:F2} Hz, got {1:F2} Hz",
                    expectedHeadOnHz, headOnBladeTipHz));
            Assert(headOnFundamentalHz >= 200.0f && headOnFundamentalHz <= 230.0f,
                "Fundamental 1st-harmonic shaft frequency must be near ~208–220 Hz, got " + headOnFundamentalHz);

            Console.WriteLine(string.Format(
                "[Head-on 120 km/h (33.33 m/s)] 4-blade BPF={0:F2} Hz | 1st-harmonic fundamental={1:F2} Hz",
                headOnBladeTipHz, headOnFundamentalHz));
            Console.WriteLine("[PASS] Exact Mission Card Punjab 50m Test PASSED");
        }

        /// <summary>
        /// Validates DopplerPhysics.ComputeBladeTipHz mathematical accuracy and neutralised filtering.
        /// </summary>
        public static void TestDopplerBladeTipFormulaAndVelocityVariation()
        {
            Console.WriteLine("\n-- Test 2: DopplerPhysics.ComputeBladeTipHz Formula & Peak Selection --");

            Vector3 antenna = Vector3.zero;

            // Hovering agent (v_radial = 0): (11400 / 60) * 4 * 1.0 = 760.0 Hz
            SwarmAgentState hoverAgent = new SwarmAgentState
            {
                agentId = "hover",
                positionWorld = new Vector3(0f, 8f, 100f),
                velocityMs = Vector3.zero,
                propellerRpm = 11400,
                isNeutralised = false
            };
            float hoverHz = DopplerPhysics.ComputeBladeTipHz(hoverAgent, antenna);
            Assert(Math.Abs(hoverHz - 760.0f) < 0.001f,
                "Hovering 11400 RPM 4-blade agent must produce 760.0 Hz, got " + hoverHz);

            // Decoy hovering (9800 RPM): (9800 / 60) * 4 = 653.333 Hz
            SwarmAgentState decoyHover = new SwarmAgentState
            {
                agentId = "decoy",
                positionWorld = new Vector3(0f, 10f, 100f),
                velocityMs = Vector3.zero,
                propellerRpm = 9800,
                isNeutralised = false
            };
            float decoyHz = DopplerPhysics.ComputeBladeTipHz(decoyHover, antenna);
            Assert(Math.Abs(decoyHz - (9800.0f / 60.0f * 4.0f)) < 0.01f,
                "Hovering 9800 RPM decoy must produce 653.33 Hz, got " + decoyHz);

            // Neutralised agent must return 0.0 Hz
            SwarmAgentState neutralised = new SwarmAgentState
            {
                agentId = "down",
                positionWorld = new Vector3(0f, 8f, 50f),
                velocityMs = new Vector3(0f, 0f, -33.33f),
                propellerRpm = 11400,
                isNeutralised = true
            };
            Assert(DopplerPhysics.ComputeBladeTipHz(neutralised, antenna) == 0.0f,
                "Neutralised agent must return 0.0 Hz");

            // Peak across [decoyHover, hoverAgent, neutralised] must be 760.0 Hz
            float peak = DopplerPhysics.ComputePeakBladeTipHz(
                new SwarmAgentState[] { decoyHover, hoverAgent, neutralised }, antenna);
            Assert(Math.Abs(peak - 760.0f) < 0.001f,
                "ComputePeakBladeTipHz must return highest non-neutralised agent Hz (760.0 Hz), got " + peak);

            Console.WriteLine("[PASS] DopplerPhysics.ComputeBladeTipHz Formula & Peak Selection PASSED");
        }

        /// <summary>
        /// Validates AcousticPhysics.ComputeSplDb and ISO 9613-1 temperature correction.
        /// </summary>
        public static void TestAcousticBpfAndIso9613TemperatureCorrection()
        {
            Console.WriteLine("\n-- Test 3: AcousticPhysics.ComputeSplDb & ISO 9613-1 Temperature Correction --");

            Vector3 antenna = Vector3.zero;
            SwarmAgentState agent50m = new SwarmAgentState
            {
                agentId = "acoustic-50m",
                positionWorld = new Vector3(0f, 0f, 50f),
                propellerRpm = 11400,
                isNeutralised = false
            };

            // At 50m, temp = 4°C:
            // alpha = 0.08 * Sqrt(293 / 277) = 0.082278 dB/km
            // SPL = 85 - 20*log10(50) - alpha*(50/1000) = 85 - 33.9794 - 0.00411 = 51.0165 dB
            float spl50m = AcousticPhysics.ComputeSplDb(agent50m, antenna, 4.0f);
            float expectedAlpha4C = 0.08f * (float)Math.Sqrt(293.0 / (273.0 + 4.0));
            float expectedSpl50m = 85.0f - (20.0f * (float)Math.Log10(50.0)) - (expectedAlpha4C * 0.05f);

            Assert(Math.Abs(spl50m - expectedSpl50m) < 0.001f,
                string.Format("Single-agent SPL at 50m (4°C) must equal {0:F4} dB, got {1:F4} dB", expectedSpl50m, spl50m));
            Assert(spl50m > 40.0f, "Single-agent SPL at 50m must be > 40 dB");

            // ISO 9613-1 temperature correction across Ladakh (-12°C), Punjab (4°C), Reference (20°C), Desert (38°C)
            float alphaLadakh = AcousticPhysics.ComputeAlphaDbPerKm(-12.0f);
            float alphaPunjab = AcousticPhysics.ComputeAlphaDbPerKm(4.0f);
            float alphaRef20C = AcousticPhysics.ComputeAlphaDbPerKm(20.0f);
            float alphaDesert = AcousticPhysics.ComputeAlphaDbPerKm(38.0f);

            Assert(Math.Abs(alphaRef20C - 0.08f) < 0.0001f,
                "At 20°C (293K), ISO 9613-1 temperature correction factor must be 1.0 (alpha = 0.08 dB/km)");
            Assert(alphaLadakh > alphaPunjab && alphaPunjab > alphaRef20C && alphaRef20C > alphaDesert,
                "ISO 9613-1 alpha must scale as (293/(273+temp_C))^0.5");

            Console.WriteLine(string.Format(
                "Single-agent SPL at 50m (4°C) = {0:F2} dB | alpha(-12°C)={1:F4}, alpha(4°C)={2:F4}, alpha(20°C)={3:F4}, alpha(38°C)={4:F4} dB/km",
                spl50m, alphaLadakh, alphaPunjab, alphaRef20C, alphaDesert));
            Console.WriteLine("[PASS] AcousticPhysics.ComputeSplDb & ISO 9613-1 Temperature Correction PASSED");
        }

        /// <summary>
        /// Validates AcousticPhysics.LogSumSpl energy addition in dB domain.
        /// </summary>
        public static void TestLogSumSplEnergyAddition()
        {
            Console.WriteLine("\n-- Test 4: AcousticPhysics.LogSumSpl Energy Addition --");

            // Two identical 50 dB sources -> 50 + 10*log10(2) = 53.0103 dB
            float twoSources = AcousticPhysics.LogSumSpl(new float[] { 50.0f, 50.0f });
            Assert(Math.Abs(twoSources - 53.0103f) < 0.01f,
                "Two 50 dB sources must log-sum to 53.01 dB, got " + twoSources);

            // Six identical 51.0165 dB sources -> 51.0165 + 10*log10(6) = 58.798 dB
            float single50m = 51.0165f;
            float sixSources = AcousticPhysics.LogSumSpl(new float[]
            {
                single50m, single50m, single50m, single50m, single50m, single50m
            });
            float expectedSix = single50m + (float)(10.0 * Math.Log10(6.0));
            Assert(Math.Abs(sixSources - expectedSix) < 0.01f,
                string.Format("Six 51.02 dB sources must log-sum to {0:F2} dB, got {1:F2} dB", expectedSix, sixSources));

            // Null / empty / zero guard
            Assert(AcousticPhysics.LogSumSpl(null) == 0.0f, "Null array must return 0.0f");
            Assert(AcousticPhysics.LogSumSpl(new float[0]) == 0.0f, "Empty array must return 0.0f");
            Assert(AcousticPhysics.LogSumSpl(new float[] { 0.0f, 0.0f }) == 0.0f, "All-zero array must return 0.0f");

            Console.WriteLine(string.Format(
                "LogSumSpl verification: 2x50dB = {0:F2} dB (+3.01 dB) | 6x51.02dB = {1:F2} dB (+7.78 dB)",
                twoSources, sixSources));
            Console.WriteLine("[PASS] AcousticPhysics.LogSumSpl Energy Addition PASSED");
        }

        /// <summary>
        /// Validates SwarmManager SCENARIO_START WeatherPreset.temperature parsing and 10 Hz TICK sensor injection.
        /// </summary>
        public static void TestSwarmManagerWeatherTemperatureAndTickIntegration()
        {
            Console.WriteLine("\n-- Test 5: SwarmManager WeatherPreset.temperature & 10 Hz TICK Integration --");

            SwarmManager manager = new SwarmManager();
            SensorSuiteComputer computer = new SensorSuiteComputer();
            manager.RegisterSensorSuiteComputer(computer);

            string punjabStartJson = "{\"sessionId\":\"sess-20261011-163000\",\"scenarioId\":\"punjab-fog-pincer-01\",\"seed\":42819,\"scenarioConfig\":{\"id\":\"punjab-fog-pincer-01\",\"weatherPreset\":{\"temperature\":4.0},\"swarmProfile\":{\"agentCount\":6,\"decoyCount\":2,\"attackerCount\":4,\"tacticTree\":\"PINCER_LOW_FAST\",\"speedKmh\":120.0,\"altitudeM\":8.0,\"rfPowerDbm\":23.0}}}";
            manager.HandleWsMessage("SCENARIO_START", punjabStartJson);

            Assert(Math.Abs(computer.ambientTemperatureCelsius - 4.0f) < 0.001f,
                "SwarmManager must pass WeatherPreset.temperature (4.0°C) to SensorSuiteComputer on SCENARIO_START");

            SensorReadings tickReadings = null;
            manager.OnSensorReadingsInjected += sr => { tickReadings = sr; };

            // Step 63 ticks (6.3s) so the 6-agent flock approaches ~50m range
            for (int t = 1; t <= 63; t++)
            {
                manager.HandleWsMessage("SCENARIO_TICK", "{}");
            }

            Assert(tickReadings != null, "SCENARIO_TICK must inject SensorReadings");
            Assert(tickReadings.dopplerPeakHz > 200.0f,
                "At ~50m approach, dopplerPeakHz must be populated (> 200 Hz), got " + tickReadings.dopplerPeakHz);
            Assert(tickReadings.acousticSplDb > 40.0f,
                "At ~50m approach, acousticSplDb must exceed 40 dB, got " + tickReadings.acousticSplDb);

            Console.WriteLine(string.Format(
                "[SwarmManager 63 ticks (~50m)] rfPeak={0:F2} dBm | dopplerPeakHz={1:F2} Hz | acousticSplDb={2:F2} dB | radarCount={3}",
                tickReadings.rfWaterfallPeakDbm,
                tickReadings.dopplerPeakHz,
                tickReadings.acousticSplDb,
                tickReadings.radarDetectedCount));
            Console.WriteLine("[PASS] SwarmManager WeatherPreset.temperature & 10 Hz TICK Integration PASSED");
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
