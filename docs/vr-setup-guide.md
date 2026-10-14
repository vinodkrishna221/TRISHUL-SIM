# VR Demo Setup — 6 Steps

> **TRISHUL-SIM · VR Track (SIH 2026 · Problem SIH26247)**  
> Hardware: Windows Laptop (Host / C2) + Android Phone + VR BOX Mini Headset (~₹500) + Gen Game Bluetooth Gamepad.  
> Total hardware budget: ₹0 extra infrastructure beyond phone + viewer.

---

## 6-Step Quick-Start Procedure

### Step 1: Install spacedesk Driver on Laptop (One-time)
1. Download Windows spacedesk Driver: https://www.spacedesk.net
2. Run installer and complete Windows display adapter driver installation.
3. Verify that `spacedeskService` is running in Windows Services (`services.msc`).

### Step 2: Install spacedesk App on Android Phone (One-time)
1. Open Google Play Store on Android smartphone.
2. Search and install **spacedesk (remote display)**.
3. Grant network and display permissions when prompted.

### Step 3: Enable Windows WiFi Hotspot on Laptop
1. In Windows: Open **Settings** → **Network & internet** → **Mobile hotspot**.
2. Turn mobile hotspot **ON**.
3. Configure SSID (e.g., `TRISHUL-HOTSPOT`) and WPA2 Password.
4. Note your local hotspot IPv4 address (run `ipconfig` in PowerShell; look for the Mobile Hotspot adapter, typically `192.168.137.1`).

### Step 4: Phone Connects to Laptop Hotspot
1. On Android phone: Open Wi-Fi settings.
2. Connect to `TRISHUL-HOTSPOT`.
3. Verify phone receives an IP address on the hotspot subnet (e.g., `192.168.137.X`).

### Step 5: Phone Opens spacedesk App → Select Laptop (Display 2)
1. Launch the **spacedesk** app on the Android phone.
2. The laptop hostname/IP should automatically appear under "Discovered Machines".
3. Tap the laptop entry (or manually enter `192.168.137.1`).
4. Phone display now mirrors/extends Windows as **Display 2**.
5. Verify in Windows: Right-click Desktop → **Display settings** → Confirm Display 1 (Laptop) and Display 2 (Phone) are present. Set Display 2 orientation to Landscape.

### Step 6: Phone Opens Gyro Tracking Page
1. On laptop, launch the gyro server script:
   ```powershell
   powershell -ExecutionPolicy Bypass -File docs/serve-gyro.ps1
   ```
2. On phone, open Google Chrome and navigate to:
   ```
   http://192.168.137.1:8080/
   ```
3. Tap the screen to grant device orientation permission.
4. The page will display **TRACKING** (green status).
5. Slide phone into VR BOX Mini headset tray and close visor.

---

## Live Demo Operation

1. **Instructor C2 Station:**
   - Launch TRISHUL-SIM executable or open `InstructorC2` in Unity.
   - The C2 Station UI shows link status and scenario options.
   - Click the **[VR MODE]** button in the C2 Controls Panel.
2. **Dual-Display Routing:**
   - Unity automatically routes `LeftEyeCamera` and `RightEyeCamera` to **Display 2** (the phone inside VR BOX).
   - Instructor C2 Station remains on **Display 1** (laptop screen) for full tactical oversight.
   - C2 status badge updates to:
     ```
     VR MODE: ON ● GYRO: LIVE
     ```
3. **Trainee Execution:**
   - Trainee wears VR BOX and holds Bluetooth gamepad.
   - Head orientation turns view; floating HUD sensor panels (RF Waterfall, Doppler Scope, Acoustic Sonar, EW/SMASH) appear in 3D darkness.
   - Left stick slews EW jammer, A button engages, right trigger fires SMASH 2000 kinetic countermeasure.
4. **Exit VR Mode:**
   - Click **[VR ACTIVE]** button on C2 Station to toggle off and return to standard desktop mode.

---

## Fail-Safe Fallbacks & Contingency Rules

| Scenario | Behavior | Action / Fallback |
|---|---|---|
| **Phone disconnected / No spacedesk** | `Display.displays.Length == 1` | `VRModeController` automatically routes stereo cameras to **Display 1** (laptop screen) in split-stereo. Still 100% demoable. |
| **Phone Gyro dropped / Wi-Fi lag** | Gyro WebSocket disconnects | `VRModeController.IsGyroConnected == false`. C2 status badge displays `VR MODE: ON ○ GYRO: OFFLINE`. View angle holds last valid pose or falls back to gamepad right-stick look. |
| **High latency on venue Wi-Fi** | Packet jitter on port 9800 | Windows Mobile Hotspot eliminates external venue Wi-Fi dependence entirely. Ensure 5 GHz hotspot band is selected in Windows settings for lowest latency. |
| **Zero VR Available (T-3h Endgame Cut)** | Extreme emergency | Toggle VR MODE off. Gamepad controls (`VG-P1-A`) work directly with standard 2D desktop Cockpit HUD. |

---

## Technical Specifications

- **IPC Game Port:** `ws://127.0.0.1:9700` (Scenario ticks and commands)
- **Gyro Tracking Port:** `ws://0.0.0.0:9800` (Device orientation JSON: alpha, beta, gamma)
- **Gyro HTTP Server Port:** `http://+:8080/` (Serves `vr-gyro-sender.html`)
- **Display 1 (Primary):** Laptop display — Instructor C2 (`targetDisplay = 0`)
- **Display 2 (Secondary):** Phone via spacedesk — VR Sentry Post stereo cameras (`targetDisplay = 1`)
- **Target FPS:** 60 FPS on Intel Iris Xe / standard integrated graphics
