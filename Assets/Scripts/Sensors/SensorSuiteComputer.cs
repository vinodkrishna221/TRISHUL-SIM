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
        [System.NonSerialized] public long? neutralisedAtMs;

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
            acousticSplDb = 0f;
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
    /// Sensor suite computing engine for the simulation (SA-P0-B Step 2).
    /// Runs on each 100 ms tick to update Friis RF & radar readings
    /// and evaluate ROE friendly VHF blackout risk.
    /// </summary>
    public class SensorSuiteComputer : MonoBehaviour
    {
        // ── State ─────────────────────────────────────────────
        public bool friendlyVhfActive;
        public EWJammerState currentEwState;
        public SensorReadings currentReadings;
        public string activeScenarioId;

        public SensorReadings CurrentSensorReadings
        {
            get { return currentReadings; }
        }

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
        /// Processes trainee action and kinetic fire for SCENARIO_TICK payload (SA-P1-D).
        /// If kineticFireTriggered is true, resolves lead-angle intercept for locked target,
        /// applies neutralisation on hit, and injects KineticEvent into action.kinetic.
        /// </summary>
        public KineticEvent ProcessTraineeAction(
            TraineeAction action,
            SwarmAgentState[] swarmSnapshot,
            long t,
            Vector3 traineePos,
            string targetAgentId = null)
        {
            if (action == null)
                return null;

            if (action.kineticFireTriggered)
            {
                SwarmAgentState target = null;
                if (!string.IsNullOrEmpty(targetAgentId) && swarmSnapshot != null)
                {
                    for (int i = 0; i < swarmSnapshot.Length; i++)
                    {
                        if (swarmSnapshot[i] != null && string.Equals(swarmSnapshot[i].agentId, targetAgentId, StringComparison.OrdinalIgnoreCase))
                        {
                            target = swarmSnapshot[i];
                            break;
                        }
                    }
                }

                if (target == null && swarmSnapshot != null)
                {
                    target = SmashFireControl.FindLockedTarget(
                        agents: swarmSnapshot,
                        aimBearingDeg: action.antennaBearingDeg,
                        aimElevationDeg: action.antennaElevationDeg,
                        traineePos: traineePos
                    );
                }

                if (target != null)
                {
                    KineticEvent ke = SmashFireControl.ComputeKineticEvent(
                        action: action,
                        target: target,
                        t: t,
                        traineePos: traineePos
                    );

                    if (ke.hit)
                    {
                        target.isNeutralised = true;
                        target.neutralisedAtMs = t;
                        target.velocityMs = Vector3.zero;
                        target.propellerRpm = 0;
                        target.rfEmitting = false;
                    }

                    action.kinetic = ke;
                    return ke;
                }
                else
                {
                    KineticEvent emptyKe = new KineticEvent(t, "none", false, 999.0f);
                    action.kinetic = emptyKe;
                    return emptyKe;
                }
            }

            return action.kinetic;
        }

        /// <summary>
        /// SA-P0-B Step 2: Computes multi-spectral sensor readings from a swarm snapshot and trainee antenna position.
        /// - rfWaterfallPeakDbm: iterate emitting agents -> compute Friis -> return max power agent's dBm
        /// - rfWaterfallPeakGHz: frequency of max-power agent
        /// - radarDetectedCount: count agents where two-way radar return > -90 dBm
        /// - dopplerPeakHz = 0.0f; acousticSplDb = 0.0f (P1 placeholders)
        /// </summary>
        public SensorReadings ComputeSensorReadings(SwarmAgentState[] snapshot, Vector3 antennaPos)
        {
            if (snapshot == null || snapshot.Length == 0)
            {
                currentReadings = new SensorReadings(0.0f, RfPhysics.NOISE_FLOOR_DBM, 0.0f, 0.0f, 0);
                return currentReadings;
            }

            float maxRfDbm = float.NegativeInfinity;
            float maxRfFreqGHz = 0.0f;
            bool foundEmitting = false;
            int radarHits = 0;

            for (int i = 0; i < snapshot.Length; i++)
            {
                SwarmAgentState agent = snapshot[i];
                if (agent == null || agent.isNeutralised)
                    continue;

                float dx = agent.positionWorld.x - antennaPos.x;
                float dy = agent.positionWorld.y - antennaPos.y;
                float dz = agent.positionWorld.z - antennaPos.z;
                float distM = (float)Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
                if (distM < RfPhysics.MIN_DISTANCE_METRES)
                    distM = RfPhysics.MIN_DISTANCE_METRES;

                if (agent.rfEmitting)
                {
                    float receivedDbm = RfPhysics.ComputeFriisDbm(agent.rfPowerDbm, agent.rfFreqGHz, distM);
                    if (!foundEmitting || receivedDbm > maxRfDbm)
                    {
                        maxRfDbm = receivedDbm;
                        maxRfFreqGHz = agent.rfFreqGHz;
                        foundEmitting = true;
                    }
                }

                // SA-P2-B performance optimization: early-out when agent distance > 500m
                // Beyond 500m, two-way radar return is well below -90 dBm threshold.
                if (distM > 500.0f)
                    continue;

                float radarReturnDbm = RfPhysics.ComputeRadarReturnDbm(agent.rfPowerDbm, agent.rfFreqGHz, distM, agent.rcs);
                if (radarReturnDbm > RfPhysics.RADAR_DETECTION_THRESHOLD_DBM)
                {
                    radarHits++;
                }
            }

            if (!foundEmitting)
            {
                maxRfDbm = RfPhysics.NOISE_FLOOR_DBM;
                maxRfFreqGHz = 0.0f;
            }

            currentReadings = new SensorReadings(
                maxRfFreqGHz,
                maxRfDbm,
                0.0f,
                0.0f,
                radarHits
            );

            return currentReadings;
        }
    }
}
