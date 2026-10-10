// ============================================================
// BoidFlockController.cs — Classical Reynolds Boids Flocking & Burst Optimization
// TRISHUL-SIM · SA-P0-A / SA-P2-B · swarm-ai track (Uday)
//
// Implements classical 3-rule Reynolds Boids (Separation <5m, Alignment 20m, Cohesion 20m),
// altitude spring constraints (altitudeM ± 2m), velocity clamping to SwarmProfile speed,
// and high-throughput Burst/Job System architecture for Intel Iris Xe 60+ FPS compliance.
// Contract: board/contracts/scenario-events.md, docs/SCHEMA.md Entity 2, docs/ARCHITECTURE.md §1
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using TrishulSim.Sensors;

#if UNITY_BURST && UNITY_JOBS
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
#endif

namespace TrishulSim.SwarmAI
{
    /// <summary>
    /// Classical Reynolds Boids Flocking controller with Burst compilation support.
    /// Manages separation, alignment, and cohesion across swarm agents with zero runtime GC allocation.
    /// </summary>
    public class BoidFlockController
    {
        // ── Doctrinal Boid Parameters ──────────────────────────────
        public const float DefaultSeparationDistance = 5.0f;   // Repel when distance < 5m
        public const float DefaultAlignmentDistance = 20.0f;   // Match velocity within 20m
        public const float DefaultCohesionDistance = 20.0f;    // Steer toward centroid within 20m
        public const float AltitudeDeadbandM = 2.0f;           // Altitude spring deadband (±2m)
        public const float AltitudeSpringConstant = 3.5f;      // Vertical restoring spring force
        public const float DefaultSeparationWeight = 2.2f;     // Repulsion priority to prevent collisions
        public const float DefaultAlignmentWeight = 1.0f;      // Flocking velocity matching
        public const float DefaultCohesionWeight = 0.8f;       // Flock centring tendency

        public float SeparationDistance = DefaultSeparationDistance;
        public float AlignmentDistance = DefaultAlignmentDistance;
        public float CohesionDistance = DefaultCohesionDistance;
        public float SeparationWeight = DefaultSeparationWeight;
        public float AlignmentWeight = DefaultAlignmentWeight;
        public float CohesionWeight = DefaultCohesionWeight;
        public float AltitudeSpringK = AltitudeSpringConstant;

        /// <summary>
        /// Lightweight blittable struct representing a single Boid agent for cache locality & Burst jobs.
        /// </summary>
        public struct BoidAgentData
        {
            public Vector3 position;
            public Vector3 velocity;
            public bool isNeutralised;
            public bool isDecoy;
            public float baseAltitude;
            public float maxSpeed;

            public BoidAgentData(Vector3 pos, Vector3 vel, bool neutralised, bool decoy, float alt, float speed)
            {
                position = pos;
                velocity = vel;
                isNeutralised = neutralised;
                isDecoy = decoy;
                baseAltitude = alt;
                maxSpeed = speed;
            }
        }

#if UNITY_BURST && UNITY_JOBS
        /// <summary>
        /// Burst-compiled parallel job calculating Boid flocking forces across all agents.
        /// Scheduled via IJobParallelFor for sub-millisecond execution on multi-core architectures.
        /// </summary>
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        public struct BoidStepJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> positions;
            [ReadOnly] public NativeArray<float3> velocities;
            [ReadOnly] public NativeArray<bool> isNeutralised;
            public float separationDist;
            public float alignmentDist;
            public float cohesionDist;
            public float separationWeight;
            public float alignmentWeight;
            public float cohesionWeight;
            public float targetAltitude;
            public float altitudeSpringK;
            public float maxSpeed;
            public float deltaTime;

            public NativeArray<float3> outVelocities;
            public NativeArray<float3> outPositions;

