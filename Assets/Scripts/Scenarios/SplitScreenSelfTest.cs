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
    /// Runnable self-test for US-P1-A:
    /// - Split-screen fallback with two station roots (InstructorC2Root, TraineeCockpitRoot)
    /// - Normalized Camera viewport rects [0, 0, 0.5, 1] and [0.5, 0, 0.5, 1]
    /// - Single AudioListener constraint enforcement (cockpit side only)
    /// - OperationMode toggle (Two-Laptop vs Single-Laptop Split)
    /// - Loopback WsServer duplicate bind protection
    /// - Live loopback launch with 6 Boids, 10 Hz ticks, NDJSON growth, and Scorecard relay
    /// </summary>
    public static class SplitScreenSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllAsync().GetAwaiter().GetResult();
                Console.WriteLine("\n[PASS] All US-P1-A Split-Screen Fallback tests passed successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("\n[FAIL] " + ex);
                return 1;
            }
        }
#endif

        public static async Task RunAllAsync()
        {
            Console.WriteLine("=== Running US-P1-A Split-Screen Fallback Test Suite ===");

            TestPrefabAndSceneFileStructure();
            Console.WriteLine("[PASS] TestPrefabAndSceneFileStructure");

            TestCameraViewportRectsAndAudioListener();
            Console.WriteLine("[PASS] TestCameraViewportRectsAndAudioListener");

            TestOperationModeTransitionsAndEvents();
            Console.WriteLine("[PASS] TestOperationModeTransitionsAndEvents");

            TestDefaultBoidSwarmGeneration();
            Console.WriteLine("[PASS] TestDefaultBoidSwarmGeneration");

            await TestWsServerDuplicateBindProtectionAsync();
            Console.WriteLine("[PASS] TestWsServerDuplicateBindProtectionAsync");

            await TestLiveLoopbackSplitScreenExecutionAsync();
            Console.WriteLine("[PASS] TestLiveLoopbackSplitScreenExecutionAsync");
        }

        private static void TestPrefabAndSceneFileStructure()
        {
            // 1. InstructorC2Root prefab & meta
            string c2Prefab = Path.Combine("Assets", "Prefabs", "InstructorC2Root.prefab");
            string c2Meta = Path.Combine("Assets", "Prefabs", "InstructorC2Root.prefab.meta");
            Assert(File.Exists(c2Prefab), "InstructorC2Root.prefab must exist at " + c2Prefab);
            Assert(File.Exists(c2Meta), "InstructorC2Root.prefab.meta must exist at " + c2Meta);
            string c2MetaContent = File.ReadAllText(c2Meta, Encoding.UTF8);
            Assert(c2MetaContent.Contains("guid: c1b2c3d4e5f647898a1b2c3d4e5f7001"), "InstructorC2Root GUID mismatch");

            // 2. TraineeCockpitRoot prefab & meta
            string cockpitPrefab = Path.Combine("Assets", "Prefabs", "TraineeCockpitRoot.prefab");
            string cockpitMeta = Path.Combine("Assets", "Prefabs", "TraineeCockpitRoot.prefab.meta");
            Assert(File.Exists(cockpitPrefab), "TraineeCockpitRoot.prefab must exist at " + cockpitPrefab);
            Assert(File.Exists(cockpitMeta), "TraineeCockpitRoot.prefab.meta must exist at " + cockpitMeta);
            string cockpitMetaContent = File.ReadAllText(cockpitMeta, Encoding.UTF8);
            Assert(cockpitMetaContent.Contains("guid: c1b2c3d4e5f647898a1b2c3d4e5f7002"), "TraineeCockpitRoot GUID mismatch");

            // 3. SplitScreen scene & meta
            string splitScene = Path.Combine("Assets", "Scenes", "SplitScreen.unity");
            string splitMeta = Path.Combine("Assets", "Scenes", "SplitScreen.unity.meta");
            Assert(File.Exists(splitScene), "SplitScreen.unity must exist at " + splitScene);
            Assert(File.Exists(splitMeta), "SplitScreen.unity.meta must exist at " + splitMeta);
            string splitMetaContent = File.ReadAllText(splitMeta, Encoding.UTF8);
            Assert(splitMetaContent.Contains("guid: c1b2c3d4e5f647898a1b2c3d4e5f7003"), "SplitScreen scene GUID mismatch");

            // 4. InstructorC2 scene has ModeDropdown
            string c2Scene = Path.Combine("Assets", "Scenes", "InstructorC2.unity");
            Assert(File.Exists(c2Scene), "InstructorC2.unity must exist");
            string c2SceneContent = File.ReadAllText(c2Scene, Encoding.UTF8);
            Assert(c2SceneContent.Contains("ModeDropdown"), "InstructorC2.unity must contain ModeDropdown");
            Assert(c2SceneContent.Contains("modeDropdown: {fileID: 440000003}"), "InstructorC2.unity must wire modeDropdown");
            Assert(c2SceneContent.Contains("Single-Laptop (Split)"), "InstructorC2.unity must contain Single-Laptop (Split) option");
        }

        private static void TestCameraViewportRectsAndAudioListener()
        {
            string splitScenePath = Path.Combine("Assets", "Scenes", "SplitScreen.unity");
            string splitText = File.ReadAllText(splitScenePath, Encoding.UTF8);

            // Assert C2 Camera rect: x: 0, y: 0, width: 0.5, height: 1
            Assert(Regex.IsMatch(splitText, @"CameraC2[\s\S]*?NormalizedViewPortRect:[\s\S]*?x:\s*0\b[\s\S]*?y:\s*0\b[\s\S]*?width:\s*0\.5\b[\s\S]*?height:\s*1\b"),
                "CameraC2 must have normalized viewport rect [0, 0, 0.5, 1]");

            // Assert Cockpit Camera rect: x: 0.5, y: 0, width: 0.5, height: 1
            Assert(Regex.IsMatch(splitText, @"CameraCockpit[\s\S]*?NormalizedViewPortRect:[\s\S]*?x:\s*0\.5\b[\s\S]*?y:\s*0\b[\s\S]*?width:\s*0\.5\b[\s\S]*?height:\s*1\b"),
                "CameraCockpit must have normalized viewport rect [0.5, 0, 0.5, 1]");

            // AudioListener rule: Exactly one AudioListener in the SplitScreen scene, on CameraCockpit
            int audioListenerCount = Regex.Matches(splitText, @"--- !u!81\b").Count;
            Assert(audioListenerCount == 1, "SplitScreen.unity must have exactly 1 AudioListener to prevent Unity warnings, found: " + audioListenerCount);

            // Verify CameraC2 component does not declare AudioListener
            int c2Index = splitText.IndexOf("m_Name: CameraC2", StringComparison.Ordinal);
            int c2CameraIndex = splitText.IndexOf("Camera:", c2Index, StringComparison.Ordinal);
            int nextComponent = splitText.IndexOf("--- !u!", c2CameraIndex + 7, StringComparison.Ordinal);
            string c2CameraBlock = nextComponent > 0 ? splitText.Substring(c2Index, nextComponent - c2Index) : splitText.Substring(c2Index);
            Assert(!c2CameraBlock.Contains("AudioListener"), "CameraC2 must NOT have an AudioListener attached");

            // Verify CameraCockpit has AudioListener
            int cockpitIndex = splitText.IndexOf("m_Name: CameraCockpit", StringComparison.Ordinal);
            int nextObject = splitText.IndexOf("m_Name: CockpitNetworkManager", cockpitIndex, StringComparison.Ordinal);
            string cockpitBlock = nextObject > 0 ? splitText.Substring(cockpitIndex, nextObject - cockpitIndex) : splitText.Substring(cockpitIndex);
            Assert(cockpitBlock.Contains("AudioListener:"), "CameraCockpit MUST have the single AudioListener attached");
        }

        private static void TestOperationModeTransitionsAndEvents()
        {
            var manager = new ScenarioManager();

            Assert(manager.CurrentMode == OperationMode.TwoLaptop, "Default mode must be TwoLaptop");

            OperationMode firedMode = (OperationMode)(-1);
            int eventFiredCount = 0;
            manager.OnOperationModeChanged += mode =>
            {
                firedMode = mode;
                eventFiredCount++;
            };

            // Switch to SplitScreen
            manager.SetOperationMode(OperationMode.SingleLaptopSplit);
            Assert(manager.CurrentMode == OperationMode.SingleLaptopSplit, "CurrentMode must be SingleLaptopSplit");
            Assert(eventFiredCount == 1, "OnOperationModeChanged should have fired once");
            Assert(firedMode == OperationMode.SingleLaptopSplit, "Fired mode must be SingleLaptopSplit");

            // Switch back to TwoLaptop
            manager.SetOperationMode(OperationMode.TwoLaptop);
            Assert(manager.CurrentMode == OperationMode.TwoLaptop, "CurrentMode must be TwoLaptop");
            Assert(eventFiredCount == 2, "OnOperationModeChanged should have fired twice");
            Assert(firedMode == OperationMode.TwoLaptop, "Fired mode must be TwoLaptop");

            // Setting same mode again should be a no-op
            manager.SetOperationMode(OperationMode.TwoLaptop);
            Assert(eventFiredCount == 2, "Setting same mode should not fire redundant event");

            // Mode dropdown helper
#if UNITY_5_3_OR_NEWER
            var dropdown = new GameObject("ModeDropdown").AddComponent<Dropdown>();
#else
            var dropdown = new Dropdown();
#endif
            manager.ModeDropdown = dropdown;
            Assert(dropdown.options.Count == 2, "Mode dropdown must have 2 options");
            Assert(dropdown.options[0].text == ScenarioManager.ModeTwoLaptopText, "Option 0 must be Two-Laptop");
            Assert(dropdown.options[1].text == ScenarioManager.ModeSplitScreenText, "Option 1 must be Single-Laptop (Split)");

            // Dropdown selection should change mode
            dropdown.value = 1;
            Assert(manager.CurrentMode == OperationMode.SingleLaptopSplit, "Selecting dropdown 1 must switch to SplitScreen");
            Assert(eventFiredCount == 3, "Dropdown change should fire OnOperationModeChanged");

            dropdown.value = 0;
            Assert(manager.CurrentMode == OperationMode.TwoLaptop, "Selecting dropdown 0 must switch to TwoLaptop");
            Assert(eventFiredCount == 4, "Dropdown change should fire OnOperationModeChanged");
        }

        private static void TestDefaultBoidSwarmGeneration()
        {
            var boids = ScenarioManager.CreateDefaultBoidSwarm();
            Assert(boids != null, "CreateDefaultBoidSwarm must not return null");
            Assert(boids.Length == 6, "Expected exactly 6 Boids for procedural split-screen swarm, found: " + boids.Length);

            for (int i = 0; i < boids.Length; i++)
            {
                var agent = boids[i];
                Assert(agent.agentId == "agent-" + i, "Boid agentId mismatch at index " + i + ": " + agent.agentId);
                Assert(agent.altitudeM > 0.0f, "Boid altitude must be positive");
                Assert(agent.rfPowerDbm > 0.0f, "Boid rfPowerDbm must be positive");
                Assert(!agent.isNeutralised, "Boid must be active (not neutralised)");
            }
        }

        private static async Task TestWsServerDuplicateBindProtectionAsync()
        {
            var server1 = new WsServer();
            server1.Port = 9700;
            server1.HeartbeatIntervalMs = 5000;
            server1.StaleThresholdMs = 15000;

            bool server1Started = server1.StartServer();
            Assert(server1Started, "Server 1 should bind to port 9700 successfully");
            Assert(server1.IsRunning, "Server 1 IsRunning must be true");

            var server2 = new WsServer();
            server2.Port = 9700;
            server2.HeartbeatIntervalMs = 5000;
            server2.StaleThresholdMs = 15000;

            // Attempting to bind duplicate server on same port must NOT throw unhandled exception or crash
            bool server2Started = false;
            try
            {
                server2Started = server2.StartServer();
            }
            catch (Exception ex)
            {
                Assert(false, "Server 2 bind should not throw unhandled exception: " + ex.Message);
            }

            Assert(!server2Started, "Duplicate server bind should return false gracefully");
            Assert(!server2.IsRunning, "Server 2 IsRunning must be false");

            // Clean up
            server1.Stop();
            await Task.Delay(150);
            Assert(!server1.IsRunning, "Server 1 should have stopped");
        }

        private static async Task TestLiveLoopbackSplitScreenExecutionAsync()
        {
            // Setup temp session directory for telemetry validation
            string testSessionDir = Path.Combine(Path.GetTempPath(), "TRISHUL-SIM-SplitTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testSessionDir);

            // 1. Setup C2 Server + ScenarioManager
            var server = new WsServer();
            server.Port = 9700;
            server.HeartbeatIntervalMs = 5000;
            server.StaleThresholdMs = 15000;

            var c2Manager = new ScenarioManager();
            c2Manager.Server = server;
            c2Manager.CurrentMode = OperationMode.SingleLaptopSplit;
            c2Manager.LoadSceneOnScenarioStart = false; // Split screen doesn't reload scene
            c2Manager.SpawnDefaultSwarmIfEmpty = true; // Generates 6 Boids
            c2Manager.TelemetryDirectoryOverride = testSessionDir;
            c2Manager.TickIntervalSeconds = 0.05f; // Fast 50ms ticks for test speed

            // 2. Setup Trainee Cockpit Client + ScenarioManager
            var client = new WsClient();
            client.ServerUri = "ws://127.0.0.1:9700/";
            client.ClientRole = "TRAINEE_COCKPIT";
            client.HeartbeatIntervalMs = 5000;
            client.StaleThresholdMs = 15000;

            var cockpitManager = new ScenarioManager();
            cockpitManager.Client = client;
            cockpitManager.CurrentMode = OperationMode.SingleLaptopSplit;
            cockpitManager.LoadSceneOnScenarioStart = false; // Split screen: do not unload
            cockpitManager.SpawnDefaultSwarmIfEmpty = true;

            // Start server
            bool srvOk = server.StartServer();
            Assert(srvOk, "WsServer failed to start on 9700");

            // Tracking variables
            string startSessionId = null;
            var receivedTicks = new List<JsonEnvelope>();
            bool cockpitStartReceived = false;
            string receivedScorecardJson = null;

            client.OnMessageReceived += env =>
            {
                if (env.type == "SCENARIO_START")
                {
                    cockpitStartReceived = true;
                }
                else if (env.type == "SCENARIO_TICK")
                {
                    lock (receivedTicks)
                    {
                        receivedTicks.Add(env);
                    }
                }
            };

            cockpitManager.OnScorecardReceived += env =>
            {
                if (!string.IsNullOrEmpty(env.payload))
                {
                    receivedScorecardJson = env.payload;
                }
            };

            // Connect client
            client.StartClient();
            for (int i = 0; i < 40 && (!client.IsConnected || server.ConnectedCount == 0); i++)
            {
                await Task.Delay(50);
            }
            Assert(client.IsConnected, "WsClient failed to connect to loopback server");
            Assert(server.ConnectedCount >= 1, "Server has no connected clients");

            // Load scenario
            string punjabPath = Path.Combine("Assets", "StreamingAssets", "scenarios", "punjab-fog-pincer-01.json");
            string punjabJson = File.ReadAllText(punjabPath, Encoding.UTF8);
            var scenario = ScenarioConfig.Parse(punjabJson);

            // Launch scenario from C2 station
            startSessionId = c2Manager.LaunchScenario(scenario);
            Assert(!string.IsNullOrEmpty(startSessionId), "LaunchScenario must return non-empty sessionId");

            // Wait for SCENARIO_START on Trainee station
            for (int i = 0; i < 40 && !cockpitStartReceived; i++)
            {
                await Task.Delay(50);
            }
            Assert(cockpitStartReceived, "Cockpit station did not receive SCENARIO_START in split-screen mode");

            // Wait for 10 Hz ticks to accumulate (at least 5 ticks)
            for (int i = 0; i < 40 && receivedTicks.Count < 5; i++)
            {
                await Task.Delay(50);
            }
            Assert(receivedTicks.Count >= 5, "Expected at least 5 SCENARIO_TICK broadcasts, got: " + receivedTicks.Count);

            // Verify Boid swarm is populated in tick payload
            JsonEnvelope sampleTick;
            lock (receivedTicks)
            {
                sampleTick = receivedTicks[receivedTicks.Count - 1];
            }
            Assert(!string.IsNullOrEmpty(sampleTick.payload), "Tick payload must not be empty");
            Assert(sampleTick.payload.Contains("\"swarmSnapshot\":["), "Tick payload must contain swarmSnapshot array");
            Assert(sampleTick.payload.Contains("agent-"), "Tick payload must contain procedurally generated Boid drones");

            // Verify NDJSON telemetry log file exists and grows on disk
            string sessionNdjsonPath = Path.Combine(testSessionDir, startSessionId + ".ndjson");
            Assert(File.Exists(sessionNdjsonPath), "Telemetry NDJSON file must exist at " + sessionNdjsonPath);
            List<string> ndjsonLines = new List<string>();
            using (FileStream fs = new FileStream(sessionNdjsonPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
            {
                string l;
                while ((l = sr.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(l))
                        ndjsonLines.Add(l);
                }
            }
            Assert(ndjsonLines.Count >= 5, "Expected at least 5 NDJSON lines recorded on disk, found: " + ndjsonLines.Count);

            // Test SCORECARD_PAYLOAD relay
            string mockScorecard = "{\"sessionId\":\"" + startSessionId + "\",\"scenarioId\":\"" + scenario.id + "\",\"dronesTotal\":6,\"dronesNeutralised\":6,\"threatState\":\"GREEN\"}";

            await c2Manager.SendScorecardPayloadAsync(mockScorecard);
            for (int i = 0; i < 40 && receivedScorecardJson == null; i++)
            {
                await Task.Delay(50);
            }
            Assert(receivedScorecardJson != null, "Scorecard payload not received on cockpit station");
            Assert(receivedScorecardJson.Contains(startSessionId), "Scorecard payload sessionId mismatch: " + receivedScorecardJson);
            Assert(receivedScorecardJson.Contains("\"dronesNeutralised\":6"), "Scorecard dronesNeutralised mismatch: " + receivedScorecardJson);

            // Send Abort
            c2Manager.BroadcastScenarioEnd("INSTRUCTOR_ABORT");
            await Task.Delay(100);
            Assert(!c2Manager.IsScenarioRunning, "Scenario should not be running after scenario end");

            // Clean up
            client.Stop();
            server.Stop();
            await Task.Delay(100);

            try
            {
                if (Directory.Exists(testSessionDir))
                    Directory.Delete(testSessionDir, true);
            }
            catch { }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception("[ASSERTION FAILED] " + message);
        }
    }
}
