# Phase Plan — `ui-ux`
> Generated: 2026-10-04 · Owner: Orion (Architect persona)
> Track budget: ~56h usable (70% of ~80h allocated across 1–2 builders, 04–20 Oct 2026)
> Skeleton deadline: **09 Oct 2026 EOD** · Submission: 20 Oct 2026 23:59 IST

---

## Cross-Track Dependency DAG

```
unity-sim-P0-A  ──►  UX-P0-A   (WS IPC live; UI needs connection status from WsServer.ConnectedCount)
unity-sim-P0-B  ──►  UX-P0-A   (SCENARIO_START received; UI hides lobby + shows sensor panes)
AA-P0-A         ──►  UX-P0-A   (NDJSON shape frozen; Timeline Scrubber stub can reference field names)
AA-P0-B         ──►  UX-P0-A   (SCORECARD_PAYLOAD shape frozen; Scorecard stub panel can render zeroes)
SA-P0-B         ──►  UX-P1-A   (SensorReadings.rfWaterfallPeakDbm/GHz live; RF Waterfall renders real data)
SA-P1-A         ──►  UX-P1-A   (dopplerPeakHz + acousticSplDb live; Doppler scope + Acoustic FFT render)
SA-P1-B-ii      ──►  UX-P1-B   (HBT pincer split live; EW + SMASH controls become tactically meaningful)
SA-P1-D         ──►  UX-P1-B   (KineticEvent live; SMASH reticle can highlight targeted agent)
AA-P1-A         ──►  UX-P1-D   (real ScorecardMetrics; Scorecard Renderer shows non-zero values)
AA-P1-B         ──►  UX-P1-D   (TimelinePlaybackController.SetScrubPosition() callable; scrub bar live)
unity-sim-P1-A  ──►  UX-P1-C   (split-screen fallback layout; UI must work in split-screen Camera.rect)
AA-P2-A         ──►  UX-P2-A   (PdfExporter.Export() exists; PDF button can call it)

Mock path (P0/P1 isolation):
  • UX-P0-A uses a SensorMock fixture (hard-coded SensorReadings constants) for all 3 panes
    until SA-P0-B is complete. Mock defined in: Assets/Scripts/Mocks/SensorMock.cs (see swarm-ai P3-B,
    or create an inline stub: rfWaterfallPeakDbm=-62.3, dopplerPeakHz=220.4, acousticSplDb=54.2)
  • UX-P1-D uses ScorecardMock.cs (static Scorecard GetPunjabFixture() from scorecard.md example)
    until AA-P1-A produces real Scorecard.
  • All mocks are #if UNITY_EDITOR gated — removed from production builds.
```

**File surface audit — no overlaps:**
- `ui-ux` owns: `Assets/UI/` (USS stylesheets, UXML templates), `Assets/Scripts/UI/` (UITK controllers), `Assets/Localization/` (CSV tables, LocalizationSettings), `Assets/Fonts/` (TextMeshPro + NotoSans)
- **Shared READ-ONLY:** `Assets/Scripts/AAR/TimelinePlaybackController.cs` (calls SetScrubPosition — does not write), `Assets/Scripts/Sensors/SensorSuiteComputer.cs` (reads CurrentSensorReadings property)
- Does NOT touch: `Assets/Scripts/Network/`, `Assets/Scripts/SwarmAI/`, `Assets/Scripts/AAR/DecisionTreeScorer.cs`

---

## Load Ledger

| Phase | Cards | Timebox sum | Cumulative |
|---|---|---|---|
| P0 Skeleton | UX-P0-A, UX-P0-B | 4h + 4h = **8h** | 8h |
| P1 Core | UX-P1-A, UX-P1-B, UX-P1-C, UX-P1-D | 4h + 4h + 4h + 4h = **16h** | 24h |
| P2 Polish | UX-P2-A, UX-P2-B, UX-P2-C, UX-P2-D | 4h + 4h + 4h + 2h = **14h** | 38h |
| P3 Endgame | UX-P3-A | 2h | **40h** |

**40h of 56h usable = 71% cap. ✓** (within ceiling — 16h buffer for 1–2 builders)

---

## P0 · Skeleton (04–09 Oct 2026)

> Gate: Both screens render at 1920×1080 · Sensor pane placeholders visible · Status icon turns green

---

### UX-P0-A · UITK shell — C2 Station + Trainee Cockpit layout, status icon, sensor pane stubs [timebox: 4h]

