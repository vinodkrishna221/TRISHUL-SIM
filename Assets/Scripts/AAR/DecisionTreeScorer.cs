// ============================================================
// DecisionTreeScorer.cs — Tactical Performance Decision Tree Scorer
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Implements board/contracts/telemetry.md v1 §"Derived metrics",
// board/contracts/scorecard.md v1, docs/SCHEMA.md Entity 3 & 4,
// and missions/sohail-mission.md card AA-P1-A.
//
// Single O(n) pass over NDJSON TelemetryRecords:
// 1. timeToDetectionMs: first record where threatState != "GREEN"
// 2. classificationAccuracy: 0.0 in v1 (requires v2 UserClassificationEvent)
// 3. roeCompliance: 1.0 if zero records have friendlyBlackoutRisk==true; else 0.0
// 4. kineticRounds: count records where traineeAction.kinetic != null
// 5. kineticHits: count records where traineeAction.kinetic.hit == true
// 6. kineticEfficiency: kineticHits / kineticRounds (0.0 if rounds == 0)
// 7. agentsNeutralised: count isNeutralised == true in finalSwarmSnapshot
// 8. agentsMissed: attackerCount - agentsNeutralised (from ScenarioConfig)
// 9. prematureEwPenalty: true if EW engaged while threatState == "GREEN"
// 10. doctrinePassed: timeToDetectionMs <= threshold && roeCompliance >= 1.0 && kineticEfficiency >= 0.5
// ============================================================

