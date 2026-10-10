using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TrishulSim.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TrishulSim.Scenarios
{
    /// <summary>
    /// Runnable self-test for US-P0-B (ScenarioConfig, ScenarioLibrary, ScenarioManager, SCENARIO_START broadcast,
    /// TraineeCockpit scene load, 1s ACK, error codes, and scene/meta GUID wiring).
    /// Compiles and runs directly with csc.exe or inside Unity tests.
    /// </summary>
    public static class ScenarioLibrarySelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                WsIpcSelfTest.RunAllAsync().GetAwaiter().GetResult();
                RunAllAsync().GetAwaiter().GetResult();
                Console.WriteLine("[PASS] All US-P0-B, US-P0-C, and US-P1-B Scenario Library, 4 Scenario Configs, SCENARIO_START, SCENARIO_TICK, and SCENARIO_END tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] " + ex);
                return 1;
            }
        }
#endif

        public static async Task RunAllAsync()
        {
            TestPunjabScenarioFileAndSchemaValidation();
            TestAllFourScenarioFilesAndSchemaValidation();
            TestScenarioLibraryLoadAllAndSeedRegeneration();
            TestSceneAndMetaGuidWiring();
            TestScenarioTickAndEndPayloadContracts();
            await TestScenarioLaunchBroadcastSceneLoadAndErrorsAsync();
            await TestScenarioTickLoopAbortAndTerminalConditionsAsync();
            await TestAllFourScenariosDropdownAndSwarmProfileLogsAsync();
        }

        private static void TestPunjabScenarioFileAndSchemaValidation()
        {
            string punjabPath = Path.Combine("Assets", "StreamingAssets", "scenarios", "punjab-fog-pincer-01.json");
            Assert(File.Exists(punjabPath), "punjab-fog-pincer-01.json must exist at " + punjabPath);

            string rawJson = File.ReadAllText(punjabPath, Encoding.UTF8);
            ScenarioConfig punjab = ScenarioConfig.Parse(rawJson);

            Assert(punjab.id == "punjab-fog-pincer-01", "Expected id 'punjab-fog-pincer-01'");
            Assert(punjab.name == "Punjab Border \u2014 Zero Visibility Pincer", "Expected exact English name");
            Assert(punjab.nameHindi == "\u092A\u0902\u091C\u093E\u092C \u0938\u0940\u092E\u093E \u2014 \u0936\u0942\u0928\u094D\u092F \u0926\u0943\u0936\u094D\u092F\u0924\u093E \u092A\u093F\u0902\u0938\u0930", "Expected exact Hindi name");
            Assert(punjab.region == ScenarioConfig.REGION_PUNJAB_RIVERINE, "Expected region PUNJAB_RIVERINE");
            Assert(punjab.durationMaxSecs == 120, "Expected durationMaxSecs == 120");
            Assert(punjab.seed == 42819, "Expected disk seed == 42819");
            Assert(punjab.version == 1, "Expected version == 1");

            Assert(punjab.weatherPreset != null, "weatherPreset must not be null");
            Assert(Math.Abs(punjab.weatherPreset.visibility - 15.0f) < 0.001f, "Expected visibility == 15.0");
            Assert(Math.Abs(punjab.weatherPreset.windSpeedKmh - 12.0f) < 0.001f, "Expected windSpeedKmh == 12.0");
            Assert(Math.Abs(punjab.weatherPreset.windBearingDeg - 270.0f) < 0.001f, "Expected windBearingDeg == 270.0");
            Assert(Math.Abs(punjab.weatherPreset.temperature - 4.0f) < 0.001f, "Expected temperature == 4.0");
            Assert(punjab.weatherPreset.isNight, "Expected isNight == true");

            Assert(punjab.swarmProfile != null, "swarmProfile must not be null");
            Assert(punjab.swarmProfile.agentCount == 6, "Expected agentCount == 6");
            Assert(punjab.swarmProfile.decoyCount == 2, "Expected decoyCount == 2");
            Assert(punjab.swarmProfile.attackerCount == 4, "Expected attackerCount == 4");
            Assert(punjab.swarmProfile.tacticTree == SwarmProfile.TACTIC_PINCER_LOW_FAST, "Expected tacticTree == PINCER_LOW_FAST");
            Assert(Math.Abs(punjab.swarmProfile.speedKmh - 120.0f) < 0.001f, "Expected speedKmh == 120.0");
            Assert(Math.Abs(punjab.swarmProfile.altitudeM - 8.0f) < 0.001f, "Expected altitudeM == 8.0");
            Assert(punjab.swarmProfile.rfFreqGHz != null && punjab.swarmProfile.rfFreqGHz.Length == 2, "Expected 2 rfFreqGHz entries");
            Assert(Math.Abs(punjab.swarmProfile.rfFreqGHz[0] - 5.825f) < 0.001f, "Expected rfFreqGHz[0] == 5.825");
            Assert(Math.Abs(punjab.swarmProfile.rfFreqGHz[1] - 2.4f) < 0.001f, "Expected rfFreqGHz[1] == 2.4");
            Assert(Math.Abs(punjab.swarmProfile.rfPowerDbm - 23.0f) < 0.001f, "Expected rfPowerDbm == 23.0");

            // Round-trip serialization test
            string roundTripJson = punjab.ToJson();
            ScenarioConfig roundTrip = ScenarioConfig.Parse(roundTripJson);
            Assert(roundTrip.id == punjab.id && roundTrip.nameHindi == punjab.nameHindi && roundTrip.seed == punjab.seed,
                "ScenarioConfig round-trip must preserve all fields");

            // Reject invalid configs
            ScenarioConfig invalid;
            string error;
            Assert(!ScenarioConfig.TryParse("{}", out invalid, out error), "Empty object must fail ScenarioConfig.TryParse");
            Assert(!ScenarioConfig.TryParse(rawJson.Replace("\"PUNJAB_RIVERINE\"", "\"UNKNOWN_REGION\""), out invalid, out error),
                "Unsupported region must fail ScenarioConfig.TryParse");
            Assert(!ScenarioConfig.TryParse(rawJson.Replace("\"PINCER_LOW_FAST\"", "\"INVALID_TACTIC\""), out invalid, out error),
                "Unsupported tacticTree must fail ScenarioConfig.TryParse");
            Assert(!ScenarioConfig.TryParse(rawJson.Replace("\"attackerCount\": 4", "\"attackerCount\": 5"), out invalid, out error),
                "Mismatched decoyCount + attackerCount != agentCount must fail");
        }

        private static void TestAllFourScenarioFilesAndSchemaValidation()
        {
            string dir = Path.Combine("Assets", "StreamingAssets", "scenarios");

            // 1. Punjab Border — Zero Visibility Pincer
            string punjabPath = Path.Combine(dir, "punjab-fog-pincer-01.json");
            Assert(File.Exists(punjabPath), "punjab-fog-pincer-01.json must exist");
            ScenarioConfig punjab = ScenarioConfig.Parse(File.ReadAllText(punjabPath, Encoding.UTF8));
            Assert(punjab.id == "punjab-fog-pincer-01", "Expected punjab id");
            Assert(punjab.region == ScenarioConfig.REGION_PUNJAB_RIVERINE, "Expected PUNJAB_RIVERINE");
            Assert(punjab.weatherPreset.isNight && Math.Abs(punjab.weatherPreset.visibility - 15.0f) < 0.01f, "Expected night fog");
            Assert(punjab.swarmProfile.tacticTree == SwarmProfile.TACTIC_PINCER_LOW_FAST, "Expected PINCER_LOW_FAST");
            Assert(punjab.swarmProfile.agentCount == 6 && punjab.swarmProfile.decoyCount == 2 && punjab.swarmProfile.attackerCount == 4, "Expected 6 agents (2+4)");
            Assert(Math.Abs(punjab.swarmProfile.speedKmh - 120.0f) < 0.01f, "Expected 120 km/h speed");
            Assert(Math.Abs(punjab.swarmProfile.altitudeM - 8.0f) < 0.01f, "Expected 8m altitude");

            // 2. Ladakh LAC Ridge — High-Altitude Saturation
            string ladakhPath = Path.Combine(dir, "ladakh-lac-ridge-01.json");
            Assert(File.Exists(ladakhPath), "ladakh-lac-ridge-01.json must exist");
            ScenarioConfig ladakh = ScenarioConfig.Parse(File.ReadAllText(ladakhPath, Encoding.UTF8));
            Assert(ladakh.id == "ladakh-lac-ridge-01", "Expected ladakh id");
            Assert(ladakh.name == "Ladakh LAC Ridge \u2014 High-Altitude Saturation", "Expected Ladakh English name");
            Assert(ladakh.nameHindi == "\u0932\u0926\u094D\u0926\u093E\u0916 \u090F\u0932\u090F\u0938\u0940 \u0930\u093F\u091C \u2014 \u0909\u091a\u094D\u091a \u090a\u0902\u091a\u093E\u0908 \u0938\u0902\u0924\u0943\u092A\u094D\u0924\u093F", "Expected Ladakh Hindi name");
            Assert(ladakh.region == ScenarioConfig.REGION_LADAKH_LAC, "Expected LADAKH_LAC region");
            Assert(Math.Abs(ladakh.weatherPreset.visibility - 200.0f) < 0.01f, "Expected Ladakh 200m visibility");
            Assert(Math.Abs(ladakh.weatherPreset.temperature - (-12.0f)) < 0.01f, "Expected Ladakh -12C temperature");
            Assert(!ladakh.weatherPreset.isNight, "Expected day scenario for Ladakh");
            Assert(ladakh.swarmProfile.agentCount == 8 && ladakh.swarmProfile.decoyCount == 4 && ladakh.swarmProfile.attackerCount == 4, "Expected 8 agents (4+4) for Ladakh");
            Assert(ladakh.swarmProfile.tacticTree == SwarmProfile.TACTIC_SATURATION_DIVE, "Expected SATURATION_DIVE for Ladakh");
            Assert(Math.Abs(ladakh.swarmProfile.speedKmh - 100.0f) < 0.01f, "Expected 100 km/h speed for Ladakh");
            Assert(Math.Abs(ladakh.swarmProfile.altitudeM - 3800.0f) < 0.01f, "Expected 3800m altitude for Ladakh");
            Assert(ladakh.swarmProfile.rfFreqGHz != null && ladakh.swarmProfile.rfFreqGHz.Length == 2, "Expected 2 RF freqs for Ladakh");

            // 3. LoC Mountain Pass — Multi-Axis Split
            string locPath = Path.Combine(dir, "loc-mountain-pass-01.json");
            Assert(File.Exists(locPath), "loc-mountain-pass-01.json must exist");
            ScenarioConfig loc = ScenarioConfig.Parse(File.ReadAllText(locPath, Encoding.UTF8));
            Assert(loc.id == "loc-mountain-pass-01", "Expected loc id");
            Assert(loc.name == "LoC Mountain Pass \u2014 Multi-Axis Split", "Expected LoC English name");
            Assert(loc.nameHindi == "\u090F\u0932\u0913\u0938\u0940 \u092A\u0930\u094D\u0935\u0924\u0940\u092F \u0926\u0930\u094D\u0930\u093E \u2014 \u092C\u0939\u0941-\u0905\u0915\u094D\u0937 \u0935\u093F\u092D\u093E\u091C\u0928", "Expected LoC Hindi name");
            Assert(loc.region == ScenarioConfig.REGION_LOC_MOUNTAIN, "Expected LOC_MOUNTAIN region");
            Assert(Math.Abs(loc.weatherPreset.visibility - 80.0f) < 0.01f, "Expected LoC 80m visibility");
            Assert(Math.Abs(loc.weatherPreset.windSpeedKmh - 35.0f) < 0.01f, "Expected crosswind 35 km/h for LoC");
            Assert(loc.weatherPreset.isNight, "Expected night scenario for LoC");
            Assert(loc.swarmProfile.agentCount == 5 && loc.swarmProfile.decoyCount == 1 && loc.swarmProfile.attackerCount == 4, "Expected 5 agents (1+4) for LoC");
            Assert(loc.swarmProfile.tacticTree == SwarmProfile.TACTIC_MULTI_AXIS_SPLIT, "Expected MULTI-AXIS_SPLIT for LoC");
            Assert(Math.Abs(loc.swarmProfile.speedKmh - 80.0f) < 0.01f, "Expected 80 km/h speed for LoC");
            Assert(loc.swarmProfile.rfFreqGHz != null && loc.swarmProfile.rfFreqGHz.Length == 1 && Math.Abs(loc.swarmProfile.rfFreqGHz[0] - 0.915f) < 0.001f,
                "Expected 915 MHz ELRS only for LoC");

            // 4. Desert FOB — Dust Storm Saturation Dive
            string desertPath = Path.Combine(dir, "desert-fob-01.json");
            Assert(File.Exists(desertPath), "desert-fob-01.json must exist");
            ScenarioConfig desert = ScenarioConfig.Parse(File.ReadAllText(desertPath, Encoding.UTF8));
            Assert(desert.id == "desert-fob-01", "Expected desert id");
            Assert(desert.name == "Desert FOB \u2014 Dust Storm Saturation Dive", "Expected Desert English name");
            Assert(desert.nameHindi == "\u0930\u0947\u0917\u093F\u0938\u094D\u0924\u093E\u0928\u0940 \u090F\u092B\u0913\u092C\u0940 \u2014 \u0927\u0942\u0932 \u092D\u0930\u0940 \u0906\u0902\u0927\u0940 \u0939\u092E\u0932\u093E", "Expected Desert Hindi name");
            Assert(desert.region == ScenarioConfig.REGION_DESERT_FOB, "Expected DESERT_FOB region");
            Assert(Math.Abs(desert.weatherPreset.visibility - 5000.0f) < 0.01f, "Expected Desert 5000m visibility");
            Assert(Math.Abs(desert.weatherPreset.windSpeedKmh - 55.0f) < 0.01f, "Expected dust storm wind 55 km/h for Desert");
            Assert(Math.Abs(desert.weatherPreset.temperature - 38.0f) < 0.01f, "Expected 38C temperature for Desert");
            Assert(!desert.weatherPreset.isNight, "Expected day scenario for Desert");
            Assert(desert.swarmProfile.agentCount == 8 && desert.swarmProfile.decoyCount == 2 && desert.swarmProfile.attackerCount == 6, "Expected 8 agents (2+6) for Desert");
            Assert(desert.swarmProfile.tacticTree == SwarmProfile.TACTIC_SATURATION_DIVE, "Expected SATURATION_DIVE for Desert");
            Assert(Math.Abs(desert.swarmProfile.speedKmh - 110.0f) < 0.01f, "Expected 110 km/h speed for Desert");
            Assert(Math.Abs(desert.swarmProfile.altitudeM - 15.0f) < 0.01f, "Expected 15m altitude for Desert");
        }

        private static void TestScenarioLibraryLoadAllAndSeedRegeneration()
        {
            UnityEngine.Random.InitState(12345);
            List<ScenarioConfig> run1 = ScenarioLibrary.LoadAll();
            Assert(run1.Count == 4, "ScenarioLibrary.LoadAll() must load 4 scenario JSON files, got " + run1.Count);
            Assert(run1[0].id == "punjab-fog-pincer-01", "Primary scenario punjab-fog-pincer-01 must be first in library");

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < run1.Count; i++)
            {
                ScenarioConfig cfg = run1[i];
                ids.Add(cfg.id);
                Assert(cfg.seed >= ScenarioLibrary.MinProceduralSeed && cfg.seed < ScenarioLibrary.MaxProceduralSeed,
                    "Regenerated seed must be in [10000, 99999), got " + cfg.seed);
            }

            Assert(ids.Contains("punjab-fog-pincer-01"), "Must contain punjab-fog-pincer-01");
            Assert(ids.Contains("ladakh-lac-ridge-01"), "Must contain ladakh-lac-ridge-01");
            Assert(ids.Contains("loc-mountain-pass-01"), "Must contain loc-mountain-pass-01");
            Assert(ids.Contains("desert-fob-01"), "Must contain desert-fob-01");

            List<ScenarioConfig> run2 = ScenarioLibrary.LoadAll();
            Assert(run2.Count == 4, "Second LoadAll() must also return 4 scenarios");
            Assert(run1[0].seed != run2[0].seed, "Procedural seed must regenerate across runs");
        }

        private static void TestSceneAndMetaGuidWiring()
        {
            string c2Scene = File.ReadAllText("Assets/Scenes/InstructorC2.unity", Encoding.UTF8);
            string cockpitScene = File.ReadAllText("Assets/Scenes/TraineeCockpit.unity", Encoding.UTF8);
            string configMeta = File.ReadAllText("Assets/Scripts/Scenarios/ScenarioConfig.cs.meta", Encoding.UTF8);
            string libraryMeta = File.ReadAllText("Assets/Scripts/Scenarios/ScenarioLibrary.cs.meta", Encoding.UTF8);
            string managerMeta = File.ReadAllText("Assets/Scripts/Scenarios/ScenarioManager.cs.meta", Encoding.UTF8);

            Assert(configMeta.Contains("guid: a1b2c3d4e5f647898a1b2c3d4e5f6010"), "ScenarioConfig.cs.meta GUID mismatch");
            Assert(libraryMeta.Contains("guid: a1b2c3d4e5f647898a1b2c3d4e5f6011"), "ScenarioLibrary.cs.meta GUID mismatch");
            Assert(managerMeta.Contains("guid: a1b2c3d4e5f647898a1b2c3d4e5f6012"), "ScenarioManager.cs.meta GUID mismatch");

            Assert(c2Scene.Contains("guid: a1b2c3d4e5f647898a1b2c3d4e5f6012"), "InstructorC2.unity must reference ScenarioManager GUID");
            Assert(c2Scene.Contains("m_Name: ScenarioDropdown"), "InstructorC2.unity must contain ScenarioDropdown");
            Assert(c2Scene.Contains("m_Name: LaunchButton"), "InstructorC2.unity must contain LaunchButton");
            Assert(c2Scene.Contains("m_MethodName: LaunchSelectedScenario"), "LaunchButton must wire LaunchSelectedScenario");
            Assert(c2Scene.Contains("m_Name: AbortButton"), "InstructorC2.unity must contain AbortButton");
            Assert(c2Scene.Contains("abortButton: {fileID: 430000003}"), "ScenarioManager must wire abortButton in InstructorC2.unity");
            Assert(c2Scene.Contains("m_MethodName: BroadcastScenarioEnd"), "AbortButton must wire BroadcastScenarioEnd");
            Assert(c2Scene.Contains("m_StringArgument: INSTRUCTOR_ABORT"), "AbortButton must pass INSTRUCTOR_ABORT");

            Assert(cockpitScene.Contains("guid: a1b2c3d4e5f647898a1b2c3d4e5f6012"), "TraineeCockpit.unity must reference ScenarioManager GUID");
        }

        private static void TestScenarioTickAndEndPayloadContracts()
        {
            // Verify P0 default stub SCENARIO_TICK payload shape
            ScenarioTickPayload stubTick = new ScenarioTickPayload
            {
                sessionId = "sess-20261004-143201",
                t = 100
            };

            JsonEnvelope stubEnv = ScenarioTickPayload.CreateEnvelope(stubTick, 1759600021100);
            Assert(stubEnv.type == JsonEnvelope.SCENARIO_TICK, "Expected envelope type SCENARIO_TICK");
            ScenarioTickPayload parsedStub = ScenarioTickPayload.Parse(stubEnv.payload);
            Assert(parsedStub.sessionId == "sess-20261004-143201", "Expected sessionId preserved");
            Assert(parsedStub.t == 100, "Expected t == 100");
            Assert(parsedStub.swarmSnapshot != null && parsedStub.swarmSnapshot.Length == 0, "Expected empty stub swarmSnapshot=[]");
            Assert(parsedStub.sensorReadings != null, "Expected non-null sensorReadings");
            Assert(Math.Abs(parsedStub.sensorReadings.rfWaterfallPeakGHz) < 0.0001f &&
                   Math.Abs(parsedStub.sensorReadings.rfWaterfallPeakDbm) < 0.0001f &&
                   Math.Abs(parsedStub.sensorReadings.dopplerPeakHz) < 0.0001f &&
                   Math.Abs(parsedStub.sensorReadings.acousticSplDb) < 0.0001f &&
                   parsedStub.sensorReadings.radarDetectedCount == 0, "Expected stub sensorReadings all zeros");
            Assert(parsedStub.threatState == ScenarioTickPayload.THREAT_GREEN, "Expected stub threatState == GREEN");
            Assert(!parsedStub.friendlyVhfActive, "Expected stub friendlyVhfActive == false");
            Assert(parsedStub.traineeAction != null && !parsedStub.traineeAction.ewEngaged &&
                   !parsedStub.traineeAction.ewBandGHz.HasValue && parsedStub.traineeAction.kinetic == null,
                   "Expected default stub traineeAction");
            Assert(parsedStub.ewJammerState != null && !parsedStub.ewJammerState.engaged &&
                   !parsedStub.ewJammerState.friendlyBlackoutRisk, "Expected default stub ewJammerState");

            // Verify full SCENARIO_TICK example from board/contracts/scenario-events.md
            ScenarioTickPayload richTick = new ScenarioTickPayload
            {
                sessionId = "sess-20261004-143201",
                t = 12300,
                swarmSnapshot = new SwarmAgentState[]
                {
                    new SwarmAgentState
                    {
                        agentId = "agent-0",
                        isDecoy = true,
                        positionWorld = new Vector3(200.0f, 10.0f, -100.0f),
                        velocityMs = new Vector3(-28.0f, 0.0f, 10.0f),
                        headingDeg = 250.0f,
                        altitudeM = 10.0f,
                        rfEmitting = true,
                        rfFreqGHz = 2.4f,
                        rfPowerDbm = 20.0f,
                        propellerRpm = 9800,
                        rcs = 0.08f,
                        isNeutralised = false,
                        neutralisedAtMs = null
                    },
                    new SwarmAgentState
                    {
                        agentId = "agent-3",
                        isDecoy = false,
                        positionWorld = new Vector3(142.3f, 8.3f, -87.1f),
                        velocityMs = new Vector3(-28.4f, -0.2f, 12.1f),
                        headingDeg = 247.0f,
                        altitudeM = 8.3f,
                        rfEmitting = true,
                        rfFreqGHz = 5.825f,
                        rfPowerDbm = 23.0f,
                        propellerRpm = 11400,
                        rcs = 0.012f,
                        isNeutralised = false,
                        neutralisedAtMs = null
                    }
                },
                sensorReadings = new SensorReadings
                {
                    rfWaterfallPeakGHz = 5.825f,
                    rfWaterfallPeakDbm = -62.3f,
                    dopplerPeakHz = 220.4f,
                    acousticSplDb = 54.2f,
                    radarDetectedCount = 4
                },
                threatState = ScenarioTickPayload.THREAT_RED,
                traineeAction = new TraineeAction
                {
                    antennaBearingDeg = 247.0f,
                    antennaElevationDeg = -5.0f,
                    ewEngaged = true,
                    ewBandGHz = 5.825f,
                    kinetic = new KineticEvent
                    {
                        firedAtMs = 12300,
                        targetAgentId = "agent-3",
                        hit = true,
                        leadAngleDeg = 3.2f
                    }
                },
                ewJammerState = new EWJammerState
                {
                    engaged = true,
                    bearingDeg = 247.0f,
                    elevationDeg = -5.0f,
                    bandGHz = 5.825f,
                    friendlyBlackoutRisk = false
                },
                friendlyVhfActive = false
            };

            string richTickJson = richTick.ToJson();
            ScenarioTickPayload roundTripTick = ScenarioTickPayload.Parse(richTickJson);
            Assert(roundTripTick.t == 12300 && roundTripTick.swarmSnapshot.Length == 2, "Expected 2 agents in round-trip SCENARIO_TICK");
            Assert(roundTripTick.swarmSnapshot[1].agentId == "agent-3" &&
                   Math.Abs(roundTripTick.swarmSnapshot[1].positionWorld.x - 142.3f) < 0.001f &&
                   Math.Abs(roundTripTick.swarmSnapshot[1].propellerRpm - 11400f) < 0.001f,
                   "Expected agent-3 fields preserved in round-trip");
            Assert(roundTripTick.traineeAction.kinetic != null &&
                   roundTripTick.traineeAction.kinetic.targetAgentId == "agent-3" &&
                   roundTripTick.traineeAction.kinetic.hit, "Expected kinetic event preserved");

            // Verify SCENARIO_END payload round-trip and validation
            JsonEnvelope endEnv = ScenarioEndPayload.CreateEnvelope(
                "sess-20261004-143201",
                55000,
                ScenarioEndPayload.REASON_ALL_NEUTRALISED,
                new SwarmAgentState[]
                {
                    new SwarmAgentState
                    {
                        agentId = "agent-3",
                        isDecoy = false,
                        isNeutralised = true,
                        neutralisedAtMs = 45210,
                        positionWorld = new Vector3(12.0f, 6.1f, -3.4f),
                        velocityMs = Vector3.zero,
                        headingDeg = 247.0f,
                        altitudeM = 6.1f,
                        rfEmitting = false,
                        rfFreqGHz = 5.825f,
                        rfPowerDbm = 0f,
                        propellerRpm = 0f,
                        rcs = 0.012f
                    }
                });
            Assert(endEnv.type == JsonEnvelope.SCENARIO_END, "Expected envelope type SCENARIO_END");
            ScenarioEndPayload parsedEnd = ScenarioEndPayload.Parse(endEnv.payload);
            Assert(parsedEnd.reason == ScenarioEndPayload.REASON_ALL_NEUTRALISED, "Expected reason ALL_NEUTRALISED");
            Assert(parsedEnd.t == 55000, "Expected t == 55000");
            Assert(parsedEnd.finalSwarmSnapshot.Length == 1 && parsedEnd.finalSwarmSnapshot[0].neutralisedAtMs == 45210,
                "Expected finalSwarmSnapshot agent neutralisedAtMs == 45210");

            ScenarioTickPayload invalidTick;
            ScenarioEndPayload invalidEnd;
            string err;
            Assert(!ScenarioTickPayload.TryParse(richTickJson.Replace("\"RED\"", "\"PURPLE\""), out invalidTick, out err),
                "Unsupported threatState must fail ScenarioTickPayload.TryParse");
            Assert(!ScenarioEndPayload.TryParse(endEnv.payload.Replace("\"ALL_NEUTRALISED\"", "\"UNKNOWN_REASON\""), out invalidEnd, out err),
                "Unsupported reason must fail ScenarioEndPayload.TryParse");
        }

        private static async Task TestScenarioLaunchBroadcastSceneLoadAndErrorsAsync()
        {
            List<string> logs = new List<string>();
            object logLock = new object();

#if !UNITY_5_3_OR_NEWER
            Action<string> onLog = msg => { lock (logLock) { logs.Add(msg); } };
            Debug.OnLog += onLog;
#endif
            SceneLoader.ResetStats();

            WsServer server = new WsServer();
            server.Port = WsServer.DefaultPort;
            server.HeartbeatIntervalMs = 5000;
            server.StaleThresholdMs = 15000;

            WsClient client = new WsClient();
            client.ServerUri = WsClient.DefaultServerUri;
            client.HeartbeatIntervalMs = 5000;
            client.StaleThresholdMs = 15000;

            Dropdown dropdown = CreateDropdown("ScenarioDropdown");
            Button launchBtn = CreateButton("LaunchBtn");
            GameObject cockpitRoot = new GameObject("TraineeCockpitRoot");
            cockpitRoot.SetActive(false);

            ScenarioManager instructorMgr = new ScenarioManager();
            instructorMgr.Server = server;
            instructorMgr.Client = null;
            instructorMgr.ScenarioDropdown = dropdown;
            instructorMgr.LaunchButton = launchBtn;
            instructorMgr.LoadSceneOnScenarioStart = false;

            ScenarioManager traineeMgr = new ScenarioManager();
            traineeMgr.Server = null;
            traineeMgr.Client = client;
            traineeMgr.TraineeCockpitRoot = cockpitRoot;
            traineeMgr.TraineeCockpitSceneName = "TraineeCockpit";
            traineeMgr.LoadSceneOnScenarioStart = true;

            try
            {
                TaskCompletionSource<bool> linkConfirmedTcs = new TaskCompletionSource<bool>();
                TaskCompletionSource<ScenarioStartPayload> traineeReceivedStartTcs = new TaskCompletionSource<ScenarioStartPayload>();
                Queue<JsonEnvelope> serverErrorsReceived = new Queue<JsonEnvelope>();
                object errLock = new object();
                SemaphoreSlim errSignal = new SemaphoreSlim(0, 10);

                client.OnLinkConfirmed += () => linkConfirmedTcs.TrySetResult(true);
                traineeMgr.OnScenarioStartReceived += payload => traineeReceivedStartTcs.TrySetResult(payload);
                server.OnMessageReceived += env =>
                {
                    if (string.Equals(env.type, JsonEnvelope.ERROR, StringComparison.Ordinal))
                    {
                        lock (errLock)
                        {
                            serverErrorsReceived.Enqueue(env);
                        }
                        errSignal.Release();
                    }
                };

                Assert(server.StartServer(), "WsServer must bind on port 9700");
                client.StartClient();

                bool connected = await WaitTaskAsync(linkConfirmedTcs.Task, 3000);
                Assert(connected, "WsClient must connect and confirm link before scenario launch");

                // Start both ScenarioManagers
                instructorMgr.Start();
                traineeMgr.Start();

                Assert(dropdown.options.Count == 4, "Dropdown must be populated with 4 scenarios");
                Assert(instructorMgr.SelectScenarioByName("Punjab Border \u2014 Zero Visibility Pincer"),
                    "Must select 'Punjab Border — Zero Visibility Pincer' in dropdown");

                long launchWallMs = JsonEnvelope.NowUnixMs();
                // Click Launch button on InstructorC2 UI
                launchBtn.onClick.Invoke();

                bool received = await WaitTaskAsync(traineeReceivedStartTcs.Task, 2000);
                Assert(received, "Trainee ScenarioManager must receive SCENARIO_START");

                long deliveryLatencyMs = JsonEnvelope.NowUnixMs() - launchWallMs;
                Assert(deliveryLatencyMs <= 200, "SCENARIO_START delivery latency must be <= 200ms (got " + deliveryLatencyMs + "ms)");

                ScenarioStartPayload startPayload = traineeReceivedStartTcs.Task.Result;
                Assert(Regex.IsMatch(startPayload.sessionId, @"^sess-\d{8}-\d{6}$"),
                    "sessionId must match sess-YYYYMMDD-HHMMSS format, got: " + startPayload.sessionId);
                Assert(startPayload.scenarioId == "punjab-fog-pincer-01", "Expected scenarioId punjab-fog-pincer-01");
                Assert(startPayload.seed == startPayload.scenarioConfig.seed, "Payload seed must match scenarioConfig.seed");
                Assert(startPayload.seed >= 10000 && startPayload.seed < 99999, "Seed must be in [10000, 99999)");

                // Verify exact console output required by US-P0-B TEST
                string expectedLog = "[WsClient] SCENARIO_START received \u2014 sessionId: " + startPayload.sessionId;
                bool foundExpectedLog = false;
                lock (logLock)
                {
                    for (int i = 0; i < logs.Count; i++)
                    {
                        if (logs[i].Contains(expectedLog))
                        {
                            foundExpectedLog = true;
                            break;
                        }
                    }
                }
                Assert(foundExpectedLog, "Console must contain exact log: " + expectedLog);

                // Verify TraineeCockpit scene load was triggered
                Assert(SceneLoader.LastRequestedScene == "TraineeCockpit", "TraineeCockpit scene must load on SCENARIO_START");
                Assert(SceneLoader.LoadRequestCount == 1, "SceneLoader.LoadAsync must be called once");
                Assert(cockpitRoot.activeSelf, "TraineeCockpitRoot must be activated");

                // Wait briefly for the Trainee's immediate ACK heartbeat to arrive at WsServer
                await Task.Delay(100);
                Assert(instructorMgr.LastScenarioStartAcked, "Server must record Trainee ACK within 1s of SCENARIO_START");

                // Test contract error: second SCENARIO_START mid-run -> SCENARIO_ALREADY_RUNNING (fatal: false)
                launchBtn.onClick.Invoke();
                bool gotAlreadyRunningErr = await errSignal.WaitAsync(2000);
                Assert(gotAlreadyRunningErr, "Second SCENARIO_START mid-run must emit ERROR to server");
                JsonEnvelope alreadyRunningEnv;
                lock (errLock) { alreadyRunningEnv = serverErrorsReceived.Dequeue(); }
                Assert(alreadyRunningEnv.GetPayloadString("code") == JsonEnvelope.ERR_SCENARIO_ALREADY_RUNNING,
                    "Expected ERROR code SCENARIO_ALREADY_RUNNING");
                bool fatalFlag;
                Assert(alreadyRunningEnv.TryGetPayloadBool("fatal", out fatalFlag) && !fatalFlag,
                    "SCENARIO_ALREADY_RUNNING must have fatal == false");

                // Reset Trainee state and test SCENARIO_NOT_FOUND (fatal: true)
                traineeMgr.ResetScenarioState();
                ScenarioConfig unknownConfig = startPayload.scenarioConfig.Clone();
                unknownConfig.id = "non-existent-scenario-99";
                await instructorMgr.LaunchScenarioAsync(unknownConfig);

                bool gotNotFoundErr = await errSignal.WaitAsync(2000);
                Assert(gotNotFoundErr, "Unknown scenarioId must emit SCENARIO_NOT_FOUND ERROR");
                JsonEnvelope notFoundEnv;
                lock (errLock) { notFoundEnv = serverErrorsReceived.Dequeue(); }
                Assert(notFoundEnv.GetPayloadString("code") == JsonEnvelope.ERR_SCENARIO_NOT_FOUND,
                    "Expected ERROR code SCENARIO_NOT_FOUND");
                Assert(notFoundEnv.TryGetPayloadBool("fatal", out fatalFlag) && fatalFlag,
                    "SCENARIO_NOT_FOUND must have fatal == true");

                // Test 1-second ACK timeout warning when Trainee does not ACK SCENARIO_START
                List<string> warnLogs = new List<string>();
#if !UNITY_5_3_OR_NEWER
                Action<string> onWarn = msg => { lock (logLock) { warnLogs.Add(msg); } };
                Debug.OnLogWarning += onWarn;
#endif
                try
                {
                    client.Stop();
                    instructorMgr.AckTimeoutMs = 100;
                    await instructorMgr.LaunchScenarioAsync(startPayload.scenarioConfig);
                    await Task.Delay(220);

                    Assert(!instructorMgr.LastScenarioStartAcked, "Expected LastScenarioStartAcked == false when client is disconnected");
                    bool foundAckTimeoutWarn = false;
                    lock (logLock)
                    {
                        for (int i = 0; i < warnLogs.Count; i++)
                        {
                            if (warnLogs[i].Contains("did not ACK SCENARIO_START"))
                            {
                                foundAckTimeoutWarn = true;
                                break;
                            }
                        }
                    }
                    Assert(foundAckTimeoutWarn, "Server must log warning if Trainee does not ACK within timeout");
                }
                finally
                {
#if !UNITY_5_3_OR_NEWER
                    Debug.OnLogWarning -= onWarn;
#endif
                }
            }
            finally
            {
#if !UNITY_5_3_OR_NEWER
                Debug.OnLog -= onLog;
#endif
                traineeMgr.OnDestroy();
                instructorMgr.OnDestroy();
                client.Stop();
                server.Stop();
            }
        }

        private static async Task TestScenarioTickLoopAbortAndTerminalConditionsAsync()
        {
            List<string> logs = new List<string>();
            object logLock = new object();

#if !UNITY_5_3_OR_NEWER
            Action<string> onLog = msg => { lock (logLock) { logs.Add(msg); } };
            Debug.OnLog += onLog;
#endif

            WsServer server = new WsServer();
            server.Port = WsServer.DefaultPort;
            server.HeartbeatIntervalMs = 5000;
            server.StaleThresholdMs = 15000;

            WsClient client = new WsClient();
            client.ServerUri = WsClient.DefaultServerUri;
            client.HeartbeatIntervalMs = 5000;
            client.StaleThresholdMs = 15000;

            Dropdown dropdown = CreateDropdown("ScenarioDropdown");
            Button launchBtn = CreateButton("LaunchBtn");
            Button abortBtn = CreateButton("AbortBtn");

            ScenarioManager instructorMgr = new ScenarioManager();
            instructorMgr.Server = server;
            instructorMgr.Client = null;
            instructorMgr.ScenarioDropdown = dropdown;
            instructorMgr.LaunchButton = launchBtn;
            instructorMgr.AbortButton = abortBtn;
            instructorMgr.LoadSceneOnScenarioStart = false;
            instructorMgr.TickIntervalSeconds = 0.1f;
            instructorMgr.TickStepMs = 100;
            instructorMgr.AutoStartTickLoop = true;

            ScenarioManager traineeMgr = new ScenarioManager();
            traineeMgr.Server = null;
            traineeMgr.Client = client;
            traineeMgr.LoadSceneOnScenarioStart = false;

            try
            {
                TaskCompletionSource<bool> linkConfirmedTcs = new TaskCompletionSource<bool>();
                List<ScenarioTickPayload> receivedTicks = new List<ScenarioTickPayload>();
                object tickLock = new object();
                TaskCompletionSource<bool> threeTicksTcs = new TaskCompletionSource<bool>();
                TaskCompletionSource<ScenarioTickPayload> injectedTickTcs = new TaskCompletionSource<ScenarioTickPayload>();
                TaskCompletionSource<ScenarioEndPayload> abortEndTcs = new TaskCompletionSource<ScenarioEndPayload>();

                client.OnLinkConfirmed += () => linkConfirmedTcs.TrySetResult(true);
                traineeMgr.OnScenarioTickReceived += tick =>
                {
                    lock (tickLock)
                    {
                        receivedTicks.Add(tick);
                        if (receivedTicks.Count >= 3)
                            threeTicksTcs.TrySetResult(true);
                        if (tick.swarmSnapshot != null && tick.swarmSnapshot.Length == 2)
                            injectedTickTcs.TrySetResult(tick);
                    }
                };
                traineeMgr.OnScenarioEndReceived += endPayload => abortEndTcs.TrySetResult(endPayload);

                Assert(server.StartServer(), "WsServer must bind on port 9700 for US-P0-C test");
                client.StartClient();

                Assert(await WaitTaskAsync(linkConfirmedTcs.Task, 3000), "WsClient must connect before US-P0-C test");

                instructorMgr.Start();
                traineeMgr.Start();

                // 1. Launch scenario -> TickLoop runs at 10 Hz (0.1s) with stub data
                launchBtn.onClick.Invoke();
                Assert(await WaitTaskAsync(threeTicksTcs.Task, 1500), "Trainee must receive >=3 SCENARIO_TICK messages at ~10 Hz");

                ScenarioTickPayload firstTick;
                ScenarioTickPayload secondTick;
                ScenarioTickPayload thirdTick;
                lock (tickLock)
                {
                    firstTick = receivedTicks[0];
                    secondTick = receivedTicks[1];
                    thirdTick = receivedTicks[2];
                }

                Assert(firstTick.t == 100 && secondTick.t == 200 && thirdTick.t == 300,
                    "Expected t to increment by 100ms per tick (100, 200, 300), got: " +
                    firstTick.t + ", " + secondTick.t + ", " + thirdTick.t);
                Assert(firstTick.swarmSnapshot != null && firstTick.swarmSnapshot.Length == 0,
                    "Expected P0 stub swarmSnapshot=[]");
                Assert(Math.Abs(firstTick.sensorReadings.rfWaterfallPeakGHz) < 0.001f &&
                       Math.Abs(firstTick.sensorReadings.rfWaterfallPeakDbm) < 0.001f &&
                       Math.Abs(firstTick.sensorReadings.dopplerPeakHz) < 0.001f &&
                       Math.Abs(firstTick.sensorReadings.acousticSplDb) < 0.001f &&
                       firstTick.sensorReadings.radarDetectedCount == 0,
                    "Expected P0 stub sensorReadings all zeros");
                Assert(firstTick.threatState == ScenarioTickPayload.THREAT_GREEN,
                    "Expected P0 stub threatState == GREEN");

                // 2. InjectSwarmSnapshot(SwarmAgentState[]) -> appears in next SCENARIO_TICK
                SwarmAgentState[] injectedAgents = new SwarmAgentState[]
                {
                    new SwarmAgentState
                    {
                        agentId = "agent-0",
                        isDecoy = true,
                        positionWorld = new Vector3(180f, 10f, -90f),
                        velocityMs = new Vector3(-25f, 0f, 10f),
                        headingDeg = 250f,
                        altitudeM = 10f,
                        rfEmitting = true,
                        rfFreqGHz = 2.4f,
                        rfPowerDbm = 20f,
                        propellerRpm = 9800f,
                        rcs = 0.08f,
                        isNeutralised = false,
                        neutralisedAtMs = null
                    },
                    new SwarmAgentState
                    {
                        agentId = "agent-1",
                        isDecoy = false,
                        positionWorld = new Vector3(140f, 8f, -75f),
                        velocityMs = new Vector3(-28f, 0f, 12f),
                        headingDeg = 247f,
                        altitudeM = 8f,
                        rfEmitting = true,
                        rfFreqGHz = 5.825f,
                        rfPowerDbm = 23f,
                        propellerRpm = 11400f,
                        rcs = 0.012f,
                        isNeutralised = false,
                        neutralisedAtMs = null
                    }
                };
                instructorMgr.InjectSwarmSnapshot(injectedAgents);

                Assert(await WaitTaskAsync(injectedTickTcs.Task, 1000),
                    "Next SCENARIO_TICK must include injected SwarmAgentState[]");
                ScenarioTickPayload injectedTick = injectedTickTcs.Task.Result;
                Assert(injectedTick.swarmSnapshot.Length == 2 && injectedTick.swarmSnapshot[1].agentId == "agent-1",
                    "Expected injected agent-1 in SCENARIO_TICK");

                // 3. Click Abort button -> BroadcastScenarioEnd("INSTRUCTOR_ABORT"), stops TickLoop
                abortBtn.onClick.Invoke();
                Assert(await WaitTaskAsync(abortEndTcs.Task, 1500),
                    "Clicking Abort button must broadcast SCENARIO_END to Trainee");
                ScenarioEndPayload abortEnd = abortEndTcs.Task.Result;
                Assert(abortEnd.reason == ScenarioEndPayload.REASON_INSTRUCTOR_ABORT,
                    "Expected SCENARIO_END reason INSTRUCTOR_ABORT, got " + abortEnd.reason);
                Assert(abortEnd.t >= injectedTick.t, "Expected SCENARIO_END t >= last tick t");
                Assert(abortEnd.finalSwarmSnapshot != null && abortEnd.finalSwarmSnapshot.Length == 2,
                    "Expected finalSwarmSnapshot in SCENARIO_END to carry last injected agents");
                Assert(!instructorMgr.IsScenarioRunning && !traineeMgr.IsScenarioRunning,
                    "Both Instructor and Trainee ScenarioManagers must stop running after SCENARIO_END");

                int ticksAtEnd;
                lock (tickLock) { ticksAtEnd = receivedTicks.Count; }
                await Task.Delay(250);
                int ticksAfterWait;
                lock (tickLock) { ticksAfterWait = receivedTicks.Count; }
                Assert(ticksAfterWait == ticksAtEnd,
                    "TickLoop must stop after SCENARIO_END (expected " + ticksAtEnd + " ticks, got " + ticksAfterWait + ")");

                // Verify Console logs contain SCENARIO_TICK and SCENARIO_END
                bool sawTickLog = false;
                bool sawEndLog = false;
                lock (logLock)
                {
                    for (int i = 0; i < logs.Count; i++)
                    {
                        if (logs[i].Contains("SCENARIO_TICK"))
                            sawTickLog = true;
                        if (logs[i].Contains("SCENARIO_END") && logs[i].Contains("INSTRUCTOR_ABORT"))
                            sawEndLog = true;
                    }
                }
                Assert(sawTickLog, "Console must show SCENARIO_TICK logs");
                Assert(sawEndLog, "Console must show SCENARIO_END broadcast with INSTRUCTOR_ABORT");

                // 4. Test TIME_LIMIT terminal condition: t >= durationMaxSecs * 1000 -> SCENARIO_END ("TIME_LIMIT")
                TaskCompletionSource<ScenarioEndPayload> timeLimitEndTcs = new TaskCompletionSource<ScenarioEndPayload>();
                Action<ScenarioEndPayload> timeLimitHandler = p => timeLimitEndTcs.TrySetResult(p);
                traineeMgr.OnScenarioEndReceived += timeLimitHandler;
                try
                {
                    ScenarioConfig shortConfig = instructorMgr.Scenarios[0].Clone();
                    shortConfig.durationMaxSecs = 1; // 1000 ms (10 ticks of 100 ms)
                    instructorMgr.TickIntervalSeconds = 0.02f; // fast tick for test
                    instructorMgr.TickStepMs = 250; // 4 ticks -> 1000 ms

                    await instructorMgr.LaunchScenarioAsync(shortConfig);
                    Assert(await WaitTaskAsync(timeLimitEndTcs.Task, 2000),
                        "TickLoop must fire SCENARIO_END when t >= durationMaxSecs * 1000");
                    ScenarioEndPayload timeLimitEnd = timeLimitEndTcs.Task.Result;
                    Assert(timeLimitEnd.reason == ScenarioEndPayload.REASON_TIME_LIMIT,
                        "Expected TIME_LIMIT reason, got " + timeLimitEnd.reason);
                    Assert(timeLimitEnd.t == 1000, "Expected TIME_LIMIT at t == 1000ms, got " + timeLimitEnd.t);
                }
                finally
                {
                    traineeMgr.OnScenarioEndReceived -= timeLimitHandler;
                }

                // 5. Test ALL_NEUTRALISED and EXCLUSION_BREACH terminal conditions
                instructorMgr.AutoStartTickLoop = false;
                instructorMgr.TickStepMs = 100;
                ScenarioConfig tacConfig = instructorMgr.Scenarios[0].Clone();
                tacConfig.swarmProfile.attackerCount = 1;
                tacConfig.swarmProfile.decoyCount = 0;
                tacConfig.swarmProfile.agentCount = 1;

                TaskCompletionSource<ScenarioEndPayload> neutralisedEndTcs = new TaskCompletionSource<ScenarioEndPayload>();
                Action<ScenarioEndPayload> neutralisedHandler = p => neutralisedEndTcs.TrySetResult(p);
                traineeMgr.OnScenarioEndReceived += neutralisedHandler;
                try
                {
                    await instructorMgr.LaunchScenarioAsync(tacConfig);
                    instructorMgr.InjectSwarmSnapshot(new SwarmAgentState[]
                    {
                        new SwarmAgentState
                        {
                            agentId = "agent-0",
                            isDecoy = false,
                            positionWorld = new Vector3(80f, 8f, -40f),
                            isNeutralised = true,
                            neutralisedAtMs = 100
                        }
                    });
                    await instructorMgr.StepScenarioTickAsync();
                    Assert(await WaitTaskAsync(neutralisedEndTcs.Task, 1500),
                        "All attackers neutralised must trigger SCENARIO_END");
                    Assert(neutralisedEndTcs.Task.Result.reason == ScenarioEndPayload.REASON_ALL_NEUTRALISED,
                        "Expected ALL_NEUTRALISED reason");
                }
                finally
                {
                    traineeMgr.OnScenarioEndReceived -= neutralisedHandler;
                }

                TaskCompletionSource<ScenarioEndPayload> breachEndTcs = new TaskCompletionSource<ScenarioEndPayload>();
                Action<ScenarioEndPayload> breachHandler = p => breachEndTcs.TrySetResult(p);
                traineeMgr.OnScenarioEndReceived += breachHandler;
                try
                {
                    await instructorMgr.LaunchScenarioAsync(tacConfig);
                    instructorMgr.InjectSwarmSnapshot(new SwarmAgentState[]
                    {
                        new SwarmAgentState
                        {
                            agentId = "agent-0",
                            isDecoy = false,
                            positionWorld = new Vector3(10f, 5f, 5f), // within 20m radius of origin
                            isNeutralised = false
                        }
                    });
                    await instructorMgr.StepScenarioTickAsync();
                    Assert(await WaitTaskAsync(breachEndTcs.Task, 1500),
                        "Attacker within 20m exclusion zone must trigger SCENARIO_END");
                    Assert(breachEndTcs.Task.Result.reason == ScenarioEndPayload.REASON_EXCLUSION_BREACH,
                        "Expected EXCLUSION_BREACH reason");
                }
                finally
                {
                    traineeMgr.OnScenarioEndReceived -= breachHandler;
                }
            }
            finally
            {
#if !UNITY_5_3_OR_NEWER
                Debug.OnLog -= onLog;
#endif
                traineeMgr.OnDestroy();
                instructorMgr.OnDestroy();
                client.Stop();
                server.Stop();
            }
        }

        private static async Task TestAllFourScenariosDropdownAndSwarmProfileLogsAsync()
        {
            List<string> logs = new List<string>();
            object logLock = new object();

#if !UNITY_5_3_OR_NEWER
            Action<string> onLog = msg => { lock (logLock) { logs.Add(msg); } };
            Debug.OnLog += onLog;
#endif

            Dropdown dropdown = CreateDropdown("ScenarioDropdown");
            Button launchBtn = CreateButton("LaunchBtn");

            WsServer server = new WsServer();
            server.Port = 9704;
            server.HeartbeatIntervalMs = 5000;
            server.StaleThresholdMs = 15000;

            WsClient client = new WsClient();
            client.ServerUri = "ws://127.0.0.1:9704/";
            client.HeartbeatIntervalMs = 5000;
            client.StaleThresholdMs = 15000;

            ScenarioManager instructorMgr = new ScenarioManager();
            instructorMgr.Server = server;
            instructorMgr.Client = null;
            instructorMgr.ScenarioDropdown = dropdown;
            instructorMgr.LaunchButton = launchBtn;
            instructorMgr.LoadSceneOnScenarioStart = false;
            instructorMgr.AutoStartTickLoop = false;

            ScenarioManager traineeMgr = new ScenarioManager();
            traineeMgr.Server = null;
            traineeMgr.Client = client;
            traineeMgr.LoadSceneOnScenarioStart = false;
            traineeMgr.AutoStartTickLoop = false;

            try
            {
                instructorMgr.Start();
                traineeMgr.Start();

                // 1. Verify Dropdown has 4 options and each contains English and Hindi names
                Assert(dropdown.options.Count == 4, "Dropdown must have exactly 4 scenario options, got " + dropdown.options.Count);
                List<ScenarioConfig> allConfigs = ScenarioLibrary.LoadAll();
                Assert(allConfigs.Count == 4, "ScenarioLibrary.LoadAll() must return 4 configs");

                for (int i = 0; i < allConfigs.Count; i++)
                {
                    ScenarioConfig cfg = allConfigs[i];
                    string optionText = dropdown.options[i].text;
                    Assert(optionText.Contains(cfg.name), "Dropdown option " + i + " must contain English name '" + cfg.name + "', got: " + optionText);
                    Assert(optionText.Contains(cfg.nameHindi), "Dropdown option " + i + " must contain Hindi name '" + cfg.nameHindi + "', got: " + optionText);
                    Assert(optionText.Contains(" / "), "Dropdown option " + i + " must format as 'English / Hindi', got: " + optionText);
                }

                // 2. Verify SelectScenarioByName selects by English name, Hindi name, id, or bilingual string
                for (int i = 0; i < allConfigs.Count; i++)
                {
                    ScenarioConfig cfg = allConfigs[i];
                    Assert(instructorMgr.SelectScenarioByName(cfg.name), "Must select scenario by English name: " + cfg.name);
                    Assert(instructorMgr.SelectedScenario.id == cfg.id, "SelectedScenario must match id " + cfg.id);

                    Assert(instructorMgr.SelectScenarioByName(cfg.nameHindi), "Must select scenario by Hindi name: " + cfg.nameHindi);
                    Assert(instructorMgr.SelectedScenario.id == cfg.id, "SelectedScenario must match id " + cfg.id);

                    Assert(instructorMgr.SelectScenarioByName(cfg.id), "Must select scenario by id: " + cfg.id);
                    Assert(instructorMgr.SelectedScenario.id == cfg.id, "SelectedScenario must match id " + cfg.id);

                    string bilingual = ScenarioManager.FormatScenarioOption(cfg);
                    Assert(instructorMgr.SelectScenarioByName(bilingual), "Must select scenario by bilingual string: " + bilingual);
                    Assert(instructorMgr.SelectedScenario.id == cfg.id, "SelectedScenario must match id " + cfg.id);
                }

                // 3. Connect WS server/client and launch each scenario to verify distinct SwarmProfile logs
                TaskCompletionSource<bool> linkConfirmedTcs = new TaskCompletionSource<bool>();
                client.OnLinkConfirmed += () => linkConfirmedTcs.TrySetResult(true);

                Assert(server.StartServer(), "WsServer must bind on port 9704");
                client.StartClient();
                Assert(await WaitTaskAsync(linkConfirmedTcs.Task, 3000), "WsClient must connect to WsServer on 9704");

                string[] scenarioIds = new string[]
                {
                    "punjab-fog-pincer-01",
                    "ladakh-lac-ridge-01",
                    "loc-mountain-pass-01",
                    "desert-fob-01"
                };

                for (int s = 0; s < scenarioIds.Length; s++)
                {
                    string targetId = scenarioIds[s];
                    ScenarioConfig targetCfg = ScenarioLibrary.FindById(targetId);
                    Assert(targetCfg != null, "Scenario config must exist: " + targetId);

                    TaskCompletionSource<ScenarioStartPayload> startReceivedTcs = new TaskCompletionSource<ScenarioStartPayload>();
                    Action<ScenarioStartPayload> onStart = p =>
                    {
                        if (string.Equals(p.scenarioId, targetId, StringComparison.Ordinal))
                            startReceivedTcs.TrySetResult(p);
                    };
                    traineeMgr.OnScenarioStartReceived += onStart;

                    try
                    {
                        instructorMgr.ResetScenarioState();
                        traineeMgr.ResetScenarioState();

                        instructorMgr.SelectScenarioByName(targetId);
                        launchBtn.onClick.Invoke();

                        bool ok = await WaitTaskAsync(startReceivedTcs.Task, 2000);
                        Assert(ok, "Trainee must receive SCENARIO_START for scenario: " + targetId);

                        ScenarioStartPayload receivedPayload = startReceivedTcs.Task.Result;
                        Assert(receivedPayload.scenarioId == targetId, "ScenarioId mismatch in SCENARIO_START");
                        Assert(receivedPayload.scenarioConfig != null, "scenarioConfig must not be null");
                        Assert(receivedPayload.scenarioConfig.swarmProfile != null, "swarmProfile must not be null");

                        // Verify distinct SwarmProfile data logged to Console
                        string expectedTactic = targetCfg.swarmProfile.tacticTree;
                        int expectedAgents = targetCfg.swarmProfile.agentCount;

                        bool sawServerLog = false;
                        bool sawClientLog = false;
                        lock (logLock)
                        {
                            for (int k = 0; k < logs.Count; k++)
                            {
                                string log = logs[k];
                                if (log.Contains("SCENARIO_START broadcast") &&
                                    log.Contains("scenarioId: " + targetId) &&
                                    log.Contains("tactic=" + expectedTactic) &&
                                    log.Contains("agents=" + expectedAgents))
                                {
                                    sawServerLog = true;
                                }

                                if (log.Contains("SCENARIO_START received") &&
                                    log.Contains("scenarioId: " + targetId) &&
                                    log.Contains("tactic=" + expectedTactic) &&
                                    log.Contains("agents=" + expectedAgents))
                                {
                                    sawClientLog = true;
                                }
                            }
                        }

                        Assert(sawServerLog, "Console must contain SCENARIO_START broadcast log with SwarmProfile for " + targetId);
                        Assert(sawClientLog, "Console must contain SCENARIO_START received log with SwarmProfile for " + targetId);
                    }
                    finally
                    {
                        traineeMgr.OnScenarioStartReceived -= onStart;
                    }
                }
            }
            finally
            {
#if !UNITY_5_3_OR_NEWER
                Debug.OnLog -= onLog;
#endif
                traineeMgr.OnDestroy();
                instructorMgr.OnDestroy();
                client.Stop();
                server.Stop();
            }
        }

        private static async Task<bool> WaitTaskAsync(Task task, int timeoutMs)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            if (ReferenceEquals(completed, task))
            {
                await task;
                return true;
            }
            return false;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message);
        }

        private static Dropdown CreateDropdown(string name = "Dropdown")
        {
#if UNITY_5_3_OR_NEWER
            return new GameObject(name).AddComponent<Dropdown>();
#else
            return new Dropdown();
#endif
        }

        private static Button CreateButton(string name = "Button")
        {
#if UNITY_5_3_OR_NEWER
            return new GameObject(name).AddComponent<Button>();
#else
            return new Button();
#endif
        }
    }
}
