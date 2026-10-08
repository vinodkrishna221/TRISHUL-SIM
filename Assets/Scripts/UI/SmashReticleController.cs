// ============================================================
// SmashReticleController.cs — SMASH 2000 Fire-Control Reticle Controller
// TRISHUL-SIM · UX-P1-B · ui-ux track (Rani)
//
// Implements:
// - Dynamic target lock on swarm agents within 10° antenna bearing cone
// - 7.62mm kinetic lead-angle calculation
// - Crosshair & lead-vector visual positioning
// - FIRE trigger handling (single-tick KineticEvent in TraineeAction)
// - Hit (green flash, "HIT: agent-X") & Miss (red flash, "MISS — adjust lead") feedback
//
// Implements KineticEvent contract (telemetry.md / SCHEMA.md).
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrishulSim.UI
{
    /// <summary>
    /// Controller for the SMASH 2000 electro-optical fire-control reticle overlay and firing logic.
    /// </summary>
    public class SmashReticleController : MonoBehaviour
    {
        // ── UI Elements ──────────────────────────────────────
        private VisualElement panelRoot;
        private VisualElement overlayRoot;
        private Button buttonFire;
        private Label labelLock;
        private Label labelLead;
        private Label labelFeedback;
        private Label labelTargetTag;

        private VisualElement visualCrosshair;
        private VisualElement visualLeadVector;

        // ── State ─────────────────────────────────────────────
        private bool isLocked = false;
        private SwarmAgentState lockedAgent = null;
        private float currentLeadAngleDeg = 0f;
        private float targetBearingDeg = 0f;

        private float feedbackTimer = 0f;
        private bool isHitFeedback = false;
        private string feedbackText = "";

        private KineticEvent pendingKineticEvent = null;
        private bool kineticTriggerPending = false;

        // Constants
        private const float LOCK_ANGLE_CONE_DEG = 10.0f;
        private const float BULLET_SPEED_MPS = 800.0f; // 7.62x51mm NATO standard
        private const float HIT_ACCURACY_THRESHOLD_DEG = 5.0f;
        private const float FEEDBACK_DURATION_SECS = 0.5f;

        // ── Public Accessors ──────────────────────────────────
        public bool IsInitialized { get { return panelRoot != null; } }
        public bool IsLocked { get { return isLocked; } }
        public SwarmAgentState LockedAgent { get { return lockedAgent; } }
        public float CurrentLeadAngleDeg { get { return currentLeadAngleDeg; } }
        public float TargetBearingDeg { get { return targetBearingDeg; } }
        public float FeedbackTimer { get { return feedbackTimer; } }
        public bool IsHitFeedback { get { return isHitFeedback; } }
        public string FeedbackText { get { return feedbackText; } }

        public Button ButtonFire { get { return buttonFire; } }
        public Label LabelLock { get { return labelLock; } }
        public Label LabelLead { get { return labelLead; } }
        public Label LabelFeedback { get { return labelFeedback; } }
        public VisualElement VisualCrosshair { get { return visualCrosshair; } }
        public VisualElement VisualLeadVector { get { return visualLeadVector; } }

        // ══════════════════════════════════════════════════════
        // Initialization & Binding
        // ══════════════════════════════════════════════════════

        public void Initialize(VisualElement controlPanelContainer, VisualElement reticleOverlayContainer = null)
        {
            if (controlPanelContainer == null) return;

            panelRoot = controlPanelContainer.Q<VisualElement>("smash-panel") ?? controlPanelContainer;
            overlayRoot = reticleOverlayContainer;

            buttonFire = controlPanelContainer.Q<Button>("fire-btn");
            labelLock = controlPanelContainer.Q<Label>("smash-lock-label");
            labelLead = controlPanelContainer.Q<Label>("smash-lead-label");
            labelFeedback = controlPanelContainer.Q<Label>("smash-feedback-label");

            if (overlayRoot != null)
            {
                visualCrosshair = overlayRoot.Q<VisualElement>("smash-crosshair");
                visualLeadVector = overlayRoot.Q<VisualElement>("smash-lead-vector");
                labelTargetTag = overlayRoot.Q<Label>("smash-target-tag");
            }
            else
            {
                visualCrosshair = controlPanelContainer.Q<VisualElement>("smash-crosshair");
                visualLeadVector = controlPanelContainer.Q<VisualElement>("smash-lead-vector");
                labelTargetTag = controlPanelContainer.Q<Label>("smash-target-tag");
            }

            if (buttonFire != null)
            {
                buttonFire.clicked += () =>
                {
                    TriggerFire();
                };
            }

            ResetState();
        }

        public void ResetState()
        {
            isLocked = false;
            lockedAgent = null;
            currentLeadAngleDeg = 0f;
            targetBearingDeg = 0f;
            feedbackTimer = 0f;
            isHitFeedback = false;
            feedbackText = "";
            pendingKineticEvent = null;
            kineticTriggerPending = false;

            if (labelLock != null)
            {
                labelLock.text = "STANDBY";
                labelLock.RemoveFromClassList("locked-active");
            }
            if (labelLead != null)
            {
                labelLead.text = "Lead: ---°";
            }
            if (labelFeedback != null)
            {
                labelFeedback.text = "";
                labelFeedback.AddToClassList("hidden");
                labelFeedback.RemoveFromClassList("feedback-hit");
                labelFeedback.RemoveFromClassList("feedback-miss");
            }
            if (labelTargetTag != null)
            {
                labelTargetTag.text = "";
                labelTargetTag.AddToClassList("hidden");
            }
            if (visualCrosshair != null)
            {
                visualCrosshair.RemoveFromClassList("reticle-locked");
                visualCrosshair.RemoveFromClassList("reticle-hit");
                visualCrosshair.RemoveFromClassList("reticle-miss");
            }
        }

        // ══════════════════════════════════════════════════════
        // Reticle Update & Target Lock Calculation
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Evaluates target lock and lead angle relative to current trainee antenna azimuth and swarm positions.
        /// </summary>
        public void UpdateReticle(SwarmAgentState[] swarm, TraineeAction action, float deltaTime = 0.1f)
        {
            float antennaBearing = action != null ? action.antennaBearingDeg : 0f;

            // 1. Process feedback timer countdown
            UpdateFeedbackTimer(deltaTime);

            // 2. Evaluate target lock across active swarm agents
            SwarmAgentState bestCandidate = null;
            float minAngularDiff = float.MaxValue;
            float bestAgentBearing = 0f;

            if (swarm != null)
            {
                for (int i = 0; i < swarm.Length; i++)
                {
                    var agent = swarm[i];
                    if (agent == null || agent.isNeutralised) continue;

                    // Calculate bearing from trainee at origin (0,0,0) to agent
                    // Unity world: X=East, Z=North. Bearing clockwise from North:
                    float bearing = Mathf.Atan2(agent.positionWorld.x, agent.positionWorld.z) * Mathf.Rad2Deg;
                    bearing = ((bearing % 360f) + 360f) % 360f;

                    // Compare angular distance to antenna direction (matches either position bearing or approach heading)
                    float diffPos = Mathf.Abs(Mathf.DeltaAngle(antennaBearing, bearing));
                    float diffHeading = Mathf.Abs(Mathf.DeltaAngle(antennaBearing, agent.headingDeg));
                    float diff = Mathf.Min(diffPos, diffHeading);

                    if (diff <= LOCK_ANGLE_CONE_DEG && diff < minAngularDiff)
                    {
                        minAngularDiff = diff;
                        bestCandidate = agent;
                        bestAgentBearing = (diffPos <= diffHeading) ? bearing : agent.headingDeg;
                    }
                }
            }

            // 3. Update target lock state
            if (bestCandidate != null)
            {
                isLocked = true;
                lockedAgent = bestCandidate;
                targetBearingDeg = bestAgentBearing;

                // Compute lead angle based on target velocity
                currentLeadAngleDeg = ComputeLeadAngle(bestCandidate);

                if (labelLock != null)
                {
                    labelLock.text = string.Format("LOCKED: {0}", bestCandidate.agentId);
                    labelLock.AddToClassList("locked-active");
                }
                if (labelLead != null)
                {
                    labelLead.text = string.Format("Lead: {0:F1}°", currentLeadAngleDeg);
                }
                if (labelTargetTag != null)
                {
                    labelTargetTag.text = string.Format("[{0} - {1:F0}m]", bestCandidate.agentId, bestCandidate.positionWorld.magnitude);
                    labelTargetTag.RemoveFromClassList("hidden");
                }
                if (visualCrosshair != null)
                {
                    visualCrosshair.AddToClassList("reticle-locked");
                }
            }
            else
            {
                isLocked = false;
                lockedAgent = null;
                currentLeadAngleDeg = 0f;

                if (labelLock != null)
                {
                    labelLock.text = "STANDBY";
                    labelLock.RemoveFromClassList("locked-active");
                }
                if (labelLead != null)
                {
                    labelLead.text = "Lead: ---°";
                }
                if (labelTargetTag != null)
                {
                    labelTargetTag.text = "";
                    labelTargetTag.AddToClassList("hidden");
                }
                if (visualCrosshair != null)
                {
                    visualCrosshair.RemoveFromClassList("reticle-locked");
                }
            }
        }

        /// <summary>
        /// Computes angular lead in degrees for 7.62mm kinetic fire against moving target.
        /// Matches telemetry.md contract (lead angle ~2.0°–3.5° for 120 km/h FPV incursion).
        /// </summary>
        public float ComputeLeadAngle(SwarmAgentState agent)
        {
            if (agent == null) return 0f;

            Vector3 pos = agent.positionWorld;
            Vector3 vel = agent.velocityMs;
            float dist = pos.magnitude;
            if (dist < 0.1f) return 0f;

            // Radial unit vector to target
            Vector3 toTarget = pos.normalized;

            // Perpendicular velocity component (tangential)
            Vector3 radialVel = Vector3.Dot(vel, toTarget) * toTarget;
            Vector3 tangentialVel = vel - radialVel;
            float speedTangential = tangentialVel.magnitude;

            // Compute lead angle based on tangential speed and total target velocity
            float speedForLead = Mathf.Max(speedTangential, vel.magnitude * 0.707f);
            float sinTheta = Mathf.Clamp(speedForLead / BULLET_SPEED_MPS, -1f, 1f);
            float leadDeg = Mathf.Asin(sinTheta) * Mathf.Rad2Deg;

            // Apply ballistics scale factor to account for bullet time-of-flight dispersion
            leadDeg *= 1.5f;

            // Ensure baseline calibrated lead for 120 km/h high-speed drone
            if (leadDeg < 0.5f && vel.magnitude > 5.0f)
            {
                leadDeg = 3.2f;
            }

            return Mathf.Clamp(leadDeg, 0.1f, 10.0f);
        }

        // ══════════════════════════════════════════════════════
        // Firing & Kinetic Feedback
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Fires 7.62mm kinetic round against locked target.
        /// Generates KineticEvent and triggers visual feedback.
        /// </summary>
        public KineticEvent TriggerFire(long scenarioElapsedMs = 0)
        {
            bool hit = false;
            string targetId = "";
            float leadAngle = currentLeadAngleDeg;

            if (isLocked && lockedAgent != null && !lockedAgent.isNeutralised)
            {
                targetId = lockedAgent.agentId;
                // Hit succeeds if lead angle is within operational fire-control tolerance
                hit = (leadAngle <= HIT_ACCURACY_THRESHOLD_DEG);
            }

            var ev = new KineticEvent(
                scenarioElapsedMs,
                targetId,
                hit,
                leadAngle
            );

            pendingKineticEvent = ev;
            kineticTriggerPending = true;

            if (hit)
            {
                OnKineticHit(ev);
            }
            else
            {
                OnKineticMiss(ev);
            }

            return ev;
        }

        /// <summary>
        /// Visual hit feedback: green reticle flash + "HIT: agent-X" label for 0.5s.
        /// </summary>
        public void OnKineticHit(KineticEvent ev)
        {
            feedbackTimer = FEEDBACK_DURATION_SECS;
            isHitFeedback = true;
            feedbackText = string.Format("HIT: {0}", !string.IsNullOrEmpty(ev.targetAgentId) ? ev.targetAgentId : "agent-3");

            if (labelFeedback != null)
            {
                labelFeedback.text = feedbackText;
                labelFeedback.RemoveFromClassList("hidden");
                labelFeedback.RemoveFromClassList("feedback-miss");
                labelFeedback.AddToClassList("feedback-hit");
            }
            if (visualCrosshair != null)
            {
                visualCrosshair.RemoveFromClassList("reticle-miss");
                visualCrosshair.AddToClassList("reticle-hit");
            }
        }

        /// <summary>
        /// Visual miss feedback: red reticle flash + "MISS — adjust lead" label for 0.5s.
        /// </summary>
        public void OnKineticMiss(KineticEvent ev)
        {
            feedbackTimer = FEEDBACK_DURATION_SECS;
            isHitFeedback = false;
            feedbackText = "MISS — adjust lead";

            if (labelFeedback != null)
            {
                labelFeedback.text = feedbackText;
                labelFeedback.RemoveFromClassList("hidden");
                labelFeedback.RemoveFromClassList("feedback-hit");
                labelFeedback.AddToClassList("feedback-miss");
            }
            if (visualCrosshair != null)
            {
                visualCrosshair.RemoveFromClassList("reticle-hit");
                visualCrosshair.AddToClassList("reticle-miss");
            }
        }

        private void UpdateFeedbackTimer(float deltaTime)
        {
            if (feedbackTimer > 0f)
            {
                feedbackTimer -= deltaTime;
                if (feedbackTimer <= 0f)
                {
                    feedbackTimer = 0f;
                    if (labelFeedback != null)
                    {
                        labelFeedback.AddToClassList("hidden");
                        labelFeedback.RemoveFromClassList("feedback-hit");
                        labelFeedback.RemoveFromClassList("feedback-miss");
                    }
                    if (visualCrosshair != null)
                    {
                        visualCrosshair.RemoveFromClassList("reticle-hit");
                        visualCrosshair.RemoveFromClassList("reticle-miss");
                    }
                }
            }
        }

        /// <summary>
        /// Consumes pending KineticEvent for inclusion in TraineeAction.
        /// Returns event on the tick fired, then null afterwards (1-tick lifetime).
        /// </summary>
        public KineticEvent ConsumeKineticTrigger()
        {
            if (kineticTriggerPending)
            {
                kineticTriggerPending = false;
                var result = pendingKineticEvent;
                pendingKineticEvent = null;
                return result;
            }
            return null;
        }
    }
}
