// ============================================================
// GamepadInputController.cs — Gamepad & Keyboard Input Actions Controller
// TRISHUL-SIM · VG-P1-A · vr-gamepad track (Vinod)
//
// Implements:
// - Unity Input System (com.unity.inputsystem) binding for Trainee Cockpit HUD:
//   * Left Stick X / (A, D keys)   -> JammerAzimuth (continuous, ±3°/frame at full deflection, 0-359° wrap)
//   * D-Pad Y / (W, S keys)        -> JammerElevation (±2°/press step, clamped -90° to +90°)
//   * Right Trigger / Space        -> FireSMASH (Kinetic fire trigger on performed)
//   * A / buttonSouth / J key      -> ToggleJammer (Toggle EW jammer engagement on performed)
//   * Y / buttonNorth / Equals key -> BandUp (Cycle 2.4 -> 5.825 -> 0.915 GHz)
//   * X / buttonWest / Minus key   -> BandDown (Reverse cycle 0.915 -> 5.825 -> 2.4 GHz)
//   * Select / R key               -> Recalibrate (Fires OnRecalibrate event for GyroReceiver in VG-P1-D)
// - Automatic gamepad discovery logging: "[GamepadInput] Gamepad detected: <device name>"
// - CockpitController parameter synchronization
// ============================================================

using System;
using UnityEngine;
using TrishulSim.UI;

#if UNITY_5_3_OR_NEWER
using UnityEngine.InputSystem;
#endif

namespace TrishulSim.Input
{
    /// <summary>
    /// Handles Gamepad and Keyboard input using Unity's new Input System,
    /// routing actions to CockpitController and providing Recalibrate event for VR head tracking.
    /// </summary>
#if UNITY_5_3_OR_NEWER
    public class GamepadInputController : UnityEngine.MonoBehaviour
#else
    public class GamepadInputController
#endif
    {
#if UNITY_5_3_OR_NEWER
        [SerializeField] private CockpitController cockpit;
        [SerializeField] private float azimuthSensitivity = 3.0f;
        [SerializeField] private float lookSensitivity = 60.0f;
        [SerializeField] private float deadzone = 0.05f;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private TrishulSim.VR.GyroReceiver gyroReceiver;
        [SerializeField] private InputActionAsset inputActionsAsset;
#else
        private CockpitController cockpit;
        private float azimuthSensitivity = 3.0f;
        private float lookSensitivity = 60.0f;
        private float deadzone = 0.05f;
#endif

        // ── State ─────────────────────────────────────────────
        private float currentAzimuth = 0f;
        private int currentElevation = 0;
        private float currentBandGHz = 5.825f;
        private bool currentEngaged = false;
        private int currentBandIndex = 1; // 1 -> 5.825 GHz default

        // ── Camera Look State ─────────────────────────────────
        private float currentCameraYaw = 0f;
        private float currentCameraPitch = 0f;
        private float pitchClampMin = -80f;
        private float pitchClampMax = 80f;

        // Available bands cycle order: 2.4 -> 5.825 -> 0.915 GHz
        private readonly float[] availableBands = new float[]
        {
            2.4f,
            5.825f,
            0.915f
        };

        // ── Events ────────────────────────────────────────────
        /// <summary>
        /// Fired when Select button or R key is pressed. Subscribed by GyroReceiver in VG-P1-D.
        /// </summary>
        public event Action OnRecalibrate;

#if UNITY_5_3_OR_NEWER
        // ── Input System Actions ──────────────────────────────
        private InputActionAsset runtimeActions;
        private InputAction jammerAzimuthAction;
        private InputAction jammerElevationAction;
        private InputAction fireSmashAction;
        private InputAction toggleJammerAction;
        private InputAction bandUpAction;
        private InputAction bandDownAction;
        private InputAction recalibrateAction;
        private InputAction cameraLookAction;
#endif

        // ── Public Accessors ──────────────────────────────────
        public CockpitController Cockpit
        {
            get { return cockpit; }
            set { cockpit = value; }
        }

        public float AzimuthSensitivity
        {
            get { return azimuthSensitivity; }
            set { azimuthSensitivity = value; }
        }

        public float LookSensitivity
        {
            get { return lookSensitivity; }
            set { lookSensitivity = value; }
        }

        public float Deadzone
        {
            get { return deadzone; }
            set { deadzone = value; }
        }

        public float CurrentAzimuth { get { return currentAzimuth; } }
        public int CurrentElevation { get { return currentElevation; } }
        public float CurrentBandGHz { get { return currentBandGHz; } }
        public bool CurrentEngaged { get { return currentEngaged; } }
        public int CurrentBandIndex { get { return currentBandIndex; } }
        public float[] AvailableBands { get { return availableBands; } }

        public float CurrentCameraYaw { get { return currentCameraYaw; } }
        public float CurrentCameraPitch { get { return currentCameraPitch; } }
        public float PitchClampMin { get { return pitchClampMin; } set { pitchClampMin = value; } }
        public float PitchClampMax { get { return pitchClampMax; } set { pitchClampMax = value; } }

#if UNITY_5_3_OR_NEWER
        public Transform CameraTransform
        {
            get { return cameraTransform; }
            set { cameraTransform = value; }
        }

        public TrishulSim.VR.GyroReceiver Gyro
        {
            get { return gyroReceiver; }
            set { gyroReceiver = value; }
        }

        public InputActionAsset RuntimeActions { get { return runtimeActions; } }
        public InputAction JammerAzimuthAction { get { return jammerAzimuthAction; } }
        public InputAction JammerElevationAction { get { return jammerElevationAction; } }
        public InputAction FireSmashAction { get { return fireSmashAction; } }
        public InputAction ToggleJammerAction { get { return toggleJammerAction; } }
        public InputAction BandUpAction { get { return bandUpAction; } }
        public InputAction BandDownAction { get { return bandDownAction; } }
        public InputAction RecalibrateAction { get { return recalibrateAction; } }
        public InputAction CameraLookAction { get { return cameraLookAction; } }
#endif

        // ══════════════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            ResolveCockpit();
            InitializeInputActions();
        }

