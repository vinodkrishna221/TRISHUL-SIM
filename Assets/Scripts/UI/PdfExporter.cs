// ============================================================
// PdfExporter.cs — Tactical Performance Dossier PDF Generator
// TRISHUL-SIM · UX-P2-A · ui-ux track (Rajeswari)
//
// Implements client-side PDF dossier export matching:
// board/contracts/scorecard.md §"PDF export shape" (all 6 sections):
// 1. Header: "TRISHUL-SIM · Tactical Performance Dossier" + completedAtIso + traineeId
// 2. Scenario name (English + Hindi)
// 3. Doctrine verdict badge (large, coloured)
// 4. Metrics table (all ScorecardMetrics fields, value vs benchmark)
// 5. Timeline mini-chart (sparkline / summary of timelineIndex)
// 6. Footer: "CONFIDENTIAL — For training use only · Air-gapped system"
//
// Writes to %AppData%\TRISHUL-SIM\sessions\<sessionId>.pdf
// Zero external NuGet dependencies (valid pure-C# PDF 1.4 stream).
// ============================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace TrishulSim.UI
{
    public static class PdfExporter
    {
        /// <summary>
        /// Generates the PDF document as byte array.
        /// </summary>
        public static byte[] Export(Scorecard sc)
        {
            if (sc == null)
                throw new ArgumentNullException("sc", "Scorecard cannot be null for PDF export.");

            var sb = new StringBuilder();

            // ── Section 1: Header ───────────────────────────────────────────
            sb.AppendLine("BT /F2 16 Tf 50 740 Td (TRISHUL-SIM · Tactical Performance Dossier) Tj ET");
            sb.AppendLine("BT /F1 9 Tf 50 725 Td (Completed: " + EscapePdfString(sc.completedAtIso ?? DateTime.UtcNow.ToString("o")) + "   |   Trainee: " + EscapePdfString(string.IsNullOrEmpty(sc.traineeId) ? "Anonymous" : sc.traineeId) + ") Tj ET");
            
            // Header divider line
            sb.AppendLine("0.3 0.3 0.3 RG 1 w 50 715 m 562 715 l S");

            // ── Section 2: Scenario Name ────────────────────────────────────
            string scEn = sc.scenarioName ?? "Punjab Border — Zero Visibility Pincer";
            string scHi = sc.scenarioNameHindi ?? "Punjab Border (Hindi: Zero Visibility Pincer)";
            sb.AppendLine("BT /F2 12 Tf 50 695 Td (Scenario: " + EscapePdfString(scEn) + ") Tj ET");
            sb.AppendLine("BT /F1 9 Tf 50 682 Td (Localization: " + EscapePdfString(scHi) + ") Tj ET");

            // ── Section 3: Doctrine Verdict Badge ───────────────────────────
            if (sc.doctrinePassed)
            {
                // MIL-STD-2525 GREEN #00B300 -> RGB (0.0, 0.702, 0.0)
                sb.AppendLine("0.0 0.702 0.0 rg 50 645 160 26 re f");
                sb.AppendLine("BT /F2 11 Tf 1 1 1 rg 60 653 Td (DOCTRINE PASSED) Tj ET");
            }
            else
            {
                // MIL-STD-2525 RED #CC0000 -> RGB (0.8, 0.0, 0.0)
                sb.AppendLine("0.8 0.0 0.0 rg 50 645 160 26 re f");
                sb.AppendLine("BT /F2 11 Tf 1 1 1 rg 60 653 Td (DOCTRINE FAILED) Tj ET");
            }

            // ── Section 4: Metrics Table ────────────────────────────────────
            sb.AppendLine("0 0 0 rg");
            sb.AppendLine("BT /F2 11 Tf 50 625 Td (TACTICAL BENCHMARKS & ROE COMPLIANCE) Tj ET");

            // Table header box
            sb.AppendLine("0.15 0.15 0.15 rg 50 600 512 18 re f");
            sb.AppendLine("BT /F2 8 Tf 1 1 1 rg 55 605 Td (METRIC) Tj 220 605 Td (RECORDED) Tj 340 605 Td (BENCHMARK) Tj 460 605 Td (VERDICT) Tj ET");

            int rowY = 582;
            var m = sc.metrics ?? new ScorecardMetrics();
            var b = sc.doctrineBenchmarks ?? new DoctrineBenchmarks();

            // Metric 1: Time to detection
            bool ttdPass = m.timeToDetectionMs <= (b.timeToDetectionThresholdMs > 0 ? b.timeToDetectionThresholdMs : 15000);
            AppendTableRow(sb, ref rowY, "Time-to-Detection",
                string.Format(CultureInfo.InvariantCulture, "{0:F1}s ({1} ms)", m.timeToDetectionMs / 1000f, m.timeToDetectionMs),
                string.Format(CultureInfo.InvariantCulture, "<= {0:F0}s", (b.timeToDetectionThresholdMs > 0 ? b.timeToDetectionThresholdMs : 15000) / 1000f),
                ttdPass ? "PASS" : "FAIL", ttdPass);

            // Metric 2: ROE Compliance
            bool roePass = m.roeCompliance >= (b.roeMinCompliance > 0 ? b.roeMinCompliance : 1.0f);
            AppendTableRow(sb, ref rowY, "ROE Compliance",
                string.Format(CultureInfo.InvariantCulture, "{0:P0}", m.roeCompliance),
                "100% (Clean)",
                roePass ? "PASS" : "VIOLATION", roePass);

            // Metric 3: Kinetic Efficiency
            float minEff = b.kineticMinEfficiency > 0 ? b.kineticMinEfficiency : 0.5f;
            bool effPass = m.kineticEfficiency >= minEff;
            AppendTableRow(sb, ref rowY, "Kinetic Efficiency",
                string.Format(CultureInfo.InvariantCulture, "{0:P0} ({1}/{2})", m.kineticEfficiency, m.kineticHits, m.kineticRounds),
                string.Format(CultureInfo.InvariantCulture, ">= {0:P0}", minEff),
                effPass ? "PASS" : "FAIL", effPass);

            // Metric 4: Agents Neutralised
            int totalAttackers = m.agentsNeutralised + m.agentsMissed;
            bool allNeut = m.agentsMissed == 0;
            AppendTableRow(sb, ref rowY, "Agents Neutralised",
                string.Format(CultureInfo.InvariantCulture, "{0} / {1}", m.agentsNeutralised, totalAttackers),
                "All Attackers",
                allNeut ? "100%" : string.Format("{0} Missed", m.agentsMissed), allNeut);

            // Metric 5: Premature EW Penalty
            bool ewPenalty = m.prematureEwPenalty;
            AppendTableRow(sb, ref rowY, "Premature EW Penalty",
                ewPenalty ? "YES (Penalty)" : "NO (Clean)",
                "None",
                !ewPenalty ? "PASS" : "PENALTY", !ewPenalty);

            // Metric 6: Classification Accuracy
            AppendTableRow(sb, ref rowY, "Classification Accuracy",
                "N/A (v1)",
                "—",
                "INFO", true, isInfo: true);

            // ── Section 5: Timeline Mini-Chart (Sparkline / Summary) ────────
            rowY -= 15;
            sb.AppendLine("0 0 0 rg");
            sb.AppendLine("BT /F2 11 Tf 50 " + rowY + " Td (TIMELINE REPLAY SPARKLINE) Tj ET");

            rowY -= 35;
            // Sparkline frame
            sb.AppendLine("0.1 0.1 0.1 rg 50 " + rowY + " 512 30 re f");
            sb.AppendLine("0.4 0.4 0.4 RG 1 w 50 " + rowY + " 512 30 re S");

            // Draw sparkline timeline ticks & milestones
            int totalDuration = sc.durationMs > 0 ? sc.durationMs : 55000;
            if (sc.timelineIndex != null && sc.timelineIndex.Count > 0)
            {
                // Baseline grid
                sb.AppendLine("0.2 0.8 0.2 RG 1.5 w");
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} {1} m", 50, rowY + 15));
                for (int i = 0; i < sc.timelineIndex.Count; i++)
                {
                    float ratio = Mathf.Clamp01((float)sc.timelineIndex[i] / totalDuration);
                    float x = 50 + ratio * 512f;
                    float y = rowY + 15;
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F1} {1:F1} l", x, y));
                }
                sb.AppendLine("S");
            }

            // Milestone labels under sparkline
            sb.AppendLine("BT /F1 8 Tf 0.8 0.8 0.8 rg 55 " + (rowY + 4) + " Td (T+0s Launch) Tj 250 " + (rowY + 4) + " Td (T+12.3s Detection) Tj 480 " + (rowY + 4) + " Td (T+" + (totalDuration / 1000) + "s End) Tj ET");

            // ── Section 6: Footer ───────────────────────────────────────────
            sb.AppendLine("0.4 0.4 0.4 RG 1 w 50 60 m 562 60 l S");
            sb.AppendLine("BT /F2 8 Tf 0.5 0.5 0.5 rg 50 45 Td (CONFIDENTIAL — For training use only · Air-gapped system) Tj ET");
            sb.AppendLine("BT /F1 7 Tf 0.5 0.5 0.5 rg 380 45 Td (TRISHUL-SIM Dossier v1 · Generated " + EscapePdfString(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")) + " UTC) Tj ET");

            return AssemblePdf14(sb.ToString());
        }

        private static void AppendTableRow(StringBuilder sb, ref int rowY, string name, string val, string bench, string verdict, bool pass, bool isInfo = false)
        {
            // Alternate row background
            sb.AppendLine("0.95 0.95 0.95 rg 50 " + rowY + " 512 16 re f");
            sb.AppendLine("0.8 0.8 0.8 RG 0.5 w 50 " + rowY + " 512 16 re S");

            // Column text
            sb.AppendLine("0 0 0 rg");
            sb.AppendLine("BT /F1 8 Tf 55 " + (rowY + 4) + " Td (" + EscapePdfString(name) + ") Tj ET");
            sb.AppendLine("BT /F1 8 Tf 220 " + (rowY + 4) + " Td (" + EscapePdfString(val) + ") Tj ET");
            sb.AppendLine("BT /F1 8 Tf 340 " + (rowY + 4) + " Td (" + EscapePdfString(bench) + ") Tj ET");

            // Coloured verdict
            if (isInfo)
            {
                sb.AppendLine("0.3 0.3 0.3 rg");
            }
            else if (pass)
            {
                sb.AppendLine("0.0 0.6 0.0 rg");
            }
            else
            {
                sb.AppendLine("0.8 0.0 0.0 rg");
            }
            sb.AppendLine("BT /F2 8 Tf 460 " + (rowY + 4) + " Td (" + EscapePdfString(verdict) + ") Tj ET");

            rowY -= 18;
        }

        /// <summary>
        /// Writes the Scorecard PDF to %AppData%\TRISHUL-SIM\sessions\<sessionId>.pdf
        /// and returns the absolute destination path.
        /// </summary>
        public static string WriteToFile(Scorecard sc)
        {
            if (sc == null)
                throw new ArgumentNullException("sc", "Scorecard cannot be null for WriteToFile.");

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
            {
                appData = Path.Combine(Directory.GetCurrentDirectory(), "AppData");
            }

            string sessionsDir = Path.Combine(appData, "TRISHUL-SIM", "sessions");
            if (!Directory.Exists(sessionsDir))
            {
                Directory.CreateDirectory(sessionsDir);
            }

            string fileName = (string.IsNullOrEmpty(sc.sessionId) ? "session-unknown" : sc.sessionId) + ".pdf";
            string pdfPath = Path.Combine(sessionsDir, fileName);

            byte[] bytes = Export(sc);
            File.WriteAllBytes(pdfPath, bytes);

            Debug.Log(string.Format("[PdfExporter] Wrote {0} bytes to: {1}", bytes.Length, pdfPath));
            return pdfPath;
        }

        private static string EscapePdfString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }

        private static byte[] AssemblePdf14(string contentStream)
        {
            byte[] contentBytes = Encoding.UTF8.GetBytes(contentStream);

            var objects = new List<string>();
            // Object 1: Catalog
            objects.Add("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            // Object 2: Pages
            objects.Add("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
            // Object 3: Page (Letter 612 x 792)
            objects.Add("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>\nendobj\n");
            // Object 4: Font F1 (Helvetica)
            objects.Add("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
            // Object 5: Font F2 (Helvetica-Bold)
            objects.Add("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>\nendobj\n");
            // Object 6: Content stream
            string obj6Header = "6 0 obj\n<< /Length " + contentBytes.Length + " >>\nstream\n";
            string obj6Footer = "\nendstream\nendobj\n";

            var ms = new MemoryStream();
            var writer = new StreamWriter(ms, new UTF8Encoding(false));
            writer.Write("%PDF-1.4\n");
            writer.Flush();

            var offsets = new List<long>();
            offsets.Add(0); // 0 is null entry in xref

            for (int i = 0; i < 5; i++)
            {
                offsets.Add(ms.Position);
                writer.Write(objects[i]);
                writer.Flush();
            }

            // Object 6
            offsets.Add(ms.Position);
            writer.Write(obj6Header);
            writer.Flush();
            ms.Write(contentBytes, 0, contentBytes.Length);
            writer.Write(obj6Footer);
            writer.Flush();

            long xrefOffset = ms.Position;
            writer.Write("xref\n0 " + (offsets.Count) + "\n0000000000 65535 f \n");
            for (int i = 1; i < offsets.Count; i++)
            {
                writer.Write(string.Format("{0:D10} 00000 n \n", offsets[i]));
            }

            writer.Write("trailer\n<< /Size " + offsets.Count + " /Root 1 0 R >>\n");
            writer.Write("startxref\n" + xrefOffset + "\n%%EOF\n");
            writer.Flush();

            return ms.ToArray();
        }
    }
}
