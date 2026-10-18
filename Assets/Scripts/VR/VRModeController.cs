// ============================================================
// VRModeController.cs — Spacedesk Display Mirror & VR Mode Toggle
// TRISHUL-SIM · VG-P1-E · vr-gamepad track (Uday)
//
// Manages dual-display routing for spacedesk VR streaming and desktop fallback:
// 1. Spacedesk mode (phone connected as Windows Display 2):
//    - LeftEyeCamera + RightEyeCamera -> targetDisplay = 1 (Display 2 / phone)
//    - Instructor C2 camera -> targetDisplay = 0 (Display 1 / laptop)
//    - Activates Display 2 via Display.displays[1].Activate()
// 2. Laptop-only stereo fallback (phone absent / test mode):
//    - LeftEyeCamera + RightEyeCamera -> targetDisplay = 0 (Display 1)
//    - ViewportRects [0, 0, 0.5, 1] and [0.5, 0, 0.5, 1] for side-by-side stereo
// 3. VR Mode Toggle lifecycle:
//    - Subscribes to ScenarioManager.OnOperationModeChanged
//    - Loads VRSentryPost scene on VR enter
//    - Loads TraineeCockpit / restores previous mode on VR exit
//    - C2 badge status query integration ("VR MODE: ON ● GYRO: LIVE" / "OFFLINE")
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TrishulSim.Scenarios;

namespace TrishulSim.VR
{
    /// <summary>
    /// Persistent controller managing VR mode transitions, dual-display output to spacedesk,
    /// camera targetDisplay routing, and gyro head tracking status querying.
    /// </summary>
    public class VRModeController : MonoBehaviour
    {
        private static VRModeController _instance;
        public static VRModeController Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_5_3_OR_NEWER
                    _instance = FindAnyObjectByType<VRModeController>();
                    if (_instance == null)
                    {
                        var go = new GameObject("VRModeController");
                        _instance = go.AddComponent<VRModeController>();
                        if (Application.isPlaying)
                        {
                            DontDestroyOnLoad(go);
                        }
                    }
#endif
                }
                return _instance;
            }
            private set { _instance = value; }
        }

#if UNITY_5_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (_instance == null)
            {
                var inst = Instance;
            }
        }
#endif

        public const string DefaultVrSceneName = "VRSentryPost";
        public const string DefaultDesktopCockpitSceneName = "TraineeCockpit";
        public const string DefaultInstructorC2SceneName = "InstructorC2";

        [Header("Scene Configuration")]
        [SerializeField] private string vrSceneName = DefaultVrSceneName;
        [SerializeField] private string desktopCockpitSceneName = DefaultDesktopCockpitSceneName;
        [SerializeField] private string instructorC2SceneName = DefaultInstructorC2SceneName;

        [Header("Display Configuration")]
        [SerializeField] private bool autoActivateSecondaryDisplay = true;
        [SerializeField] private int vrTargetDisplay = 1;        // Display 2 (0-indexed 1)
        [SerializeField] private int desktopTargetDisplay = 0;   // Display 1 (0-indexed 0)

        [Header("Camera References (Auto-discovered if unassigned)")]
        [SerializeField] private Camera leftEyeCamera;
        [SerializeField] private Camera rightEyeCamera;
        [SerializeField] private Camera c2Camera;

        // ── State ────────────────────────────────────────────────
        private bool isVrActive = false;
        private OperationMode previousMode = OperationMode.TwoLaptop;
        private bool _lastDisplay2State = false;

        // ── Testing / Simulation Overrides ───────────────────────
        public static bool? ForceDisplay2Available = null;
        public static bool? ForceGyroConnected = null;

        // ── Public Accessors ─────────────────────────────────────
        public bool IsVRActive { get { return isVrActive; } }
        public OperationMode PreviousMode { get { return previousMode; } set { previousMode = value; } }
        public string VrSceneName { get { return vrSceneName; } set { vrSceneName = value; } }
        public string DesktopCockpitSceneName { get { return desktopCockpitSceneName; } set { desktopCockpitSceneName = value; } }
        public string InstructorC2SceneName { get { return instructorC2SceneName; } set { instructorC2SceneName = value; } }
        public Camera LeftEyeCamera { get { return leftEyeCamera; } set { leftEyeCamera = value; } }
        public Camera RightEyeCamera { get { return rightEyeCamera; } set { rightEyeCamera = value; } }
        public Camera C2Camera { get { return c2Camera; } set { c2Camera = value; } }
        public int VrTargetDisplay { get { return vrTargetDisplay; } set { vrTargetDisplay = value; } }
        public int DesktopTargetDisplay { get { return desktopTargetDisplay; } set { desktopTargetDisplay = value; } }

        /// <summary>
        /// True when spacedesk secondary display (phone) is connected and active.
        /// </summary>
        public bool IsDisplay2Connected
        {
            get
            {
                if (ForceDisplay2Available.HasValue)
                    return ForceDisplay2Available.Value;

                return Display.displays != null && Display.displays.Length > 1;
            }
        }

        /// <summary>
        /// Queries phone gyroscope connection status.
        /// VG-P1-E mock path: returns false if GyroReceiver is offline or not yet merged (VG-P1-D).
        /// </summary>
        public bool IsGyroConnected
        {
            get
            {
                if (ForceGyroConnected.HasValue)
                    return ForceGyroConnected.Value;

                return QueryGyroReceiver();
            }
        }

        // ══════════════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
