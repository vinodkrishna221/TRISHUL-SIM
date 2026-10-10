// ============================================================
// SmashFireControl.cs — SMASH 2000 Fire Control System & Kinetic Intercept
// TRISHUL-SIM · SA-P1-D · swarm-ai track (Uday)
//
// Computes lead-angle reticle for 7.62mm kinetic fire engagement
// and evaluates KineticEvent hit / miss logic (telemetry.md / SCHEMA.md).
// ============================================================

using System;
using UnityEngine;

namespace TrishulSim.Sensors
{
    /// <summary>
    /// Kinetic fire event recorded on trigger pull (board/contracts/telemetry.md / SCHEMA.md Entity 3).
    /// </summary>
    [Serializable]
    public class KineticEvent
    {
        public long firedAtMs;
        public string targetAgentId;
        public bool hit;
        public float leadAngleDeg;

        public KineticEvent()
        {
            firedAtMs = 0;
            targetAgentId = "";
            hit = false;
            leadAngleDeg = 0f;
        }

        public KineticEvent(long firedAtMs, string targetAgentId, bool hit, float leadAngleDeg)
        {
            this.firedAtMs = firedAtMs;
            this.targetAgentId = targetAgentId;
            this.hit = hit;
            this.leadAngleDeg = leadAngleDeg;
        }

        public override string ToString()
        {
            return string.Format("KineticEvent{{hit:{0}, leadAngleDeg:{1:F1}, targetAgentId:\"{2}\", firedAtMs:{3}}}",
                hit.ToString().ToLower(), leadAngleDeg, targetAgentId, firedAtMs);
        }
    }

    /// <summary>
    /// Trainee input state captured at each 100 ms tick (telemetry.md / SCHEMA.md Entity 3).
    /// </summary>
    [Serializable]
    public class TraineeAction
    {
        public float antennaBearingDeg;
        public float antennaElevationDeg;
        public bool ewEngaged;
        public float? ewBandGHz;
        public KineticEvent kinetic;
        public bool kineticFireTriggered;

        public TraineeAction()
        {
            antennaBearingDeg = 0f;
            antennaElevationDeg = 0f;
            ewEngaged = false;
            ewBandGHz = null;
            kinetic = null;
            kineticFireTriggered = false;
        }

        public TraineeAction(float bearing, float elevation, bool ewActive = false, float? band = null)
        {
            antennaBearingDeg = bearing;
            antennaElevationDeg = elevation;
            ewEngaged = ewActive;
            ewBandGHz = band;
            kinetic = null;
            kineticFireTriggered = false;
        }
    }

    /// <summary>
    /// SMASH 2000 Electro-Optic Fire Control Computer.
    /// Provides lead-angle reticle computation, ballistic intercept prediction,
    /// and kinetic engagement hit/miss resolution for 7.62mm NATO munitions.
    /// </summary>
    public static class SmashFireControl
    {
        /// <summary>
        /// 7.62mm NATO standard muzzle velocity in metres per second.
        /// Doctrinal constant from docs/PRD.md §4 MUST and missions/uday-mission.md.
        /// </summary>
        public const float BULLET_SPEED_MS = 900.0f;

        /// <summary>
        /// Maximum lead angle tolerance (half-angle cone in degrees) for a confirmed kinetic hit.
        /// </summary>
        public const float ACCURACY_CONE_HALF_ANGLE_DEG = 5.0f;

        /// <summary>
        /// Converts azimuth bearing (0-360° clockwise from North) and elevation (-90 to +90°)
        /// into a normalized 3D direction vector in Unity coordinate space.
        /// North = +Z, East = +X, Up = +Y.
        /// </summary>
        public static Vector3 BearingElevationToDirection(float bearingDeg, float elevationDeg)
        {
            float bearingRad = bearingDeg * ((float)Math.PI / 180.0f);
            float elevationRad = elevationDeg * ((float)Math.PI / 180.0f);

            float cosElev = (float)Math.Cos(elevationRad);
            float sinElev = (float)Math.Sin(elevationRad);
            float sinBearing = (float)Math.Sin(bearingRad);
            float cosBearing = (float)Math.Cos(bearingRad);

            return new Vector3(cosElev * sinBearing, sinElev, cosElev * cosBearing);
        }

