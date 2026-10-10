// ============================================================
// TelemetryLoggerSelfTest.cs — Runnable Self-Test for AA-P0-A
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Validates:
// 1. Exact mission card tests & contract fidelity:
//    - TelemetryRecord schema & required fields matching telemetry.md
//    - Single-line NDJSON formatting and zero-reflection parse/serialize
// 2. 10 Hz NDJSON session lifecycle:
//    - SCENARIO_START creates %AppData%\TRISHUL-SIM\sessions\<sessionId>.ndjson
//    - 100 SCENARIO_TICK payloads (10s @ 10 Hz) appended with monotonic t (+100ms)
//    - 6-Boid swarm snapshot serialized on every line
//    - SCENARIO_END flushes and cleanly closes the file
// 3. WsClient envelope handling (SCENARIO_START, TICK, END)
// 4. TraineeActionInputStub integration (WASD bearing/elevation, kinetic shot)
// 5. 10-tick flush cadence & Explorer size growth
// 6. Real %AppData%\TRISHUL-SIM\sessions\ runtime verification
//
// Compiles directly with csc.exe or inside Unity Editor tests.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TrishulSim.Network;
using TrishulSim.Scenarios;
using UnityEngine;

namespace TrishulSim.AAR
{
    public static class TelemetryLoggerSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllTests();
                Console.WriteLine("\n[PASS] All AA-P0-A TelemetryLogger tests passed.");
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
            Console.WriteLine("=== Running TRISHUL-SIM AA-P0-A TelemetryLogger Self-Test ===");

            TestTelemetryRecordContractFidelity();
            TestTelemetryLoggerSessionLifecycle10Hz();
            TestWsClientEnvelopeDispatch();
            TestTraineeActionInputStubIntegration();
            TestFlushCadence();
            TestDefaultAppDataPathAndRuntimeFileCreation();
            TestPlayPunjabScenario30Seconds();

