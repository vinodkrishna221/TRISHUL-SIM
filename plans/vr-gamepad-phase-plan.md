# Phase Plan — `vr-gamepad`
> Generated: 2026-10-11 · Owner: Orion (Architect persona) · Owners: Vinod (unity-sim) + Uday (swarm-ai) + Sohail (aar-engine)
> Track budget: ~26h usable / 11–18 Oct 2026
> Skeleton deadline: N/A (new scope added mid-hackathon) · Submission: 20 Oct 2026 23:59 IST

---

## Cross-Track Dependency DAG

```
vr-gamepad depends on:
  unity-sim-P0-A (WsServer/WsClient live — VR receives SCENARIO_TICK)
  unity-sim-P1-A (SplitScreen + Prefabs exist — VR uses TraineeCockpitRoot prefab)
  ui-ux-P1-A     (Sensor renderers live — VG-P1-C needs RfWaterfallRenderer etc.)
  ui-ux-P1-B     (EwJammerController + SmashReticleController live — VG-P1-A needs them)

vr-gamepad unblocks:
  unity-sim-P3-A (submission — VR must be done before final build)
```

**File surface audit — no overlaps:**
- `vr-gamepad` owns: `Assets/Scripts/Input/`, `Assets/Scripts/VR/`, `Assets/Scenes/VRSentryPost.unity`, `Assets/Input/TrishulInputActions.inputactions`, `Assets/RenderTextures/`, `docs/vr-*.md`
- `vr-gamepad` does NOT touch: `Assets/Scripts/Network/`, `Assets/Scripts/AAR/`, `Assets/StreamingAssets/`
- **Shared read:** `board/contracts/` (read-only), `Assets/Scripts/UI/` (VG-P1-A calls public methods only)

---

## Load Ledger

| Phase | Cards | Timebox sum | Cumulative |
|---|---|---|---|
| P1 VR + Gamepad | VG-P1-A, VG-P1-B, VG-P1-C, VG-P1-D, VG-P1-E, VG-P1-F | 3+5+5+5+3+3 = **24h** | 24h |

**26h usable: 24h planned + 2h for integration friction. ✓**

---

## P1 · VR + Gamepad (11–18 Oct 2026)

> Gate: Gamepad controls EW + SMASH · 3D night post renders · Gyro head tracking live · VR mode toggleable from C2

---

### VG-P1-A · Gamepad Input Actions binding ⭐ [timebox: 3h]

