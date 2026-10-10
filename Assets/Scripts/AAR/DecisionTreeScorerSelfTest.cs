// ============================================================
// DecisionTreeScorerSelfTest.cs — Runnable Self-Test for AA-P1-A
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Validates:
// 1. Exact mission card deliverable & test criteria:
//    - DecisionTreeScorer single O(n) math and unit test against docs/SCHEMA.md
//    - timeToDetectionMs: positive integer (e.g. 12300 or 2000)
//    - roeCompliance: 1.0 (no blackout events) or 0.0 (if violation occurred)
//    - kineticEfficiency: non-zero if trainee fired (hits / rounds)
//    - doctrinePassed: true/false consistent with doctrinal threshold comparison
// 2. End-to-end integration with TelemetryLogger + ScorecardComputer:
//    - SCENARIO_START -> SCENARIO_TICK (10 Hz NDJSON) -> SCENARIO_END
//    - Reads session NDJSON from disk and scores in <50ms (SLA <= 8s)
//    - Writes sessions/<sessionId>.scorecard.json with real values
//    - Updates sessions/manifest.json
// 3. Live WebSocket SCORECARD_PAYLOAD broadcast with real metrics
// 4. Real %AppData%\TRISHUL-SIM\sessions\ runtime verification
//
// Compiles directly with csc.exe or inside Unity Editor tests.
// ============================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TrishulSim.Network;
using TrishulSim.Scenarios;
using UnityEngine;

namespace TrishulSim.AAR
{
    public static class DecisionTreeScorerSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllAsync().GetAwaiter().GetResult();
                Console.WriteLine("\n[PASS] All AA-P1-A DecisionTreeScorer tests passed.");
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
            Console.WriteLine("=== Running TRISHUL-SIM AA-P1-A DecisionTreeScorer Self-Test ===");

            TestDecisionTreeScorerUnitTestsAndSchemaContract();
            TestPunjabScenarioEndToEndScoringAndDiskFiles();
            TestEdgeCasesAndDoctrinalBoundaryConditions();
            await TestWebSocketPayloadBroadcastWithRealMetricsAsync();
            TestRealAppDataRuntimeScoring();

