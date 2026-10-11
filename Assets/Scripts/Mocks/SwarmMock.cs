// ============================================================
// SwarmMock.cs — Swarm Agent Mock Fixtures & Scenario Profiles
// TRISHUL-SIM · SA-P3-B / SA-P1-D / SA-P2-A mock path · swarm-ai track (Uday)
//
// Provides stable contract fixtures for isolated unit testing,
// offline simulation, and cross-track integration without live Boids.
// Contract: board/contracts/scenario-events.md, board/contracts/telemetry.md, docs/SCHEMA.md
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using TrishulSim.Sensors;
using TrishulSim.SwarmAI;

namespace TrishulSim.Mocks
{
    public static class SwarmMock
    {
        // ── Canonical Swarm Profiles (docs/SCHEMA.md Entity 1) ───────────

        public static SwarmProfile GetPunjabProfile()
        {
            return new SwarmProfile(
                agentCount: 6,
                decoyCount: 2,
                attackerCount: 4,
                tacticTree: "PINCER_LOW_FAST",
                speedKmh: 120.0f,
                altitudeM: 8.0f,
                rfFreqGHz: new float[] { 5.825f, 2.4f },
                rfPowerDbm: 23.0f
            );
        }

        public static SwarmProfile GetLadakhProfile()
        {
            return new SwarmProfile(
                agentCount: 8,
                decoyCount: 4,
                attackerCount: 4,
                tacticTree: "SATURATION_DIVE",
                speedKmh: 100.0f,
                altitudeM: 25.0f, // Sim terrain relative altitude
                rfFreqGHz: new float[] { 2.4f, 5.8f },
                rfPowerDbm: 23.0f
            );
        }

        public static SwarmProfile GetLocProfile()
        {
            return new SwarmProfile(
                agentCount: 5,
                decoyCount: 1,
                attackerCount: 4,
                tacticTree: "MULTI-AXIS_SPLIT",
                speedKmh: 80.0f,
                altitudeM: 45.0f,
                rfFreqGHz: new float[] { 0.915f },
                rfPowerDbm: 20.0f
            );
        }

        public static SwarmProfile GetDesertProfile()
        {
            return new SwarmProfile(
                agentCount: 8,
                decoyCount: 2,
                attackerCount: 6,
                tacticTree: "SATURATION_DIVE",
                speedKmh: 110.0f,
                altitudeM: 15.0f,
                rfFreqGHz: new float[] { 2.4f, 5.8f },
                rfPowerDbm: 23.0f
            );
        }

        // ── Agent Fixtures ───────────────────────────────────────────────

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

        /// <summary>
        /// Returns initial 8-agent Ladakh scenario fixture in broad front formation.
        /// 4 Decoys (0..3) + 4 Attackers (4..7) spanning 105m broad front.
        /// </summary>
        public static SwarmAgentState[] GetLadakhFixture(float spawnDistanceM = 300.0f, float lateralSpacingM = 15.0f)
        {
            SwarmAgentState[] agents = new SwarmAgentState[8];
            float totalWidth = (8 - 1) * lateralSpacingM;
            float startX = -totalWidth * 0.5f;
            float alt = 25.0f;

            for (int i = 0; i < 8; i++)
            {
                bool isDecoy = i < 4;
                float x = startX + (i * lateralSpacingM);
                float z = spawnDistanceM;

                agents[i] = new SwarmAgentState
                {
                    agentId = "agent-" + i,
                    isDecoy = isDecoy,
                    positionWorld = new Vector3(x, alt, z),
                    velocityMs = new Vector3(0.0f, 0.0f, -27.78f), // 100 km/h towards origin
                    headingDeg = 180.0f,
                    altitudeM = alt,
                    rfEmitting = true,
                    rfFreqGHz = (i % 2 == 0) ? 2.4f : 5.8f,
                    rfPowerDbm = 23.0f,
                    propellerRpm = isDecoy ? 9800 : 11400,
                    rcs = isDecoy ? 0.08f : 0.012f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                };
            }

            return agents;
        }

        /// <summary>
        /// Returns initial 5-agent LoC scenario fixture in cardinal split formation.
        /// 1 Decoy (agent-0, North/frontal) + 4 Attackers (1..4 split into 4 compass points).
        /// </summary>
        public static SwarmAgentState[] GetLocFixture(float spawnDistanceM = 300.0f)
        {
            float alt = 45.0f;
            float speedMs = 80.0f / 3.6f; // 22.22 m/s

            return new SwarmAgentState[]
            {
                // Decoy: Direct frontal approach from North (0, 45, +spawnDistance)
                new SwarmAgentState
                {
                    agentId = "agent-0",
                    isDecoy = true,
                    positionWorld = new Vector3(0.0f, alt, spawnDistanceM),
                    velocityMs = new Vector3(0.0f, 0.0f, -speedMs),
                    headingDeg = 180.0f,
                    altitudeM = alt,
                    rfEmitting = true,
                    rfFreqGHz = 0.915f,
                    rfPowerDbm = 20.0f,
                    propellerRpm = 9800,
                    rcs = 0.08f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                },

                // Attacker 1: North approach (0, alt, +spawnDistance) -> moves South
                new SwarmAgentState
                {
                    agentId = "agent-1",
                    isDecoy = false,
                    positionWorld = new Vector3(0.0f, alt, spawnDistanceM),
                    velocityMs = new Vector3(0.0f, 0.0f, -speedMs),
                    headingDeg = 180.0f,
                    altitudeM = alt,
                    rfEmitting = true,
                    rfFreqGHz = 0.915f,
                    rfPowerDbm = 20.0f,
                    propellerRpm = 11400,
                    rcs = 0.012f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                },

                // Attacker 2: East approach (+spawnDistance, alt, 0) -> moves West
                new SwarmAgentState
                {
                    agentId = "agent-2",
                    isDecoy = false,
                    positionWorld = new Vector3(spawnDistanceM, alt, 0.0f),
                    velocityMs = new Vector3(-speedMs, 0.0f, 0.0f),
                    headingDeg = 270.0f,
                    altitudeM = alt,
                    rfEmitting = true,
                    rfFreqGHz = 0.915f,
                    rfPowerDbm = 20.0f,
                    propellerRpm = 11400,
                    rcs = 0.012f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                },

                // Attacker 3: South approach (0, alt, -spawnDistance) -> moves North
                new SwarmAgentState
                {
                    agentId = "agent-3",
                    isDecoy = false,
                    positionWorld = new Vector3(0.0f, alt, -spawnDistanceM),
                    velocityMs = new Vector3(0.0f, 0.0f, speedMs),
                    headingDeg = 0.0f,
                    altitudeM = alt,
                    rfEmitting = true,
                    rfFreqGHz = 0.915f,
                    rfPowerDbm = 20.0f,
                    propellerRpm = 11400,
                    rcs = 0.012f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                },

                // Attacker 4: West approach (-spawnDistance, alt, 0) -> moves East
                new SwarmAgentState
                {
                    agentId = "agent-4",
                    isDecoy = false,
                    positionWorld = new Vector3(-spawnDistanceM, alt, 0.0f),
                    velocityMs = new Vector3(speedMs, 0.0f, 0.0f),
                    headingDeg = 90.0f,
                    altitudeM = alt,
                    rfEmitting = true,
                    rfFreqGHz = 0.915f,
                    rfPowerDbm = 20.0f,
                    propellerRpm = 11400,
                    rcs = 0.012f,
                    isNeutralised = false,
                    neutralisedAtMs = null
                }
            };
        }
    }
}
