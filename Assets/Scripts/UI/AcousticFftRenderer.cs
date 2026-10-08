// ============================================================
// AcousticFftRenderer.cs — Real-Time Acoustic FFT Sonar Bar Chart
// TRISHUL-SIM · UX-P1-A · ui-ux track (Rani)
//
// Renders an 8-band audio spectrum analyzer across 20–2000 Hz.
// Dominant band (blade-tip harmonic ~220 Hz) is driven by acousticSplDb:
// - Below 40 dB: ambient noise floor (bars low/dim)
// - 40–55 dB: approaching drone swarm (amber warning #FFB300)
// - >55 dB: close-range acoustic lock (red alert flash #CC0000)
// Telemetry readout: "SPL: XX.X dB"
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrishulSim.UI
{
    /// <summary>
    /// Renders an 8-band acoustic FFT equalizer to a UITK VisualElement container.
    /// </summary>
    public class AcousticFftRenderer : MonoBehaviour
    {
        public const int BandCount = 8;
        public const int BladeTipBandIndex = 2; // ~220 Hz blade-tip harmonic band (60-250 Hz)
        public const float AmbientNoiseFloorDb = 30.0f;
        public const float ThreatAmberThresholdDb = 40.0f;
        public const float ThreatRedThresholdDb = 55.0f;
        public const float MaxSplDb = 85.0f;

        // Band center frequency labels for 8 octaves: 32Hz, 64Hz, 128Hz, 250Hz, 500Hz, 1kHz, 2kHz, 4kHz
        public static readonly string[] BandLabels = new string[]
        {
            "32Hz", "64Hz", "220Hz", "500Hz", "1kHz", "1.5k", "2kHz", "3kHz"
        };

        private VisualElement container;
        private VisualElement[] barElements;
        private Label readingLabel;
        private bool isInitialized = false;

        public bool IsInitialized { get { return isInitialized; } }
        public VisualElement[] BarElements { get { return barElements; } }

        /// <summary>
        /// Binds and initializes the 8-band bar chart inside acoustic-content.
        /// </summary>
        public void Initialize(VisualElement contentContainer, Label splReadingLabel)
        {
            container = contentContainer;
            readingLabel = splReadingLabel;
            if (container == null) return;

            // Clear any placeholder text
            container.Clear();

            // Setup container layout: horizontal row with bottom alignment
            container.style.flexDirection = FlexDirection.Row;
            container.style.justifyContent = Justify.SpaceBetween;
            container.style.alignItems = Align.FlexEnd;
            container.style.paddingLeft = 8;
            container.style.paddingRight = 8;
            container.style.paddingBottom = 8;
            container.style.paddingTop = 8;

            barElements = new VisualElement[BandCount];

            for (int i = 0; i < BandCount; i++)
            {
                // Each band column
                var bandColumn = new VisualElement();
                bandColumn.name = string.Format("fft-col-{0}", i);
                bandColumn.style.flexGrow = 1;
                bandColumn.style.flexBasis = 0;
                bandColumn.style.marginRight = (i < BandCount - 1) ? 4 : 0;
                bandColumn.style.height = Length.Percent(100);
                bandColumn.style.justifyContent = Justify.FlexEnd;
                bandColumn.style.alignItems = Align.Stretch;

                // Inner filled bar
                var bar = new VisualElement();
                bar.name = string.Format("fft-bar-{0}", i);
                bar.style.height = Length.Percent(5);
                bar.style.backgroundColor = new Color(0.12f, 0.15f, 0.12f, 1.0f); // Inactive dim color
                bar.style.borderTopLeftRadius = 2;
                bar.style.borderTopRightRadius = 2;

                bandColumn.Add(bar);
                container.Add(bandColumn);
                barElements[i] = bar;
            }

            isInitialized = true;
        }

        /// <summary>
        /// Updates the 8 FFT bars and the SPL readout label from the latest sensor tick.
        /// </summary>
        public void UpdateAcoustic(SensorReadings sr)
        {
            if (!isInitialized || barElements == null) return;

            float spl = (sr != null) ? sr.acousticSplDb : AmbientNoiseFloorDb;

            // Update SPL label
            if (readingLabel != null)
            {
                readingLabel.text = string.Format("SPL: {0:F1} dB", spl);
            }

            for (int i = 0; i < BandCount; i++)
            {
                var bar = barElements[i];
                if (bar == null) continue;

                float bandSpl;
                if (i == BladeTipBandIndex)
                {
                    // Dominant blade-tip acoustic spike
                    bandSpl = spl;
                }
                else
                {
                    // Ambient sidebands with realistic low amplitude
                    float distanceToDominant = Mathf.Abs(i - BladeTipBandIndex);
                    float rollOff = 12.0f * distanceToDominant;
                    bandSpl = Mathf.Max(AmbientNoiseFloorDb, spl - rollOff);
                }

                // Compute normalized height percentage (0% to 100%)
                float heightPercent;
                Color barColor;

                if (bandSpl < ThreatAmberThresholdDb)
                {
                    // Noise floor (< 40 dB): Minimal bar height, dim green
                    heightPercent = Mathf.Clamp(bandSpl / ThreatAmberThresholdDb * 15.0f, 4.0f, 15.0f);
                    barColor = new Color(0.08f, 0.25f, 0.08f, 0.6f);
                }
                else if (bandSpl <= ThreatRedThresholdDb)
                {
                    // Amber warning (40–55 dB): Mid height, MIL-STD-2525 Amber (#FFB300)
                    float t = (bandSpl - ThreatAmberThresholdDb) / (ThreatRedThresholdDb - ThreatAmberThresholdDb);
                    heightPercent = Mathf.Lerp(25.0f, 65.0f, t);
                    barColor = new Color(1.0f, 0.70f, 0.0f, 1.0f); // #FFB300
                }
                else
                {
                    // Red alert (> 55 dB): High height, MIL-STD-2525 Red (#CC0000)
                    float t = Mathf.Clamp01((bandSpl - ThreatRedThresholdDb) / (MaxSplDb - ThreatRedThresholdDb));
                    heightPercent = Mathf.Lerp(70.0f, 98.0f, t);
                    barColor = new Color(0.80f, 0.0f, 0.0f, 1.0f); // #CC0000
                }

                bar.style.height = Length.Percent(heightPercent);
                bar.style.backgroundColor = barColor;
            }
        }
    }
}