            Console.WriteLine("\n=== AA-P1-A DecisionTreeScorer Self-Test COMPLETE: ALL PASS ===");
        }

        /// <summary>
        /// Validates step 3 of card AA-P1-A: DecisionTreeScorer.RunUnitTests()
        /// against docs/SCHEMA.md Entity 3 &amp; 4 example.
        /// </summary>
        public static void TestDecisionTreeScorerUnitTestsAndSchemaContract()
        {
            Console.WriteLine("\n-- Test 1: DecisionTreeScorer Schema Unit Tests --");
            DecisionTreeScorer.RunUnitTests();
            Console.WriteLine("[PASS] Test 1: DecisionTreeScorer Schema Unit Tests PASSED");
        }

        /// <summary>
        /// Validates the exact card test from missions/sohail-mission.md:
        /// "Play Punjab scenario ... Scorecard JSON:
        ///   timeToDetectionMs: positive integer (e.g. 12300)
        ///   roeCompliance: 1.0 (no blackout events)
        ///   kineticEfficiency: non-zero if trainee fired
        ///   doctrinePassed: consistent with threshold comparison"
        /// </summary>
        public static void TestPunjabScenarioEndToEndScoringAndDiskFiles()
        {
            Console.WriteLine("\n-- Test 2: Punjab Scenario End-to-End Scoring & Disk Files --");

            string tempDir = Path.Combine(Path.GetTempPath(), "TrishulSim_ScorerE2E_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                TelemetryLogger logger = new TelemetryLogger();
                logger.SessionsDirectoryOverride = tempDir;

                ScorecardComputer computer = new ScorecardComputer();
                computer.SessionsDirectoryOverride = tempDir;
                computer.Logger = logger;

                string sessionId = "sess-punjab-scorer-01";
                string scenarioId = "punjab-fog-pincer-01";

                // 1. Dispatch SCENARIO_START
                ScenarioStartPayload startPayload = new ScenarioStartPayload
                {
                    sessionId = sessionId,
                    scenarioId = scenarioId,
                    seed = 42819,
                    startedAtMs = JsonEnvelope.NowUnixMs(),
                    scenarioConfig = new ScenarioConfig
                    {
                        id = scenarioId,
                        name = "Punjab Border — Zero Visibility Pincer",
                        nameHindi = "पंजाब सीमा — शून्य दृश्यता पिंसर"
                    }
                };
                startPayload.scenarioConfig.swarmProfile.attackerCount = 4;
                startPayload.scenarioConfig.swarmProfile.decoyCount = 2;
                startPayload.scenarioConfig.swarmProfile.agentCount = 6;

                JsonEnvelope startEnv = JsonEnvelope.Create(JsonEnvelope.SCENARIO_START, startPayload.ToJson());
                logger.HandleMessageReceived(startEnv);
                computer.HandleMessageReceived(startEnv);

                // 2. Dispatch 100 ticks (10s @ 10 Hz) with realistic Punjab tactical telemetry:
                // - t=0..1900: GREEN threat
                // - t=2000: first non-GREEN threat (AMBER) -> timeToDetectionMs = 2000
                // - t=5000: RED threat
                // - t=6000..9000: 5 kinetic rounds fired, 4 hits -> efficiency = 4/5 = 0.8
                // - friendlyBlackoutRisk = false throughout -> roeCompliance = 1.0
                for (int i = 0; i < 100; i++)
                {
                    long t = i * 100;
                    ScenarioTickPayload tick = CreateRealisticPunjabTickPayload(sessionId, t);
                    logger.LogScenarioTick(tick);
                }

                // 3. Dispatch SCENARIO_END with 4 neutralised attackers
                SwarmAgentState[] finalSwarm = new SwarmAgentState[6];
                for (int a = 0; a < 6; a++)
                {
                    finalSwarm[a] = new SwarmAgentState
                    {
                        agentId = "agent-" + a,
                        isDecoy = (a == 0 || a == 1),
                        isNeutralised = (a >= 2), // 4 attackers neutralised
                        neutralisedAtMs = (a >= 2) ? (long?)(6000 + (a * 500)) : null
                    };
                }

                DateTime startCompute = DateTime.UtcNow;
                ScenarioEndPayload endPayload = new ScenarioEndPayload
                {
                    sessionId = sessionId,
                    t = 10000,
                    reason = "ALL_NEUTRALISED",
                    finalSwarmSnapshot = finalSwarm
                };
                JsonEnvelope endEnv = JsonEnvelope.Create(JsonEnvelope.SCENARIO_END, endPayload.ToJson());

                logger.HandleMessageReceived(endEnv);
                computer.HandleMessageReceived(endEnv);

                double computeElapsedSeconds = (DateTime.UtcNow - startCompute).TotalSeconds;
                Console.WriteLine(string.Format("End-to-end file read & score completed in {0:F4}s (SLA <= 10.0s)", computeElapsedSeconds));
                Assert(computeElapsedSeconds < 8.0, "Scorecard computation must complete well within SLA");

                // 4. Verify disk scorecard file
                string scorecardFile = Path.Combine(tempDir, sessionId + ".scorecard.json");
                Assert(File.Exists(scorecardFile), "Scorecard JSON file must exist on disk");

                string fileContent = File.ReadAllText(scorecardFile, Encoding.UTF8);
                Scorecard diskSc;
                string err;
                bool ok = Scorecard.TryParse(fileContent, out diskSc, out err);
                Assert(ok, "Disk scorecard file must parse cleanly: " + err);

                // Verify exact card requirements
                Console.WriteLine(string.Format("Disk Scorecard Metrics: ttd={0}ms, roe={1}, rounds={2}, hits={3}, eff={4:F2}, neutralised={5}, missed={6}, doctrinePassed={7}",
                    diskSc.metrics.timeToDetectionMs,
                    diskSc.metrics.roeCompliance,
                    diskSc.metrics.kineticRounds,
                    diskSc.metrics.kineticHits,
                    diskSc.metrics.kineticEfficiency,
                    diskSc.metrics.agentsNeutralised,
                    diskSc.metrics.agentsMissed,
                    diskSc.doctrinePassed));

                Assert(diskSc.metrics.timeToDetectionMs == 2000L, "timeToDetectionMs must be positive integer matching first non-GREEN tick (2000)");
                Assert(Math.Abs(diskSc.metrics.roeCompliance - 1.0f) < 0.001f, "roeCompliance must be 1.0 (no blackout events)");
                Assert(diskSc.metrics.kineticRounds == 5, "kineticRounds must be 5");
                Assert(diskSc.metrics.kineticHits == 4, "kineticHits must be 4");
                Assert(Math.Abs(diskSc.metrics.kineticEfficiency - 0.8f) < 0.001f, "kineticEfficiency must be 0.8");
                Assert(diskSc.metrics.agentsNeutralised == 4, "agentsNeutralised must be 4");
                Assert(diskSc.metrics.agentsMissed == 0, "agentsMissed must be 0");
                Assert(diskSc.metrics.prematureEwPenalty == false, "prematureEwPenalty must be false");
                Assert(diskSc.doctrinePassed == true, "doctrinePassed must be true (all benchmarks satisfied)");

                // Verify manifest update
                string manifestFile = Path.Combine(tempDir, "manifest.json");
                Assert(File.Exists(manifestFile), "manifest.json must exist");
                SessionManifest manifest = SessionManifest.LoadOrCreate(manifestFile);
                Assert(manifest.sessions.Count == 1, "Manifest must have 1 entry");
                Assert(manifest.sessions[0].doctrinePassed == true, "Manifest session doctrinePassed must be true");

                Console.WriteLine("[PASS] Test 2: Punjab Scenario End-to-End Scoring PASSED");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        /// <summary>
        /// Validates edge cases:
        /// 1. Zero rounds fired -> kineticEfficiency=0.0, doctrinePassed=false
        /// 2. ROE friendlyBlackoutRisk violation -> roeCompliance=0.0, doctrinePassed=false
        /// 3. Premature EW engagement in GREEN state -> prematureEwPenalty=true
        /// 4. Detection after 15s threshold -> doctrinePassed=false
        /// 5. Attacker breach (missed agents)
        /// </summary>
        public static void TestEdgeCasesAndDoctrinalBoundaryConditions()
        {
            Console.WriteLine("\n-- Test 3: Edge Cases & Doctrinal Boundary Conditions --");

            DoctrineBenchmarks bench = new DoctrineBenchmarks
            {
                timeToDetectionThresholdMs = 15000,
                roeMinCompliance = 1.0f,
                kineticMinEfficiency = 0.5f
            };
            ScenarioConfig cfg = new ScenarioConfig();
            cfg.swarmProfile.attackerCount = 4;

            // Edge Case 1: Zero rounds fired
            List<TelemetryRecord> noFireRecords = new List<TelemetryRecord>
            {
                new TelemetryRecord { t = 0, threatState = "GREEN" },
                new TelemetryRecord { t = 4000, threatState = "AMBER" }
            };
            ScorecardMetrics noFireMetrics = DecisionTreeScorer.Score(noFireRecords, null, cfg, bench);
            Assert(noFireMetrics.kineticRounds == 0, "kineticRounds must be 0");
            Assert(noFireMetrics.kineticEfficiency == 0.0f, "kineticEfficiency must be 0.0");
            Assert(DecisionTreeScorer.EvaluateDoctrine(noFireMetrics, bench, threatDetected: true) == false, "Must fail doctrine when 0 rounds fired");

            // Edge Case 2: ROE blackout violation
            List<TelemetryRecord> blackoutRecords = new List<TelemetryRecord>
            {
                new TelemetryRecord { t = 0, threatState = "GREEN" },
                new TelemetryRecord { t = 3000, threatState = "RED", ewJammerState = new EWJammerState { friendlyBlackoutRisk = true } },
                new TelemetryRecord { t = 5000, threatState = "RED", traineeAction = new TraineeAction { kinetic = new KineticEvent { hit = true } } }
            };
            ScorecardMetrics blackoutMetrics = DecisionTreeScorer.Score(blackoutRecords, null, cfg, bench);
            Assert(blackoutMetrics.roeCompliance == 0.0f, "roeCompliance must be 0.0 on blackout");
            Assert(DecisionTreeScorer.EvaluateDoctrine(blackoutMetrics, bench, threatDetected: true) == false, "Must fail doctrine on ROE violation");

            // Edge Case 3: Premature EW in GREEN state
            List<TelemetryRecord> prematureEwRecords = new List<TelemetryRecord>
            {
                new TelemetryRecord
                {
                    t = 0,
                    threatState = "GREEN",
                    traineeAction = new TraineeAction { ewEngaged = true }
                },
                new TelemetryRecord { t = 5000, threatState = "AMBER" }
            };
            ScorecardMetrics premMetrics = DecisionTreeScorer.Score(prematureEwRecords, null, cfg, bench);
            Assert(premMetrics.prematureEwPenalty == true, "prematureEwPenalty must be true when EW used in GREEN state");

            // Edge Case 4: Detection timeout (>15s)
            List<TelemetryRecord> timeoutRecords = new List<TelemetryRecord>
            {
                new TelemetryRecord { t = 0, threatState = "GREEN" },
                new TelemetryRecord { t = 14900, threatState = "GREEN" },
                new TelemetryRecord { t = 15100, threatState = "AMBER" },
                new TelemetryRecord { t = 16000, threatState = "RED", traineeAction = new TraineeAction { kinetic = new KineticEvent { hit = true } } }
            };
            ScorecardMetrics timeoutMetrics = DecisionTreeScorer.Score(timeoutRecords, null, cfg, bench);
            Assert(timeoutMetrics.timeToDetectionMs == 15100, "Detection must be 15100ms");
            Assert(DecisionTreeScorer.EvaluateDoctrine(timeoutMetrics, bench, threatDetected: true) == false, "Must fail doctrine when detection > 15000ms");

            // Edge Case 5: Empty records handled gracefully
            ScorecardMetrics emptyMetrics = DecisionTreeScorer.Score(new List<TelemetryRecord>(), null, cfg, bench);
            Assert(emptyMetrics.timeToDetectionMs == 0, "Empty records ttd must be 0");
            Assert(emptyMetrics.kineticRounds == 0, "Empty records rounds must be 0");
            Assert(DecisionTreeScorer.EvaluateDoctrine(emptyMetrics, bench, threatDetected: false) == false, "Empty records must not pass doctrine");

            Console.WriteLine("[PASS] Test 3: Edge Cases & Doctrinal Boundary Conditions PASSED");
        }

        /// <summary>
        /// Validates WebSocket SCORECARD_PAYLOAD broadcast containing real ScorecardMetrics:
        /// Trainee -> WsServer -> re-broadcast to clients.
        /// </summary>
        public static async Task TestWebSocketPayloadBroadcastWithRealMetricsAsync()
        {
            Console.WriteLine("\n-- Test 4: WebSocket SCORECARD_PAYLOAD Broadcast with Real Metrics --");

            int testPort = 9719; // isolated port
            string serverUri = "ws://127.0.0.1:" + testPort + "/";

            WsServer server = new WsServer();
            server.Port = testPort;

            WsClient client = new WsClient();
            client.ServerUri = serverUri;
            client.AutoReconnect = false;

            ScorecardComputer computer = new ScorecardComputer();

            string tempDir = Path.Combine(Path.GetTempPath(), "TrishulSim_WsRealMetrics_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                computer.SessionsDirectoryOverride = tempDir;
                bool serverStarted = server.StartServer();
                Assert(serverStarted && server.IsRunning, "WsServer must start on test port " + testPort);

                TaskCompletionSource<bool> linkReadyTcs = new TaskCompletionSource<bool>();
                TaskCompletionSource<Scorecard> clientScorecardTcs = new TaskCompletionSource<Scorecard>();

                client.OnLinkConfirmed += () => linkReadyTcs.TrySetResult(true);
                client.OnMessageReceived += env =>
                {
                    if (string.Equals(env.type, JsonEnvelope.SCORECARD_PAYLOAD, StringComparison.Ordinal))
                    {
                        Dictionary<string, JsonToken> fields;
                        string err;
                        if (JsonParser.TryParseObject(env.payload, out fields, out err))
                        {
                            string scRaw;
                            if (JsonParser.TryGetRequiredObjectRaw(fields, "scorecard", out scRaw, out err))
                            {
                                Scorecard parsedSc;
                                if (Scorecard.TryParse(scRaw, out parsedSc, out err))
                                {
                                    clientScorecardTcs.TrySetResult(parsedSc);
                                }
                            }
                        }
                    }
                };

                client.StartClient();
                Task readyDone = await Task.WhenAny(linkReadyTcs.Task, Task.Delay(3000));
                Assert(readyDone == linkReadyTcs.Task, "Client must connect and handshake with server");

                computer.Client = client;

                // Pre-create an NDJSON session with 30 ticks (3s, reaching AMBER detection at t=2000)
                string sId = "sess-ws-real-metrics-01";
                string ndjsonPath = Path.Combine(tempDir, sId + ".ndjson");
                using (StreamWriter sw = new StreamWriter(ndjsonPath, false, Encoding.UTF8))
                {
                    for (int i = 0; i < 30; i++)
                    {
                        ScenarioTickPayload tick = CreateRealisticPunjabTickPayload(sId, i * 100);
                        TelemetryRecord rec = TelemetryRecord.FromTickPayload(tick, "punjab-fog-pincer-01");
                        sw.WriteLine(rec.ToJson());
                    }
                }

                // Compute and broadcast real Scorecard
                Scorecard broadcasted = computer.ComputeScorecard(sId, "punjab-fog-pincer-01", "ALL_NEUTRALISED", 3000);
                Assert(broadcasted.metrics.timeToDetectionMs == 2000L, "Computed ttd must be 2000ms");

                Task scoreDone = await Task.WhenAny(clientScorecardTcs.Task, Task.Delay(3000));
                Assert(scoreDone == clientScorecardTcs.Task, "WsClient must receive broadcasted SCORECARD_PAYLOAD with real metrics");

                Scorecard received = await clientScorecardTcs.Task;
                Assert(received.sessionId == sId, "Received sessionId must match");
                Assert(received.metrics.timeToDetectionMs == 2000L, "Received ttd must be 2000ms");
                Assert(received.metrics.roeCompliance == 1.0f, "Received roeCompliance must be 1.0");

                Console.WriteLine("[PASS] Test 4: WebSocket SCORECARD_PAYLOAD with Real Metrics PASSED");
            }
            finally
            {
                try { client.Stop(); } catch { }
                try { server.Stop(); } catch { }
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        /// <summary>
        /// Validates runtime scoring into real %AppData%\TRISHUL-SIM\sessions\ directory.
        /// </summary>
        public static void TestRealAppDataRuntimeScoring()
        {
            Console.WriteLine("\n-- Test 5: Real %AppData%\\TRISHUL-SIM\\sessions\\ Runtime Scoring --");

            ScorecardComputer computer = new ScorecardComputer();
            computer.SessionsDirectoryOverride = null; // Use real default AppData

            string defaultDir = computer.GetSessionsDirectory();
            Assert(defaultDir.Contains("TRISHUL-SIM") && defaultDir.Contains("sessions"), "Must resolve to TRISHUL-SIM/sessions");

            string sessionId = "sess-appdata-real-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string ndjsonPath = Path.Combine(defaultDir, sessionId + ".ndjson");

            // Write 30 ticks of realistic Punjab telemetry to real AppData
            using (StreamWriter sw = new StreamWriter(ndjsonPath, false, Encoding.UTF8))
            {
                for (int i = 0; i < 30; i++)
                {
                    ScenarioTickPayload tick = CreateRealisticPunjabTickPayload(sessionId, i * 100);
                    TelemetryRecord rec = TelemetryRecord.FromTickPayload(tick, "punjab-fog-pincer-01");
                    sw.WriteLine(rec.ToJson());
                }
            }

            // Compute scorecard
            Scorecard sc = computer.ComputeScorecard(sessionId, "punjab-fog-pincer-01", "ALL_NEUTRALISED", 3000);
            string scorecardFile = Path.Combine(defaultDir, sessionId + ".scorecard.json");

            Assert(File.Exists(scorecardFile), "Real scorecard file must exist in AppData");
            Scorecard readSc = Scorecard.Parse(File.ReadAllText(scorecardFile, Encoding.UTF8));

            Assert(readSc.sessionId == sessionId, "SessionId must match in real AppData file");
            Assert(readSc.metrics.timeToDetectionMs == 2000L, "Real AppData scorecard must have real non-zero timeToDetectionMs");
            Assert(readSc.metrics.roeCompliance == 1.0f, "Real AppData scorecard roeCompliance must be 1.0");

            Console.WriteLine(string.Format("Real AppData Scorecard verified: ttd={0}ms, roe={1}, rounds={2}, doctrine={3}",
                readSc.metrics.timeToDetectionMs,
                readSc.metrics.roeCompliance,
                readSc.metrics.kineticRounds,
                readSc.doctrinePassed));

            Console.WriteLine("[PASS] Test 5: Real AppData Runtime Scoring PASSED");
        }

        private static ScenarioTickPayload CreateRealisticPunjabTickPayload(string sessionId, long elapsedMs)
        {
            // Threat state: GREEN for <2000ms, AMBER for 2000..4900ms, RED for >=5000ms
            string threat = elapsedMs < 2000 ? ScenarioTickPayload.THREAT_GREEN : (elapsedMs < 5000 ? ScenarioTickPayload.THREAT_AMBER : ScenarioTickPayload.THREAT_RED);

            SwarmAgentState[] agents = new SwarmAgentState[6];
            for (int a = 0; a < 6; a++)
            {
                agents[a] = new SwarmAgentState
                {
                    agentId = "agent-" + a,
                    isDecoy = (a == 0 || a == 1),
                    positionWorld = new Vector3(200f - (elapsedMs * 0.05f), 10f, -100f + (a * 15f)),
                    velocityMs = new Vector3(-28.0f, 0f, 5.0f),
                    headingDeg = 247.0f,
                    altitudeM = 8.5f,
                    rfEmitting = true,
                    rfFreqGHz = (a % 2 == 0) ? 5.825f : 2.4f,
                    rfPowerDbm = 23.0f,
                    propellerRpm = 11400f,
                    rcs = (a == 0 || a == 1) ? 0.08f : 0.012f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                };
            }

            // Trainee kinetic firing at specific timestamps: 6000, 6500, 7000, 7500, 8000 (5 shots, 4 hits)
            KineticEvent shot = null;
            if (elapsedMs == 6000) shot = new KineticEvent { firedAtMs = 6000, targetAgentId = "agent-2", hit = true, leadAngleDeg = 2.4f };
            else if (elapsedMs == 6500) shot = new KineticEvent { firedAtMs = 6500, targetAgentId = "agent-3", hit = true, leadAngleDeg = 2.9f };
            else if (elapsedMs == 7000) shot = new KineticEvent { firedAtMs = 7000, targetAgentId = "agent-4", hit = true, leadAngleDeg = 1.8f };
            else if (elapsedMs == 7500) shot = new KineticEvent { firedAtMs = 7500, targetAgentId = "agent-5", hit = false, leadAngleDeg = 7.1f };
            else if (elapsedMs == 8000) shot = new KineticEvent { firedAtMs = 8000, targetAgentId = "agent-5", hit = true, leadAngleDeg = 2.0f };

            bool ewActive = elapsedMs >= 4000; // activated in AMBER (not premature)

            return new ScenarioTickPayload
            {
                sessionId = sessionId,
                t = elapsedMs,
                swarmSnapshot = agents,
                sensorReadings = new SensorReadings
                {
                    rfWaterfallPeakGHz = 5.825f,
                    rfWaterfallPeakDbm = -65.0f + (elapsedMs * 0.005f),
                    dopplerPeakHz = 220.4f,
                    acousticSplDb = 45.0f + (elapsedMs * 0.01f),
                    radarDetectedCount = 4
                },
                threatState = threat,
                friendlyVhfActive = false,
                ewJammerState = new EWJammerState
                {
                    engaged = ewActive,
                    bearingDeg = 247.0f,
                    elevationDeg = -5.0f,
                    bandGHz = 5.825f,
                    friendlyBlackoutRisk = false
                },
                traineeAction = new TraineeAction
                {
                    antennaBearingDeg = 247.0f,
                    antennaElevationDeg = -5.0f,
                    ewEngaged = ewActive,
                    ewBandGHz = ewActive ? (float?)5.825f : null,
                    kinetic = shot
                }
            };
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