```
CONTEXT:
  Prerequisites: unity-sim P0-A (WsServer/WsClient exist; ConnectedCount property available),
  unity-sim P0-B (ScenarioManager.LaunchScenario() exists; SCENARIO_START received by Trainee).
  AA-P0-A and AA-P0-B may or may not be done — use mocks for Scorecard stub.
  
  Files to read before starting:
    • docs/ARCHITECTURE.md §1 — "Unity UI Toolkit (UITK) + USS theming; TextMeshPro; scales to
      1920×1080 and 4K; Bilingual (Hindi/English) via TextMeshPro; Unity Localization package (CSV)"
    • board/contracts/scorecard.md §"UI rendering contract" — required display elements list
    • docs/PRD.md §3 — Beat 1 ("Two-laptop LAN link confirmed — green icon"),
      Beat 2 ("three live sensor panes: RF Waterfall, Micro-Doppler Radar scope, Acoustic FFT Sonar")
    • board/contracts/scenario-events.md — consumer reactions for SCENARIO_START:
      "ui-ux: hide lobby screen; show Sensor Suite panes; set threat state to GREEN"
  
  Repo state: Unity 2022 LTS URP project. InstructorC2.unity, TraineeCockpit.unity exist.
  ConnectionStatusUI.cs stub exists (plain uGUI Text) — this card REPLACES it with UITK.
  No UITK code exists yet. Target: Assets/UI/ + Assets/Scripts/UI/
  
  Steps:
  
  --- C2 Station UI (InstructorC2 scene) ---
  
  1. Create Assets/UI/InstructorC2.uxml:
     <VisualElement class="c2-root">
       <VisualElement class="status-bar">
         <Label name="app-title" text="TRISHUL-SIM · C2 STATION" />
         <VisualElement name="link-status-icon" class="status-icon status-waiting" />
         <Label name="link-status-label" text="○ Waiting for Trainee..." />
       </VisualElement>
       <DropdownField name="scenario-dropdown" label="Scenario" />
       <TextField name="trainee-id" label="Trainee ID (optional)" />
       <Button name="launch-btn" text="LAUNCH SCENARIO" />
       <Button name="abort-btn" text="ABORT" />
       <VisualElement name="scorecard-mirror" class="scorecard-panel hidden" />
     </VisualElement>
  
  2. Create Assets/UI/InstructorC2.uss:
     .c2-root { background-color: #0D0D0D; color: #E0E0E0; font-size: 14px; }
     .status-icon { width: 12px; height: 12px; border-radius: 6px; }
     .status-waiting { background-color: #888888; }
     .status-connected { background-color: #00B300; }  /* MIL-STD-2525 GREEN */
     .status-lost { background-color: #CC0000; }
     .hidden { display: none; }
     .threat-green { background-color: #00B300; }
     .threat-amber { background-color: #FFB300; }
     .threat-red { background-color: #CC0000; }
  
  3. Assets/Scripts/UI/C2StationController.cs — MonoBehaviour (UIDocument on C2 scene):
     - Awake(): GetComponent<UIDocument>().rootVisualElement; bind all named elements
     - Start(): populate scenario-dropdown from ScenarioLibrary.LoadAll() — names in current locale
     - Subscribe to WsServer.OnClientConnected → set status-icon class to "status-connected"
     - Subscribe to WsServer.OnClientDisconnected → "status-lost"
     - launch-btn.clicked → ScenarioManager.LaunchScenario(selectedScenario)
     - abort-btn.clicked → ScenarioManager.BroadcastScenarioEnd("INSTRUCTOR_ABORT")
     - Subscribe to WsClient.OnMessageReceived → if SCORECARD_PAYLOAD: show scorecard-mirror pane
       (stub: just show sessionId text for now)
  
  --- Trainee Cockpit UI (TraineeCockpit scene) ---
  
  4. Create Assets/UI/TraineeCockpit.uxml:
     <VisualElement class="cockpit-root">
       <VisualElement class="threat-indicator" name="threat-indicator">
         <Label name="threat-label" text="GREEN" />
       </VisualElement>
       <VisualElement class="sensor-row">
         <VisualElement name="rf-waterfall-pane" class="sensor-pane">
           <Label class="pane-title" text="RF WATERFALL" />
           <VisualElement name="rf-content" class="sensor-content" />  <!-- stub: grey box -->
         </VisualElement>
         <VisualElement name="doppler-pane" class="sensor-pane">
           <Label class="pane-title" text="MICRO-DOPPLER RADAR" />
           <VisualElement name="doppler-content" class="sensor-content" />
         </VisualElement>
         <VisualElement name="acoustic-pane" class="sensor-pane">
           <Label class="pane-title" text="ACOUSTIC SONAR" />
           <VisualElement name="acoustic-content" class="sensor-content" />
         </VisualElement>
       </VisualElement>
       <VisualElement name="controls-row" class="hidden" />  <!-- EW + SMASH — added in P1 -->
       <VisualElement name="scorecard-panel" class="hidden" />
     </VisualElement>
  
  5. Create Assets/UI/TraineeCockpit.uss: dark theme matching C2; sensor-pane has border #333;
     sensor-content is a placeholder grey box (background #1A1A1A; height 200px)
  
  6. Assets/Scripts/UI/CockpitController.cs — MonoBehaviour:
     - Subscribe to WsClient.OnMessageReceived
     - On SCENARIO_START: remove "hidden" class from controls-row; set threat-indicator to GREEN
     - On SCENARIO_TICK: update threat-indicator class (threat-green/amber/red from threatState);
       forward SensorReadings to SensorPaneController (stubbed for now — just log values)
     - On SCENARIO_END: show "Computing Scorecard…" overlay
     - On SCORECARD_PAYLOAD: show scorecard-panel (stub text for now)

DELIVERABLE:
  Assets/UI/InstructorC2.uxml + InstructorC2.uss
  Assets/UI/TraineeCockpit.uxml + TraineeCockpit.uss
  Assets/Scripts/UI/C2StationController.cs
  Assets/Scripts/UI/CockpitController.cs
  Both scenes updated to use UIDocument with respective UXML

TEST:
  Play InstructorC2: green status icon appears when Trainee connects. Dropdown shows ≥1 scenario.
  Play TraineeCockpit: 3 sensor pane placeholders visible (grey boxes, labelled). Threat indicator shows "GREEN".
  Trigger scenario: threat indicator updates to "RED" when swarm-ai emits RED (or mock data).
  Resolution: both screens fill correctly at 1920×1080 — no overflow, no clipping.

DEPENDS: unity-sim P0-A, P0-B     UNBLOCKS: UX-P0-B, all UX-P1 cards

DEMO: "Both screens render with correct layout at 1920×1080." (P0 gate · Beat 1 visual foundation)
PERSONA: ui-ux builder            STATUS: [ ] todo
```

---

### UX-P0-B · Unity Localization setup — Hindi/English CSV + language toggle [timebox: 4h]

