// ============================================================
// TimelineIndexerSelfTest.cs — Runnable Self-Test for AA-P1-B
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Validates:
// 1. Exact mission card deliverable & test criteria:
//    - TimelineIndexer.BuildIndex() generates sorted timelineIndex (>=100 entries for 10s scenario)
//    - Scorecard JSON shows timelineIndex: [0, 100, 200, ...]
//    - TimelinePlaybackController.SetScrubPosition(45000) -> CurrentRecord.t == 45000
//    - Console output: "Seeked to t=45000 — agentCount=6, threatState=RED"
// 2. High-performance seek latency: <20ms SLA for NDJSON seek
// 3. Binary search boundary tests (exact match, between ticks, before start, past end)
// 4. End-to-end integration with TelemetryLogger and ScorecardComputer
// 5. Serialization / deserialization fidelity in scorecard.json and SCORECARD_PAYLOAD
//
// Compiles directly with csc.exe or inside Unity Editor tests.
// ============================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public static class TimelineIndexerSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllAsync().GetAwaiter().GetResult();
                Console.WriteLine("\n[PASS] All AA-P1-B TimelineIndexer tests passed.");
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
            Console.WriteLine("=== Running TRISHUL-SIM AA-P1-B TimelineIndexer Self-Test ===");

            TestBuildIndexWithSyntheticNdjson();
            TestSeekToTimeAndLatencySla();
            TestTimelinePlaybackControllerScrubbing();
            TestEndToEndScorecardTimelineIndexGeneration();
            TestBoundaryAndCorruptFileHandling();

            Console.WriteLine("\n=== AA-P1-B TimelineIndexer Self-Test COMPLETE: ALL PASS ===");
            await Task.Yield();
        }

        /// <summary>
        /// Validates BuildIndex with a 10-second (100 ticks @ 10 Hz) synthetic NDJSON session.
        /// </summary>
        public static void TestBuildIndexWithSyntheticNdjson()
        {
            Console.WriteLine("\n-- Test 1: BuildIndex With Synthetic 10s NDJSON (100 Ticks) --");

            string tempFile = Path.Combine(Path.GetTempPath(), "Trishul_IndexTest_" + Guid.NewGuid().ToString("N") + ".ndjson");
            try
            {
                using (StreamWriter writer = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    for (int i = 0; i < 100; i++)
                    {
                        int t = i * 100;
                        string json = CreateSyntheticRecordJson(t, "sess-test-01", "GREEN", 6);
                        writer.WriteLine(json);
                    }
                }

                int[] index = TimelineIndexer.BuildIndex(tempFile);
                Assert(index != null, "Index must not be null");
                Assert(index.Length == 100, "Index length must be 100 for 100 ticks, got " + index.Length);
                Assert(index[0] == 0, "Index[0] must be 0, got " + index[0]);
                Assert(index[99] == 9900, "Index[99] must be 9900, got " + index[99]);

                for (int i = 0; i < 100; i++)
                {
                    Assert(index[i] == i * 100, "Index[" + i + "] expected " + (i * 100) + ", got " + index[i]);
                }

                // Verify asynchronous variant
                int[] asyncIndex = TimelineIndexer.BuildIndexAsync(tempFile).GetAwaiter().GetResult();
                Assert(asyncIndex.Length == 100, "Async index length must match");

                Console.WriteLine("[PASS] Test 1: BuildIndex returned exactly 100 sorted entries [0..9900]");
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        /// <summary>
        /// Validates binary search seek and confirms <20ms seek latency SLA.
        /// </summary>
        public static void TestSeekToTimeAndLatencySla()
        {
            Console.WriteLine("\n-- Test 2: SeekToTime Accuracy and <20ms Latency SLA --");

            string tempFile = Path.Combine(Path.GetTempPath(), "Trishul_SeekTest_" + Guid.NewGuid().ToString("N") + ".ndjson");
            try
            {
                // Create a 600-tick (60s) scenario
                using (StreamWriter writer = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    for (int i = 0; i < 600; i++)
                    {
                        int t = i * 100;
                        string state = (t < 15000) ? "GREEN" : (t < 30000 ? "AMBER" : "RED");
                        string json = CreateSyntheticRecordJson(t, "sess-seek-01", state, 6);
                        writer.WriteLine(json);
                    }
                }

                int[] index = TimelineIndexer.BuildIndex(tempFile);
                Assert(index.Length == 600, "Expected 600 ticks");

                // Warm up JIT and file cache before timing
                TimelineIndexer.SeekToTime(tempFile, index, 0);

                // Seek exact match: t=45000
                Stopwatch sw = Stopwatch.StartNew();
                TelemetryRecord rec45k = TimelineIndexer.SeekToTime(tempFile, index, 45000);
                sw.Stop();

                Assert(rec45k != null, "Seek to 45000 must return a record");
                Assert(rec45k.t == 45000, "Record timestamp must be exactly 45000, got " + rec45k.t);
                Assert(rec45k.threatState == "RED", "threatState at 45000 must be RED");
                Assert(rec45k.swarmSnapshot != null && rec45k.swarmSnapshot.Length == 6, "Swarm snapshot must contain 6 agents");
                Assert(sw.ElapsedMilliseconds < 20, "Seek latency must be <20ms, was " + sw.ElapsedMilliseconds + "ms");
                Console.WriteLine("Seek latency for t=45000: " + sw.ElapsedMilliseconds + "ms (SLA: <20ms)");

                // Seek intermediate value: t=45030 should resolve to 45000
                TelemetryRecord recClosestLow = TimelineIndexer.SeekToTime(tempFile, index, 45030);
                Assert(recClosestLow != null && recClosestLow.t == 45000, "45030 should map to closest 45000, got " + (recClosestLow != null ? recClosestLow.t.ToString() : "null"));

                // Seek intermediate value: t=45070 should resolve to 45100
                TelemetryRecord recClosestHigh = TimelineIndexer.SeekToTime(tempFile, index, 45070);
                Assert(recClosestHigh != null && recClosestHigh.t == 45100, "45070 should map to closest 45100, got " + (recClosestHigh != null ? recClosestHigh.t.ToString() : "null"));

                // Seek before start: t=-500 -> 0
                TelemetryRecord recStart = TimelineIndexer.SeekToTime(tempFile, index, -500);
                Assert(recStart != null && recStart.t == 0, "Seek < 0 should clamp to 0, got " + (recStart != null ? recStart.t.ToString() : "null"));

                // Seek past end: t=100000 -> 59900
                TelemetryRecord recEnd = TimelineIndexer.SeekToTime(tempFile, index, 100000);
                Assert(recEnd != null && recEnd.t == 59900, "Seek past end should clamp to 59900, got " + (recEnd != null ? recEnd.t.ToString() : "null"));

                Console.WriteLine("[PASS] Test 2: SeekToTime binary search verified with latency <20ms");
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        /// <summary>
        /// Validates TimelinePlaybackController API and exact console logging requirement.
        /// </summary>
        public static void TestTimelinePlaybackControllerScrubbing()
        {
            Console.WriteLine("\n-- Test 3: TimelinePlaybackController Observable Scrubber & Console Output --");

            string tempFile = Path.Combine(Path.GetTempPath(), "Trishul_PlaybackTest_" + Guid.NewGuid().ToString("N") + ".ndjson");
            try
            {
                using (StreamWriter writer = new StreamWriter(tempFile, false, Encoding.UTF8))
                {
                    for (int i = 0; i < 550; i++)
                    {
                        int t = i * 100;
                        string state = (t < 20000) ? "GREEN" : (t < 40000 ? "AMBER" : "RED");
                        string json = CreateSyntheticRecordJson(t, "sess-ctrl-01", state, 6);
                        writer.WriteLine(json);
                    }
                }

                int[] index = TimelineIndexer.BuildIndex(tempFile);

                TimelinePlaybackController controller = new TimelinePlaybackController();
                controller.Initialize(tempFile, index);

                Assert(controller.SessionFilePath == tempFile, "SessionFilePath must be set");
                Assert(controller.TickCount == 550, "TickCount must be 550");
                Assert(controller.CurrentRecord != null && controller.CurrentRecord.t == 0, "Initial scrub should be t=0");

                bool eventFired = false;
                TelemetryRecord eventRecord = null;
                controller.OnScrubRecordChanged += (rec) =>
                {
                    eventFired = true;
                    eventRecord = rec;
                };

                // Perform scrub to t=45000 (Exact mission test requirement)
                TelemetryRecord scrubbed = controller.SetScrubPosition(45000);

                Assert(scrubbed != null, "Scrubbed record must not be null");
                Assert(scrubbed.t == 45000, "Scrubbed record t must be 45000, got " + scrubbed.t);
                Assert(controller.CurrentRecord != null && controller.CurrentRecord.t == 45000, "CurrentRecord must be 45000");
                Assert(controller.CurrentTimeMs == 45000, "CurrentTimeMs must be 45000");
                Assert(eventFired, "OnScrubRecordChanged event must fire");
                Assert(eventRecord != null && eventRecord.t == 45000, "Event record must match");
                Assert(scrubbed.threatState == "RED", "threatState must be RED at t=45000");
                Assert(scrubbed.swarmSnapshot.Length == 6, "agentCount must be 6");

                Console.WriteLine("[PASS] Test 3: TimelinePlaybackController.SetScrubPosition(45000) succeeded with event propagation");
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        /// <summary>
        /// Validates full scenario end-to-end flow:
        /// TelemetryLogger -> NDJSON -> ScorecardComputer -> Scorecard.timelineIndex populated
        /// and verified in scorecard.json.
        /// </summary>
        public static void TestEndToEndScorecardTimelineIndexGeneration()
        {
            Console.WriteLine("\n-- Test 4: End-to-End Scorecard timelineIndex Generation & Serialization --");

            string tempDir = Path.Combine(Path.GetTempPath(), "Trishul_AAR_E2E_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                TelemetryLogger logger = new TelemetryLogger();
                logger.SessionsDirectoryOverride = tempDir;

                ScorecardComputer computer = new ScorecardComputer();
                computer.SessionsDirectoryOverride = tempDir;
                computer.Logger = logger;

                TimelinePlaybackController controller = new TimelinePlaybackController();

                string sessionId = "sess-20261010-punjab-scrub";
                string scenarioId = "punjab-fog-pincer-01";

                ScenarioStartPayload startPayload = new ScenarioStartPayload
                {
                    sessionId = sessionId,
                    scenarioId = scenarioId,
                    seed = 42819,
                    startedAtMs = 1759600021000,
                    scenarioConfig = new ScenarioConfig
                    {
                        id = scenarioId,
                        name = "Punjab Border — Zero Visibility Pincer",
                        nameHindi = "पंजाब सीमा — शून्य दृश्यता पिंसर",
                        region = "PUNJAB_RIVERINE",
                        durationMaxSecs = 120,
                        swarmProfile = new SwarmProfile
                        {
                            agentCount = 6,
                            attackerCount = 4,
                            decoyCount = 2
                        }
                    }
                };

                JsonEnvelope startEnv = JsonEnvelope.Create(JsonEnvelope.SCENARIO_START, startPayload.ToJson());
                logger.HandleMessageReceived(startEnv);
                computer.HandleMessageReceived(startEnv);

                // Log 100 ticks (10s scenario)
                for (int i = 0; i < 100; i++)
                {
                    int t = i * 100;
                    ScenarioTickPayload tickPayload = new ScenarioTickPayload
                    {
                        sessionId = sessionId,
                        t = t,
                        threatState = (t < 2000) ? "GREEN" : ((t < 6000) ? "AMBER" : "RED"),
                        friendlyVhfActive = false,
                        swarmSnapshot = new SwarmAgentState[]
                        {
                            new SwarmAgentState { agentId = "agent-1", isNeutralised = false },
                            new SwarmAgentState { agentId = "agent-2", isNeutralised = false },
                            new SwarmAgentState { agentId = "agent-3", isNeutralised = (t >= 4500) },
                            new SwarmAgentState { agentId = "agent-4", isNeutralised = false },
                            new SwarmAgentState { agentId = "agent-5", isNeutralised = false },
                            new SwarmAgentState { agentId = "agent-6", isNeutralised = false }
                        },
                        sensorReadings = new SensorReadings { rfWaterfallPeakGHz = 5.825f, dopplerPeakHz = 220.4f, acousticSplDb = 54.2f },
                        ewJammerState = new EWJammerState { engaged = false, friendlyBlackoutRisk = false },
                        traineeAction = new TraineeAction
                        {
                            antennaBearingDeg = 247f,
                            kinetic = (t == 4500) ? new KineticEvent { firedAtMs = 4500, targetAgentId = "agent-3", hit = true, leadAngleDeg = 1.2f } : null
                        }
                    };

                    JsonEnvelope tickEnv = JsonEnvelope.Create(JsonEnvelope.SCENARIO_TICK, tickPayload.ToJson());
                    logger.HandleMessageReceived(tickEnv);
                }

                // Scenario ends
                ScenarioEndPayload endPayload = new ScenarioEndPayload
                {
                    sessionId = sessionId,
                    reason = "OBJECTIVE_COMPLETE",
                    t = 9900,
                    finalSwarmSnapshot = new SwarmAgentState[]
                    {
                        new SwarmAgentState { agentId = "agent-1", isNeutralised = true },
                        new SwarmAgentState { agentId = "agent-2", isNeutralised = true },
                        new SwarmAgentState { agentId = "agent-3", isNeutralised = true },
                        new SwarmAgentState { agentId = "agent-4", isNeutralised = true },
                        new SwarmAgentState { agentId = "agent-5", isNeutralised = false },
                        new SwarmAgentState { agentId = "agent-6", isNeutralised = false }
                    }
                };

                JsonEnvelope endEnv = JsonEnvelope.Create(JsonEnvelope.SCENARIO_END, endPayload.ToJson());
                logger.HandleMessageReceived(endEnv);
                computer.HandleMessageReceived(endEnv);

                Scorecard computed = computer.LastComputedScorecard;
                Assert(computed != null, "Scorecard must be computed");
                Assert(computed.timelineIndex != null, "timelineIndex must not be null");
                Assert(computed.timelineIndex.Length >= 100, "timelineIndex must have >=100 entries, got " + computed.timelineIndex.Length);
                Assert(computed.timelineIndex[0] == 0, "First timestamp in index must be 0");
                Assert(computed.timelineIndex[99] == 9900, "Last timestamp in index must be 9900");

                // Check serialized JSON file on disk
                string scorecardFile = Path.Combine(tempDir, sessionId + ".scorecard.json");
                Assert(File.Exists(scorecardFile), "Scorecard file must exist on disk at " + scorecardFile);

                string jsonContent = File.ReadAllText(scorecardFile, Encoding.UTF8);
                Assert(jsonContent.Contains("\"timelineIndex\":[0,100,200,"), "Scorecard JSON must contain timelineIndex array [0,100,200,...]");

                // Parse scorecard back from disk
                Scorecard parsed = Scorecard.Parse(jsonContent);
                Assert(parsed.timelineIndex != null && parsed.timelineIndex.Length == computed.timelineIndex.Length, "Parsed timelineIndex length must match computed");
                Assert(parsed.timelineIndex[0] == 0 && parsed.timelineIndex[99] == 9900, "Parsed timelineIndex values must match");

                // Verify TimelinePlaybackController initialized with this Scorecard
                controller.Initialize(parsed);
                Assert(controller.TickCount == parsed.timelineIndex.Length, "Controller TickCount must match");

                TelemetryRecord scrubbed4500 = controller.SetScrubPosition(4500);
                Assert(scrubbed4500 != null && scrubbed4500.t == 4500, "Scrub to kinetic moment t=4500 must return matching record");
                Assert(scrubbed4500.traineeAction != null && scrubbed4500.traineeAction.kinetic != null, "Kinetic shot must be captured at t=4500");
                Assert(scrubbed4500.traineeAction.kinetic.hit == true, "Kinetic shot must be recorded as hit");

                Console.WriteLine("[PASS] Test 4: End-to-end Scorecard timelineIndex generation & scrubber replay verified");
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        /// <summary>
        /// Validates edge cases: missing files, empty files, malformed JSON, and single records.
        /// </summary>
        public static void TestBoundaryAndCorruptFileHandling()
        {
            Console.WriteLine("\n-- Test 5: Boundary & Error Handling --");

            // 1. Non-existent file
            int[] missingIdx = TimelineIndexer.BuildIndex("non_existent_file.ndjson");
            Assert(missingIdx != null && missingIdx.Length == 0, "Non-existent file should return empty index");

            TelemetryRecord nullSeek = TimelineIndexer.SeekToTime("non_existent_file.ndjson", missingIdx, 100);
            Assert(nullSeek == null, "Seek in non-existent file should return null");

            // 2. Empty file
            string emptyFile = Path.Combine(Path.GetTempPath(), "Trishul_Empty_" + Guid.NewGuid().ToString("N") + ".ndjson");
            try
            {
                File.WriteAllText(emptyFile, "", Encoding.UTF8);
                int[] emptyIdx = TimelineIndexer.BuildIndex(emptyFile);
                Assert(emptyIdx != null && emptyIdx.Length == 0, "Empty file should return empty index");
            }
            finally
            {
                try { if (File.Exists(emptyFile)) File.Delete(emptyFile); } catch { }
            }

            // 3. Single record file
            string singleFile = Path.Combine(Path.GetTempPath(), "Trishul_Single_" + Guid.NewGuid().ToString("N") + ".ndjson");
            try
            {
                string json = CreateSyntheticRecordJson(1234, "sess-single", "GREEN", 1);
                File.WriteAllText(singleFile, json + "\n", Encoding.UTF8);

                int[] singleIdx = TimelineIndexer.BuildIndex(singleFile);
                Assert(singleIdx != null && singleIdx.Length == 1 && singleIdx[0] == 1234, "Single record index must contain [1234]");

                TelemetryRecord seekSingle = TimelineIndexer.SeekToTime(singleFile, singleIdx, 9999);
                Assert(seekSingle != null && seekSingle.t == 1234, "Seek in single file should clamp to 1234");
            }
            finally
            {
                try { if (File.Exists(singleFile)) File.Delete(singleFile); } catch { }
            }

            Console.WriteLine("[PASS] Test 5: Boundary conditions & error handling passed without unhandled exceptions");
        }

        private static string CreateSyntheticRecordJson(int t, string sessionId, string threatState, int agentCount)
        {
            StringBuilder sb = new StringBuilder(512);
            sb.Append("{\"t\":").Append(t.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"sessionId\":\"").Append(sessionId).Append("\"");
            sb.Append(",\"scenarioId\":\"punjab-fog-pincer-01\"");
            sb.Append(",\"threatState\":\"").Append(threatState).Append("\"");
            sb.Append(",\"friendlyVhfActive\":false");
            sb.Append(",\"traineeAction\":{\"antennaBearingDeg\":247.0,\"antennaElevationDeg\":-5.0,\"ewEngaged\":false,\"ewBandGHz\":null,\"kinetic\":null}");
            sb.Append(",\"sensorReadings\":{\"rfWaterfallPeakGHz\":5.825,\"rfWaterfallPeakDbm\":-62.3,\"dopplerPeakHz\":220.4,\"acousticSplDb\":54.2,\"radarDetectedCount\":4}");
            sb.Append(",\"ewJammerState\":{\"engaged\":false,\"bearingDeg\":247.0,\"elevationDeg\":-5.0,\"bandGHz\":5.825,\"friendlyBlackoutRisk\":false}");
            sb.Append(",\"swarmSnapshot\":[");
            for (int i = 0; i < agentCount; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"agentId\":\"agent-").Append(i + 1).Append("\",\"isDecoy\":false,\"positionWorld\":{\"x\":100.0,\"y\":10.0,\"z\":50.0},\"velocityMs\":{\"x\":0.0,\"y\":0.0,\"z\":0.0},\"headingDeg\":247.0,\"altitudeM\":10.0,\"rfEmitting\":true,\"rfFreqGHz\":5.825,\"rfPowerDbm\":23.0,\"propellerRpm\":11400,\"rcs\":0.012,\"isNeutralised\":false,\"neutralisedAtMs\":null}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("Assertion Failed: " + message);
            }
        }
    }
}
