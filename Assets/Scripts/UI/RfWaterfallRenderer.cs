// ============================================================
// RfWaterfallRenderer.cs — Live RF Spectrogram Waterfall Display
// TRISHUL-SIM · UX-P1-A · ui-ux track (Rani)
//
// Renders a real-time scrolling RF spectrogram across 0–6 GHz.
// Color mapping: black (-90 dBm noise floor), cyan (-70 dBm),
// yellow (-50 dBm), red (-30 dBm high signal).
// Frequency markers: 915 MHz (ELRS), 2.4 GHz, 5.8 GHz.
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrishulSim.UI
{
    /// <summary>
    /// Renders a scrolling 256x128 RF waterfall spectrogram to a UITK VisualElement.
    /// </summary>
    public class RfWaterfallRenderer : MonoBehaviour
    {
        public const int Width = 256;
        public const int Height = 128;
        public const float MinFreqGHz = 0.0f;
        public const float MaxFreqGHz = 6.0f;
        public const float NoiseFloorDbm = -90.0f;

        private Texture2D waterfallTexture;
        private VisualElement container;
        private Color[] rowBuffer;
        private bool isInitialized = false;

        public Texture2D Texture { get { return waterfallTexture; } }
        public bool IsInitialized { get { return isInitialized; } }

        /// <summary>
        /// Binds the renderer to the rf-content VisualElement container.
        /// </summary>
        public void Initialize(VisualElement contentContainer)
        {
            container = contentContainer;
            if (container == null) return;

            // Clear any placeholder text
            container.Clear();

            // Allocate 256x128 texture
            waterfallTexture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            rowBuffer = new Color[Width];

            // Initialize entire texture to noise floor (black)
            Color[] initialPixels = new Color[Width * Height];
            Color noiseColor = MapDbmToColor(NoiseFloorDbm);
            for (int i = 0; i < initialPixels.Length; i++)
            {
                initialPixels[i] = noiseColor;
            }
            waterfallTexture.SetPixels(initialPixels);
            waterfallTexture.Apply();

            // Set as background of the container
            container.style.backgroundImage = new StyleBackground(waterfallTexture);
            isInitialized = true;
        }

        /// <summary>
        /// Updates the waterfall with the latest 10 Hz sensor tick data.
        /// Shifts rows upward and writes the new frequency intensity scan at the bottom.
        /// </summary>
        public void UpdateWaterfall(SensorReadings sr)
        {
            if (!isInitialized || waterfallTexture == null || sr == null) return;

            // 1. Shift existing texture rows up by 1 pixel (scroll upwards)
            // Rows 0..(Height-2) move to 1..(Height-1)
            Color[] oldRows = waterfallTexture.GetPixels(0, 0, Width, Height - 1);
            waterfallTexture.SetPixels(0, 1, Width, Height - 1, oldRows);

            // 2. Generate bottom row (index y = 0)
            // Start with ambient background noise floor with subtle realistic jitter
            for (int x = 0; x < Width; x++)
            {
                float ambientJitter = -90.0f + (float)(Math.Sin(x * 0.15f) * 1.5f);
                rowBuffer[x] = MapDbmToColor(ambientJitter);
            }

            // 3. If a signal peak is detected, map GHz to X coordinate and plot Gaussian spread
            if (sr.rfWaterfallPeakGHz > 0.01f && sr.rfWaterfallPeakDbm > NoiseFloorDbm)
            {
                float normalizedX = (sr.rfWaterfallPeakGHz - MinFreqGHz) / (MaxFreqGHz - MinFreqGHz);
                int peakCenter = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (Width - 1)), 0, Width - 1);
                int spreadRadius = 4;

                for (int offset = -spreadRadius; offset <= spreadRadius; offset++)
                {
                    int x = peakCenter + offset;
                    if (x >= 0 && x < Width)
                    {
                        // Gaussian attenuation around center peak
                        float attenuation = Mathf.Exp(-0.5f * (offset * offset) / 2.0f);
                        float peakPower = Mathf.Lerp(NoiseFloorDbm, sr.rfWaterfallPeakDbm, attenuation);
                        rowBuffer[x] = MapDbmToColor(peakPower);
                    }
                }
            }

            // 4. Commit new row and apply texture changes
            waterfallTexture.SetPixels(0, 0, Width, 1, rowBuffer);
            waterfallTexture.Apply();
        }

        /// <summary>
        /// Converts power level in dBm to spectrogram false-color:
        /// black (-90 dBm) -> cyan (-70 dBm) -> yellow (-50 dBm) -> red (-30 dBm).
        /// </summary>
        public static Color MapDbmToColor(float dbm)
        {
            if (dbm <= -90.0f)
            {
                return Color.black;
            }
            if (dbm <= -70.0f)
            {
                // Lerp black (#000000) to cyan (#00FFFF)
                float t = Mathf.Clamp01((dbm - (-90.0f)) / 20.0f);
                return Color.Lerp(Color.black, Color.cyan, t);
            }
            if (dbm <= -50.0f)
            {
                // Lerp cyan (#00FFFF) to yellow (#FFFF00)
                float t = Mathf.Clamp01((dbm - (-70.0f)) / 20.0f);
                return Color.Lerp(Color.cyan, Color.yellow, t);
            }
            if (dbm <= -30.0f)
            {
                // Lerp yellow (#FFFF00) to red (#FF0000)
                float t = Mathf.Clamp01((dbm - (-50.0f)) / 20.0f);
                return Color.Lerp(Color.yellow, Color.red, t);
            }

            // Saturated max power (> -30 dBm)
            return Color.red;
        }
    }
}