        /// <summary>
        /// Converts a 3D direction vector into azimuth bearing [0, 360) and elevation [-90, +90] in degrees.
        /// </summary>
        public static void DirectionToBearingElevation(Vector3 direction, out float bearingDeg, out float elevationDeg)
        {
            float mag = direction.magnitude;
            if (mag < 1e-6f)
            {
                bearingDeg = 0.0f;
                elevationDeg = 0.0f;
                return;
            }

            float sinElev = direction.y / mag;
            if (sinElev > 1.0f) sinElev = 1.0f;
            if (sinElev < -1.0f) sinElev = -1.0f;
            elevationDeg = (float)(Math.Asin(sinElev) * (180.0 / Math.PI));

            float horizMag = (float)Math.Sqrt(direction.x * direction.x + direction.z * direction.z);
            if (horizMag < 1e-6f)
            {
                bearingDeg = 0.0f;
            }
            else
            {
                float bearing = (float)(Math.Atan2(direction.x, direction.z) * (180.0 / Math.PI));
                bearingDeg = ((bearing % 360.0f) + 360.0f) % 360.0f;
            }
        }

        /// <summary>
        /// Computes 3D angular separation between two direction vectors in degrees.
        /// </summary>
        public static float ComputeAngleDeg(Vector3 dirA, Vector3 dirB)
        {
            float magA = dirA.magnitude;
            float magB = dirB.magnitude;
            if (magA < 1e-6f || magB < 1e-6f)
                return 0.0f;

            float dot = (dirA.x * dirB.x + dirA.y * dirB.y + dirA.z * dirB.z) / (magA * magB);
            if (dot > 1.0f) dot = 1.0f;
            if (dot < -1.0f) dot = -1.0f;

            return (float)(Math.Acos(dot) * (180.0 / Math.PI));
        }

        /// <summary>
        /// Computes time of flight, lead vector, and projected intercept aim point for a moving target.
        /// Formula:
        ///   distanceToTarget = ||targetPos - traineePos||
        ///   timeToTarget = distanceToTarget / bulletSpeedMs
        ///   leadVector = target.velocityMs * timeToTarget
        ///   aimPoint = targetPos + leadVector
        /// </summary>
        public static Vector3 ComputeLeadAimPoint(SwarmAgentState target, Vector3 traineePos, float bulletSpeedMs = BULLET_SPEED_MS)
        {
            if (target == null)
                return traineePos;

            float dx = target.positionWorld.x - traineePos.x;
            float dy = target.positionWorld.y - traineePos.y;
            float dz = target.positionWorld.z - traineePos.z;
            float distanceToTarget = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

            float speed = bulletSpeedMs > 0f ? bulletSpeedMs : BULLET_SPEED_MS;
            float timeToTarget = distanceToTarget / speed;

            Vector3 leadVector = new Vector3(
                target.velocityMs.x * timeToTarget,
                target.velocityMs.y * timeToTarget,
                target.velocityMs.z * timeToTarget
            );

            return new Vector3(
                target.positionWorld.x + leadVector.x,
                target.positionWorld.y + leadVector.y,
                target.positionWorld.z + leadVector.z
            );
        }

        /// <summary>
        /// Computes required bearing and elevation angles for the SMASH 2000 HUD reticle.
        /// Allows the cockpit UI to render the dynamic lead-angle reticle on screen.
        /// </summary>
        public static void ComputeLeadReticleAngles(
            SwarmAgentState target,
            Vector3 traineePos,
            out float requiredBearingDeg,
            out float requiredElevationDeg,
            float bulletSpeedMs = BULLET_SPEED_MS)
        {
            Vector3 aimPoint = ComputeLeadAimPoint(target, traineePos, bulletSpeedMs);
            Vector3 leadDir = new Vector3(
                aimPoint.x - traineePos.x,
                aimPoint.y - traineePos.y,
                aimPoint.z - traineePos.z
            );
            DirectionToBearingElevation(leadDir, out requiredBearingDeg, out requiredElevationDeg);
        }