        private void Start()
        {
            ResolveCockpit();
            SyncWithCockpitState();
            DetectConnectedGamepad();
        }

        private void OnEnable()
        {
            ResolveCockpit();
            InitializeInputActions();
            SubscribeActions();
            DetectConnectedGamepad();
#if UNITY_5_3_OR_NEWER
            InputSystem.onDeviceChange -= HandleDeviceChange;
            InputSystem.onDeviceChange += HandleDeviceChange;
#endif
        }

        private void OnDisable()
        {
            UnsubscribeActions();
#if UNITY_5_3_OR_NEWER
            InputSystem.onDeviceChange -= HandleDeviceChange;
#endif
        }

        private void OnDestroy()
        {
#if UNITY_5_3_OR_NEWER
            if (runtimeActions != null)
            {
                UnityEngine.Object.Destroy(runtimeActions);
                runtimeActions = null;
            }
#endif
        }

        private void Update()
        {
            ResolveCockpit();
            ResolveCameraTransform();

#if UNITY_5_3_OR_NEWER
            if (runtimeActions == null)
            {
                InitializeInputActions();
                SubscribeActions();
            }

            float stickX = 0f;
            if (jammerAzimuthAction != null)
            {
                stickX = jammerAzimuthAction.ReadValue<float>();
            }
            ApplyAzimuthDelta(stickX);

            // Read Camera Look (Right Stick / Arrow Keys)
            Vector2 lookVal = Vector2.zero;
            if (cameraLookAction != null)
            {
                lookVal = cameraLookAction.ReadValue<Vector2>();
            }

            // Fallback 1: Direct Gamepad rightStick polling for maximum hardware/driver compatibility
            if (lookVal.sqrMagnitude < 0.001f && Gamepad.current != null)
            {
                lookVal = Gamepad.current.rightStick.ReadValue();
            }

            // Fallback 2: Keyboard Arrow keys
            if (lookVal.sqrMagnitude < 0.001f && Keyboard.current != null)
            {
                float kx = 0f;
                float ky = 0f;
                if (Keyboard.current.rightArrowKey.isPressed) kx += 1f;
                if (Keyboard.current.leftArrowKey.isPressed) kx -= 1f;
                if (Keyboard.current.upArrowKey.isPressed) ky += 1f;
                if (Keyboard.current.downArrowKey.isPressed) ky -= 1f;
                lookVal = new Vector2(kx, ky);
            }

            ApplyCameraLook(lookVal.x, lookVal.y);
#endif
        }

