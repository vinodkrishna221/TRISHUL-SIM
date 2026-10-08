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
                Console.WriteLine("[PASS] All US-P0-B Scenario Library + SCENARIO_START tests passed.");
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
            TestScenarioLibraryLoadAllAndSeedRegeneration();
            TestSceneAndMetaGuidWiring();
            await TestScenarioLaunchBroadcastSceneLoadAndErrorsAsync();
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

            Assert(cockpitScene.Contains("guid: a1b2c3d4e5f647898a1b2c3d4e5f6012"), "TraineeCockpit.unity must reference ScenarioManager GUID");
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

            Dropdown dropdown = new Dropdown();
            Button launchBtn = new Button();
            GameObject cockpitRoot = new GameObject("TraineeCockpitRoot") { activeSelf = false };

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
    }
}
