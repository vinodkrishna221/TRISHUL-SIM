# Mission Brief — Rajeswari · `ui-ux` (P1 second half + P2 + P3)
> Generated: 2026-10-04 · Assigned by: Orion
> Track: `ui-ux` · Budget: 22h planned / 28h usable (79% — last third at 50% capacity per plan)
> Skeleton deadline: N/A for your cards · Feature freeze: 19 Oct ~21:00 IST

---

## Project one-liner

*"The first battalion-deployable C-UAS trainer that teaches Indian soldiers to defeat a drone swarm in the dark — for ₹10 a seat."*
Full demo narrative → `docs/PRD.md §3`. Your work completes **Beat 5** (Scorecard + Timeline Scrubber) and **Beat 6** (Hindi, PDF, night aesthetic, split-screen layout) — the judge's lasting impression after the wow moment.

## Mission goal

Adapt the UI for split-screen mode, build the Scorecard Renderer + Interactive Timeline Scrubber, wire the PDF export button, complete the full bilingual pass, implement the night/fog threat-state aesthetic, and freeze the UI for submission — **the polish and judge-facing finish of the entire product**.

**Victory definition:** Scorecard renders on both screens within 10 s of scenario end with real metrics + Timeline Scrubber navigable to any moment + PDF exports in one click + Hindi/English toggle works across all labels + pitch-black night mode on Punjab scenario.

---

## Contracts

| Contract | Your role | File |
|---|---|---|
| `scorecard.md` | **CONSUMER** — render all required display elements per §"UI rendering contract" | `board/contracts/scorecard.md` |
| `scenario-events.md` | **CONSUMER** — SCENARIO_START (set isNight flag), SCENARIO_END (freeze panes) | `board/contracts/scenario-events.md` |
| `telemetry.md` | **CONSUMER** — TelemetryRecord.swarmSnapshot for spatial overlay in Timeline Scrubber | `board/contracts/telemetry.md` |