```
CONTEXT:
  UX-P0-A complete — UITK shell exists with labelled elements.
  Bilingual UI is a PRD MUST item (Beat 6). Set up the localization infrastructure now
  so all labels added in P1 are automatically bilingual.
  
  Files to read before starting:
    • docs/PRD.md §4 MUST — "Bilingual UI — Hindi + English with MIL-STD-2525 tactical symbology"
    • docs/ARCHITECTURE.md §1 — "Unity Localization package (CSV backend, bundled); offline-capable;
      runtime language toggle"
    • board/contracts/scorecard.md §"Bilingual" — scenarioName vs scenarioNameHindi swap on toggle;
      metric labels have matching Hindi strings in localisation CSV
  
  Steps:
    1. Package Manager → com.unity.localization (Unity Localization) → install
    2. Edit → Project Settings → Localization → Add Locales: English (en) + Hindi (hi)
       Set Default Locale: en
    3. Create Assets/Localization/StringTable_en.csv and StringTable_hi.csv:
       Key,Value
       APP_TITLE,"TRISHUL-SIM · C2 STATION"
       LINK_WAITING,"○ Waiting for Trainee..."
       LINK_CONNECTED,"● LAN LINK CONFIRMED"
       LAUNCH_BTN,"LAUNCH SCENARIO"
       ABORT_BTN,"ABORT"
       THREAT_GREEN,"GREEN"
       THREAT_AMBER,"AMBER"
       THREAT_RED,"RED"
       SENSOR_RF_TITLE,"RF WATERFALL"
       SENSOR_DOPPLER_TITLE,"MICRO-DOPPLER RADAR"
       SENSOR_ACOUSTIC_TITLE,"ACOUSTIC SONAR"
       SCORECARD_TITLE,"TACTICAL PERFORMANCE SCORECARD"
       METRIC_TTD,"Time-to-Detection"
       METRIC_ROE,"ROE Compliance"
       METRIC_KINETIC_EFF,"Kinetic Efficiency"
       METRIC_AGENTS_NEUT,"Agents Neutralised"
       DOCTRINE_PASSED,"DOCTRINE PASSED"
       DOCTRINE_FAILED,"DOCTRINE FAILED"
       PDF_EXPORT_BTN,"Export PDF"
       (Hindi translations in StringTable_hi.csv using Devanagari Unicode strings)
    4. Import CSVs into Unity Localization string tables (via Localization Tables window)
    5. Assets/Scripts/UI/LocaleToggle.cs — MonoBehaviour:
       - ToggleLanguage(): LocalizationSettings.SelectedLocale = isEnglish ? hi : en
       - Add a toggle button to InstructorC2 UXML: <Button name="lang-toggle" text="हिंदी" />
       - Wire in C2StationController: lang-toggle.clicked → LocaleToggle.ToggleLanguage()
    6. Update C2StationController and CockpitController: replace hard-coded strings with
       LocalizedString references (UITK binding via LocalizeStringEvent or manual lookup)
    7. Ensure NotoSansDevanagari-Regular.ttf is in Assets/Fonts/ for TextMeshPro Hindi rendering

DELIVERABLE:
  Assets/Localization/StringTable_en.csv
  Assets/Localization/StringTable_hi.csv
  Assets/Scripts/UI/LocaleToggle.cs
  Unity Localization installed; locales configured; CSV imported

TEST:
  Play C2 screen in Editor. Click language toggle button. All labels switch to Hindi (Devanagari script).
  Click toggle again → back to English. Verify: "LAUNCH SCENARIO" ↔ Hindi equivalent.
  Verify: NotoSansDevanagari font renders correctly — no placeholder boxes.

DEPENDS: UX-P0-A               UNBLOCKS: UX-P2-B (bilingual build test)

DEMO: "Toggle button; Hindi renders correctly." (Beat 6 — bilingual UI)
PERSONA: ui-ux builder         STATUS: [ ] todo
```

---

## P1 · Core (09–16 Oct 2026)

> Gate: RF Waterfall scrolling · Doppler scope live · Acoustic FFT live · EW+SMASH controls operable · Scorecard renders on both screens

---

### UX-P1-A · Live sensor suite — RF Waterfall, Micro-Doppler scope, Acoustic FFT ⭐ [timebox: 4h]

