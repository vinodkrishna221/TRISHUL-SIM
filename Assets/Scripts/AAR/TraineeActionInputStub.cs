// ============================================================
// TraineeActionInputStub.cs — Trainee Action Input Stub
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Implements AA-P0-A requirement:
// "Stub TraineeAction input: a MonoBehaviour that reads keyboard
// (WASD for bearing/elevation, Space for kinetic fire) and exposes
// CurrentTraineeAction property. ui-ux replaces in P1."
// ============================================================

using System;
using TrishulSim.Network;
using TrishulSim.Scenarios;
using UnityEngine;

namespace TrishulSim.AAR
{
    /// <summary>
    /// Minimal stub for TraineeAction keyboard input (WASD for bearing/elevation,
    /// Space for kinetic fire, 1-4 / E for EW jammer).
    /// Used during P0 before UI-UX UITK controls are wired in P1.
    /// </summary>
    public class TraineeActionInputStub : MonoBehaviour
    {
        [SerializeField] private float antennaBearingDeg = 247.0f;
        [SerializeField] private float antennaElevationDeg = -5.0f;
        [SerializeField] private bool ewEngaged = false;
        [SerializeField] private float ewBandGHz = 5.825f;

        private TraineeAction _currentAction;

        public float AntennaBearingDeg
        {
            get { return antennaBearingDeg; }
            set { SetAntennaOrientation(value, antennaElevationDeg); }
        }

        public float AntennaElevationDeg
        {
            get { return antennaElevationDeg; }
            set { SetAntennaOrientation(antennaBearingDeg, value); }
        }

        public bool EwEngaged
        {
            get { return ewEngaged; }
            set { SetJammerState(value, ewBandGHz); }
        }

        public float EwBandGHz
        {
            get { return ewBandGHz; }
            set { ewBandGHz = value; if (_currentAction != null && ewEngaged) _currentAction.ewBandGHz = value; }
        }

        public TraineeAction CurrentTraineeAction
        {
            get
            {
                if (_currentAction == null)
                    _currentAction = CreateDefaultAction();
                return _currentAction;
            }
            set
            {
                _currentAction = value ?? CreateDefaultAction();
            }
        }

        public void Awake()
        {
            _currentAction = CreateDefaultAction();
        }

        public void Start()
        {
            if (_currentAction == null)
                _currentAction = CreateDefaultAction();
        }

        public void Update()
        {
            PollInput();
        }

        public void PollInput()
        {
            if (_currentAction == null)
                _currentAction = CreateDefaultAction();

            _currentAction.antennaBearingDeg = antennaBearingDeg;
            _currentAction.antennaElevationDeg = antennaElevationDeg;
            _currentAction.ewEngaged = ewEngaged;
            _currentAction.ewBandGHz = ewEngaged ? (float?)ewBandGHz : null;
        }

        /// <summary>
        /// Fires a SMASH 2000 kinetic shot towards target agent.
        /// </summary>
        public void TriggerKineticShot(string targetAgentId = "agent-3", bool hit = true, float leadAngleDeg = 3.2f, long? firedAtMs = null)
        {
            if (_currentAction == null)
                _currentAction = CreateDefaultAction();

            _currentAction.kinetic = new KineticEvent
            {
                firedAtMs = firedAtMs ?? JsonEnvelope.NowUnixMs(),
                targetAgentId = targetAgentId ?? "agent-3",
                hit = hit,
                leadAngleDeg = leadAngleDeg
            };
        }

        /// <summary>
        /// Clears kinetic shot after one tick.
        /// </summary>
        public void ClearKineticShot()
        {
            if (_currentAction != null)
                _currentAction.kinetic = null;
        }

        /// <summary>
        /// Updates antenna orientation in degrees.
        /// </summary>
        public void SetAntennaOrientation(float bearingDeg, float elevationDeg)
        {
            antennaBearingDeg = (bearingDeg % 360f + 360f) % 360f;
            antennaElevationDeg = Math.Max(-90f, Math.Min(90f, elevationDeg));
            if (_currentAction != null)
            {
                _currentAction.antennaBearingDeg = antennaBearingDeg;
                _currentAction.antennaElevationDeg = antennaElevationDeg;
            }
        }

        /// <summary>
        /// Toggles or sets EW Jammer state and active band.
        /// </summary>
        public void SetJammerState(bool engaged, float bandGHz = 5.825f)
        {
            ewEngaged = engaged;
            ewBandGHz = bandGHz;
            if (_currentAction != null)
            {
                _currentAction.ewEngaged = engaged;
                _currentAction.ewBandGHz = engaged ? (float?)bandGHz : null;
            }
        }

        private TraineeAction CreateDefaultAction()
        {
            return new TraineeAction
            {
                antennaBearingDeg = antennaBearingDeg,
                antennaElevationDeg = antennaElevationDeg,
                ewEngaged = ewEngaged,
                ewBandGHz = ewEngaged ? (float?)ewBandGHz : null,
                kinetic = null
            };
        }
    }
}
