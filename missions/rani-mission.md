# Mission Brief — Rani · `ui-ux` (P0 + P1 first half + P2-D smoke)
> Generated: 2026-10-04 · Assigned by: Orion
> Track: `ui-ux` · Budget: 18h planned / 28h usable (64%)
> Skeleton deadline: **09 Oct 2026 EOD** · Feature freeze: 19 Oct ~21:00 IST

---

## Project one-liner

*"The first battalion-deployable C-UAS trainer that teaches Indian soldiers to defeat a drone swarm in the dark — for ₹10 a seat."*
Full demo narrative → `docs/PRD.md §3`. Your work is the **visual soul of the demo**: the UITK shell that greets the judge (Beat 1), the three live sensor panes (Beat 2/3), and the EW + SMASH controls (Beat 4).

## Mission goal

Build the UITK shell for both stations (C2 + Cockpit), set up bilingual localization (Hindi/English), wire the three live sensor panes (RF Waterfall scrolling spectrogram, Micro-Doppler scope, Acoustic FFT bars), and the EW Jammer + SMASH 2000 controls — **all the visual and interactive surfaces the trainee touches during the demo**.

**Victory definition:** Punjab scenario plays with all 3 sensor panes alive at 10 Hz, EW and SMASH controls operable (ROE warning fires on wrong band, kinetic hit registered), and the full 6-beat demo path passes smoke.

---

## Contracts

