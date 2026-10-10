// ============================================================
// ScorecardSelfTest.cs — Automated Verification Test for UX-P1-D
// TRISHUL-SIM · ui-ux track (Rajeswari)
//
// Validates:
// 1. ScorecardPanel.uxml & ScorecardPanel.uss integrity & MIL-STD-2525 styling
// 2. ScorecardPanelController data binding with standard Punjab fixture (PASS)
// 3. ScorecardPanelController data binding with failure fixture (FAIL)
// 4. Metrics table dynamic generation & benchmark thresholds (5 metrics + 1 v2 info)
// 5. Interactive Timeline scrubber seeking & spatial radar overlay projection (6 agents)
// 6. Bilingual pass (Hindi/English runtime switching)
// 7. Full dual-screen integration across CockpitController & C2StationController
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

// Note: Mock UnityEngine and UnityEngine.UIElements are defined in UiShellSelfTest.cs


namespace TrishulSim.UI
{
    using UnityEngine;
    using UnityEngine.UIElements;

    public static class ScorecardSelfTest
    {
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            Console.WriteLine("=================================================");
            Console.WriteLine("TRISHUL-SIM · UX-P1-D Scorecard Renderer Self-Test");
            Console.WriteLine("=================================================");

            try
            {
                TestFilesAndUxmlSchema();
                Console.WriteLine("[PASS] 1. ScorecardPanel UXML & USS schema and styling verified.");

                TestPassedScorecardBinding();
                Console.WriteLine("[PASS] 2. ScorecardPanelController PASSED doctrine fixture bound & verified.");

                TestFailedScorecardBinding();
                Console.WriteLine("[PASS] 3. ScorecardPanelController FAILED doctrine fixture bound & verified.");

                TestTimelineScrubberAndSpatialOverlay();
                Console.WriteLine("[PASS] 4. Interactive Timeline Scrubber & 6-agent spatial radar overlay verified.");

                TestBilingualLocalizationToggle();
                Console.WriteLine("[PASS] 5. Bilingual runtime switching (English <-> Hindi) verified.");

                TestDualScreenControllerIntegration();
                Console.WriteLine("[PASS] 6. Simultaneous Cockpit & C2 Scorecard mirror integration verified.");

                Console.WriteLine("\n[ALL TESTS PASSED] UX-P1-D Scorecard Renderer & Scrubber verified successfully!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[TEST FAILED] " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                return 1;
            }
        }