            Console.WriteLine("\n=== AA-P0-A TelemetryLogger Self-Test COMPLETE: ALL PASS ===");
        }

        /// <summary>
        /// Validates TelemetryRecord serialization and parsing against board/contracts/telemetry.md example.
        /// </summary>
        public static void TestTelemetryRecordContractFidelity()
        {
            Console.WriteLine("\n-- Test 1: TelemetryRecord Contract Fidelity --");

            // Build record matching telemetry.md lines 75-121
            TelemetryRecord record = new TelemetryRecord
            {
                t = 45200,
                sessionId = "sess-20261004-143201",
                scenarioId = "punjab-fog-pincer-01",
                threatState = "RED",
                friendlyVhfActive = false,
                traineeAction = new TraineeAction
                {
                    antennaBearingDeg = 247.0f,
                    antennaElevationDeg = -5.0f,
                    ewEngaged = true,
                    ewBandGHz = 5.825f,
                    kinetic = null
                },
                sensorReadings = new SensorReadings
                {
                    rfWaterfallPeakGHz = 5.825f,
                    rfWaterfallPeakDbm = -62.3f,
                    dopplerPeakHz = 220.4f,
                    acousticSplDb = 54.2f,
                    radarDetectedCount = 4
                },
                ewJammerState = new EWJammerState
                {
                    engaged = true,
                    bearingDeg = 247.0f,
                    elevationDeg = -5.0f,
                    bandGHz = 5.825f,
                    friendlyBlackoutRisk = false
                },
                swarmSnapshot = new SwarmAgentState[]
                {
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
                        propellerRpm = 11400f,
                        rcs = 0.012f,
                        isNeutralised = false,
                        neutralisedAtMs = null
                    }
                }
            };

            string json = record.ToJson();
            Assert(!string.IsNullOrEmpty(json), "Serialized JSON must not be empty");
            Assert(!json.Contains("\n") && !json.Contains("\r"), "NDJSON record must be a single line");

            // Required field presence assertions
            Assert(json.Contains("\"t\":45200"), "JSON must contain 't':45200");
            Assert(json.Contains("\"sessionId\":\"sess-20261004-143201\""), "JSON must contain sessionId");
            Assert(json.Contains("\"scenarioId\":\"punjab-fog-pincer-01\""), "JSON must contain scenarioId");
            Assert(json.Contains("\"traineeAction\":"), "JSON must contain traineeAction");
            Assert(json.Contains("\"swarmSnapshot\":"), "JSON must contain swarmSnapshot");
            Assert(json.Contains("\"sensorReadings\":"), "JSON must contain sensorReadings");
            Assert(json.Contains("\"threatState\":\"RED\""), "JSON must contain threatState");
            Assert(json.Contains("\"friendlyVhfActive\":false"), "JSON must contain friendlyVhfActive");
            Assert(json.Contains("\"ewJammerState\":"), "JSON must contain ewJammerState");

            // Parse back
            TelemetryRecord parsed;
            string error;
            bool ok = TelemetryRecord.TryParse(json, out parsed, out error);
            Assert(ok, "TelemetryRecord.TryParse must succeed: " + error);
            Assert(parsed != null, "Parsed record must not be null");

            Assert(parsed.t == 45200, "Parsed t must match 45200");
            Assert(parsed.sessionId == "sess-20261004-143201", "Parsed sessionId must match");
            Assert(parsed.scenarioId == "punjab-fog-pincer-01", "Parsed scenarioId must match");
            Assert(parsed.threatState == "RED", "Parsed threatState must match RED");
            Assert(parsed.friendlyVhfActive == false, "Parsed friendlyVhfActive must be false");
            Assert(Math.Abs(parsed.traineeAction.antennaBearingDeg - 247.0f) < 0.01f, "Antenna bearing must match");
            Assert(parsed.traineeAction.ewEngaged == true, "ewEngaged must be true");
            Assert(parsed.traineeAction.ewBandGHz.HasValue && Math.Abs(parsed.traineeAction.ewBandGHz.Value - 5.825f) < 0.01f, "ewBandGHz must match 5.825");
            Assert(parsed.traineeAction.kinetic == null, "kinetic must be null");
            Assert(parsed.swarmSnapshot != null && parsed.swarmSnapshot.Length == 1, "swarmSnapshot must have 1 agent");
            Assert(parsed.swarmSnapshot[0].agentId == "agent-3", "agentId must be agent-3");
            Assert(parsed.swarmSnapshot[0].isNeutralised == false, "isNeutralised must be false");
            Assert(parsed.sensorReadings != null && parsed.sensorReadings.radarDetectedCount == 4, "radarDetectedCount must be 4");

            Console.WriteLine("[PASS] TelemetryRecord Contract Fidelity PASSED");
        }

        /// <summary>
        /// Validates 10-second scenario execution at 10 Hz (100 ticks) with monotonic timestamps and 6-agent swarm.
        /// </summary>
        public static void TestTelemetryLoggerSessionLifecycle10Hz()
        {
            Console.WriteLine("\n-- Test 2: 10 Hz NDJSON Session Lifecycle (100 ticks / 10s) --");

            string tempDir = Path.Combine(Path.GetTempPath(), "TrishulSim_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                TelemetryLogger logger = new TelemetryLogger();
                logger.SessionsDirectoryOverride = tempDir;

                string sessionId = "sess-20261008-p0-test";
                string scenarioId = "punjab-fog-pincer-01";

                logger.StartSession(sessionId, scenarioId);
                Assert(logger.IsLogging, "Logger must be in logging state");

                string expectedFile = Path.Combine(tempDir, sessionId + ".ndjson");
                Assert(File.Exists(expectedFile), "Session NDJSON file must exist after StartSession");

                // Simulate 10 seconds of Punjab scenario ticks at 10 Hz (100 ticks)
                for (int i = 0; i < 100; i++)
                {
                    long elapsedMs = i * 100;
                    ScenarioTickPayload tick = CreatePunjabTickPayload(sessionId, elapsedMs);
                    logger.LogScenarioTick(tick);
                }

                logger.EndSession("ALL_NEUTRALISED");
                Assert(!logger.IsLogging, "Logger must be closed after EndSession");
                Assert(logger.TotalTicksWritten == 100, "Logger must report 100 ticks written");

                // Verify written file
                string[] lines = File.ReadAllLines(expectedFile, Encoding.UTF8);
                Console.WriteLine("Written lines count: " + lines.Length + " (expected: 100)");
                Assert(lines.Length == 100, "Expected exactly 100 NDJSON lines for 10s simulation");

                long lastT = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    Assert(!string.IsNullOrWhiteSpace(line), "Line " + i + " must not be empty");

                    TelemetryRecord tr;
                    string err;
                    bool ok = TelemetryRecord.TryParse(line, out tr, out err);
                    Assert(ok, "Line " + i + " must parse cleanly: " + err);
                    Assert(tr.sessionId == sessionId, "Line " + i + " sessionId must match");
                    Assert(tr.scenarioId == scenarioId, "Line " + i + " scenarioId must match");
                    Assert(tr.t == i * 100, "Line " + i + " t must be " + (i * 100) + " but was " + tr.t);
                    Assert(tr.t > lastT, "t must be strictly increasing");
                    lastT = tr.t;

                    Assert(tr.swarmSnapshot != null && tr.swarmSnapshot.Length == 6, "Line " + i + " must contain 6 swarm agents");
                }

                Console.WriteLine("[PASS] 10 Hz NDJSON Session Lifecycle PASSED");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        /// <summary>
        /// Validates WsClient envelope handling for SCENARIO_START, SCENARIO_TICK, and SCENARIO_END.
        /// </summary>
        public static void TestWsClientEnvelopeDispatch()
        {
            Console.WriteLine("\n-- Test 3: WsClient Envelope Dispatch --");

            string tempDir = Path.Combine(Path.GetTempPath(), "TrishulSim_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                TelemetryLogger logger = new TelemetryLogger();
                logger.SessionsDirectoryOverride = tempDir;

                string sessionId = "sess-ws-dispatch-01";
                string scenarioId = "punjab-fog-pincer-01";

                // 1. Dispatch SCENARIO_START envelope
                ScenarioStartPayload startPayload = new ScenarioStartPayload
                {
                    sessionId = sessionId,
                    scenarioId = scenarioId,
                    seed = 42819,
                    startedAtMs = JsonEnvelope.NowUnixMs()
                };
                JsonEnvelope startEnv = JsonEnvelope.Create(JsonEnvelope.SCENARIO_START, startPayload.ToJson());
                logger.HandleMessageReceived(startEnv);

                Assert(logger.IsLogging, "Logger must be logging after SCENARIO_START envelope");
                Assert(logger.CurrentSessionId == sessionId, "CurrentSessionId must match");

                // 2. Dispatch 20 SCENARIO_TICK envelopes
                for (int i = 0; i < 20; i++)
                {
                    ScenarioTickPayload tick = CreatePunjabTickPayload(sessionId, i * 100);
                    JsonEnvelope tickEnv = JsonEnvelope.Create(JsonEnvelope.SCENARIO_TICK, tick.ToJson());
                    logger.HandleMessageReceived(tickEnv);
                }

                // 3. Dispatch SCENARIO_END envelope
                ScenarioEndPayload endPayload = new ScenarioEndPayload
                {
                    sessionId = sessionId,
                    t = 2000,
                    reason = "TIME_LIMIT",
                    finalSwarmSnapshot = new SwarmAgentState[0]
                };
                JsonEnvelope endEnv = JsonEnvelope.Create(JsonEnvelope.SCENARIO_END, endPayload.ToJson());
                logger.HandleMessageReceived(endEnv);

                Assert(!logger.IsLogging, "Logger must be closed after SCENARIO_END envelope");
                Assert(logger.TotalTicksWritten == 20, "Total ticks written must be 20");

                string filePath = Path.Combine(tempDir, sessionId + ".ndjson");
                Assert(File.Exists(filePath), "Telemetry file must exist");
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                Assert(lines.Length == 20, "Lines count must be 20");

                Console.WriteLine("[PASS] WsClient Envelope Dispatch PASSED");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        /// <summary>
        /// Validates TraineeActionInputStub keyboard/programmatic input integration.
        /// </summary>
        public static void TestTraineeActionInputStubIntegration()
        {
            Console.WriteLine("\n-- Test 4: TraineeActionInputStub Integration --");

            string tempDir = Path.Combine(Path.GetTempPath(), "TrishulSim_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                TelemetryLogger logger = new TelemetryLogger();
                logger.SessionsDirectoryOverride = tempDir;

                TraineeActionInputStub stub = new TraineeActionInputStub();
                stub.Awake();
                logger.InputStub = stub;

                string sessionId = "sess-stub-test-01";
                logger.StartSession(sessionId, "punjab-fog-pincer-01");

                // Tick 1: Default state (247° bearing, -5° elevation, ewEngaged=false)
                ScenarioTickPayload tick1 = CreatePunjabTickPayload(sessionId, 0);
                logger.LogScenarioTick(tick1);

                // Tick 2: Trainee rotates antenna to 90° azimuth and engages EW on 5.825 GHz
                stub.SetAntennaOrientation(90.0f, 10.0f);
                stub.SetJammerState(true, 5.825f);
                ScenarioTickPayload tick2 = CreatePunjabTickPayload(sessionId, 100);
                logger.LogScenarioTick(tick2);

                // Tick 3: Trainee pulls trigger on SMASH 2000
                stub.TriggerKineticShot("agent-3", true, 3.2f, 200);
                ScenarioTickPayload tick3 = CreatePunjabTickPayload(sessionId, 200);
                logger.LogScenarioTick(tick3);

                // Tick 4: Trainee releases trigger
                stub.ClearKineticShot();
                ScenarioTickPayload tick4 = CreatePunjabTickPayload(sessionId, 300);
                logger.LogScenarioTick(tick4);

                logger.EndSession("NORMAL");

                string filePath = Path.Combine(tempDir, sessionId + ".ndjson");
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                Assert(lines.Length == 4, "Must have written 4 records");

                TelemetryRecord r1 = TelemetryRecord.Parse(lines[0]);
                Assert(Math.Abs(r1.traineeAction.antennaBearingDeg - 247.0f) < 0.01f, "r1 bearing must be 247");
                Assert(r1.traineeAction.ewEngaged == false, "r1 ewEngaged must be false");
                Assert(r1.traineeAction.kinetic == null, "r1 kinetic must be null");

                TelemetryRecord r2 = TelemetryRecord.Parse(lines[1]);
                Assert(Math.Abs(r2.traineeAction.antennaBearingDeg - 90.0f) < 0.01f, "r2 bearing must be 90");
                Assert(r2.traineeAction.ewEngaged == true, "r2 ewEngaged must be true");
                Assert(r2.traineeAction.ewBandGHz.HasValue && Math.Abs(r2.traineeAction.ewBandGHz.Value - 5.825f) < 0.01f, "r2 ewBandGHz must be 5.825");

                TelemetryRecord r3 = TelemetryRecord.Parse(lines[2]);
                Assert(r3.traineeAction.kinetic != null, "r3 kinetic must not be null");
                Assert(r3.traineeAction.kinetic.targetAgentId == "agent-3", "r3 targetAgentId must be agent-3");
                Assert(r3.traineeAction.kinetic.hit == true, "r3 hit must be true");
                Assert(Math.Abs(r3.traineeAction.kinetic.leadAngleDeg - 3.2f) < 0.01f, "r3 leadAngle must be 3.2");

                TelemetryRecord r4 = TelemetryRecord.Parse(lines[3]);
                Assert(r4.traineeAction.kinetic == null, "r4 kinetic must be cleared back to null");

                Console.WriteLine("[PASS] TraineeActionInputStub Integration PASSED");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        /// <summary>
        /// Validates 10-tick flush cadence.
        /// </summary>
        public static void TestFlushCadence()
        {
            Console.WriteLine("\n-- Test 5: Flush Cadence (every 10 ticks / 1 s) --");

            string tempDir = Path.Combine(Path.GetTempPath(), "TrishulSim_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                TelemetryLogger logger = new TelemetryLogger();
                logger.SessionsDirectoryOverride = tempDir;

                string sessionId = "sess-flush-cadence-01";
                logger.StartSession(sessionId, "punjab-fog-pincer-01");

                string filePath = Path.Combine(tempDir, sessionId + ".ndjson");

                // Write 9 ticks (not yet flushed)
                for (int i = 0; i < 9; i++)
                {
                    logger.LogScenarioTick(CreatePunjabTickPayload(sessionId, i * 100));
                }

                // Write 10th tick -> triggers auto flush
                logger.LogScenarioTick(CreatePunjabTickPayload(sessionId, 900));

                FileInfo info = new FileInfo(filePath);
                Assert(info.Length > 0, "File size must be > 0 bytes after 10th tick flush");

                logger.EndSession("DONE");

                Console.WriteLine("[PASS] Flush Cadence PASSED");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        /// <summary>
        /// Validates runtime file creation in real %AppData%\TRISHUL-SIM\sessions\ directory.
        /// </summary>
        public static void TestDefaultAppDataPathAndRuntimeFileCreation()
        {
            Console.WriteLine("\n-- Test 6: Real %AppData%\\TRISHUL-SIM\\sessions\\ Runtime File Creation --");

            TelemetryLogger logger = new TelemetryLogger();
            logger.SessionsDirectoryOverride = null; // Use real default AppData

            string expectedDir = logger.GetSessionsDirectory();
            Console.WriteLine("Resolved default sessions directory: " + expectedDir);
            Assert(expectedDir.Contains("TRISHUL-SIM") && expectedDir.Contains("sessions"), "Directory must contain TRISHUL-SIM/sessions");

            string testSessionId = "sess-appdata-verify-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            logger.StartSession(testSessionId, "punjab-fog-pincer-01");

            string realFilePath = logger.CurrentSessionFilePath;
            Console.WriteLine("Real session file path: " + realFilePath);
            Assert(File.Exists(realFilePath), "Real file in AppData must exist after StartSession");

            // Write 10 ticks
            for (int i = 0; i < 10; i++)
            {
                logger.LogScenarioTick(CreatePunjabTickPayload(testSessionId, i * 100));
            }

            logger.EndSession("VERIFIED");

            FileInfo info = new FileInfo(realFilePath);
            Console.WriteLine("Real file created on disk: " + realFilePath + " (Size: " + info.Length + " bytes)");
            Assert(info.Exists && info.Length > 0, "Real file must exist with non-zero size");

            string[] lines = File.ReadAllLines(realFilePath, Encoding.UTF8);
            Assert(lines.Length == 10, "Real file must contain 10 lines");

            Console.WriteLine("[PASS] Real AppData Runtime File Creation PASSED");
        }

        /// <summary>
        /// Exact mission test for AA-P0-A:
        /// "Play Punjab scenario 30s -> navigate to %AppData%\TRISHUL-SIM\sessions\
        /// File exists and grows (Explorer shows increasing Size).
        /// Open in Notepad: each line is valid JSON; line count after 10s ~ 100.
        /// Fields: t (increasing by ~100), sessionId (matches), swarmSnapshot (array)."
        /// </summary>
        public static void TestPlayPunjabScenario30Seconds()
        {
            Console.WriteLine("\n-- Test 7: Exact Card Test — Play Punjab Scenario 30s (300 ticks) --");

            TelemetryLogger logger = new TelemetryLogger();
            logger.SessionsDirectoryOverride = null; // %AppData%\TRISHUL-SIM\sessions\

            string sessionId = "sess-punjab-30s-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string scenarioId = "punjab-fog-pincer-01";

            logger.StartSession(sessionId, scenarioId);
            string sessionPath = logger.CurrentSessionFilePath;
            Console.WriteLine("Punjab 30s session path: " + sessionPath);

            long lastReportedSize = 0;
            // 30 seconds at 10 Hz = 300 ticks
            for (int i = 0; i < 300; i++)
            {
                long elapsedMs = i * 100;
                ScenarioTickPayload tick = CreatePunjabTickPayload(sessionId, elapsedMs);
                logger.LogScenarioTick(tick);

                // Every 5 seconds (50 ticks), report file size growth (matching Explorer observation test)
                if ((i + 1) % 50 == 0)
                {
                    FileInfo fi = new FileInfo(sessionPath);
                    fi.Refresh();
                    Console.WriteLine(string.Format("T+{0}s (tick {1}/300): File size = {2} bytes (grew by +{3} bytes)",
                        (i + 1) / 10, i + 1, fi.Length, fi.Length - lastReportedSize));
                    Assert(fi.Length > lastReportedSize, "File size must increase as scenario progresses");
                    lastReportedSize = fi.Length;
                }
            }

            logger.EndSession("TIME_LIMIT");
            Assert(logger.TotalTicksWritten == 300, "Must have written exactly 300 ticks for 30s scenario");

            FileInfo finalInfo = new FileInfo(sessionPath);
            Console.WriteLine("Final Punjab 30s file size: " + finalInfo.Length + " bytes");
            Assert(finalInfo.Length > 0, "Final file size must be non-zero");

            string[] allLines = File.ReadAllLines(sessionPath, Encoding.UTF8);
            Console.WriteLine("Total lines in file: " + allLines.Length + " (expected: 300)");
            Assert(allLines.Length == 300, "Total lines after 30s must be exactly 300");

            // Line count after 10s should correspond to index 100
            TelemetryRecord rec10s = TelemetryRecord.Parse(allLines[99]);
            Console.WriteLine(string.Format("Check record at 10s (line 100): t={0}ms, sessionId={1}, agents={2}",
                rec10s.t, rec10s.sessionId, rec10s.swarmSnapshot.Length));
            Assert(rec10s.t == 9900, "Record at line 100 must be t=9900 (9.9s)");
            Assert(rec10s.sessionId == sessionId, "SessionId must match");
            Assert(rec10s.swarmSnapshot != null && rec10s.swarmSnapshot.Length == 6, "swarmSnapshot must have 6 agents");

            Console.WriteLine("[PASS] Exact Card Test — Play Punjab Scenario 30s PASSED");
        }

        public static ScenarioTickPayload CreatePunjabTickPayload(string sessionId, long elapsedMs)
        {
            string threat = elapsedMs < 2000 ? "GREEN" : (elapsedMs < 5000 ? "AMBER" : "RED");

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
                    engaged = elapsedMs >= 4000,
                    bearingDeg = 247.0f,
                    elevationDeg = -5.0f,
                    bandGHz = 5.825f,
                    friendlyBlackoutRisk = false
                },
                traineeAction = new TraineeAction
                {
                    antennaBearingDeg = 247.0f,
                    antennaElevationDeg = -5.0f,
                    ewEngaged = elapsedMs >= 4000,
                    ewBandGHz = elapsedMs >= 4000 ? (float?)5.825f : null,
                    kinetic = null
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