**You own:** `Assets/UI/ScorecardPanel.uxml` · `Assets/Scripts/UI/ScorecardPanelController.cs` · `Assets/Scripts/UI/SmashReticleController.cs` (extends Rani's) · updates to `TraineeCockpit.uss` for night mode · `Assets/UI/TestChecklist.md`
**You do NOT touch:** `Assets/Scripts/AAR/` · `Assets/Scripts/Network/` · `Assets/Scripts/SwarmAI/`

## Stack rules to load

- `board/contracts/scorecard.md §"UI rendering contract"` — ALL required display elements (read this first)
- `board/contracts/scorecard.md §"PDF export shape"` — 6-section PDF content order
- `board/contracts/scorecard.md §"Threat-state colour coding"` — GREEN #00B300, AMBER #FFB300, RED #CC0000
- `docs/PRD.md §3 Beat 5` — "judge replays any moment with spatial overlay"
- `docs/PRD.md §3 Beat 4 ⭐` — "pitch-black screen, two sensor panes, one correct decision"
- `docs/ARCHITECTURE.md §1` — UITK + USS; Unity Localization CSV backend

## Branch names

| Card | Branch |
|---|---|
| UX-P1-C | `feat/ui-ux-split-screen-layout` |
| UX-P1-D | `feat/ui-ux-scorecard-renderer` |
| UX-P2-A | `feat/ui-ux-pdf-button` |
| UX-P2-B | `feat/ui-ux-bilingual-pass` |
| UX-P2-C | `feat/ui-ux-night-fog-aesthetic` |
| UX-P3-A | `feat/ui-ux-demo-freeze` |

## WAITS-ON

| Your card | Waits on | Mock path |
|---|---|---|
| UX-P1-C split-screen | **Vinod: US-P1-A** (SplitScreen.unity + Camera.rect split must exist) | Cannot be mocked — inspect SplitScreen scene when it lands. Start UX-P1-D in parallel. |
| UX-P1-D Scorecard Renderer | **Sohail: AA-P1-A** (real ScorecardMetrics) + **AA-P1-B** (TimelinePlaybackController.SetScrubPosition()) | **Use ScorecardMock immediately:** parse the JSON example from scorecard.md into a C# Scorecard object. Bind ScorecardPanelController to mock; swap to real SCORECARD_PAYLOAD when Sohail lands AA-P1-A. |
| UX-P2-A PDF button | **Sohail: AA-P2-A** (PdfExporter.WriteToFile() exists) | Stub: button click → Debug.Log("PDF would export here") + show loading spinner |
| UX-P2-B bilingual pass | **Rani: UX-P0-B** (Localization CSV + toggle wired) | Rani's branch must be merged first |
| UX-P2-C night aesthetic | **Rani: UX-P1-A** (sensor panes exist to add night borders) | Start writing USS rules; apply when Rani's branch is merged |
| UX-P3-A demo freeze | All ui-ux P2 cards | Final step — no mock needed |

---

## Your cards

### UX-P1-C · Split-screen layout compatibility [timebox: 4h]
```
CONTEXT:
  Vinod's US-P1-A (SplitScreen.unity + Camera.rect) must be merged first.
  Rani's UX-P0-A (UITK shell) must be merged.
  Files: plans/unity-sim-phase-plan.md US-P1-A, docs/PRD.md §4 MUST (split-screen fallback).

  Split-screen context:
    Each panel renders at 960×1080 (at 1920×1080 monitor).
    UITK Panel Settings: "Scale With Screen Size" reference 960×1080.
    Potential issues: sensor panes overflow; sliders clip; text wraps.

  Steps:
    1. Open SplitScreen.unity. Verify each UIDocument Panel Settings:
       C2 and Cockpit: reference resolution 960×1080, screen match mode Width
    2. C2 layout at 960px wide: verify dropdown + launch button don't overlap.
       Fix: flex-wrap: wrap on controls; reduce font to 12px if needed.
    3. Cockpit layout at 960px wide: 3 sensor panes in horizontal row with min-width 280px.
       If they wrap onto 2 lines: add max-height or switch to 2-column grid layout.
       EW sliders: verify they don't clip.
    4. Test at: 1920×1080 full · 1920×1080 split (960×1080) · 1366×768 split.
    5. Create Assets/UI/TestChecklist.md — resolution test table.

DELIVERABLE:
  TraineeCockpit.uss + InstructorC2.uss updated for split-screen
  Assets/UI/TestChecklist.md

TEST:
  SplitScreen.unity: both panels display without clipping at 1920×1080 split.
  All 3 sensor panes visible in Cockpit half; C2 controls visible. No horizontal scrollbar.
DEMO: "Single laptop running full demo — both panels readable." (Beat 6)
STATUS: [ ] todo
```

---

### UX-P1-D · Scorecard Renderer + Interactive Timeline Scrubber ⭐ [timebox: 4h]
```
CONTEXT:
  UX-P1-C merged (layout stable).
  Sohail's AA-P1-A (real ScorecardMetrics) + AA-P1-B (TimelinePlaybackController) should be done.
  If not: use ScorecardMock → parse scorecard.md JSON example into C# Scorecard object.

  Files:
    • board/contracts/scorecard.md §"UI rendering contract" — ALL required display elements:
        Time-to-Detection, ROE Compliance, Kinetic Efficiency, Agents Neutralised, Doctrine Badge,
        Timeline Scrubber (navigable + spatial overlay), PDF Export button
    • board/contracts/scorecard.md §"Bilingual" — scenarioName vs scenarioNameHindi on toggle
    • board/contracts/scorecard.md §"Threat-state colour coding" — colours for badges

  SCORECARD PANEL UXML (Assets/UI/ScorecardPanel.uxml):
  <VisualElement class="scorecard-root">
    <Label name="scorecard-title" text="TACTICAL PERFORMANCE SCORECARD" />
    <Label name="scenario-name" />
    <Label name="trainee-id-label" />
    <VisualElement name="doctrine-badge" class="badge">
      <Label name="doctrine-text" text="DOCTRINE PASSED" />
    </VisualElement>
    <VisualElement class="metrics-table" name="metrics-table" />
    <VisualElement class="timeline-section">
      <Label text="TIMELINE" />
      <Slider name="timeline-slider" low-value="0" high-value="100" />
      <Label name="timeline-time" text="T+0s" />
      <VisualElement name="spatial-overlay" class="spatial-overlay" />
    </VisualElement>
    <Button name="pdf-export-btn" text="Export PDF" />
  </VisualElement>

  Steps:
    1. Assets/Scripts/UI/ScorecardPanelController.cs:
       Bind(Scorecard sc):
         scenario-name: sc.scenarioName (or sc.scenarioNameHindi based on current locale)
         trainee-id-label: "Trainee: " + sc.traineeId
         doctrine-badge: add USS class "badge-pass" (#00B300) or "badge-fail" (#CC0000)
         metrics-table: generate rows dynamically (VisualElement per row, 4 Label children):
           [Metric Name (localised)] · [Value] · [Benchmark] · [✓ PASS / ✗ FAIL (coloured)]
           Rows:
             Time-to-Detection: sc.metrics.timeToDetectionMs ms / ≤ timeToDetectionThresholdMs ms
             ROE Compliance: format(sc.metrics.roeCompliance) / ≥ 1.0
             Kinetic Efficiency: format(sc.metrics.kineticEfficiency, "P0") / ≥ 50%
             Agents Neutralised: sc.metrics.agentsNeutralised / display only
             Premature EW Penalty: sc.metrics.prematureEwPenalty ? "YES ✗" : "NO ✓" / no threshold
         timelineIndex: slider max-value = sc.timelineIndex[^1] (last t value in ms)
         pdf-export-btn: stub log for now (UX-P2-A wires real export)
    2. Timeline Scrubber:
       timeline-slider.RegisterValueChangedCallback: on change →
         int targetMs = Mathf.RoundToInt(slider.value * 1000)
         TimelinePlaybackController.SetScrubPosition(targetMs)
         timeline-time label: "T+" + (targetMs/1000) + "s"
       On CurrentRecord change: update spatial-overlay drone positions
    3. Spatial overlay: for each agent in CurrentRecord.swarmSnapshot:
       Small VisualElement circle at projected bearing/distance position
       Colour: decoy=blue, attacker=red, neutralised=grey
       (Use same projection logic as DopplerScopeRenderer — bearing angle to X/Y on overlay)
    4. CockpitController: on SCORECARD_PAYLOAD → deserialize Scorecard →
       ScorecardPanelController.Bind(); show scorecard-panel; hide "Computing Scorecard..." overlay
       C2StationController: same binding for scorecard-mirror panel.
    5. Connect scorecard-panel UXML to ScorecardPanel.uxml (add as child of existing panel VE)

DELIVERABLE:
  Assets/UI/ScorecardPanel.uxml
  Assets/Scripts/UI/ScorecardPanelController.cs
  CockpitController.cs + C2StationController.cs updated

TEST:
  Run Punjab scenario end. Within 10s:
  - Scorecard panel appears on BOTH screens simultaneously
  - Doctrine badge: coloured correctly (real from Sohail / mock shows "DOCTRINE FAILED" in red)
  - 5 metric rows with values + pass/fail
  - Timeline slider: drag to T+45s → spatial overlay shows 6 drone dots at that moment
  - Language toggle: "TACTICAL PERFORMANCE SCORECARD" ↔ Hindi equivalent

PUSH IMMEDIATELY — this is Beat 5, the scorecard that judges examine longest.
DEMO: ⭐ "Beat 5 fully visual on both screens — judge scrubs to the moment he fired." (Beat 5)
STATUS: [ ] todo
```

---

### UX-P2-A · PDF Export button wiring to PdfExporter [timebox: 4h]
```
CONTEXT:
  Sohail's AA-P2-A (PdfExporter.WriteToFile() method) must be done.
  UX-P1-D (pdf-export-btn stub) merged.
  Files: board/contracts/scorecard.md §"PDF export shape" (6 ordered sections).

  Steps:
    1. ScorecardPanelController.pdf-export-btn.clicked:
       - Show loading spinner (VisualElement with CSS rotate animation, 360° in 1s)
       - string pdfPath = PdfExporter.WriteToFile(currentScorecard)  (Sohail's method)
       - Hide spinner
       - System.Diagnostics.Process.Start(pdfPath) — opens Windows default PDF viewer
         Fallback: GUIUtility.systemCopyBuffer = pdfPath; show dialog "PDF saved to: <path>"
    2. Error handling: wrap in try/catch Exception → show error label "PDF export failed — check disk space"
    3. Loading spinner USS:
       @keyframes spin { 0% { rotate: 0deg; } 100% { rotate: 360deg; } }
       .spinner { width: 24px; height: 24px; border: 3px solid #00B300; border-radius: 50%;
                  border-top-color: transparent; animation: spin 1s linear infinite; }
    4. Localise: pdf-export-btn text = LocalizedString("PDF_EXPORT_BTN") (already in CSV)

DELIVERABLE: ScorecardPanelController.cs updated (real PDF wiring + spinner + error handling)

TEST:
  Run scenario to end. Click "Export PDF". Windows PDF viewer opens within 3s.
  PDF contains all 6 sections (check visually). Spinner visible during generation.
DEMO: "Judge gets a PDF in one click." (Beat 6)
STATUS: [ ] todo
```

---

### UX-P2-B · Full bilingual pass — all P1 labels + scenario names [timebox: 4h]
```
CONTEXT:
  Rani's UX-P0-B (Localization CSV + toggle) must be merged first.
  This card audits all labels added in UX-P1-A through UX-P1-D and P2-A.
  Files: board/contracts/scorecard.md §"Bilingual", Assets/Localization/StringTable_en.csv.

  Steps:
    1. Audit all Label.text values added since UX-P0-B — collect every hard-coded string
    2. Add missing keys to StringTable_en.csv + StringTable_hi.csv:
       SMASH_TITLE, AZIMUTH_LABEL, ELEVATION_LABEL, JAM_BAND_LABEL, ENGAGE_LABEL,
       HIT_LABEL, MISS_LABEL, SCORECARD_TITLE, TIMELINE_LABEL, LOCKED_PREFIX,
       SCENARIO_en names ↔ scenarioNameHindi from SCHEMA.md Entity 1 example
    3. MIL-STD-2525 tactical symbology: apply colours from scorecard.md to ALL indicators:
       - Threat-state indicator: GREEN #00B300, AMBER #FFB300, RED #CC0000 (already in USS)
       - Agent icons in spatial overlay: decoy=blue (#0000FF), attacker=red (#CC0000),
         neutralised=grey (#666666) — confirm these are applied in ScorecardPanelController
    4. Wire remaining LocalizedString references in all controllers
    5. Final test: full demo run in Hindi mode — no English text visible (except "TRISHUL-SIM" brand)

DELIVERABLE: StringTable_en.csv + StringTable_hi.csv fully populated; all labels localised
TEST: Launch in Hindi mode. Run all 6 beats. No English labels visible.
DEMO: "Full bilingual demo path." (Beat 6)
STATUS: [ ] todo
```

---

### UX-P2-C · Threat-state colour + MIL-STD-2525 consistency + night/fog ambience ⭐ [timebox: 4h]
```
CONTEXT:
  UX-P2-B merged (USS consistent). This card adds the pitch-black night aesthetic for the Punjab demo.
  Files: board/contracts/scorecard.md §"Threat-state colour coding",
  docs/PRD.md §3 Beat 4 ⭐ ("pitch-black screen, two sensor panes, one correct decision").

  Steps:
    1. CSS custom properties pass — replace raw hex values with variables in ALL *.uss:
       :root { --mil-green: #00B300; --mil-amber: #FFB300; --mil-red: #CC0000; --bg: #0D0D0D; }
    2. Night/fog ambience (Punjab Pincer, WeatherPreset.isNight == true):
       CockpitController: on SCENARIO_START — if scenarioConfig.weatherPreset.isNight:
         cockpit-root.AddToClassList("night-mode")
       TraineeCockpit.uss:
         .night-mode { background-color: #000000; }
         .night-mode .sensor-pane { border-color: var(--mil-green); }
         Fog overlay VE: position:absolute; width:100%; height:100%; background:#B0C4DE;
                         opacity:0.12; pointer-events:none; (place behind sensors)
    3. Threat indicator pulse (RED state):
       TraineeCockpit.uss:
         .threat-red { background-color: var(--mil-red); animation: pulse 1s infinite; }
         @keyframes pulse { 0%,100% { opacity:1.0; } 50% { opacity:0.4; } }
    4. Sensor pane borders follow threat state:
       threat-green → border-color #00B300; threat-amber → #FFB300; threat-red → #CC0000
    5. Final run: Punjab scenario in night mode — black background, glowing sensors, pulsing RED
       indicator. "Pitch-black screen + sensor-only aesthetic" visible to judges.

DELIVERABLE:
  All *.uss updated with CSS custom properties
  Night/fog ambience + pulsing threat indicator implemented

TEST:
  Launch Punjab Pincer (isNight=true): background is black; sensors glow with borders.
  Threat indicator pulses red during RED state.
  Switch to Desert FOB (isNight=false): lighter background.
DEMO: ⭐ "Pitch-black screen + two sensor panes — visual centrepiece of Beat 4." (Beat 4 aesthetic)
STATUS: [ ] todo
```

---

### UX-P3-A · Demo-script alignment + UI freeze [timebox: 2h]
```
CONTEXT:
  Feature freeze: 19 Oct ~21:00 IST. Final card for all of ui-ux.
  Files: docs/PRD.md §5 (wow moment definition), docs/BUILD-PLAN.md Freeze calendar,
  .agent/rules/40-endgame.md.

  Steps:
    1. Read docs/DEMO-SCRIPT.md (if written by Herald); verify every UI label matches the script's
       exact words (e.g., dropdown shows "Punjab Border — Zero Visibility Pincer" — no typos)
    2. Freeze: git update-index --chmod=-w on all *.uxml + *.uss files to prevent accidental edits
    3. Verify: Unity window title = "TRISHUL-SIM C-UAS Trainer" (Edit → Project Settings → Player)
    4. Verify: cursor visible; fullscreen escape not conflicting with abort button keybind
    5. Disable Unity splash screen (Project Settings → Player → Splash Screen → Show Splash Screen off)
    6. board/STATUS.md: "ui-ux: FROZEN — 19 Oct <time>"

DELIVERABLE: All UI files frozen; board/STATUS.md FROZEN status
TEST: Cold launch of .exe: UI appears immediately, no Unity splash. All controls work first click.
STATUS: [ ] todo
```

---

## Sync expectations

- **Start UX-P1-D (Scorecard Renderer) with ScorecardMock immediately** — don't wait for Sohail's real Scorecard; swap to live when AA-P1-A lands.
- **UX-P1-C: start when Vinod pushes US-P1-A** (SplitScreen.unity).
- **UX-P2-C: night mode USS rules can be written before Rani's sensor panes are merged** — apply class when her branch lands.
- **PRs:** Tag Rani as reviewer for all P2 cards (she owns the base USS + controllers you extend). Tag Sohail for UX-P1-D (he produces the Scorecard API you consume).

---

## Accept

> I, Rajeswari, have read this brief and will start UX-P1-D with ScorecardMock immediately — no waiting.

- [ ] **ACCEPTED**