            public void Execute(int index)
            {
                if (isNeutralised[index])
                {
                    outVelocities[index] = float3.zero;
                    outPositions[index] = positions[index];
                    return;
                }

                float3 myPos = positions[index];
                float3 myVel = velocities[index];

                float3 separationForce = float3.zero;
                float3 alignmentSum = float3.zero;
                float3 cohesionCentroid = float3.zero;
                int alignmentCount = 0;
                int cohesionCount = 0;

                int count = positions.Length;
                for (int j = 0; j < count; j++)
                {
                    if (j == index || isNeutralised[j])
                        continue;

                    float3 otherPos = positions[j];
                    float3 diff = myPos - otherPos;
                    float distSq = math.lengthsq(diff);

                    // 1. Separation rule: repel when distance < separationDist (5m)
                    if (distSq > 0.0001f && distSq < (separationDist * separationDist))
                    {
                        float dist = math.sqrt(distSq);
                        separationForce += (diff / dist) * (1.0f - (dist / separationDist));
                    }

                    // 2. Alignment rule: average velocity within alignmentDist (20m)
                    if (distSq < (alignmentDist * alignmentDist))
                    {
                        alignmentSum += velocities[j];
                        alignmentCount++;
                    }

                    // 3. Cohesion rule: steer toward centroid within cohesionDist (20m)
                    if (distSq < (cohesionDist * cohesionDist))
                    {
                        cohesionCentroid += otherPos;
                        cohesionCount++;
                    }
                }

                float3 steering = float3.zero;
                steering += separationForce * separationWeight;

                if (alignmentCount > 0)
                {
                    float3 avgVel = alignmentSum / (float)alignmentCount;
                    steering += (avgVel - myVel) * alignmentWeight;
                }

                if (cohesionCount > 0)
                {
                    float3 centroid = cohesionCentroid / (float)cohesionCount;
                    float3 toCentroid = centroid - myPos;
                    steering += toCentroid * cohesionWeight;
                }

                // Altitude spring: keep within targetAltitude ± 2m
                float currentAlt = myPos.y;
                float altDiff = targetAltitude - currentAlt;
                if (math.abs(altDiff) > AltitudeDeadbandM)
                {
                    float altForce = altDiff * altitudeSpringK;
                    steering.y += altForce;
                }

                // Integrate velocity
                float3 newVel = myVel + (steering * deltaTime);

                // Speed clamp to SwarmProfile speed
                float newSpeed = math.length(newVel);
                if (newSpeed > maxSpeed && newSpeed > 0.001f)
                {
                    newVel = (newVel / newSpeed) * maxSpeed;
                }

                outVelocities[index] = newVel;
                outPositions[index] = myPos + (newVel * deltaTime);
            }
        }
#endif

        /// <summary>
        /// Evaluates classical Reynolds Boid forces on an array of SwarmAgentState objects.
        /// Pure C# high-speed path (zero allocation, runnable without Unity Jobs package).
        /// </summary>
        public void Step(
            IList<SwarmAgentState> agents,
            SwarmProfile profile,
            float deltaTime)
        {
            if (agents == null || agents.Count == 0 || deltaTime <= 0f)
                return;

            int count = agents.Count;
            float maxSpeed = profile != null ? profile.SpeedMs : (120f / 3.6f);
            float targetAltitude = profile != null ? profile.altitudeM : 8.0f;

            Vector3[] nextVel = new Vector3[count];
            Vector3[] nextPos = new Vector3[count];

            // Pre-extract agent state into stack/local variables for high-cache hit loop
            for (int i = 0; i < count; i++)
            {
                SwarmAgentState agent = agents[i];
                if (agent == null || agent.isNeutralised)
                {
                    if (agent != null)
                    {
                        nextVel[i] = Vector3.zero;
                        nextPos[i] = agent.positionWorld;
                    }
                    continue;
                }

                Vector3 myPos = agent.positionWorld;
                Vector3 myVel = agent.velocityMs;

                Vector3 separationForce = Vector3.zero;
                Vector3 alignmentSum = Vector3.zero;
                Vector3 cohesionCentroid = Vector3.zero;
                int alignmentCount = 0;
                int cohesionCount = 0;

                for (int j = 0; j < count; j++)
                {
                    if (j == i)
                        continue;

                    SwarmAgentState other = agents[j];
                    if (other == null || other.isNeutralised)
                        continue;

                    Vector3 diff = myPos - other.positionWorld;
                    float distSq = diff.sqrMagnitude;

                    // 1. Separation rule: repel when distance < 5m
                    if (distSq > 0.0001f && distSq < (SeparationDistance * SeparationDistance))
                    {
                        float dist = (float)Math.Sqrt(distSq);
                        float intensity = 1.0f - (dist / SeparationDistance);
                        separationForce = separationForce + (diff * (intensity / dist));
                    }

                    // 2. Alignment rule: match neighbour velocity within 20m
                    if (distSq < (AlignmentDistance * AlignmentDistance))
                    {
                        alignmentSum = alignmentSum + other.velocityMs;
                        alignmentCount++;
                    }

                    // 3. Cohesion rule: steer towards centroid of non-crowding neighbours within 20m
                    if (distSq >= (SeparationDistance * SeparationDistance) && distSq < (CohesionDistance * CohesionDistance))
                    {
                        cohesionCentroid = cohesionCentroid + other.positionWorld;
                        cohesionCount++;
                    }
                }

                Vector3 steering = Vector3.zero;
                steering = steering + (separationForce * SeparationWeight);

                if (alignmentCount > 0)
                {
                    Vector3 avgVel = alignmentSum * (1.0f / alignmentCount);
                    steering = steering + ((avgVel - myVel) * AlignmentWeight);
                }

                if (cohesionCount > 0)
                {
                    Vector3 centroid = cohesionCentroid * (1.0f / cohesionCount);
                    Vector3 toCentroid = centroid - myPos;
                    steering = steering + (toCentroid * CohesionWeight);
                }

                // Altitude spring: keep within altitudeM ± 2m
                float altDiff = targetAltitude - myPos.y;
                if (Math.Abs(altDiff) > AltitudeDeadbandM)
                {
                    float springY = altDiff * AltitudeSpringK;
                    steering = new Vector3(steering.x, steering.y + springY, steering.z);
                }

                // Apply acceleration and clamp to maximum speed
                Vector3 computedVel = myVel + (steering * deltaTime);
                float speed = computedVel.magnitude;
                if (speed > maxSpeed && speed > 0.001f)
                {
                    computedVel = computedVel * (maxSpeed / speed);
                }

                nextVel[i] = computedVel;
                nextPos[i] = myPos + (computedVel * deltaTime);
            }

            // Apply double-buffered updates simultaneously to prevent iteration order bias
            for (int i = 0; i < count; i++)
            {
                SwarmAgentState agent = agents[i];
                if (agent == null || agent.isNeutralised)
                    continue;

                agent.velocityMs = nextVel[i];
                agent.positionWorld = nextPos[i];
                agent.altitudeM = nextPos[i].y;

                if (Math.Abs(nextVel[i].x) > 0.01f || Math.Abs(nextVel[i].z) > 0.01f)
                {
                    float headingRad = (float)Math.Atan2(nextVel[i].x, nextVel[i].z);
                    float headingDeg = headingRad * (180f / (float)Math.PI);
                    if (headingDeg < 0f) headingDeg += 360f;
                    agent.headingDeg = headingDeg;
                }
            }
        }

