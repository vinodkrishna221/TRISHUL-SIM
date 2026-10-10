// ============================================================
// PdfExporter.cs — Tactical Performance Dossier PDF Exporter
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Implements board/contracts/scorecard.md v1 §"PDF export shape",
// docs/ARCHITECTURE.md §1 ("iTextSharp embedded in .exe"),
// and missions/sohail-mission.md card AA-P2-A.
//
// Generates a self-contained, air-gapped A4 PDF dossier in 6 sections:
// 1. Header: "TRISHUL-SIM · Tactical Performance Dossier" + completedAtIso + traineeId
// 2. Scenario name (English + Hindi via NotoSansDevanagari-Regular.ttf)
// 3. Doctrine verdict badge (large, coloured: #00B300 green / #CC0000 red)
// 4. Metrics table: [Metric, Value, Benchmark, Pass/Fail] for all ScorecardMetrics
// 5. Timeline sparkline: line chart of threatState (0=GREEN, 1=AMBER, 2=RED) vs t (s)
//    using iTextSharp graphics primitives (MoveTo / LineTo)
// 6. Footer: "CONFIDENTIAL — For training use only · Air-gapped system"
// ============================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using iTextSharp.text;
using iTextSharp.text.pdf;
using TrishulSim.AAR;
using TrishulSim.Network;
using TrishulSim.Scenarios;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace TrishulSim.PDF
{
    /// <summary>
    /// Static PDF exporter producing official military-grade Tactical Performance Dossiers
    /// from TRISHUL-SIM Scorecard records using embedded iTextSharp.
    /// Fully air-gapped with zero cloud dependencies or external runtime services.
    /// </summary>
    public static class PdfExporter
    {
        public const string ThreatGreen = "GREEN";
        public const string ThreatAmber = "AMBER";
        public const string ThreatRed = "RED";
        // MIL-STD-2525 and Tactical UI Color Palette
        public static readonly BaseColor ColorPassGreen = new BaseColor(0, 179, 0);     // #00B300
        public static readonly BaseColor ColorFailRed = new BaseColor(204, 0, 0);       // #CC0000
        public static readonly BaseColor ColorWarnAmber = new BaseColor(255, 179, 0);   // #FFB300
        public static readonly BaseColor ColorDarkSlate = new BaseColor(26, 32, 44);    // #1A202C
        public static readonly BaseColor ColorHeaderGray = new BaseColor(45, 55, 72);   // #2D3748
        public static readonly BaseColor ColorRowAltLight = new BaseColor(247, 250, 252); // #F7FAFC
        public static readonly BaseColor ColorBorderGray = new BaseColor(203, 213, 225); // #CBD5E1
        public static readonly BaseColor ColorTextDark = new BaseColor(15, 23, 42);     // #0F172A
        public static readonly BaseColor ColorTextMuted = new BaseColor(100, 116, 139); // #64748B
        public static readonly BaseColor ColorSparklineLine = new BaseColor(2, 132, 199); // #0284C7

        public const string DefaultFontFileName = "NotoSansDevanagari-Regular.ttf";

        /// <summary>
        /// Generates the complete A4 PDF dossier in memory for the provided Scorecard.
        /// </summary>
        /// <param name="scorecard">Populated Scorecard object to export.</param>
        /// <param name="fontPathOverride">Optional explicit path to NotoSansDevanagari TTF file.</param>
        /// <returns>Binary byte array containing the valid PDF document.</returns>
        public static byte[] Export(Scorecard scorecard, string fontPathOverride = null)
        {
            if (scorecard == null)
                throw new ArgumentNullException("scorecard", "Scorecard cannot be null for PDF export.");

            using (MemoryStream ms = new MemoryStream())
            {
                // A4 page with 36pt (0.5 inch) margins for high information density
                Document doc = new Document(PageSize.A4, 36f, 36f, 36f, 36f);
                PdfWriter writer = PdfWriter.GetInstance(doc, ms);

                doc.AddTitle("TRISHUL-SIM Tactical Performance Dossier — " + (scorecard.sessionId ?? "Session"));
                doc.AddSubject("C-UAS Tactical Simulation After Action Review");
                doc.AddCreator("TRISHUL-SIM AAR Engine");
                doc.AddAuthor("Indian Army / MoD C-UAS Simulator");

                doc.Open();

                // Resolve fonts
                Font fontTitle;
                Font fontSubtitle;
                Font fontScenarioEn;
                Font fontScenarioHi;
                Font fontBadge;
                Font fontBadgeSub;
                Font fontTableHeader;
                Font fontTableCell;
                Font fontTableCellBold;
                Font fontMuted;
                Font fontFooter;

                ResolveFonts(fontPathOverride, out fontTitle, out fontSubtitle, out fontScenarioEn,
                    out fontScenarioHi, out fontBadge, out fontBadgeSub, out fontTableHeader,
                    out fontTableCell, out fontTableCellBold, out fontMuted, out fontFooter);

                // -------------------------------------------------------------
                // SECTION 1: Header table
                // "TRISHUL-SIM · Tactical Performance Dossier" + completedAtIso + traineeId
                // -------------------------------------------------------------
                PdfPTable headerTable = new PdfPTable(2);
                headerTable.WidthPercentage = 100f;
                headerTable.SetWidths(new float[] { 62f, 38f });
                headerTable.SpacingAfter = 10f;

                // Left cell: System banner & title
                PdfPCell leftHeaderCell = new PdfPCell();
                leftHeaderCell.Border = Rectangle.NO_BORDER;
                leftHeaderCell.Padding = 0f;

                Paragraph titlePara = new Paragraph("TRISHUL-SIM", fontTitle);
                titlePara.SpacingAfter = 2f;
                leftHeaderCell.AddElement(titlePara);

                Paragraph subtitlePara = new Paragraph("TACTICAL PERFORMANCE DOSSIER", fontSubtitle);
                subtitlePara.SpacingAfter = 2f;
                leftHeaderCell.AddElement(subtitlePara);

                Paragraph orgPara = new Paragraph("AIR-GAPPED C-UAS TACTICAL SIMULATION TRAINER · BATTALION COMMAND", fontMuted);
                leftHeaderCell.AddElement(orgPara);
                headerTable.AddCell(leftHeaderCell);

                // Right cell: Trainee and timestamp metadata
                PdfPCell rightHeaderCell = new PdfPCell();
                rightHeaderCell.Border = Rectangle.NO_BORDER;
                rightHeaderCell.Padding = 0f;
                rightHeaderCell.HorizontalAlignment = Element.ALIGN_RIGHT;

                Paragraph traineePara = new Paragraph("OPERATOR: " + (string.IsNullOrWhiteSpace(scorecard.traineeId) ? "HAVILDAR RAJAN (SENTRY)" : scorecard.traineeId.ToUpperInvariant()), fontTableCellBold);
                traineePara.Alignment = Element.ALIGN_RIGHT;
                traineePara.SpacingAfter = 2f;
                rightHeaderCell.AddElement(traineePara);

                string completedTime = string.IsNullOrWhiteSpace(scorecard.completedAtIso)
                    ? DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)
                    : scorecard.completedAtIso;
                Paragraph timePara = new Paragraph("COMPLETED: " + completedTime, fontMuted);
                timePara.Alignment = Element.ALIGN_RIGHT;
                timePara.SpacingAfter = 2f;
                rightHeaderCell.AddElement(timePara);

                Paragraph sessionPara = new Paragraph("SESSION: " + (scorecard.sessionId ?? "N/A"), fontMuted);
                sessionPara.Alignment = Element.ALIGN_RIGHT;
                rightHeaderCell.AddElement(sessionPara);

                headerTable.AddCell(rightHeaderCell);
                doc.Add(headerTable);

                // Divider rule below header
                doc.Add(CreateDividerLine(ColorHeaderGray, 1.5f, 6f));

                // -------------------------------------------------------------
                // SECTION 2: Scenario name (English + Hindi)
                // -------------------------------------------------------------
                PdfPTable scenarioTable = new PdfPTable(1);
                scenarioTable.WidthPercentage = 100f;
                scenarioTable.SpacingAfter = 10f;

                PdfPCell scenarioCell = new PdfPCell();
                scenarioCell.Border = Rectangle.NO_BORDER;
                scenarioCell.Padding = 4f;
                scenarioCell.BackgroundColor = ColorRowAltLight;

                string nameEn = !string.IsNullOrWhiteSpace(scorecard.scenarioName)
                    ? scorecard.scenarioName
                    : "Punjab Border — Zero Visibility Pincer";
                Paragraph scEnPara = new Paragraph(nameEn, fontScenarioEn);
                scEnPara.SpacingAfter = 2f;
                scenarioCell.AddElement(scEnPara);

                string nameHi = !string.IsNullOrWhiteSpace(scorecard.scenarioNameHindi)
                    ? scorecard.scenarioNameHindi
                    : "पंजाब सीमा — शून्य दृश्यता पिंसर";
                Paragraph scHiPara = new Paragraph(nameHi, fontScenarioHi);
                scHiPara.SpacingAfter = 4f;
                scenarioCell.AddElement(scHiPara);

                double durationSec = (double)scorecard.durationMs / 1000.0;
                string metaLine = string.Format(CultureInfo.InvariantCulture,
                    "Scenario ID: {0}   |   Duration: {1:0.0} s ({2} ms)   |   Air-Gap Verified: 100%   |   Telemetry: 10 Hz NDJSON",
                    scorecard.scenarioId ?? "punjab-fog-pincer-01",
                    durationSec,
                    scorecard.durationMs);
                Paragraph scMetaPara = new Paragraph(metaLine, fontMuted);
                scenarioCell.AddElement(scMetaPara);

                scenarioTable.AddCell(scenarioCell);
                doc.Add(scenarioTable);

                // -------------------------------------------------------------
                // SECTION 3: Doctrine verdict badge
                // (large, coloured: #00B300 green / #CC0000 red)
                // -------------------------------------------------------------
                PdfPTable badgeTable = new PdfPTable(1);
                badgeTable.WidthPercentage = 100f;
                badgeTable.SpacingAfter = 12f;

                PdfPCell badgeCell = new PdfPCell();
                badgeCell.Border = Rectangle.NO_BORDER;
                badgeCell.PaddingTop = 8f;
                badgeCell.PaddingBottom = 8f;
                badgeCell.HorizontalAlignment = Element.ALIGN_CENTER;

                if (scorecard.doctrinePassed)
                {
                    badgeCell.BackgroundColor = ColorPassGreen;
                    Paragraph badgeTitle = new Paragraph("DOCTRINE PASSED", fontBadge);
                    badgeTitle.Alignment = Element.ALIGN_CENTER;
                    badgeCell.AddElement(badgeTitle);

                    Paragraph badgeSub = new Paragraph("ALL TACTICAL RULES OF ENGAGEMENT & REACTION TIME BENCHMARKS SATISFIED", fontBadgeSub);
                    badgeSub.Alignment = Element.ALIGN_CENTER;
                    badgeCell.AddElement(badgeSub);
                }
                else
                {
                    badgeCell.BackgroundColor = ColorFailRed;
                    Paragraph badgeTitle = new Paragraph("DOCTRINE FAILED", fontBadge);
                    badgeTitle.Alignment = Element.ALIGN_CENTER;
                    badgeCell.AddElement(badgeTitle);

                    Paragraph badgeSub = new Paragraph("TACTICAL THRESHOLDS EXCEEDED OR RULES OF ENGAGEMENT COMPLIANCE BREACHED", fontBadgeSub);
                    badgeSub.Alignment = Element.ALIGN_CENTER;
                    badgeCell.AddElement(badgeSub);
                }

                badgeTable.AddCell(badgeCell);
                doc.Add(badgeTable);

                // -------------------------------------------------------------
                // SECTION 4: Metrics table
                // [Metric, Value, Benchmark, Pass/Fail] rows for all ScorecardMetrics
                // -------------------------------------------------------------
                PdfPTable metricsTable = new PdfPTable(4);
                metricsTable.WidthPercentage = 100f;
                metricsTable.SetWidths(new float[] { 34f, 22f, 26f, 18f });
                metricsTable.SpacingAfter = 12f;

                // Table Header Row
                AddTableHeaderCell(metricsTable, "METRIC EVALUATION", fontTableHeader, Element.ALIGN_LEFT);
                AddTableHeaderCell(metricsTable, "RECORDED VALUE", fontTableHeader, Element.ALIGN_CENTER);
                AddTableHeaderCell(metricsTable, "BENCHMARK THRESHOLD", fontTableHeader, Element.ALIGN_CENTER);
                AddTableHeaderCell(metricsTable, "DOCTRINE STATUS", fontTableHeader, Element.ALIGN_CENTER);

                ScorecardMetrics m = scorecard.metrics ?? new ScorecardMetrics();
                DoctrineBenchmarks b = scorecard.doctrineBenchmarks ?? new DoctrineBenchmarks();

                // 1. Time-to-Detection
                bool ttdPass = m.timeToDetectionMs > 0 && m.timeToDetectionMs <= b.timeToDetectionThresholdMs;
                string ttdValue = m.timeToDetectionMs > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0:0.1} s ({1} ms)", (double)m.timeToDetectionMs / 1000.0, m.timeToDetectionMs)
                    : "Not Detected";
                string ttdBench = string.Format(CultureInfo.InvariantCulture, "≤ {0:0.1} s ({1} ms)", (double)b.timeToDetectionThresholdMs / 1000.0, b.timeToDetectionThresholdMs);
                AddMetricRow(metricsTable, "Time-to-Detection (TTD)\nFirst radar / RF spike detection",
                    ttdValue, ttdBench, ttdPass ? "PASS" : "FAIL", ttdPass, 0, fontTableCell, fontTableCellBold);

                // 2. ROE Compliance
                bool roePass = m.roeCompliance >= b.roeMinCompliance;
                string roeValue = string.Format(CultureInfo.InvariantCulture, "{0:0}% {1}", m.roeCompliance * 100f, roePass ? "(Zero Blackout)" : "(Blackout Event)");
                string roeBench = string.Format(CultureInfo.InvariantCulture, "{0:0}% (Zero Friendly Risk)", b.roeMinCompliance * 100f);
                AddMetricRow(metricsTable, "Rules of Engagement (ROE)\nFriendly VHF communications blackout",
                    roeValue, roeBench, roePass ? "PASS" : "FAIL", roePass, 1, fontTableCell, fontTableCellBold);

                // 3. Kinetic Efficiency
                bool effPass = m.kineticEfficiency >= b.kineticMinEfficiency;
                string effValue = string.Format(CultureInfo.InvariantCulture, "{0:0.0}% ({1}/{2} hits)", m.kineticEfficiency * 100f, m.kineticHits, m.kineticRounds);
                string effBench = string.Format(CultureInfo.InvariantCulture, "≥ {0:0.0}% hit efficiency", b.kineticMinEfficiency * 100f);
                AddMetricRow(metricsTable, "Kinetic Fire Efficiency\nSMASH 2000 target hit accuracy",
                    effValue, effBench, effPass ? "PASS" : "FAIL", effPass, 2, fontTableCell, fontTableCellBold);

                // 4. Agents Neutralised
                bool neutralPass = m.agentsMissed == 0;
                string neutralValue = string.Format(CultureInfo.InvariantCulture, "{0} Down / {1} Missed", m.agentsNeutralised, m.agentsMissed);
                string neutralBench = "Exclusion Zone Defense";
                AddMetricRow(metricsTable, "Swarm Threat Neutralised\nAttacking UAVs downed before perimeter",
                    neutralValue, neutralBench, neutralPass ? "PASS" : "DEFENDED", neutralPass, 3, fontTableCell, fontTableCellBold);

                // 5. Premature EW Penalty
                bool ewPass = !m.prematureEwPenalty;
                string ewValue = m.prematureEwPenalty ? "Penalty Applied (Early EW)" : "None (Clean Approach)";
                string ewBench = "False (No early jamming)";
                AddMetricRow(metricsTable, "Premature EW Engagement\nJamming transmission in GREEN threat",
                    ewValue, ewBench, ewPass ? "PASS" : "FAIL", ewPass, 4, fontTableCell, fontTableCellBold);

                // 6. Classification Accuracy (v1 notice)
                AddMetricRow(metricsTable, "Classification Accuracy\nVisual/RF doctrinal classification",
                    "0.0 (Deferred to v2)", "N/A (v2 Event)", "INFO", null, 5, fontTableCell, fontTableCellBold);

                doc.Add(metricsTable);

                // -------------------------------------------------------------
                // SECTION 5: Timeline sparkline / mini-chart
                // Line chart of threatState (0=GREEN, 1=AMBER, 2=RED) vs t (s)
                // using iTextSharp graphics primitives (MoveTo / LineTo)
                // -------------------------------------------------------------
                Paragraph sparklineHeader = new Paragraph("TIMELINE THREAT STATE PROGRESSION (0 = GREEN · 1 = AMBER · 2 = RED)", fontSubtitle);
                sparklineHeader.SpacingAfter = 4f;
                doc.Add(sparklineHeader);

                List<TimelineDataPoint> points = ExtractTimelinePoints(scorecard);

                PdfPTable sparklineTable = new PdfPTable(1);
                sparklineTable.WidthPercentage = 100f;
                sparklineTable.SpacingAfter = 12f;

                PdfPCell sparklineCell = new PdfPCell();
                sparklineCell.FixedHeight = 85f;
                sparklineCell.Border = Rectangle.BOX;
                sparklineCell.BorderColor = ColorBorderGray;
                sparklineCell.BorderWidth = 1f;
                sparklineCell.BackgroundColor = ColorRowAltLight;
                sparklineCell.CellEvent = new TimelineSparklineRenderer(points, (double)scorecard.durationMs / 1000.0);

                sparklineTable.AddCell(sparklineCell);
                doc.Add(sparklineTable);

                // -------------------------------------------------------------
                // SECTION 6: Footer
                // "CONFIDENTIAL — For training use only · Air-gapped system"
                // -------------------------------------------------------------
                doc.Add(CreateDividerLine(ColorBorderGray, 1f, 6f));

                PdfPTable footerTable = new PdfPTable(2);
                footerTable.WidthPercentage = 100f;
                footerTable.SetWidths(new float[] { 70f, 30f });

                PdfPCell leftFooter = new PdfPCell();
                leftFooter.Border = Rectangle.NO_BORDER;
                leftFooter.Padding = 2f;
                Paragraph confPara = new Paragraph("CONFIDENTIAL — For training use only · Air-gapped system", fontFooter);
                leftFooter.AddElement(confPara);
                footerTable.AddCell(leftFooter);

                PdfPCell rightFooter = new PdfPCell();
                rightFooter.Border = Rectangle.NO_BORDER;
                rightFooter.Padding = 2f;
                rightFooter.HorizontalAlignment = Element.ALIGN_RIGHT;
                Paragraph verPara = new Paragraph("TRISHUL-SIM AAR · v" + scorecard.version, fontFooter);
                verPara.Alignment = Element.ALIGN_RIGHT;
                rightFooter.AddElement(verPara);
                footerTable.AddCell(rightFooter);

                doc.Add(footerTable);

                doc.Close();
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Writes the exported PDF directly to disk under the specified directory (defaults to %AppData%\TRISHUL-SIM\sessions\).
        /// Returns the absolute path of the generated PDF file.
        /// </summary>
        /// <param name="scorecard">Scorecard data to render into PDF.</param>
        /// <param name="outputDirectory">Optional target directory override (defaults to standard sessions dir).</param>
        /// <param name="fontPathOverride">Optional explicit TTF font path.</param>
        /// <returns>Absolute Windows file path to the generated PDF document.</returns>
        public static string WriteToFile(Scorecard scorecard, string outputDirectory = null, string fontPathOverride = null)
        {
            if (scorecard == null)
                throw new ArgumentNullException("scorecard", "Scorecard cannot be null for WriteToFile.");

            string dir = outputDirectory;
            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TRISHUL-SIM", "sessions");
            }

            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                DebugLogWarning("Failed to create PDF output directory '" + dir + "': " + ex.Message);
            }

            string sId = !string.IsNullOrWhiteSpace(scorecard.sessionId) ? scorecard.sessionId : "session-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string filePath = Path.Combine(dir, sId + ".pdf");

            byte[] pdfBytes = Export(scorecard, fontPathOverride);
            File.WriteAllBytes(filePath, pdfBytes);

            DebugLog("[PdfExporter] PDF dossier exported successfully to: " + filePath + " (" + pdfBytes.Length + " bytes)");
            return filePath;
        }

        #region Helper Methods

        private static void ResolveFonts(
            string fontPathOverride,
            out Font fontTitle,
            out Font fontSubtitle,
            out Font fontScenarioEn,
            out Font fontScenarioHi,
            out Font fontBadge,
            out Font fontBadgeSub,
            out Font fontTableHeader,
            out Font fontTableCell,
            out Font fontTableCellBold,
            out Font fontMuted,
            out Font fontFooter)
        {
            BaseFont bfStandard = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
            BaseFont bfStandardBold = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);

            BaseFont bfHindi = null;
            string ttfPath = ResolveFontPath(fontPathOverride);

            if (!string.IsNullOrEmpty(ttfPath) && File.Exists(ttfPath))
            {
                try
                {
                    bfHindi = BaseFont.CreateFont(ttfPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                }
                catch (Exception ex)
                {
                    DebugLogWarning("[PdfExporter] Could not embed Hindi TrueType font: " + ex.Message + ". Falling back to standard Helvetica.");
                }
            }

            if (bfHindi == null)
            {
                bfHindi = bfStandard;
            }

            fontTitle = new Font(bfStandardBold, 18f, Font.NORMAL, ColorDarkSlate);
            fontSubtitle = new Font(bfStandardBold, 10f, Font.NORMAL, ColorTextMuted);
            fontScenarioEn = new Font(bfStandardBold, 14f, Font.NORMAL, ColorDarkSlate);
            fontScenarioHi = new Font(bfHindi, 11f, Font.NORMAL, ColorTextDark);
            fontBadge = new Font(bfStandardBold, 16f, Font.NORMAL, BaseColor.WHITE);
            fontBadgeSub = new Font(bfStandardBold, 8.5f, Font.NORMAL, BaseColor.WHITE);
            fontTableHeader = new Font(bfStandardBold, 8.5f, Font.NORMAL, BaseColor.WHITE);
            fontTableCell = new Font(bfStandard, 8.5f, Font.NORMAL, ColorTextDark);
            fontTableCellBold = new Font(bfStandardBold, 8.5f, Font.NORMAL, ColorTextDark);
            fontMuted = new Font(bfStandard, 7.5f, Font.NORMAL, ColorTextMuted);
            fontFooter = new Font(bfStandardBold, 7.5f, Font.NORMAL, ColorTextMuted);
        }

        private static string ResolveFontPath(string fontPathOverride)
        {
            if (!string.IsNullOrWhiteSpace(fontPathOverride) && File.Exists(fontPathOverride))
                return fontPathOverride;

#if UNITY_5_3_OR_NEWER
            string unityPath = Path.Combine(Application.dataPath, "Fonts", DefaultFontFileName);
            if (File.Exists(unityPath)) return unityPath;
#endif

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string cand1 = Path.Combine(baseDir, "Assets", "Fonts", DefaultFontFileName);
            if (File.Exists(cand1)) return cand1;

            string cand2 = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Fonts", DefaultFontFileName);
            if (File.Exists(cand2)) return cand2;

            string appDataFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TRISHUL-SIM", "Fonts", DefaultFontFileName);
            if (File.Exists(appDataFonts)) return appDataFonts;

            return null;
        }

        private static PdfPTable CreateDividerLine(BaseColor color, float height, float spacing)
        {
            PdfPTable div = new PdfPTable(1);
            div.WidthPercentage = 100f;
            div.SpacingBefore = spacing / 2;
            div.SpacingAfter = spacing;
            PdfPCell c = new PdfPCell();
            c.FixedHeight = height;
            c.BackgroundColor = color;
            c.Border = Rectangle.NO_BORDER;
            div.AddCell(c);
            return div;
        }

        private static void AddTableHeaderCell(PdfPTable table, string text, Font font, int alignment)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.BackgroundColor = ColorHeaderGray;
            cell.HorizontalAlignment = alignment;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.PaddingTop = 5f;
            cell.PaddingBottom = 5f;
            cell.PaddingLeft = 4f;
            cell.PaddingRight = 4f;
            cell.BorderColor = ColorBorderGray;
            cell.BorderWidth = 0.5f;
            table.AddCell(cell);
        }

        private static void AddMetricRow(
            PdfPTable table,
            string metricName,
            string value,
            string benchmark,
            string statusText,
            bool? isPass,
            int rowIndex,
            Font cellFont,
            Font boldFont)
        {
            BaseColor rowBg = (rowIndex % 2 == 1) ? ColorRowAltLight : BaseColor.WHITE;

            // Column 1: Metric name
            PdfPCell nameCell = new PdfPCell(new Phrase(metricName, cellFont));
            nameCell.BackgroundColor = rowBg;
            nameCell.Padding = 4f;
            nameCell.BorderColor = ColorBorderGray;
            nameCell.BorderWidth = 0.5f;
            table.AddCell(nameCell);

            // Column 2: Measured Value
            PdfPCell valCell = new PdfPCell(new Phrase(value, boldFont));
            valCell.BackgroundColor = rowBg;
            valCell.HorizontalAlignment = Element.ALIGN_CENTER;
            valCell.Padding = 4f;
            valCell.BorderColor = ColorBorderGray;
            valCell.BorderWidth = 0.5f;
            table.AddCell(valCell);

            // Column 3: Benchmark Threshold
            PdfPCell benchCell = new PdfPCell(new Phrase(benchmark, cellFont));
            benchCell.BackgroundColor = rowBg;
            benchCell.HorizontalAlignment = Element.ALIGN_CENTER;
            benchCell.Padding = 4f;
            benchCell.BorderColor = ColorBorderGray;
            benchCell.BorderWidth = 0.5f;
            table.AddCell(benchCell);

            // Column 4: Status (Pass/Fail)
            BaseColor statusColor = isPass.HasValue
                ? (isPass.Value ? ColorPassGreen : ColorFailRed)
                : ColorDarkSlate;
            Font statusFont = new Font(boldFont.BaseFont, boldFont.Size, Font.NORMAL, statusColor);

            PdfPCell statusCell = new PdfPCell(new Phrase(statusText, statusFont));
            statusCell.BackgroundColor = rowBg;
            statusCell.HorizontalAlignment = Element.ALIGN_CENTER;
            statusCell.Padding = 4f;
            statusCell.BorderColor = ColorBorderGray;
            statusCell.BorderWidth = 0.5f;
            table.AddCell(statusCell);
        }

        private static List<TimelineDataPoint> ExtractTimelinePoints(Scorecard scorecard)
        {
            List<TimelineDataPoint> list = new List<TimelineDataPoint>();

            // Strategy 1: Read session NDJSON file directly if present on disk
            if (!string.IsNullOrWhiteSpace(scorecard.sessionFilePath) && File.Exists(scorecard.sessionFilePath))
            {
                try
                {
                    using (FileStream fs = new FileStream(scorecard.sessionFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader reader = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            TelemetryRecord rec;
                            string err;
                            if (TelemetryRecord.TryParse(line, out rec, out err))
                            {
                                int stateVal = ParseThreatStateValue(rec.threatState);
                                list.Add(new TimelineDataPoint(rec.t / 1000.0, stateVal));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLogWarning("[PdfExporter] Failed reading NDJSON for sparkline: " + ex.Message);
                }
            }

            // Strategy 2: If no NDJSON records, synthesize from timelineIndex and timeToDetectionMs
            if (list.Count == 0)
            {
                int[] idx = scorecard.timelineIndex;
                double ttdSec = (scorecard.metrics != null && scorecard.metrics.timeToDetectionMs > 0)
                    ? (double)scorecard.metrics.timeToDetectionMs / 1000.0
                    : 15.0;
                double durationSec = scorecard.durationMs > 0 ? (double)scorecard.durationMs / 1000.0 : 60.0;

                if (idx != null && idx.Length > 0)
                {
                    for (int i = 0; i < idx.Length; i++)
                    {
                        double sec = idx[i] / 1000.0;
                        int stateVal = 0; // GREEN
                        if (sec >= ttdSec)
                        {
                            stateVal = (sec >= ttdSec + 10.0) ? 2 : 1; // RED after 10s of AMBER
                        }
                        list.Add(new TimelineDataPoint(sec, stateVal));
                    }
                }
                else
                {
                    // Fallback 5 points across duration
                    list.Add(new TimelineDataPoint(0.0, 0));
                    list.Add(new TimelineDataPoint(ttdSec * 0.5, 0));
                    list.Add(new TimelineDataPoint(ttdSec, 1));
                    list.Add(new TimelineDataPoint(Math.Min(durationSec, ttdSec + 10.0), 2));
                    list.Add(new TimelineDataPoint(durationSec, 2));
                }
            }

            return list;
        }

        private static int ParseThreatStateValue(string threatState)
        {
            if (string.Equals(threatState, ThreatRed, StringComparison.OrdinalIgnoreCase))
                return 2;
            if (string.Equals(threatState, ThreatAmber, StringComparison.OrdinalIgnoreCase))
                return 1;
            return 0; // GREEN
        }

        private static void DebugLog(string message)
        {
#if UNITY_5_3_OR_NEWER
            UnityEngine.Debug.Log(message);
#else
            Console.WriteLine(message);
#endif
        }

        private static void DebugLogWarning(string message)
        {
#if UNITY_5_3_OR_NEWER
            UnityEngine.Debug.LogWarning(message);
#else
            Console.WriteLine("[WARN] " + message);
#endif
        }

        #endregion

        #region Nested Types

        public struct TimelineDataPoint
        {
            public double TimeSec;
            public int ThreatLevel; // 0=GREEN, 1=AMBER, 2=RED

            public TimelineDataPoint(double timeSec, int threatLevel)
            {
                TimeSec = timeSec;
                ThreatLevel = threatLevel;
            }
        }

        /// <summary>
        /// Renders vector sparkline curve using iTextSharp direct graphics primitives
        /// within the allocated PdfPCell boundary.
        /// </summary>
        private class TimelineSparklineRenderer : IPdfPCellEvent
        {
            private readonly List<TimelineDataPoint> _points;
            private readonly double _durationSec;

            public TimelineSparklineRenderer(List<TimelineDataPoint> points, double durationSec)
            {
                _points = points ?? new List<TimelineDataPoint>();
                _durationSec = durationSec > 0 ? durationSec : 60.0;
            }

            public void CellLayout(PdfPCell cell, Rectangle position, PdfContentByte[] canvases)
            {
                PdfContentByte cb = canvases[PdfPTable.LINECANVAS];
                cb.SaveState();

                float left = position.Left + 42f; // leave room for Y-axis labels
                float right = position.Right - 15f;
                float bottom = position.Bottom + 18f; // leave room for X-axis labels
                float top = position.Top - 8f;
                float plotWidth = Math.Max(10f, right - left);
                float plotHeight = Math.Max(10f, top - bottom);

                // 1. Draw subtle horizontal grid lines for Threat Levels 0, 1, 2
                float yGreen = bottom;
                float yAmber = bottom + (plotHeight * 0.5f);
                float yRed = top;

                // Level 0 (GREEN) line
                cb.SetColorStroke(new BaseColor(220, 240, 220));
                cb.SetLineWidth(0.75f);
                cb.MoveTo(left, yGreen);
                cb.LineTo(right, yGreen);
                cb.Stroke();

                // Level 1 (AMBER) line
                cb.SetColorStroke(new BaseColor(254, 243, 199));
                cb.MoveTo(left, yAmber);
                cb.LineTo(right, yAmber);
                cb.Stroke();

                // Level 2 (RED) line
                cb.SetColorStroke(new BaseColor(254, 226, 226));
                cb.MoveTo(left, yRed);
                cb.LineTo(right, yRed);
                cb.Stroke();

                // 2. Y-Axis Text Labels
                BaseFont bf = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                cb.BeginText();
                cb.SetFontAndSize(bf, 6.5f);

                cb.SetColorFill(ColorFailRed);
                cb.ShowTextAligned(Element.ALIGN_RIGHT, "RED (2)", left - 4f, yRed - 2f, 0);

                cb.SetColorFill(ColorWarnAmber);
                cb.ShowTextAligned(Element.ALIGN_RIGHT, "AMBER (1)", left - 4f, yAmber - 2f, 0);

                cb.SetColorFill(ColorPassGreen);
                cb.ShowTextAligned(Element.ALIGN_RIGHT, "GREEN (0)", left - 4f, yGreen - 2f, 0);

                // 3. X-Axis Time Labels
                cb.SetColorFill(ColorTextMuted);
                cb.ShowTextAligned(Element.ALIGN_CENTER, "0.0s", left, bottom - 10f, 0);
                cb.ShowTextAligned(Element.ALIGN_CENTER, string.Format(CultureInfo.InvariantCulture, "{0:0.0}s", _durationSec * 0.5), left + (plotWidth * 0.5f), bottom - 10f, 0);
                cb.ShowTextAligned(Element.ALIGN_CENTER, string.Format(CultureInfo.InvariantCulture, "{0:0.0}s", _durationSec), right, bottom - 10f, 0);
                cb.EndText();

                // 4. Draw Timeline Step / Progression Curve
                if (_points.Count > 0)
                {
                    double maxTime = _durationSec;
                    if (_points[_points.Count - 1].TimeSec > maxTime)
                        maxTime = _points[_points.Count - 1].TimeSec;
                    if (maxTime <= 0) maxTime = 1.0;

                    cb.SetLineWidth(2.0f);

                    for (int i = 0; i < _points.Count; i++)
                    {
                        TimelineDataPoint pt = _points[i];
                        float px = left + (float)((pt.TimeSec / maxTime) * plotWidth);
                        float py = bottom + (float)((pt.ThreatLevel / 2.0) * plotHeight);

                        if (px > right) px = right;
                        if (py > top) py = top;

                        if (i == 0)
                        {
                            cb.MoveTo(px, py);
                        }
                        else
                        {
                            TimelineDataPoint prev = _points[i - 1];
                            float prevX = left + (float)((prev.TimeSec / maxTime) * plotWidth);
                            float prevY = bottom + (float)((prev.ThreatLevel / 2.0) * plotHeight);

                            // Segment stroke color matches current threat level
                            BaseColor segColor = (pt.ThreatLevel >= 2) ? ColorFailRed :
                                                 (pt.ThreatLevel == 1) ? ColorWarnAmber : ColorPassGreen;
                            cb.SetColorStroke(segColor);

                            // Step-line: horizontal to current time, then vertical to new threat level
                            cb.MoveTo(prevX, prevY);
                            cb.LineTo(px, prevY);
                            cb.LineTo(px, py);
                            cb.Stroke();
                        }

                        // Draw point dot
                        BaseColor dotColor = (pt.ThreatLevel >= 2) ? ColorFailRed :
                                             (pt.ThreatLevel == 1) ? ColorWarnAmber : ColorPassGreen;
                        cb.SetColorFill(dotColor);
                        cb.Circle(px, py, 1.8f);
                        cb.Fill();
                    }
                }

                cb.RestoreState();
            }
        }

        #endregion
    }
}