        /// <summary>
        /// Computes KineticEvent on trigger pull for a designated target agent.
        /// 
        /// Logic:
        /// 1. bulletSpeedMs = 900 m/s (7.62mm NATO MV)
        /// 2. timeToTarget = distanceToTarget / bulletSpeedMs
        /// 3. leadVector = agent.velocityMs * timeToTarget
        /// 4. aimPoint = agent.positionWorld + leadVector
        /// 5. leadAngleDeg = angle between (trainee aim direction) and (aimPoint - traineePos)
        /// 6. hit = leadAngleDeg < 5° AND agent.isNeutralised == false
        /// </summary>
        public static KineticEvent ComputeKineticEvent(
            TraineeAction action,
            SwarmAgentState target,
            long t,
            Vector3 traineePos,
            float accuracyConeHalfAngle = ACCURACY_CONE_HALF_ANGLE_DEG,
            float bulletSpeedMs = BULLET_SPEED_MS)
        {
            if (action == null || target == null)
            {
                string targetId = target != null ? target.agentId : "none";
                return new KineticEvent(t, targetId, false, 999.0f);
            }

            return ComputeKineticEvent(
                aimBearingDeg: action.antennaBearingDeg,
                aimElevationDeg: action.antennaElevationDeg,
                target: target,
                t: t,
                traineePos: traineePos,
                accuracyConeHalfAngle: accuracyConeHalfAngle,
                bulletSpeedMs: bulletSpeedMs
            );
        }

        /// <summary>
        /// Overload taking raw bearing and elevation without TraineeAction allocation.
        /// </summary>
        public static KineticEvent ComputeKineticEvent(
            float aimBearingDeg,
            float aimElevationDeg,
            SwarmAgentState target,
            long t,
            Vector3 traineePos,
            float accuracyConeHalfAngle = ACCURACY_CONE_HALF_ANGLE_DEG,
            float bulletSpeedMs = BULLET_SPEED_MS)
        {
            if (target == null)
            {
                return new KineticEvent(t, "none", false, 999.0f);
            }

            Vector3 traineeAimDir = BearingElevationToDirection(aimBearingDeg, aimElevationDeg);
            Vector3 aimPoint = ComputeLeadAimPoint(target, traineePos, bulletSpeedMs);

            Vector3 requiredLeadDir = new Vector3(
                aimPoint.x - traineePos.x,
                aimPoint.y - traineePos.y,
                aimPoint.z - traineePos.z
            );

            float leadAngleDeg = ComputeAngleDeg(traineeAimDir, requiredLeadDir);
            bool hit = (leadAngleDeg < accuracyConeHalfAngle) && !target.isNeutralised;

            return new KineticEvent(t, target.agentId, hit, leadAngleDeg);
        }

        /// <summary>
        /// Finds the active (non-neutralised) target locked within the sentry's field of view.
        /// Prefers targets nearest to current boresight axis within lock cone.
        /// </summary>
        public static SwarmAgentState FindLockedTarget(
            SwarmAgentState[] agents,
            float aimBearingDeg,
            float aimElevationDeg,
            Vector3 traineePos,
            float maxLockAngleDeg = 30.0f)
        {
            if (agents == null || agents.Length == 0)
                return null;

            Vector3 aimDir = BearingElevationToDirection(aimBearingDeg, aimElevationDeg);
            SwarmAgentState bestTarget = null;
            float minAngle = maxLockAngleDeg;

            for (int i = 0; i < agents.Length; i++)
            {
                SwarmAgentState candidate = agents[i];
                if (candidate == null || candidate.isNeutralised)
                    continue;

                Vector3 targetDir = new Vector3(
                    candidate.positionWorld.x - traineePos.x,
                    candidate.positionWorld.y - traineePos.y,
                    candidate.positionWorld.z - traineePos.z
                );

                float angle = ComputeAngleDeg(aimDir, targetDir);
                if (angle < minAngle)
                {
                    minAngle = angle;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }
    }
}
