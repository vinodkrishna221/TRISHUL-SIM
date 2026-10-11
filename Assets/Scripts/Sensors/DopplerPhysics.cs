// ============================================================
// DopplerPhysics.cs — Micro-Doppler Blade-Tip Harmonic Physics Model
// TRISHUL-SIM · SA-P1-A · swarm-ai track (Venu)
//
// Implements deterministic blade-tip Doppler shift calculation:
//   f_blade_tip_Hz = (propellerRpm / 60) * bladeCount * (1 + v_radial / c)
//   bladeCount = 4 (typical FPV quad propeller)
//   c = 343 m/s (speed of sound)
//   v_radial = dot(agent.velocityMs, unit vector agent -> antenna)
// Contract: board/contracts/telemetry.md, docs/SCHEMA.md Entity 3, docs/PRD.md §3 Beat 3
// ============================================================

using System;
using UnityEngine;

namespace TrishulSim.Sensors
{
    /// <summary>
    /// Static Micro-Doppler blade-tip harmonic physics calculations (SA-P1-A Step 1).
    /// Zero external dependencies (ponytail).
    /// </summary>
    public static class DopplerPhysics
    {
        /// <summary>
        /// Number of propeller blades for a typical FPV quadrotor munition / decoy.
        /// </summary>
        public const int DEFAULT_BLADE_COUNT = 4;

        /// <summary>
        /// Speed of sound in air (m/s).
        /// </summary>
        public const float SPEED_OF_SOUND_MS = 343.0f;

        /// <summary>
        /// Minimum distance squared threshold to safely normalize the agent-to-antenna vector.
        /// </summary>
        private const float MIN_DIST_SQ = 1e-6f;

        /// <summary>
        /// Computes the radial closing velocity (m/s) of an agent toward the trainee antenna:
        ///   v_radial = dot(agent.velocityMs, unit_vec(agent -> antenna))
        /// Positive when the agent is approaching the antenna; negative when receding.
        /// </summary>
        public static float ComputeRadialVelocityMs(SwarmAgentState agent, Vector3 antennaPos)
        {
            if (agent == null || agent.isNeutralised)
                return 0.0f;

            float dx = antennaPos.x - agent.positionWorld.x;
            float dy = antennaPos.y - agent.positionWorld.y;
            float dz = antennaPos.z - agent.positionWorld.z;
            float distSq = (dx * dx) + (dy * dy) + (dz * dz);

            if (distSq <= MIN_DIST_SQ)
                return 0.0f;

            float invDist = 1.0f / (float)Math.Sqrt(distSq);
            float ux = dx * invDist;
            float uy = dy * invDist;
            float uz = dz * invDist;

            return (agent.velocityMs.x * ux)
                 + (agent.velocityMs.y * uy)
                 + (agent.velocityMs.z * uz);
        }

        /// <summary>
        /// SA-P1-A Step 1: Computes the Doppler-shifted blade-tip harmonic frequency in Hz for a single agent:
        ///   f_blade_tip_Hz = (propellerRpm / 60) * bladeCount * (1 + v_radial / c)
        /// where bladeCount = 4, c = 343 m/s, and v_radial = dot(agent.velocityMs, unit vec agent->antenna).
        /// Returns 0.0f for null, neutralised, or zero-RPM agents.
        /// </summary>
        public static float ComputeBladeTipHz(SwarmAgentState agent, Vector3 antennaPos)
        {
            if (agent == null || agent.isNeutralised || agent.propellerRpm <= 0)
                return 0.0f;

            float vRadial = ComputeRadialVelocityMs(agent, antennaPos);
            float dopplerFactor = 1.0f + (vRadial / SPEED_OF_SOUND_MS);
            if (dopplerFactor < 0.0f)
                dopplerFactor = 0.0f;

            float shaftHz = agent.propellerRpm / 60.0f;
            return shaftHz * DEFAULT_BLADE_COUNT * dopplerFactor;
        }

        /// <summary>
        /// Computes the 1st-harmonic rotor shaft Doppler frequency in Hz:
        ///   f_fundamental_Hz = (propellerRpm / 60) * (1 + v_radial / c)
        /// (~208–220 Hz for 11400 RPM FPV approaching at 120 km/h; see PRD §3 Beat 3).
        /// </summary>
        public static float ComputeFundamentalHarmonicHz(SwarmAgentState agent, Vector3 antennaPos)
        {
            if (agent == null || agent.isNeutralised || agent.propellerRpm <= 0)
                return 0.0f;

            float vRadial = ComputeRadialVelocityMs(agent, antennaPos);
            float dopplerFactor = 1.0f + (vRadial / SPEED_OF_SOUND_MS);
            if (dopplerFactor < 0.0f)
                dopplerFactor = 0.0f;

            return (agent.propellerRpm / 60.0f) * dopplerFactor;
        }

        /// <summary>
        /// SA-P1-A Step 1: Reports the peak blade-tip Doppler frequency across all non-neutralised agents.
        /// Returns 0.0f if snapshot is null, empty, or all agents are neutralised.
        /// </summary>
        public static float ComputePeakBladeTipHz(SwarmAgentState[] snapshot, Vector3 antennaPos)
        {
            if (snapshot == null || snapshot.Length == 0)
                return 0.0f;

            float peakHz = 0.0f;
            for (int i = 0; i < snapshot.Length; i++)
            {
                SwarmAgentState agent = snapshot[i];
                if (agent == null || agent.isNeutralised || agent.propellerRpm <= 0)
                    continue;

                float hz = ComputeBladeTipHz(agent, antennaPos);
                if (hz > peakHz)
                {
                    peakHz = hz;
                }
            }

            return peakHz;
        }
    }
}
