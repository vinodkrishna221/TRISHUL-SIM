// ============================================================
// TacticTree.cs — Hierarchical Behavior Tree Base for Swarm AI
// TRISHUL-SIM · SA-P1-B-i / SA-P2-A · swarm-ai track (Venu / Uday)
//
// Base class for deterministic swarm tactics.
// Contract: board/contracts/scenario-events.md, docs/SCHEMA.md Entity 1 & 2
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using TrishulSim.Sensors;

namespace TrishulSim.SwarmAI.HBT
{
    /// <summary>
    /// Tactical state of the swarm behavior tree (SA-P1-B-i Step 2:
    /// APPROACH -> SPLIT / SPLIT_TRIGGER -> PINCER -> DIVE / DIVE_ATTACK -> NEUTRALISED).
    /// </summary>
    public enum TacticState
    {
        APPROACH = 0,
        SPLIT = 1,
        SPLIT_TRIGGER = SPLIT,
        PINCER = 2,
        DIVE_ATTACK = 3,
        DIVE = DIVE_ATTACK,
        EXCLUSION_BREACH = 4,
        NEUTRALISED = 5
    }

    /// <summary>
    /// Base class for all Swarm AI Hierarchical Behavior Trees (HBT).
    /// Enforces deterministic behavior, event callbacks, and exclusion zone rules.
    /// </summary>
    public abstract class TacticTree
    {
        public abstract string Name { get; }

        public TacticState CurrentState { get; protected set; }

        public Vector3 TargetOrigin { get; protected set; }

        /// <summary>
        /// SA-P1-B-i Step 3: Defended position target origin (defaults to Vector3.zero).
        /// </summary>
        public Vector3 TargetPosition
        {
            get { return TargetOrigin; }
            protected set { TargetOrigin = value; }
        }

        public int Seed { get; protected set; }

        public SwarmProfile Profile { get; protected set; }

        public List<SwarmAgentState> Agents { get; protected set; }

        public bool ExclusionBreached { get; protected set; }

        public long? ExclusionBreachedAtMs { get; protected set; }

        public float ExclusionZoneRadius { get; set; }

        public event Action<TacticState> OnStateChanged;

        protected TacticTree()
        {
            CurrentState = TacticState.APPROACH;
            TargetOrigin = Vector3.zero;
            ExclusionBreached = false;
            ExclusionBreachedAtMs = null;
            ExclusionZoneRadius = 20.0f; // Standard 20m inner exclusion zone
        }

        /// <summary>
        /// Initialises the tactic tree with scenario parameters and agent references.
        /// </summary>
        public virtual void Initialize(SwarmProfile profile, int seed, List<SwarmAgentState> agents, Vector3 targetOrigin)
        {
            Profile = profile;
            Seed = seed;
            Agents = agents ?? new List<SwarmAgentState>();
            TargetOrigin = targetOrigin;
            CurrentState = TacticState.APPROACH;
            ExclusionBreached = false;
            ExclusionBreachedAtMs = null;
        }

        /// <summary>
        /// Advances the behavior tree by deltaTime seconds.
        /// Evaluates triggers, applies kinematic updates, and handles EW responses.
        /// </summary>
        public abstract void Tick(float deltaTime, long currentScenarioTimeMs, EWJammerState ewJammer, Vector3 traineePos);

        /// <summary>
        /// Transitions to a new tactical state and fires notification events.
        /// </summary>
        protected virtual void SetState(TacticState newState)
        {
            if (CurrentState != newState)
            {
                CurrentState = newState;
                if (OnStateChanged != null)
                {
                    OnStateChanged(newState);
                }
            }
        }

        /// <summary>
        /// Evaluates whether any active attacker has breached the inner exclusion zone (20m radius).
        /// </summary>
        protected bool CheckExclusionZone(long currentScenarioTimeMs)
        {
            if (ExclusionBreached)
                return true;

            for (int i = 0; i < Agents.Count; i++)
            {
                SwarmAgentState agent = Agents[i];
                if (agent == null || agent.isDecoy || agent.isNeutralised)
                    continue;

                float distSq = (agent.positionWorld.x - TargetOrigin.x) * (agent.positionWorld.x - TargetOrigin.x)
                             + (agent.positionWorld.z - TargetOrigin.z) * (agent.positionWorld.z - TargetOrigin.z);

                if (distSq <= ExclusionZoneRadius * ExclusionZoneRadius)
                {
                    ExclusionBreached = true;
                    ExclusionBreachedAtMs = currentScenarioTimeMs;
                    SetState(TacticState.EXCLUSION_BREACH);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Utility to calculate distance of an agent to target origin in the horizontal plane (X-Z).
        /// </summary>
        protected float HorizontalDistanceToOrigin(Vector3 position)
        {
            float dx = position.x - TargetOrigin.x;
            float dz = position.z - TargetOrigin.z;
            return (float)Math.Sqrt((dx * dx) + (dz * dz));
        }

        /// <summary>
        /// Utility to compute heading angle in degrees (0° = North/+Z, 90° = East/+X, 180° = South/-Z, 270° = West/-X).
        /// </summary>
        public static float CalculateHeadingDeg(Vector3 velocity)
        {
            if (velocity.sqrMagnitude < 0.0001f)
                return 0f;

            float heading = (float)(Math.Atan2(velocity.x, velocity.z) * (180.0 / Math.PI));
            if (heading < 0f)
                heading += 360f;
            return heading;
        }
    }
}
