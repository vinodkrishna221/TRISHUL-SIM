// ============================================================
// GyroReceiver.cs — Phone Gyroscope Head Tracking via WebSocket
// TRISHUL-SIM · VG-P1-D · vr-gamepad track (Sohail)
//
// WebSocket server on port 9800 receiving {a,b,g} from phone
// gyroscope (vr-gyro-sender.html). Rotates StereoHead transform
// using Quaternion.Slerp smoothing.
//
// Follows the HttpListener WebSocket pattern from WsServer.cs
// (board/contracts/websocket-ipc.md confirms port 9700 is game
// IPC; port 9800 is free for gyro).
//
// Dependencies:
//   VG-P1-A: GamepadInputController.OnRecalibrate event
//   VG-P1-B: VRRig/StereoHead GameObjects in VRSentryPost.unity
// ============================================================

using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TrishulSim.VR
{
    /// <summary>
    /// Receives phone gyroscope orientation via WebSocket on port 9800
    /// and applies it as StereoHead rotation with smooth interpolation.
    /// </summary>
    public class GyroReceiver : MonoBehaviour
    {
        // ── Inspector Fields ─────────────────────────────────
        [Header("Networking")]
        [SerializeField] private int port = 9800;

        [Header("References")]
        [SerializeField] private Transform stereoHead;

        [Header("Smoothing")]
        [SerializeField] private float smoothSpeed = 15f;

        [Header("Timeout")]
        [SerializeField] private float disconnectTimeoutSec = 2f;

        // ── Public State ─────────────────────────────────────
        /// <summary>
        /// True when a phone client is connected and sending data.
        /// Used by VG-P1-E for the status badge.
        /// </summary>
        public bool IsConnected
        {
            get { return _isConnected; }
        }

        // ── Private State ────────────────────────────────────
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private volatile bool _isRunning;
        private volatile bool _isConnected;

        // Gyro angles (thread-safe via volatile/Interlocked)
        private volatile bool _hasNewData;
        private float _rawAlpha;  // yaw   0-360
        private float _rawBeta;   // pitch -180 to 180
        private float _rawGamma;  // roll  -90 to 90
        private readonly object _dataLock = new object();

        // Calibration offsets
        private float _yawOffset;
        private float _pitchOffset;
        private bool _calibrationRequested;

        // Timeout tracking
        private float _lastMessageTime;

        // Target rotation (computed on main thread)
        private Quaternion _targetRotation = Quaternion.identity;

        // Main-thread queue for log messages from WS thread
        private readonly object _logQueueLock = new object();
        private System.Collections.Generic.Queue<string> _logQueue =
            new System.Collections.Generic.Queue<string>();

        // ══════════════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════════════

        private void Start()
        {
            // Resolve StereoHead if not assigned
            if (stereoHead == null)
            {
                Transform t = transform.Find("StereoHead");
                if (t != null) stereoHead = t;
            }

            // Subscribe to Recalibrate event from GamepadInputController
            SubscribeRecalibrate();

            // Start WebSocket server
            StartServer();
        }

        private void Update()
        {
            // Drain log queue
            DrainLogQueue();

            // Check disconnect timeout
            if (_isConnected && (Time.time - _lastMessageTime) > disconnectTimeoutSec)
            {
                _isConnected = false;
                Debug.Log("[GyroReceiver] DISCONNECTED — no data for " +
                          disconnectTimeoutSec.ToString("F1") + "s. Holding last rotation.");
            }

            // Apply rotation if we have a head transform
            if (stereoHead != null)
            {
                if (_hasNewData)
                {
                    float alpha, beta, gamma;
                    lock (_dataLock)
                    {
                        alpha = _rawAlpha;
                        beta = _rawBeta;
                        gamma = _rawGamma;
                        _hasNewData = false;
                    }

                    // Handle calibration request
                    if (_calibrationRequested)
                    {
                        _yawOffset = alpha;
                        _pitchOffset = beta;
                        _calibrationRequested = false;
                        Debug.Log("[GyroReceiver] Recalibrated — yawOffset=" +
                                  _yawOffset.ToString("F1") + " pitchOffset=" +
                                  _pitchOffset.ToString("F1"));
                    }

                    // Convert gyro angles to Unity rotation
                    // Phone deviceorientation: alpha=yaw(0-360), beta=pitch(-180..180), gamma=roll(-90..90)
                    // Unity: Euler(x=pitch, y=yaw, z=roll)
                    float pitch = -(beta - _pitchOffset);
                    float yaw = alpha - _yawOffset;
                    float roll = -gamma;

                    _targetRotation = Quaternion.Euler(pitch, yaw, roll);
                }

                // Smooth interpolation — always runs (even without new data, for smoothing)
                stereoHead.localRotation = Quaternion.Slerp(
                    stereoHead.localRotation,
                    _targetRotation,
                    smoothSpeed * Time.deltaTime
                );
            }
        }

        private void OnDestroy()
        {
            StopServer();
            UnsubscribeRecalibrate();
        }

        private void OnApplicationQuit()
        {
            StopServer();
        }

        // ══════════════════════════════════════════════════════
        // Recalibrate Event Wiring
        // ══════════════════════════════════════════════════════

        private Input.GamepadInputController _gamepadController;

        private void SubscribeRecalibrate()
        {
#if UNITY_2023_1_OR_NEWER
            _gamepadController = FindAnyObjectByType<Input.GamepadInputController>();
#else
            _gamepadController = FindObjectOfType<Input.GamepadInputController>();
#endif
            if (_gamepadController != null)
            {
                _gamepadController.OnRecalibrate += HandleRecalibrate;
                Debug.Log("[GyroReceiver] Subscribed to GamepadInputController.OnRecalibrate");
            }
            else
            {
                Debug.LogWarning("[GyroReceiver] GamepadInputController not found — " +
                                 "Recalibrate event won't fire. Keyboard 'R' still works via that controller.");
            }
        }

        private void UnsubscribeRecalibrate()
        {
            if (_gamepadController != null)
            {
                _gamepadController.OnRecalibrate -= HandleRecalibrate;
                _gamepadController = null;
            }
        }

        private void HandleRecalibrate()
        {
            _calibrationRequested = true;
        }

        // ══════════════════════════════════════════════════════
        // WebSocket Server (port 9800)
        // Follows the HttpListener pattern from WsServer.cs
        // ══════════════════════════════════════════════════════

        private void StartServer()
        {
            if (_isRunning) return;

            string prefix = "http://+:" + port + "/";
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(prefix);
                _listener.Start();
                _cts = new CancellationTokenSource();
                _isRunning = true;
                _lastMessageTime = Time.time;

                Task.Run(() => AcceptLoopAsync(_cts.Token));

                Debug.Log("[GyroReceiver] WebSocket server listening on ws://0.0.0.0:" + port + "/");
            }
            catch (HttpListenerException ex)
            {
                Debug.LogWarning("[GyroReceiver] Could not bind to port " + port + ": " + ex.Message +
                                 "\nTry running as Administrator or: " +
                                 "netsh http add urlacl url=http://+:" + port + "/ user=Everyone");
                CleanupListener();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GyroReceiver] Server start failed: " + ex.Message);
                CleanupListener();
            }
        }

        private void StopServer()
        {
            _isRunning = false;

            if (_cts != null)
            {
                try { _cts.Cancel(); } catch { }
            }

            CleanupListener();

            if (_cts != null)
            {
                try { _cts.Dispose(); } catch { }
                _cts = null;
            }

            _isConnected = false;
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning && _listener != null && _listener.IsListening)
            {
                HttpListenerContext context = null;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { break; }
                catch (HttpListenerException)
                {
                    if (!_isRunning || ct.IsCancellationRequested) break;
                    try { await Task.Delay(100, ct).ConfigureAwait(false); } catch { }
                    continue;
                }
                catch (Exception ex)
                {
                    if (!_isRunning || ct.IsCancellationRequested) break;
                    EnqueueLog("[GyroReceiver] Accept error: " + ex.Message);
                    try { await Task.Delay(250, ct).ConfigureAwait(false); } catch { }
                    continue;
                }

                if (context == null) continue;

                // Check if this is a WebSocket upgrade request
                bool isWs = context.Request.IsWebSocketRequest;
                if (!isWs)
                {
                    // Fallback: check Upgrade header manually (Mono compatibility)
                    string upgrade = context.Request.Headers["Upgrade"];
                    string connection = context.Request.Headers["Connection"];
                    if (!string.IsNullOrEmpty(upgrade) &&
                        string.Equals(upgrade.Trim(), "websocket", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(connection) &&
                        connection.IndexOf("Upgrade", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isWs = true;
                    }
                }

                if (!isWs)
                {
                    // Non-WS request — return 400
                    try
                    {
                        context.Response.StatusCode = 400;
                        byte[] body = Encoding.UTF8.GetBytes("WebSocket upgrade required on this port.");
                        context.Response.OutputStream.Write(body, 0, body.Length);
                        context.Response.Close();
                    }
                    catch { }
                    continue;
                }

                try
                {
                    HttpListenerWebSocketContext wsContext =
                        await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);

                    string clientIp = context.Request.RemoteEndPoint != null
                        ? context.Request.RemoteEndPoint.Address.ToString()
                        : "unknown";

                    EnqueueLog("[GyroReceiver] Client connected from " + clientIp + ".");

                    _isConnected = true;
                    _lastMessageTime = Time.time;

                    // Handle this client (only one at a time — last client wins)
                    Task _ = Task.Run(() => ReceiveLoopAsync(wsContext.WebSocket, ct));
                }
                catch (Exception ex)
                {
                    EnqueueLog("[GyroReceiver] WebSocket handshake failed: " + ex.Message);
                    try
                    {
                        context.Response.StatusCode = 500;
                        context.Response.Close();
                    }
                    catch { }
                }
            }
        }

        private async Task ReceiveLoopAsync(WebSocket socket, CancellationToken ct)
        {
            byte[] buffer = new byte[512]; // Gyro messages are tiny (~30 bytes)
            try
            {
                while (!ct.IsCancellationRequested && _isRunning &&
                       socket != null && socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        do
                        {
                            result = await socket.ReceiveAsync(
                                new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);

                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                _isConnected = false;
                                EnqueueLog("[GyroReceiver] Client disconnected (close frame).");
                                try
                                {
                                    await socket.CloseOutputAsync(
                                        WebSocketCloseStatus.NormalClosure,
                                        "Bye", CancellationToken.None).ConfigureAwait(false);
                                }
                                catch { }
                                return;
                            }

                            if (result.Count > 0)
                                ms.Write(buffer, 0, result.Count);
                        }
                        while (!result.EndOfMessage);

                        if (ms.Length == 0) continue;

                        string json = Encoding.UTF8.GetString(ms.ToArray());
                        ParseGyroMessage(json);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            catch (Exception ex)
            {
                if (_isRunning)
                    EnqueueLog("[GyroReceiver] Receive error: " + ex.Message);
            }
            finally
            {
                _isConnected = false;
                try { socket?.Dispose(); } catch { }
            }
        }

        // ══════════════════════════════════════════════════════
        // JSON Parsing (minimal — no dependency on Newtonsoft)
        // Message format: {"a":123.45,"b":-12.3,"g":5.67}
        // ══════════════════════════════════════════════════════

        private void ParseGyroMessage(string json)
        {
            // Lightweight parse — avoid GC from JsonUtility for 60 Hz messages
            float a = 0f, b = 0f, g = 0f;
            bool gotA = false, gotB = false, gotG = false;

            try
            {
                // Find "a": value
                int idx = json.IndexOf("\"a\"", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    a = ExtractNumber(json, idx + 3);
                    gotA = true;
                }

                idx = json.IndexOf("\"b\"", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    b = ExtractNumber(json, idx + 3);
                    gotB = true;
                }

                idx = json.IndexOf("\"g\"", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    g = ExtractNumber(json, idx + 3);
                    gotG = true;
                }
            }
            catch
            {
                // Malformed JSON — skip this frame
                return;
            }

            if (!gotA && !gotB && !gotG) return;

            lock (_dataLock)
            {
                _rawAlpha = a;
                _rawBeta = b;
                _rawGamma = g;
                _hasNewData = true;
            }

            _isConnected = true;
            _lastMessageTime = Time.time;
        }

        /// <summary>
        /// Extracts a floating-point number after a colon in JSON.
        /// Starts scanning from startIdx looking for ':' then the number.
        /// </summary>
        private static float ExtractNumber(string json, int startIdx)
        {
            // Skip to ':'
            int i = json.IndexOf(':', startIdx);
            if (i < 0) return 0f;
            i++; // skip ':'

            // Skip whitespace
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t')) i++;

            // Find end of number
            int numStart = i;
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == '-' || json[i] == '+' || json[i] == 'e' || json[i] == 'E'))
                i++;

            if (i == numStart) return 0f;

            string numStr = json.Substring(numStart, i - numStart);
            float val;
            if (float.TryParse(numStr, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out val))
            {
                return val;
            }
            return 0f;
        }

        // ══════════════════════════════════════════════════════
        // Thread-safe logging (enqueue on WS thread, drain on main)
        // ══════════════════════════════════════════════════════

        private void EnqueueLog(string message)
        {
            lock (_logQueueLock)
            {
                _logQueue.Enqueue(message);
            }
        }

        private void DrainLogQueue()
        {
            lock (_logQueueLock)
            {
                while (_logQueue.Count > 0)
                {
                    string msg = _logQueue.Dequeue();
                    if (msg.Contains("error") || msg.Contains("failed") || msg.Contains("Could not"))
                        Debug.LogWarning(msg);
                    else
                        Debug.Log(msg);
                }
            }
        }

        // ── Cleanup ──────────────────────────────────────────

        private void CleanupListener()
        {
            if (_listener != null)
            {
                try { if (_listener.IsListening) _listener.Stop(); } catch { }
                try { _listener.Close(); } catch { }
                _listener = null;
            }
        }

        // ══════════════════════════════════════════════════════
        // Editor Self-Test (MenuItem runner)
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        [UnityEditor.MenuItem("TRISHUL-SIM/Tests/GyroReceiver Self-Test")]
        public static void RunSelfTest()
        {
            int passed = 0;
            int failed = 0;

            // Test 1: ExtractNumber basic positive
            {
                float val = ExtractNumber("{\"a\":123.45,\"b\":0}", 3);
                if (Mathf.Abs(val - 123.45f) < 0.01f) { passed++; Debug.Log("PASS: ExtractNumber positive"); }
                else { failed++; Debug.LogError("FAIL: ExtractNumber positive — got " + val); }
            }

            // Test 2: ExtractNumber negative
            {
                float val = ExtractNumber("{\"b\":-12.3}", 3);
                if (Mathf.Abs(val - (-12.3f)) < 0.1f) { passed++; Debug.Log("PASS: ExtractNumber negative"); }
                else { failed++; Debug.LogError("FAIL: ExtractNumber negative — got " + val); }
            }

            // Test 3: ExtractNumber zero
            {
                float val = ExtractNumber("{\"g\":0}", 3);
                if (Mathf.Abs(val) < 0.001f) { passed++; Debug.Log("PASS: ExtractNumber zero"); }
                else { failed++; Debug.LogError("FAIL: ExtractNumber zero — got " + val); }
            }

            // Test 4: ParseGyroMessage round-trip
            {
                var receiver = new GameObject("_TestGyroReceiver").AddComponent<GyroReceiver>();
                receiver.ParseGyroMessage("{\"a\":180.5,\"b\":-45.2,\"g\":10.3}");
                bool ok;
                lock (receiver._dataLock)
                {
                    ok = Mathf.Abs(receiver._rawAlpha - 180.5f) < 0.1f &&
                         Mathf.Abs(receiver._rawBeta - (-45.2f)) < 0.1f &&
                         Mathf.Abs(receiver._rawGamma - 10.3f) < 0.1f &&
                         receiver._hasNewData;
                }
                if (ok) { passed++; Debug.Log("PASS: ParseGyroMessage round-trip"); }
                else { failed++; Debug.LogError("FAIL: ParseGyroMessage round-trip"); }
                DestroyImmediate(receiver.gameObject);
            }

            // Test 5: Calibration offset zeroing
            {
                var receiver = new GameObject("_TestGyroReceiver2").AddComponent<GyroReceiver>();
                receiver._calibrationRequested = true;
                receiver.ParseGyroMessage("{\"a\":90.0,\"b\":30.0,\"g\":5.0}");
                // Simulate Update logic
                float alpha, beta, gamma;
                lock (receiver._dataLock)
                {
                    alpha = receiver._rawAlpha;
                    beta = receiver._rawBeta;
                    gamma = receiver._rawGamma;
                }
                // Apply calibration
                receiver._yawOffset = alpha;
                receiver._pitchOffset = beta;
                float pitch = -(beta - receiver._pitchOffset);
                float yaw = alpha - receiver._yawOffset;
                bool ok = Mathf.Abs(pitch) < 0.01f && Mathf.Abs(yaw) < 0.01f;
                if (ok) { passed++; Debug.Log("PASS: Calibration zeroing"); }
                else { failed++; Debug.LogError("FAIL: Calibration zeroing — pitch=" + pitch + " yaw=" + yaw); }
                DestroyImmediate(receiver.gameObject);
            }

            // Test 6: IsConnected default false
            {
                var receiver = new GameObject("_TestGyroReceiver3").AddComponent<GyroReceiver>();
                if (!receiver.IsConnected) { passed++; Debug.Log("PASS: IsConnected default false"); }
                else { failed++; Debug.LogError("FAIL: IsConnected should be false initially"); }
                DestroyImmediate(receiver.gameObject);
            }

            // Test 7: Malformed JSON does not crash
            {
                var receiver = new GameObject("_TestGyroReceiver4").AddComponent<GyroReceiver>();
                try
                {
                    receiver.ParseGyroMessage("not json at all");
                    receiver.ParseGyroMessage("{}");
                    receiver.ParseGyroMessage("{\"x\":1}");
                    passed++;
                    Debug.Log("PASS: Malformed JSON handled gracefully");
                }
                catch (Exception ex)
                {
                    failed++;
                    Debug.LogError("FAIL: Malformed JSON threw — " + ex.Message);
                }
                DestroyImmediate(receiver.gameObject);
            }

            Debug.Log(string.Format(
                "══════════════════════════════════════\n" +
                " GyroReceiver Self-Test: {0}/{1} PASSED\n" +
                "══════════════════════════════════════",
                passed, passed + failed));

            if (failed > 0)
                Debug.LogError("GyroReceiverSelfTest: " + failed + " FAILED");
        }
#endif
    }
}