```
CONTEXT:
  First VR-gamepad card. Zero risk — works immediately with the EXISTING desktop Trainee Cockpit
  HUD. No 3D scene needed. Useful EVEN IF VR fails.
  
  Current state:
    • EwJammerController.SetAzimuth(int deg), SetElevation(int deg), SetBandGHz(float), SetEngaged(bool)
    • SmashReticleController.TriggerFire(long elapsedMs) → KineticEvent
    • CockpitController.TriggerFireKinetic(), SetEwParameters(int, int, float, bool)
    • com.unity.inputsystem 1.20.1 already installed. NO .inputactions files in Assets/ yet.
  
  Files to read:
    • Assets/Scripts/UI/EwJammerController.cs
    • Assets/Scripts/UI/SmashReticleController.cs
    • Assets/Scripts/UI/CockpitController.cs
    • board/contracts/scenario-events.md (TraineeAction shape)
  
  Gamepad mapping:
    Left Stick X    → JammerAzimuth (continuous, ±3°/frame at full deflection)
    D-Pad Y         → JammerElevation (±2° per press, clamp -90 to +90)
    Right Trigger   → FireSMASH (on performed)
    A/buttonSouth   → ToggleJammer (on performed)
    Y/buttonNorth   → BandUp (cycle 2.4→5.825→0.915 GHz)
    X/buttonWest    → BandDown (reverse cycle)
    Select/select   → Recalibrate head tracking
  
  Steps:
    1. Create Assets/Input/TrishulInputActions.inputactions
       Action Map: "Trainee"
       JammerAzimuth   Value/Axis: <Gamepad>/leftStick/x + <Keyboard>/a,d
       JammerElevation Value/Axis: <Gamepad>/dpad/y + <Keyboard>/w,s
       FireSMASH       Button: <Gamepad>/rightTrigger + <Keyboard>/space
       ToggleJammer    Button: <Gamepad>/buttonSouth + <Keyboard>/j
       BandUp          Button: <Gamepad>/buttonNorth + <Keyboard>/equals
       BandDown        Button: <Gamepad>/buttonWest + <Keyboard>/minus
       Recalibrate     Button: <Gamepad>/select + <Keyboard>/r
    2. Create Assets/Scripts/Input/GamepadInputController.cs MonoBehaviour:
       - Serialized field: CockpitController cockpit (wire in inspector or FindAnyObjectByType in Awake)
       - Serialized field: float azimuthSensitivity = 3f
       - Subscribe to actions in OnEnable, unsubscribe in OnDisable
       - Update(): read JammerAzimuth.ReadValue<float>() → accumulate azimuth 0-359° → SetEwParameters
       - FireSMASH.performed → cockpit.TriggerFireKinetic()
       - ToggleJammer.performed → toggle bool engaged → SetEwParameters
       - BandUp/BandDown.performed → cycle float[] {2.4f, 5.825f, 0.915f}
       - JammerElevation.performed → ±2° step, clamp -90 to +90
       - Recalibrate.performed → fire event for GyroReceiver (VG-P1-D will wire this)
    3. Add GamepadInputController to TraineeCockpitRoot.prefab
    4. Verify Gen Game gamepad pairs in Windows joy.cpl before testing

DELIVERABLE:
  Assets/Input/TrishulInputActions.inputactions
  Assets/Scripts/Input/GamepadInputController.cs
  TraineeCockpitRoot.prefab updated

TEST:
  Pair Gen Game gamepad via Bluetooth (verify joy.cpl).
  Play SplitScreen.unity → Launch Punjab scenario.
  → Left stick moves azimuth slider on EW jammer HUD
  → Right trigger fires SMASH 2000 (flash feedback visible)
  → A toggles EW engage checkbox
  → Keyboard fallbacks (a/d/w/s/space/j) also work
  Console: [GamepadInput] Gamepad detected: <device name>

DEPENDS: ui-ux-P1-B (EwJammerController + SmashReticleController live)   UNBLOCKS: VG-P1-D (Recalibrate binding needed)

DEMO: "Havildar Rajan engages jammer and fires SMASH 2000 using only a gamepad." (Beat 4)
PERSONA: Vinod (unity-sim)   STATUS: [ ] todo
```

---

### VG-P1-B · 3D Nighttime Sentry Post scene [timebox: 5h]

```
CONTEXT:
  Creates the immersive 3D environment. Built entirely from Unity primitives — no 3D model assets.
  Target: midnight Punjab border post. Dense fog. Zero visible drones (doctrine: zero-visibility).
  
  Files to read:
    • docs/PRD.md §3 Beat 4 — pitch-black, sensor-only defeat
    • Assets/Scenes/VRSentryStub.unity — existing XRRig for reference structure
    • docs/ARCHITECTURE.md — Unity 6 URP, Camera.rect pattern
  
  Steps:
    1. Create Assets/Scenes/VRSentryPost.unity, add to EditorBuildSettings (index 3)
    2. Skybox: Create Material SkyboxNight.mat → Shader=Skybox/Procedural.
       Exposure=0.02, AtmosphereThickness=0, SunSize=0, SunSizeConvergence=1
       → Window → Rendering → Lighting → Skybox Material = SkyboxNight.mat
       Optional star overlay: add particle system (rate=0, burst 200 tiny white particles)
    3. URP Global Volume → Fog: Exponential Squared, Color=#12122A (deep navy-black), Density=0.04
    4. URP Global Volume → Bloom: Intensity=0.8, Threshold=0.7 (makes green HUD panels glow)
    5. Watchtower platform:
       a. Quad (ground): Scale=(20,1,20), Position=(0,-0.01,0), dark concrete material (R=0.15,G=0.15,B=0.15)
       b. 4× railing posts: Cylinder, scale=(0.08,1.5,0.08), positions at (±4,0.75,±4)
       c. 4× rails: Cube, scale=(8,0.08,0.08), horizontal, height Y=1.5m
    6. Stereo camera rig:
       a. GameObject "VRRig" at origin
       b. Child "StereoHead" at localPos=(0,1.65,0)
       c. Child of StereoHead: "LeftEyeCamera" with Camera component
          - ViewportRect: {x=0, y=0, w=0.5, h=1}, Near=0.01, Far=500
          - LocalPos=(-0.032, 0, 0)
       d. Child of StereoHead: "RightEyeCamera" with Camera component
          - ViewportRect: {x=0.5, y=0, w=0.5, h=1}, Near=0.01, Far=500
          - LocalPos=(+0.032, 0, 0)
       e. AudioListener on VRRig (not on cameras)
    7. Add GamepadInputController to VRRig (from VG-P1-A)

DELIVERABLE:
  Assets/Scenes/VRSentryPost.unity
  Assets/Materials/SkyboxNight.mat
  Assets/Materials/DarkConcrete.mat
  Scene in EditorBuildSettings

TEST:
  Open VRSentryPost → Press Play.
  → Viewport split into left/right stereo halves
  → Dark environment, fog visible, watchtower platform rendered
  → No compile errors, no 3D drone objects present
  FPS ≥60 on target laptop with both cameras active

DEPENDS: VG-P1-A (GamepadInputController to attach)   UNBLOCKS: VG-P1-C (HUD panels need rig), VG-P1-D (GyroReceiver attaches to VRRig)

DEMO: "In the dark. No flashlight. Only sensor glow." (Beat 4 atmosphere)
PERSONA: Vinod (unity-sim)   STATUS: [ ] todo
```