using System;
using System.Collections.Generic;
using TrishulSim.Network;
using TrishulSim.Scenarios;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace TrishulSim.AAR
{
    /// <summary>
    /// Static Decision Tree Scorer deriving all Tactical Performance ScorecardMetrics
    /// from NDJSON telemetry records and scenario end state.
    /// Zero external dependencies, deterministic, replay-safe O(n) scan.
    /// </summary>
    public static class DecisionTreeScorer
    {
        public const long DefaultTimeToDetectionThresholdMs = 15000;
        public const float DefaultRoeMinCompliance = 1.0f;
        public const float DefaultKineticMinEfficiency = 0.5f;

        /// <summary>
        /// Scores a completed scenario session from its sequentially ordered TelemetryRecords.
        /// Performs a single O(n) pass over the record stream to guarantee compute completion in &lt;100ms.
        /// </summary>
        /// <param name="records">Chronological list of TelemetryRecords parsed from the session NDJSON file.</param>
        /// <param name="finalSwarm">Final swarm snapshot passed in SCENARIO_END payload (optional fallback to records).</param>
        /// <param name="config">Scenario configuration containing swarmProfile.attackerCount (defaults to Punjab).</param>
        /// <param name="benchmarks">Doctrinal pass/fail thresholds (defaults to standard 15s / 1.0 / 0.5).</param>
        /// <returns>Populated ScorecardMetrics object matching scorecard.md schema.</returns>
        public static ScorecardMetrics Score(
            List<TelemetryRecord> records,
            SwarmAgentState[] finalSwarm = null,
            ScenarioConfig config = null,
            DoctrineBenchmarks benchmarks = null)
        {
            ScorecardMetrics metrics = new ScorecardMetrics();

            long firstDetectionMs = 0;
            bool detectionFound = false;
            bool hasFriendlyBlackout = false;
            int rounds = 0;
            int hits = 0;
            bool prematureEw = false;

            if (records != null && records.Count > 0)
            {
                for (int i = 0; i < records.Count; i++)
                {
                    TelemetryRecord r = records[i];
                    if (r == null)
                        continue;

                    bool isGreen = string.Equals(r.threatState, ScenarioTickPayload.THREAT_GREEN, StringComparison.OrdinalIgnoreCase);

                    // 1. timeToDetectionMs: first record where threatState != "GREEN"
                    if (!detectionFound && !isGreen)
                    {
                        detectionFound = true;
                        firstDetectionMs = r.t;
                    }

                    // 2. roeCompliance check: any friendly VHF blackout risk
                    if (r.ewJammerState != null && r.ewJammerState.friendlyBlackoutRisk)
                    {
                        hasFriendlyBlackout = true;
                    }

                    // 3. Kinetic firing metrics
                    if (r.traineeAction != null && r.traineeAction.kinetic != null)
                    {
                        rounds++;
                        if (r.traineeAction.kinetic.hit)
                        {
                            hits++;
                        }
                    }

                    // 4. prematureEwPenalty: EW engaged while threatState is still GREEN
                    bool ewEngaged = (r.traineeAction != null && r.traineeAction.ewEngaged) ||
                                     (r.ewJammerState != null && r.ewJammerState.engaged);
                    if (isGreen && ewEngaged)
                    {
                        prematureEw = true;
                    }
                }
            }

            metrics.timeToDetectionMs = detectionFound ? firstDetectionMs : 0L;
            metrics.classificationAccuracy = 0.0f; // v1: hardcoded to 0.0 per scorecard.md contract

            // roeCompliance: 1.0 if zero records had friendlyBlackoutRisk==true AND records exist; else 0.0
            if (records == null || records.Count == 0)
            {
                metrics.roeCompliance = 0.0f;
            }
            else
            {
                metrics.roeCompliance = hasFriendlyBlackout ? 0.0f : 1.0f;
            }

            metrics.kineticRounds = rounds;
            metrics.kineticHits = hits;
            metrics.kineticEfficiency = rounds > 0 ? ((float)hits / (float)rounds) : 0.0f;
            metrics.prematureEwPenalty = prematureEw;

            // 5. Swarm outcome metrics: agentsNeutralised and agentsMissed
            int neutralisedCount = 0;
            SwarmAgentState[] swarmSnapshot = finalSwarm;
            if ((swarmSnapshot == null || swarmSnapshot.Length == 0) && records != null && records.Count > 0)
            {
                // Fallback to the latest record's swarmSnapshot
                for (int i = records.Count - 1; i >= 0; i--)
                {
                    if (records[i] != null && records[i].swarmSnapshot != null && records[i].swarmSnapshot.Length > 0)
                    {
                        swarmSnapshot = records[i].swarmSnapshot;
                        break;
                    }
                }
            }

            if (swarmSnapshot != null)
            {
                for (int i = 0; i < swarmSnapshot.Length; i++)
                {
                    SwarmAgentState agent = swarmSnapshot[i];
                    if (agent != null && agent.isNeutralised)
                    {
                        neutralisedCount++;
                    }
                }
            }
            metrics.agentsNeutralised = neutralisedCount;

            int attackerCount = 4; // Punjab default
            if (config != null && config.swarmProfile != null)
            {
                attackerCount = config.swarmProfile.attackerCount;
            }
            else if (swarmSnapshot != null && swarmSnapshot.Length > 0)
            {
                int nonDecoys = 0;
                for (int i = 0; i < swarmSnapshot.Length; i++)
                {
                    if (swarmSnapshot[i] != null && !swarmSnapshot[i].isDecoy)
                        nonDecoys++;
                }
                if (nonDecoys > 0)
                    attackerCount = nonDecoys;
            }

            metrics.agentsMissed = Math.Max(0, attackerCount - neutralisedCount);

            return metrics;
        }

        /// <summary>
        /// Evaluates whether the computed metrics satisfy all doctrinal benchmark thresholds:
        /// doctrinePassed = timeToDetectionMs &lt;= threshold &amp;&amp; roeCompliance &gt;= 1.0 &amp;&amp; kineticEfficiency &gt;= 0.5
        /// </summary>
        public static bool EvaluateDoctrine(
            ScorecardMetrics metrics,
            DoctrineBenchmarks benchmarks,
            bool threatDetected = true)
        {
            if (metrics == null)
                return false;

            DoctrineBenchmarks bench = benchmarks ?? new DoctrineBenchmarks();

            // Threat must have been detected within the threshold window
            bool ttdPass = threatDetected &&
                           metrics.timeToDetectionMs > 0 &&
                           metrics.timeToDetectionMs <= bench.timeToDetectionThresholdMs;

            // Zero ROE violations permitted (roeCompliance must be >= 1.0)
            bool roePass = metrics.roeCompliance >= bench.roeMinCompliance;

            // Kinetic accuracy must meet or exceed minimum efficiency benchmark (e.g. >= 0.5)
            bool kineticPass = metrics.kineticEfficiency >= bench.kineticMinEfficiency;

            return ttdPass && roePass && kineticPass;
        }

#if UNITY_EDITOR || !UNITY_5_3_OR_NEWER
        /// <summary>
        /// Standalone unit test suite verifying DecisionTreeScorer against docs/SCHEMA.md Entity 3 &amp; 4.
        /// Can be run in Unity Editor tests or invoked directly from CLI self-tests.
        /// </summary>
        public static void RunUnitTests()
        {
#if !UNITY_5_3_OR_NEWER
            Console.WriteLine("[DecisionTreeScorer] Running Schema & Contract Unit Tests...");
#else
            Debug.Log("[DecisionTreeScorer] Running Schema & Contract Unit Tests...");
#endif

            // --- Test A: Exact docs/SCHEMA.md Entity 4 Example ---
            // Example benchmark values:
            // timeToDetectionMs = 12300 (<= 15000) -> PASS
            // roeCompliance = 1.0 (>= 1.0) -> PASS
            // kineticRounds = 5, kineticHits = 4 -> kineticEfficiency = 0.8 (>= 0.5) -> PASS
            // agentsNeutralised = 4, agentsMissed = 0 -> attackerCount = 4
            // prematureEwPenalty = false
            // doctrinePassed = true

            List<TelemetryRecord> schemaRecords = CreateSchemaExampleRecords();
            SwarmAgentState[] finalSwarm = CreateSchemaFinalSwarm(4, 2);
            ScenarioConfig cfg = new ScenarioConfig();
            cfg.swarmProfile.attackerCount = 4;
            cfg.swarmProfile.decoyCount = 2;
            cfg.swarmProfile.agentCount = 6;

            DoctrineBenchmarks bench = new DoctrineBenchmarks
            {
                timeToDetectionThresholdMs = 15000,
                roeMinCompliance = 1.0f,
                kineticMinEfficiency = 0.5f
            };

            ScorecardMetrics metrics = Score(schemaRecords, finalSwarm, cfg, bench);
            bool passed = EvaluateDoctrine(metrics, bench, threatDetected: true);

            AssertEqual(12300L, metrics.timeToDetectionMs, "timeToDetectionMs must match SCHEMA.md 12300");
            AssertFloatEqual(1.0f, metrics.roeCompliance, "roeCompliance must be 1.0");
            AssertEqual(5, metrics.kineticRounds, "kineticRounds must be 5");
            AssertEqual(4, metrics.kineticHits, "kineticHits must be 4");
            AssertFloatEqual(0.8f, metrics.kineticEfficiency, "kineticEfficiency must match 0.8");
            AssertEqual(4, metrics.agentsNeutralised, "agentsNeutralised must be 4");
            AssertEqual(0, metrics.agentsMissed, "agentsMissed must be 0");
            AssertEqual(false, metrics.prematureEwPenalty, "prematureEwPenalty must be false");
            AssertEqual(true, passed, "doctrinePassed must evaluate to true for SCHEMA.md example");

            // --- Test B: ROE Violation Detection ---
            List<TelemetryRecord> roeViolationRecords = CreateSchemaExampleRecords();
            // Inject friendlyBlackoutRisk on tick at t=25000
            roeViolationRecords[3].ewJammerState.friendlyBlackoutRisk = true;
            ScorecardMetrics roeMetrics = Score(roeViolationRecords, finalSwarm, cfg, bench);
            bool roePassed = EvaluateDoctrine(roeMetrics, bench, threatDetected: true);
            AssertFloatEqual(0.0f, roeMetrics.roeCompliance, "roeCompliance must drop to 0.0 on blackout risk");
            AssertEqual(false, roePassed, "doctrinePassed must be false when ROE violation occurs");

            // --- Test C: Premature EW Penalty ---
            List<TelemetryRecord> prematureEwRecords = CreateSchemaExampleRecords();
            // Engage EW at t=5000 while threatState is still GREEN
            prematureEwRecords[0].threatState = ScenarioTickPayload.THREAT_GREEN;
            prematureEwRecords[0].traineeAction.ewEngaged = true;
            ScorecardMetrics ewMetrics = Score(prematureEwRecords, finalSwarm, cfg, bench);
            AssertEqual(true, ewMetrics.prematureEwPenalty, "prematureEwPenalty must be true when EW active during GREEN threat");

            // --- Test D: Low Kinetic Efficiency Failure ---
            List<TelemetryRecord> lowKineticRecords = CreateSchemaExampleRecords();
            // Change kinetic hits: 1 hit out of 5 rounds -> 0.2 < 0.5
            lowKineticRecords[4].traineeAction.kinetic.hit = false;
            lowKineticRecords[5].traineeAction.kinetic.hit = false;
            lowKineticRecords[6].traineeAction.kinetic.hit = false;
            ScorecardMetrics lowKinMetrics = Score(lowKineticRecords, finalSwarm, cfg, bench);
            AssertFloatEqual(0.2f, lowKinMetrics.kineticEfficiency, "kineticEfficiency must be 0.2");
            bool lowKinPassed = EvaluateDoctrine(lowKinMetrics, bench, threatDetected: true);
            AssertEqual(false, lowKinPassed, "doctrinePassed must be false when kinetic efficiency < 0.5");

            // --- Test E: Detection Timeout Failure (t=16000 > 15000) ---
            List<TelemetryRecord> lateDetectRecords = CreateSchemaExampleRecords();
            lateDetectRecords[0].threatState = ScenarioTickPayload.THREAT_GREEN;
            lateDetectRecords[1].threatState = ScenarioTickPayload.THREAT_GREEN;
            lateDetectRecords[2].t = 16000;
            lateDetectRecords[2].threatState = ScenarioTickPayload.THREAT_AMBER;
            ScorecardMetrics lateMetrics = Score(lateDetectRecords, finalSwarm, cfg, bench);
            AssertEqual(16000L, lateMetrics.timeToDetectionMs, "timeToDetectionMs must be 16000");
            bool latePassed = EvaluateDoctrine(lateMetrics, bench, threatDetected: true);
            AssertEqual(false, latePassed, "doctrinePassed must be false when detection exceeds 15s threshold");

#if !UNITY_5_3_OR_NEWER
            Console.WriteLine("[DecisionTreeScorer] All Unit Tests PASSED cleanly.");
#else
            Debug.Log("[DecisionTreeScorer] All Unit Tests PASSED cleanly.");
#endif
        }

        private static List<TelemetryRecord> CreateSchemaExampleRecords()
        {
            List<TelemetryRecord> list = new List<TelemetryRecord>();
            string sId = "sess-20261004-143201";
            string scId = "punjab-fog-pincer-01";

            // Record 0: t=0, GREEN
            list.Add(new TelemetryRecord
            {
                t = 0,
                sessionId = sId,
                scenarioId = scId,
                threatState = ScenarioTickPayload.THREAT_GREEN,
                traineeAction = new TraineeAction { antennaBearingDeg = 240f, antennaElevationDeg = 0f, ewEngaged = false },
                ewJammerState = new EWJammerState { engaged = false, friendlyBlackoutRisk = false }
            });

            // Record 1: t=5000, GREEN
            list.Add(new TelemetryRecord
            {
                t = 5000,
                sessionId = sId,
                scenarioId = scId,
                threatState = ScenarioTickPayload.THREAT_GREEN,
                traineeAction = new TraineeAction { antennaBearingDeg = 245f, antennaElevationDeg = -2f, ewEngaged = false },
                ewJammerState = new EWJammerState { engaged = false, friendlyBlackoutRisk = false }
            });

            // Record 2: t=12300, first detection -> AMBER
            list.Add(new TelemetryRecord
            {
                t = 12300,
                sessionId = sId,
                scenarioId = scId,
                threatState = ScenarioTickPayload.THREAT_AMBER,
                traineeAction = new TraineeAction { antennaBearingDeg = 247f, antennaElevationDeg = -5f, ewEngaged = false },
                ewJammerState = new EWJammerState { engaged = false, friendlyBlackoutRisk = false }
            });

            // Record 3: t=25000, RED, EW active, no blackout
            list.Add(new TelemetryRecord
            {
                t = 25000,
                sessionId = sId,
                scenarioId = scId,
                threatState = ScenarioTickPayload.THREAT_RED,
                traineeAction = new TraineeAction { antennaBearingDeg = 247f, antennaElevationDeg = -5f, ewEngaged = true, ewBandGHz = 5.825f },
                ewJammerState = new EWJammerState { engaged = true, bearingDeg = 247f, elevationDeg = -5f, bandGHz = 5.825f, friendlyBlackoutRisk = false }
            });

            // Record 4: t=45200, Kinetic round 1 -> HIT
            list.Add(new TelemetryRecord
            {
                t = 45200,
                sessionId = sId,
                scenarioId = scId,
                threatState = ScenarioTickPayload.THREAT_RED,
                traineeAction = new TraineeAction
                {
                    antennaBearingDeg = 247f,
                    antennaElevationDeg = -5f,
                    ewEngaged = true,
                    ewBandGHz = 5.825f,
                    kinetic = new KineticEvent { firedAtMs = 45200, targetAgentId = "agent-2", hit = true, leadAngleDeg = 3.2f }
                },
                ewJammerState = new EWJammerState { engaged = true, bearingDeg = 247f, elevationDeg = -5f, bandGHz = 5.825f, friendlyBlackoutRisk = false }
            });

            // Record 5: t=46000, Kinetic round 2 -> HIT
            list.Add(new TelemetryRecord
            {
                t = 46000,
                sessionId = sId,
                scenarioId = scId,
                threatState = ScenarioTickPayload.THREAT_RED,
                traineeAction = new TraineeAction
                {
                    antennaBearingDeg = 247f,
                    antennaElevationDeg = -5f,
                    ewEngaged = true,
                    ewBandGHz = 5.825f,
                    kinetic = new KineticEvent { firedAtMs = 46000, targetAgentId = "agent-3", hit = true, leadAngleDeg = 2.8f }
                },
                ewJammerState = new EWJammerState { engaged = true, bearingDeg = 247f, elevationDeg = -5f, bandGHz = 5.825f, friendlyBlackoutRisk = false }
            });

            // Record 6: t=47000, Kinetic round 3 -> HIT
            list.Add(new TelemetryRecord
            {
                t = 47000,
                sessionId = sId,
                scenarioId = scId,
                threatState = ScenarioTickPayload.THREAT_RED,
                traineeAction = new TraineeAction
                {
                    antennaBearingDeg = 247f,
                    antennaElevationDeg = -5f,
                    ewEngaged = true,
                    ewBandGHz = 5.825f,
                    kinetic = new KineticEvent { firedAtMs = 47000, targetAgentId = "agent-4", hit = true, leadAngleDeg = 1.9f }
                },
                ewJammerState = new EWJammerState { engaged = true, bearingDeg = 247f, elevationDeg = -5f, bandGHz = 5.825f, friendlyBlackoutRisk = false }
            });

            // Record 7: t=48000, Kinetic round 4 -> MISS
            list.Add(new TelemetryRecord
            {
                t = 48000,
                sessionId = sId,
                scenarioId = scId,
                threatState = ScenarioTickPayload.THREAT_RED,
                traineeAction = new TraineeAction
                {
                    antennaBearingDeg = 247f,
                    antennaElevationDeg = -5f,
                    ewEngaged = true,
                    ewBandGHz = 5.825f,
                    kinetic = new KineticEvent { firedAtMs = 48000, targetAgentId = "agent-5", hit = false, leadAngleDeg = 6.4f }
                },
                ewJammerState = new EWJammerState { engaged = true, bearingDeg = 247f, elevationDeg = -5f, bandGHz = 5.825f, friendlyBlackoutRisk = false }
            });

            // Record 8: t=49000, Kinetic round 5 -> HIT
            list.Add(new TelemetryRecord
            {
                t = 49000,
                sessionId = sId,
                scenarioId = scId,
                threatState = ScenarioTickPayload.THREAT_RED,
                traineeAction = new TraineeAction
                {
                    antennaBearingDeg = 247f,
                    antennaElevationDeg = -5f,
                    ewEngaged = true,
                    ewBandGHz = 5.825f,
                    kinetic = new KineticEvent { firedAtMs = 49000, targetAgentId = "agent-5", hit = true, leadAngleDeg = 2.1f }
                },
                ewJammerState = new EWJammerState { engaged = true, bearingDeg = 247f, elevationDeg = -5f, bandGHz = 5.825f, friendlyBlackoutRisk = false }
            });

            // Record 9: t=55000, scenario end
            list.Add(new TelemetryRecord
            {
                t = 55000,
                sessionId = sId,
                scenarioId = scId,
                threatState = ScenarioTickPayload.THREAT_RED,
                traineeAction = new TraineeAction { antennaBearingDeg = 247f, antennaElevationDeg = -5f, ewEngaged = false },
                ewJammerState = new EWJammerState { engaged = false, friendlyBlackoutRisk = false }
            });

            return list;
        }

        private static SwarmAgentState[] CreateSchemaFinalSwarm(int neutralisedAttackers, int decoys)
        {
            List<SwarmAgentState> list = new List<SwarmAgentState>();
            for (int i = 0; i < decoys; i++)
            {
                list.Add(new SwarmAgentState
                {
                    agentId = "decoy-" + i,
                    isDecoy = true,
                    isNeutralised = false
                });
            }
            for (int i = 0; i < neutralisedAttackers; i++)
            {
                list.Add(new SwarmAgentState
                {
                    agentId = "agent-" + (i + 2),
                    isDecoy = false,
                    isNeutralised = true,
                    neutralisedAtMs = 45000 + (i * 1000)
                });
            }
            return list.ToArray();
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(string.Format("Assertion failed: {0}. Expected '{1}', got '{2}'.", message, expected, actual));
            }
        }

        private static void AssertFloatEqual(float expected, float actual, string message, float tolerance = 0.001f)
        {
            if (Math.Abs(expected - actual) > tolerance)
            {
                throw new InvalidOperationException(string.Format("Assertion failed: {0}. Expected '{1}', got '{2}'.", message, expected, actual));
            }
        }
#endif
    }
}
