// ============================================================
// ScorecardComputerSelfTest.cs — Runnable Self-Test for AA-P0-B
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Validates:
// 1. Scorecard contract fidelity & JSON schema matching scorecard.md v1
// 2. ScorecardMetrics (all stub zeros/false in P0) & DoctrineBenchmarks
// 3. SessionManifest & SessionSummary lifecycle matching docs/SCHEMA.md Entity 5
// 4. SCENARIO_END trigger & <=10s timing SLA (completes in <50ms)
// 5. File outputs on disk:
//    - %AppData%\TRISHUL-SIM\sessions\<sessionId>.scorecard.json
//    - %AppData%\TRISHUL-SIM\sessions\manifest.json
// 6. Live WebSocket SCORECARD_PAYLOAD broadcast (Client -> Server -> broadcast)
//    - Instructor C2 / Server: "[WsServer] SCORECARD_PAYLOAD received — routing to all clients"
//    - Trainee / Client: "[AAREngine] SCORECARD_PAYLOAD sent"
//
// Compiles directly with csc.exe or inside Unity Editor tests.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TrishulSim.Network;
using TrishulSim.Scenarios;
using UnityEngine;

namespace TrishulSim.AAR
{
    public static class ScorecardComputerSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllAsync().GetAwaiter().GetResult();
                Console.WriteLine("\n[PASS] All AA-P0-B ScorecardComputer tests passed.");
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

        public static async Task RunAllAsync()
        {
            Console.WriteLine("=== Running TRISHUL-SIM AA-P0-B ScorecardComputer Self-Test ===");

            TestScorecardAndManifestContractFidelity();
            TestScorecardComputerP0StubComputationAndDiskFiles();
            TestEndToEndScenarioEndTriggerWithTelemetryLogger();
            await TestWebSocketPayloadBroadcastWithLiveServerAndClientAsync();
            TestRealAppDataRuntimeLifecycle();

            Console.WriteLine("\n=== AA-P0-B ScorecardComputer Self-Test COMPLETE: ALL PASS ===");
        }