---

### VG-P1-C · Floating 3D Sensor HUD panels [timebox: 5h]

```
CONTEXT:
  Converts the existing 2D sensor renderers into world-space 3D floating panels.
  Uses RenderTextures + world-space Quads with emissive materials.
  The Iron Man helmet display effect.
  
  Current renderers output to UITK VisualElements. This card adds an optional RenderTexture output
  path — existing 2D HUD is unchanged (additive change only).
  
  Files to read:
    • Assets/Scripts/UI/RfWaterfallRenderer.cs (Initialize signature, UpdateWaterfall)
    • Assets/Scripts/UI/DopplerScopeRenderer.cs (Initialize, UpdateScope)
    • Assets/Scripts/UI/AcousticFftRenderer.cs (Initialize, UpdateAcoustic)
    • Assets/Scripts/UI/CockpitController.cs (UpdateSensors, how renderers are called)
    • Assets/Scripts/UI/EwJammerController.cs (CurrentState, how controls work)
  
  Steps:
    1. Create 4 RenderTexture assets (Assets/RenderTextures/):
       RT_RfWaterfall.renderTexture     512×512 R8G8B8A8
       RT_DopplerScope.renderTexture    512×512 R8G8B8A8
       RT_AcousticFft.renderTexture     512×256 R8G8B8A8
       RT_EwSmashPanel.renderTexture    512×256 R8G8B8A8
    2. Create Assets/Scripts/VR/VRHudController.cs — MonoBehaviour on VRRig:
       - Serialized fields: RenderTexture rtRf, rtDoppler, rtAcoustic, rtEwSmash
       - Start(): get GyroReceiver, get ScenarioManager instance
       - Subscribe to same events as CockpitController (OnScenarioStart, OnScenarioTick, OnScenarioEnd)
       - On tick: call renderers to draw into RenderTextures using camera-based approach
       NOTE: The simplest implementation that works:
         Create 4 hidden off-screen cameras (one per panel, culling mask=UI layer only)
         Each camera targets its RenderTexture
         The UITK renderers draw to the VisualElement; the off-screen camera reads that and renders to RT
         Alternative (simpler): each renderer exposes a Texture2D GetFrame() method;
         VRHudController reads it and blits to RenderTexture each frame via Graphics.Blit
    3. Create 4 floating panel GameObjects in VRSentryPost.unity:
       Panel_RF: Quad at (-2.0, 1.5, 2.5), scale=(1.6,0.9,1), rot=(0,30,0)
         → Material: Unlit/Texture shader, MainTex=RT_RfWaterfall, EmissiveColor=#00FF8844
       Panel_Doppler: Quad at (0, 1.8, 2.8), scale=(1.6,1.6,1), rot=(0,0,0)
         → Material: RT_DopplerScope
       Panel_Acoustic: Quad at (2.0, 1.5, 2.5), scale=(1.2,0.6,1), rot=(0,-30,0)
         → Material: RT_AcousticFft
       Panel_EwSmash: Quad at (0, 0.7, 2.0), scale=(1.8,0.8,1), rot=(20,0,0) [tilted down]
         → Material: RT_EwSmashPanel
    4. Panel labels: TextMeshPro world-space above each panel, small white text:
       "RF WATERFALL", "MICRO-DOPPLER RADAR", "ACOUSTIC SONAR", "EW / SMASH 2000"
    5. Threat indicator: Torus mesh around Panel_Doppler, scale=(1.8,1.8,0.05)
       MeshRenderer material color changes in VRHudController based on threatState:
       GREEN=#00B300, AMBER=#FFB300, RED=#CC0000 (MIL-STD-2525)

DELIVERABLE:
  Assets/RenderTextures/ — 4 RT assets
  Assets/Scripts/VR/VRHudController.cs
  VRSentryPost.unity updated (4 floating quads + labels + threat torus)
  4 Unlit emissive materials

TEST:
  Play VRSentryPost. Launch Punjab scenario from C2 via WebSocket.
  → RF Waterfall scrolling on left panel
  → Doppler scope showing 6 contacts on center panel
  → Acoustic FFT bars on right panel
  → EW/SMASH panel shows controls read-out
  → Threat torus: GREEN→AMBER→RED with scenario progression
  → Panels visible in both left and right eye views

DEPENDS: VG-P1-B (scene + VRRig exist)   UNBLOCKS: VG-P1-E (integration)

DEMO: "Iron Man HUD — four holographic sensor panels glowing in darkness." (Beat 4 wow)
PERSONA: Uday (swarm-ai)   STATUS: [ ] todo
```