```
CONTEXT:
  UX-P0-A complete — sensor pane placeholders exist in Cockpit UI.
  SA-P0-B (Friis model) and SA-P1-A (Doppler + Acoustic) should be complete.
  If not: use SensorMock fixture (rfWaterfallPeakDbm=-62.3, dopplerPeakHz=220.4, acousticSplDb=54.2).
  
  Files to read before starting:
    • docs/PRD.md §3 Beat 2 — "RF Waterfall (2.4 / 5.8 GHz / 915 MHz ELRS), Micro-Doppler Radar
      scope (PPI radar display), Acoustic FFT Sonar — all showing baseline noise"
    • docs/PRD.md §3 Beat 3 — "220 Hz acoustic spike appears. RF waterfall erupts at 5.825 GHz"
    • board/contracts/scenario-events.md — SCENARIO_TICK.sensorReadings shape
    • docs/SCHEMA.md Entity 3 — SensorReadings field values and semantics
  
  Rendering approach (ponytail — UITK custom draw + Unity Texture2D scrolling):
  
  RF Waterfall (scrolling spectrogram):
    - Allocate a Texture2D (256×128, RGB) representing frequency (X) × time (Y, scrolling)
    - Each SCENARIO_TICK: generate one row of pixels:
      X axis = 0–6 GHz mapped to 0–255 pixels
      Pixel intensity = received power (dBm) mapped to colour (black=noise floor -90dBm, cyan=-70, yellow=-50, red=-30)
      Scroll: shift texture rows up by 1 (Texture2D.GetPixels → shift → SetPixels → Apply)
      Plot rfWaterfallPeakDbm at rfWaterfallPeakGHz X position
    - Render: VisualElement with background = new StyleBackground(texture)
    - 3 frequency markers drawn as overlaid Labels: "2.4 GHz", "5.8 GHz", "915 MHz"
  
  Micro-Doppler scope (PPI radar display):
    - Allocate Texture2D (200×200) for circular PPI scope
    - Background: dark green grid circles (range rings at 25%, 50%, 75%, 100%)
    - Each TICK: draw radarDetectedCount blips at positions derived from agent bearing/distance
      (use SwarmAgentState positions projected to screen space)
      Blip: a 3×3 bright green square at agent position
      Decoys: larger blip (5×5); attackers: smaller (3×3)
    - dopplerPeakHz: display as a label below the scope "Doppler: XXX Hz"
    
  Acoustic FFT Sonar:
    - Simple bar chart: 8 frequency bands across 20–2000 Hz
    - acousticSplDb drives the amplitude of the dominant band (blade-tip frequency)
    - Below 40 dB: no bars visible (noise floor). 40–55: amber. >55: red flash.
    - Label: "SPL: XX.X dB"
  
  Steps:
    1. Assets/Scripts/UI/RfWaterfallRenderer.cs — MonoBehaviour:
       UpdateWaterfall(SensorReadings sr): adds one row to Texture2D scroll buffer
    2. Assets/Scripts/UI/DopplerScopeRenderer.cs:
       UpdateScope(SensorReadings sr, SwarmAgentState[] swarm): draws blips + Doppler label
    3. Assets/Scripts/UI/AcousticFftRenderer.cs:
       UpdateAcoustic(SensorReadings sr): updates bar heights + SPL label
    4. CockpitController.cs updated: on SCENARIO_TICK → call all 3 renderers
       (if swarm-ai not live: call with SensorMock.GetPunjabFixture())
    5. Wire UXML sensor-content divs to each renderer (replace grey stub boxes)

DELIVERABLE:
  Assets/Scripts/UI/RfWaterfallRenderer.cs
  Assets/Scripts/UI/DopplerScopeRenderer.cs
  Assets/Scripts/UI/AcousticFftRenderer.cs
  CockpitController.cs updated

TEST:
  Play Punjab scenario. RF Waterfall: scrolling spectrogram; bright strip at 5.825 GHz on approach.
  Doppler scope: 6 blips visible in correct formation; 2 large (decoys) + 4 small (attackers).
  Acoustic FFT: bar at ~220 Hz lights amber/red as agents approach.
  All 3 update smoothly at ~10 Hz without visible tearing.

DEPENDS: UX-P0-A, SA-P0-B, SA-P1-A     UNBLOCKS: UX-P1-B (visuals established; controls layer on top)

DEMO: ⭐ "Beat 2 and Beat 3 fully visual — sensors alive in pitch-black screen." (Beat 2 + 3)
PERSONA: ui-ux builder                  STATUS: [ ] todo
```

---

### UX-P1-B · EW Jammer controls + SMASH 2000 lead-angle reticle overlay ⭐ [timebox: 4h]

```
CONTEXT:
  UX-P1-A complete — sensor panes live. SA-P1-B-ii (HBT pincer) and SA-P1-D (SMASH) should be done.
  This card adds the interactive controls that let the trainee act during Beat 4.
  
  Files to read before starting:
    • docs/PRD.md §3 Beat 4 ⭐ — "locks Directional EW jammer on the attack axis...
      engages the SMASH 2000 fire-control lead-angle reticle on the lead FPV"
    • docs/PRD.md §4 MUST — "EW Jammer controls — directional, azimuth/elevation/band-select;
      ROE deconfliction (prevents friendly VHF blackout)"
    • docs/SCHEMA.md Entity 3 — TraineeAction (antennaBearingDeg, antennaElevationDeg, ewEngaged,
      ewBandGHz, kinetic); EWJammerState
    • board/contracts/telemetry.md — TraineeAction shape and KineticEvent semantics
  
  Steps:
  
  --- EW Jammer Controls ---
  
  1. Add to TraineeCockpit.uxml controls-row (un-hidden on SCENARIO_START):
     <VisualElement class="controls-panel">
       <Label text="EW JAMMER" class="panel-label" />
       <SliderInt name="ew-azimuth" label="Azimuth" low-value="0" high-value="359" />
       <SliderInt name="ew-elevation" label="Elevation" low-value="-90" high-value="90" />
       <DropdownField name="ew-band" label="Jam Band" />  <!-- choices: 2.4 GHz, 5.825 GHz, 0.915 GHz -->
       <Toggle name="ew-engage" label="ENGAGE" />
       <Label name="ew-roe-warning" class="roe-warning hidden" text="⚠ ROE VIOLATION — FRIENDLY BLACKOUT" />
     </VisualElement>
  
  2. Assets/Scripts/UI/EwJammerController.cs — MonoBehaviour:
     - Bind sliders, dropdown, toggle
     - Build TraineeAction on each change; pass to ScenarioManager.SetCurrentTraineeAction()
       (ScenarioManager reads this each tick to embed in SCENARIO_TICK.traineeAction)
     - On SCENARIO_TICK received: if ewJammerState.friendlyBlackoutRisk == true →
       show ew-roe-warning label (red flashing text)
  
  --- SMASH 2000 Lead-Angle Reticle ---
  
  3. SMASH 2000 overlay: a VisualElement rendered on top of the sensor panes
     - A crosshair circle (CSS border-radius:50%; border: 2px solid #00FF00)
     - Lead-angle indicator: a short line from crosshair to predicted impact point
       (position computed from: antenna bearing + velocityMs from targeted agent's SwarmAgentState)
     - Target lock: when trainee's antenna bearing aligns within 10° of an agent bearing
       → highlight that agent's Doppler blip in yellow + show "LOCKED: agent-X" label
     - FIRE button: <Button name="fire-btn" text="FIRE — 7.62mm" />
       fire-btn.clicked → set kineticFireTriggered=true in TraineeAction for ONE tick
       → SmashFireControl.ComputeKineticEvent() fires (swarm-ai SA-P1-D)
  
  4. Assets/Scripts/UI/SmashReticleController.cs:
     - UpdateReticle(SwarmAgentState[] swarm, TraineeAction action): compute target lock,
       move crosshair VisualElement, draw lead-angle indicator
     - OnKineticHit(KineticEvent ev): flash reticle green + show "HIT: agent-X" label 0.5s
     - OnKineticMiss(KineticEvent ev): flash reticle red + "MISS — adjust lead" 0.5s
  
  5. Wire CockpitController: each SCENARIO_TICK → SmashReticleController.UpdateReticle()

DELIVERABLE:
  EW Jammer controls wired in TraineeCockpit.uxml
  Assets/Scripts/UI/EwJammerController.cs
  Assets/Scripts/UI/SmashReticleController.cs

TEST:
  Play scenario. Move EW azimuth slider to 90° with band 0.915 GHz → "⚠ ROE VIOLATION" appears.
  Move slider to 247° (attack axis), band 5.825 GHz → ROE warning disappears.
  Click FIRE: "HIT: agent-3" flash if lead-angle < 5°; "MISS" if not.
  EW Engage toggle → agents visibly evade (verify via Doppler scope blip movement).

DEPENDS: UX-P1-A, SA-P1-B-ii, SA-P1-D     UNBLOCKS: UX-P1-C, UX-P1-D

DEMO: ⭐ "Trainee can operate EW and kinetic controls — the interaction at Beat 4." (Beat 4)
PERSONA: ui-ux builder                     STATUS: [x] done — 2026-10-06 (verified with UiShellSelfTest PASS)
```