        // ══════════════════════════════════════════════════════
        // Initialization & Binding
        // ══════════════════════════════════════════════════════

        public void ResolveCockpit()
        {
            if (cockpit == null)
            {
#if UNITY_5_3_OR_NEWER
                cockpit = GetComponent<CockpitController>();
                if (cockpit == null)
                {
#if UNITY_2023_1_OR_NEWER
                    cockpit = FindAnyObjectByType<CockpitController>();
#else
                    cockpit = FindObjectOfType<CockpitController>();
#endif
                }
#endif
            }
        }

        public void SyncWithCockpitState()
        {
            if (cockpit != null && cockpit.EwController != null)
            {
                var ew = cockpit.EwController;
                currentAzimuth = ew.AzimuthDeg;
                currentElevation = ew.ElevationDeg;
                currentBandGHz = ew.SelectedBandGHz;
                currentEngaged = ew.IsEngaged;

                for (int i = 0; i < availableBands.Length; i++)
                {
                    if (UnityEngine.Mathf.Abs(availableBands[i] - currentBandGHz) < 0.05f)
                    {
                        currentBandIndex = i;
                        break;
                    }
                }
            }
        }

        public void InitializeInputActions()
        {
#if UNITY_5_3_OR_NEWER
            if (runtimeActions != null) return;

            if (inputActionsAsset != null)
            {
                runtimeActions = UnityEngine.Object.Instantiate(inputActionsAsset);
            }
            else
            {
                // Fallback 1: Resources load
                runtimeActions = Resources.Load<InputActionAsset>("Input/TrishulInputActions");
                // Fallback 2: From canonical JSON
                if (runtimeActions == null)
                {
                    runtimeActions = InputActionAsset.FromJson(CanonicalInputActionsJson);
                }
            }

            if (runtimeActions != null)
            {
                var map = runtimeActions.FindActionMap("Trainee", false) ?? runtimeActions.FindActionMap("trainee", false);
                if (map != null)
                {
                    jammerAzimuthAction   = map.FindAction("JammerAzimuth");
                    jammerElevationAction = map.FindAction("JammerElevation");
                    fireSmashAction       = map.FindAction("FireSMASH");
                    toggleJammerAction    = map.FindAction("ToggleJammer");
                    bandUpAction          = map.FindAction("BandUp");
                    bandDownAction        = map.FindAction("BandDown");
                    recalibrateAction     = map.FindAction("Recalibrate");
                    cameraLookAction      = map.FindAction("CameraLook") ?? map.FindAction("Look");
                }
            }
#endif
        }

        public void DetectConnectedGamepad()
        {
#if UNITY_5_3_OR_NEWER
            if (Gamepad.current != null)
            {
                UnityEngine.Debug.Log(string.Format("[GamepadInput] Gamepad detected: {0}", Gamepad.current.displayName));
            }
#endif
        }

#if UNITY_5_3_OR_NEWER
        private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad && (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected || change == InputDeviceChange.Enabled))
            {
                UnityEngine.Debug.Log(string.Format("[GamepadInput] Gamepad detected: {0}", device.displayName));
            }
        }
