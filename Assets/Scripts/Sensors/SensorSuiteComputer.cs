// ============================================================
// SensorSuiteComputer.cs — Multi-Spectral Sensor Suite Computer
// TRISHUL-SIM · SA-P1-C · swarm-ai track (Uday)
//
// Updates live sensor readings and computes EW ROE deconfliction
// on every 10 Hz SCENARIO_TICK heartbeat.
// ============================================================

using System;
using UnityEngine;

namespace TrishulSim.Sensors
{
    /// <summary>
    /// Swarm agent state representation (SCHEMA.md Entity 2).
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
        public long? neutralisedAtMs;

        public SwarmAgentState()
        {
            agentId = "agent-0";
            isDecoy = false;
            positionWorld = Vector3.zero;
            velocityMs = Vector3.zero;
            headingDeg = 0f;
            altitudeM = 8f;
            rfEmitting = true;
            rfFreqGHz = 5.825f;
            rfPowerDbm = 23f;
            propellerRpm = 11400;
            rcs = 0.012f;
            isNeutralised = false;
            neutralisedAtMs = null;
        }
    }

    /// <summary>
    /// Multi-spectral sensor readings passed in SCENARIO_TICK payload (telemetry.md / SCHEMA.md Entity 3).
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
            rfWaterfallPeakDbm = -90f;
            dopplerPeakHz = 0f;
            acousticSplDb = 32.5f;
            radarDetectedCount = 0;
        }

        public SensorReadings(float rfWaterfallPeakGHz, float rfWaterfallPeakDbm, float dopplerPeakHz, float acousticSplDb, int radarDetectedCount)
        {
            this.rfWaterfallPeakGHz = rfWaterfallPeakGHz;
            this.rfWaterfallPeakDbm = rfWaterfallPeakDbm;
            this.dopplerPeakHz = dopplerPeakHz;
            this.acousticSplDb = acousticSplDb;
            this.radarDetectedCount = radarDetectedCount;
        }
    }

    /// <summary>
    /// Sensor suite computing engine for the simulation.
    /// Runs on each 100 ms tick to update RF/Doppler/Acoustic readings
    /// and evaluate ROE friendly VHF blackout risk.
    /// </summary>
    public class SensorSuiteComputer
    {
        // ── State ─────────────────────────────────────────────
        public bool friendlyVhfActive;
        public EWJammerState currentEwState;
        public SensorReadings currentReadings;
        public string activeScenarioId;

        public SensorSuiteComputer()
        {
            friendlyVhfActive = true;
            currentEwState = new EWJammerState();
            currentReadings = new SensorReadings();
            activeScenarioId = "punjab-fog-pincer-01";
        }

        /// <summary>
        /// Configures scenario parameters.
        /// Punjab scenario (punjab-fog-pincer-01): friendlyVhfActive = true.
        /// </summary>
        public void ConfigureScenario(string scenarioId, bool? vhfActiveOverride = null)
        {
            activeScenarioId = scenarioId;
            if (vhfActiveOverride.HasValue)
            {
                friendlyVhfActive = vhfActiveOverride.Value;
            }
            else
            {
                // Doctrinal default: Punjab border forward post has friendly VHF active
                friendlyVhfActive = string.IsNullOrEmpty(scenarioId) || scenarioId.StartsWith("punjab", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Heartbeat update called on each 100 ms tick.
        /// Evaluates EwDeconfliction and updates current EW state and friendlyBlackoutRisk.
        /// </summary>
        public void UpdateTick(EWJammerState jammer, bool? vhfActiveOverride = null)
        {
            if (vhfActiveOverride.HasValue)
            {
                friendlyVhfActive = vhfActiveOverride.Value;
            }

            if (jammer != null)
            {
                // ROE deconfliction evaluation (SA-P1-C)
                jammer.friendlyBlackoutRisk = EwDeconfliction.ComputeBlackoutRisk(jammer, friendlyVhfActive);
                currentEwState = jammer;
            }
        }

        /// <summary>
        /// Injects the computed EW state and friendlyVhfActive flag into the SCENARIO_TICK broadcast payload.
        /// </summary>
        public void InjectTickPayload(EWJammerState tickJammer, out bool vhfActive)
        {
            vhfActive = friendlyVhfActive;
            if (tickJammer != null)
            {
                tickJammer.friendlyBlackoutRisk = EwDeconfliction.ComputeBlackoutRisk(tickJammer, friendlyVhfActive);
            }
        }

        /// <summary>
        /// Computes or updates multi-spectral sensor readings from a swarm snapshot and trainee antenna position.
        /// </summary>
        public SensorReadings ComputeSensorReadings(SwarmAgentState[] snapshot, Vector3 antennaPos)
        {
            if (snapshot == null || snapshot.Length == 0)
            {
                currentReadings = new SensorReadings(0f, -90f, 0f, 32.5f, 0);
                return currentReadings;
            }

            float maxRfDbm = -120f;
            float maxRfFreqGHz = 0f;
            float maxDopplerHz = 0f;
            float maxSplDb = 0f;
            int radarHits = 0;

            for (int i = 0; i < snapshot.Length; i++)
            {
                SwarmAgentState agent = snapshot[i];
                if (agent == null || agent.isNeutralised)
                    continue;

                // Simple range approximation for radar/sensor return
                float dx = agent.positionWorld.x - antennaPos.x;
                float dy = agent.positionWorld.y - antennaPos.y;
                float dz = agent.positionWorld.z - antennaPos.z;
                float distM = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (distM < 1.0f) distM = 1.0f;

                if (agent.rfEmitting && agent.rfPowerDbm > maxRfDbm)
                {
                    maxRfDbm = agent.rfPowerDbm;
                    maxRfFreqGHz = agent.rfFreqGHz;
                }

                if (distM <= 350.0f)
                {
                    radarHits++;
                }

                // Blade-tip harmonic approx
                float bladeTip = (agent.propellerRpm / 60f) * 4f * 0.289f; // ~220 Hz
                if (bladeTip > maxDopplerHz)
                {
                    maxDopplerHz = bladeTip;
                }

                float spl = 85.0f - (20.0f * (float)Math.Log10(distM));
                if (spl > maxSplDb)
                {
                    maxSplDb = spl;
                }
            }

            currentReadings = new SensorReadings(
                maxRfFreqGHz > 0 ? maxRfFreqGHz : 5.825f,
                maxRfDbm > -120f ? maxRfDbm - 40f : -62.3f,
                maxDopplerHz > 0 ? maxDopplerHz : 220.4f,
                maxSplDb > 0 ? maxSplDb : 54.2f,
                radarHits
            );

            return currentReadings;
        }
    }
}