---

### UX-P1-C · Split-screen layout compatibility [timebox: 4h]

```
CONTEXT:
  unity-sim P1-A (split-screen fallback) complete — SplitScreen.unity loads both scenes
  side-by-side using Camera.rect (C2 on left 0–50%, Cockpit on right 50–100%).
  This card verifies and adapts UITK layouts for the half-width viewport.
  
  Files to read before starting:
    • plans/unity-sim-phase-plan.md US-P1-A — Camera.rect split-screen implementation details
    • docs/PRD.md §4 MUST — "single-laptop split-screen fallback" (Beat 1/6)
    • docs/ARCHITECTURE.md §1 — UITK "scales to 1920×1080 and 4K without code change"
  
  UITK scaling context:
    Split-screen: each panel renders at 960×1080 (at 1920×1080 monitor).
    UITK uses Panel Settings with scale mode "Scale With Screen Size" → layouts reflow.
    Potential issues: sensor panes may be too wide; sliders may overflow; text may wrap awkwardly.
  
  Steps:
    1. Open SplitScreen.unity. Verify UITK UIDocument Panel Settings:
       C2: reference resolution 960×1080, screen match mode Width
       Cockpit: same
    2. Inspect C2 layout at 960 wide: scenario dropdown and launch button should not overlap
       Fix if needed: add flex-wrap: wrap to controls-panel; reduce font size from 14px to 12px in USS
    3. Inspect Cockpit layout at 960 wide:
       - 3 sensor panes: from horizontal row → may need to go 2-column for 960px
         Solution: use flex-direction: row with min-width: 280px per pane; let them wrap if needed
         OR: allow scroll in sensor-row (overflow: scroll)
       - EW sliders: verify they don't clip
       - SMASH reticle: adjust size if crosshair is too large in half-width
    4. Test at multiple resolutions: 1920×1080 (full), 1920×1080 split (960×1080), 1366×768 split
    5. Create a UI test checklist (Assets/UI/TestChecklist.md) noting confirmed resolutions

DELIVERABLE:
  TraineeCockpit.uss + C2StationController.cs + CockpitController.cs updated for split-screen
  Assets/UI/TestChecklist.md

TEST:
  SplitScreen.unity: both panels display without clipping at 1920×1080.
  Run Punjab scenario in split-screen: all 3 sensor panes visible in Cockpit half; C2 controls visible.
  No horizontal scroll bar needed.

DEPENDS: UX-P0-A, unity-sim P1-A     UNBLOCKS: UX-P1-D (layout stable before adding Scorecard)

DEMO: "Single laptop running full demo — both panels readable." (Beat 6)
PERSONA: ui-ux builder               STATUS: [ ] todo
```

---

### UX-P1-D · Scorecard Renderer + Interactive Timeline Scrubber ⭐ [timebox: 4h]

