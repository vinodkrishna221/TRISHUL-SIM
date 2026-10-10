// ============================================================
// ScorecardMock.cs — Mock Fixture & Data Model for Scorecard (UX-P1-D)
// TRISHUL-SIM · ui-ux track (Rajeswari)
//
// Conforms strictly to board/contracts/scorecard.md v1
// and board/contracts/telemetry.md v1.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrishulSim.UI
{
    [Serializable]
    public class Vector3Dto
    {
        public float x;
        public float y;
        public float z;

        public Vector3Dto() { }
        public Vector3Dto(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    [Serializable]
    public class SwarmAgentStateDto
    {
        public string agentId;
        public bool isDecoy;
        public Vector3Dto positionWorld;
        public Vector3Dto velocityMs;
        public float headingDeg;
        public float altitudeM;
        public bool rfEmitting;
        public float rfFreqGHz;
        public float rfPowerDbm;
        public int propellerRpm;
        public float rcs;
        public bool isNeutralised;
        public int? neutralisedAtMs;
    }

    [Serializable]
    public class TelemetrySnapshotDto
    {
        public int t;
        public List<SwarmAgentStateDto> swarmSnapshot = new List<SwarmAgentStateDto>();
    }

    [Serializable]
    public class ScorecardMetrics
    {
        public int timeToDetectionMs;
        public float classificationAccuracy;
        public float roeCompliance;
        public int kineticRounds;
        public int kineticHits;
        public float kineticEfficiency;
        public int agentsNeutralised;
        public int agentsMissed;
        public bool prematureEwPenalty;
    }

    [Serializable]
    public class DoctrineBenchmarks
    {
        public int timeToDetectionThresholdMs;
        public float roeMinCompliance;
        public float kineticMinEfficiency;
    }

    [Serializable]
    public class Scorecard
    {
        public string sessionId;
        public string scenarioId;
        public string scenarioName;
        public string scenarioNameHindi;
        public string traineeId;
        public string completedAtIso;
        public int durationMs;
        public bool doctrinePassed;

        public ScorecardMetrics metrics;
        public DoctrineBenchmarks doctrineBenchmarks;
        public List<int> timelineIndex = new List<int>();
        public string sessionFilePath;
        public bool partial;
        public int version;
    }

    /// <summary>
    /// Static mock fixtures matching contract examples.
    /// Used when AA-P1-A and AA-P1-B are not yet merged.
    /// </summary>
    public static class ScorecardMock
    {
        /// <summary>
        /// Exact sample scorecard from board/contracts/scorecard.md §"Full example".
        /// </summary>
        public static Scorecard GetPunjabFixture()
        {
            return new Scorecard
            {
                sessionId = "sess-20261004-143201",
                scenarioId = "punjab-fog-pincer-01",
                scenarioName = "Punjab Border — Zero Visibility Pincer",
                scenarioNameHindi = "पंजाब सीमा — शून्य दृश्यता पिंसर",
                traineeId = "havildar-rajan",
                completedAtIso = "2026-10-04T14:33:01+05:30",
                durationMs = 55000,
                doctrinePassed = true,
                metrics = new ScorecardMetrics
                {
                    timeToDetectionMs = 12300,
                    classificationAccuracy = 0.0f,
                    roeCompliance = 1.0f,
                    kineticRounds = 5,
                    kineticHits = 4,
                    kineticEfficiency = 0.8f,
                    agentsNeutralised = 4,
                    agentsMissed = 0,
                    prematureEwPenalty = false
                },
                doctrineBenchmarks = new DoctrineBenchmarks
                {
                    timeToDetectionThresholdMs = 15000,
                    roeMinCompliance = 1.0f,
                    kineticMinEfficiency = 0.5f
                },
                timelineIndex = new List<int> { 0, 5000, 10000, 12300, 20000, 30000, 45000, 45200, 50000, 55000 },
                sessionFilePath = "sessions/sess-20261004-143201.ndjson",
                partial = false,
                version = 1
            };
        }

        /// <summary>
        /// Failed doctrine fixture for testing red badge / failure conditions.
        /// </summary>
        public static Scorecard GetFailedFixture()
        {
            return new Scorecard
            {
                sessionId = "sess-20261004-144500-fail",
                scenarioId = "punjab-fog-pincer-01",
                scenarioName = "Punjab Border — Zero Visibility Pincer",
                scenarioNameHindi = "पंजाब सीमा — शून्य दृश्यता पिंसर",
                traineeId = "trainee-cadet-04",
                completedAtIso = "2026-10-04T14:46:12+05:30",
                durationMs = 60000,
                doctrinePassed = false,
                metrics = new ScorecardMetrics
                {
                    timeToDetectionMs = 18500, // Exceeds 15000ms threshold
                    classificationAccuracy = 0.0f,
                    roeCompliance = 0.0f,      // Friendly blackout risk triggered
                    kineticRounds = 8,
                    kineticHits = 2,
                    kineticEfficiency = 0.25f, // Below 50%
                    agentsNeutralised = 2,
                    agentsMissed = 2,
                    prematureEwPenalty = true
                },
                doctrineBenchmarks = new DoctrineBenchmarks
                {
                    timeToDetectionThresholdMs = 15000,
                    roeMinCompliance = 1.0f,
                    kineticMinEfficiency = 0.5f
                },
                timelineIndex = new List<int> { 0, 10000, 20000, 30000, 40000, 50000, 60000 },
                sessionFilePath = "sessions/sess-20261004-144500-fail.ndjson",
                partial = false,
                version = 1
            };
        }

        /// <summary>
        /// Returns simulated spatial positions of 6 swarm agents (2 decoys, 4 attackers)
        /// at any elapsed millisecond t.
        /// </summary>
        public static List<SwarmAgentStateDto> GetSimulatedSwarmAtTime(int elapsedMs)
        {
            float progress = Mathf.Clamp01(elapsedMs / 55000.0f);
            var agents = new List<SwarmAgentStateDto>();

            // 2 Decoys (Diverge wide, emit heavily, approach from left/right flanks)
            agents.Add(new SwarmAgentStateDto
            {
                agentId = "decoy-0",
                isDecoy = true,
                positionWorld = new Vector3Dto(
                    Mathf.Lerp(-400f, -60f, progress),
                    Mathf.Lerp(25f, 15f, progress),
                    Mathf.Lerp(-250f, -40f, progress)
                ),
                headingDeg = 230.0f,
                altitudeM = 15.0f,
                rfEmitting = true,
                rfFreqGHz = 2.4f,
                isNeutralised = false
            });

            agents.Add(new SwarmAgentStateDto
            {
                agentId = "decoy-1",
                isDecoy = true,
                positionWorld = new Vector3Dto(
                    Mathf.Lerp(-350f, -50f, progress),
                    Mathf.Lerp(28f, 16f, progress),
                    Mathf.Lerp(200f, 30f, progress)
                ),
                headingDeg = 265.0f,
                altitudeM = 16.0f,
                rfEmitting = true,
                rfFreqGHz = 2.4f,
                isNeutralised = false
            });

            // 4 Attackers (Low & fast pincer on axis ~247 degrees)
            for (int i = 0; i < 4; i++)
            {
                float offset = (i - 1.5f) * 20f;
                bool neutralised = (elapsedMs >= (40000 + i * 3000));
                
                agents.Add(new SwarmAgentStateDto
                {
                    agentId = "agent-" + (i + 1),
                    isDecoy = false,
                    positionWorld = new Vector3Dto(
                        Mathf.Lerp(-500f, neutralised ? -80f - (i * 10f) : -20f, progress) + offset,
                        Mathf.Lerp(12f, 8f, progress),
                        Mathf.Lerp(-150f, neutralised ? -30f : -10f, progress) + (offset * 0.5f)
                    ),
                    headingDeg = 247.0f,
                    altitudeM = 8.0f,
                    rfEmitting = true,
                    rfFreqGHz = 5.825f,
                    isNeutralised = neutralised,
                    neutralisedAtMs = neutralised ? (40000 + i * 3000) : (int?)null
                });
            }

            return agents;
        }
    }
}