#endif

        private void SubscribeActions()
        {
#if UNITY_5_3_OR_NEWER
            if (runtimeActions != null)
            {
                runtimeActions.Enable();
            }

            if (jammerElevationAction != null)
                jammerElevationAction.performed += HandleElevationPerformed;
            if (fireSmashAction != null)
                fireSmashAction.performed += HandleFirePerformed;
            if (toggleJammerAction != null)
                toggleJammerAction.performed += HandleToggleJammerPerformed;
            if (bandUpAction != null)
                bandUpAction.performed += HandleBandUpPerformed;
            if (bandDownAction != null)
                bandDownAction.performed += HandleBandDownPerformed;
            if (recalibrateAction != null)
                recalibrateAction.performed += HandleRecalibratePerformed;
#endif
        }

        private void UnsubscribeActions()
        {
#if UNITY_5_3_OR_NEWER
            if (jammerElevationAction != null)
                jammerElevationAction.performed -= HandleElevationPerformed;
            if (fireSmashAction != null)
                fireSmashAction.performed -= HandleFirePerformed;
            if (toggleJammerAction != null)
                toggleJammerAction.performed -= HandleToggleJammerPerformed;
            if (bandUpAction != null)
                bandUpAction.performed -= HandleBandUpPerformed;
            if (bandDownAction != null)
                bandDownAction.performed -= HandleBandDownPerformed;
            if (recalibrateAction != null)
                recalibrateAction.performed -= HandleRecalibratePerformed;

            if (runtimeActions != null)
            {
                runtimeActions.Disable();
            }

            InputSystem.onDeviceChange -= HandleDeviceChange;
#endif
        }

        // ══════════════════════════════════════════════════════
        // Action Handlers & Logic Methods
        // ══════════════════════════════════════════════════════

#if UNITY_5_3_OR_NEWER
        private void HandleElevationPerformed(InputAction.CallbackContext ctx)
        {
            ApplyElevationStep(ctx.ReadValue<float>());
        }

        private void HandleFirePerformed(InputAction.CallbackContext ctx)
        {
            TriggerFire();
        }

        private void HandleToggleJammerPerformed(InputAction.CallbackContext ctx)
        {
            ToggleJammer();
        }

        private void HandleBandUpPerformed(InputAction.CallbackContext ctx)
        {
            StepBand(1);
        }

        private void HandleBandDownPerformed(InputAction.CallbackContext ctx)
        {
            StepBand(-1);
        }

        private void HandleRecalibratePerformed(InputAction.CallbackContext ctx)
        {
            TriggerRecalibrate();
        }
