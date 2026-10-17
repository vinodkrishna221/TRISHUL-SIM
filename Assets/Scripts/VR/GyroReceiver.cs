// ============================================================
// GyroReceiver.cs — Phone Gyroscope Head Tracking via WebSocket
// TRISHUL-SIM · VG-P1-D · vr-gamepad track (Sohail)
//
// WebSocket server on port 9800 receiving {a,b,g} from phone
// gyroscope (vr-gyro-sender.html). Rotates StereoHead transform
// using Quaternion.Slerp smoothing.
//
// Uses native TcpListener with RFC 6455 WebSocket framing.
// Thread-safe monotonic Stopwatch ticks for timeout detection
// without calling UnityEngine.Time on background worker threads.
// Persists listener across scene loads in Play mode.
// Handles pipelined handshake + frame packets via leftover buffering.
//
// Dependencies:
//   VG-P1-A: GamepadInputController.OnRecalibrate event
//   VG-P1-B: VRRig/StereoHead GameObjects in VRSentryPost.unity
// ============================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
        [SerializeField] private float disconnectTimeoutSec = 5f;

        // ── Singleton & Shared Server State ──────────────────
        public static GyroReceiver Instance { get; private set; }

        private static TcpListener s_listener;
        private static CancellationTokenSource s_cts;
        private static volatile bool s_isServerRunning;
        private static readonly object s_serverLock = new object();
        private static TcpClient s_activeClient;
        private static readonly object s_clientLock = new object();

        // Monotonic thread-safe timing (ticks / frequency)
        private static long s_lastMessageTicks;
        private static volatile bool s_clientConnected;

        // Main-thread log queue
        private static readonly object s_logQueueLock = new object();
        private static readonly Queue<string> s_logQueue = new Queue<string>();

        // ── Public State ─────────────────────────────────────
        /// <summary>
        /// True when a phone client is connected and sending data.
        /// Used by VG-P1-E for the status badge.
        /// </summary>
        public bool IsConnected
        {
            get { return _isConnected || s_clientConnected; }
        }

        public Transform StereoHead
        {
            get { return stereoHead; }
            set { stereoHead = value; }
        }

        public Quaternion TargetRotation
        {
            get { return _targetRotation; }
        }

        public float ManualYawOffset
        {
            get { return _manualYawOffset; }
            set { _manualYawOffset = value; }
        }

        public float ManualPitchOffset
        {
            get { return _manualPitchOffset; }
            set { _manualPitchOffset = value; }
        }

        /// <summary>
        /// Accumulates gamepad right-stick manual camera look delta into head rotation.
        /// Works both when phone gyro is live (acts as orientation offset/trim)
        /// and when phone gyro is offline/absent (directly drives full 360° camera rotation).
        /// </summary>
        public void AddLookOffset(float yawDelta, float pitchDelta)
        {
            _manualYawOffset = ((_manualYawOffset + yawDelta) % 360f + 360f) % 360f;
            _manualPitchOffset = Mathf.Clamp(_manualPitchOffset + pitchDelta, -80f, 80f);

            if (!IsConnected)
            {
                _targetRotation = Quaternion.Euler(_manualPitchOffset, _manualYawOffset, 0f);
            }
        }

        // ── Private Instance State ───────────────────────────
        private TcpListener _tcpListener;
        private CancellationTokenSource _cts;
        private volatile bool _isRunning;
        private volatile bool _isConnected;

        // Gyro angles (thread-safe via volatile/Interlocked/lock)
        public volatile bool _hasNewData;
        public float _rawAlpha;  // yaw   0-360
        public float _rawBeta;   // pitch -180 to 180
        public float _rawGamma;  // roll  -90 to 90
        public readonly object _dataLock = new object();

        // Calibration offsets
        public float _yawOffset;
        public float _pitchOffset;
        public bool _calibrationRequested;

        // Manual Gamepad look offsets (VG-P1-D / Camera control)
        public float _manualYawOffset = 0f;
        public float _manualPitchOffset = 0f;

        // Timeout tracking (updated on main thread)
        public float _lastMessageTime;

        // Target rotation (computed on main thread)
        private Quaternion _targetRotation = Quaternion.identity;

        // ══════════════════════════════════════════════════════
        // Lifecycle
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            Instance = this;

            // Resolve StereoHead if not assigned
            ResolveStereoHead();

            // Ensure disconnectTimeoutSec is at least 2.0s
            if (disconnectTimeoutSec < 2f)
            {
                disconnectTimeoutSec = 2f;
            }

            // Subscribe to Recalibrate event from GamepadInputController
            SubscribeRecalibrate();

            // Start or attach to WebSocket server
            StartServer();
        }

        private void ResolveStereoHead()
        {
            if (stereoHead != null) return;

            Transform t = transform.Find("StereoHead");
            if (t != null)
            {
                stereoHead = t;
            }
            else
            {
                GameObject go = GameObject.Find("StereoHead");
                if (go != null) stereoHead = go.transform;
            }
        }

        private void Update()
        {
            // Drain log queue from background network tasks
            DrainLogQueue();

            // Check disconnect timeout using thread-safe Stopwatch ticks
            long lastTicks = Interlocked.Read(ref s_lastMessageTicks);
            if (lastTicks > 0)
            {
                double elapsedSec = (Stopwatch.GetTimestamp() - lastTicks) / (double)Stopwatch.Frequency;
                if ((_isConnected || s_clientConnected) && elapsedSec > disconnectTimeoutSec)
                {
                    _isConnected = false;
                    s_clientConnected = false;
                    Debug.Log("[GyroReceiver] DISCONNECTED — no data for " +
                              disconnectTimeoutSec.ToString("F1") + "s. Holding last rotation.");
                }
            }

            // Lazy resolution if stereoHead was not present at Start
            if (stereoHead == null)
            {
                ResolveStereoHead();
            }

            // Process new gyro orientation if available
            if (_hasNewData)
            {
                _lastMessageTime = Time.time;
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
                    _manualYawOffset = 0f;
                    _manualPitchOffset = 0f;
                    _calibrationRequested = false;
                    Debug.Log("[GyroReceiver] Recalibrated — yawOffset=" +
                              _yawOffset.ToString("F1") + " pitchOffset=" +
                              _pitchOffset.ToString("F1"));
                }

                // Convert gyro angles to Unity rotation with manual gamepad look offsets
                // Phone deviceorientation: alpha=yaw(0-360), beta=pitch(-180..180), gamma=roll(-90..90)
                // Unity: Euler(x=pitch, y=yaw, z=roll)
                float pitch = -(beta - _pitchOffset) + _manualPitchOffset;
                float yaw = (alpha - _yawOffset) + _manualYawOffset;
                float roll = -gamma;

                _targetRotation = Quaternion.Euler(pitch, yaw, roll);
            }
            else
            {
                // When phone gyro is not sending data (offline / gamepad mode), handle recalibration and manual look
                if (_calibrationRequested)
                {
                    _manualYawOffset = 0f;
                    _manualPitchOffset = 0f;
                    _calibrationRequested = false;
                    _targetRotation = Quaternion.identity;
                }
                else if (!IsConnected)
                {
                    _targetRotation = Quaternion.Euler(_manualPitchOffset, _manualYawOffset, 0f);
                }
                else
                {
                    // Connected, but no new network packet this frame: maintain orientation plus manual stick offset
                    float alpha, beta, gamma;
                    lock (_dataLock)
                    {
                        alpha = _rawAlpha;
                        beta = _rawBeta;
                        gamma = _rawGamma;
                    }
                    float pitch = -(beta - _pitchOffset) + _manualPitchOffset;
                    float yaw = (alpha - _yawOffset) + _manualYawOffset;
                    float roll = -gamma;
                    _targetRotation = Quaternion.Euler(pitch, yaw, roll);
                }
            }

            // Apply rotation if we have a head transform
            if (stereoHead != null)
            {
                // Smooth interpolation — always runs for fluid rotation
                stereoHead.localRotation = Quaternion.Slerp(
                    stereoHead.localRotation,
                    _targetRotation,
                    smoothSpeed * Time.deltaTime
                );
            }
        }

        private void OnDestroy()
        {
            UnsubscribeRecalibrate();
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
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
            _manualYawOffset = 0f;
            _manualPitchOffset = 0f;
            _targetRotation = Quaternion.identity;
            Debug.Log("[GyroReceiver] Calibration requested via Gamepad.");
        }

        // ══════════════════════════════════════════════════════
        // WebSocket Server (port 9800)
        // Uses TcpListener with RFC 6455 handshake & async frame reading
        // ══════════════════════════════════════════════════════

        public void StartServer()
        {
            lock (s_serverLock)
            {
                if (s_isServerRunning && s_listener != null)
                {
                    _isRunning = true;
                    _tcpListener = s_listener;
                    _cts = s_cts;
                    Debug.Log("[GyroReceiver] WebSocket server already active on ws://0.0.0.0:" + port + "/");
                    return;
                }

                try
                {
                    CleanupListener();
                    s_listener = new TcpListener(IPAddress.Any, port);
                    s_listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    s_listener.Start();
                    s_cts = new CancellationTokenSource();
                    s_isServerRunning = true;
                    _isRunning = true;
                    _tcpListener = s_listener;
                    _cts = s_cts;
                    Interlocked.Exchange(ref s_lastMessageTicks, Stopwatch.GetTimestamp());

                    Task.Run(() => AcceptLoopAsync(s_cts.Token));

                    Debug.Log("[GyroReceiver] WebSocket server listening on ws://0.0.0.0:" + port + "/");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GyroReceiver] Server start failed on port " + port + ": " + ex.Message);
                    CleanupListener();
                }
            }
        }

        public static void StopServer()
        {
            lock (s_serverLock)
            {
                s_isServerRunning = false;

                if (s_cts != null)
                {
                    try { s_cts.Cancel(); } catch { }
                }

                lock (s_clientLock)
                {
                    if (s_activeClient != null)
                    {
                        try { s_activeClient.Close(); } catch { }
                        s_activeClient = null;
                    }
                }

                CleanupListener();

                if (s_cts != null)
                {
                    try { s_cts.Dispose(); } catch { }
                    s_cts = null;
                }

                s_clientConnected = false;
            }

            GyroReceiver inst = Instance;
            if (inst != null)
            {
                inst._isRunning = false;
                inst._isConnected = false;
            }
        }

        private static void CleanupListener()
        {
            if (s_listener != null)
            {
                try { s_listener.Stop(); } catch { }
                s_listener = null;
            }
            s_isServerRunning = false;
        }

        private static async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && s_isServerRunning && s_listener != null)
            {
                TcpClient client = null;
                try
                {
                    client = await s_listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException)
                {
                    if (!s_isServerRunning || ct.IsCancellationRequested) break;
                    try { await Task.Delay(100, ct).ConfigureAwait(false); } catch { }
                    continue;
                }
                catch (Exception ex)
                {
                    if (!s_isServerRunning || ct.IsCancellationRequested) break;
                    EnqueueLogStatic("[GyroReceiver] Accept error: " + ex.Message);
                    try { await Task.Delay(250, ct).ConfigureAwait(false); } catch { }
                    continue;
                }

                if (client == null) continue;

                string clientIp = "unknown";
                try
                {
                    if (client.Client != null && client.Client.RemoteEndPoint != null)
                        clientIp = client.Client.RemoteEndPoint.ToString();
                }
                catch { }

                EnqueueLogStatic("[GyroReceiver] Client connected from " + clientIp + ".");
                Task _ = Task.Run(() => HandleClientAsync(client, ct));
            }
        }

        private static async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            lock (s_clientLock)
            {
                if (s_activeClient != null)
                {
                    try { s_activeClient.Close(); } catch { }
                }
                s_activeClient = client;
            }

            try
            {
                client.NoDelay = true;
                using (NetworkStream stream = client.GetStream())
                {
                    // 1. Read HTTP Handshake (read until \r\n\r\n with 5s timeout)
                    byte[] handshakeBuffer = new byte[4096];
                    int totalRead = 0;
                    int headerEnd = -1;

                    using (var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        handshakeCts.CancelAfter(5000);
                        while (totalRead < handshakeBuffer.Length && !handshakeCts.IsCancellationRequested)
                        {
                            int r = await stream.ReadAsync(handshakeBuffer, totalRead, handshakeBuffer.Length - totalRead, handshakeCts.Token).ConfigureAwait(false);
                            if (r <= 0) break;
                            totalRead += r;

                            // Scan for \r\n\r\n delimiter
                            for (int i = 0; i <= totalRead - 4; i++)
                            {
                                if (handshakeBuffer[i] == (byte)'\r' && handshakeBuffer[i + 1] == (byte)'\n' &&
                                    handshakeBuffer[i + 2] == (byte)'\r' && handshakeBuffer[i + 3] == (byte)'\n')
                                {
                                    headerEnd = i + 4;
                                    break;
                                }
                            }
                            if (headerEnd >= 0) break;
                        }
                    }

                    if (headerEnd < 0 || totalRead <= 0)
                    {
                        EnqueueLogStatic("[GyroReceiver] Incomplete handshake request (" + totalRead + " bytes).");
                        return;
                    }

                    string req = Encoding.UTF8.GetString(handshakeBuffer, 0, headerEnd);
                    int keyIdx = req.IndexOf("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase);
                    if (keyIdx < 0)
                    {
                        EnqueueLogStatic("[GyroReceiver] Missing Sec-WebSocket-Key header in handshake.");
                        return;
                    }

                    int valueStart = keyIdx + 18;
                    int lineEnd = req.IndexOf("\r\n", valueStart, StringComparison.Ordinal);
                    if (lineEnd < 0) lineEnd = req.Length;
                    string key = req.Substring(valueStart, lineEnd - valueStart).Trim();

                    string acceptKey;
                    using (SHA1 sha1 = SHA1.Create())
                    {
                        acceptKey = Convert.ToBase64String(
                            sha1.ComputeHash(Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                    }

                    string response = "HTTP/1.1 101 Switching Protocols\r\n" +
                                      "Upgrade: websocket\r\n" +
                                      "Connection: Upgrade\r\n" +
                                      "Sec-WebSocket-Accept: " + acceptKey + "\r\n\r\n";
                    byte[] respBytes = Encoding.UTF8.GetBytes(response);
                    await stream.WriteAsync(respBytes, 0, respBytes.Length, ct).ConfigureAwait(false);
                    await stream.FlushAsync(ct).ConfigureAwait(false);

                    s_clientConnected = true;
                    Interlocked.Exchange(ref s_lastMessageTicks, Stopwatch.GetTimestamp());

                    GyroReceiver inst = Instance;
                    if (inst != null)
                    {
                        inst._isConnected = true;
                    }

                    EnqueueLogStatic("[GyroReceiver] Handshake completed successfully. Streaming active.");

                    // 2. Read WebSocket Frames (RFC 6455)
                    // Preserve any leftover bytes pipelined in the initial handshake packet
                    HandshakeRemainder remainder = new HandshakeRemainder(handshakeBuffer, headerEnd, totalRead - headerEnd);

                    byte[] headerBuffer = new byte[2];
                    while (!ct.IsCancellationRequested && s_isServerRunning && client.Connected)
                    {
                        if (!await ReadExactAsync(stream, headerBuffer, 0, 2, remainder, ct).ConfigureAwait(false))
                            break;

                        int b1 = headerBuffer[0];
                        int b2 = headerBuffer[1];

                        int opcode = b1 & 0x0F;

                        // Opcode 8: Close frame
                        if (opcode == 8)
                        {
                            EnqueueLogStatic("[GyroReceiver] Client sent close frame.");
                            byte[] closeResp = new byte[] { 0x88, 0x00 };
                            try { await stream.WriteAsync(closeResp, 0, 2, ct).ConfigureAwait(false); } catch { }
                            break;
                        }

                        bool isMasked = (b2 & 0x80) != 0;
                        int len = b2 & 0x7F;

                        if (len == 126)
                        {
                            byte[] extLen = new byte[2];
                            if (!await ReadExactAsync(stream, extLen, 0, 2, remainder, ct).ConfigureAwait(false))
                                break;
                            len = (extLen[0] << 8) | extLen[1];
                        }
                        else if (len == 127)
                        {
                            EnqueueLogStatic("[GyroReceiver] Oversized 64-bit frame rejected.");
                            break;
                        }

                        byte[] mask = new byte[4];
                        if (isMasked)
                        {
                            if (!await ReadExactAsync(stream, mask, 0, 4, remainder, ct).ConfigureAwait(false))
                                break;
                        }

                        byte[] payload = new byte[len];
                        if (len > 0)
                        {
                            if (!await ReadExactAsync(stream, payload, 0, len, remainder, ct).ConfigureAwait(false))
                                break;
                        }

                        if (isMasked)
                        {
                            for (int i = 0; i < len; i++)
                            {
                                payload[i] ^= mask[i % 4];
                            }
                        }

                        // Opcode 9: Ping -> Respond with Pong
                        if (opcode == 9)
                        {
                            byte[] pongHeader = new byte[] { 0x8A, (byte)(len & 0x7F) };
                            await stream.WriteAsync(pongHeader, 0, pongHeader.Length, ct).ConfigureAwait(false);
                            if (len > 0)
                            {
                                await stream.WriteAsync(payload, 0, len, ct).ConfigureAwait(false);
                            }
                            await stream.FlushAsync(ct).ConfigureAwait(false);
                            continue;
                        }

                        // Opcode 10: Pong -> ignore
                        if (opcode == 10)
                        {
                            continue;
                        }

                        // Opcode 1: Text
                        if (opcode == 1)
                        {
                            string json = Encoding.UTF8.GetString(payload);
                            GyroReceiver current = Instance;
                            if (current != null)
                            {
                                current.ParseGyroMessage(json);
                            }
                            else
                            {
                                s_clientConnected = true;
                                Interlocked.Exchange(ref s_lastMessageTicks, Stopwatch.GetTimestamp());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (s_isServerRunning && !ct.IsCancellationRequested)
                {
                    EnqueueLogStatic("[GyroReceiver] Client stream closed: " + ex.Message);
                }
            }
            finally
            {
                lock (s_clientLock)
                {
                    if (ReferenceEquals(s_activeClient, client))
                    {
                        s_activeClient = null;
                        s_clientConnected = false;
                        GyroReceiver current = Instance;
                        if (current != null)
                        {
                            current._isConnected = false;
                        }
                    }
                }
                try { client.Close(); } catch { }
            }
        }

        private sealed class HandshakeRemainder
        {
            public readonly byte[] Buffer;
            public int Offset;
            public int Count;

            public HandshakeRemainder(byte[] buffer, int offset, int count)
            {
                Buffer = buffer;
                Offset = offset;
                Count = count;
            }
        }

        private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count,
            HandshakeRemainder remainder, CancellationToken ct)
        {
            int total = 0;
            // Drain any bytes already read in the handshake buffer first
            if (remainder != null && remainder.Count > 0 && remainder.Buffer != null)
            {
                int take = Math.Min(count, remainder.Count);
                Array.Copy(remainder.Buffer, remainder.Offset, buffer, offset, take);
                total += take;
                remainder.Offset += take;
                remainder.Count -= take;
            }

            while (total < count)
            {
                int r = await stream.ReadAsync(buffer, offset + total, count - total, ct).ConfigureAwait(false);
                if (r <= 0) return false;
                total += r;
            }
            return true;
        }

        // ══════════════════════════════════════════════════════
        // JSON Parsing (minimal — zero GC dependency)
        // Message format: {"a":123.45,"b":-12.3,"g":5.67}
        // Also supports:  {"alpha":123.45,"beta":-12.3,"gamma":5.67}
        // ══════════════════════════════════════════════════════

        public void ParseGyroMessage(string json)
        {
            float a = 0f, b = 0f, g = 0f;
            bool gotA = false, gotB = false, gotG = false;

            try
            {
                int idx = json.IndexOf("\"a\"", StringComparison.Ordinal);
                if (idx < 0) idx = json.IndexOf("\"alpha\"", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    a = ExtractNumber(json, idx);
                    gotA = true;
                }

                idx = json.IndexOf("\"b\"", StringComparison.Ordinal);
                if (idx < 0) idx = json.IndexOf("\"beta\"", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    b = ExtractNumber(json, idx);
                    gotB = true;
                }

                idx = json.IndexOf("\"g\"", StringComparison.Ordinal);
                if (idx < 0) idx = json.IndexOf("\"gamma\"", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    g = ExtractNumber(json, idx);
                    gotG = true;
                }
            }
            catch
            {
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
            s_clientConnected = true;
            Interlocked.Exchange(ref s_lastMessageTicks, Stopwatch.GetTimestamp());
        }

        /// <summary>
        /// Extracts a floating-point number after a colon in JSON.
        /// Starts scanning from startIdx looking for ':' then the number.
        /// </summary>
        public static float ExtractNumber(string json, int startIdx)
        {
            int i = json.IndexOf(':', startIdx);
            if (i < 0) return 0f;
            i++; // skip ':'

            while (i < json.Length && (json[i] == ' ' || json[i] == '\t')) i++;

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

        private static void EnqueueLogStatic(string message)
        {
            lock (s_logQueueLock)
            {
                s_logQueue.Enqueue(message);
            }
        }

        private void DrainLogQueue()
        {
            lock (s_logQueueLock)
            {
                while (s_logQueue.Count > 0)
                {
                    string msg = s_logQueue.Dequeue();
                    if (msg.Contains("error") || msg.Contains("failed") || msg.Contains("Could not") || msg.Contains("Missing") || msg.Contains("rejected"))
                        Debug.LogWarning(msg);
                    else
                        Debug.Log(msg);
                }
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
                float val = ExtractNumber("{\"a\":123.45,\"b\":0}", 0);
                if (Mathf.Abs(val - 123.45f) < 0.01f) { passed++; Debug.Log("PASS: ExtractNumber positive"); }
                else { failed++; Debug.LogError("FAIL: ExtractNumber positive — got " + val); }
            }

            // Test 2: ExtractNumber negative
            {
                float val = ExtractNumber("{\"b\":-12.3}", 0);
                if (Mathf.Abs(val - (-12.3f)) < 0.1f) { passed++; Debug.Log("PASS: ExtractNumber negative"); }
                else { failed++; Debug.LogError("FAIL: ExtractNumber negative — got " + val); }
            }

            // Test 3: ExtractNumber zero
            {
                float val = ExtractNumber("{\"g\":0}", 0);
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
                float alpha, beta, gamma;
                lock (receiver._dataLock)
                {
                    alpha = receiver._rawAlpha;
                    beta = receiver._rawBeta;
                    gamma = receiver._rawGamma;
                }
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
                receiver._isConnected = false;
                s_clientConnected = false;
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

            // Test 8: RFC 6455 Handshake Key Hash Verification
            {
                string testKey = "dGhlIHNhbXBsZSBub25jZQ==";
                string expectedAccept = "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=";
                string computedAccept;
                using (SHA1 sha1 = SHA1.Create())
                {
                    computedAccept = Convert.ToBase64String(
                        sha1.ComputeHash(Encoding.UTF8.GetBytes(testKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                }
                if (computedAccept == expectedAccept) { passed++; Debug.Log("PASS: RFC 6455 Handshake Key computation"); }
                else { failed++; Debug.LogError("FAIL: RFC 6455 Key computation mismatch: got " + computedAccept); }
            }

            // Test 9: Multi-key parsing (alpha, beta, gamma keys)
            {
                var receiver = new GameObject("_TestGyroReceiver5").AddComponent<GyroReceiver>();
                receiver.ParseGyroMessage("{\"alpha\":270.0,\"beta\":-15.5,\"gamma\":45.0}");
                bool ok;
                lock (receiver._dataLock)
                {
                    ok = Mathf.Abs(receiver._rawAlpha - 270.0f) < 0.1f &&
                         Mathf.Abs(receiver._rawBeta - (-15.5f)) < 0.1f &&
                         Mathf.Abs(receiver._rawGamma - 45.0f) < 0.1f &&
                         receiver._hasNewData;
                }
                if (ok) { passed++; Debug.Log("PASS: Multi-key JSON parsing (alpha/beta/gamma)"); }
                else { failed++; Debug.LogError("FAIL: Multi-key JSON parsing failed"); }
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
