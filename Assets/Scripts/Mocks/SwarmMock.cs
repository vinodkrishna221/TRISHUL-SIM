// ============================================================
// SwarmMock.cs — Swarm Agent Mock Fixtures
// TRISHUL-SIM · SA-P3-B / SA-P1-D mock path · swarm-ai track (Uday)
//
// Provides stable contract fixtures for isolated unit testing,
// offline simulation, and cross-track integration without live Boids.
// Contract: board/contracts/scenario-events.md, board/contracts/telemetry.md, docs/SCHEMA.md
// ============================================================

using System;
using UnityEngine;
using TrishulSim.Sensors;

namespace TrishulSim.Mocks
{
    public static class SwarmMock
    {
        /// <summary>
        /// Returns the standard 6-agent Punjab scenario swarm snapshot fixture
        /// at T=45200ms (Beat 4 pincer engagement moment from SCHEMA.md / telemetry.md).
        /// 2 Decoys (agents 0, 1) + 4 Attackers (agents 2, 3, 4, 5).
        /// </summary>
        public static SwarmAgentState[] GetPunjabFixture()
        {
            return new SwarmAgentState[]
            {
                // Decoy 0: 2.4 GHz, larger RCS (0.08 m²), higher altitude, peeling away
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

                // Decoy 1: 2.4 GHz, larger RCS (0.08 m²), climbing
                new SwarmAgentState
                {
                    agentId = "agent-1",
                    isDecoy = true,
                    positionWorld = new Vector3(180.0f, 12.0f, -120.0f),
                    velocityMs = new Vector3(-27.0f, 0.0f, 12.0f),
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

                // Attacker 2: FPV munition, 5.825 GHz, low-level pincer
                new SwarmAgentState
                {
                    agentId = "agent-2",
                    isDecoy = false,
                    positionWorld = new Vector3(155.0f, 8.0f, -75.0f),
                    velocityMs = new Vector3(-29.0f, -0.1f, 11.5f),
                    headingDeg = 245.0f,
                    altitudeM = 8.0f,
                    rfEmitting = true,
                    rfFreqGHz = 5.825f,
                    rfPowerDbm = 23.0f,
                    propellerRpm = 11400,
                    rcs = 0.012f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                },

                // Attacker 3: Exact canonical agent from docs/SCHEMA.md & board/contracts/telemetry.md
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
                    propellerRpm = 11400,
                    rcs = 0.012f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                },

                // Attacker 4: FPV munition, paired pincer wingman
                new SwarmAgentState
                {
                    agentId = "agent-4",
                    isDecoy = false,
                    positionWorld = new Vector3(160.0f, 8.5f, -95.0f),
                    velocityMs = new Vector3(-28.0f, -0.2f, 12.0f),
                    headingDeg = 248.0f,
                    altitudeM = 8.5f,
                    rfEmitting = true,
                    rfFreqGHz = 5.825f,
                    rfPowerDbm = 23.0f,
                    propellerRpm = 11400,
                    rcs = 0.012f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                },

                // Attacker 5: FPV munition, closing at 120 km/h
                new SwarmAgentState
                {
                    agentId = "agent-5",
                    isDecoy = false,
                    positionWorld = new Vector3(150.0f, 7.8f, -65.0f),
                    velocityMs = new Vector3(-30.0f, -0.1f, 11.0f),
                    headingDeg = 246.0f,
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