#endif

        /// <summary>
        /// Continuous Left Stick X / (A, D) axis deflection update.
        /// Accumulates azimuth at ±3°/frame at full deflection, wrapping 0-359°.
        /// </summary>
        public void ApplyAzimuthDelta(float stickX)
        {
            if (UnityEngine.Mathf.Abs(stickX) > 0.05f)
            {
                if (cockpit != null && cockpit.EwController != null)
                {
                    int cockpitAz = cockpit.EwController.AzimuthDeg;
                    int localAzInt = ((UnityEngine.Mathf.RoundToInt(currentAzimuth) % 360) + 360) % 360;
                    if (Math.Abs(cockpitAz - localAzInt) > 1)
                    {
                        currentAzimuth = cockpitAz;
                    }
                    currentElevation = cockpit.EwController.ElevationDeg;
                    currentBandGHz = cockpit.EwController.SelectedBandGHz;
                    currentEngaged = cockpit.EwController.IsEngaged;
                }

                currentAzimuth += stickX * azimuthSensitivity;
                // Wrap 0-359°
                currentAzimuth = ((currentAzimuth % 360f) + 360f) % 360f;
                PushStateToCockpit();
            }
        }

        /// <summary>
        /// D-Pad Y / (W, S) step.
        /// ±2° step per press, clamped between -90° and +90°.
        /// </summary>
        public void ApplyElevationStep(float val)
        {
            if (val > 0.1f)
            {
                if (cockpit != null && cockpit.EwController != null)
                {
                    currentAzimuth = cockpit.EwController.AzimuthDeg;
                    currentElevation = cockpit.EwController.ElevationDeg;
                    currentBandGHz = cockpit.EwController.SelectedBandGHz;
                    currentEngaged = cockpit.EwController.IsEngaged;
                }
                currentElevation = UnityEngine.Mathf.Clamp(currentElevation + 2, -90, 90);
                PushStateToCockpit();
            }
            else if (val < -0.1f)
            {
                if (cockpit != null && cockpit.EwController != null)
                {
                    currentAzimuth = cockpit.EwController.AzimuthDeg;
                    currentElevation = cockpit.EwController.ElevationDeg;
                    currentBandGHz = cockpit.EwController.SelectedBandGHz;
                    currentEngaged = cockpit.EwController.IsEngaged;
                }
                currentElevation = UnityEngine.Mathf.Clamp(currentElevation - 2, -90, 90);
                PushStateToCockpit();
            }
        }

        /// <summary>
        /// Right Trigger / Spacebar: fires SMASH 2000 electro-optical kinetic shot.
        /// </summary>
        public void TriggerFire()
        {
            if (cockpit != null)
            {
                cockpit.TriggerFireKinetic();
            }
        }

        /// <summary>
        /// A button / ButtonSouth / J key: toggles EW jammer engagement.
        /// </summary>
        public void ToggleJammer()
        {
            if (cockpit != null && cockpit.EwController != null)
            {
                currentAzimuth = cockpit.EwController.AzimuthDeg;
                currentElevation = cockpit.EwController.ElevationDeg;
                currentBandGHz = cockpit.EwController.SelectedBandGHz;
                currentEngaged = !cockpit.EwController.IsEngaged;
            }
            else
            {
                currentEngaged = !currentEngaged;
            }
            PushStateToCockpit();
        }

        /// <summary>
        /// Y button / ButtonNorth / Equals (direction +1) -> BandUp: cycles 2.4 -> 5.825 -> 0.915 GHz.
        /// X button / ButtonWest / Minus (direction -1) -> BandDown: reverse cycle.
        /// </summary>
        public void StepBand(int direction)
        {
            if (cockpit != null && cockpit.EwController != null)
            {
                currentAzimuth = cockpit.EwController.AzimuthDeg;
                currentElevation = cockpit.EwController.ElevationDeg;
                currentEngaged = cockpit.EwController.IsEngaged;

                for (int i = 0; i < availableBands.Length; i++)
                {
                    if (UnityEngine.Mathf.Abs(availableBands[i] - cockpit.EwController.SelectedBandGHz) < 0.05f)
                    {
                        currentBandIndex = i;
                        break;
                    }
                }
            }

            if (direction > 0)
            {
                currentBandIndex = (currentBandIndex + 1) % availableBands.Length;
            }
            else if (direction < 0)
            {
                currentBandIndex = (currentBandIndex - 1 + availableBands.Length) % availableBands.Length;
            }
            currentBandGHz = availableBands[currentBandIndex];
            PushStateToCockpit();
        }

        /// <summary>
        /// Reads right-stick or arrow key deflection and rotates the camera (pitch and yaw).
        /// Uses Time.deltaTime for smooth frame-rate independent rotation.
        /// </summary>
        public void ApplyCameraLook(float lookX, float lookY)
        {
#if UNITY_5_3_OR_NEWER
            float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.02f;
#else
            float dt = 0.02f;
#endif
            ApplyCameraLookDelta(lookX, lookY, dt);
        }

        public void ApplyCameraLookDelta(float lookX, float lookY, float dt)
        {
            float magSq = lookX * lookX + lookY * lookY;
            if (magSq > deadzone * deadzone)
            {
                float yawDelta = lookX * lookSensitivity * dt;
                float pitchDelta = -lookY * lookSensitivity * dt;

                currentCameraYaw += yawDelta;
                currentCameraYaw = ((currentCameraYaw % 360f) + 360f) % 360f;

                currentCameraPitch = ClampFloat(currentCameraPitch + pitchDelta, pitchClampMin, pitchClampMax);

                ResolveCameraTransform();

#if UNITY_5_3_OR_NEWER
                if (gyroReceiver != null)
                {
                    gyroReceiver.AddLookOffset(yawDelta, pitchDelta);
                }
                else if (cameraTransform != null)
                {
                    cameraTransform.localRotation = Quaternion.Euler(currentCameraPitch, currentCameraYaw, 0f);
                }
#endif
            }
        }

        private static float ClampFloat(float val, float min, float max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        public void ResolveCameraTransform()
        {
#if UNITY_5_3_OR_NEWER
            if (gyroReceiver == null)
            {
                gyroReceiver = GetComponent<TrishulSim.VR.GyroReceiver>();
                if (gyroReceiver == null)
                {
#if UNITY_2023_1_OR_NEWER
                    gyroReceiver = FindAnyObjectByType<TrishulSim.VR.GyroReceiver>();
#else
                    gyroReceiver = FindObjectOfType<TrishulSim.VR.GyroReceiver>();
#endif
                }
            }

            if (cameraTransform == null)
            {
                // 1. If GyroReceiver exists and has StereoHead
                if (gyroReceiver != null && gyroReceiver.StereoHead != null)
                {
                    cameraTransform = gyroReceiver.StereoHead;
                }
                else
                {
                    // 2. Check for StereoHead in hierarchy / scene
                    Transform head = transform.Find("StereoHead");
                    if (head == null)
                    {
                        GameObject go = GameObject.Find("StereoHead");
                        if (go != null) head = go.transform;
                    }

                    if (head != null)
                    {
                        cameraTransform = head;
                    }
                    else
                    {
                        // 3. Check for CameraCockpit
                        Transform camCockpit = transform.Find("CameraCockpit");
                        if (camCockpit == null)
                        {
                            GameObject go = GameObject.Find("CameraCockpit");
                            if (go != null) camCockpit = go.transform;
                        }

                        if (camCockpit != null)
                        {
                            cameraTransform = camCockpit;
                        }
                        else if (Camera.main != null)
                        {
                            cameraTransform = Camera.main.transform;
                        }
                        else
                        {
#if UNITY_2023_1_OR_NEWER
                            Camera cam = FindAnyObjectByType<Camera>();
#else
                            Camera cam = FindObjectOfType<Camera>();
#endif
                            if (cam != null) cameraTransform = cam.transform;
                        }
                    }
                }

                if (cameraTransform != null && gyroReceiver == null)
                {
                    Vector3 euler = cameraTransform.localEulerAngles;
                    currentCameraYaw = euler.y;
                    currentCameraPitch = euler.x > 180f ? euler.x - 360f : euler.x;
                }
            }
#endif
        }

        /// <summary>
        /// Select button / R key: triggers head tracking zero-point recalibration
        /// and resets camera look angles.
        /// Subscribed by GyroReceiver in VG-P1-D.
        /// </summary>
        public void TriggerRecalibrate()
        {
            currentCameraYaw = 0f;
            currentCameraPitch = 0f;
#if UNITY_5_3_OR_NEWER
            ResolveCameraTransform();
            if (gyroReceiver == null && cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.identity;
            }
#endif
            UnityEngine.Debug.Log("[GamepadInput] Recalibrate event triggered.");
            if (OnRecalibrate != null)
            {
                OnRecalibrate.Invoke();
            }
        }

        /// <summary>
        /// Pushes current EW state to CockpitController.SetEwParameters.
        /// </summary>
        public void PushStateToCockpit()
        {
            if (cockpit != null)
            {
                int safeAzimuth = ((UnityEngine.Mathf.RoundToInt(currentAzimuth) % 360) + 360) % 360;
                cockpit.SetEwParameters(
                    safeAzimuth,
                    currentElevation,
                    currentBandGHz,
                    currentEngaged
                );
            }
        }

        // ══════════════════════════════════════════════════════
        // Canonical JSON definition for fallback / tests
        // ══════════════════════════════════════════════════════
        public const string CanonicalInputActionsJson = @"{
    ""version"": 1,
    ""name"": ""TrishulInputActions"",
    ""maps"": [
        {
            ""name"": ""Trainee"",
            ""id"": ""e49f82d1-0f72-4d2a-8991-628f237ef110"",
            ""actions"": [
                {
                    ""name"": ""JammerAzimuth"",
                    ""type"": ""Value"",
                    ""id"": ""b304c40b-7e61-4fa3-9f50-32b0ebfa8001"",
                    ""expectedControlType"": ""Axis"",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""JammerElevation"",
                    ""type"": ""Value"",
                    ""id"": ""b304c40b-7e61-4fa3-9f50-32b0ebfa8002"",
                    ""expectedControlType"": ""Axis"",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""FireSMASH"",
                    ""type"": ""Button"",
                    ""id"": ""b304c40b-7e61-4fa3-9f50-32b0ebfa8003"",
                    ""expectedControlType"": ""Button"",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""ToggleJammer"",
                    ""type"": ""Button"",
                    ""id"": ""b304c40b-7e61-4fa3-9f50-32b0ebfa8004"",
                    ""expectedControlType"": ""Button"",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""BandUp"",
                    ""type"": ""Button"",
                    ""id"": ""b304c40b-7e61-4fa3-9f50-32b0ebfa8005"",
                    ""expectedControlType"": ""Button"",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""BandDown"",
                    ""type"": ""Button"",
                    ""id"": ""b304c40b-7e61-4fa3-9f50-32b0ebfa8006"",
                    ""expectedControlType"": ""Button"",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Recalibrate"",
                    ""type"": ""Button"",
                    ""id"": ""b304c40b-7e61-4fa3-9f50-32b0ebfa8007"",
                    ""expectedControlType"": ""Button"",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""CameraLook"",
                    ""type"": ""Value"",
                    ""id"": ""b304c40b-7e61-4fa3-9f50-32b0ebfa8008"",
                    ""expectedControlType"": ""Vector2"",
                    ""initialStateCheck"": true
                }
            ],
            ""bindings"": [
                {
                    ""name"": """",
                    ""id"": ""c1a2b3c4-0001-4000-8000-000000000001"",
                    ""path"": ""<Gamepad>/leftStick/x"",
                    ""groups"": ""Gamepad"",
                    ""action"": ""JammerAzimuth"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""1DAxis"",
                    ""id"": ""c1a2b3c4-0001-4000-8000-000000000002"",
                    ""path"": ""1DAxis"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""JammerAzimuth"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""negative"",
                    ""id"": ""c1a2b3c4-0001-4000-8000-000000000003"",
                    ""path"": ""<Keyboard>/a"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""JammerAzimuth"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""positive"",
                    ""id"": ""c1a2b3c4-0001-4000-8000-000000000004"",
                    ""path"": ""<Keyboard>/d"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""JammerAzimuth"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": """",
                    ""id"": ""c1a2b3c4-0002-4000-8000-000000000001"",
                    ""path"": ""<Gamepad>/dpad/y"",
                    ""groups"": ""Gamepad"",
                    ""action"": ""JammerElevation"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""1DAxis"",
                    ""id"": ""c1a2b3c4-0002-4000-8000-000000000002"",
                    ""path"": ""1DAxis"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""JammerElevation"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""negative"",
                    ""id"": ""c1a2b3c4-0002-4000-8000-000000000003"",
                    ""path"": ""<Keyboard>/s"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""JammerElevation"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""positive"",
                    ""id"": ""c1a2b3c4-0002-4000-8000-000000000004"",
                    ""path"": ""<Keyboard>/w"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""JammerElevation"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": """",
                    ""id"": ""c1a2b3c4-0003-4000-8000-000000000001"",
                    ""path"": ""<Gamepad>/rightTrigger"",
                    ""groups"": ""Gamepad"",
                    ""action"": ""FireSMASH"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""c1a2b3c4-0003-4000-8000-000000000002"",
                    ""path"": ""<Keyboard>/space"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""FireSMASH"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""c1a2b3c4-0004-4000-8000-000000000001"",
                    ""path"": ""<Gamepad>/buttonSouth"",
                    ""groups"": ""Gamepad"",
                    ""action"": ""ToggleJammer"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""c1a2b3c4-0004-4000-8000-000000000002"",
                    ""path"": ""<Keyboard>/j"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""ToggleJammer"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""c1a2b3c4-0005-4000-8000-000000000001"",
                    ""path"": ""<Gamepad>/buttonNorth"",
                    ""groups"": ""Gamepad"",
                    ""action"": ""BandUp"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""c1a2b3c4-0005-4000-8000-000000000002"",
                    ""path"": ""<Keyboard>/equals"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""BandUp"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""c1a2b3c4-0006-4000-8000-000000000001"",
                    ""path"": ""<Gamepad>/buttonWest"",
                    ""groups"": ""Gamepad"",
                    ""action"": ""BandDown"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""c1a2b3c4-0006-4000-8000-000000000002"",
                    ""path"": ""<Keyboard>/minus"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""BandDown"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""c1a2b3c4-0007-4000-8000-000000000001"",
                    ""path"": ""<Gamepad>/select"",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Recalibrate"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""c1a2b3c4-0007-4000-8000-000000000002"",
                    ""path"": ""<Keyboard>/r"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""Recalibrate"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""c1a2b3c4-0008-4000-8000-000000000001"",
                    ""path"": ""<Gamepad>/rightStick"",
                    ""groups"": ""Gamepad"",
                    ""action"": ""CameraLook"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""2DVector"",
                    ""id"": ""c1a2b3c4-0008-4000-8000-000000000002"",
                    ""path"": ""2DVector"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""CameraLook"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""up"",
                    ""id"": ""c1a2b3c4-0008-4000-8000-000000000003"",
                    ""path"": ""<Keyboard>/upArrow"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""CameraLook"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""down"",
                    ""id"": ""c1a2b3c4-0008-4000-8000-000000000004"",
                    ""path"": ""<Keyboard>/downArrow"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""CameraLook"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""left"",
                    ""id"": ""c1a2b3c4-0008-4000-8000-000000000005"",
                    ""path"": ""<Keyboard>/leftArrow"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""CameraLook"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""right"",
                    ""id"": ""c1a2b3c4-0008-4000-8000-000000000006"",
                    ""path"": ""<Keyboard>/rightArrow"",
                    ""groups"": ""Keyboard"",
                    ""action"": ""CameraLook"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                }
            ]
        }
    ],
    ""controlSchemes"": [
        {
            ""name"": ""Gamepad"",
            ""bindingGroup"": ""Gamepad"",
            ""devices"": [
                {
                    ""devicePath"": ""<Gamepad>"",
                    ""isOptional"": false,
                    ""isOR"": false
                }
            ]
        },
        {
            ""name"": ""Keyboard"",
            ""bindingGroup"": ""Keyboard"",
            ""devices"": [
                {
                    ""devicePath"": ""<Keyboard>"",
                    ""isOptional"": false,
                    ""isOR"": false
                }
            ]
        }
    ]
}";
    }
}
