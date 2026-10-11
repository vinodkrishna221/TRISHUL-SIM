// ============================================================
// AarPartialGuardSelfTest.cs — Runnable Self-Test for AA-P3-A
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Validates:
// 1. TelemetryLogger: wrap writer.WriteLine in try/catch IOException:
//    - On IOException: sets partialFlag = true
//    - Logs exact warning: "IOException during telemetry write — continuing in partial mode"
//    - Scenario continues without aborting
// 2. ScorecardComputer: if NDJSON size == 0 or read fails:
//    - Broadcasts WS ERROR {code:"SCORECARD_COMPUTE_FAILED", message:"Session file corrupt or missing.", fatal:false}
//    - Emits graceful partial Scorecard (partial: true)
// 3. SessionManifest: records partial: true for failed sessions
// 4. On startup: scan manifest for sessions missing .scorecard.json;
//    - Logs orphaned sessions to Console without re-computing
// 5. Idempotent %AppData%\TRISHUL-SIM\sessions\ directory creation
// 6. Clean recovery: subsequent normal run works normally without errors
//
// Compiles directly with csc.exe or inside Unity Editor tests.
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TrishulSim.Network;
using TrishulSim.PDF;
using TrishulSim.Scenarios;
using UnityEngine;

namespace TrishulSim.AAR
{
    public static class AarPartialGuardSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            // Register assembly resolver to locate itextsharp.dll in Assets\Plugins\
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                string asmName = new AssemblyName(resolveArgs.Name).Name;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string p1 = Path.Combine(baseDir, "Assets", "Plugins", asmName + ".dll");
                if (File.Exists(p1)) return Assembly.LoadFrom(p1);
                string p2 = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Plugins", asmName + ".dll");
                if (File.Exists(p2)) return Assembly.LoadFrom(p2);
                string p3 = Path.Combine(baseDir, asmName + ".dll");
                if (File.Exists(p3)) return Assembly.LoadFrom(p3);
                return null;
            };

            try
            {
                RunAllAsync().GetAwaiter().GetResult();
                Console.WriteLine("\n[PASS] All AA-P3-A Partial-File Guard & Session Manifest Hardening tests passed.");
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
            Console.WriteLine("=== Running TRISHUL-SIM AA-P3-A Partial-File Guard & Manifest Hardening Self-Test ===");

            string testRoot = Path.Combine(Path.GetTempPath(), "TrishulSim_PartialGuard_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);

            try
            {
                TestDirectoryCreationIdempotency(testRoot);
                TestTelemetryLoggerDiskFailureSimulation(testRoot);
                TestScorecardComputerZeroByteNdjson(testRoot);
                TestScorecardComputerMissingNdjson(testRoot);
                TestStartupOrphanedSessionsScan(testRoot);
                TestSimulatedWriteProtectedStorageAndRecovery(testRoot);
                await TestLiveWsErrorBroadcastAsync(testRoot);

                Console.WriteLine("\nAll 7 test suites completed successfully.");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(testRoot))
                    {
                        NormalizeDirectoryAttributes(testRoot);
                        Directory.Delete(testRoot, true);
                    }
                }
                catch { }
            }
        }

        private static void NormalizeDirectoryAttributes(string dir)
        {
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(dir);
                dirInfo.Attributes = FileAttributes.Normal;
                foreach (FileInfo file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    file.Attributes = FileAttributes.Normal;
                }
                foreach (DirectoryInfo subDir in dirInfo.GetDirectories("*", SearchOption.AllDirectories))
                {
                    subDir.Attributes = FileAttributes.Normal;
                }
            }
            catch { }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Assertion failed: " + message);
            }
        }

        private sealed class LogWarningScope : IDisposable
        {
            private readonly Action<string> _handler;
#if UNITY_5_3_OR_NEWER
            private readonly Application.LogCallback _callback;
#endif

            public LogWarningScope(Action<string> handler)
            {
                _handler = handler;
#if !UNITY_5_3_OR_NEWER
                Debug.OnLogWarning += _handler;
#else
                _callback = (msg, stack, type) =>
                {
                    if (type == LogType.Warning && _handler != null)
                    {
                        _handler(msg);
                    }
                };
                Application.logMessageReceived += _callback;
#endif
            }

            public void Dispose()
            {
#if !UNITY_5_3_OR_NEWER
                Debug.OnLogWarning -= _handler;
#else
                Application.logMessageReceived -= _callback;
#endif
            }
        }

        /// <summary>
        /// Suite 1: Verifies EnsureSessionsDirectoryExists is completely idempotent and race-condition free.
        /// </summary>
        private static void TestDirectoryCreationIdempotency(string root)
        {
            Console.WriteLine("\n--- Suite 1: Directory Creation Idempotency (AA-P3-A Step 5) ---");

            string targetDir = Path.Combine(root, "idempotent_sessions");
            Assert(!Directory.Exists(targetDir), "Target directory should not exist before creation.");

            bool r1 = ScorecardComputer.EnsureSessionsDirectoryExists(targetDir);
            Assert(r1 && Directory.Exists(targetDir), "First EnsureSessionsDirectoryExists must create directory.");

            // Repeated calls
            bool r2 = ScorecardComputer.EnsureSessionsDirectoryExists(targetDir);
            bool r3 = ScorecardComputer.EnsureSessionsDirectoryExists(targetDir);
            Assert(r2 && r3, "Subsequent calls to EnsureSessionsDirectoryExists must succeed without error.");

            // Multi-threaded concurrent creation stress test
            List<Task<bool>> tasks = new List<Task<bool>>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(() => ScorecardComputer.EnsureSessionsDirectoryExists(targetDir)));
            }
            Task.WaitAll(tasks.ToArray());
            foreach (var t in tasks)
            {
                Assert(t.Result, "Concurrent call to EnsureSessionsDirectoryExists must return true.");
            }

            Console.WriteLine("  [OK] Idempotent directory creation confirmed under sequential and concurrent calls.");
        }

        /// <summary>
        /// Suite 2: Verifies TelemetryLogger catches IOException on write, sets partialFlag,
        /// logs exact warning, and continues scenario without throwing or crashing.
        /// </summary>
        private static void TestTelemetryLoggerDiskFailureSimulation(string root)
        {
            Console.WriteLine("\n--- Suite 2: TelemetryLogger IOException Catch & Partial Flag (AA-P3-A Step 1) ---");

            string sessionsDir = Path.Combine(root, "logger_sessions");
            Directory.CreateDirectory(sessionsDir);

            TelemetryLogger logger = new TelemetryLogger();
            logger.SessionsDirectoryOverride = sessionsDir;

            string capturedWarning = null;
            Action<string> logWarningHandler = msg =>
            {
                if (msg != null && msg.Contains("IOException during telemetry write — continuing in partial mode"))
                {
                    capturedWarning = msg;
                }
            };
            var warningScope = new LogWarningScope(logWarningHandler);

            try
            {
                string sessionId = "sess-partial-test-01";
                logger.StartSession(sessionId, "punjab-fog-pincer-01");
                Assert(!logger.IsPartial, "New session must initialize with partialFlag = false.");
                Assert(!logger.PartialFlag, "PartialFlag property must match IsPartial.");

                // Log tick 1 normally
                ScenarioTickPayload tick1 = CreateMockTick(sessionId, 100);
                logger.LogScenarioTick(tick1);
                Assert(logger.TotalTicksWritten == 1, "First tick must write successfully.");
                Assert(!logger.IsPartial, "Session must still be non-partial after normal write.");

                // Simulate disk write failure (write-protection / disk full simulation)
                logger.SimulateDiskWriteFailure = true;

                ScenarioTickPayload tick2 = CreateMockTick(sessionId, 200);
                // Must NOT throw exception — must continue in partial mode
                logger.LogScenarioTick(tick2);

                Assert(logger.IsPartial, "partialFlag must be set to true on IOException.");
                Assert(logger.PartialFlag, "PartialFlag must return true.");
                Assert(capturedWarning != null, "Console must log 'IOException during telemetry write — continuing in partial mode'.");
                Console.WriteLine("  Verified logged warning: " + capturedWarning);

                // Subsequent tick while in partial mode
                ScenarioTickPayload tick3 = CreateMockTick(sessionId, 300);
                logger.LogScenarioTick(tick3);
                Assert(logger.IsPartial, "Session remains partial for remainder of run.");

                // Conclude session gracefully
                logger.EndSession("NORMAL");
                Assert(!logger.IsLogging, "Session must close cleanly without crashing.");

                string ndjsonPath = Path.Combine(sessionsDir, sessionId + ".ndjson");
                Assert(File.Exists(ndjsonPath), "NDJSON file must exist on disk.");
                string[] lines = File.ReadAllLines(ndjsonPath);
                Assert(lines.Length == 1, "Only tick 1 should be committed; subsequent ticks were caught as partial.");

                Console.WriteLine("  [OK] TelemetryLogger partialFlag=true and IOException graceful continuation verified.");
            }
            finally
            {
                warningScope.Dispose();
            }
        }

        /// <summary>
        /// Suite 3: Verifies ScorecardComputer detects 0-byte NDJSON, broadcasts WS ERROR
        /// {code:"SCORECARD_COMPUTE_FAILED", message:"Session file corrupt or missing.", fatal:false},
        /// marks Scorecard partial: true, and writes manifest partial: true.
        /// </summary>
        private static void TestScorecardComputerZeroByteNdjson(string root)
        {
            Console.WriteLine("\n--- Suite 3: ScorecardComputer 0-Byte NDJSON Detection & WS ERROR (AA-P3-A Step 2 & 3) ---");

            string sessionsDir = Path.Combine(root, "zerobyte_sessions");
            Directory.CreateDirectory(sessionsDir);

            string sessionId = "sess-zero-byte-01";
            string ndjsonPath = Path.Combine(sessionsDir, sessionId + ".ndjson");
            // Create an empty (0 bytes) file
            File.WriteAllText(ndjsonPath, string.Empty);
            Assert(new FileInfo(ndjsonPath).Length == 0, "File must be 0 bytes.");

            ScorecardComputer computer = new ScorecardComputer();
            computer.SessionsDirectoryOverride = sessionsDir;

            JsonEnvelope? capturedError = null;
            computer.OnErrorBroadcast += env => capturedError = env;

            Scorecard scorecard = computer.ComputeScorecard(sessionId, "punjab-fog-pincer-01", "TIME_LIMIT", 55000);

            Assert(scorecard != null, "ComputeScorecard must return a graceful partial Scorecard object.");
            Assert(scorecard.partial == true, "Scorecard.partial must be true for 0-byte NDJSON.");
            Assert(scorecard.doctrinePassed == false, "Scorecard.doctrinePassed must be false for failed/partial session.");

            // Check WS ERROR broadcast
            Assert(capturedError.HasValue, "WS ERROR envelope must be broadcast.");
            JsonEnvelope errorEnv = capturedError.Value;
            Assert(string.Equals(errorEnv.type, JsonEnvelope.ERROR, StringComparison.Ordinal), "Envelope type must be 'ERROR'.");
            Assert(errorEnv.v == 1, "Envelope version must be 1.");

            // Parse error payload
            Dictionary<string, JsonToken> errorFields;
            string parseErr;
            Assert(JsonParser.TryParseObject(errorEnv.payload, out errorFields, out parseErr), "Error payload must be valid JSON: " + parseErr);
            string errCode;
            Assert(JsonParser.TryGetRequiredString(errorFields, "code", out errCode, out parseErr), "Payload must have 'code'.");
            Assert(string.Equals(errCode, "SCORECARD_COMPUTE_FAILED", StringComparison.Ordinal), "Error code must be SCORECARD_COMPUTE_FAILED.");

            string errMsg;
            Assert(JsonParser.TryGetRequiredString(errorFields, "message", out errMsg, out parseErr), "Payload must have 'message'.");
            Assert(string.Equals(errMsg, "Session file corrupt or missing.", StringComparison.Ordinal), "Message must be 'Session file corrupt or missing.'.");

            bool isFatal;
            Assert(JsonParser.TryGetRequiredBool(errorFields, "fatal", out isFatal, out parseErr), "Payload must have 'fatal'.");
            Assert(isFatal == false, "fatal must be false (warning only).");

            // Verify manifest.json has partial: true
            string manifestPath = Path.Combine(sessionsDir, "manifest.json");
            Assert(File.Exists(manifestPath), "manifest.json must exist.");
            SessionManifest manifest = SessionManifest.LoadOrCreate(manifestPath);
            SessionSummary summary = manifest.sessions.Find(s => string.Equals(s.sessionId, sessionId, StringComparison.Ordinal));
            Assert(summary != null, "Session must be present in manifest.json.");
            Assert(summary.partial == true, "SessionSummary.partial in manifest.json must be true.");

            Console.WriteLine("  [OK] 0-byte NDJSON detected, WS ERROR broadcast verified, manifest partial:true verified.");
        }

        /// <summary>
        /// Suite 4: Verifies ScorecardComputer handles completely missing session NDJSON.
        /// </summary>
        private static void TestScorecardComputerMissingNdjson(string root)
        {
            Console.WriteLine("\n--- Suite 4: ScorecardComputer Missing NDJSON Handling (AA-P3-A Step 2 & 3) ---");

            string sessionsDir = Path.Combine(root, "missing_sessions");
            Directory.CreateDirectory(sessionsDir);

            string sessionId = "sess-nonexistent-01";
            ScorecardComputer computer = new ScorecardComputer();
            computer.SessionsDirectoryOverride = sessionsDir;

            JsonEnvelope? capturedError = null;
            computer.OnErrorBroadcast += env => capturedError = env;

            Scorecard scorecard = computer.ComputeScorecard(sessionId, "punjab-fog-pincer-01", "ABORT", 12000);

            Assert(scorecard != null, "Scorecard must be returned.");
            Assert(scorecard.partial == true, "Scorecard.partial must be true when NDJSON is missing.");
            Assert(capturedError.HasValue, "WS ERROR must be broadcast when file is missing.");
            Assert(capturedError.Value.payload.Contains("SCORECARD_COMPUTE_FAILED"), "Error code must be SCORECARD_COMPUTE_FAILED.");

            SessionManifest manifest = SessionManifest.LoadOrCreate(Path.Combine(sessionsDir, "manifest.json"));
            SessionSummary summary = manifest.sessions.Find(s => string.Equals(s.sessionId, sessionId, StringComparison.Ordinal));
            Assert(summary != null && summary.partial == true, "Session manifest must record partial: true.");

            Console.WriteLine("  [OK] Missing NDJSON handled with SCORECARD_COMPUTE_FAILED broadcast and manifest partial:true.");
        }

        /// <summary>
        /// Suite 5: Verifies startup scan for sessions missing .scorecard.json (AA-P3-A Step 4).
        /// Logs orphaned sessions to Console without attempting recomputation.
        /// </summary>
        private static void TestStartupOrphanedSessionsScan(string root)
        {
            Console.WriteLine("\n--- Suite 5: Startup Scan for Orphaned Sessions (AA-P3-A Step 4) ---");

            string sessionsDir = Path.Combine(root, "orphan_scan_sessions");
            Directory.CreateDirectory(sessionsDir);

            // Populate manifest with 3 sessions:
            // 1 valid session with existing scorecard.json
            // 2 orphaned sessions missing .scorecard.json
            string validSessionId = "sess-valid-01";
            string validScorecardPath = Path.Combine(sessionsDir, validSessionId + ".scorecard.json");
            File.WriteAllText(validScorecardPath, "{}", Encoding.UTF8);

            string orphanSession1 = "sess-orphan-01";
            string orphanSession2 = "sess-orphan-02";

            SessionManifest manifest = new SessionManifest();
            manifest.AddOrUpdate(new SessionSummary(validSessionId, "punjab-fog-pincer-01", "2026-10-10T12:00:00+05:30", true, validScorecardPath, false));
            manifest.AddOrUpdate(new SessionSummary(orphanSession1, "punjab-fog-pincer-01", "2026-10-10T12:05:00+05:30", false, Path.Combine(sessionsDir, orphanSession1 + ".scorecard.json"), true));
            manifest.AddOrUpdate(new SessionSummary(orphanSession2, "ladakh-ridge-scout-02", "2026-10-10T12:10:00+05:30", false, Path.Combine(sessionsDir, orphanSession2 + ".scorecard.json"), false));

            string manifestPath = Path.Combine(sessionsDir, "manifest.json");
            manifest.Save(manifestPath);

            ScorecardComputer computer = new ScorecardComputer();
            computer.SessionsDirectoryOverride = sessionsDir;

            List<string> warningsLogged = new List<string>();
            Action<string> warningHandler = msg =>
            {
                if (msg != null && msg.Contains("Orphaned session detected in manifest"))
                {
                    warningsLogged.Add(msg);
                }
            };
            var warningScope = new LogWarningScope(warningHandler);

            try
            {
                int orphanedCount = computer.ScanAndLogOrphanedSessions(sessionsDir);

                Assert(orphanedCount == 2, "Expected exactly 2 orphaned sessions to be detected, found: " + orphanedCount);
                Assert(warningsLogged.Count == 2, "Expected 2 warning logs for orphaned sessions, got: " + warningsLogged.Count);
                Assert(warningsLogged.Exists(w => w.Contains(orphanSession1)), "Warning must mention orphanSession1.");
                Assert(warningsLogged.Exists(w => w.Contains(orphanSession2)), "Warning must mention orphanSession2.");

                // Verify helper method on SessionManifest
                List<SessionSummary> orphans = manifest.FindOrphanedSessions(sessionsDir);
                Assert(orphans.Count == 2, "SessionManifest.FindOrphanedSessions must report 2 orphans.");

                Console.WriteLine("  [OK] Orphaned session scan correctly detected 2 orphans without attempting risky re-computations.");
            }
            finally
            {
                warningScope.Dispose();
            }
        }

        /// <summary>
        /// Suite 6: Verifies simulation of write-protected storage and subsequent clean recovery.
        /// (Matches the card's exact TEST section).
        /// </summary>
        private static void TestSimulatedWriteProtectedStorageAndRecovery(string root)
        {
            Console.WriteLine("\n--- Suite 6: Simulated Write-Protected Storage & Recovery ---");

            string sessionsDir = Path.Combine(root, "recovery_sessions");
            Directory.CreateDirectory(sessionsDir);

            TelemetryLogger logger = new TelemetryLogger();
            logger.SessionsDirectoryOverride = sessionsDir;

            ScorecardComputer computer = new ScorecardComputer();
            computer.SessionsDirectoryOverride = sessionsDir;
            computer.Logger = logger;

            string capturedWarning = null;
            Action<string> logWarningHandler = msg =>
            {
                if (msg != null && msg.Contains("IOException during telemetry write — continuing in partial mode"))
                {
                    capturedWarning = msg;
                }
            };
            var warningScope = new LogWarningScope(logWarningHandler);

            try
            {
                // Run 1: Failure run under simulated write-protection / disk failure
                string sessionFailId = "sess-fail-recovery-01";
                logger.StartSession(sessionFailId, "punjab-fog-pincer-01");
                logger.SimulateDiskWriteFailure = true; // Simulates write-protect

                // Write ticks during simulated failure
                for (int i = 0; i < 5; i++)
                {
                    logger.LogScenarioTick(CreateMockTick(sessionFailId, i * 100));
                }

                Assert(logger.IsPartial, "logger must be marked partial.");
                Assert(capturedWarning != null, "Warning 'IOException during telemetry write — continuing in partial mode' must be logged.");

                logger.EndSession("DISK_ERR");

                // Scorecard compute on the failed run
                Scorecard failScorecard = computer.ComputeScorecard(sessionFailId, "punjab-fog-pincer-01", "DISK_ERR", 500);
                Assert(failScorecard.partial == true, "Fail run scorecard must have partial: true.");

                // Run 2: Remove protection -> next run works completely normally
                logger.SimulateDiskWriteFailure = false; // Protection removed

                string sessionCleanId = "sess-clean-recovery-02";
                logger.StartSession(sessionCleanId, "punjab-fog-pincer-01");
                Assert(!logger.IsPartial, "Clean run must reset partial flag to false.");

                for (int i = 0; i < 10; i++)
                {
                    logger.LogScenarioTick(CreateMockTick(sessionCleanId, i * 100));
                }

                logger.EndSession("NORMAL");
                Assert(!logger.IsPartial, "Clean run must complete with partial == false.");

                Scorecard cleanScorecard = computer.ComputeScorecard(sessionCleanId, "punjab-fog-pincer-01", "NORMAL", 1000);
                Assert(cleanScorecard.partial == false, "Clean run scorecard must have partial == false.");
                Assert(cleanScorecard.timelineIndex.Length > 0, "Clean run must have populated timelineIndex.");

                // Check manifest has both runs recorded correctly: fail with partial: true, clean with partial: false
                SessionManifest manifest = SessionManifest.LoadOrCreate(Path.Combine(sessionsDir, "manifest.json"));
                SessionSummary failSum = manifest.sessions.Find(s => s.sessionId == sessionFailId);
                SessionSummary cleanSum = manifest.sessions.Find(s => s.sessionId == sessionCleanId);

                Assert(failSum != null && failSum.partial == true, "Manifest must reflect partial: true for failed run.");
                Assert(cleanSum != null && cleanSum.partial == false, "Manifest must reflect partial: false for clean run.");

                Console.WriteLine("  [OK] Clean recovery verified: write-protected failure mode followed by clean normal run.");
            }
            finally
            {
                warningScope.Dispose();
            }
        }

        /// <summary>
        /// Suite 7: Verifies live WebSocket delivery of SCORECARD_COMPUTE_FAILED error envelope
        /// when running across real loopback WsServer and WsClient.
        /// </summary>
        private static async Task TestLiveWsErrorBroadcastAsync(string root)
        {
            Console.WriteLine("\n--- Suite 7: Live WebSocket SCORECARD_COMPUTE_FAILED Delivery ---");

            int port = 9871;
            WsServer server = new WsServer();
            server.Port = port;
            server.HeartbeatIntervalMs = 200;
            server.StaleThresholdMs = 500;
            server.Start();

            WsClient client = new WsClient();
            client.ServerUri = "ws://127.0.0.1:" + port + "/";
            client.HeartbeatIntervalMs = 200;
            client.StaleThresholdMs = 500;
            client.Start();

            string sessionsDir = Path.Combine(root, "ws_error_sessions");
            Directory.CreateDirectory(sessionsDir);

            // Create 0-byte file
            string sessionId = "sess-ws-error-01";
            string emptyFile = Path.Combine(sessionsDir, sessionId + ".ndjson");
            File.WriteAllText(emptyFile, string.Empty);

            ScorecardComputer computer = new ScorecardComputer();
            computer.SessionsDirectoryOverride = sessionsDir;
            computer.Client = client;

            TaskCompletionSource<JsonEnvelope> errorTcs = new TaskCompletionSource<JsonEnvelope>();

            // Listen on server for incoming ERROR message sent upstream by client
            server.OnMessageReceived += env =>
            {
                if (string.Equals(env.type, JsonEnvelope.ERROR, StringComparison.Ordinal))
                {
                    errorTcs.TrySetResult(env);
                }
            };

            // Wait up to 3 seconds for client connection link to establish
            for (int i = 0; i < 30 && !client.IsLinkConfirmed; i++)
            {
                await Task.Delay(100);
            }

            try
            {
                computer.ComputeScorecard(sessionId, "punjab-fog-pincer-01", "TIME_LIMIT", 30000);

                Task completed = await Task.WhenAny(errorTcs.Task, Task.Delay(4000));
                Assert(completed == errorTcs.Task, "WebSocket server must receive SCORECARD_COMPUTE_FAILED ERROR within 4 seconds.");

                JsonEnvelope receivedError = await errorTcs.Task;
                Assert(receivedError.payload.Contains("SCORECARD_COMPUTE_FAILED"), "Received error payload must contain SCORECARD_COMPUTE_FAILED.");
                Assert(receivedError.payload.Contains("Session file corrupt or missing."), "Received error payload must contain error message.");

                Console.WriteLine("  [OK] Live WebSocket delivery of SCORECARD_COMPUTE_FAILED confirmed on localhost wire.");
            }
            finally
            {
                client.Stop();
                server.Stop();
            }
        }

        private static ScenarioTickPayload CreateMockTick(string sessionId, long elapsedMs)
        {
            ScenarioTickPayload tick = new ScenarioTickPayload();
            tick.sessionId = sessionId;
            tick.t = elapsedMs;
            tick.threatState = ScenarioTickPayload.THREAT_GREEN;
            tick.friendlyVhfActive = false;

            tick.sensorReadings = new SensorReadings
            {
                rfWaterfallPeakGHz = 5.825f,
                rfWaterfallPeakDbm = -65f,
                dopplerPeakHz = 220f,
                acousticSplDb = 52f,
                radarDetectedCount = 4
            };

            tick.ewJammerState = new EWJammerState
            {
                engaged = false,
                bearingDeg = 0f,
                elevationDeg = 0f,
                bandGHz = 5.825f,
                friendlyBlackoutRisk = false
            };

            tick.traineeAction = new TraineeAction
            {
                antennaBearingDeg = 180f,
                antennaElevationDeg = 0f,
                ewEngaged = false,
                ewBandGHz = null,
                kinetic = null
            };

            tick.swarmSnapshot = new SwarmAgentState[0];
            return tick;
        }
    }
}