#if UNITY_5_3_OR_NEWER
                if (transform.parent != null)
                    transform.SetParent(null);
                if (Application.isPlaying)
                    DontDestroyOnLoad(gameObject);
                SceneManager.sceneLoaded += OnSceneLoaded;
#endif
            }
            else if (_instance != this)
            {
#if UNITY_5_3_OR_NEWER
                Destroy(gameObject);
#endif
                return;
            }

#if UNITY_5_3_OR_NEWER
            // Hook C2StationController gyro query delegate
            UI.C2StationController.GyroStatusQuery = () => this.IsGyroConnected;
#endif
        }

        private void Start()
        {
            SubscribeScenarioManager();
            FindCameras();
#if UNITY_EDITOR
            ApplyEditorPreviewMode();
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// In the Unity Editor without a secondary display, shows both eye cameras
        /// side-by-side on Display 1 (left half = left eye, right half = right eye)
        /// so the VR stereo split is visible when pressing Play during development.
        /// </summary>
        private void ApplyEditorPreviewMode()
        {
            if (IsDisplay2Connected) return; // Phone attached — normal stereo path

            if (leftEyeCamera != null)
            {
                leftEyeCamera.targetDisplay = 0;
                leftEyeCamera.rect = new Rect(0f, 0f, 0.5f, 1f);
                leftEyeCamera.enabled = true;
                Debug.Log("[VRModeController] Editor preview: LeftEyeCamera → left half of Display 1 (stereo split).");
            }

            if (rightEyeCamera != null)
            {
                rightEyeCamera.targetDisplay = 0;
                rightEyeCamera.rect = new Rect(0.5f, 0f, 0.5f, 1f);
                rightEyeCamera.enabled = true;
                Debug.Log("[VRModeController] Editor preview: RightEyeCamera → right half of Display 1 (stereo split).");
            }
        }
#endif

        public void Update()
        {
            if (isVrActive)
            {
                bool currentDisplay2 = IsDisplay2Connected;
                if (currentDisplay2 != _lastDisplay2State)
                {
                    Debug.Log(string.Format("[VRModeController] Secondary display connection state changed: {0} -> {1}. Re-routing cameras.", _lastDisplay2State, currentDisplay2));
                    _lastDisplay2State = currentDisplay2;
                    if (currentDisplay2 && autoActivateSecondaryDisplay && Display.displays != null && Display.displays.Length > 1)
                    {
                        try
                        {
                            if (!Display.displays[1].active)
                                Display.displays[1].Activate();
                        }
                        catch { }
                    }
                    ConfigureCameras(currentDisplay2 ? vrTargetDisplay : desktopTargetDisplay);
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
#if UNITY_5_3_OR_NEWER
                UI.C2StationController.GyroStatusQuery = null;
                SceneManager.sceneLoaded -= OnSceneLoaded;
#endif
                UnsubscribeScenarioManager();
            }
        }

        // ══════════════════════════════════════════════════════
        // ScenarioManager Subscription
        // ══════════════════════════════════════════════════════

        private void SubscribeScenarioManager()
        {
            if (ScenarioManager.Instance != null)
            {
                ScenarioManager.Instance.OnOperationModeChanged -= HandleOperationModeChanged;
                ScenarioManager.Instance.OnOperationModeChanged += HandleOperationModeChanged;
                if (ScenarioManager.Instance.CurrentMode == OperationMode.VRMode)
                {
                    EnterVRMode(transitionScene: false);
                }
                else
                {
                    previousMode = ScenarioManager.Instance.CurrentMode;
                }
            }
        }

        private void UnsubscribeScenarioManager()
        {
            if (ScenarioManager.Instance != null)
            {
                ScenarioManager.Instance.OnOperationModeChanged -= HandleOperationModeChanged;
            }
        }

        public void HandleOperationModeChanged(OperationMode mode)
        {
            if (mode == OperationMode.VRMode)
            {
                if (!isVrActive)
                {
                    EnterVRMode(transitionScene: false);
                }
            }
            else
            {
                if (!isVrActive)
                {
                    previousMode = mode;
                }
                else
                {
                    previousMode = mode;
                    ExitVRMode(transitionScene: false);
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // VR Mode Entry & Exit
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Transitions into VR Mode. Activates secondary display if spacedesk is connected,
        /// assigns stereo cameras to Display 2 (or Display 1 fallback), and loads VRSentryPost.
        /// </summary>
        public void EnterVRMode(bool transitionScene = true)
        {
            Debug.Log("[VRModeController] Entering VR Mode...");
            if (ScenarioManager.Instance != null && ScenarioManager.Instance.CurrentMode != OperationMode.VRMode)
            {
                previousMode = ScenarioManager.Instance.CurrentMode;
            }
            isVrActive = true;
            _lastDisplay2State = IsDisplay2Connected;

            // Step 1: Detect and activate secondary display (phone via spacedesk)
            bool hasDisplay2 = IsDisplay2Connected;
            if (hasDisplay2)
            {
                Debug.Log("[VRModeController] Spacedesk secondary display detected. Activating Display 2.");
                if (autoActivateSecondaryDisplay && Display.displays != null && Display.displays.Length > 1)
                {
                    try
                    {
                        if (!Display.displays[1].active)
                            Display.displays[1].Activate();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[VRModeController] Could not activate Display 2: " + ex.Message);
                    }
                }
            }
            else
            {
                Debug.LogWarning("[VRModeController] Phone secondary display not detected — running in laptop stereo fallback mode (Display 1).");
            }

            // Step 2: Configure stereo camera routing
            int activeDisplay = hasDisplay2 ? vrTargetDisplay : desktopTargetDisplay;
            ConfigureCameras(activeDisplay);

            // Step 3: Scene transition if requested
            if (transitionScene)
            {
                if (ScenarioManager.Instance != null && ScenarioManager.Instance.CurrentMode != OperationMode.VRMode)
                {
                    ScenarioManager.Instance.SetOperationMode(OperationMode.VRMode);
                }
                else
                {
                    Debug.Log("[VRModeController] Loading VR scene: " + vrSceneName);
                    SceneLoader.LoadAsync(vrSceneName);
                }
            }
        }

        /// <summary>
        /// Exits VR Mode and restores desktop mode without regression.
        /// </summary>
        public void ExitVRMode(bool transitionScene = true)
        {
            Debug.Log("[VRModeController] Exiting VR Mode...");
            isVrActive = false;

            // Reset camera displays
            ResetCameras();

            // Scene transition if requested
            if (transitionScene)
            {
                OperationMode restoreMode = (previousMode == OperationMode.VRMode) ? OperationMode.TwoLaptop : previousMode;
                if (ScenarioManager.Instance != null && ScenarioManager.Instance.CurrentMode == OperationMode.VRMode)
                {
                    ScenarioManager.Instance.SetOperationMode(restoreMode);
                }
                else
                {
                    string targetScene = (restoreMode == OperationMode.SingleLaptopSplit)
                        ? (ScenarioManager.Instance != null ? ScenarioManager.Instance.SplitScreenSceneName : "SplitScreen")
                        : desktopCockpitSceneName;
                    Debug.Log("[VRModeController] Restoring desktop scene: " + targetScene);
                    SceneLoader.LoadAsync(targetScene);
                }
            }
        }

        /// <summary>
        /// Toggles VR Mode on or off.
        /// </summary>
        public void ToggleVRMode()
        {
            if (isVrActive)
                ExitVRMode(transitionScene: true);
            else
                EnterVRMode(transitionScene: true);
        }

        // ══════════════════════════════════════════════════════
        // Camera Routing & Display Configuration
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Configures LeftEyeCamera and RightEyeCamera target displays and Viewport Rects:
        /// - Left Eye:  Rect(0.0, 0.0, 0.5, 1.0)
        /// - Right Eye: Rect(0.5, 0.0, 0.5, 1.0)
        /// - TargetDisplay: displayIndex (1 for phone via spacedesk, 0 for laptop stereo fallback)
        /// - C2 Camera: TargetDisplay always stays 0 (laptop screen)
        /// - When spacedesk phone (Display 2) is active, C2 Camera viewport fills Display 1: Rect(0, 0, 1, 1)
        /// </summary>
        public void ConfigureCameras(int targetDisplay)
        {
            FindCameras();

            if (leftEyeCamera != null)
            {
                leftEyeCamera.targetDisplay = targetDisplay;
                leftEyeCamera.rect = new Rect(0.0f, 0.0f, 0.5f, 1.0f);
            }

            if (rightEyeCamera != null)
            {
                rightEyeCamera.targetDisplay = targetDisplay;
                rightEyeCamera.rect = new Rect(0.5f, 0.0f, 0.5f, 1.0f);
            }

            if (c2Camera != null)
            {
                c2Camera.targetDisplay = desktopTargetDisplay; // C2 stays on laptop screen (Display 1)
                if (targetDisplay != desktopTargetDisplay)
                {
                    // Phone displays VR on Display 2; Instructor C2 expands to full primary laptop screen
                    c2Camera.rect = new Rect(0.0f, 0.0f, 1.0f, 1.0f);
                }
            }

            Debug.Log(string.Format(
                "[VRModeController] Configured cameras: Left/Right Eye -> Display {0} (stereo split), C2 -> Display {1}",
                targetDisplay + 1, desktopTargetDisplay + 1));
        }

        /// <summary>
        /// Resets all VR cameras to primary desktop display.
        /// </summary>
        public void ResetCameras()
        {
            FindCameras();

            if (leftEyeCamera != null)
            {
                leftEyeCamera.targetDisplay = desktopTargetDisplay;
                leftEyeCamera.rect = new Rect(0.0f, 0.0f, 0.5f, 1.0f);
            }

            if (rightEyeCamera != null)
            {
                rightEyeCamera.targetDisplay = desktopTargetDisplay;
                rightEyeCamera.rect = new Rect(0.5f, 0.0f, 0.5f, 1.0f);
            }

            if (c2Camera != null)
            {
                c2Camera.targetDisplay = desktopTargetDisplay;
                if (previousMode == OperationMode.SingleLaptopSplit)
                {
                    c2Camera.rect = new Rect(0.0f, 0.0f, 0.5f, 1.0f);
                }
                else
                {
                    c2Camera.rect = new Rect(0.0f, 0.0f, 1.0f, 1.0f);
                }
            }
        }

        /// <summary>
        /// Searches scene for LeftEyeCamera, RightEyeCamera, and C2Camera by name if not already wired.
        /// </summary>
        public void FindCameras()
        {
#if UNITY_5_3_OR_NEWER
            if (leftEyeCamera == null)
            {
                var go = GameObject.Find("LeftEyeCamera") ?? GameObject.Find("CameraLeft");
                if (go != null) leftEyeCamera = go.GetComponent<Camera>();
            }

            if (rightEyeCamera == null)
            {
                var go = GameObject.Find("RightEyeCamera") ?? GameObject.Find("CameraRight");
                if (go != null) rightEyeCamera = go.GetComponent<Camera>();
            }

            if (c2Camera == null)
            {
                var go = GameObject.Find("CameraC2") ?? GameObject.Find("InstructorC2Camera") ?? GameObject.Find("Main Camera");
                if (go != null) c2Camera = go.GetComponent<Camera>();
                if (c2Camera == null && Camera.main != null && Camera.main != leftEyeCamera && Camera.main != rightEyeCamera)
                {
                    c2Camera = Camera.main;
                }
            }
#endif
        }

        // ══════════════════════════════════════════════════════
        // Gyro Tracking Status Query
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Checks if phone gyroscope WebSocket stream is connected.
        /// Safely uses reflection to avoid hard compile-time dependency on VG-P1-D (GyroReceiver).
        /// </summary>
        private bool QueryGyroReceiver()
        {
            try
            {
#if UNITY_5_3_OR_NEWER
                // 1. Look for GyroReceiver component on VRRig or in scene
                var type = Type.GetType("TrishulSim.VR.GyroReceiver, Assembly-CSharp") ??
                           Type.GetType("TrishulSim.VR.GyroReceiver");

                if (type != null)
                {
                    var receiver = FindAnyObjectByType(type);
                    if (receiver != null)
                    {
                        var prop = type.GetProperty("IsConnected");
                        if (prop != null)
                        {
                            object val = prop.GetValue(receiver, null);
                            if (val is bool)
                                return (bool)val;
                        }
                    }
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[VRModeController] Error querying GyroReceiver: " + ex.Message);
            }

            return false; // Default mock path until VG-P1-D is live
        }

        // ══════════════════════════════════════════════════════
        // Scene Loaded Event Handler
        // ══════════════════════════════════════════════════════

        #if UNITY_5_3_OR_NEWER
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log("[VRModeController] Scene loaded: " + scene.name);
            SubscribeScenarioManager();

            if (string.Equals(scene.name, vrSceneName, StringComparison.OrdinalIgnoreCase) || isVrActive)
            {
                bool hasDisplay2 = IsDisplay2Connected;
                int targetDisplay = hasDisplay2 ? vrTargetDisplay : desktopTargetDisplay;
                ConfigureCameras(targetDisplay);
            }
            else
            {
                ResetCameras();
            }
        }
#endif
    }
}
