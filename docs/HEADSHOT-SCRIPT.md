# 🎙️ TRISHUL-SIM — Headshot Video Script (Voiceover Narration) v2
> **Version:** 2.0 · **Date:** 2026-10-18 · **VR-First revision**
> **Format:** Headshot to camera + voiceover overlay (~3:10)
> Sync target: TRISHUL-SIM-Demo-Script.md v2 (VR mode = 50% of showcase runtime)

---

## 🎤 1. Intro — The Problem (0:00 – 0:25)

> *[Look directly at camera. Calm, direct, authoritative.]*

"Every night, Indian soldiers at Punjab border outposts have a reaction window of 15 to 45 seconds to detect, classify, and neutralise an incoming drone swarm — in pitch-black fog, where visual acquisition is impossible.

400 plus infantry battalions. No unit-level C-UAS simulator. Existing rigs cost over ₹10 crore, installed only at central training schools.

So we built TRISHUL-SIM."

---

## 🎤 2. The Solution (0:25 – 0:45)

> *[Slightly forward, energised.]*

"TRISHUL-SIM is a 100% software-only, air-gapped counter-drone trainer. Single .exe, any battalion laptop, no internet, no cloud.

The Instructor selects from four procedurally seeded scenarios — Punjab riverine fog, Ladakh LAC ridge, Line of Control mountain pass, Desert FOB. Every run is different. The Jawan cannot memorise — he must build reflexes."

---

## 🎤 3. Non-VR Sensor Glimpse (0:45 – 1:10)

> *[Matter-of-fact, like briefing a colleague.]*

"On the Trainee Cockpit you'll see the three-pane physics-accurate sensor suite — RF Waterfall, Micro-Doppler Radar, Acoustic FFT. These are not animations. The Swarm AI — eight autonomous agents running Reynolds Boids with Burst-compiled physics — is computing real drone signatures in real-time.

But we took it further."

---

## 🎤 4. VR Sentry Post — Scene & Gyro (1:10 – 1:40)

> *[Slower, let the visuals breathe with you.]*

"The same scenario, the same physics, the same swarm — now runs inside a VR sentry post. A 3D recreation of a Punjab military watchtower. Nighttime. Full-moon lighting. Dense exponential fog.

The soldier uses a smartphone as a VR headset — Google Cardboard compatible. The phone streams gyroscope data at 60 Hz over WebSocket. No app install. No headset license. Total cost: zero.

You're watching the VR camera track head movement in real-time."

---

## 🎤 5. Gamepad Controls the HUD (1:40 – 2:25)

> *[This is the centrepiece. Confident, deliberate. Name each control clearly.]*

"All the HUD panels — the sensor suite, the EW jammer, the fire control — are operated entirely by a standard gamepad. No keyboard. No mouse. Just like a real weapons station.

Left stick slews the directional antenna — azimuth, 3 degrees per frame. D-pad adjusts elevation. Y and X cycle the frequency band — 2.4 gigahertz, 5.825, 915 MHz ELRS.

When the swarm enters the threat cone — A button activates the EW jammer. The ROE deconfliction engine prevents friendly VHF blackout — if the Jawan jams the wrong band, the system blocks him and logs a violation.

And then — right trigger — SMASH 2000 fire control. The lead-angle reticle locks to the incoming FPV. The trained reflex fires.

All four attack drones neutralised. Zero friendly fire. 100% ROE compliance.

If you lose your orientation in VR — one press of Select recalibrates the heading. Back in the fight."

---

## 🎤 6. Holographic Scorecard in VR (2:25 – 2:45)

> *[Slightly leaning in — this is the reveal.]*

"Ten seconds after scenario end, a holographic 3D scorecard materialises over the observation desk — in VR space. Time-to-Detection, ROE compliance, kinetic efficiency, doctrine pass or fail. The Jawan reads his score without removing the headset.

The same scorecard renders on the flat screen too — interactive timeline scrubber, spatial radar overlay, one-click PDF export in Hindi and English."

---

## 🎤 7. Impact Close (2:45 – 3:10)

> *[Confident, proud, finalising. This is your last line to the judges.]*

"TRISHUL-SIM is the first battalion-deployable C-UAS trainer that teaches Indian soldiers to defeat a drone swarm in the dark — for ₹10 a seat. 100% air-gapped, runs on Intel Iris Xe at 60-plus FPS. Gamepad. Smartphone VR. Single installer.

We built this for the Jawan at the forward post who has 45 seconds and no simulator."

---

## 🕹️ Gamepad Control Map (Quick Reference)

| Button | HUD Action |
|---|---|
| **Left Stick** L/R | Azimuth slew ±3°/frame |
| **D-Pad** Up/Down | Elevation ±2° (clamped -90° to +90°) |
| **Y / X** | Band cycle: 2.4 → 5.825 → 915 MHz ELRS |
| **A** | Toggle EW Jammer on/off |
| **Right Trigger** | SMASH 2000 fire (lead-angle reticle) |
| **Select** | Recalibrate VR heading |

---

## 📋 Recording Guidelines

| | |
|---|---|
| **Pacing** | ~130–140 words/min. Pause on VR and gamepad beats. |
| **VR section (0:45 onwards)** | Let the visuals lead. Don't rush. |
| **Section takes** | Record sections 1–7 as separate takes. Easier to sync in post. |
| **Eye contact** | Camera lens, not the screen. |
| **Audio** | Lapel or close mic. Minimise room echo. |

---

*TRISHUL-SIM · Smart India Hackathon 2026 · Problem SIH26247 · Team Resilient Curie*