        private static string FindRepoRoot()
        {
            string dir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "START-HERE.md")) || Directory.Exists(Path.Combine(dir, "Assets")))
                    return dir;
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return Directory.GetCurrentDirectory();
        }

        public static void TestFilesAndUxmlSchema()
        {
            string root = FindRepoRoot();
            string uxmlPath = Path.Combine(root, "Assets", "UI", "ScorecardPanel.uxml");
            string ussPath = Path.Combine(root, "Assets", "UI", "ScorecardPanel.uss");

            Assert(File.Exists(uxmlPath), "ScorecardPanel.uxml must exist at Assets/UI/ScorecardPanel.uxml");
            Assert(File.Exists(ussPath), "ScorecardPanel.uss must exist at Assets/UI/ScorecardPanel.uss");

            string uxmlText = File.ReadAllText(uxmlPath);
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(uxmlText);

            Assert(uxmlText.Contains("name=\"scorecard-title\""), "UXML must contain scorecard-title");
            Assert(uxmlText.Contains("name=\"scenario-name\""), "UXML must contain scenario-name");
            Assert(uxmlText.Contains("name=\"trainee-id-label\""), "UXML must contain trainee-id-label");
            Assert(uxmlText.Contains("name=\"doctrine-badge\""), "UXML must contain doctrine-badge");
            Assert(uxmlText.Contains("name=\"metrics-table\""), "UXML must contain metrics-table");
            Assert(uxmlText.Contains("name=\"timeline-slider\""), "UXML must contain timeline-slider");
            Assert(uxmlText.Contains("name=\"timeline-time\""), "UXML must contain timeline-time");
            Assert(uxmlText.Contains("name=\"spatial-overlay\""), "UXML must contain spatial-overlay");
            Assert(uxmlText.Contains("name=\"pdf-export-btn\""), "UXML must contain pdf-export-btn");

            string ussText = File.ReadAllText(ussPath);
            Assert(ussText.Contains("#00B300"), "USS must define MIL-STD-2525 GREEN #00B300");
            Assert(ussText.Contains("#FFB300"), "USS must define MIL-STD-2525 AMBER #FFB300");
            Assert(ussText.Contains("#CC0000"), "USS must define MIL-STD-2525 RED #CC0000");
            Assert(ussText.Contains(".badge-pass"), "USS must define .badge-pass");
            Assert(ussText.Contains(".badge-fail"), "USS must define .badge-fail");
            Assert(ussText.Contains(".spatial-overlay"), "USS must define .spatial-overlay");
        }

        public static void TestPassedScorecardBinding()
        {
            var go = new GameObject("ScorecardPanelTest");
            var controller = go.AddComponent<ScorecardPanelController>();
            var mockRoot = CreateMockScorecardHierarchy();

            controller.BindElements(mockRoot);
            var sc = ScorecardMock.GetPunjabFixture();
            controller.Bind(sc);

            Assert(controller.CurrentScorecard != null, "Scorecard must be bound");
            Assert(controller.ScorecardTitle.text == "TACTICAL PERFORMANCE SCORECARD", "Title must match English default");
            Assert(controller.ScenarioNameLabel.text == "Punjab Border — Zero Visibility Pincer", "Scenario name must match fixture");
            Assert(controller.TraineeIdLabel.text.Contains("havildar-rajan"), "Trainee ID must be displayed");

            Assert(controller.DoctrineBadge.ClassListContains("badge-pass"), "Doctrine badge must have badge-pass class");
            Assert(!controller.DoctrineBadge.ClassListContains("badge-fail"), "Doctrine badge must NOT have badge-fail class");
            Assert(controller.DoctrineText.text == "DOCTRINE PASSED", "Doctrine text must be DOCTRINE PASSED");

            Assert(controller.MetricsTable.childCount == 6, "Metrics table must have 6 rows");
            Assert(controller.PdfExportBtn.text == "Export PDF", "PDF export button text must match");
        }

        public static void TestFailedScorecardBinding()
        {
            var go = new GameObject("ScorecardFailTest");
            var controller = go.AddComponent<ScorecardPanelController>();
            var mockRoot = CreateMockScorecardHierarchy();

            controller.BindElements(mockRoot);
            var sc = ScorecardMock.GetFailedFixture();
            controller.Bind(sc);

            Assert(controller.DoctrineBadge.ClassListContains("badge-fail"), "Doctrine badge must have badge-fail class for failed fixture");
            Assert(!controller.DoctrineBadge.ClassListContains("badge-pass"), "Doctrine badge must NOT have badge-pass class");
            Assert(controller.DoctrineText.text == "DOCTRINE FAILED", "Doctrine text must be DOCTRINE FAILED");
        }

        public static void TestTimelineScrubberAndSpatialOverlay()
        {
            var go = new GameObject("ScrubberTest");
            var controller = go.AddComponent<ScorecardPanelController>();
            var mockRoot = CreateMockScorecardHierarchy();

            controller.BindElements(mockRoot);
            var sc = ScorecardMock.GetPunjabFixture();
            controller.Bind(sc);

            // Set scrub position to T+45s (45000 ms) — the moment of engagement in Punjab scenario
            controller.SetScrubPosition(45000);
            Assert(controller.CurrentScrubMs == 45000, "Current scrub MS must be 45000");
            Assert(controller.TimelineTimeLabel.text == "T+45s", "Timeline time label must display T+45s");

            // Spatial overlay must render 6 agent dots
            Assert(controller.ActiveBlipCount == 6, "Spatial overlay must contain 6 agent blips");

            // Verify Decoys vs Attackers styling
            var blips = controller.BlipContainer.Children();
            int decoyCount = 0;
            int attackerOrNeutralised = 0;

            foreach (var b in blips)
            {
                if (b.ClassListContains("dot-decoy")) decoyCount++;
                if (b.ClassListContains("dot-attacker") || b.ClassListContains("dot-neutralised")) attackerOrNeutralised++;
            }

            Assert(decoyCount == 2, "Must contain exactly 2 decoy blips");
            Assert(attackerOrNeutralised == 4, "Must contain exactly 4 attacker/neutralised blips");
        }

        public static void TestBilingualLocalizationToggle()
        {
            var go = new GameObject("BilingualTest");
            var controller = go.AddComponent<ScorecardPanelController>();
            var mockRoot = CreateMockScorecardHierarchy();

            controller.BindElements(mockRoot);
            var sc = ScorecardMock.GetPunjabFixture();
            controller.Bind(sc);

            // Toggle to Hindi
            if (LocaleToggle.Instance.CurrentLocale == TrishulLocale.English)
            {
                LocaleToggle.Instance.ToggleLanguage();
            }

            Assert(LocaleToggle.Instance.IsHindi, "Locale must be Hindi");
            controller.RefreshView();

            Assert(controller.ScorecardTitle.text == "सामरिक प्रदर्शन स्कोरकार्ड", "Title must be in Hindi");
            Assert(controller.ScenarioNameLabel.text == "पंजाब सीमा — शून्य दृश्यता पिंसर", "Scenario name must be Hindi");
            Assert(controller.DoctrineText.text == "सिद्धांत उत्तीर्ण", "Doctrine passed text must be in Hindi");
            Assert(controller.PdfExportBtn.text == "पीडीएफ निर्यात", "PDF export button must be in Hindi");

            // Toggle back to English
            LocaleToggle.Instance.ToggleLanguage();
            Assert(!LocaleToggle.Instance.IsHindi, "Locale must be English");
            controller.RefreshView();

            Assert(controller.ScorecardTitle.text == "TACTICAL PERFORMANCE SCORECARD", "Title must be English again");
            Assert(controller.DoctrineText.text == "DOCTRINE PASSED", "Doctrine text must be DOCTRINE PASSED");
        }

        public static void TestDualScreenControllerIntegration()
        {
            // Cockpit Controller integration
            var cockpitGo = new GameObject("CockpitGo");
            var cockpitDoc = cockpitGo.AddComponent<UIDocument>();
            var cockpitController = cockpitGo.AddComponent<CockpitController>();

            var cockpitRoot = new VisualElement();
            var threatInd = new VisualElement { name = "threat-indicator" };
            threatInd.Add(new Label { name = "threat-label" });
            cockpitRoot.Add(threatInd);
            cockpitRoot.Add(new VisualElement { name = "rf-waterfall-pane" });
            cockpitRoot.Add(new VisualElement { name = "rf-content" });
            cockpitRoot.Add(new VisualElement { name = "doppler-pane" });
            cockpitRoot.Add(new VisualElement { name = "doppler-content" });
            cockpitRoot.Add(new Label { name = "doppler-reading" });
            cockpitRoot.Add(new VisualElement { name = "acoustic-pane" });
            cockpitRoot.Add(new VisualElement { name = "acoustic-content" });
            cockpitRoot.Add(new Label { name = "acoustic-reading" });
            cockpitRoot.Add(new VisualElement { name = "controls-row" });
            
            var cockpitScorecardPanel = new VisualElement { name = "scorecard-panel" };
            cockpitScorecardPanel.AddToClassList("hidden");
            var cockpitScorecardChild = CreateMockScorecardHierarchy();
            cockpitScorecardPanel.Add(cockpitScorecardChild);
            cockpitRoot.Add(cockpitScorecardPanel);

            var computingOverlay = new VisualElement { name = "computing-overlay" };
            computingOverlay.AddToClassList("hidden");
            cockpitRoot.Add(computingOverlay);

            cockpitController.BindElements(cockpitRoot);

            // C2 Station Controller integration
            var c2Go = new GameObject("C2Go");
            var c2Doc = c2Go.AddComponent<UIDocument>();
            var c2Controller = c2Go.AddComponent<C2StationController>();

            var c2Root = new VisualElement();
            c2Root.Add(new Label { name = "app-title" });
            var statusGrp = new VisualElement();
            statusGrp.Add(new VisualElement { name = "link-status-icon" });
            statusGrp.Add(new Label { name = "link-status-label" });
            c2Root.Add(statusGrp);
            c2Root.Add(new DropdownField { name = "scenario-dropdown" });
            c2Root.Add(new TextField { name = "trainee-id" });
            c2Root.Add(new Button { name = "launch-btn" });
            c2Root.Add(new Button { name = "abort-btn" });
            c2Root.Add(new Button { name = "lang-toggle" });

            var c2ScorecardMirror = new VisualElement { name = "scorecard-mirror" };
            c2ScorecardMirror.AddToClassList("hidden");
            var c2ScorecardChild = CreateMockScorecardHierarchy();
            c2ScorecardMirror.Add(c2ScorecardChild);
            c2Root.Add(c2ScorecardMirror);

            c2Controller.BindElements(c2Root);

            // Simulate SCORECARD_PAYLOAD on both
            var fixture = ScorecardMock.GetPunjabFixture();
            cockpitController.OnScorecardReceived(fixture);
            c2Controller.OnScorecardReceived(fixture);

            Assert(!cockpitScorecardPanel.ClassListContains("hidden"), "Cockpit scorecard panel must un-hide on SCORECARD_PAYLOAD");
            Assert(!c2ScorecardMirror.ClassListContains("hidden"), "C2 scorecard mirror must un-hide on SCORECARD_PAYLOAD");

            Assert(cockpitController.ScorecardController != null, "Cockpit must possess bound ScorecardController");
            Assert(c2Controller.ScorecardController != null, "C2 must possess bound ScorecardController");

            Assert(cockpitController.ScorecardController.DoctrineBadge.ClassListContains("badge-pass"), "Cockpit badge must be pass");
            Assert(c2Controller.ScorecardController.DoctrineBadge.ClassListContains("badge-pass"), "C2 mirror badge must be pass");
        }

        private static VisualElement CreateMockScorecardHierarchy()
        {
            var root = new VisualElement { name = "scorecard-root" };
            root.Add(new Label { name = "scorecard-title" });
            root.Add(new Label { name = "scenario-name" });
            root.Add(new Label { name = "trainee-id-label" });

            var badge = new VisualElement { name = "doctrine-badge" };
            badge.Add(new Label { name = "doctrine-text" });
            root.Add(badge);

            root.Add(new VisualElement { name = "metrics-table" });

            var timelineSec = new VisualElement { name = "timeline-section" };
            timelineSec.Add(new Slider { name = "timeline-slider" });
            timelineSec.Add(new Label { name = "timeline-time" });

            var overlay = new VisualElement { name = "spatial-overlay" };
            overlay.Add(new VisualElement { name = "drone-blip-container" });
            timelineSec.Add(overlay);

            root.Add(timelineSec);
            root.Add(new Button { name = "pdf-export-btn" });

            return root;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message);
        }
    }
}