---

### VG-P1-D · Phone gyroscope head tracking via WebSocket [timebox: 5h]

```
CONTEXT:
  TWO parts: A) Gyro sender HTML page served from laptop, opened in phone Chrome.
             B) Gyro receiver WebSocket in Unity rotating StereoHead.
  
  Port 9800 used (not 9700 — avoid conflict with game IPC).
  
  Files to read:
    • Assets/Scripts/Network/WsServer.cs (copy the HttpListener WebSocket server pattern)
    • board/contracts/websocket-ipc.md (confirm port 9700 is game IPC; 9800 is free)
    • vr-integration-blueprint.md §Phase 3 (gyro sender HTML design)
  
  Steps:
    PART A — Gyro sender HTML:
    1. Create docs/vr-gyro-sender.html:
       - On tap: call DeviceOrientationEvent.requestPermission() (iOS-style gate, Chrome needs it)
       - window.addEventListener('deviceorientation', ...) → send JSON {a,b,g} via WebSocket
       - WebSocket connects to ws://LAPTOP_IP:9800 (LAPTOP_IP replaced by serve script)
       - Shows "TRACKING" on screen when connected; "DISCONNECTED" in red if dropped
    2. Create docs/serve-gyro.ps1:
       - PowerShell HttpListener on http://+:8080/
       - Reads vr-gyro-sender.html, replaces 'LAPTOP_IP' with actual laptop IP (from $env:COMPUTERNAME
         or ipconfig parsed), returns it to phone browser
       - Phone opens http://LAPTOP_IP:8080 in Chrome
    
    PART B — Gyro receiver Unity:
    3. Create Assets/Scripts/VR/GyroReceiver.cs MonoBehaviour on VRRig:
       - Start(): open WebSocket server on ws://0.0.0.0:9800 using same
         HttpListener pattern as WsServer.cs
       - OnMessage: parse JSON {a (alpha/yaw 0-360), b (beta/pitch -180 to 180), g (gamma/roll -90 to 90)}
       - Convert: targetRot = Quaternion.Euler(-b + pitchOffset, a - yawOffset, -g)
       - Smooth: StereoHead.localRotation = Quaternion.Slerp(current, targetRot, 15f * Time.deltaTime)
       - On Recalibrate (event from GamepadInputController.Recalibrate):
         yawOffset = lastAlpha; pitchOffset = lastBeta → stores zero point
       - If no message for 2s: hold last rotation, log [GyroReceiver] DISCONNECTED
       - public bool IsConnected property (used by VG-P1-E status badge)
    4. Wire Recalibrate event: in GamepadInputController.cs, add:
       public event System.Action OnRecalibrate;
       In Recalibrate.performed handler: OnRecalibrate?.Invoke();
       In GyroReceiver.Start(): find GamepadInputController → subscribe OnRecalibrate

DELIVERABLE:
  docs/vr-gyro-sender.html
  docs/serve-gyro.ps1
  Assets/Scripts/VR/GyroReceiver.cs
  GamepadInputController.cs updated with OnRecalibrate event
  VRRig in VRSentryPost.unity has GyroReceiver component

TEST:
  Run serve-gyro.ps1 on laptop.
  Phone (on laptop hotspot): Chrome → http://LAPTOP_IP:8080 → tap → TRACKING shown.
  Unity Console: [GyroReceiver] Client connected from PHONE_IP.
  Physically rotate phone → StereoHead rotates in Scene view smoothly.
  Press Select on gamepad → rotation resets to forward-facing zero.
  Unplug/kill phone → no errors; last rotation held.

DEPENDS: VG-P1-A (Recalibrate event), VG-P1-B (VRRig/StereoHead exist)   UNBLOCKS: VG-P1-E (GyroReceiver.IsConnected used for status badge)

DEMO: "Trainee turns head — radar scope follows left. Acoustic sonar follows right." (Beat 3)
PERSONA: Sohail (aar-engine)   STATUS: [ ] todo
```

