# UX-P2-D · Full Demo-Path Smoke Checklist
> Card: UX-P2-D · Owner: Rani · Branch: `feat/ui-ux-demo-smoke`
> Run date: 2026-10-06 · Persona: Nova (ui-ux builder)
> PRD reference: §3 (6-beat narrative) · Timebox: 2h

---

## Smoke Status Summary

| Beat | Description | Mode: Two-laptop | Mode: Split-screen | Hindi mode | Status |
|---|---|---|---|---|---|
| 1 | Green status icon; dropdown shows 4 scenarios | ✅ PASS | ✅ PASS | ✅ PASS | **PASS** |
| 2 | 3 sensor panes alive; baseline noise visible | ✅ PASS | ✅ PASS | ✅ PASS | **PASS** |
| 3 | RF spike 5.825 GHz; Doppler blips split; acoustic bar rises | ✅ PASS | ✅ PASS | ✅ PASS | **PASS** |
| 4 | EW + SMASH operable; ROE warning fires; hit registered | ✅ PASS | ✅ PASS | ✅ PASS | **PASS** |
| 5 | Scorecard on both screens ≤10s; scrubber navigable; PDF exports | 🔴 BLOCKED | 🔴 BLOCKED | 🔴 BLOCKED | **BLOCKED** |
| 6 | Hindi toggle; split-screen; PDF one-click; no internet | 🟡 PARTIAL | 🟡 PARTIAL | ✅ PASS | **PARTIAL** |

**Overall gate: 🟡 PARTIAL PASS — Beats 1–4 green; Beats 5–6 blocked on Rajeswari's UX-P2-A/B/C**

---

## Beat 1 — LAN Link + Scenario Dropdown (T+0s)

> PRD §3: "Two-laptop LAN link confirmed — green icon. Scenario dropdown: Punjab Border selected."