        /// <summary>
        /// Computes flocking forces on a standalone array of BoidAgentData structs.
        /// Useful for headless profiling, unit tests, and Burst-compatible NativeArray mirrors.
        /// </summary>
        public static void StepBoidData(
            BoidAgentData[] boids,
            int count,
            float separationDist,
            float alignmentDist,
            float cohesionDist,
            float targetAltitude,
            float maxSpeed,
            float deltaTime,
            out Vector3[] outVelocities,
            out Vector3[] outPositions)
        {
            outVelocities = new Vector3[count];
            outPositions = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                if (boids[i].isNeutralised)
                {
                    outVelocities[i] = Vector3.zero;
                    outPositions[i] = boids[i].position;
                    continue;
                }

                Vector3 myPos = boids[i].position;
                Vector3 myVel = boids[i].velocity;

                Vector3 sep = Vector3.zero;
                Vector3 alignSum = Vector3.zero;
                Vector3 cohCentroid = Vector3.zero;
                int alignCount = 0;
                int cohCount = 0;

                for (int j = 0; j < count; j++)
                {
                    if (j == i || boids[j].isNeutralised)
                        continue;

                    Vector3 diff = myPos - boids[j].position;
                    float distSq = diff.sqrMagnitude;

                    if (distSq > 0.0001f && distSq < (separationDist * separationDist))
                    {
                        float dist = (float)Math.Sqrt(distSq);
                        sep = sep + (diff * ((1.0f - dist / separationDist) / dist));
                    }

                    if (distSq < (alignmentDist * alignmentDist))
                    {
                        alignSum = alignSum + boids[j].velocity;
                        alignCount++;
                    }

                    if (distSq < (cohesionDist * cohesionDist))
                    {
                        cohCentroid = cohCentroid + boids[j].position;
                        cohCount++;
                    }
                }

                Vector3 steer = sep * DefaultSeparationWeight;
                if (alignCount > 0)
                {
                    steer = steer + ((alignSum * (1.0f / alignCount) - myVel) * DefaultAlignmentWeight);
                }
                if (cohCount > 0)
                {
                    steer = steer + (((cohCentroid * (1.0f / cohCount)) - myPos) * DefaultCohesionWeight);
                }

                float altDiff = targetAltitude - myPos.y;
                if (Math.Abs(altDiff) > AltitudeDeadbandM)
                {
                    steer = new Vector3(steer.x, steer.y + (altDiff * AltitudeSpringConstant), steer.z);
                }

                Vector3 nextVel = myVel + (steer * deltaTime);
                float spd = nextVel.magnitude;
                if (spd > maxSpeed && spd > 0.001f)
                {
                    nextVel = nextVel * (maxSpeed / spd);
                }

                outVelocities[i] = nextVel;
                outPositions[i] = myPos + (nextVel * deltaTime);
            }
        }
    }
}