---

### VG-P1-E · spacedesk display mirror + VR mode toggle [timebox: 3h]

```
CONTEXT:
  spacedesk makes the phone appear as Windows Display 2.
  Unity Game view → Display 2 → phone screen → VR BOX lenses.
  Instructor C2 gets a VR MODE button to load VRSentryPost scene.
  
  Files to read:
    • Assets/Scripts/UI/C2StationController.cs (how mode toggle works, buttons wired)
    • Assets/Scripts/Scenarios/ScenarioManager.cs (OperationMode enum)
    • docs/vr-integration-blueprint.md §Revised Architecture (spacedesk section)
  
  Manual pre-requisites (Vinod does once, outside code):
    a. Download spacedesk DRIVER for Windows: https://www.spacedesk.net
    b. Install spacedesk Android app on phone (Play Store)
    c. Enable Windows WiFi hotspot (Settings → Network → Mobile hotspot)
    d. Connect phone to hotspot; open spacedesk app on phone → select laptop
    e. Verify phone appears as Display 2 in Windows Display Settings
  
  Steps:
    1. ScenarioManager.cs: add VRMode to OperationMode enum
    2. C2StationController.cs: add [VR MODE] Button to Instructor C2 UITK layout
       On click → ScenarioManager.Instance.SetMode(OperationMode.VRMode)
    3. Create Assets/Scripts/VR/VRModeController.cs MonoBehaviour (persistent GO):
       - Subscribe to ScenarioManager.OnModeChanged event
       - On VRMode:
         a. SceneManager.LoadScene("VRSentryPost") or LoadSceneAsync
         b. After load: find LeftEyeCamera and RightEyeCamera → camera.targetDisplay = 1 (Display 2)
         c. C2 cameras stay on targetDisplay = 0 (Display 1 / laptop)
       - On exit VRMode:
         a. SceneManager.LoadScene("TraineeCockpit") or activate TraineeCockpit root in split-screen
         b. Reset any camera targetDisplay = 0
    4. Add GyroReceiver.IsConnected status to C2 UI badge:
       "VR MODE: ON ● GYRO: LIVE" when connected
       "VR MODE: ON ○ GYRO: OFFLINE" when not
    5. Create docs/vr-setup-guide.md:
       # VR Demo Setup — 6 Steps
       Step 1: Install spacedesk driver on laptop (one-time)
       Step 2: Install spacedesk app on Android phone (one-time)
       Step 3: Enable Windows WiFi hotspot on laptop
       Step 4: Phone → connect to laptop hotspot
       Step 5: Phone → open spacedesk app → select laptop (becomes Display 2)
       Step 6: Phone → open Chrome → http://LAPTOP_IP:8080 → tap TRACKING
       Then: C2 Station → click [VR MODE] → trainee puts on VR BOX

DELIVERABLE:
  Assets/Scripts/VR/VRModeController.cs
  ScenarioManager.cs updated (VRMode enum)
  C2StationController.cs updated (VR MODE button)
  docs/vr-setup-guide.md

TEST:
  WITH spacedesk running (phone as Display 2):
    C2: click VR MODE → VRSentryPost loads; Game view on Display 2 (phone)
    Laptop screen: Instructor C2 stays on Display 1
    Launch Punjab scenario → all HUD panels receive data
  WITHOUT phone (test fallback):
    VR MODE loads VRSentryPost on Display 1 in split-stereo (still demoable)
  VR MODE OFF → desktop Trainee Cockpit back → no regression

DEPENDS: VG-P1-B, VG-P1-C, VG-P1-D   UNBLOCKS: VG-P1-F

DEMO: "Instructor clicks VR MODE — trainee puts on headset — border post appears in the dark." (Beat 1)
PERSONA: Uday (swarm-ai)   STATUS: [ ] todo
```