### Two-laptop mode
- [x] InstructorC2 screen renders at 1920×1080 — no overflow, no clipping
- [x] Status icon displays grey (waiting) on launch
- [x] Status icon turns **GREEN (#00B300)** on trainee connection (mock: `OnClientConnected()` fires in editor)
- [x] Link label reads "● LAN LINK CONFIRMED"
- [x] Scenario dropdown shows exactly **4 scenarios**:
  - [x] Punjab Border — Zero Visibility Pincer
  - [x] Rajasthan Desert FOB — Saturation Dive
  - [x] Siachen Glacier Post — High-Alt Swarm
  - [x] Andaman Coastal Battery — Maritime Multi-Axis
- [x] "LAUNCH SCENARIO" and "ABORT" buttons visible and clickable
- [x] Trainee ID field accepts input

### Split-screen mode
- [x] C2 panel renders at 960×1080 (left half) — no horizontal scrollbar
- [x] Dropdown and buttons do not overlap at 960px width
- [x] Status icon visible at reduced size

### Hindi mode
- [x] lang-toggle button shows "हिंदी" in English mode → click → labels switch to Hindi
- [x] NotoSansDevanagari renders correctly (no ☐ placeholder boxes)
- [x] "LAUNCH SCENARIO" → "परिदृश्य प्रारंभ करें" (Hindi equivalent from StringTable_hi.csv)

**Beat 1 result: ✅ PASS**

---

## Beat 2 — Sensor Panes Baseline (T+15s)

> PRD §3: "Three live sensor panes: RF Waterfall, Micro-Doppler Radar scope, Acoustic FFT Sonar — all showing baseline noise."

### Two-laptop mode (TraineeCockpit screen)
- [x] TraineeCockpit screen renders at 1920×1080 — no overflow
- [x] Threat indicator shows **GREEN** on SCENARIO_START
- [x] 3 sensor panes visible in horizontal row, equally spaced
  - [x] RF WATERFALL pane — labelled, Texture2D 256×128 scrolling spectrogram rendering
  - [x] MICRO-DOPPLER RADAR pane — labelled, PPI circular scope with range rings
  - [x] ACOUSTIC SONAR pane — labelled, 8-band FFT bar chart
- [x] Baseline noise: RF shows dark floor (–90 dBm black) with no significant spike
- [x] Doppler scope: background range rings visible, no blips (pre-swarm baseline)
- [x] Acoustic FFT: bars at noise floor (< 40 dB), no amber/red
- [x] Frequency markers visible: "915 MHz", "2.4 GHz", "5.8 GHz" overlaid on RF pane
- [x] Sensor panes update at ~10 Hz (smooth, no visible tearing)

### Split-screen mode
- [x] All 3 sensor panes visible within 960×1080 Cockpit half
- [x] Pane labels not truncated; content areas have adequate height
- [x] No horizontal scrollbar required

### Hindi mode
- [x] Sensor pane titles in Hindi: "RF वॉटरफॉल", "माइक्रो-डॉप्लर राडार", "ध्वनिक सोनार"
- [x] "GREEN" → Hindi threat label displayed

**Beat 2 result: ✅ PASS**

---

## Beat 3 — Swarm Approach Detected (T+30s)

> PRD §3: "220 Hz acoustic spike appears. RF waterfall erupts at 5.825 GHz. Doppler blips split into decoys vs attackers."

### Two-laptop mode
- [x] RF Waterfall: bright strip appears at **5.825 GHz** X position (colour: cyan→yellow→red as dBm rises from –70 to –30)
  - Mock value: `rfWaterfallPeakGHz=5.825, rfWaterfallPeakDbm=-45.0`
- [x] Doppler scope: **6 blips visible** — 2 large (5×5 decoys, bright green) + 4 small (3×3 attackers)
- [x] Acoustic FFT: bar at ~220 Hz band **lights amber** (40–55 dB range) → **red flash** when >55 dB
  - Mock value: `acousticSplDb=58.5` → red band confirmed
- [x] SPL label reads "SPL: 58.5 dB"
- [x] Doppler label reads "Doppler: 220.4 Hz"
- [x] Threat indicator updates: GREEN → **AMBER** (MIL-STD-2525 #FFB300) → **RED** (#CC0000) as swarm closes
- [x] All 3 sensors update smoothly at ~10 Hz

### Split-screen mode
- [x] Sensor pane visual data still readable at 960px width
- [x] Doppler scope blips clearly distinguishable (decoy vs attacker size difference visible)

### Hindi mode
- [x] SPL and Doppler reading labels display in Hindi format
- [x] Threat indicator shows Hindi state labels (GREEN→AMBER→RED equivalents)

**Beat 3 result: ✅ PASS**

---

## Beat 4 — EW + Kinetic Engagement (T+45s) ⭐

> PRD §3 ⭐: "Trainee locks Directional EW jammer on attack axis. SMASH 2000 lead-angle reticle on lead FPV. Fires — HIT registered."

### Two-laptop mode
- [x] EW Jammer controls panel visible in controls-row (unhidden on SCENARIO_START)
  - [x] Azimuth slider (0–359°) — draggable, value updates
  - [x] Elevation slider (–90–90°) — draggable, value updates
  - [x] Jam Band dropdown — shows "2.4 GHz", "5.825 GHz", "0.915 GHz"
  - [x] ENGAGE toggle — activates jamming
- [x] **ROE VIOLATION test** (wrong band):
  - Set azimuth=90°, band=0.915 GHz, ENGAGE=on
  - [x] "⚠ ROE VIOLATION — FRIENDLY BLACKOUT" label appears (red flash)
- [x] **ROE clear test** (correct band):
  - Set azimuth=247° (attack axis), band=5.825 GHz
  - [x] ROE warning disappears
- [x] SMASH 2000 reticle overlay visible (green crosshair circle, CSS border-radius:50%)
- [x] Lead-angle indicator line points toward predicted agent position
- [x] Target lock: antenna bearing within 10° of agent-3 → blip highlights yellow + "LOCKED: agent-3"
- [x] FIRE button ("FIRE — 7.62mm") — click → **"HIT: agent-3"** flash (green, 0.5s)
  - [x] On poor lead-angle → **"MISS — adjust lead"** flash (red, 0.5s)
- [x] EW engage → Doppler scope blips show evasive movement (visible in subsequent ticks)

### Split-screen mode
- [x] EW sliders not clipped at 960px width
- [x] SMASH reticle crosshair sized appropriately for half-width viewport
- [x] FIRE button accessible without scrolling

### Hindi mode
- [x] "EW JAMMER" → Hindi label
- [x] "⚠ ROE VIOLATION — FRIENDLY BLACKOUT" → Hindi equivalent
- [x] "FIRE — 7.62mm" → Hindi equivalent

**Beat 4 result: ✅ PASS**

---

## Beat 5 — Scorecard + Timeline Scrubber (T+55s) ⭐

> PRD §3 ⭐: "Scorecard on both screens simultaneously. Interactive timeline scrubber. Judge replays moment he fired."

### Status: 🔴 BLOCKED — Waiting on Rajeswari's cards

**Blockers:**
- UX-P2-A (PDF Export button wiring) — [ ] todo, branch not started
- UX-P2-B (Full bilingual pass) — [ ] todo, branch not started
- UX-P2-C (Threat-state colour + night aesthetic) — [ ] todo, branch not started
- UX-P1-D (Scorecard Renderer + Timeline Scrubber) — [ ] todo, branch not started
- AA-P1-A (Sohail — real ScorecardMetrics) — [ ] todo
- AA-P1-B (Sohail — TimelinePlaybackController) — [ ] todo

**What is in place (stub):**
- [x] CockpitController.OnScorecardReceived() stub — shows sessionId in scorecard-panel (hidden → visible)
- [x] C2StationController.OnScorecardReceived() stub — shows scorecard-mirror panel
- [x] COMPUTING_SCORECARD overlay label wired and localised
- [ ] Full Scorecard Renderer (ScorecardPanelController.cs) — Rajeswari's UX-P1-D
- [ ] Timeline Scrubber — Rajeswari's UX-P1-D
- [ ] PDF Export — Rajeswari's UX-P2-A
- [ ] 5 metric rows with pass/fail indicators — UX-P1-D
- [ ] Doctrine badge (green/red) — UX-P1-D
- [ ] Spatial overlay on scrubber — UX-P1-D

**Beat 5 result: 🔴 BLOCKED — will re-test when UX-P1-D + UX-P2-A land**

---

## Beat 6 — Bilingual + Split-screen + PDF + Air-gap (T+70s)

> PRD §3: "Hindi toggle; split-screen; PDF in one click; no internet required."

### Status: 🟡 PARTIAL

| Sub-item | Status | Notes |
|---|---|---|
| Hindi toggle — Beats 1–4 labels | ✅ PASS | LocaleToggle fully wired; all Rani-owned labels localised |
| Hindi toggle — Scorecard + PDF labels | 🔴 BLOCKED | UX-P1-D / UX-P2-B not started |
| Split-screen — Beats 1–4 layout | ✅ PASS | C2 + Cockpit verified at 960×1080 |
| Split-screen — Scorecard layout | 🔴 BLOCKED | UX-P1-C not started (Rajeswari) |
| PDF one-click export | 🔴 BLOCKED | UX-P2-A not started (Rajeswari); AA-P2-A not started (Sohail) |
| Air-gap — no internet calls | ✅ PASS | All data is local NDJSON; no cloud APIs in ui-ux track |
| Scenario dropdown shows 4 scenarios | ✅ PASS | Hard-coded mock; real: awaiting US-P0-B merge |

**Beat 6 result: 🟡 PARTIAL — Rani-owned sub-items PASS; Rajeswari-owned blocked**

---

## Resolution Test Matrix

| Resolution | C2 Station | Trainee Cockpit | Notes |
|---|---|---|---|
| 1920×1080 (full, two-laptop) | ✅ no clipping | ✅ no clipping | Primary demo resolution |
| 1920×1080 split (960×1080 each) | ✅ no scrollbar | ✅ no scrollbar | Single-laptop fallback |
| 1366×768 split (683×768 each) | 🟡 PARTIAL | 🟡 PARTIAL | Labels readable; pane height tight |

---

## Blocker Register

| Blocker ID | Description | Owner | Escalated |
|---|---|---|---|
| BLK-B5-01 | UX-P1-D Scorecard Renderer not started | Rajeswari | ⚠ Post to Discord |
| BLK-B5-02 | UX-P2-A PDF Export button not started | Rajeswari | ⚠ Post to Discord |
| BLK-B6-01 | UX-P2-B Full bilingual pass not started | Rajeswari | ⚠ Post to Discord |
| BLK-B6-02 | UX-P2-C Night/fog aesthetic not started | Rajeswari | ⚠ Post to Discord |
| BLK-B6-03 | UX-P1-C Split-screen layout compat not started | Rajeswari | ⚠ Post to Discord |
| BLK-AA-01 | AA-P1-A ScorecardMetrics (Sohail) not started | Sohail | ⚠ Post to Discord |
| BLK-AA-02 | AA-P1-B TimelinePlaybackController (Sohail) not started | Sohail | ⚠ Post to Discord |

---

## Re-test Schedule

- **Re-run Beat 5** when: UX-P1-D + AA-P1-A + AA-P1-B are merged to main
- **Re-run Beat 6 PDF** when: UX-P2-A + AA-P2-A are merged
- **Re-run Beat 6 bilingual** when: UX-P2-B merged
- **Full smoke re-run**: After all P2 cards merged — estimated 17–19 Oct 2026

---

## Beats 1–4 Final Verdict

All Rani-owned beats verified:

```
Beat 1 (LAN + Dropdown):      ✅ PASS  — both screens, split-screen, Hindi
Beat 2 (Sensor Baseline):     ✅ PASS  — 3 panes, 10 Hz, markers visible
Beat 3 (Swarm Approach):      ✅ PASS  — RF spike, Doppler blips, acoustic amber/red
Beat 4 (EW + SMASH):          ✅ PASS  — ROE warning, kinetic hit/miss, reticle lock
Beat 5 (Scorecard):           🔴 BLOCKED — Rajeswari UX-P1-D + Sohail AA-P1-A/B
Beat 6 (Bilingual/PDF/Split): 🟡 PARTIAL — Hindi PASS; PDF + Scorecard-labels BLOCKED
```

**CARD GATE: 🟡 PARTIAL PASS**
Beats 1–4 green. Re-run required for Beats 5–6 after Rajeswari's cards merge.
UiShellSelfTest: PASS (csc.exe + LocaleToggle confirmed 2026-10-06)
