// ============================================================
// SensorContracts.cs — Sensor Data Structures & Mock Fixtures
// TRISHUL-SIM · UX-P1-A · ui-ux track (Rani)
//
// Implements SensorReadings & SwarmAgentState entity contracts
// matching docs/SCHEMA.md (Entity 2 & 3) and board/contracts/scenario-events.md.
// Provides SensorMock fixtures when swarm-ai physics are not merged.
// ============================================================

using System;
using UnityEngine;

namespace TrishulSim.UI
{
    /// <summary>
    /// Per-agent state snapshot embedded in SCENARIO_TICK (Entity 2).
    /// </summary>
    [Serializable]
    public class SwarmAgentState
    {
        public string agentId;
        public bool isDecoy;
        public Vector3 positionWorld;
        public Vector3 velocityMs;
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

    /// <summary>
    /// Synthetic sensor readings computed from swarm physics (Entity 3).
    /// </summary>
    [Serializable]
    public class SensorReadings
    {
        public float rfWaterfallPeakGHz;
        public float rfWaterfallPeakDbm;
        public float dopplerPeakHz;
        public float acousticSplDb;
        public int radarDetectedCount;

        public SensorReadings()
        {
            rfWaterfallPeakGHz = 0f;
            rfWaterfallPeakDbm = -90f; // Noise floor
            dopplerPeakHz = 0f;
            acousticSplDb = 30f; // Ambient noise
            radarDetectedCount = 0;
        }

        public SensorReadings(float rfGHz, float rfDbm, float dopplerHz, float acousticDb, int radarCount)
        {
            rfWaterfallPeakGHz = rfGHz;
            rfWaterfallPeakDbm = rfDbm;
            dopplerPeakHz = dopplerHz;
            acousticSplDb = acousticDb;
            radarDetectedCount = radarCount;
        }
    }

    /// <summary>
    /// Mock fixture generator for offline / pre-merge testing of sensor suite.
    /// </summary>
    public static class SensorMock
    {
        /// <summary>
        /// Baseline quiet border readings (Beat 2).
        /// </summary>
        public static SensorReadings GetBaselineFixture()
        {
            return new SensorReadings
            {
                rfWaterfallPeakGHz = 0f,
                rfWaterfallPeakDbm = -90f,
                dopplerPeakHz = 0f,
                acousticSplDb = 32.5f,
                radarDetectedCount = 0
            };
        }

        /// <summary>
        /// Punjab Zero-Visibility Incursion in-flight readings (Beat 3).
        /// RF spike at 5.825 GHz (-62.3 dBm), Doppler 220.4 Hz, Acoustic 54.2 dB.
        /// </summary>
        public static SensorReadings GetPunjabFixture()
        {
            return new SensorReadings
            {
                rfWaterfallPeakGHz = 5.825f,
                rfWaterfallPeakDbm = -62.3f,
                dopplerPeakHz = 220.4f,
                acousticSplDb = 54.2f,
                radarDetectedCount = 6
            };
        }

        /// <summary>
        /// 6-agent swarm snapshot for Punjab Pincer scenario:
        /// 2 Decoys (large RCS, 2.4 GHz) + 4 Attackers (low RCS, 5.825 GHz).
        /// </summary>
        public static SwarmAgentState[] GetPunjabSwarmFixture()
        {
            return new SwarmAgentState[]
            {
                // Decoys (large RCS 0.08m2, 2.4 GHz)
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
                    agentId = "agent-1",
                    isDecoy = true,
                    positionWorld = new Vector3(220.0f, 12.0f, -120.0f),
                    velocityMs = new Vector3(-28.0f, 0.0f, 10.0f),
                    headingDeg = 252.0f,
                    altitudeM = 12.0f,
                    rfEmitting = true,
                    rfFreqGHz = 2.4f,
                    rfPowerDbm = 20.0f,
                    propellerRpm = 9800,
                    rcs = 0.08f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                },
                // Attackers (small RCS 0.012m2, 5.825 GHz FPV)
                new SwarmAgentState
                {
                    agentId = "agent-2",
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
                },
                new SwarmAgentState
                {
                    agentId = "agent-3",
                    isDecoy = false,
                    positionWorld = new Vector3(148.0f, 8.0f, -82.0f),
                    velocityMs = new Vector3(-28.4f, -0.2f, 12.1f),
                    headingDeg = 247.0f,
                    altitudeM = 8.0f,
                    rfEmitting = true,
                    rfFreqGHz = 5.825f,
                    rfPowerDbm = 23.0f,
                    propellerRpm = 11400,
                    rcs = 0.012f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                },
                new SwarmAgentState
                {
                    agentId = "agent-4",
                    isDecoy = false,
                    positionWorld = new Vector3(155.0f, 8.5f, -95.0f),
                    velocityMs = new Vector3(-28.0f, 0.0f, 11.5f),
                    headingDeg = 245.0f,
                    altitudeM = 8.5f,
                    rfEmitting = true,
                    rfFreqGHz = 5.825f,
                    rfPowerDbm = 23.0f,
                    propellerRpm = 11400,
                    rcs = 0.012f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                },
                new SwarmAgentState
                {
                    agentId = "agent-5",
                    isDecoy = false,
                    positionWorld = new Vector3(138.0f, 7.8f, -79.0f),
                    velocityMs = new Vector3(-28.5f, -0.1f, 12.0f),
                    headingDeg = 248.0f,
                    altitudeM = 7.8f,
                    rfEmitting = true,
                    rfFreqGHz = 5.825f,
                    rfPowerDbm = 23.0f,
                    propellerRpm = 11400,
                    rcs = 0.012f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                }
            };
        }
    }
}
