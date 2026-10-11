// ============================================================
// DopplerScopeRenderer.cs — Micro-Doppler PPI Circular Radar Scope
// TRISHUL-SIM · UX-P1-A · ui-ux track (Rani)
//
// Renders a 200x200 Plan Position Indicator (PPI) radar scope with:
// - Dark green circular grid with 25%, 50%, 75%, 100% range rings
// - Crosshair reference grid lines (N-S, E-W axes)
// - Swarm radar returns: Decoys (5x5 blip) vs Attackers (3x3 blip)
// - Real-time Doppler frequency telemetry label (e.g. "Doppler: 220.4 Hz")
// ============================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrishulSim.UI
{
    /// <summary>
    /// Renders a circular Micro-Doppler PPI radar scope to a UITK VisualElement.
    /// </summary>
    public class DopplerScopeRenderer : MonoBehaviour
    {
        public const int Size = 200;
        public const float MaxRadarRangeM = 500.0f; // 500m instrumented radar range

        private Texture2D scopeTexture;
        private Color[] backgroundPixels;
        private Color[] renderBuffer;
        private VisualElement container;
        private Label readingLabel;
        private bool isInitialized = false;

        public Texture2D Texture { get { return scopeTexture; } }
        public bool IsInitialized { get { return isInitialized; } }

        /// <summary>
        /// Binds the renderer to the doppler-content VisualElement and doppler-reading Label.
        /// </summary>
        public void Initialize(VisualElement contentContainer, Label dopplerReadingLabel)
        {
            container = contentContainer;
            readingLabel = dopplerReadingLabel;
            if (container == null) return;

            // Clear any placeholder text
            container.Clear();

            // Allocate 200x200 texture
            scopeTexture = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            backgroundPixels = new Color[Size * Size];
            renderBuffer = new Color[Size * Size];

            GenerateScopeBackground();

            // Commit background to texture
            Array.Copy(backgroundPixels, renderBuffer, backgroundPixels.Length);
            scopeTexture.SetPixels(renderBuffer);
            scopeTexture.Apply();

            // Set as background of container
            container.style.backgroundImage = new StyleBackground(scopeTexture);
            isInitialized = true;
        }

        /// <summary>
        /// Precomputes the circular PPI radar background with MIL-STD range rings and crosshairs.
        /// </summary>
        private void GenerateScopeBackground()
        {
            float center = Size / 2.0f;
            float maxRadius = center - 2.0f;

            Color backgroundBlack = new Color(0.04f, 0.05f, 0.04f, 1.0f); // Deep radar green-black (#0A0D0A)
            Color ringColor = new Color(0.0f, 0.35f, 0.0f, 1.0f);        // Range rings (#005900)
            Color axisColor = new Color(0.0f, 0.22f, 0.0f, 1.0f);        // Graticule axes
            Color bezelColor = new Color(0.0f, 0.50f, 0.0f, 1.0f);       // Outer boundary ring

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int idx = y * Size + x;
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > maxRadius)
                    {
                        // Outside scope boundary
                        backgroundPixels[idx] = Color.black;
                        continue;
                    }

                    // Outer bezel ring
                    if (Mathf.Abs(dist - maxRadius) <= 1.2f)
                    {
                        backgroundPixels[idx] = bezelColor;
                        continue;
                    }

                    // Range rings at 25%, 50%, 75%, 100%
                    float normDist = dist / maxRadius;
                    bool isRing = Mathf.Abs(normDist - 0.25f) < 0.015f ||
                                  Mathf.Abs(normDist - 0.50f) < 0.015f ||
                                  Mathf.Abs(normDist - 0.75f) < 0.015f;

                    if (isRing)
                    {
                        backgroundPixels[idx] = ringColor;
                        continue;
                    }

                    // Graticule crosshairs (center X and Y axes)
                    bool isAxis = (Mathf.Abs(dx) < 0.8f || Mathf.Abs(dy) < 0.8f);
                    if (isAxis)
                    {
                        backgroundPixels[idx] = axisColor;
                        continue;
                    }

                    backgroundPixels[idx] = backgroundBlack;
                }
            }
        }

        /// <summary>
        /// Updates the PPI scope with the current sensor readings and swarm agent positions.
        /// </summary>
        public void UpdateScope(SensorReadings sr, SwarmAgentState[] swarm)
        {
            if (!isInitialized || scopeTexture == null) return;

            // 1. Reset frame to static radar background
            Array.Copy(backgroundPixels, renderBuffer, backgroundPixels.Length);

            float center = Size / 2.0f;
            float maxRadius = center - 4.0f;

            // 2. Draw active radar blips from swarm snapshot
            if (swarm != null && swarm.Length > 0)
            {
                for (int i = 0; i < swarm.Length; i++)
                {
                    var agent = swarm[i];
                    if (agent == null || agent.isNeutralised) continue;

                    // Ground-plane coordinates relative to BOP sensor origin (x = East, z = North)
                    float range = Mathf.Sqrt(agent.positionWorld.x * agent.positionWorld.x + agent.positionWorld.z * agent.positionWorld.z);
                    if (range > MaxRadarRangeM) continue;

                    float normRange = Mathf.Clamp01(range / MaxRadarRangeM);
                    float pixelDist = normRange * maxRadius;

                    // Bearing angle in radians
                    float bearingRad = Mathf.Atan2(agent.positionWorld.x, agent.positionWorld.z);
                    int px = Mathf.RoundToInt(center + pixelDist * Mathf.Sin(bearingRad));
                    int py = Mathf.RoundToInt(center + pixelDist * Mathf.Cos(bearingRad));

                    // Decoys have high RCS (5x5 blip), Attackers have low RCS (3x3 blip)
                    int blipSize = agent.isDecoy ? 5 : 3;
                    Color blipColor = agent.isDecoy
                        ? new Color(0.2f, 1.0f, 0.4f, 1.0f)   // Bright phosphor green
                        : new Color(0.0f, 0.85f, 0.15f, 1.0f); // Standard radar green

                    DrawBlip(px, py, blipSize, blipColor);
                }
            }
            else if (sr != null && sr.radarDetectedCount > 0)
            {
                // Fallback procedural blip generator if swarm physics not attached
                int count = Mathf.Min(sr.radarDetectedCount, 6);
                for (int i = 0; i < count; i++)
                {
                    float angle = (240.0f + i * 8.0f) * Mathf.Deg2Rad;
                    float rangeNorm = 0.45f + (i % 2) * 0.1f;
                    int px = Mathf.RoundToInt(center + rangeNorm * maxRadius * Mathf.Sin(angle));
                    int py = Mathf.RoundToInt(center + rangeNorm * maxRadius * Mathf.Cos(angle));
                    int size = (i < 2) ? 5 : 3;
                    DrawBlip(px, py, size, Color.green);
                }
            }

            // 3. Commit rendered radar image
            scopeTexture.SetPixels(renderBuffer);
            scopeTexture.Apply();

            // 4. Update Doppler reading text
            if (readingLabel != null)
            {
                float doppler = (sr != null) ? sr.dopplerPeakHz : 0f;
                readingLabel.text = string.Format("Doppler: {0:F1} Hz", doppler);
            }
        }

        private void DrawBlip(int cx, int cy, int blipSize, Color color)
        {
            int half = blipSize / 2;
            for (int dy = -half; dy <= half; dy++)
            {
                for (int dx = -half; dx <= half; dx++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (x >= 0 && x < Size && y >= 0 && y < Size)
                    {
                        renderBuffer[y * Size + x] = color;
                    }
                }
            }
        }
    }
}
