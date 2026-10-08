// ============================================================
// EwJammerController.cs — Directional EW Jammer Controls Controller
// TRISHUL-SIM · UX-P1-B · ui-ux track (Rani)
//
// Manages EW Jammer UI elements:
// - Azimuth (0–359°) & Elevation (-90° to +90°) sliders
// - Jam frequency band selector (2.4 GHz, 5.825 GHz, 0.915 GHz)
// - ENGAGE toggle
// - ROE Deconfliction check & Friendly Blackout warning (MIL-STD-2525 Red)
//
// Implements TraineeAction & EWJammerState contracts (telemetry.md / SCHEMA.md).
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrishulSim.UI
{
    /// <summary>
    /// Controller for the Trainee Cockpit's Directional EW Jammer panel.
    /// </summary>
    public class EwJammerController : MonoBehaviour
    {
        // ── UI Elements ──────────────────────────────────────
        private VisualElement panelRoot;
        private SliderInt sliderAzimuth;
        private SliderInt sliderElevation;
        private DropdownField dropdownBand;
        private Toggle toggleEngage;
        private Label labelRoeWarning;

        // ── State ─────────────────────────────────────────────
        private int azimuthDeg = 0;
        private int elevationDeg = 0;
        private float selectedBandGHz = 5.825f;
        private string selectedBandStr = "5.825 GHz";
        private bool isEngaged = false;
        private bool friendlyBlackoutRisk = false;

        private readonly string[] availableBands = new string[]
        {
            "5.825 GHz",
            "2.4 GHz",
            "0.915 GHz"
        };

        // ── Public Accessors & State ──────────────────────────
        public bool IsInitialized { get { return panelRoot != null; } }
        public int AzimuthDeg { get { return azimuthDeg; } }
        public int ElevationDeg { get { return elevationDeg; } }
        public float SelectedBandGHz { get { return selectedBandGHz; } }
        public string SelectedBandStr { get { return selectedBandStr; } }
        public bool IsEngaged { get { return isEngaged; } }
        public bool FriendlyBlackoutRisk { get { return friendlyBlackoutRisk; } }

        public SliderInt SliderAzimuth { get { return sliderAzimuth; } }
        public SliderInt SliderElevation { get { return sliderElevation; } }
        public DropdownField DropdownBand { get { return dropdownBand; } }
        public Toggle ToggleEngage { get { return toggleEngage; } }
        public Label LabelRoeWarning { get { return labelRoeWarning; } }

        public EWJammerState CurrentState
        {
            get
            {
                return new EWJammerState(
                    isEngaged,
                    azimuthDeg,
                    elevationDeg,
                    selectedBandGHz,
                    friendlyBlackoutRisk
                );
            }
        }

        // ══════════════════════════════════════════════════════
        // Initialization & Binding
        // ══════════════════════════════════════════════════════

        public void Initialize(VisualElement container)
        {
            if (container == null) return;

            panelRoot = container.Q<VisualElement>("ew-panel") ?? container;

            sliderAzimuth = container.Q<SliderInt>("ew-azimuth");
            sliderElevation = container.Q<SliderInt>("ew-elevation");
            dropdownBand = container.Q<DropdownField>("ew-band");
            toggleEngage = container.Q<Toggle>("ew-engage");
            labelRoeWarning = container.Q<Label>("ew-roe-warning");

            // Setup dropdown choices
            if (dropdownBand != null)
            {
                dropdownBand.choices.Clear();
                foreach (var band in availableBands)
                {
                    dropdownBand.choices.Add(band);
                }
                dropdownBand.value = selectedBandStr;
                dropdownBand.RegisterValueChangedCallback(evt =>
                {
                    SetBand(evt.newValue);
                });
            }

            // Setup Azimuth slider
            if (sliderAzimuth != null)
            {
                sliderAzimuth.lowValue = 0;
                sliderAzimuth.highValue = 359;
                sliderAzimuth.value = azimuthDeg;
                sliderAzimuth.RegisterValueChangedCallback(evt =>
                {
                    SetAzimuth(evt.newValue);
                });
            }

            // Setup Elevation slider
            if (sliderElevation != null)
            {
                sliderElevation.lowValue = -90;
                sliderElevation.highValue = 90;
                sliderElevation.value = elevationDeg;
                sliderElevation.RegisterValueChangedCallback(evt =>
                {
                    SetElevation(evt.newValue);
                });
            }

            // Setup Engage toggle
            if (toggleEngage != null)
            {
                toggleEngage.value = isEngaged;
                toggleEngage.RegisterValueChangedCallback(evt =>
                {
                    SetEngaged(evt.newValue);
                });
            }

            UpdateRoeWarningVisual();
        }

        public void ResetState()
        {
            azimuthDeg = 0;
            elevationDeg = 0;
            selectedBandGHz = 5.825f;
            selectedBandStr = "5.825 GHz";
            isEngaged = false;
            friendlyBlackoutRisk = false;

            if (sliderAzimuth != null) sliderAzimuth.value = 0;
            if (sliderElevation != null) sliderElevation.value = 0;
            if (dropdownBand != null) dropdownBand.value = "5.825 GHz";
            if (toggleEngage != null) toggleEngage.value = false;

            UpdateRoeWarningVisual();
        }

        // ══════════════════════════════════════════════════════
        // Control Setters
        // ══════════════════════════════════════════════════════

        public void SetAzimuth(int deg)
        {
            azimuthDeg = ((deg % 360) + 360) % 360;
            if (sliderAzimuth != null && sliderAzimuth.value != azimuthDeg)
            {
                sliderAzimuth.value = azimuthDeg;
            }
            EvaluateRoeRisk();
        }

        public void SetElevation(int deg)
        {
            elevationDeg = Mathf.Clamp(deg, -90, 90);
            if (sliderElevation != null && sliderElevation.value != elevationDeg)
            {
                sliderElevation.value = elevationDeg;
            }
            EvaluateRoeRisk();
        }

        public void SetBand(string bandStr)
        {
            selectedBandStr = bandStr;
            if (bandStr.Contains("5.8") || bandStr.Contains("5.825"))
            {
                selectedBandGHz = 5.825f;
            }
            else if (bandStr.Contains("2.4"))
            {
                selectedBandGHz = 2.4f;
            }
            else if (bandStr.Contains("915") || bandStr.Contains("0.915"))
            {
                selectedBandGHz = 0.915f;
            }

            if (dropdownBand != null && dropdownBand.value != bandStr)
            {
                dropdownBand.value = bandStr;
            }
            EvaluateRoeRisk();
        }

        public void SetBandGHz(float ghz)
        {
            selectedBandGHz = ghz;
            if (Math.Abs(ghz - 5.825f) < 0.05f)
                selectedBandStr = "5.825 GHz";
            else if (Math.Abs(ghz - 2.4f) < 0.05f)
                selectedBandStr = "2.4 GHz";
            else if (Math.Abs(ghz - 0.915f) < 0.05f)
                selectedBandStr = "0.915 GHz";

            if (dropdownBand != null && dropdownBand.value != selectedBandStr)
            {
                dropdownBand.value = selectedBandStr;
            }
            EvaluateRoeRisk();
        }

        public void SetEngaged(bool engaged)
        {
            isEngaged = engaged;
            if (toggleEngage != null && toggleEngage.value != isEngaged)
            {
                toggleEngage.value = isEngaged;
            }
            EvaluateRoeRisk();
        }

        // ══════════════════════════════════════════════════════
        // ROE Deconfliction Logic
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Evaluates ROE deconfliction risk:
        /// Friendly VHF radio network operates in 30-88 MHz / 915 MHz band or in friendly base sector (60°–120° azimuth).
        /// Emitting on 0.915 GHz or beaming at 90° azimuth creates friendly VHF blackout risk.
        /// Beaming at 247° attack axis on 5.825 GHz clears ROE violation.
        /// </summary>
        public bool EvaluateRoeRisk(bool friendlyVhfActive = false)
        {
            bool isFriendlySector = (azimuthDeg >= 60 && azimuthDeg <= 120);
            bool isVhfOverlapBand = (Math.Abs(selectedBandGHz - 0.915f) < 0.05f);

            // Risk occurs if jamming on VHF band or jamming towards friendly sector with VHF active
            friendlyBlackoutRisk = (isVhfOverlapBand && isFriendlySector) ||
                                  (friendlyVhfActive && isFriendlySector) ||
                                  (isVhfOverlapBand && isEngaged);

            UpdateRoeWarningVisual();
            return friendlyBlackoutRisk;
        }

        public void OnScenarioTick(string threatState, bool friendlyVhfActive)
        {
            EvaluateRoeRisk(friendlyVhfActive);
        }

        private void UpdateRoeWarningVisual()
        {
            if (labelRoeWarning == null) return;

            if (friendlyBlackoutRisk)
            {
                labelRoeWarning.RemoveFromClassList("hidden");
                labelRoeWarning.AddToClassList("roe-warning-active");
            }
            else
            {
                labelRoeWarning.AddToClassList("hidden");
                labelRoeWarning.RemoveFromClassList("roe-warning-active");
            }
        }

        // ══════════════════════════════════════════════════════
        // Telemetry Export
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Builds TraineeAction snapshot for the current tick (Entity 3 / telemetry.md).
        /// </summary>
        public TraineeAction BuildTraineeAction(KineticEvent kinetic = null)
        {
            return new TraineeAction(
                azimuthDeg,
                elevationDeg,
                isEngaged,
                isEngaged ? (float?)selectedBandGHz : null,
                kinetic
            );
        }
    }
}