| Contract | Your role | File |
|---|---|---|
| `scenario-events.md` | **CONSUMER** — react to SCENARIO_START (show panes), TICK (redraw sensors, update threat state), END (show overlay) | `board/contracts/scenario-events.md` |
| `scorecard.md` | **CONSUMER** — render Scorecard UI elements (Rajeswari's card does this; your UX-P0-A stubs the panel shape) | `board/contracts/scorecard.md` |
| `telemetry.md` | **CONSUMER** — TraineeAction shape you produce each tick from UI inputs | `board/contracts/telemetry.md` |

**You own:** `Assets/UI/*.uxml` · `Assets/UI/*.uss` · `Assets/Scripts/UI/C2StationController.cs` · `Assets/Scripts/UI/CockpitController.cs` · `Assets/Scripts/UI/RfWaterfallRenderer.cs` · `Assets/Scripts/UI/DopplerScopeRenderer.cs` · `Assets/Scripts/UI/AcousticFftRenderer.cs` · `Assets/Scripts/UI/EwJammerController.cs` · `Assets/Scripts/UI/SmashReticleController.cs` · `Assets/Localization/`
**You do NOT touch:** `Assets/Scripts/Network/` · `Assets/Scripts/SwarmAI/` · `Assets/Scripts/AAR/`

## Stack rules to load

- `docs/ARCHITECTURE.md §1` — UITK + USS; Unity Localization (CSV backend); TextMeshPro; scalable to 1920×1080 + 4K
- `board/contracts/scorecard.md §"UI rendering contract"` — required Scorecard display elements
- `board/contracts/scorecard.md §"Threat-state colour coding"` — MIL-STD-2525: GREEN #00B300, AMBER #FFB300, RED #CC0000
- Unity 2022 docs: UIDocument, VisualElement, StyleSheet, SliderInt, DropdownField, Toggle, Texture2D

## Branch names

| Card | Branch |
|---|---|
| UX-P0-A | `feat/ui-ux-uitk-shell` |
| UX-P0-B | `feat/ui-ux-localization` |
| UX-P1-A | `feat/ui-ux-sensor-panes` |
| UX-P1-B | `feat/ui-ux-ew-smash-controls` |
| UX-P2-D | `feat/ui-ux-demo-smoke` |

## WAITS-ON

| Your card | Waits on | Mock path |
|---|---|---|
| UX-P0-A UITK shell | **Vinod: US-P0-A + US-P0-B** (WsServer.ConnectedCount; SCENARIO_START received by Trainee) | If delayed: stub ConnectedCount=1 (hard-code for editor testing); mock SCENARIO_START by calling OnScenarioStart() directly |
| UX-P1-A sensor panes | **Venu: SA-P0-B** (SensorReadings.rfWaterfallPeakDbm live) + **SA-P1-A** (dopplerPeakHz + acousticSplDb) | **Use SensorMock immediately:** `SensorReadings mock = new() { rfWaterfallPeakGHz=5.825f, rfWaterfallPeakDbm=-62.3f, dopplerPeakHz=220.4f, acousticSplDb=54.2f, radarDetectedCount=4 }` — renders real-looking data before physics is live |
| UX-P1-B EW+SMASH | **Venu: SA-P1-B-ii** (HBT pincer live for EW evasion to be visible) + **Uday: SA-P1-D** (KineticEvent for SMASH hit feedback) | Build and test controls against mock KineticEvent fixtures; EW evasion not visible until SA-P1-B-ii lands |
| UX-P2-D smoke | All P1 cards from all tracks | Cannot be mocked — this is the real integration test |

---

## Your cards

### UX-P0-A · UITK shell — C2 Station + Trainee Cockpit layout, status icon, sensor stubs [timebox: 4h]
```
CONTEXT:
  Vinod's US-P0-A (WsServer/WsClient + ConnectedCount) + US-P0-B (ScenarioManager) should be merged.
  This card replaces Vinod's stub ConnectionStatusUI.cs (plain uGUI Text) with full UITK.
  Sohail's AA-P0-A + AA-P0-B may not be done — stub Scorecard panel with a label for now.

  Files:
    • docs/ARCHITECTURE.md §1 — "UITK + USS; TextMeshPro; scales to 1920×1080 and 4K"
    • board/contracts/scenario-events.md consumer reactions for SCENARIO_START (hide lobby; show panes; GREEN)
    • docs/PRD.md §3 Beat 1 (green LAN icon) + Beat 2 (three sensor panes)
    • board/contracts/scorecard.md §"Threat-state colour coding" — colours for status icon

  Target directories: Assets/UI/ + Assets/Scripts/UI/

  --- C2 STATION UXML (Assets/UI/InstructorC2.uxml) ---
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
    <Button name="lang-toggle" text="हिंदी" />
    <VisualElement name="scorecard-mirror" class="scorecard-panel hidden" />
  </VisualElement>

  --- C2 USS (Assets/UI/InstructorC2.uss) ---
  .c2-root { background-color: #0D0D0D; color: #E0E0E0; font-size: 14px; }
  .status-icon { width: 12px; height: 12px; border-radius: 6px; }
  .status-waiting { background-color: #888888; }
  .status-connected { background-color: #00B300; }
  .status-lost { background-color: #CC0000; }
  .hidden { display: none; }

  --- TRAINEE COCKPIT UXML (Assets/UI/TraineeCockpit.uxml) ---
  <VisualElement class="cockpit-root">
    <VisualElement name="threat-indicator" class="threat-indicator threat-green">
      <Label name="threat-label" text="GREEN" />
    </VisualElement>
    <VisualElement class="sensor-row">
      <VisualElement name="rf-waterfall-pane" class="sensor-pane">
        <Label class="pane-title" text="RF WATERFALL" />
        <VisualElement name="rf-content" class="sensor-content" />
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
    <VisualElement name="controls-row" class="hidden" />
    <VisualElement name="scorecard-panel" class="hidden" />
  </VisualElement>

  Steps:
    1. Create Assets/UI/*.uxml + *.uss as above
    2. Assets/Scripts/UI/C2StationController.cs (UIDocument on C2 scene):
       - Awake(): bind all named elements
       - Start(): populate scenario-dropdown from ScenarioLibrary.LoadAll()
       - Subscribe WsServer.OnClientConnected → status-icon class "status-connected"
       - Subscribe WsServer.OnClientDisconnected → "status-lost"
       - launch-btn.clicked → ScenarioManager.LaunchScenario(selected)
       - abort-btn.clicked → ScenarioManager.BroadcastScenarioEnd("INSTRUCTOR_ABORT")
       - On SCORECARD_PAYLOAD: show scorecard-mirror pane (stub label with sessionId for now)
    3. Assets/Scripts/UI/CockpitController.cs (UIDocument on Cockpit scene):
       - On SCENARIO_START: remove "hidden" from controls-row; set threat-indicator to GREEN
       - On SCENARIO_TICK: update threat-indicator class (threat-green/amber/red);
         forward SensorReadings to renderer stubs (stub: just log values for now)
       - On SCENARIO_END: show "Computing Scorecard…" overlay label
       - On SCORECARD_PAYLOAD: show scorecard-panel (stub "Scorecard received" label for now)
    4. Both scenes: add UIDocument component, assign respective UXML + Panel Settings
    5. Remove Vinod's stub ConnectionStatusUI.cs (old uGUI Text)

DELIVERABLE:
  Assets/UI/InstructorC2.uxml + InstructorC2.uss
  Assets/UI/TraineeCockpit.uxml + TraineeCockpit.uss
  Assets/Scripts/UI/C2StationController.cs
  Assets/Scripts/UI/CockpitController.cs
  Assets/Scripts/UI/UiShellSelfTest.cs

TEST:
  UiShellSelfTest PASS: csc.exe /main:TrishulSim.UI.UiShellSelfTest Assets\Scripts\UI\*.cs
  Play InstructorC2: green status icon when Trainee connects; dropdown shows ≥1 scenario.
  Play TraineeCockpit: 3 grey sensor pane boxes labelled; GREEN threat indicator visible.
  Trigger scenario: threat indicator updates on TICK. Resolution: 1920×1080 — no overflow.
DEMO: "Both screens render with correct layout at 1920×1080." (P0 gate · Beat 1 foundation)
STATUS: [x] done — 2026-10-05 (verified with UiShellSelfTest PASS)
```

---

### UX-P0-B · Unity Localization setup — Hindi/English CSV + language toggle [timebox: 4h]
```
CONTEXT:
  UX-P0-A merged — UITK shell with labelled elements exists.
  Files: docs/PRD.md §4 MUST ("Bilingual UI — Hindi + English"),
  docs/ARCHITECTURE.md §1 ("Unity Localization package, CSV backend, bundled, runtime toggle").
  board/contracts/scorecard.md §"Bilingual" — metric labels need Hindi strings.

  Steps:
    1. Package Manager → com.unity.localization → install
    2. Edit → Project Settings → Localization → Add Locales: English (en) + Hindi (hi); Default: en
    3. Create Assets/Localization/StringTable_en.csv:
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
       EW_JAMMER_PANEL,"EW JAMMER"
       FIRE_BTN,"FIRE — 7.62mm"
       ROE_WARNING,"⚠ ROE VIOLATION — FRIENDLY BLACKOUT"
       COMPUTING_SCORECARD,"Computing Scorecard..."
    4. Create Assets/Localization/StringTable_hi.csv with Hindi translations for all keys
       (Use NotoSansDevanagari-Regular.ttf — free from Google Fonts → Assets/Fonts/)
    5. Import CSVs into Unity Localization string tables (via Localization Tables window in Unity)
    6. Assets/Scripts/UI/LocaleToggle.cs:
       ToggleLanguage(): LocalizationSettings.SelectedLocale = isEnglish ? hi : en
    7. Wire lang-toggle button in C2StationController → LocaleToggle.ToggleLanguage()
    8. Update C2StationController + CockpitController: replace hard-coded strings with
       LocalizedString.GetLocalizedString(key) calls

DELIVERABLE:
  Assets/Localization/StringTable_en.csv + StringTable_hi.csv
  Assets/Scripts/UI/LocaleToggle.cs
  Unity Localization installed; locales configured; CSVs imported
  Assets/Fonts/NotoSansDevanagari-Regular.ttf

TEST:
  Play C2 → click lang toggle → all labels switch to Hindi (Devanagari script).
  Toggle again → English. NotoSansDevanagari renders correctly (no placeholder boxes).
DEMO: "Toggle button; Hindi renders correctly." (Beat 6)
STATUS: [ ] todo
```

---

### UX-P1-A · Live sensor suite — RF Waterfall, Micro-Doppler scope, Acoustic FFT ⭐ [timebox: 4h]
```
CONTEXT:
  UX-P0-A merged. Venu's SA-P0-B (Friis) + SA-P1-A (Doppler + Acoustic) should be done.
  If not: use SensorMock inline (rfWaterfallPeakDbm=-62.3, dopplerPeakHz=220.4, acousticSplDb=54.2)

  Files: docs/PRD.md §3 Beat 2+3, board/contracts/scenario-events.md SCENARIO_TICK.sensorReadings,
  docs/SCHEMA.md Entity 3 SensorReadings fields.

  RF WATERFALL (scrolling spectrogram):
    Texture2D(256×128, RGB). Each TICK: one row of pixels.
    X axis = 0–6 GHz mapped to 0–255 pixels.
    Pixel colour: black=-90dBm, cyan=-70, yellow=-50, red=-30 (lerp between stops)
    Scroll: GetPixels shift up 1 row + SetPixels + Apply()
    Plot rfWaterfallPeakDbm at rfWaterfallPeakGHz X position
    Render as StyleBackground on rf-content VisualElement.
    3 frequency markers overlaid: "2.4 GHz" "5.8 GHz" "915 MHz"

  MICRO-DOPPLER SCOPE (PPI circular radar):
    Texture2D(200×200). Background: dark green grid circles (range rings 25/50/75/100%)
    Each TICK: draw radarDetectedCount blips at bearing/distance from SwarmAgentState positions
    Decoys: 5×5 bright green square; attackers: 3×3.
    dopplerPeakHz: label below scope "Doppler: XXX Hz"

  ACOUSTIC FFT SONAR (bar chart):
    8 bands across 20–2000 Hz.
    acousticSplDb drives amplitude of dominant band (blade-tip ~220 Hz).
    Below 40 dB: no bars; 40–55: amber; >55: red flash.
    Label: "SPL: XX.X dB"

  Steps:
    1. RfWaterfallRenderer.cs: UpdateWaterfall(SensorReadings sr) — Texture2D scroll logic
    2. DopplerScopeRenderer.cs: UpdateScope(SensorReadings sr, SwarmAgentState[] swarm)
    3. AcousticFftRenderer.cs: UpdateAcoustic(SensorReadings sr)
    4. CockpitController.cs: on SCENARIO_TICK → call all 3 renderers
       (if swarm-ai not live: inject SensorMock values)
    5. Wire UXML sensor-content divs to each renderer (replace grey stub boxes)

DELIVERABLE:
  Assets/Scripts/UI/RfWaterfallRenderer.cs
  Assets/Scripts/UI/DopplerScopeRenderer.cs
  Assets/Scripts/UI/AcousticFftRenderer.cs
  CockpitController.cs updated

TEST:
  Play Punjab. RF Waterfall: scrolling spectrogram; bright strip at 5.825 GHz on approach.
  Doppler scope: 6 blips in formation; 2 large + 4 small.
  Acoustic FFT: bar at ~220 Hz lights amber/red as agents approach.
  All 3 update smoothly at ~10 Hz.
DEMO: ⭐ "Beat 2 and Beat 3 fully visual — sensors alive." (Beat 2 + 3)
STATUS: [x] done — 2026-10-06 (verified with UiShellSelfTest PASS: 3 live renderers, spectrogram scroll, PPI scope blips, 8-band FFT)
```

---

### UX-P1-B · EW Jammer controls + SMASH 2000 lead-angle reticle overlay ⭐ [timebox: 4h]
```
CONTEXT:
  UX-P1-A merged. Venu's SA-P1-B-ii + Uday's SA-P1-D should be done.
  If not: build controls and test against mock KineticEvent.

  Files: docs/PRD.md §3 Beat 4 ⭐, docs/SCHEMA.md Entity 3 TraineeAction + EWJammerState,
  board/contracts/telemetry.md TraineeAction shape.

  EW JAMMER CONTROLS (add to controls-row in TraineeCockpit.uxml):
  <VisualElement class="controls-panel">
    <Label text="EW JAMMER" />
    <SliderInt name="ew-azimuth" label="Azimuth" low-value="0" high-value="359" />
    <SliderInt name="ew-elevation" label="Elevation" low-value="-90" high-value="90" />
    <DropdownField name="ew-band" label="Jam Band" />  <!-- 2.4 GHz, 5.825 GHz, 0.915 GHz -->
    <Toggle name="ew-engage" label="ENGAGE" />
    <Label name="ew-roe-warning" class="roe-warning hidden" text="⚠ ROE VIOLATION — FRIENDLY BLACKOUT" />
  </VisualElement>

  SMASH 2000 RETICLE:
    Crosshair: VisualElement (CSS border-radius:50%; border: 2px solid #00FF00)
    Lead-angle indicator: short line from crosshair toward predicted impact point
    Target lock: when antenna bearing within 10° of agent → highlight blip yellow + "LOCKED: agent-X"
    FIRE button: on click → set kineticFireTriggered=true in TraineeAction for ONE tick

  Steps:
    1. EwJammerController.cs:
       Bind sliders, dropdown, toggle.
       Build EWJammerState each change → ScenarioManager.SetCurrentEwState(ewState)
       On SCENARIO_TICK: if ewJammerState.friendlyBlackoutRisk → show roe-warning (red flash)
    2. SmashReticleController.cs:
       UpdateReticle(SwarmAgentState[], TraineeAction): compute target lock; move crosshair VE
       OnKineticHit(KineticEvent ev): flash green + "HIT: agent-X" 0.5s
       OnKineticMiss(KineticEvent ev): flash red + "MISS — adjust lead" 0.5s
    3. CockpitController: each SCENARIO_TICK → SmashReticleController.UpdateReticle()
    4. Add "FIRE" button to controls-row UXML → calls SmashReticleController.TriggerFire()

DELIVERABLE:
  EW Jammer controls in TraineeCockpit.uxml
  Assets/Scripts/UI/EwJammerController.cs
  Assets/Scripts/UI/SmashReticleController.cs

TEST:
  EW azimuth=90°, band=0.915 GHz → "⚠ ROE VIOLATION" appears.
  EW azimuth=247°, band=5.825 GHz → ROE warning disappears.
  Click FIRE: "HIT: agent-3" or "MISS — adjust lead" flash.
DEMO: ⭐ "Trainee operates EW and kinetic controls — Beat 4 interaction." (Beat 4)
STATUS: [x] done — 2026-10-06 (verified with UiShellSelfTest PASS: EW controls, ROE blackout warning, SMASH reticle lock, lead-angle, kinetic hit/miss)
```

---

### UX-P2-D · Full demo-path smoke — all 6 beats, both screen modes [timebox: 2h]
```
CONTEXT:
  All ui-ux features complete (Rajeswari's P2-A/B/C done). This is the master integration gate.
  Files: docs/PRD.md §3 (6-beat narrative + timings), docs/BUILD-PLAN.md P2.

  Steps:
    1. Two-laptop mode: run full demo path; verify each beat on both screens
    2. Split-screen mode: same; verify C2 left + Cockpit right both readable
    3. Hindi mode: run demo path; verify no English labels bleed through
    4. Fill Assets/UI/TestChecklist.md:
       [ ] Beat 1: green status icon; dropdown shows 4 scenarios
       [ ] Beat 2: 3 sensor panes alive; baseline noise visible
       [ ] Beat 3: RF spike 5.825 GHz; Doppler blips split; acoustic bar rises
       [ ] Beat 4: EW + SMASH operable; ROE warning fires; hit registered
       [ ] Beat 5: Scorecard on both screens ≤10s; scrubber navigable; PDF exports
       [ ] Beat 6: Hindi toggle; split-screen; PDF one-click; no internet
    5. Log any failures → fix immediately (1h buffer in this timebox)
    6. board/STATUS.md: "ui-ux: smoke PASSED — <date>"

DELIVERABLE: Assets/UI/TestChecklist.md (all ticked) · board/STATUS.md updated
TEST: All 6 checkboxes ticked. Two-laptop + split-screen complete.
STATUS: [ ] todo
```

---

## Sync expectations

- **Push `feat/ui-ux-uitk-shell` (UX-P0-A) the same day as Vinod's WS IPC lands** — this is the shell Rajeswari's P1-C + P1-D build on top of.
- **UX-P1-A: start with SensorMock immediately** — don't wait for Venu. Real data wires in when SA-P0-B is pushed.
- **UX-P1-B: start building the controls UI** — test against mock KineticEvent until Uday's SA-P1-D lands.
- **PRs:** Tag Rajeswari as reviewer for UX-P0-A + UX-P1-A (she extends your UXML files).

---

## Accept

> I, Rani, have read this brief and will push `feat/ui-ux-uitk-shell` the same day Vinod's WS IPC branch lands.

- [x] **ACCEPTED**