```
CONTEXT:
  UX-P1-C complete (layout stable). AA-P1-A (real ScorecardMetrics) and AA-P1-B
  (TimelinePlaybackController) should be complete.
  If not: use ScorecardMock.GetPunjabFixture() (static fixture from scorecard.md example).
  
  Files to read before starting:
    • board/contracts/scorecard.md §"UI rendering contract" — ALL required display elements with
      source field and pass/fail threshold for each. Also SCORECARD_PAYLOAD WebSocket message.
    • board/contracts/scorecard.md §"Bilingual" — scenarioName vs scenarioNameHindi on toggle
    • board/contracts/scorecard.md §"Threat-state colour coding" — MIL-STD-2525 palette:
      GREEN #00B300 · AMBER #FFB300 · RED #CC0000
    • docs/PRD.md §3 Beat 5 — "Scorecard on both screens; Interactive timeline scrubber;
      judge can replay any moment with spatial overlay"
  
  Steps:
  
  --- Scorecard Renderer ---
  
  1. Create Assets/UI/ScorecardPanel.uxml:
     <VisualElement class="scorecard-root">
       <Label name="scorecard-title" text="TACTICAL PERFORMANCE SCORECARD" />
       <Label name="scenario-name" />
       <VisualElement name="doctrine-badge" class="badge">
         <Label name="doctrine-text" text="DOCTRINE PASSED" />
       </VisualElement>
       <!-- Metrics table as 4-column grid -->
       <VisualElement class="metrics-table">
         <!-- Rows generated at runtime by ScorecardPanelController -->
       </VisualElement>
       <!-- Timeline Scrubber -->
       <VisualElement class="timeline-section">
         <Slider name="timeline-slider" low-value="0" high-value="100" />
         <Label name="timeline-time" text="T+0s" />
         <VisualElement name="spatial-overlay" class="spatial-overlay" />
       </VisualElement>
       <Button name="pdf-export-btn" text="Export PDF" />
     </VisualElement>
  
  2. Assets/Scripts/UI/ScorecardPanelController.cs:
     - Bind(Scorecard sc): populate all fields
       • scenario-name: sc.scenarioName (or sc.scenarioNameHindi based on current locale)
       • doctrine-badge: add class "badge-pass" (#00B300) or "badge-fail" (#CC0000)
       • metrics-table: generate rows dynamically:
         For each metric, add a VisualElement row with 4 Labels:
           [Metric Name (localised)] [Value] [Benchmark] [✓ PASS / ✗ FAIL (coloured)]
         Rows:
           Time-to-Detection: sc.metrics.timeToDetectionMs ms / ≤ sc.doctrineBenchmarks.timeToDetectionThresholdMs ms
           ROE Compliance: sc.metrics.roeCompliance / ≥ 1.0
           Kinetic Efficiency: format as % / ≥ 50%
           Agents Neutralised: sc.metrics.agentsNeutralised / display only
           Premature EW Penalty: sc.metrics.prematureEwPenalty ? "YES (−)" : "NO (✓)" / no threshold
       • timelineIndex loaded; slider max-value = sc.timelineIndex[^1] (last t value)
     - pdf-export-btn.clicked → PdfExporter.Export(sc) (AA-P2-A; stub log if not yet available)
  
  3. Timeline Scrubber:
     - timeline-slider.RegisterValueChangedCallback: on change →
       TimelinePlaybackController.SetScrubPosition(Mathf.RoundToInt(slider.value * 1000))
       (slider value = seconds; multiply by 1000 for ms)
     - On CurrentRecord change: update timeline-time label "T+{t/1000:F0}s"
     - Spatial overlay: render swarm positions from CurrentRecord.swarmSnapshot:
       For each agent: draw a dot (VisualElement small circle) at projected screen position
       Color: decoy = blue, attacker = red, neutralised = grey
       (Use same projection as DopplerScopeRenderer — bearing from trainee perspective)
  
  4. CockpitController.cs: on SCORECARD_PAYLOAD → deserialize Scorecard → ScorecardPanelController.Bind()
     Show scorecard-panel; hide computing overlay
     C2StationController.cs: same binding for scorecard-mirror panel

DELIVERABLE:
  Assets/UI/ScorecardPanel.uxml
  Assets/Scripts/UI/ScorecardPanelController.cs
  CockpitController.cs + C2StationController.cs updated

TEST:
  Run Punjab scenario to end. Within 10 s:
  - Scorecard panel appears on BOTH screens simultaneously
  - Doctrine badge is green or red (real result from AA-P1-A, or mock value if not ready)
  - 5 metric rows visible with values and pass/fail indicators
  - Timeline slider navigable: drag to T+45s → spatial overlay shows 6 drone positions at that moment
  - Language toggle: "TACTICAL PERFORMANCE SCORECARD" ↔ Hindi equivalent

DEPENDS: UX-P1-C, AA-P1-A, AA-P1-B     UNBLOCKS: UX-P2-A (PDF button target exists)

DEMO: ⭐ "Beat 5 fully visual on both screens simultaneously — judge scrubs to 'the moment he fired'." (Beat 5)
PERSONA: ui-ux builder                  STATUS: [x] done
```

---

## P2 · Polish & Hardening (16–19 Oct 2026)

---

### UX-P2-A · PDF Export button + wiring to PdfExporter [timebox: 4h]

```
CONTEXT:
  AA-P2-A (PdfExporter.Export() exists) must be complete.
  UX-P1-D (pdf-export-btn exists as stub) complete.
  
  Files to read before starting:
    • board/contracts/scorecard.md §"PDF export shape" — ordered content; reminder that
      PdfExporter is aar-engine's responsibility; ui-ux just calls it and handles the result
    • docs/PRD.md §4 MUST — "PDF Scorecard export — one-click export of Tactical Performance Dossier"
  
  Steps:
    1. ScorecardPanelController.pdf-export-btn.clicked callback:
       - byte[] pdfBytes = PdfExporter.Export(currentScorecard)
       - Write to %AppData%\TRISHUL-SIM\sessions\<sessionId>.pdf (if not already written by aar-engine)
         OR: call PdfExporter.WriteToFile(currentScorecard) which returns the path
       - Open file with System.Diagnostics.Process.Start(pdfPath):
         This opens the default PDF viewer (Windows) — satisfies "judge gets a PDF in one click"
         Fallback: copy path to clipboard + show dialog "PDF saved to: <path>"
    2. Show a loading spinner overlay while PDF generates (PDF generation may take 1-2 s for sparkline)
       Use a simple animated VisualElement (CSS animation: rotate 360° in 1s)
    3. Error handling: if PdfExporter throws → show error label "PDF export failed — check disk space"
    4. Update lang-toggle: PDF export button label localised ("Export PDF" ↔ Hindi)

DELIVERABLE:
  ScorecardPanelController.cs updated with real PDF wiring
  %AppData%\TRISHUL-SIM\sessions\<sessionId>.pdf  (generated at runtime)

TEST:
  Run scenario to end. Click "Export PDF" on Scorecard panel.
  Windows PDF viewer opens the document within 3 s.
  PDF contains all 6 sections from scorecard.md (check visually).

DEPENDS: AA-P2-A, UX-P1-D     UNBLOCKS: UX-P2-C (P2 smoke)

DEMO: "Judge gets a PDF in one click." (Beat 6)
PERSONA: ui-ux builder         STATUS: [ ] todo
```

