using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TrishulSim.Network
{
    /// <summary>
    /// Instructor C2 localhost WebSocket IPC server (ws://127.0.0.1:9700).
    /// Implements board/contracts/websocket-ipc.md v1.
    /// </summary>
    public class WsServer : MonoBehaviour
    {
        public const string DefaultHost = "127.0.0.1";
        public const int DefaultPort = 9700;

        public static WsServer Instance { get; private set; }
        public static int ActiveConnectedCount
        {
            get { return Instance != null ? Instance.ConnectedCount : 0; }
        }

        [SerializeField] private int port = DefaultPort;
        [SerializeField] private int heartbeatIntervalMs = 5000;
        [SerializeField] private int staleThresholdMs = 15000;
        [SerializeField] private bool autoStart = true;

        private readonly object _clientsLock = new object();
        private readonly Dictionary<string, ClientSession> _clients = new Dictionary<string, ClientSession>(StringComparer.Ordinal);
        private readonly object _mainThreadQueueLock = new object();
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();

        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private SynchronizationContext _syncContext;
        private int _mainThreadId;
        private volatile bool _isRunning;
#if UNITY_5_3_OR_NEWER
        private bool _dispatchEventsOnMainThread = true;
#else
        private bool _dispatchEventsOnMainThread = false;
#endif

        public event Action<JsonEnvelope> OnMessageReceived;
        public event Action<string, string> OnClientReady;
        public event Action OnClientConnected;
        public event Action OnClientDisconnected;

        public int Port
        {
            get { return port; }
            set { port = value; }
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

        public bool IsRunning
        {
            get { return _isRunning; }
        }

        public int ConnectedCount
        {
            get
            {
                lock (_clientsLock)
                {
                    int active = 0;
                    foreach (KeyValuePair<string, ClientSession> kvp in _clients)
                    {
                        if (kvp.Value.Socket != null && kvp.Value.Socket.State == WebSocketState.Open && kvp.Value.IsReady)
                            active++;
                    }
                    return active;
                }
            }
        }

        public int RawOpenSocketCount
        {
            get
            {
                lock (_clientsLock)
                {
                    int open = 0;
                    foreach (KeyValuePair<string, ClientSession> kvp in _clients)
                    {
                        if (kvp.Value.Socket != null && kvp.Value.Socket.State == WebSocketState.Open)
                            open++;
                    }
                    return open;
                }
            }
        }

        public int StaleClientCount
        {
            get
            {
                lock (_clientsLock)
                {
                    int stale = 0;
                    foreach (KeyValuePair<string, ClientSession> kvp in _clients)
                    {
                        if (kvp.Value.Socket != null && kvp.Value.Socket.State == WebSocketState.Open && kvp.Value.IsStale)
                            stale++;
                    }
                    return stale;
                }
            }
        }

        public static JsonEnvelope Parse(string json)
        {
            return JsonEnvelope.Parse(json);
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

            StartServer();
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
                    Debug.LogError("[WsServer] Event callback error: " + ex.Message);
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

        public static void CheckAirGapStatus()
        {
            if (Application.internetReachability != NetworkReachability.NotReachable)
            {
                Debug.LogWarning("[WsServer] [AIR-GAP WARNING] External internet connectivity detected (" + Application.internetReachability + "). TRISHUL-SIM requires air-gapped deployment; zero external telemetry or outbound connections permitted.");
            }
            else
            {
                Debug.Log("[WsServer] [AIR-GAP CONFIRMED] NetworkReachability is NotReachable. Air-gapped localhost operation verified.");
            }
        }

        public bool StartServer()
        {
            CaptureMainThreadContext();
            CheckAirGapStatus();
            if (_isRunning)
                return true;

            string prefix = "http://" + DefaultHost + ":" + port + "/";
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(prefix);
                _listener.Start();
                _cts = new CancellationTokenSource();
                _isRunning = true;

                Task.Run(() => AcceptLoopAsync(_cts.Token));
                Task.Run(() => HeartbeatLoopAsync(_cts.Token));

                Debug.Log("[WsServer] Listening on ws://" + DefaultHost + ":" + port + "/");
                return true;
            }
            catch (Exception ex)
            {
                // Split-screen loopback safe: log warning rather than crashing if already bound.
                Debug.LogWarning("[WsServer] Could not bind " + prefix + " (already bound or unavailable): " + ex.Message);
                CleanupListener();
                return false;
            }
        }

        public void Stop()
        {
            if (!_isRunning && _listener == null)
                return;

            _isRunning = false;

            if (_cts != null)
            {
                try { _cts.Cancel(); } catch { }
            }

            List<ClientSession> sessionsSnapshot;
            lock (_clientsLock)
            {
                sessionsSnapshot = new List<ClientSession>(_clients.Values);
                _clients.Clear();
            }

            for (int i = 0; i < sessionsSnapshot.Count; i++)
            {
                ClientSession session = sessionsSnapshot[i];
                try
                {
                    if (session.Socket != null &&
                        (session.Socket.State == WebSocketState.Open || session.Socket.State == WebSocketState.CloseReceived))
                    {
                        session.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Server stopping", CancellationToken.None)
                            .Wait(200);
                    }
                }
                catch { }
                finally
                {
                    try { if (session.Socket != null) session.Socket.Dispose(); } catch { }
                    session.DisposeLock();
                }
            }

            CleanupListener();

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

        public async Task BroadcastAsync(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length > JsonEnvelope.MaxMessageBytes)
            {
                Debug.LogWarning("[WsServer] Broadcast rejected: payload exceeds 64 KB limit (" + bytes.Length + " bytes).");
                return;
            }

            List<ClientSession> sessions;
            lock (_clientsLock)
            {
                sessions = new List<ClientSession>(_clients.Values);
            }

            for (int i = 0; i < sessions.Count; i++)
            {
                ClientSession session = sessions[i];
                if (session.Socket != null && session.Socket.State == WebSocketState.Open)
                {
                    await SendBytesSafeAsync(session, bytes, _cts != null ? _cts.Token : CancellationToken.None).ConfigureAwait(false);
                }
            }
        }

        public void CheckStaleClients(long nowUnixMs)
        {
            List<ClientSession> sessions;
            lock (_clientsLock)
            {
                sessions = new List<ClientSession>(_clients.Values);
            }

            for (int i = 0; i < sessions.Count; i++)
            {
                ClientSession session = sessions[i];
                if (session.Socket == null || session.Socket.State != WebSocketState.Open)
                    continue;

                long silenceMs = nowUnixMs - Interlocked.Read(ref session.LastSeenMs);
                if (silenceMs >= staleThresholdMs && !session.IsStale)
                {
                    session.IsStale = true;
                    Debug.LogWarning("[WsServer] Client " + session.Id + " silent for " + silenceMs +
                                     "ms (>15s) \u2014 marked stale (degraded mode, scenario continues)");
                }
            }
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
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    if (!_isRunning || ct.IsCancellationRequested)
                        break;
                    continue;
                }
                catch (Exception ex)
                {
                    if (!_isRunning || ct.IsCancellationRequested)
                        break;
                    Debug.LogWarning("[WsServer] Accept error: " + ex.Message);
                    continue;
                }

                if (context == null)
                    continue;

                if (!context.Request.IsWebSocketRequest)
                {
                    try
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                    catch { }
                    continue;
                }

                try
                {
                    HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
                    long nowMs = JsonEnvelope.NowUnixMs();
                    ClientSession session = new ClientSession(
                        "client-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                        wsContext.WebSocket,
                        nowMs);

                    lock (_clientsLock)
                    {
                        _clients[session.Id] = session;
                    }

                    Task _ = Task.Run(() => ReceiveClientLoopAsync(session, ct));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[WsServer] WebSocket handshake failed: " + ex.Message);
                    try
                    {
                        context.Response.StatusCode = 500;
                        context.Response.Close();
                    }
                    catch { }
                }
            }
        }

        private async Task ReceiveClientLoopAsync(ClientSession session, CancellationToken ct)
        {
            byte[] buffer = new byte[8192];
            try
            {
                while (!ct.IsCancellationRequested && _isRunning && session.Socket != null && session.Socket.State == WebSocketState.Open)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        bool oversized = false;
                        do
                        {
                            result = await session.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                await CloseSessionAsync(session, WebSocketCloseStatus.NormalClosure, "Client closed").ConfigureAwait(false);
                                return;
                            }

                            if (result.MessageType == WebSocketMessageType.Binary)
                            {
                                await SendErrorAsync(session, JsonEnvelope.ERR_PAYLOAD_INVALID, "Binary WebSocket frames are not supported.", false, ct).ConfigureAwait(false);
                                oversized = true;
                            }
                            else if (!oversized)
                            {
                                if (ms.Length + result.Count > JsonEnvelope.MaxMessageBytes)
                                {
                                    oversized = true;
                                    await SendErrorAsync(session, JsonEnvelope.ERR_PAYLOAD_INVALID, "Message exceeds max size of 64 KB.", false, ct).ConfigureAwait(false);
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
                        await HandleIncomingMessageAsync(session, rawJson, ct).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (WebSocketException)
            {
                // Client disconnected abruptly
            }
            catch (Exception ex)
            {
                if (_isRunning && !ct.IsCancellationRequested)
                    Debug.LogWarning("[WsServer] Client receive loop error (" + session.Id + "): " + ex.Message);
            }
            finally
            {
                RemoveSession(session);
            }
        }

        private async Task HandleIncomingMessageAsync(ClientSession session, string rawJson, CancellationToken ct)
        {
            long nowMs = JsonEnvelope.NowUnixMs();
            Interlocked.Exchange(ref session.LastSeenMs, nowMs);
            session.IsStale = false;

            JsonEnvelope envelope;
            string parseError;
            if (!JsonEnvelope.TryParse(rawJson, out envelope, out parseError))
            {
                Debug.LogWarning("[WsServer] Invalid envelope from " + session.Id + ": " + parseError);
                await SendErrorAsync(session, JsonEnvelope.ERR_PAYLOAD_INVALID, parseError, false, ct).ConfigureAwait(false);
                return;
            }

            if (envelope.v != JsonEnvelope.CurrentVersion)
            {
                string msg = "Server is v" + JsonEnvelope.CurrentVersion + ", client sent v" + envelope.v + " \u2014 update client binary.";
                Debug.LogWarning("[WsServer] Version mismatch from " + session.Id + ": " + msg);
                await SendErrorAsync(session, JsonEnvelope.ERR_VERSION_MISMATCH, msg, true, ct).ConfigureAwait(false);
                return;
            }

            if (string.Equals(envelope.type, JsonEnvelope.CLIENT_READY, StringComparison.Ordinal))
            {
                string clientRole = envelope.GetPayloadString("clientRole");
                string buildVersion = envelope.GetPayloadString("buildVersion");
                if (string.IsNullOrWhiteSpace(clientRole))
                {
                    await SendErrorAsync(session, JsonEnvelope.ERR_PAYLOAD_INVALID, "CLIENT_READY missing required field 'clientRole'.", false, ct).ConfigureAwait(false);
                    return;
                }

                bool wasReady = session.IsReady;
                session.ClientRole = clientRole;
                session.BuildVersion = buildVersion ?? "0.1.0";
                session.IsReady = true;

                Debug.Log("[WsServer] CLIENT_READY received from " + clientRole);

                string resolvedVersion = session.BuildVersion;
                DispatchEvent(() =>
                {
                    Action<string, string> readyHandler = OnClientReady;
                    if (readyHandler != null)
                        readyHandler(clientRole, resolvedVersion);

                    if (!wasReady)
                    {
                        Action connectedHandler = OnClientConnected;
                        if (connectedHandler != null)
                            connectedHandler();
                    }
                });

                string ackJson = JsonEnvelope.CreateServerHeartbeatAck(nowMs).ToJson();
                await SendBytesSafeAsync(session, Encoding.UTF8.GetBytes(ackJson), ct).ConfigureAwait(false);
            }
            else if (string.Equals(envelope.type, JsonEnvelope.CLIENT_HEARTBEAT, StringComparison.Ordinal))
            {
                string ackJson = JsonEnvelope.CreateServerHeartbeatAck(nowMs).ToJson();
                await SendBytesSafeAsync(session, Encoding.UTF8.GetBytes(ackJson), ct).ConfigureAwait(false);
            }
            else if (string.Equals(envelope.type, JsonEnvelope.SCORECARD_PAYLOAD, StringComparison.Ordinal))
            {
                // Per board/contracts/scorecard.md and AA-P0-B: Server logs and immediately re-broadcasts SCORECARD_PAYLOAD to all connected clients.
                Debug.Log("[WsServer] SCORECARD_PAYLOAD received \u2014 routing to all clients");
                await BroadcastAsync(rawJson).ConfigureAwait(false);
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
            while (!ct.IsCancellationRequested && _isRunning)
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
                CheckStaleClients(nowMs);

                string ackJson = JsonEnvelope.CreateServerHeartbeatAck(nowMs).ToJson();
                await BroadcastAsync(ackJson).ConfigureAwait(false);
            }
        }

        private async Task SendErrorAsync(ClientSession session, string code, string message, bool fatal, CancellationToken ct)
        {
            string errJson = JsonEnvelope.CreateError(code, message, fatal).ToJson();
            await SendBytesSafeAsync(session, Encoding.UTF8.GetBytes(errJson), ct).ConfigureAwait(false);
        }

        private async Task SendBytesSafeAsync(ClientSession session, byte[] bytes, CancellationToken ct)
        {
            if (session == null || session.Socket == null || session.Socket.State != WebSocketState.Open)
                return;

            bool acquired = false;
            try
            {
                await session.SendLock.WaitAsync(ct).ConfigureAwait(false);
                acquired = true;
                if (session.Socket.State == WebSocketState.Open)
                {
                    await session.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
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
                RemoveSession(session);
            }
            catch (Exception ex)
            {
                if (_isRunning)
                    Debug.LogWarning("[WsServer] Send error to " + session.Id + ": " + ex.Message);
            }
            finally
            {
                if (acquired)
                {
                    try { session.SendLock.Release(); } catch { }
                }
            }
        }

        private async Task CloseSessionAsync(ClientSession session, WebSocketCloseStatus status, string description)
        {
            try
            {
                if (session.Socket != null &&
                    (session.Socket.State == WebSocketState.Open || session.Socket.State == WebSocketState.CloseReceived))
                {
                    await session.Socket.CloseOutputAsync(status, description, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch { }
            finally
            {
                RemoveSession(session);
            }
        }

        private void RemoveSession(ClientSession session)
        {
            if (session == null)
                return;

            bool removed;
            bool wasReady = session.IsReady;
            lock (_clientsLock)
            {
                removed = _clients.Remove(session.Id);
            }

            if (removed)
            {
                try { if (session.Socket != null) session.Socket.Dispose(); } catch { }
                session.DisposeLock();

                if (wasReady && _isRunning)
                {
                    DispatchEvent(() =>
                    {
                        Action disconnectedHandler = OnClientDisconnected;
                        if (disconnectedHandler != null)
                            disconnectedHandler();
                    });
                }
            }
        }

        private void CleanupListener()
        {
            if (_listener != null)
            {
                try { if (_listener.IsListening) _listener.Stop(); } catch { }
                try { _listener.Close(); } catch { }
                _listener = null;
            }
        }

        private sealed class ClientSession
        {
            public readonly string Id;
            public readonly WebSocket Socket;
            public readonly SemaphoreSlim SendLock = new SemaphoreSlim(1, 1);
            public long LastSeenMs;
            public string ClientRole = "UNKNOWN";
            public string BuildVersion = "0.1.0";
            public volatile bool IsReady;
            public volatile bool IsStale;

            public ClientSession(string id, WebSocket socket, long connectedAtMs)
            {
                Id = id;
                Socket = socket;
                LastSeenMs = connectedAtMs;
            }

            public void DisposeLock()
            {
                try { SendLock.Dispose(); } catch { }
            }
        }
    }
}
