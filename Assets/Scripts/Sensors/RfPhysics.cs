// ============================================================
// RfPhysics.cs — Free-Space Path Loss (Friis) & Monostatic Radar Return Model
// TRISHUL-SIM · SA-P0-B · swarm-ai track (Venu)
//
// Implements deterministic RF propagation physics:
//   FSPL_dB = 20·log10(d_km) + 20·log10(f_GHz) + 92.45
//   P_received_dBm = P_transmitted_dBm − FSPL_dB
//   Radar return (two-way): P_tx_dBm − TwoWayLoss_dB + 10·log10(rcs_m2)
// Contract: board/contracts/scenario-events.md, board/contracts/telemetry.md, docs/SCHEMA.md Entity 3
// ============================================================

using System;
using UnityEngine;

#if !UNITY_5_3_OR_NEWER
namespace UnityEngine
{
    public class Transform
    {
        public Vector3 position = Vector3.zero;
        public Vector3 localScale = new Vector3(1f, 1f, 1f);
    }

    public class GameObject
    {
        public string name = string.Empty;
        public bool activeSelf = true;
        public Transform transform = new Transform();

        public GameObject() { }
        public GameObject(string name) { this.name = name ?? string.Empty; }

        public void SetActive(bool active)
        {
            activeSelf = active;
        }
    }

    public class MonoBehaviour
    {
        private GameObject _gameObject;
        public GameObject gameObject
        {
            get
            {
                if (_gameObject == null)
                    _gameObject = new GameObject(GetType().Name);
                return _gameObject;
            }
            set { _gameObject = value; }
        }

        public Transform transform
        {
            get { return gameObject.transform; }
        }

        public bool enabled = true;

        public static void Destroy(object obj)
        {
            GameObject go = obj as GameObject;
            if (go != null)
            {
                go.activeSelf = false;
                return;
            }
            MonoBehaviour mb = obj as MonoBehaviour;
            if (mb != null && mb._gameObject != null)
            {
                mb._gameObject.activeSelf = false;
            }
        }
    }
}
#endif

namespace TrishulSim.Sensors
{
    /// <summary>
    /// Static RF propagation and monostatic radar physics functions (SA-P0-B Step 1).
    /// Zero external dependencies (ponytail).
    /// </summary>
    public static class RfPhysics
    {
        /// <summary>
        /// Standard Friis free-space path loss constant for distance in km and frequency in GHz:
        /// 20 * log10(4 * pi * 10^3 * 10^9 / c) = 92.45 dB.
        /// </summary>
        public const float FSPL_CONSTANT_DB = 92.45f;

        /// <summary>
        /// Additional spherical spreading factor 10 * log10(4 * pi) = 10.9921 dB for the monostatic radar return leg.
        /// </summary>
        public const float RADAR_SPHERICAL_SPREADING_DB = 10.9920986f;

        /// <summary>
        /// Minimum radar return threshold in dBm for target detection (SA-P0-B spec: > -90 dBm).
        /// </summary>
        public const float RADAR_DETECTION_THRESHOLD_DBM = -90.0f;

        /// <summary>
        /// Baseline receiver thermal noise floor in dBm.
        /// </summary>
        public const float NOISE_FLOOR_DBM = -90.0f;

        /// <summary>
        /// Minimum distance clamp in metres to guard against log10(0) singularity.
        /// </summary>
        public const float MIN_DISTANCE_METRES = 0.1f;

        /// <summary>
        /// Minimum frequency clamp in GHz to guard against log10(0) singularity.
        /// </summary>
        public const float MIN_FREQ_GHZ = 0.001f;

        /// <summary>
        /// Minimum radar cross-section clamp in m² to guard against log10(0) singularity.
        /// </summary>
        public const float MIN_RCS_M2 = 1e-6f;

        /// <summary>
        /// Computes one-way Free-Space Path Loss (FSPL) in dB:
        ///   FSPL_dB = 20·log10(d_km) + 20·log10(f_GHz) + 92.45
        /// </summary>
        public static float ComputeFsplDb(float freqGHz, float distanceMetres)
        {
            float clampedDistM = distanceMetres > MIN_DISTANCE_METRES ? distanceMetres : MIN_DISTANCE_METRES;
            float clampedFreqGHz = freqGHz > MIN_FREQ_GHZ ? freqGHz : MIN_FREQ_GHZ;
            float dKm = clampedDistM / 1000.0f;

            return (20.0f * (float)Math.Log10(dKm))
                 + (20.0f * (float)Math.Log10(clampedFreqGHz))
                 + FSPL_CONSTANT_DB;
        }

        /// <summary>
        /// SA-P0-B Step 1: Computes one-way received RF signal power in dBm using Friis propagation:
        ///   P_received_dBm = P_transmitted_dBm − FSPL_dB
        ///   FSPL_dB = 20·log10(d_km) + 20·log10(f_GHz) + 92.45
        /// </summary>
        public static float ComputeFriisDbm(float txPowerDbm, float freqGHz, float distanceMetres)
        {
            float fsplDb = ComputeFsplDb(freqGHz, distanceMetres);
            return txPowerDbm - fsplDb;
        }

        /// <summary>
        /// SA-P0-B Step 1: Computes two-way monostatic radar return power in dBm:
        ///   radar_return_dBm = txPowerDbm − TwoWayLoss_dB + 10·log10(rcsM2)
        /// where TwoWayLoss_dB applies the distance path loss twice (outbound + reflected return leg).
        /// </summary>
        public static float ComputeRadarReturnDbm(float txPowerDbm, float freqGHz, float distanceMetres, float rcsM2)
        {
            if (rcsM2 <= 0.0f)
                return -999.0f;

            float clampedDistM = distanceMetres > MIN_DISTANCE_METRES ? distanceMetres : MIN_DISTANCE_METRES;
            float dKm = clampedDistM / 1000.0f;

            float fsplOneWayDb = ComputeFsplDb(freqGHz, clampedDistM);
            float returnLegLossDb = (20.0f * (float)Math.Log10(dKm)) + RADAR_SPHERICAL_SPREADING_DB;
            float twoWayLossDb = fsplOneWayDb + returnLegLossDb;

            float clampedRcs = rcsM2 > MIN_RCS_M2 ? rcsM2 : MIN_RCS_M2;
            float rcsGainDb = 10.0f * (float)Math.Log10(clampedRcs);

            return txPowerDbm - twoWayLossDb + rcsGainDb;
        }

        /// <summary>
        /// Returns true if the agent's two-way radar return exceeds the -90 dBm detection threshold.
        /// </summary>
        public static bool IsRadarDetected(
            float txPowerDbm,
            float freqGHz,
            float distanceMetres,
            float rcsM2,
            float thresholdDbm = RADAR_DETECTION_THRESHOLD_DBM)
        {
            return ComputeRadarReturnDbm(txPowerDbm, freqGHz, distanceMetres, rcsM2) > thresholdDbm;
        }
    }
}