---

### UX-P2-B · Full bilingual pass — all P1 labels, scenario names, MIL-STD-2525 symbology [timebox: 4h]

```
CONTEXT:
  UX-P0-B set up Unity Localization with a base CSV. UX-P1-A through UX-P1-D added many
  new labels. This card audits and completes all localisation.
  
  Files to read before starting:
    • board/contracts/scorecard.md — all metric label names that need Hindi strings
    • docs/PRD.md §4 MUST — "MIL-STD-2525 tactical symbology and colour-coded threat state"
    • Assets/Localization/StringTable_en.csv — current state (add all new keys)
  
  Steps:
    1. Audit all UITK Label.text values added in P1: collect all hard-coded strings
    2. Add keys to StringTable_en.csv + StringTable_hi.csv for every new string:
       EW_JAMMER_PANEL, AZIMUTH_LABEL, ELEVATION_LABEL, JAM_BAND_LABEL, ENGAGE_LABEL,
       ROE_WARNING, SMASH_TITLE, FIRE_BTN, HIT_LABEL, MISS_LABEL,
       COMPUTING_SCORECARD, TIMELINE_SCRUBBER_LABEL, etc.
    3. MIL-STD-2525 tactical symbology:
       - Threat-state indicators: use MIL-STD-2525 colours confirmed in scorecard.md
         GREEN #00B300 · AMBER #FFB300 · RED #CC0000
       - Agent icons in spatial overlay: use standard C2 symbols if feasible in UITK
         Minimum: colour-coded circles (decoy=blue, attacker=red, neutralised=grey) with label
         Ideal: MIL-STD-2525 UAS icon (a simplified SVG if royalty-free version available)
    4. Wire ALL remaining LocalizedString references in controllers
    5. Final test: play scenario in Hindi mode end-to-end; no English text visible except
       proper nouns (TRISHUL-SIM brand name, session IDs)

DELIVERABLE:
  StringTable_en.csv + StringTable_hi.csv fully populated
  All UITK Labels using LocalizedString (no hard-coded English strings except brand name)

TEST:
  Launch in Hindi mode. Run full demo path (all 6 beats). No English labels visible.
  Switch to English mid-scenario — all labels switch immediately.

DEPENDS: UX-P0-B, UX-P1-D, UX-P1-B     UNBLOCKS: UX-P2-C

DEMO: "Hindi/English toggle — full bilingual demo path." (Beat 6)
PERSONA: ui-ux builder                  STATUS: [ ] todo
```

---

### UX-P2-C · Threat-state colour coding + MIL-STD-2525 consistency pass [timebox: 4h]

```
CONTEXT:
  UX-P2-B complete. This card enforces visual consistency of the MIL-STD-2525 colour palette
  across ALL UI elements and adds the night/fog visual ambiance for the demo scenario.
  
  Files to read before starting:
    • board/contracts/scorecard.md §"Threat-state colour coding" — GREEN #00B300, AMBER #FFB300, RED #CC0000
    • docs/PRD.md §3 Beat 4 ⭐ — "pitch-black screen, two sensor panes, one correct decision"
  
  Steps:
    1. Global USS audit: grep all colour values in *.uss → replace with USS custom properties:
       :root { --mil-green: #00B300; --mil-amber: #FFB300; --mil-red: #CC0000; --bg: #0D0D0D; }
    2. Night/fog ambience for Punjab Pincer demo:
       - TraineeCockpit background: #000000 (pitch black) when WeatherPreset.isNight == true
       - A CSS fog overlay: VisualElement with opacity: 0.15, background: #B0C4DE (light blue haze)
       - Applied: CockpitController reads scenarioConfig.weatherPreset.isNight → adds "night-mode" USS class
    3. Threat indicator pulses: when threatState = RED → CSS animation (pulse red glow on indicator)
       @keyframes pulse { 0% { opacity: 1.0; } 50% { opacity: 0.4; } 100% { opacity: 1.0; } }
       transition: 0.3s
    4. Sensor pane borders: GREEN pane borders in SCENARIO normal; AMBER at first detection;
       RED when threat is RED
    5. Final: run full demo path — verify the pitch-black + sensor-only aesthetic of Beat 4

DELIVERABLE:
  All *.uss files updated with CSS custom properties
  Night/fog ambience implemented
  Pulsing threat indicator

TEST:
  Launch Punjab Pincer (night=true): black background. Sensors glow. Threat indicator pulses red at Beat 3.
  Switch to Desert FOB scenario (day=false): lighter background, same threat colours.

DEPENDS: UX-P2-B               UNBLOCKS: UX-P2-D (final smoke)

DEMO: ⭐ "Pitch-black screen + two sensor panes + pulsing RED indicator — visual wow moment." (Beat 4 aesthetics)
PERSONA: ui-ux builder         STATUS: [ ] todo
```

