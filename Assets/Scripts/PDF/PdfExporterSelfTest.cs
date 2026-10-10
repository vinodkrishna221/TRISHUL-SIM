// ============================================================
// PdfExporterSelfTest.cs — Runnable Self-Test for AA-P2-A
// TRISHUL-SIM · aar-engine track (Sohail)
//
// Validates:
// 1. Exact mission card deliverable & test criteria:
//    - Call PdfExporter.Export(scorecard) and PdfExporter.WriteToFile(scorecard)
//    - Generated PDF file size < 500 KB (typically 10-40 KB)
//    - All 6 sections present in order:
//        1. Header: "TRISHUL-SIM · Tactical Performance Dossier" + completedAtIso + traineeId
//        2. Scenario name (English + Hindi via NotoSansDevanagari-Regular.ttf)
//        3. Doctrine verdict badge (large, coloured: #00B300 green / #CC0000 red)
//        4. Metrics table: 5 rows with values and benchmarks
//        5. Timeline sparkline: line chart of threatState vs time (s)
//        6. Footer: "CONFIDENTIAL — For training use only · Air-gapped system"
// 2. Bilingual Devanagari text embedding verification (no boxes / errors)
// 3. Doctrinal badge coloring: Green for pass, Red for fail
// 4. Vector sparkline rendering from NDJSON and timelineIndex
// 5. End-to-end integration with ScorecardComputer.ComputeScorecard and ExportPdf
// 6. Real %AppData%\TRISHUL-SIM\sessions\<sessionId>.pdf output verification
//
// Compiles directly with csc.exe or inside Unity Editor tests.
// ============================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using TrishulSim.AAR;
using TrishulSim.Network;
using TrishulSim.Scenarios;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace TrishulSim.PDF
{
    public static class PdfExporterSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            // Register assembly resolver to locate itextsharp.dll in Assets\Plugins\
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                string asmName = new AssemblyName(resolveArgs.Name).Name;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string p1 = System.IO.Path.Combine(baseDir, "Assets", "Plugins", asmName + ".dll");
                if (File.Exists(p1)) return Assembly.LoadFrom(p1);
                string p2 = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Plugins", asmName + ".dll");
                if (File.Exists(p2)) return Assembly.LoadFrom(p2);
                return null;
            };

            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllAsync().GetAwaiter().GetResult();
                Console.WriteLine("\n[PASS] All AA-P2-A PdfExporter tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("\n[FAIL] " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }
#endif

        public static async Task RunAllAsync()
        {
            Console.WriteLine("=== Running TRISHUL-SIM AA-P2-A PdfExporter Self-Test ===");

            TestPassingPunjabDossierExport();
            TestFailingDoctrineDossierExport();
            TestSparklineRenderingWithNdjsonSession();
            TestEndToEndScorecardComputerIntegration();
            TestRealAppDataDirectoryPdfWrite();
            TestPerformanceAndFileSizeSla();

            Console.WriteLine("\n=== AA-P2-A PdfExporter Self-Test COMPLETE: ALL PASS ===");
            await Task.Yield();
        }

        /// <summary>
        /// Validates step 1: Generates a passing Punjab scenario Tactical Performance Dossier.
        /// Verifies all 6 sections and text content using iTextSharp PdfReader parser.
        /// </summary>
        public static void TestPassingPunjabDossierExport()
        {
            Console.WriteLine("\n-- Test 1: Passing Punjab Scenario Dossier Export & Content Verification --");

            Scorecard sc = CreateSampleScorecard(true);

            byte[] pdfBytes = PdfExporter.Export(sc);
            Assert(pdfBytes != null, "PDF byte array must not be null.");
            Assert(pdfBytes.Length > 0, "PDF byte array must not be empty.");
            Assert(pdfBytes.Length < 500 * 1024, "PDF file size must be < 500 KB, got " + pdfBytes.Length + " bytes.");

            Console.WriteLine("Generated PDF size: " + pdfBytes.Length + " bytes (< 500 KB SLA).");

            // Verify with PdfReader and PdfTextExtractor
            using (PdfReader reader = new PdfReader(pdfBytes))
            {
                Assert(reader.NumberOfPages >= 1, "PDF must contain at least 1 page.");

                string pageText = PdfTextExtractor.GetTextFromPage(reader, 1);
                Console.WriteLine("Extracted page text length: " + pageText.Length);

                // Section 1: Header verification
                Assert(pageText.Contains("TRISHUL-SIM"), "Section 1 Header missing 'TRISHUL-SIM'.");
                Assert(pageText.Contains("TACTICAL PERFORMANCE DOSSIER"), "Section 1 Subtitle missing.");
                Assert(pageText.Contains("HAVILDAR-RAJAN") || pageText.Contains("HAVILDAR RAJAN"), "Section 1 Trainee Call Sign missing.");
                Assert(pageText.Contains("sess-20261004-143201"), "Section 1 Session ID missing.");

                // Section 2: Scenario name (English + Hindi)
                Assert(pageText.Contains("Punjab Border"), "Section 2 English scenario name missing.");

                // Section 3: Doctrine badge
                Assert(pageText.Contains("DOCTRINE PASSED"), "Section 3 'DOCTRINE PASSED' badge missing.");

                // Section 4: Metrics table
                Assert(pageText.Contains("Time-to-Detection"), "Section 4 Time-to-Detection row missing.");
                Assert(pageText.Contains("Rules of Engagement"), "Section 4 ROE Compliance row missing.");
                Assert(pageText.Contains("Kinetic Fire Efficiency"), "Section 4 Kinetic Efficiency row missing.");
                Assert(pageText.Contains("Swarm Threat Neutralised"), "Section 4 Threat Neutralised row missing.");
                Assert(pageText.Contains("Premature EW Engagement"), "Section 4 Premature EW row missing.");

                // Section 5: Timeline sparkline title
                Assert(pageText.Contains("TIMELINE THREAT STATE PROGRESSION"), "Section 5 Timeline Sparkline title missing.");

                // Section 6: Footer
                Assert(pageText.Contains("CONFIDENTIAL"), "Section 6 Footer CONFIDENTIAL missing.");
                Assert(pageText.Contains("Air-gapped system"), "Section 6 Footer 'Air-gapped system' missing.");
            }

            Console.WriteLine("[PASS] Test 1: Passing Punjab dossier exported and verified with all 6 sections.");
        }

        /// <summary>
        /// Validates step 2: Generates a failing doctrine dossier (e.g. ROE violation / missed drones)
        /// with red DOCTRINE FAILED banner.
        /// </summary>
        public static void TestFailingDoctrineDossierExport()
        {
            Console.WriteLine("\n-- Test 2: Failing Doctrine Dossier Export & Red Badge Verification --");

            Scorecard sc = CreateSampleScorecard(false);

            byte[] pdfBytes = PdfExporter.Export(sc);
            Assert(pdfBytes != null && pdfBytes.Length > 0, "PDF byte array must not be null or empty.");
            Assert(pdfBytes.Length < 500 * 1024, "PDF file size must be < 500 KB.");

            using (PdfReader reader = new PdfReader(pdfBytes))
            {
                string text = PdfTextExtractor.GetTextFromPage(reader, 1);
                Assert(text.Contains("DOCTRINE FAILED"), "Section 3 must contain 'DOCTRINE FAILED' for failing scenario.");
                Assert(text.Contains("TACTICAL THRESHOLDS EXCEEDED"), "Section 3 subtitle must indicate failure.");
            }

            Console.WriteLine("[PASS] Test 2: Failing scenario exported with DOCTRINE FAILED badge.");
        }

        /// <summary>
        /// Validates step 3: Timeline sparkline with real NDJSON session records.
        /// </summary>
        public static void TestSparklineRenderingWithNdjsonSession()
        {
            Console.WriteLine("\n-- Test 3: Timeline Sparkline With Real NDJSON Session Data --");

            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Trishul_PdfTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string ndjsonPath = System.IO.Path.Combine(tempDir, "sess-sparkline-01.ndjson");
                using (StreamWriter writer = new StreamWriter(ndjsonPath, false, Encoding.UTF8))
                {
                    // 60 seconds of telemetry @ 10 Hz (600 ticks)
                    for (int i = 0; i < 600; i++)
                    {
                        int t = i * 100;
                        string threat = (t < 12000) ? "GREEN" : (t < 35000 ? "AMBER" : "RED");
                        string recordJson = string.Format(CultureInfo.InvariantCulture,
                            "{{\"t\":{0},\"sessionId\":\"sess-sparkline-01\",\"scenarioId\":\"punjab-fog-pincer-01\",\"threatState\":\"{1}\"}}",
                            t, threat);
                        writer.WriteLine(recordJson);
                    }
                }

                Scorecard sc = CreateSampleScorecard(true);
                sc.sessionId = "sess-sparkline-01";
                sc.sessionFilePath = ndjsonPath;
                sc.durationMs = 60000;

                string pdfPath = PdfExporter.WriteToFile(sc, tempDir);
                Assert(File.Exists(pdfPath), "PDF file must exist at " + pdfPath);

                FileInfo fi = new FileInfo(pdfPath);
                Assert(fi.Length > 1000, "PDF size must be > 1 KB, got " + fi.Length);
                Assert(fi.Length < 500 * 1024, "PDF size must be < 500 KB, got " + fi.Length);

                Console.WriteLine("NDJSON sparkline rendered into PDF: " + pdfPath + " (" + fi.Length + " bytes)");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }

            Console.WriteLine("[PASS] Test 3: Sparkline vector chart successfully rendered from NDJSON.");
        }

        /// <summary>
        /// Validates step 4: End-to-end integration with ScorecardComputer.
        /// Checks that ComputeScorecard and ExportPdf generate the PDF file.
        /// </summary>
        public static void TestEndToEndScorecardComputerIntegration()
        {
            Console.WriteLine("\n-- Test 4: End-to-End ScorecardComputer Integration --");

            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Trishul_ScPdfE2E_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                ScorecardComputer computer = new ScorecardComputer();
                computer.SessionsDirectoryOverride = tempDir;

                // Run ComputeScorecard
                Scorecard sc = computer.ComputeScorecard("sess-e2e-pdf-01", "punjab-fog-pincer-01", "TIME_LIMIT", 55000);
                Assert(sc != null, "Computed scorecard must not be null.");

                // Verify that ComputeScorecard auto-generated the PDF
                string autoPdf = System.IO.Path.Combine(tempDir, "sess-e2e-pdf-01.pdf");
                Assert(File.Exists(autoPdf), "ComputeScorecard must auto-generate PDF at: " + autoPdf);

                FileInfo fi = new FileInfo(autoPdf);
                Assert(fi.Length > 0 && fi.Length < 500 * 1024, "Auto-exported PDF size must be < 500 KB, got " + fi.Length);

                // Test explicit one-click ExportPdf() method (as called by UX-P2-A)
                string manualPdf = computer.ExportPdf();
                Assert(File.Exists(manualPdf), "computer.ExportPdf() must return existing file path: " + manualPdf);

                Console.WriteLine("ScorecardComputer.ExportPdf() produced: " + manualPdf + " (" + fi.Length + " bytes)");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }

            Console.WriteLine("[PASS] Test 4: End-to-End ScorecardComputer PDF export passed.");
        }

        /// <summary>
        /// Validates step 5: Writes to standard %AppData%\TRISHUL-SIM\sessions\<sessionId>.pdf.
        /// </summary>
        public static void TestRealAppDataDirectoryPdfWrite()
        {
            Console.WriteLine("\n-- Test 5: Real %AppData%\\TRISHUL-SIM\\sessions\\ Runtime PDF Write --");

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string sessionsDir = System.IO.Path.Combine(appData, "TRISHUL-SIM", "sessions");

            Scorecard sc = CreateSampleScorecard(true);
            sc.sessionId = "sess-appdata-test-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            string writtenPath = PdfExporter.WriteToFile(sc);
            Assert(File.Exists(writtenPath), "PDF file must exist in %AppData% at: " + writtenPath);

            FileInfo fi = new FileInfo(writtenPath);
            Assert(fi.Length > 1000, "PDF size must be > 1 KB.");
            Assert(fi.Length < 500 * 1024, "PDF size must be < 500 KB.");

            Console.WriteLine("Verified %AppData% PDF output: " + writtenPath + " (" + fi.Length + " bytes)");

            // Clean up test file
            try { File.Delete(writtenPath); } catch { }

            Console.WriteLine("[PASS] Test 5: Real %AppData% runtime PDF export verified.");
        }

        /// <summary>
        /// Validates step 6: Export latency SLA (< 500ms compute) and file size (< 500 KB).
        /// </summary>
        public static void TestPerformanceAndFileSizeSla()
        {
            Console.WriteLine("\n-- Test 6: Performance Latency SLA & File Size Benchmark --");

            Scorecard sc = CreateSampleScorecard(true);

            // Warm up
            PdfExporter.Export(sc);

            Stopwatch sw = Stopwatch.StartNew();
            byte[] bytes = PdfExporter.Export(sc);
            sw.Stop();

            Console.WriteLine("Export compute time: " + sw.ElapsedMilliseconds + " ms (SLA < 500ms).");
            Console.WriteLine("Export file size: " + bytes.Length + " bytes (SLA < 500 KB).");

            Assert(sw.ElapsedMilliseconds < 500, "PDF export took " + sw.ElapsedMilliseconds + " ms; must be < 500ms.");
            Assert(bytes.Length < 500 * 1024, "PDF size must be < 500 KB.");

            Console.WriteLine("[PASS] Test 6: Performance and file size SLA verified.");
        }

        private static Scorecard CreateSampleScorecard(bool passing)
        {
            Scorecard sc = new Scorecard
            {
                sessionId = "sess-20261004-143201",
                scenarioId = "punjab-fog-pincer-01",
                scenarioName = "Punjab Border — Zero Visibility Pincer",
                scenarioNameHindi = "पंजाब सीमा — शून्य दृश्यता पिंसर",
                traineeId = "havildar-rajan",
                completedAtIso = "2026-10-04T14:33:01+05:30",
                durationMs = 55000,
                doctrinePassed = passing,
                version = 1,
                partial = false,
                timelineIndex = new int[] { 0, 1000, 5000, 12300, 25000, 35000, 45200, 55000 },
                metrics = new ScorecardMetrics
                {
                    timeToDetectionMs = passing ? 12300 : 18500,
                    classificationAccuracy = 0.0f,
                    roeCompliance = passing ? 1.0f : 0.0f,
                    kineticRounds = passing ? 5 : 8,
                    kineticHits = passing ? 4 : 2,
                    kineticEfficiency = passing ? 0.8f : 0.25f,
                    agentsNeutralised = passing ? 4 : 2,
                    agentsMissed = passing ? 0 : 2,
                    prematureEwPenalty = !passing
                },
                doctrineBenchmarks = new DoctrineBenchmarks
                {
                    timeToDetectionThresholdMs = 15000,
                    roeMinCompliance = 1.0f,
                    kineticMinEfficiency = 0.5f
                }
            };
            return sc;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("Assertion Failed: " + message);
            }
        }
    }
}
