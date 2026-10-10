// ============================================================
// SwarmManager.cs — Swarm Simulation & Kinetic Engagement Manager
// TRISHUL-SIM · SA-P1-D · swarm-ai track (Uday)
//
// Manages swarm state lifecycle, processes SMASH 2000 kinetic fire,
// executes target neutralisation, and verifies ALL_NEUTRALISED terminal state.
// Contract: board/contracts/scenario-events.md, board/contracts/telemetry.md, docs/SCHEMA.md
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using TrishulSim.Sensors;
using TrishulSim.Mocks;

namespace TrishulSim.SwarmAI
{
    /// <summary>
    /// Manager singleton / coordinating engine for the Swarm AI simulation.
    /// Manages agents, produces SCENARIO_TICK swarmSnapshot, resolves kinetic engagements,
    /// and signals terminal condition ALL_NEUTRALISED.
    /// </summary>
    public class SwarmManager
    {
        private static SwarmManager _instance;
        public static SwarmManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SwarmManager();
                }
                return _instance;
            }
        }

        private List<SwarmAgentState> _agents;
        private bool _allNeutralised;
        private long? _allNeutralisedAtMs;

        public bool AllNeutralised
        {
            get { return _allNeutralised; }
        }

        public long? AllNeutralisedAtMs
        {
            get { return _allNeutralisedAtMs; }
        }

        public SwarmManager()
        {
            _agents = new List<SwarmAgentState>();
            _allNeutralised = false;
            _allNeutralisedAtMs = null;
        }

        /// <summary>
        /// Initialises swarm from an array of agent states (e.g. Punjab fixture).
        /// </summary>
        public void LoadAgents(SwarmAgentState[] agentArray)
        {
            _agents.Clear();
            _allNeutralised = false;
            _allNeutralisedAtMs = null;

            if (agentArray != null)
            {
                for (int i = 0; i < agentArray.Length; i++)
                {
                    if (agentArray[i] != null)
                    {
                        _agents.Add(agentArray[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Loads the canonical Punjab fixture for zero-blocker standalone execution.
        /// </summary>
        public void LoadPunjabFixture()
        {
            LoadAgents(SwarmMock.GetPunjabFixture());
        }

        /// <summary>
        /// Returns the current swarm snapshot for injection into SCENARIO_TICK.
        /// </summary>
        public SwarmAgentState[] GetSwarmSnapshot()
        {
            return _agents.ToArray();
        }

        /// <summary>
        /// Retrieves an agent by its unique identifier.
        /// </summary>
        public SwarmAgentState GetAgentById(string agentId)
        {
            for (int i = 0; i < _agents.Count; i++)
            {
                if (_agents[i] != null && string.Equals(_agents[i].agentId, agentId, StringComparison.OrdinalIgnoreCase))
                {
                    return _agents[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Processes trainee kinetic trigger pull (SA-P1-D core deliverable).
        /// Resolves lead-angle intercept, evaluates hit, updates target state on confirmed hit,
        /// and evaluates the ALL_NEUTRALISED terminal condition.
        /// </summary>
        public KineticEvent ProcessKineticFire(
            TraineeAction action,
            long t,
            Vector3 traineePos,
            string targetAgentId = null)
        {
            if (action == null)
                return null;

            SwarmAgentState target = null;
            if (!string.IsNullOrEmpty(targetAgentId))
            {
                target = GetAgentById(targetAgentId);
            }

            if (target == null)
            {
                target = SmashFireControl.FindLockedTarget(
                    agents: _agents.ToArray(),
                    aimBearingDeg: action.antennaBearingDeg,
                    aimElevationDeg: action.antennaElevationDeg,
                    traineePos: traineePos
                );
            }

            if (target == null)
            {
                KineticEvent emptyEvent = new KineticEvent(t, "none", false, 999.0f);
                action.kinetic = emptyEvent;
                return emptyEvent;
            }

            KineticEvent kineticEvent = SmashFireControl.ComputeKineticEvent(
                action: action,
                target: target,
                t: t,
                traineePos: traineePos
            );

            if (kineticEvent.hit)
            {
                // Target neutralisation state transition (telemetry.md / SCENARIO_END)
                target.isNeutralised = true;
                target.neutralisedAtMs = t;
                target.velocityMs = Vector3.zero;
                target.propellerRpm = 0;
                target.rfEmitting = false;

                // Check terminal condition: all attackers neutralised
                if (CheckAllAttackersNeutralised())
                {
                    _allNeutralised = true;
                    _allNeutralisedAtMs = t;
                }
            }

            action.kinetic = kineticEvent;
            return kineticEvent;
        }

        /// <summary>
        /// Evaluates whether all non-decoy attacker agents have been neutralised.
        /// Rule: SCENARIO_END reason = "ALL_NEUTRALISED" when all attackerCount agents have isNeutralised == true.
        /// </summary>
        public bool CheckAllAttackersNeutralised()
        {
            int attackerCount = 0;
            int neutralisedCount = 0;

            for (int i = 0; i < _agents.Count; i++)
            {
                SwarmAgentState agent = _agents[i];
                if (agent == null || agent.isDecoy)
                    continue;

                attackerCount++;
                if (agent.isNeutralised)
                {
                    neutralisedCount++;
                }
            }

            return attackerCount > 0 && neutralisedCount >= attackerCount;
        }

        /// <summary>
        /// Returns total count of remaining active (non-neutralised) attackers.
        /// </summary>
        public int GetRemainingAttackerCount()
        {
            int active = 0;
            for (int i = 0; i < _agents.Count; i++)
            {
                SwarmAgentState agent = _agents[i];
                if (agent != null && !agent.isDecoy && !agent.isNeutralised)
                {
                    active++;
                }
            }
            return active;
        }
    }
}