---

### UX-P2-D · Full demo-path smoke — all 6 beats, both screen modes [timebox: 2h]

```
CONTEXT:
  All ui-ux features complete. This card runs the full demo path as a structured smoke test.
  
  Files to read before starting:
    • docs/PRD.md §3 — 6-beat demo narrative (exact timing: T+0, T+15, T+30, T+45, T+55, T+70)
    • docs/BUILD-PLAN.md P2 — "Warden review pass: demo path smoke"
  
  Steps:
    1. Two-laptop mode: run full demo path exactly as in PRD §3; verify each beat on both screens
    2. Split-screen mode: same demo path; verify C2 left + Cockpit right both readable
    3. Hindi mode: run demo path; verify no English labels bleed through
    4. Fill in UI test checklist (Assets/UI/TestChecklist.md):
       [ ] Beat 1: green status icon on C2; scenario dropdown shows 4 scenarios
       [ ] Beat 2: 3 sensor panes alive; baseline noise visible (no data artifacts)
       [ ] Beat 3: RF spike at 5.825 GHz visible; Doppler blips split; acoustic bar rises
       [ ] Beat 4: EW + SMASH controls operable; ROE warning triggers on wrong band; hit registered
       [ ] Beat 5: Scorecard on both screens within 10 s; scrubber navigable; PDF exports
       [ ] Beat 6: Hindi toggle; split-screen; PDF in one click; no internet required
    5. Log any failures → fix immediately (2h budget includes 1h buffer for minor fixes)
    6. Update board/STATUS.md: "ui-ux: smoke PASSED — <date>"

DELIVERABLE:
  Assets/UI/TestChecklist.md fully ticked (all 6 beats)
  board/STATUS.md updated

TEST:
  All 6 checkboxes ticked without skipping. Two-laptop and split-screen both complete.

DEPENDS: UX-P2-A, UX-P2-B, UX-P2-C     UNBLOCKS: UX-P3-A (endgame polish)

DEMO: "All 6 demo beats pass in sequence — ready for judges." (master gate)
PERSONA: ui-ux builder                  STATUS: [ ] todo
```

---

## P3 · Endgame (19–20 Oct 2026)

---

### UX-P3-A · Demo-script alignment + UI freeze [timebox: 2h]

```
CONTEXT:
  Feature freeze: 19 Oct ~21:00. This card aligns UI states with the demo script
  and ensures no last-minute changes break the path.
  
  Files to read before starting:
    • docs/PRD.md §5 — "Wow moment lands live" definition
    • docs/BUILD-PLAN.md Freeze calendar
    • .agent/rules/40-endgame.md — last-6-hours doctrine
  
  Steps:
    1. Read docs/DEMO-SCRIPT.md (if written by Herald); sync UI label text to match exact
       words the presenter will say (e.g., if script says "Punjab Border — Zero Visibility Pincer"
       verify dropdown shows exactly that string in English mode)
    2. Freeze: mark all .uxml, .uss files as read-only in git (git update-index --chmod=-w) —
       prevents accidental last-minute edits
    3. Verify: app window title says "TRISHUL-SIM C-UAS Trainer" (not Unity's default)
    4. Verify: cursor is always visible; no fullscreen escape key conflict
    5. Disable Unity splash screen in Project Settings (requires Unity Plus/Pro; or use free tier override)
    6. Update board/STATUS.md: "ui-ux: FROZEN — 19 Oct <time>"

DELIVERABLE:
  All UI files frozen in git
  board/STATUS.md updated (FROZEN status)

TEST:
  Cold launch of TRISHUL-SIM.exe: UI appears immediately, no Unity splash.
  Dropdown, toggle, scenario launch all work first click.

DEPENDS: UX-P2-D     UNBLOCKS: Submission (Patch)

DEMO: "Demo-ready UI — press Launch, demo runs, no surprises." (implicit in every beat)
PERSONA: ui-ux builder     STATUS: [ ] todo
```

---

## Oracle Pass — Wow Moment Analysis

| Card | Demo beat | Oracle verdict |
|---|---|---|
| UX-P0-A | Beat 1 (both screens render) | ⭐ **never-cut** — no UI = no demo |
| UX-P0-B | Beat 6 (bilingual foundation) | ⭐ **never-cut** — set up now or retrofit 50+ labels under pressure |
| UX-P1-A | Beat 2 + 3 (sensor panes live) | ⭐ **never-cut** — 3 live sensors ARE the demo spectacle |
| UX-P1-B | Beat 4 (EW + SMASH controls) | ⭐ **never-cut** — trainee interaction IS the wow moment |
| UX-P1-C | Beat 6 (split-screen layout) | ⭐ **never-cut** — demo insurance; judges may have 1 laptop |
| UX-P1-D | Beat 5 (Scorecard + scrubber) | ⭐ **never-cut** — "show me the moment he fired" is a judge Q |
| UX-P2-A | Beat 6 (PDF export) | never-cut — PRD MUST; MoD demo requires dossier |
| UX-P2-B | Beat 6 (full bilingual) | never-cut — SIH26247 is a government problem; Hindi is expected |
| UX-P2-C | Beat 4 (night/fog aesthetic) | ⭐ **never-cut** — "pitch-black screen" is the visual identity of the wow |
| UX-P2-D | Master smoke gate | never-cut — 2h that catches everything |
| UX-P3-A | Endgame freeze | high-value — 2h prevents stupid last-minute breaks |

**P3 cut order (if endgame is tight):** UX-P3-A (compress to 30 min) → UX-P2-C (keep colours, skip fog layer) → UX-P2-B (keep English-only, add Hindi in last hour only if stable)