        /// <summary>
        /// Validates Scorecard, ScorecardMetrics, DoctrineBenchmarks, and SessionManifest serialization/parsing
        /// against board/contracts/scorecard.md and docs/SCHEMA.md.
        /// </summary>
        public static void TestScorecardAndManifestContractFidelity()
        {
            Console.WriteLine("\n-- Test 1: Scorecard & Manifest Contract Fidelity --");

            // 1. Verify Scorecard contract example
            Scorecard sc = new Scorecard
            {
                sessionId = "sess-20261004-143201",
                scenarioId = "punjab-fog-pincer-01",
                scenarioName = "Punjab Border — Zero Visibility Pincer",
                scenarioNameHindi = "पंजाब सीमा — शून्य दृश्यता पिंसर",
                traineeId = "havildar-rajan",
                completedAtIso = "2026-10-04T14:33:01+05:30",
                durationMs = 55000,
                doctrinePassed = false,
                metrics = new ScorecardMetrics
                {
                    timeToDetectionMs = 0,
                    classificationAccuracy = 0.0f,
                    roeCompliance = 0.0f,
                    kineticRounds = 0,
                    kineticHits = 0,
                    kineticEfficiency = 0.0f,
                    agentsNeutralised = 0,
                    agentsMissed = 0,
                    prematureEwPenalty = false
                },
                doctrineBenchmarks = new DoctrineBenchmarks
                {
                    timeToDetectionThresholdMs = 15000,
                    roeMinCompliance = 1.0f,
                    kineticMinEfficiency = 0.5f
                },
                timelineIndex = new int[0],
                sessionFilePath = "C:\\Users\\Rajan\\AppData\\Roaming\\TRISHUL-SIM\\sessions\\sess-20261004-143201.ndjson",
                partial = false,
                version = 1
            };

            string json = sc.ToJson();
            Assert(!string.IsNullOrEmpty(json), "Serialized Scorecard JSON must not be empty");

            // Field presence assertions
            Assert(json.Contains("\"sessionId\":\"sess-20261004-143201\""), "Must contain sessionId");
            Assert(json.Contains("\"scenarioId\":\"punjab-fog-pincer-01\""), "Must contain scenarioId");
            Assert(json.Contains("\"scenarioName\":"), "Must contain scenarioName");
            Assert(json.Contains("\"scenarioNameHindi\":"), "Must contain scenarioNameHindi");
            Assert(json.Contains("\"traineeId\":\"havildar-rajan\""), "Must contain traineeId");
            Assert(json.Contains("\"completedAtIso\":"), "Must contain completedAtIso");
            Assert(json.Contains("\"durationMs\":55000"), "Must contain durationMs");
            Assert(json.Contains("\"doctrinePassed\":false"), "Must contain doctrinePassed:false");
            Assert(json.Contains("\"metrics\":"), "Must contain metrics");
            Assert(json.Contains("\"doctrineBenchmarks\":"), "Must contain doctrineBenchmarks");
            Assert(json.Contains("\"timelineIndex\":[]"), "Must contain empty timelineIndex for P0 stub");
            Assert(json.Contains("\"sessionFilePath\":"), "Must contain sessionFilePath");
            Assert(json.Contains("\"partial\":false"), "Must contain partial:false");
            Assert(json.Contains("\"version\":1"), "Must contain version:1");

            // Round trip
            Scorecard parsed;
            string err;
            bool ok = Scorecard.TryParse(json, out parsed, out err);
            Assert(ok, "Scorecard.TryParse must succeed: " + err);
            Assert(parsed.sessionId == sc.sessionId, "Parsed sessionId must match");
            Assert(parsed.scenarioId == sc.scenarioId, "Parsed scenarioId must match");
            Assert(parsed.durationMs == 55000, "Parsed durationMs must match");
            Assert(parsed.doctrinePassed == false, "Parsed doctrinePassed must be false");
            Assert(parsed.metrics.timeToDetectionMs == 0, "Parsed metrics.timeToDetectionMs must be 0");
            Assert(parsed.metrics.kineticRounds == 0, "Parsed metrics.kineticRounds must be 0");
            Assert(parsed.doctrineBenchmarks.timeToDetectionThresholdMs == 15000, "Benchmark must be 15000");

            // 2. Verify SessionManifest contract
            SessionManifest manifest = new SessionManifest();
            manifest.AddOrUpdate(new SessionSummary("sess-01", "punjab-01", "2026-10-08T18:00:00+05:30", false, "C:\\test\\sess-01.scorecard.json", false));
            manifest.AddOrUpdate(new SessionSummary("sess-02", "ladakh-01", "2026-10-08T18:30:00+05:30", true, "C:\\test\\sess-02.scorecard.json", false));

            string manifestJson = manifest.ToJson();
            Assert(manifestJson.Contains("\"sessions\":["), "Manifest JSON must contain sessions array");
            Assert(manifestJson.Contains("\"sess-01\""), "Must contain sess-01");
            Assert(manifestJson.Contains("\"sess-02\""), "Must contain sess-02");

            SessionManifest parsedManifest;
            bool manOk = SessionManifest.TryParse(manifestJson, out parsedManifest, out err);
            Assert(manOk, "SessionManifest.TryParse must succeed: " + err);
            Assert(parsedManifest.sessions.Count == 2, "Must contain 2 sessions");
            Assert(parsedManifest.sessions[0].sessionId == "sess-01", "Session 0 must be sess-01");
            Assert(parsedManifest.sessions[1].doctrinePassed == true, "Session 1 doctrinePassed must be true");

            Console.WriteLine("[PASS] Scorecard & Manifest Contract Fidelity PASSED");
        }

