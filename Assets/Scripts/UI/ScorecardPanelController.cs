// ============================================================
// ScorecardPanelController.cs — Tactical Performance Scorecard Controller
// TRISHUL-SIM · UX-P1-D · ui-ux track (Rajeswari)
//
// Binds ScorecardPanel.uxml elements to a Scorecard data object.
// Implements:
// 1. Doctrinal badge pass/fail styling with MIL-STD-2525 colours
// 2. Dynamic 4-column metric benchmarks table
// 3. Interactive timeline scrubber with spatial radar projection
// 4. Bilingual text binding and locale toggle reactivity
// 5. PDF export button stub wired for UX-P2-A
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrishulSim.UI
{
    public class ScorecardPanelController : MonoBehaviour
    {
        // ── UI Element References ──────────────────────────────
        private VisualElement root;
        private Label scorecardTitle;
        private Label scenarioNameLabel;
        private Label traineeIdLabel;
        private VisualElement doctrineBadge;
        private Label doctrineText;
        private VisualElement metricsTable;
        private Slider timelineSlider;
        private Label timelineTimeLabel;
        private VisualElement spatialOverlay;
        private VisualElement blipContainer;
        private Button pdfExportBtn;

        // ── Bound Data State ───────────────────────────────────
        private Scorecard currentScorecard;
        private int currentScrubMs = 0;
        private List<VisualElement> activeBlips = new List<VisualElement>();

        // ── Public Accessors for Validation / Tests ────────────
        public VisualElement Root { get { return root; } }
        public Scorecard CurrentScorecard { get { return currentScorecard; } }
        public int CurrentScrubMs { get { return currentScrubMs; } }
        public Label ScorecardTitle { get { return scorecardTitle; } }
        public Label ScenarioNameLabel { get { return scenarioNameLabel; } }
        public Label TraineeIdLabel { get { return traineeIdLabel; } }
        public VisualElement DoctrineBadge { get { return doctrineBadge; } }
        public Label DoctrineText { get { return doctrineText; } }
        public VisualElement MetricsTable { get { return metricsTable; } }
        public Slider TimelineSlider { get { return timelineSlider; } }
        public Label TimelineTimeLabel { get { return timelineTimeLabel; } }
        public VisualElement SpatialOverlay { get { return spatialOverlay; } }
        public VisualElement BlipContainer { get { return blipContainer; } }
        public Button PdfExportBtn { get { return pdfExportBtn; } }
        public int ActiveBlipCount { get { return activeBlips.Count; } }

        // ══════════════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc != null && uiDoc.rootVisualElement != null)
            {
                BindElements(uiDoc.rootVisualElement);
            }
        }

        private void Start()
        {
            if (LocaleToggle.Instance != null)
            {
                LocaleToggle.Instance.OnLocaleChanged += OnLocaleChanged;
            }

#if UNITY_EDITOR
            // Auto-bind fixture if opened directly in Editor
            if (currentScorecard == null)
            {
                Bind(ScorecardMock.GetPunjabFixture());
            }
#endif
        }

        private void OnDestroy()
        {
            if (LocaleToggle.Instance != null)
            {
                LocaleToggle.Instance.OnLocaleChanged -= OnLocaleChanged;
            }
            UnregisterCallbacks();
        }

        // ══════════════════════════════════════════════════════
        // Element Binding
        // ══════════════════════════════════════════════════════

        public void BindElements(VisualElement rootElement)
        {
            if (rootElement == null) return;
            root = rootElement;

            scorecardTitle = root.Q<Label>("scorecard-title");
            scenarioNameLabel = root.Q<Label>("scenario-name");
            traineeIdLabel = root.Q<Label>("trainee-id-label");
            doctrineBadge = root.Q<VisualElement>("doctrine-badge");
            doctrineText = root.Q<Label>("doctrine-text");
            metricsTable = root.Q<VisualElement>("metrics-table");
            timelineSlider = root.Q<Slider>("timeline-slider");
            timelineTimeLabel = root.Q<Label>("timeline-time");
            spatialOverlay = root.Q<VisualElement>("spatial-overlay");
            blipContainer = root.Q<VisualElement>("drone-blip-container");
            pdfExportBtn = root.Q<Button>("pdf-export-btn");

            RegisterCallbacks();
        }

        private void RegisterCallbacks()
        {
            if (timelineSlider != null)
            {
                timelineSlider.RegisterValueChangedCallback(OnTimelineSliderChanged);
            }

            if (pdfExportBtn != null)
            {
                pdfExportBtn.clicked += OnPdfExportClicked;
            }
        }

        private void UnregisterCallbacks()
        {
            if (timelineSlider != null)
            {
                timelineSlider.UnregisterValueChangedCallback(OnTimelineSliderChanged);
            }

            if (pdfExportBtn != null)
            {
                pdfExportBtn.clicked -= OnPdfExportClicked;
            }
        }

        // ══════════════════════════════════════════════════════
        // Public API: Bind Scorecard Data
        // ══════════════════════════════════════════════════════

        public void Bind(Scorecard sc)
        {
            currentScorecard = sc;
            if (currentScorecard == null || root == null) return;

            RefreshView();
        }

        public void RefreshView()
        {
            if (currentScorecard == null) return;
            var loc = LocaleToggle.Instance;
            bool isHindi = loc != null && loc.IsHindi;

            // 1. Title & Header
            if (scorecardTitle != null)
                scorecardTitle.text = loc != null ? loc.GetString("SCORECARD_TITLE") : "TACTICAL PERFORMANCE SCORECARD";

            if (scenarioNameLabel != null)
            {
                scenarioNameLabel.text = isHindi && !string.IsNullOrEmpty(currentScorecard.scenarioNameHindi)
                    ? currentScorecard.scenarioNameHindi
                    : currentScorecard.scenarioName;
            }

            if (traineeIdLabel != null)
            {
                string tId = string.IsNullOrEmpty(currentScorecard.traineeId) ? "Anonymous" : currentScorecard.traineeId;
                traineeIdLabel.text = (isHindi ? "प्रशिक्षु: " : "Trainee: ") + tId;
            }

            // 2. Doctrine Badge
            if (doctrineBadge != null && doctrineText != null)
            {
                doctrineBadge.RemoveFromClassList("badge-pass");
                doctrineBadge.RemoveFromClassList("badge-fail");

                if (currentScorecard.doctrinePassed)
                {
                    doctrineBadge.AddToClassList("badge-pass");
                    doctrineText.text = loc != null ? loc.GetString("DOCTRINE_PASSED") : "DOCTRINE PASSED";
                }
                else
                {
                    doctrineBadge.AddToClassList("badge-fail");
                    doctrineText.text = loc != null ? loc.GetString("DOCTRINE_FAILED") : "DOCTRINE FAILED";
                }
            }

            // 3. Metrics Table Rows
            PopulateMetricsTable(isHindi);

            // 4. Timeline Slider Setup
            if (timelineSlider != null)
            {
                int maxSec = Mathf.Max(1, currentScorecard.durationMs / 1000);
                if (currentScorecard.timelineIndex != null && currentScorecard.timelineIndex.Count > 0)
                {
                    maxSec = Mathf.Max(maxSec, currentScorecard.timelineIndex[currentScorecard.timelineIndex.Count - 1] / 1000);
                }
                timelineSlider.lowValue = 0;
                timelineSlider.highValue = maxSec;
                timelineSlider.value = 0;
            }

            // 5. Button Text
            if (pdfExportBtn != null)
            {
                pdfExportBtn.text = loc != null ? loc.GetString("PDF_EXPORT_BTN") : "Export PDF";
            }

            // 6. Reset Scrubber to T+0s
            SetScrubPosition(0);
        }

        private void PopulateMetricsTable(bool isHindi)
        {
            if (metricsTable == null || currentScorecard == null || currentScorecard.metrics == null) return;
            var loc = LocaleToggle.Instance;
            var m = currentScorecard.metrics;
            var b = currentScorecard.doctrineBenchmarks;

            metricsTable.Clear();

            // Row 1: Time-to-Detection
            bool ttdPass = m.timeToDetectionMs <= (b != null ? b.timeToDetectionThresholdMs : 15000);
            AddMetricRow(
                name: loc != null ? loc.GetString("METRIC_TTD") : "Time-to-Detection",
                val: string.Format("{0:F1}s ({1} ms)", m.timeToDetectionMs / 1000.0f, m.timeToDetectionMs),
                benchmark: string.Format("≤ {0:F0}s", (b != null ? b.timeToDetectionThresholdMs : 15000) / 1000.0f),
                verdict: ttdPass ? "✓ PASS" : "✗ FAIL",
                isPass: ttdPass,
                isNeutral: false
            );

            // Row 2: ROE Compliance
            bool roePass = m.roeCompliance >= (b != null ? b.roeMinCompliance : 1.0f);
            AddMetricRow(
                name: loc != null ? loc.GetString("METRIC_ROE") : "ROE Compliance",
                val: string.Format("{0:P0}", m.roeCompliance),
                benchmark: "100% (Clean)",
                verdict: roePass ? "✓ PASS" : "✗ VIOLATION",
                isPass: roePass,
                isNeutral: false
            );

            // Row 3: Kinetic Efficiency
            float minEff = b != null ? b.kineticMinEfficiency : 0.5f;
            bool effPass = m.kineticEfficiency >= minEff;
            AddMetricRow(
                name: loc != null ? loc.GetString("METRIC_KINETIC_EFF") : "Kinetic Efficiency",
                val: string.Format("{0:P0} ({1}/{2})", m.kineticEfficiency, m.kineticHits, m.kineticRounds),
                benchmark: string.Format("≥ {0:P0}", minEff),
                verdict: effPass ? "✓ PASS" : "✗ FAIL",
                isPass: effPass,
                isNeutral: false
            );

            // Row 4: Agents Neutralised
            int totalAttackers = m.agentsNeutralised + m.agentsMissed;
            bool allNeut = m.agentsMissed == 0;
            AddMetricRow(
                name: loc != null ? loc.GetString("METRIC_AGENTS_NEUT") : "Agents Neutralised",
                val: string.Format("{0} / {1}", m.agentsNeutralised, totalAttackers),
                benchmark: "All Attackers",
                verdict: allNeut ? "✓ 100%" : string.Format("{0} Missed", m.agentsMissed),
                isPass: allNeut,
                isNeutral: false
            );

            // Row 5: Premature EW Penalty
            bool ewPenalty = m.prematureEwPenalty;
            AddMetricRow(
                name: loc != null ? loc.GetString("METRIC_PREMATURE_EW") : "Premature EW Penalty",
                val: ewPenalty ? (isHindi ? "हाँ (दंड)" : "YES (Penalty)") : (isHindi ? "नहीं (स्पष्ट)" : "NO (Clean)"),
                benchmark: isHindi ? "कोई नहीं" : "None",
                verdict: !ewPenalty ? "✓ PASS" : "✗ PENALTY",
                isPass: !ewPenalty,
                isNeutral: false
            );

            // Row 6: Classification Accuracy (v1 placeholder)
            AddMetricRow(
                name: loc != null ? loc.GetString("METRIC_CLASSIFICATION") : "Classification Accuracy",
                val: "N/A (v1)",
                benchmark: "—",
                verdict: "INFO",
                isPass: false,
                isNeutral: true
            );
        }

        private void AddMetricRow(string name, string val, string benchmark, string verdict, bool isPass, bool isNeutral)
        {
            var row = new VisualElement();
            row.AddToClassList("metric-row");

            var nameLabel = new Label(name);
            nameLabel.AddToClassList("metric-col-name");
            row.Add(nameLabel);

            var valLabel = new Label(val);
            valLabel.AddToClassList("metric-col-val");
            row.Add(valLabel);

            var benchLabel = new Label(benchmark);
            benchLabel.AddToClassList("metric-col-bench");
            row.Add(benchLabel);

            var verdictLabel = new Label(verdict);
            verdictLabel.AddToClassList("metric-col-verdict");
            if (isNeutral)
            {
                verdictLabel.AddToClassList("metric-info");
            }
            else if (isPass)
            {
                verdictLabel.AddToClassList("metric-pass");
            }
            else
            {
                verdictLabel.AddToClassList("metric-fail");
            }
            row.Add(verdictLabel);

            metricsTable.Add(row);
        }

        // ══════════════════════════════════════════════════════
        // Timeline Scrubber & Spatial Radar Overlay
        // ══════════════════════════════════════════════════════

        private void OnTimelineSliderChanged(ChangeEvent<float> evt)
        {
            if (evt == null) return;
            int targetMs = Mathf.RoundToInt(evt.newValue * 1000f);
            SetScrubPosition(targetMs);
        }

        public void SetScrubPosition(int targetMs)
        {
            currentScrubMs = targetMs;

            if (timelineTimeLabel != null)
            {
                timelineTimeLabel.text = string.Format("T+{0}s", (targetMs / 1000));
            }

            UpdateSpatialOverlay(targetMs);
        }

        public void UpdateSpatialOverlay(int targetMs)
        {
            var agents = ScorecardMock.GetSimulatedSwarmAtTime(targetMs);
            RenderSpatialAgents(agents);
        }

        public void RenderSpatialAgents(List<SwarmAgentStateDto> agents)
        {
            if (blipContainer == null) return;
            blipContainer.Clear();
            activeBlips.Clear();

            if (agents == null) return;

            // Spatial overlay dimensions: relative percentage from center (50%, 50%)
            // Range scale: +/- 600m mapped to 0% - 100%
            const float maxRangeM = 600f;

            foreach (var agent in agents)
            {
                if (agent.positionWorld == null) continue;

                var blip = new VisualElement();
                blip.AddToClassList("drone-blip");

                if (agent.isNeutralised)
                {
                    blip.AddToClassList("dot-neutralised");
                }
                else if (agent.isDecoy)
                {
                    blip.AddToClassList("dot-decoy");
                }
                else
                {
                    blip.AddToClassList("dot-attacker");
                }

                // Project X/Z world coords into screen percentage
                float normX = 50f + (agent.positionWorld.x / maxRangeM) * 50f;
                float normZ = 50f - (agent.positionWorld.z / maxRangeM) * 50f;

                blip.style.left = new Length(Mathf.Clamp(normX, 2f, 98f), LengthUnit.Percent);
                blip.style.top = new Length(Mathf.Clamp(normZ, 2f, 98f), LengthUnit.Percent);

                blipContainer.Add(blip);
                activeBlips.Add(blip);
            }
        }

        // ══════════════════════════════════════════════════════
        // Localization Event
        // ══════════════════════════════════════════════════════

        private void OnLocaleChanged(TrishulLocale newLocale)
        {
            RefreshView();
        }

        // ══════════════════════════════════════════════════════
        // Action Handlers
        // ══════════════════════════════════════════════════════

        public void OnPdfExportClicked()
        {
            if (currentScorecard == null)
            {
                Debug.LogWarning("[Scorecard] Cannot export PDF — no scorecard loaded.");
                return;
            }

            Debug.Log(string.Format("[Scorecard] PDF Export requested for session: {0}. (Wired in UX-P2-A)", currentScorecard.sessionId));
        }
    }
}