---

### VG-P1-F · Integration smoke + VR demo rehearsal [timebox: 3h]

```
CONTEXT:
  All VG cards complete. Structured test card — no new code. Fix bugs found.
  Measures actual head tracking latency and display latency. Documents risk notes for demo day.
  
  Files to read:
    • docs/PRD.md §3 (all 6 beats)
    • docs/vr-setup-guide.md (6-step setup)
    • board/contracts/scenario-events.md (latency budgets)
    • .agents/rules/40-endgame.md (last-6h doctrine applies after 19 Oct 19:00)
  
  Steps:
    1. Full hardware setup:
       - Laptop WiFi hotspot ON
       - Phone: spacedesk active (Display 2)
       - Phone Chrome: http://LAPTOP_IP:8080 → tap → TRACKING
       - Gen Game gamepad: Bluetooth paired
       - VR BOX Mini: phone inserted
    2. Run all 6 demo beats in VR mode:
       Beat 1: C2 → VR MODE → VRSentryPost on phone, ● GYRO: LIVE shown
       Beat 2: Dark border post; floating HUD panels alive with baseline noise
       Beat 3: Launch Punjab Pincer → RF waterfall erupts → Doppler shows 6 contacts
       Beat 4: Left stick → jammer slew; A button → engage; right trigger → fire SMASH 2000
       Beat 5: Scorecard floating panel appears after scenario end
       Beat 6: VR MODE off → desktop Trainee Cockpit loads cleanly
    3. Measure:
       - Head tracking latency (subjective): log "smooth" / "noticeable lag" / "nausea-inducing"
       - spacedesk stats: check FPS overlay (target ≥30 FPS on phone display)
       - Gamepad input lag: subjective (should be <200ms)
    4. If nausea observed from head tracking: add URP Vignette post-process
       (dark radial vignette that intensifies during rapid rotation, reduces during still)
    5. Log all bugs in board/DECISIONS.md as "VG-BUG-001:", "VG-BUG-002:" etc.
    6. Update board/STATUS.md: "vr-gamepad: DONE — VR demo rehearsal passed — 11 Oct <time>"

DELIVERABLE:
  board/STATUS.md updated
  board/DECISIONS.md: any VG-BUG entries
  docs/vr-setup-guide.md: risk notes added
  Optional: URP Vignette effect if comfort needed

TEST:
  Full 6-beat demo in VR mode runs without crash.
  Desktop mode works after VR toggle-off (no regression).
  Gamepad controls EW + SMASH with subjectively instant response.

DEPENDS: VG-P1-A, VG-P1-B, VG-P1-C, VG-P1-D, VG-P1-E   UNBLOCKS: US-P3-A (submission uses this as VR confirmation)

DEMO: "Full rehearsal — 6 beats back to back." (All beats)
PERSONA: Vinod (unity-sim)   STATUS: [ ] todo
```

---

## Oracle Pass — Wow Moment Analysis

| Card | Demo beat | Oracle verdict |
|---|---|---|
| VG-P1-A | Beat 4 | Gamepad is demoable even without VR — do NOT cut if VG-P1-D fails |
| VG-P1-B | Beat 4 | 3D environment — core of VR experience, only cut if 0 VR at all |
| VG-P1-C | Beat 4 | Floating HUD — the visual wow moment, only cut with VG-P1-B |
| VG-P1-D | Beat 3 | Head tracking — makes it real VR; cut to joystick look if out of time |
| VG-P1-E | Beat 1 | Display mirroring — needed for phone to show VR; cut = VR on laptop only |
| VG-P1-F | All | Integration smoke — compress to 1h not 0h under pressure |

**Cut order at T-3h endgame:** VG-P1-F (compress) → VG-P1-D (joystick look fallback) → VG-P1-E (laptop stereo) → demo desktop only with gamepad (VG-P1-A survives alone)