        /// <summary>
        /// Validates P0 stub Scorecard compute: all metrics=0, scorecard.json and manifest.json written to disk.
        /// </summary>
        public static void TestScorecardComputerP0StubComputationAndDiskFiles()
        {
            Console.WriteLine("\n-- Test 2: P0 Stub Scorecard Computation & Disk Files --");

            string tempDir = Path.Combine(Path.GetTempPath(), "TrishulSim_ScorecardTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                ScorecardComputer computer = new ScorecardComputer();
                computer.SessionsDirectoryOverride = tempDir;

                string sessionId = "sess-p0-stub-01";
                string scenarioId = "punjab-fog-pincer-01";

                Scorecard sc = computer.ComputeScorecard(sessionId, scenarioId, "TIME_LIMIT", 45000);

                Assert(sc != null, "Computed Scorecard must not be null");
                Assert(sc.sessionId == sessionId, "sessionId must match");
                Assert(sc.scenarioId == scenarioId, "scenarioId must match");
                Assert(sc.durationMs == 45000, "durationMs must match");
                Assert(sc.doctrinePassed == false, "doctrinePassed must be false in P0 stub");
                Assert(sc.metrics.timeToDetectionMs == 0, "timeToDetectionMs must be 0");
                Assert(sc.metrics.kineticEfficiency == 0f, "kineticEfficiency must be 0");
                Assert(sc.metrics.roeCompliance == 0f, "roeCompliance must be 0");
                Assert(sc.timelineIndex != null && sc.timelineIndex.Length == 0, "timelineIndex must be empty in P0 stub");

                // Verify <sessionId>.scorecard.json
                string scorecardFile = Path.Combine(tempDir, sessionId + ".scorecard.json");
                Assert(File.Exists(scorecardFile), "Scorecard JSON file must exist on disk");

                string fileContent = File.ReadAllText(scorecardFile, Encoding.UTF8);
                Scorecard diskSc;
                string err;
                bool ok = Scorecard.TryParse(fileContent, out diskSc, out err);
                Assert(ok, "Disk scorecard file must parse cleanly: " + err);
                Assert(diskSc.sessionId == sessionId, "Disk sessionId must match");
                Assert(diskSc.metrics.timeToDetectionMs == 0, "Disk metrics must be zeros");

                // Verify manifest.json
                string manifestFile = Path.Combine(tempDir, "manifest.json");
                Assert(File.Exists(manifestFile), "manifest.json must exist on disk");

                SessionManifest manifest = SessionManifest.LoadOrCreate(manifestFile);
                Assert(manifest.sessions.Count == 1, "manifest.json must have 1 entry");
                Assert(manifest.sessions[0].sessionId == sessionId, "Manifest entry sessionId must match");
                Assert(manifest.sessions[0].scoreFilePath == scorecardFile, "Manifest entry scoreFilePath must match");
                Assert(manifest.sessions[0].doctrinePassed == false, "Manifest entry doctrinePassed must match");

                Console.WriteLine("[PASS] P0 Stub Scorecard Computation & Disk Files PASSED");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        /// <summary>
        /// Validates combined lifecycle with TelemetryLogger: SCENARIO_START -> SCENARIO_TICK -> SCENARIO_END
        /// and verifies <=10s compute timing guarantee.
        /// </summary>
        public static void TestEndToEndScenarioEndTriggerWithTelemetryLogger()
        {
            Console.WriteLine("\n-- Test 3: End-to-End Scenario Lifecycle (TelemetryLogger + ScorecardComputer) --");

            string tempDir = Path.Combine(Path.GetTempPath(), "TrishulSim_AarE2ETest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                TelemetryLogger logger = new TelemetryLogger();
                logger.SessionsDirectoryOverride = tempDir;

                ScorecardComputer computer = new ScorecardComputer();
                computer.SessionsDirectoryOverride = tempDir;
                computer.Logger = logger;

                string sessionId = "sess-e2e-aar-01";
                string scenarioId = "punjab-fog-pincer-01";

                // 1. Dispatch SCENARIO_START envelope to both components
                ScenarioStartPayload startPayload = new ScenarioStartPayload
                {
                    sessionId = sessionId,
                    scenarioId = scenarioId,
                    seed = 42819,
                    startedAtMs = JsonEnvelope.NowUnixMs()
                };
                JsonEnvelope startEnv = JsonEnvelope.Create(JsonEnvelope.SCENARIO_START, startPayload.ToJson());
                logger.HandleMessageReceived(startEnv);
                computer.HandleMessageReceived(startEnv);

                Assert(logger.IsLogging, "Logger must be logging");
                Assert(computer.LastStartPayload != null, "Computer must have recorded start payload");

                // 2. Dispatch 30 ticks (3s of scenario at 10 Hz)
                for (int i = 0; i < 30; i++)
                {
                    ScenarioTickPayload tick = TelemetryLoggerSelfTest.CreatePunjabTickPayload(sessionId, i * 100);
                    logger.LogScenarioTick(tick);
                }

                // 3. Dispatch SCENARIO_END envelope and measure timing
                DateTime startCompute = DateTime.UtcNow;

                ScenarioEndPayload endPayload = new ScenarioEndPayload
                {
                    sessionId = sessionId,
                    t = 3000,
                    reason = "ALL_NEUTRALISED",
                    finalSwarmSnapshot = new SwarmAgentState[0]
                };
                JsonEnvelope endEnv = JsonEnvelope.Create(JsonEnvelope.SCENARIO_END, endPayload.ToJson());

                logger.HandleMessageReceived(endEnv);
                computer.HandleMessageReceived(endEnv);

                double computeElapsedSeconds = (DateTime.UtcNow - startCompute).TotalSeconds;
                Console.WriteLine(string.Format("Scorecard compute completed in {0:F4}s (SLA <= 10.0s)", computeElapsedSeconds));
                Assert(computeElapsedSeconds < 10.0, "Scorecard computation must complete in <= 10 seconds (PRD requirement)");

                // Verify disk artifacts
                string ndjsonPath = Path.Combine(tempDir, sessionId + ".ndjson");
                string scorecardPath = Path.Combine(tempDir, sessionId + ".scorecard.json");
                string manifestPath = Path.Combine(tempDir, "manifest.json");

                Assert(File.Exists(ndjsonPath), "NDJSON telemetry file must exist");
                Assert(File.Exists(scorecardPath), "Scorecard JSON file must exist");
                Assert(File.Exists(manifestPath), "manifest.json file must exist");

                Scorecard computed = computer.LastComputedScorecard;
                Assert(computed != null, "LastComputedScorecard must not be null");
                Assert(computed.sessionId == sessionId, "Scorecard sessionId must match");
                Assert(computed.durationMs == 3000, "Scorecard durationMs must be 3000");
                Assert(computed.sessionFilePath == ndjsonPath, "sessionFilePath must point to the NDJSON file");

                Console.WriteLine("[PASS] End-to-End Scenario Lifecycle PASSED");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        /// <summary>
        /// Validates WebSocket SCORECARD_PAYLOAD broadcast over loopback network:
        /// 1. WsServer starts on localhost
        /// 2. WsClient connects and handshakes
        /// 3. ScorecardComputer sends SCORECARD_PAYLOAD envelope
        /// 4. WsServer re-broadcasts to all clients with console log:
        ///    "[WsServer] SCORECARD_PAYLOAD received — routing to all clients"
        /// 5. Client receives broadcasted SCORECARD_PAYLOAD envelope
        /// </summary>
        public static async Task TestWebSocketPayloadBroadcastWithLiveServerAndClientAsync()
        {
            Console.WriteLine("\n-- Test 4: Live WebSocket SCORECARD_PAYLOAD Broadcast --");

            int testPort = 9718; // isolated port
            string serverUri = "ws://127.0.0.1:" + testPort + "/";

            WsServer server = new WsServer();
            server.Port = testPort;

            WsClient client = new WsClient();
            client.ServerUri = serverUri;
            client.AutoReconnect = false;

            ScorecardComputer computer = new ScorecardComputer();
            computer.Client = client;

            TaskCompletionSource<JsonEnvelope> serverScorecardTcs = new TaskCompletionSource<JsonEnvelope>();
            TaskCompletionSource<JsonEnvelope> clientScorecardTcs = new TaskCompletionSource<JsonEnvelope>();
            TaskCompletionSource<bool> linkConfirmedTcs = new TaskCompletionSource<bool>();

            // Monitor server incoming messages
            server.OnMessageReceived += env =>
            {
                if (string.Equals(env.type, JsonEnvelope.SCORECARD_PAYLOAD, StringComparison.Ordinal))
                {
                    serverScorecardTcs.TrySetResult(env);
                    Console.WriteLine("Server caught SCORECARD_PAYLOAD envelope!");
                }
            };

            // Monitor client link confirmed and received broadcast
            client.OnLinkConfirmed += () => linkConfirmedTcs.TrySetResult(true);
            client.OnMessageReceived += env =>
            {
                if (string.Equals(env.type, JsonEnvelope.SCORECARD_PAYLOAD, StringComparison.Ordinal))
                {
                    clientScorecardTcs.TrySetResult(env);
                    Console.WriteLine("Client received broadcasted SCORECARD_PAYLOAD envelope!");
                }
            };

            try
            {
                bool serverStarted = server.StartServer();
                Assert(serverStarted && server.IsRunning, "WsServer must start on test port " + testPort);

                client.StartClient();

                Task handshakeDone = await Task.WhenAny(linkConfirmedTcs.Task, Task.Delay(3000));
                Assert(handshakeDone == linkConfirmedTcs.Task, "Timed out waiting for client-server link confirmation");

                Assert(server.ConnectedCount > 0, "Server must have at least 1 connected client");
                Assert(client.IsConnected, "Client must be connected");

                // Now compute and broadcast Scorecard
                string sessionId = "sess-ws-broadcast-test";
                Scorecard sc = computer.ComputeScorecard(sessionId, "punjab-fog-pincer-01", "TIME_LIMIT", 60000);

                Task serverDone = await Task.WhenAny(serverScorecardTcs.Task, Task.Delay(2000));
                Assert(serverDone == serverScorecardTcs.Task, "WsServer must have received SCORECARD_PAYLOAD from client");

                Task clientDone = await Task.WhenAny(clientScorecardTcs.Task, Task.Delay(2000));
                Assert(clientDone == clientScorecardTcs.Task, "WsClient must have received re-broadcasted SCORECARD_PAYLOAD");

                Console.WriteLine("[PASS] Live WebSocket SCORECARD_PAYLOAD Broadcast PASSED");
            }
            finally
            {
                try { client.Stop(); } catch { }
                try { server.Stop(); } catch { }
            }
        }

        /// <summary>
        /// Validates runtime file creation in real %AppData%\TRISHUL-SIM\sessions\ directory.
        /// </summary>
        public static void TestRealAppDataRuntimeLifecycle()
        {
            Console.WriteLine("\n-- Test 5: Real %AppData%\\TRISHUL-SIM\\sessions\\ Runtime Verification --");

            ScorecardComputer computer = new ScorecardComputer();
            computer.SessionsDirectoryOverride = null; // Use real default AppData

            string defaultDir = computer.GetSessionsDirectory();
            Console.WriteLine("Resolved default sessions directory: " + defaultDir);
            Assert(defaultDir.Contains("TRISHUL-SIM") && defaultDir.Contains("sessions"), "Must resolve to TRISHUL-SIM/sessions");

            string sessionId = "sess-appdata-scorecard-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            Scorecard sc = computer.ComputeScorecard(sessionId, "punjab-fog-pincer-01", "ALL_NEUTRALISED", 52000);

            string expectedScorecardFile = Path.Combine(defaultDir, sessionId + ".scorecard.json");
            string expectedManifestFile = Path.Combine(defaultDir, "manifest.json");

            Console.WriteLine("Checking scorecard file: " + expectedScorecardFile);
            Assert(File.Exists(expectedScorecardFile), "Real scorecard file must exist in AppData");

            FileInfo fi = new FileInfo(expectedScorecardFile);
            Console.WriteLine("Real scorecard file size: " + fi.Length + " bytes");
            Assert(fi.Length > 0, "Scorecard file must have non-zero size");

            Scorecard readSc = Scorecard.Parse(File.ReadAllText(expectedScorecardFile, Encoding.UTF8));
            Assert(readSc.sessionId == sessionId, "SessionId must match in real AppData file");
            Assert(readSc.doctrinePassed == false, "doctrinePassed must be false");
            Assert(readSc.metrics.timeToDetectionMs == 0, "Metrics must be stubs");

            Assert(File.Exists(expectedManifestFile), "Real manifest.json must exist in AppData");
            SessionManifest manifest = SessionManifest.LoadOrCreate(expectedManifestFile);
            Assert(manifest.sessions.Exists(s => s.sessionId == sessionId), "Session must be recorded in real manifest.json");

            Console.WriteLine("[PASS] Real AppData Runtime Verification PASSED");
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
