// ============================================================
// AcousticPhysics.cs — Acoustic BPF & ISO 9613-1 Atmospheric Attenuation Model
// TRISHUL-SIM · SA-P1-A · swarm-ai track (Venu)
//
// Implements deterministic acoustic Sound Pressure Level (SPL) propagation:
//   SPL_dB = SWL_dB − 20·log10(d) − alpha·d/1000
//   SWL_dB = 85 dB (reference FPV drone sound power level at full throttle)
//   alpha ≈ 0.08 dB/km at blade-tip harmonic (~220 Hz)
//   ISO 9613-1 temperature correction: alpha_corrected = alpha * (293 / (273 + temp_C))^0.5
//   Log-sum-exp energy addition across agents: 10·log10(sum_i 10^(SPL_i / 10))
// Contract: board/contracts/telemetry.md, docs/SCHEMA.md Entity 3, docs/PRD.md §3 Beat 3
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrishulSim.Sensors
{
    /// <summary>
    /// Static acoustic BPF propagation and ISO 9613-1 atmospheric attenuation functions (SA-P1-A Step 2).
    /// Zero external dependencies (ponytail).
    /// </summary>
    public static class AcousticPhysics
    {
        /// <summary>
        /// Reference Sound Power Level (SWL) in dB for an FPV drone at full throttle (SA-P1-A spec: 85 dB).
        /// </summary>
        public const float REFERENCE_SWL_DB = 85.0f;

        /// <summary>
        /// Baseline ISO 9613-1 atmospheric absorption coefficient in dB/km for ~220 Hz blade-tip harmonic:
        /// alpha ≈ 0.8 * (0.22)^1.5 ≈ 0.08 dB/km.
        /// </summary>
        public const float BASE_ALPHA_DB_PER_KM = 0.08f;

        /// <summary>
        /// ISO 9613-1 reference temperature in Kelvin (20°C = 293 K).
        /// </summary>
        public const float REFERENCE_TEMP_KELVIN = 293.0f;

        /// <summary>
        /// Zero Celsius offset in Kelvin (273 K).
        /// </summary>
        public const float KELVIN_OFFSET = 273.0f;

        /// <summary>
        /// Minimum distance clamp in metres to prevent log10(0) singularity.
        /// </summary>
        public const float MIN_DISTANCE_METRES = 0.1f;

        /// <summary>
        /// Default Punjab riverine winter fog temperature in Celsius (4.0°C).
        /// </summary>
        public const float DEFAULT_TEMP_CELSIUS = 4.0f;

        /// <summary>
        /// Computes the temperature-corrected ISO 9613-1 atmospheric attenuation coefficient alpha (dB/km):
        ///   alpha = baseAlpha * (293 / (273 + temp_C))^0.5
        /// </summary>
        public static float ComputeAlphaDbPerKm(float tempCelsius, float baseAlphaDbPerKm = BASE_ALPHA_DB_PER_KM)
        {
            float tempKelvin = KELVIN_OFFSET + tempCelsius;
            if (tempKelvin < 1.0f)
                tempKelvin = 1.0f;

            float correction = (float)Math.Sqrt(REFERENCE_TEMP_KELVIN / tempKelvin);
            return baseAlphaDbPerKm * correction;
        }

        /// <summary>
        /// SA-P1-A Step 2: Computes single-agent Sound Pressure Level (SPL) in dB at the trainee antenna:
        ///   SPL_dB = SWL_dB − 20·log10(d) − alpha·d/1000
        /// where SWL_dB = 85 dB and alpha is temperature-corrected via ISO 9613-1.
        /// Returns 0.0f if agent is null, neutralised, or has zero propeller RPM.
        /// </summary>
        public static float ComputeSplDb(SwarmAgentState agent, Vector3 antennaPos, float tempCelsius = DEFAULT_TEMP_CELSIUS)
        {
            if (agent == null || agent.isNeutralised || agent.propellerRpm <= 0)
                return 0.0f;

            float dx = agent.positionWorld.x - antennaPos.x;
            float dy = agent.positionWorld.y - antennaPos.y;
            float dz = agent.positionWorld.z - antennaPos.z;
            float distM = (float)Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            if (distM < MIN_DISTANCE_METRES)
                distM = MIN_DISTANCE_METRES;

            float spreadingLossDb = 20.0f * (float)Math.Log10(distM);
            float alphaDbPerKm = ComputeAlphaDbPerKm(tempCelsius);
            float atmosphericLossDb = alphaDbPerKm * (distM / 1000.0f);

            float splDb = REFERENCE_SWL_DB - spreadingLossDb - atmosphericLossDb;
            return splDb > 0.0f ? splDb : 0.0f;
        }

        /// <summary>
        /// SA-P1-A Step 2: Numerically stable log-sum-exp energy addition in the dB domain across agents:
        ///   L_total = 10·log10( sum_i 10^(L_i / 10) )
        ///           = L_max + 10·log10( sum_i 10^((L_i - L_max) / 10) )
        /// Ignores non-positive (0.0f / silent / neutralised) or NaN entries.
        /// </summary>
        public static float LogSumSpl(float[] splValues)
        {
            if (splValues == null || splValues.Length == 0)
                return 0.0f;

            return LogSumSpl(splValues, splValues.Length);
        }

        /// <summary>
        /// Count-aware overload of LogSumSpl for zero-allocation hot-path buffer reuse.
        /// </summary>
        public static float LogSumSpl(float[] splValues, int count)
        {
            if (splValues == null || count <= 0)
                return 0.0f;

            int limit = count < splValues.Length ? count : splValues.Length;
            float maxSpl = float.NegativeInfinity;
            for (int i = 0; i < limit; i++)
            {
                float val = splValues[i];
                if (!float.IsNaN(val) && !float.IsInfinity(val) && val > 0.0f && val > maxSpl)
                {
                    maxSpl = val;
                }
            }

            if (float.IsNegativeInfinity(maxSpl))
                return 0.0f;

            double sumEnergy = 0.0;
            for (int i = 0; i < limit; i++)
            {
                float val = splValues[i];
                if (!float.IsNaN(val) && !float.IsInfinity(val) && val > 0.0f)
                {
                    sumEnergy += Math.Pow(10.0, (val - maxSpl) / 10.0);
                }
            }

            if (sumEnergy <= 0.0)
                return 0.0f;

            return maxSpl + (float)(10.0 * Math.Log10(sumEnergy));
        }

        /// <summary>
        /// Computes combined swarm acoustic SPL in dB across all non-neutralised agents in a snapshot.
        /// </summary>
        public static float ComputeSwarmSplDb(SwarmAgentState[] snapshot, Vector3 antennaPos, float tempCelsius = DEFAULT_TEMP_CELSIUS)
        {
            if (snapshot == null || snapshot.Length == 0)
                return 0.0f;

            List<float> activeSpls = new List<float>(snapshot.Length);
            for (int i = 0; i < snapshot.Length; i++)
            {
                SwarmAgentState agent = snapshot[i];
                if (agent == null || agent.isNeutralised || agent.propellerRpm <= 0)
                    continue;

                float spl = ComputeSplDb(agent, antennaPos, tempCelsius);
                if (spl > 0.0f)
                {
                    activeSpls.Add(spl);
                }
            }

            if (activeSpls.Count == 0)
                return 0.0f;

            return LogSumSpl(activeSpls.ToArray());
        }
    }
}
