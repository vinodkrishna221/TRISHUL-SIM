using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TrishulSim.Network
{
    /// <summary>
    /// Trainee Cockpit localhost WebSocket IPC client (ws://127.0.0.1:9700).
    /// Implements board/contracts/websocket-ipc.md v1.
    /// </summary>
    public class WsClient : MonoBehaviour
    {
        public const string DefaultServerUri = "ws://127.0.0.1:9700/";
        public const string DefaultClientRole = "TRAINEE_COCKPIT";
        public const string DefaultBuildVersion = "0.1.0";
        public const int MaxReconnectBackoffMs = 30000;

        public static WsClient Instance { get; private set; }

        [SerializeField] private string serverUri = DefaultServerUri;
        [SerializeField] private string clientRole = DefaultClientRole;
        [SerializeField] private string buildVersion = DefaultBuildVersion;
        [SerializeField] private int heartbeatIntervalMs = 5000;
        [SerializeField] private int staleThresholdMs = 15000;
        [SerializeField] private int baseReconnectDelayMs = 1000;
        [SerializeField] private bool autoReconnect = true;
        [SerializeField] private bool autoStart = true;

        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly object _mainThreadQueueLock = new object();
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private SynchronizationContext _syncContext;
        private int _mainThreadId;
        private volatile bool _isRunning;
        private volatile bool _isLinkConfirmed;
        private volatile bool _isLinkStale;
        private long _lastServerAckMs;
        private long _clockSkewMs;
        private int _reconnectAttempts;
#if UNITY_5_3_OR_NEWER
        private bool _dispatchEventsOnMainThread = true;
#else
        private bool _dispatchEventsOnMainThread = false;
#endif

        public event Action<JsonEnvelope> OnMessageReceived;
        public event Action OnLinkConfirmed;
        public event Action OnLinkStale;

        public string ServerUri
        {
            get { return serverUri; }
            set { serverUri = string.IsNullOrWhiteSpace(value) ? DefaultServerUri : value; }
        }

        public string ClientRole
        {
            get { return clientRole; }
            set { clientRole = string.IsNullOrWhiteSpace(value) ? DefaultClientRole : value; }
        }

        public string BuildVersion
        {
            get { return buildVersion; }
            set { buildVersion = string.IsNullOrWhiteSpace(value) ? DefaultBuildVersion : value; }
        }

        public int HeartbeatIntervalMs
        {
            get { return heartbeatIntervalMs; }
            set { heartbeatIntervalMs = Math.Max(50, value); }
        }

        public int StaleThresholdMs
        {
            get { return staleThresholdMs; }
            set { staleThresholdMs = Math.Max(100, value); }
        }

        public int BaseReconnectDelayMs
        {
            get { return baseReconnectDelayMs; }
            set { baseReconnectDelayMs = Math.Max(20, value); }
        }

        public bool AutoReconnect
        {
            get { return autoReconnect; }
            set { autoReconnect = value; }
        }

        public bool AutoStart
        {
            get { return autoStart; }
            set { autoStart = value; }
        }

        public bool DispatchEventsOnMainThread
        {
            get { return _dispatchEventsOnMainThread; }
            set { _dispatchEventsOnMainThread = value; }
        }

        public bool IsConnected
        {
            get
            {
                ClientWebSocket sock = _socket;
                return sock != null && sock.State == WebSocketState.Open;
            }
        }

        public bool IsLinkConfirmed
        {
            get { return _isLinkConfirmed && IsConnected; }
        }

        public bool IsLinkStale
        {
            get { return _isLinkStale; }
        }

        public long ClockSkewMs
        {
            get { return Interlocked.Read(ref _clockSkewMs); }
        }

        public int ReconnectAttempts
        {
            get { return _reconnectAttempts; }
        }

        public static int CalculateBackoffMs(int attempt, int baseDelayMs = 1000, int maxDelayMs = MaxReconnectBackoffMs)
        {
            if (attempt < 0)
                attempt = 0;
            if (baseDelayMs <= 0)
                baseDelayMs = 1000;
            if (maxDelayMs <= 0)
                maxDelayMs = MaxReconnectBackoffMs;

            int shift = Math.Min(attempt, 10);
            long delay = (long)baseDelayMs * (1L << shift);
            return (int)Math.Min(delay, maxDelayMs);
        }

        public void Awake()
        {
            CaptureMainThreadContext();
            if (Instance == null)
                Instance = this;
        }

        public void Start()
        {
            CaptureMainThreadContext();
            if (Instance == null)
                Instance = this;

            if (!autoStart)
                return;

            StartClient();
        }

        public void StartClient()
        {
            CaptureMainThreadContext();
            if (Instance == null)
                Instance = this;

            if (_isRunning)
                return;

            _cts = new CancellationTokenSource();
            _isRunning = true;
            Task.Run(() => ConnectionLifecycleLoopAsync(_cts.Token));
        }

        public void Update()
        {
            DrainMainThreadQueue();
        }

        public int DrainMainThreadQueue()
        {
            int executed = 0;
            while (true)
            {
                Action next = null;
                lock (_mainThreadQueueLock)
                {
                    if (_mainThreadQueue.Count > 0)
                        next = _mainThreadQueue.Dequeue();
                }

                if (next == null)
                    break;

                executed++;
                try
                {
                    next();
                }
                catch (Exception ex)
                {
                    Debug.LogError("[WsClient] Event callback error: " + ex.Message);
                }
            }
            return executed;
        }

        private void CaptureMainThreadContext()
        {
            if (_mainThreadId == 0)
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            if (_syncContext == null && SynchronizationContext.Current != null)
                _syncContext = SynchronizationContext.Current;
        }

        private void DispatchEvent(Action callback)
        {
            if (callback == null)
                return;

            if (!_dispatchEventsOnMainThread && _syncContext == null)
            {
                callback();
                return;
            }

            if (_mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                callback();
                return;
            }

            lock (_mainThreadQueueLock)
            {
                _mainThreadQueue.Enqueue(callback);
            }

            SynchronizationContext ctx = _syncContext;
            if (ctx != null)
            {
                try { ctx.Post(_ => DrainMainThreadQueue(), null); } catch { }
            }
        }

        public async Task<bool> ConnectOnceAsync(CancellationToken ct = default(CancellationToken))
        {
            CleanupSocket();

            _socket = new ClientWebSocket();
            Uri uri = new Uri(EnsureTrailingSlash(serverUri));
            await _socket.ConnectAsync(uri, ct).ConfigureAwait(false);

            long nowMs = JsonEnvelope.NowUnixMs();
            Interlocked.Exchange(ref _lastServerAckMs, nowMs);
            _isLinkStale = false;

            string readyJson = JsonEnvelope.CreateClientReady(clientRole, buildVersion, nowMs).ToJson();
            await SendJsonAsync(readyJson, ct).ConfigureAwait(false);
            return true;
        }

        public void Stop()
        {
            if (!_isRunning && _socket == null)
                return;

            _isRunning = false;
            _isLinkConfirmed = false;

            if (_cts != null)
            {
                try { _cts.Cancel(); } catch { }
            }

            CleanupSocket();

            if (_cts != null)
            {
                try { _cts.Dispose(); } catch { }
                _cts = null;
            }

            if (ReferenceEquals(Instance, this))
                Instance = null;
        }

        public void OnDestroy()
        {
            Stop();
        }

        public void OnApplicationQuit()
        {
            Stop();
        }

        public Task SendAsync(string json, CancellationToken ct = default(CancellationToken))
        {
            return SendJsonAsync(json, ct);
        }

        public Task SendAsync(JsonEnvelope envelope, CancellationToken ct = default(CancellationToken))
        {
            return SendEnvelopeAsync(envelope, ct);
        }

        public async Task SendEnvelopeAsync(JsonEnvelope envelope, CancellationToken ct = default(CancellationToken))
        {
            await SendJsonAsync(envelope.ToJson(), ct).ConfigureAwait(false);
        }

        public async Task SendJsonAsync(string json, CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length > JsonEnvelope.MaxMessageBytes)
            {
                Debug.LogWarning("[WsClient] Outbound message exceeds 64 KB limit (" + bytes.Length + " bytes).");
                return;
            }

            ClientWebSocket sock = _socket;
            if (sock == null || sock.State != WebSocketState.Open)
                return;

            bool acquired = false;
            try
            {
                await _sendLock.WaitAsync(ct).ConfigureAwait(false);
                acquired = true;
                if (sock.State == WebSocketState.Open)
                {
                    await sock.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (WebSocketException)
            {
            }
            finally
            {
                if (acquired)
                {
                    try { _sendLock.Release(); } catch { }
                }
            }
        }

        public void CheckHeartbeatTimeout(long nowUnixMs)
        {
            long lastAck = Interlocked.Read(ref _lastServerAckMs);
            if (lastAck <= 0)
                return;

            long elapsed = nowUnixMs - lastAck;
            if (elapsed >= staleThresholdMs && !_isLinkStale)
            {
                _isLinkStale = true;
                Debug.LogWarning("[WsClient] \u26A0\uFE0F Link lost \u2014 no SERVER_HEARTBEAT_ACK for " + elapsed +
                                 "ms (degraded mode, scenario continues)");
                DispatchEvent(() =>
                {
                    Action staleHandler = OnLinkStale;
                    if (staleHandler != null)
                        staleHandler();
                });
            }
        }

        private async Task ConnectionLifecycleLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning)
            {
                try
                {
                    await ConnectOnceAsync(ct).ConfigureAwait(false);

                    using (CancellationTokenSource sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        Task receiveTask = ReceiveLoopAsync(_socket, sessionCts.Token);
                        Task heartbeatTask = HeartbeatLoopAsync(sessionCts.Token);

                        await receiveTask.ConfigureAwait(false);
                        sessionCts.Cancel();
                        try { await heartbeatTask.ConfigureAwait(false); } catch { }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!_isRunning || ct.IsCancellationRequested)
                        break;
                    Debug.LogWarning("[WsClient] Connection attempt failed (" + ex.Message + ").");
                }

                _isLinkConfirmed = false;

                if (!autoReconnect || !_isRunning || ct.IsCancellationRequested)
                    break;

                int delayMs = CalculateBackoffMs(_reconnectAttempts, baseReconnectDelayMs, MaxReconnectBackoffMs);
                _reconnectAttempts++;
                Debug.Log("[WsClient] Reconnecting in " + delayMs + "ms (attempt " + _reconnectAttempts + ")...");

                try
                {
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ReceiveLoopAsync(ClientWebSocket sock, CancellationToken ct)
        {
            byte[] buffer = new byte[8192];
            while (!ct.IsCancellationRequested && _isRunning && sock != null && sock.State == WebSocketState.Open)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    WebSocketReceiveResult result;
                    bool oversized = false;
                    do
                    {
                        result = await sock.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            try
                            {
                                if (sock.State == WebSocketState.CloseReceived)
                                {
                                    await sock.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge close", CancellationToken.None).ConfigureAwait(false);
                                }
                            }
                            catch { }
                            return;
                        }

                        if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            oversized = true;
                        }
                        else if (!oversized)
                        {
                            if (ms.Length + result.Count > JsonEnvelope.MaxMessageBytes)
                            {
                                oversized = true;
                                Debug.LogWarning("[WsClient] Dropped incoming frame exceeding 64 KB.");
                            }
                            else if (result.Count > 0)
                            {
                                ms.Write(buffer, 0, result.Count);
                            }
                        }
                    }
                    while (!result.EndOfMessage);

                    if (oversized)
                        continue;

                    string rawJson = Encoding.UTF8.GetString(ms.ToArray());
                    HandleServerMessage(rawJson);
                }
            }
        }

        private void HandleServerMessage(string rawJson)
        {
            JsonEnvelope envelope;
            string parseError;
            if (!JsonEnvelope.TryParse(rawJson, out envelope, out parseError))
            {
                Debug.LogWarning("[WsClient] Ignoring malformed server envelope: " + parseError);
                return;
            }

            if (string.Equals(envelope.type, JsonEnvelope.SERVER_HEARTBEAT_ACK, StringComparison.Ordinal))
            {
                long nowMs = JsonEnvelope.NowUnixMs();
                Interlocked.Exchange(ref _lastServerAckMs, nowMs);

                long serverTimeMs;
                if (envelope.TryGetPayloadLong("serverTimeMs", out serverTimeMs))
                {
                    Interlocked.Exchange(ref _clockSkewMs, nowMs - serverTimeMs);
                }

                _isLinkStale = false;
                _isLinkConfirmed = true;
                _reconnectAttempts = 0;

                Debug.Log("[WsClient] SERVER_HEARTBEAT_ACK received \u2014 link green");

                DispatchEvent(() =>
                {
                    Action confirmedHandler = OnLinkConfirmed;
                    if (confirmedHandler != null)
                        confirmedHandler();
                });
            }

            JsonEnvelope capturedEnvelope = envelope;
            DispatchEvent(() =>
            {
                Action<JsonEnvelope> msgHandler = OnMessageReceived;
                if (msgHandler != null)
                    msgHandler(capturedEnvelope);
            });
        }

        private async Task HeartbeatLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _isRunning && IsConnected)
            {
                try
                {
                    await Task.Delay(heartbeatIntervalMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (!_isRunning || ct.IsCancellationRequested)
                    break;

                long nowMs = JsonEnvelope.NowUnixMs();
                CheckHeartbeatTimeout(nowMs);

                if (IsConnected)
                {
                    string hbJson = JsonEnvelope.CreateClientHeartbeat(nowMs).ToJson();
                    await SendJsonAsync(hbJson, ct).ConfigureAwait(false);
                }
            }
        }

        private void CleanupSocket()
        {
            ClientWebSocket sock = Interlocked.Exchange(ref _socket, null);
            if (sock != null)
            {
                try
                {
                    if (sock.State == WebSocketState.Open || sock.State == WebSocketState.CloseReceived)
                    {
                        sock.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Client stopping", CancellationToken.None).Wait(200);
                    }
                }
                catch { }
                finally
                {
                    try { sock.Dispose(); } catch { }
                }
            }
        }

        private static string EnsureTrailingSlash(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return DefaultServerUri;
            string trimmed = uri.Trim();
            return trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : trimmed + "/";
        }
    }
}
