# 🛡️ TRISHUL-SIM
### Tactical Radio, Infrared & Sound Hostile UAS Lab
**An Air-Gapped, Physics-Grounded Counter-UAS Tactical Simulation Trainer & Automated After-Action Review (AAR) Range for Battalion Deployments**

[![Smart India Hackathon 2026](https://img.shields.io/badge/SIH-2026-blue.svg)](https://sih.gov.in)
[![Problem Statement](https://img.shields.io/badge/Problem-SIH26247-red.svg)](https://sih.gov.in)
[![Ministry of Defence](https://img.shields.io/badge/MoD-Innovation%20Cell-darkgreen.svg)](https://mod.gov.in)
[![Compliance](https://img.shields.io/badge/Standard-Buy%20(Indian--IDDM)%20DAP%202020-orange.svg)]()
[![Air-Gapped](https://img.shields.io/badge/Security-100%25%20Air--Gapped%20%7C%20Zero%20Cloud-success.svg)]()
[![Platform](https://img.shields.io/badge/Runtime-Unity%202022%20LTS%20URP-lightgrey.svg)]()
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## 📌 Executive Summary

Infantry sentries stationed at forward posts along the Western and Northern borders (BSF Border Outposts in Punjab, high-altitude posts across Jammu & Kashmir, Ladakh LAC, and LoC mountain ridges) face rogue drone incursions with an operational reaction window of only **15 to 45 seconds**. Critically, over **80% of cross-border incursions occur in dense night fog or low visibility**, conditions where visual acquisition is impossible until an incoming drone is under 50 meters.

Existing military-grade drone defense simulators (e.g., Zen Technologies ZADS, Rafael Drone Dome) are multi-crore, centralized motion-rig installations located exclusively at central training schools (such as Army Air Defence College, Gopalpur). They cannot be scaled across 400+ forward infantry battalions and border outposts.

**TRISHUL-SIM** bridges this operational gap as a **software-only, sensor-physics digital twin** engineered for unit-level deployment. Running 100% offline on standard inventory battalion laptops (Intel Core i5 / Iris Xe / RTX 3050) at 60+ FPS, TRISHUL-SIM trains sentries in multi-spectral sensor triage, electronic warfare deconfliction, and kinetic fire control — providing an automated, microsecond-accurate After-Action Review (AAR) scorecard in under 10 seconds.

---

## 🌟 Key Capabilities & Technical Highlights

```
                                  TRISHUL-SIM ARCHITECTURE
    ┌──────────────────────────────────────────────────────────────────────────────────┐
    │                                INSTRUCTOR C2 STATION                              │
    │  • Scenario Library (Procedural border configs)    • Dynamic Weather & Fog       │
    │  • Live Swarm Injection & Command Abort             • Read-only Scorecard Mirror  │
    └────────────────────────────────────────┬─────────────────────────────────────────┘
                                             │ Localhost WebSocket IPC (ws://127.0.0.1:9700)
    ┌────────────────────────────────────────▼─────────────────────────────────────────┐
    │                                  TRAINEE COCKPIT                                 │
    │                                                                                  │
    │  [MULTI-SPECTRAL SENSORS]     [2-TIER SWARM AI]            [COUNTERMEASURES]     │
    │  • RF Waterfall Spectrogram    • Reynolds Boids Flocking    • Directional EW      │
    │    (2.4 / 5.8 / 915 MHz)         (Burst C# Job System)        (Band & Slew)       │
    │  • Micro-Doppler PPI Radar     • Hierarchical Behavior      • ROE Deconfliction   │
    │    (Blade-tip harmonics)         Trees (Pincer, Saturation)   (VHF Comms Guard)   │
    │  • Acoustic FFT Sonar          • Evasive EW Frequency Hop   • SMASH 2000 Reticle  │
    │    (8-band BPF + ISO 9613)     • Decoy vs Attacker RCS        (Dynamic Lead Angle)│
    │                                                                                  │
    │  [AUTOMATED AAR ENGINE]       [VR SENTRY OBSERVATION POST] [BILINGUAL INTERFACE] │
    │  • 10 Hz NDJSON Telemetry      • Punjab Watchtower 3D FBX   • English & Hindi     │
    │  • Decision Tree Scoring       • 60 Hz Gyro Head Tracking     (MIL-STD-2525)      │
    │  • Interactive Timeline Scrub  • 3D Holographic Scorecard   • Dark military /     │
    │  • 1-Click Dossier PDF Export  • Standalone Gamepad HUD       Night-fog palette   │
    └──────────────────────────────────────────────────────────────────────────────────┘
```

### 1. Multi-Spectral Sensor Suite (Real Physics, Not Video Game Animations)
- **Synthetic RF Waterfall Spectrogram:** Models Friis transmission propagation ($P_r = P_t G_t G_r (\lambda / 4\pi R)^2$), signal attenuation, and frequency hopping across standard tactical bands: **2.4 GHz**, **5.825 GHz**, and **915 MHz ELRS**.
- **Micro-Doppler PPI Radar Scope:** Implements thin-wire rotor backscatter equations calculating Doppler blade-tip velocity harmonics ($f_D = 2 v f_0 / c$) and true radar cross-section (RCS) distinctions between high-RCS decoy drones and low-RCS carbon-frame FPV attackers.
- **Acoustic FFT Sonar:** Calculates Blade Pass Frequency (BPF = $\text{RPM} \times \text{Blades} / 60$) with ISO 9613-1 atmospheric sound absorption across 8 frequency bands.
- **Threat Indicator & Sensor Alert Pulse:** Real-time MIL-STD color coding (Green / Amber / Red) with CSS custom properties and dynamic border state transitions.

### 2. 2-Tier Swarm AI (Reynolds Boids + Hierarchical Behavior Trees)
- **High-Performance Flocking:** C# Job System with Burst compiler execution, maintaining 60+ FPS performance even during complex 8+ boid saturation incursions.
- **Doctrinal Attack Profiles:**
  - **Pincer Low-Fast Attack:** Decoy pair executes diversionary high-altitude loiter while attack pairs dive in split axes at 120 km/h.
  - **Saturation Dive:** Broad-front coordinated high-speed dive breaching perimeter defense.
  - **Multi-Axis Split:** Four cardinal vector approach splitting defensive radar coverage.
- **Dynamic EW Evasion:** Swarm agents autonomously detect jamming fields, execute evasive bearing shifts ($\pm 30^\circ$), and initiate RF silent glide with frequency hopping.

### 3. Defensive Countermeasures & Doctrinal Fire Control
- **Directional EW Jammer:** Azimuth/elevation steering with band-selective suppression (2.4 GHz / 5.825 GHz / 915 MHz).
- **ROE Deconfliction Guard:** Simulates Indian Army Rules of Engagement; locks out jamming or penalizes premature activation that would sever friendly battalion VHF tactical radio nets.
- **SMASH 2000 Electro-Optical Fire Control:** Dynamic lead-angle reticle computing target velocity vectors for 7.62 mm kinetic interception.

### 4. Automated AAR (After-Action Review) Engine
- **Microsecond Telemetry Logging:** Continuous 10 Hz NDJSON flight and action telemetry recording to `%AppData%\TRISHUL-SIM\sessions\`.
- **Decision Tree Scorer:** Instant computation of Time-to-Detection, Classification Accuracy, ROE Compliance (%), Kinetic Rounds Fired vs. Hits, and Doctrine Pass/Fail.
- **Interactive Timeline Scrubber:** Allows instructors and trainees to scrub through every second of an engagement with synchronized 2D/3D spatial radar replays.
- **Tactical Performance Dossier (PDF):** Embedded iTextSharp engine compiles a 6-section debrief dossier exported in one click with Hindi and English localization.

### 5. Multi-Mode Deployment Architecture
| Mode | Configuration | Hardware | Use Case |
|---|---|---|---|
| **Mode 1: Dual-Station LAN** | Instructor C2 Station + Trainee Cockpit | 2 Laptops over LAN / Loopback | Formal JCO-to-Jawan classroom instruction |
| **Mode 2: Single-Laptop Split** | 35% C2 / 65% Cockpit layout | 1 Laptop (Intel Iris Xe or better) | Solo sentry practice on standard inventory |
| **Mode 3: VR Sentry Post** | 3D Punjab Watchtower + Smartphone Gyro | 1 Laptop + Smartphone (Cardboard VR) + Gamepad | Immersive 360° binaural sentry drills |

---

## 🎮 VR Sentry Post & Gyroscope Head Tracking

TRISHUL-SIM features a zero-cost, high-immersion **VR Sentry Observation Post** (`VRSentryPost.unity`) designed for immersive drills in forward watchtowers without expensive PC VR headsets:

- **3D Environment:** High-fidelity Punjab border military watchtower with perimeter fencing, full-moon night lighting, and URP volumetric exponential fog.
- **60 Hz Smartphone Gyro Head Tracking:** Any Android or iOS smartphone running Google Chrome/Safari connects over local Wi-Fi/hotspot to `serve-gyro.py` / `serve-gyro.ps1`, streaming device orientation quaternion angles (`alpha, beta, gamma`) via WebSockets to Unity's `GyroReceiver.cs`.
- **Holographic 3D Scorecard:** Upon scenario conclusion, an interactive holographic AAR debrief floats directly inside the virtual watchtower observation desk.
- **Gamepad Control Integration:** Full HUD operation mapped to standard USB/Bluetooth gamepads:

| Button | HUD Action |
|---|---|
| **Left Stick (X-axis)** | Slew directional antenna azimuth ($\pm 3^\circ$/frame) |
| **D-Pad (Up / Down)** | Adjust elevation ($\pm 2^\circ$, clamped $-90^\circ$ to $+90^\circ$) |
| **Y / X Buttons** | Cycle frequency band (2.4 GHz $\rightarrow$ 5.825 GHz $\rightarrow$ 915 MHz ELRS) |
| **A Button** | Toggle Directional EW Jammer ON / OFF |
| **Right Trigger** | SMASH 2000 kinetic fire (lead-angle reticle lock) |
| **Select / Back** | Recalibrate VR center heading |
| **Esc / Gamepad B** | Abort scenario / Return to AAR debrief |

---

## 🗺️ Pre-Seeded Border Scenarios

TRISHUL-SIM ships with pre-configured tactical scenarios rooted in real border topography (`Assets/StreamingAssets/scenarios/`):

1. **Punjab Riverine Fog (`punjab-fog-pincer-01.json`)**: Zero-visibility midnight riverine fog along the Fazilka/Amritsar border. Decoy loiter with low-level 120 km/h pincer FPV swarm.
2. **Ladakh LAC Ridge (`ladakh-lac-ridge-01.json`)**: High-altitude thin-air acoustic propagation, mountain radar masking, and extreme thermal contrast against glacial terrain.
3. **LoC Mountain Pass (`loc-mountain-pass-01.json`)**: Steep ridgeline pass with multi-axis terrain-following drones masking micro-Doppler radar signatures.
4. **Desert FOB (`desert-fob-01.json`)**: High ambient RF thermal noise, long-range Line-of-Sight, and high-speed saturation dive profiles.

---

## 🔒 Military Air-Gap & Procurement Compliance

- **Buy (Indian-IDDM) DAP 2020 Compliant:** Developed for indigenous defense production; 100% of software logic, mathematics, and assets operate without foreign cloud dependencies.
- **Indian Army Cyber Security Policy:** Zero outgoing HTTP/HTTPS calls, zero analytics, zero external API endpoints. All IPC occurs over strictly bound local loopback sockets (`127.0.0.1`).
- **Single-File Installer:** Native IL2CPP Windows x86-64 executable packaged with InnoSetup (`dist/TRISHUL-SIM-installer-*.exe`), running cleanly in offline sandboxes with zero driver requirements.

---

## 🚀 Quick Start Guide

### Prerequisites
- **Operating System:** Windows 10 / 11 (64-bit)
- **Hardware:** Intel Core i5 (8th Gen+) / 8 GB RAM / Intel Iris Xe or dedicated GPU (NVIDIA GTX 1650 / RTX 3050+)
- **Optional:** USB/Bluetooth Gamepad, Smartphone with mobile browser + Google Cardboard headset

### Installation & Launch

1. **Standalone Installer:**
   - Run `dist/TRISHUL-SIM-installer-20261008.exe` to install to `%LocalAppData%\TRISHUL-SIM` (or custom directory).
   - Launch `TRISHUL-SIM.exe` directly from the Start menu or desktop shortcut.

2. **From Source (Unity Editor):**
   - Clone repository:
     ```bash
     git clone https://github.com/vinodkrishna221/TRISHUL-SIM.git
     ```
   - Open in **Unity 2022.3 LTS** (or Unity 6 LTS) with Universal Render Pipeline (URP).
   - Main scenes available in `Assets/Scenes/`:
     - `SplitScreen.unity` — Single-laptop dual-station view (Default).
     - `InstructorC2.unity` — Dedicated Instructor Command Station.
     - `TraineeCockpit.unity` — Dedicated Trainee Sensor Cockpit.
     - `VRSentryPost.unity` — 3D Punjab Military Watchtower VR mode.

3. **Launching Phone Gyroscope Head Tracking (for VR mode):**
   - Start the local gyro bridge server:
     ```powershell
     python docs/serve-gyro.py
     # or using PowerShell:
     .\docs\serve-gyro.ps1
     ```
   - Scan the displayed QR code or navigate to `http://<YOUR_LAN_IP>:8080/vr-gyro-sender.html` on your smartphone browser.
   - Tap "Calibrate & Stream", mount phone into a Cardboard VR headset, and wear!

---

## 🧪 Verification & Self-Test Battery

TRISHUL-SIM includes automated self-test suites across all critical sub-systems:

| Test Suite | File | Scope Verified |
|---|---|---|
| **UiShellSelfTest** | `Assets/Scripts/Tests/UiShellSelfTest.cs` | UITK UI components, bindings, language toggle, and CSS variables |
| **ScorecardSelfTest** | `Assets/Scripts/Tests/ScorecardSelfTest.cs` | Scoring math, threshold evaluation, and telemetry data integrity |
| **RfPhysicsSelfTest** | `Assets/Scripts/Tests/RfPhysicsSelfTest.cs` | Friis transmission math, path loss, and frequency band hopping |
| **DopplerAcousticSelfTest**| `Assets/Scripts/Tests/DopplerAcousticSelfTest.cs` | Blade-tip harmonics and ISO 9613-1 acoustic SPL attenuation |
| **PincerExecutionSelfTest**| `Assets/Scripts/Tests/PincerExecutionSelfTest.cs`| Swarm tactic tree states, EW evasion triggers, and neutralisation |
| **BurstPerformanceSelfTest**| `Assets/Scripts/Tests/BurstPerformanceSelfTest.cs`| C# Job System / Burst flocking frame time (<16.7 ms budget) |
| **VRSentryPostSelfTest** | `Assets/Scripts/Tests/VRSentryPostSelfTest.cs` | Watchtower environment, stereo rig, gyro stream, and HUD Prefabs |
| **AirGapValidator** | `Assets/Scripts/Tests/AirGapValidator.cs` | Offline socket assertions, absence of cloud SDKs & telemetry URLs |

To run all tests inside Unity: Select **TRISHUL-SIM > Run All Self Tests** from the top menu bar.

---

## 👥 Team Resilient Curie (SIH 2026)

- **Vinod Krishna** — Project Lead & Unity Simulation Core (`unity-sim` / `vr-gamepad`)
- **Venu** — Sensor Physics & Swarm AI Algorithms (`swarm-ai`)
- **Uday** — Behavior Trees & SMASH Fire Control (`swarm-ai`)
- **Sohail** — Telemetry Engine & After-Action Review (`aar-engine`)
- **Rani** — Tactical UI / UITK HUD & Localization (`ui-ux`)
- **Rajeswari** — Scorecard Visualization & PDF Dossier (`ui-ux`)

---

## 📜 License & Acknowledgments

This project is licensed under the [MIT License](LICENSE).

- **Inspiration:** Ministry of Defence Problem Statement SIH26247 (Smart India Hackathon 2026).
- **Libraries & Tools:** Unity URP, iTextSharp, InnoSetup.
- Dedicated to the frontline soldiers and border security forces defending forward posts.
