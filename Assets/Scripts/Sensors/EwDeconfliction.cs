// ============================================================
// EwDeconfliction.cs — Electronic Warfare ROE Deconfliction Logic
// TRISHUL-SIM · SA-P1-C · swarm-ai track (Uday)
//
// Computes friendlyBlackoutRisk for EWJammerState (telemetry.md / SCHEMA.md).
// Prevents friendly VHF radio network disruption (ROE compliance metric).
// ============================================================

using System;

#if !UNITY_5_3_OR_NEWER
namespace UnityEngine
{
    [Serializable]
    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 zero { get { return new Vector3(0f, 0f, 0f); } }
        public float sqrMagnitude { get { return (x * x) + (y * y) + (z * z); } }
        public float magnitude { get { return (float)Math.Sqrt(sqrMagnitude); } }
    }
}
#endif

namespace TrishulSim.Sensors
{
    /// <summary>
    /// Current state of the Directional EW Jammer (Entity 3 / telemetry.md).
    /// Contract: board/contracts/telemetry.md, board/contracts/scenario-events.md.
    /// </summary>
    [Serializable]
    public class EWJammerState
    {
        public bool engaged;
        public float bearingDeg;
        public float elevationDeg;
        public float bandGHz;
        public bool friendlyBlackoutRisk;

        public EWJammerState()
        {
            engaged = false;
            bearingDeg = 0f;
            elevationDeg = 0f;
            bandGHz = 5.825f;
            friendlyBlackoutRisk = false;
        }

        public EWJammerState(bool isEngaged, float bearing, float elevation, float band, bool blackoutRisk = false)
        {
            engaged = isEngaged;
            bearingDeg = bearing;
            elevationDeg = elevation;
            bandGHz = band;
            friendlyBlackoutRisk = blackoutRisk;
        }

        public EWJammerState Clone()
        {
            return new EWJammerState(engaged, bearingDeg, elevationDeg, bandGHz, friendlyBlackoutRisk);
        }
    }

    /// <summary>
    /// ROE Deconfliction computation for the Directional EW Jammer.
    /// Evaluates if active jamming overlaps friendly VHF communications antenna pattern.
    /// Doctrinal rule: roeCompliance = 1.0 if zero records have friendlyBlackoutRisk == true.
    /// </summary>
    public static class EwDeconfliction
    {
        /// <summary>
        /// Doctrinal azimuth bearing of friendly forward base VHF communications antenna (90° East).
        /// </summary>
        public const float FRIENDLY_VHF_BEARING_DEG = 90.0f;

        /// <summary>
        /// Elevation of friendly forward VHF antenna (0° horizon).
        /// </summary>
        public const float FRIENDLY_VHF_ELEVATION_DEG = 0.0f;

        /// <summary>
        /// Total beamwidth of friendly VHF antenna pattern (60°).
        /// </summary>
        public const float FRIENDLY_VHF_BEAMWIDTH_DEG = 60.0f;

        /// <summary>
        /// Half-power beamwidth half-angle (±30° from central azimuth 90°: 60° to 120°).
        /// </summary>
        public const float FRIENDLY_VHF_HALF_BEAMWIDTH_DEG = 30.0f;

        /// <summary>
        /// VHF / UHF spectrum upper frequency bound in GHz (500 MHz).
        /// </summary>
        public const float VHF_UHF_THRESHOLD_GHZ = 0.5f;

        /// <summary>
        /// Borderline 915 MHz ELRS centre frequency in GHz.
        /// </summary>
        public const float BAND_915_MHZ_GHZ = 0.915f;

        /// <summary>
        /// Tolerance for floating-point comparison of 915 MHz band.
        /// </summary>
        public const float BAND_TOLERANCE_GHZ = 0.05f;

        /// <summary>
        /// Computes shortest angular distance in degrees between two azimuth bearings [0, 360).
        /// Handles modular arithmetic and wrap-around seamlessly.
        /// </summary>
        public static float ComputeAngularDistance(float bearingA, float bearingB)
        {
            // Normalize to [0, 360)
            float normA = ((bearingA % 360.0f) + 360.0f) % 360.0f;
            float normB = ((bearingB % 360.0f) + 360.0f) % 360.0f;

            float diff = Math.Abs(normA - normB);
            if (diff > 180.0f)
            {
                diff = 360.0f - diff;
            }
            return diff;
        }

        /// <summary>
        /// Evaluates whether the active EW Jammer causes friendly VHF radio blackout risk.
        /// 
        /// Deconfliction rules (docs/PRD.md §4 MUST, board/contracts/telemetry.md, SCHEMA.md Entity 3):
        /// - Friendly VHF antenna at bearing 90° (East), elevation 0°, half-power beamwidth 60° (±30°).
        /// - friendlyBlackoutRisk = true when:
        ///     friendlyVhfActive == true
        ///     AND ewJammerState.engaged == true
        ///     AND (ewJammerState.bandGHz < 0.5f OR abs(bandGHz - 0.915f) <= 0.05f)
        ///     AND angularDistance(jammer.bearingDeg, 90°) < 30°
        /// - For 5.825 GHz and 2.4 GHz: always false (out-of-band microwave links).
        /// - For 915 MHz (ELRS borderline) or VHF/UHF: true if aimed within friendly antenna cone (< 30°).
        /// </summary>
        /// <param name="jammer">Current EWJammerState snapshot.</param>
        /// <param name="friendlyVhfActive">True if friendly VHF radios are active in the simulation zone.</param>
        /// <returns>True if friendly blackout risk exists (ROE violation), false otherwise.</returns>
        public static bool ComputeBlackoutRisk(EWJammerState jammer, bool friendlyVhfActive = true)
        {
            if (!friendlyVhfActive)
                return false;

            if (jammer == null || !jammer.engaged)
                return false;

            return ComputeBlackoutRisk(
                engaged: jammer.engaged,
                bearingDeg: jammer.bearingDeg,
                bandGHz: jammer.bandGHz,
                friendlyVhfActive: friendlyVhfActive
            );
        }

        /// <summary>
        /// Primitive overload for flexible cross-track invocation without object allocation.
        /// </summary>
        public static bool ComputeBlackoutRisk(bool engaged, float bearingDeg, float bandGHz, bool friendlyVhfActive = true)
        {
            if (!friendlyVhfActive || !engaged)
                return false;

            // Frequency check:
            // VHF/UHF (< 0.5 GHz) or borderline 915 MHz (0.915 GHz ± 0.05 GHz)
            bool isVhfUhf = bandGHz < VHF_UHF_THRESHOLD_GHZ;
            bool is915Mhz = Math.Abs(bandGHz - BAND_915_MHZ_GHZ) <= BAND_TOLERANCE_GHZ;

            if (!isVhfUhf && !is915Mhz)
                return false; // 5.825 GHz and 2.4 GHz are out-of-band -> safe

            // Azimuth check: within ±30° of 90° friendly VHF antenna
            float dist = ComputeAngularDistance(bearingDeg, FRIENDLY_VHF_BEARING_DEG);
            return dist < FRIENDLY_VHF_HALF_BEAMWIDTH_DEG;
        }
    }
}
