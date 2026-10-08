using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TrishulSim.Network
{
    /// <summary>
    /// Runnable self-test for US-P0-A (WebSocket IPC + JsonEnvelope + ConnectionStatusUI + scene GUID wiring).
    /// Compiles and runs directly with csc.exe or inside Unity tests.
    /// </summary>
    public static class WsIpcSelfTest
    {
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                RunAllAsync().GetAwaiter().GetResult();
                Console.WriteLine("[PASS] All US-P0-A WebSocket IPC tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] " + ex);
                return 1;
            }
        }
#endif

        public static async Task RunAllAsync()
        {
            TestEnvelopeParsingAndEdgeCases();
            TestExponentialBackoffCap();
            TestSceneAndMetaGuidWiring();
            TestAutoStartDisabledGuard();
            await TestLiveHandshakeBroadcastAndErrorsAsync();
            await TestUnauthenticatedSocketNotCountedAsync();
            await TestMainThreadDispatchQueueAsync();
            await TestAutoReconnectOnServerRestartAsync();
        }

        private static void TestEnvelopeParsingAndEdgeCases()
        {
            // 1. Contract CLIENT_READY example with 64-bit Unix ms (> int.MaxValue)
            string readyRaw = "{ \"type\": \"CLIENT_READY\", \"v\": 1, \"ts\": 1759600010000, \"payload\": { \"clientRole\": \"TRAINEE_COCKPIT\", \"buildVersion\": \"0.1.0\" } }";
            JsonEnvelope env = WsServer.Parse(readyRaw);
            Assert(env.type == JsonEnvelope.CLIENT_READY, "Expected CLIENT_READY type");
            Assert(env.v == 1, "Expected v == 1");
            Assert(env.ts == 1759600010000L, "Expected 64-bit ts preserved");
            Assert(env.GetPayloadString("clientRole") == "TRAINEE_COCKPIT", "Expected clientRole TRAINEE_COCKPIT");
            Assert(env.GetPayloadString("buildVersion") == "0.1.0", "Expected buildVersion 0.1.0");

            // 2. Nested SCENARIO_START with Hindi UTF-8, nested objects, arrays, and reordered top-level keys
            string scenarioStartRaw =
                "{\n" +
                "  \"payload\": {\n" +
                "    \"sessionId\": \"sess-20261004-143201\",\n" +
                "    \"scenarioId\": \"punjab-fog-pincer-01\",\n" +
                "    \"scenarioConfig\": {\n" +
                "      \"nameHindi\": \"\u092A\u0902\u091C\u093E\u092C \u0938\u0940\u092E\u093E \u2014 \u0936\u0942\u0928\u094D\u092F \u0926\u0943\u0936\u094D\u092F\u0924\u093E \u092A\u093F\u0902\u0938\u0930\",\n" +
                "      \"weatherPreset\": { \"visibility\": 15.0, \"isNight\": true },\n" +
                "      \"swarmProfile\": { \"rfFreqGHz\": [5.825, 2.4], \"tacticTree\": \"PINCER_LOW_FAST\" }\n" +
                "    }\n" +
                "  },\n" +
                "  \"ts\": 1759600021000,\n" +
                "  \"v\": 1,\n" +
                "  \"type\": \"SCENARIO_START\"\n" +
                "}";
            JsonEnvelope startEnv = JsonEnvelope.Parse(scenarioStartRaw);
            Assert(startEnv.type == JsonEnvelope.SCENARIO_START, "Reordered keys SCENARIO_START type");
            Assert(startEnv.GetPayloadString("sessionId") == "sess-20261004-143201", "Nested payload sessionId");
            Assert(startEnv.payload.Contains("\u092A\u0902\u091C\u093E\u092C \u0938\u0940\u092E\u093E"), "UTF-8 Hindi preserved in payload");

            // 3. Escaped quotes and braces inside string values
            JsonEnvelope errEnv = JsonEnvelope.CreateError(JsonEnvelope.ERR_PAYLOAD_INVALID, "Bad token \"{foo}\" in line 1", false, 1759600099000L);
            JsonEnvelope roundTrip = JsonEnvelope.Parse(errEnv.ToJson());
            Assert(roundTrip.type == JsonEnvelope.ERROR, "Round-trip ERROR type");
            Assert(roundTrip.GetPayloadString("code") == JsonEnvelope.ERR_PAYLOAD_INVALID, "Round-trip ERROR code");
            Assert(roundTrip.GetPayloadString("message") == "Bad token \"{foo}\" in line 1", "Escaped quotes & braces preserved");
            bool fatal;
            Assert(roundTrip.TryGetPayloadBool("fatal", out fatal) && !fatal, "Round-trip fatal == false");

            // 4. Reject invalid inputs (including malformed nested objects/arrays, leading-zero numbers, bad unicode escapes)
            JsonEnvelope dummy;
            string error;
            Assert(!JsonEnvelope.TryParse(null, out dummy, out error), "Null JSON must fail");
            Assert(!JsonEnvelope.TryParse("", out dummy, out error), "Empty JSON must fail");
            Assert(!JsonEnvelope.TryParse("{\"type\":\"CLIENT_READY\",\"v\":1,\"ts\":100}", out dummy, out error), "Missing payload must fail");
            Assert(!JsonEnvelope.TryParse("{\"type\":\"CLIENT_READY\",\"v\":1,\"ts\":100,\"payload\":[]}", out dummy, out error), "Array payload must fail");
            Assert(!JsonEnvelope.TryParse("{\"type\":\"\",\"v\":1,\"ts\":100,\"payload\":{}}", out dummy, out error), "Empty type must fail");
            Assert(!JsonEnvelope.TryParse("{\"type\":\"CLIENT_READY\",\"v\":1,\"ts\":-5,\"payload\":{}}", out dummy, out error), "Negative ts must fail");
            Assert(!JsonEnvelope.TryParse("{\"type\":\"CLIENT_READY\",\"v\":1,\"ts\":100,\"payload\":{\"unclosed\":1}", out dummy, out error), "Unclosed brace must fail");
            Assert(!JsonEnvelope.TryParse("{\"type\":\"CLIENT_READY\",\"v\":1,\"ts\":100,\"payload\":{\"a\":1,}}", out dummy, out error), "Trailing comma in payload must fail");
            Assert(!JsonEnvelope.TryParse("{\"type\":\"SCENARIO_START\",\"v\":1,\"ts\":100,\"payload\":{\"scenarioConfig\":{INVALID}}}", out dummy, out error), "Malformed nested object must fail");
            Assert(!JsonEnvelope.TryParse("{\"type\":\"TELEMETRY_SNAPSHOT\",\"v\":1,\"ts\":100,\"payload\":{\"swarmSnapshot\":[1,2,]}}", out dummy, out error), "Trailing comma in nested array must fail");
            Assert(!JsonEnvelope.TryParse("{\"type\":\"CLIENT_READY\",\"v\":1,\"ts\":0123,\"payload\":{}}", out dummy, out error), "Leading zero number must fail");
            Assert(!JsonEnvelope.TryParse("{\"type\":\"CLIENT_READY\",\"v\":1.5,\"ts\":100,\"payload\":{}}", out dummy, out error), "Non-integral v must fail");
            Assert(!JsonEnvelope.TryParse("{\"type\":\"CLIENT_READY\",\"v\":1,\"ts\":100,\"payload\":{\"bad\":\"\\u 123\"}}", out dummy, out error), "Whitespace in unicode escape must fail");

            string oversized = "{\"type\":\"CLIENT_READY\",\"v\":1,\"ts\":100,\"payload\":{\"pad\":\"" + new string('x', JsonEnvelope.MaxMessageBytes) + "\"}}";
            Assert(!JsonEnvelope.TryParse(oversized, out dummy, out error), "Oversized >64KB envelope must fail");
        }

        private static void TestExponentialBackoffCap()
        {
            Assert(WsClient.CalculateBackoffMs(0, 1000, 30000) == 1000, "Attempt 0 -> 1000ms");
            Assert(WsClient.CalculateBackoffMs(1, 1000, 30000) == 2000, "Attempt 1 -> 2000ms");
            Assert(WsClient.CalculateBackoffMs(2, 1000, 30000) == 4000, "Attempt 2 -> 4000ms");
            Assert(WsClient.CalculateBackoffMs(3, 1000, 30000) == 8000, "Attempt 3 -> 8000ms");
            Assert(WsClient.CalculateBackoffMs(4, 1000, 30000) == 16000, "Attempt 4 -> 16000ms");
            Assert(WsClient.CalculateBackoffMs(5, 1000, 30000) == 30000, "Attempt 5 -> capped at 30000ms");
            Assert(WsClient.CalculateBackoffMs(12, 1000, 30000) == 30000, "Attempt 12 -> capped at 30000ms");
        }

        private static void TestSceneAndMetaGuidWiring()
        {
            if (!File.Exists("Assets/Scenes/InstructorC2.unity"))
                return;

            string c2Scene = File.ReadAllText("Assets/Scenes/InstructorC2.unity", Encoding.UTF8);
            string cockpitScene = File.ReadAllText("Assets/Scenes/TraineeCockpit.unity", Encoding.UTF8);
            string wsServerMeta = File.ReadAllText("Assets/Scripts/Network/WsServer.cs.meta", Encoding.UTF8);
            string wsClientMeta = File.ReadAllText("Assets/Scripts/Network/WsClient.cs.meta", Encoding.UTF8);
            string statusUiMeta = File.ReadAllText("Assets/Scripts/Network/ConnectionStatusUI.cs.meta", Encoding.UTF8);

            Assert(wsServerMeta.Contains("guid: a1b2c3d4e5f647898a1b2c3d4e5f6001"), "WsServer.cs.meta GUID mismatch");
            Assert(wsClientMeta.Contains("guid: a1b2c3d4e5f647898a1b2c3d4e5f6002"), "WsClient.cs.meta GUID mismatch");
            Assert(statusUiMeta.Contains("guid: a1b2c3d4e5f647898a1b2c3d4e5f6003"), "ConnectionStatusUI.cs.meta GUID mismatch");

            Assert(c2Scene.Contains("m_Name: NetworkManager"), "InstructorC2.unity must contain NetworkManager GameObject");
            Assert(c2Scene.Contains("guid: a1b2c3d4e5f647898a1b2c3d4e5f6001"), "InstructorC2.unity must reference WsServer GUID");
            Assert(c2Scene.Contains("guid: a1b2c3d4e5f647898a1b2c3d4e5f6003"), "InstructorC2.unity must reference ConnectionStatusUI GUID");
            Assert(c2Scene.Contains("--- !u!223 &350000002"), "InstructorC2.unity must contain a Canvas component (!u!223) so UI.Text renders");
            Assert(c2Scene.Contains("m_Father: {fileID: 350000001}"), "ConnectionStatusText RectTransform must be parented under Canvas RectTransform");

            Assert(cockpitScene.Contains("m_Name: NetworkManager"), "TraineeCockpit.unity must contain NetworkManager GameObject");
            Assert(cockpitScene.Contains("guid: a1b2c3d4e5f647898a1b2c3d4e5f6002"), "TraineeCockpit.unity must reference WsClient GUID");
            Assert(cockpitScene.Contains("ws://127.0.0.1:9700/"), "TraineeCockpit.unity must configure ws://127.0.0.1:9700/");
        }

        private static void TestAutoStartDisabledGuard()
        {
            WsServer server = new WsServer();
            server.AutoStart = false;
            server.Start();
            Assert(!server.IsRunning, "WsServer with AutoStart=false must not start in Start()");

            WsClient client = new WsClient();
            client.AutoStart = false;
            client.Start();
            Assert(!client.IsConnected, "WsClient with AutoStart=false must not connect in Start()");
        }

        private static async Task TestLiveHandshakeBroadcastAndErrorsAsync()
        {
            List<string> logs = new List<string>();
            object logLock = new object();

#if !UNITY_5_3_OR_NEWER
            Action<string> onLog = msg => { lock (logLock) { logs.Add(msg); } };
            Debug.OnLog += onLog;
#endif

            WsServer server = new WsServer();
            server.Port = WsServer.DefaultPort; // 9700 per contract
            server.HeartbeatIntervalMs = 200;
            server.StaleThresholdMs = 500;

            Text uiText = new Text();
            ConnectionStatusUI statusUi = new ConnectionStatusUI();
            statusUi.Server = server;
            statusUi.StatusText = uiText;

            WsClient client = new WsClient();
            client.ServerUri = WsClient.DefaultServerUri; // ws://127.0.0.1:9700/
            client.HeartbeatIntervalMs = 200;
            client.StaleThresholdMs = 500;

            try
            {
                // Initial UI state before connection
                statusUi.Start();
                Assert(statusUi.CurrentStatusText == ConnectionStatusUI.WaitingText, "Initial UI text must be waiting");
                Assert(uiText.text == ConnectionStatusUI.WaitingText, "Initial UI.Text must be waiting");

                TaskCompletionSource<bool> linkConfirmedTcs = new TaskCompletionSource<bool>();
                TaskCompletionSource<bool> serverClientConnectedTcs = new TaskCompletionSource<bool>();
                TaskCompletionSource<bool> serverClientDisconnectedTcs = new TaskCompletionSource<bool>();
                TaskCompletionSource<JsonEnvelope> broadcastReceivedTcs = new TaskCompletionSource<JsonEnvelope>();
                TaskCompletionSource<JsonEnvelope> scorecardRelayedTcs = new TaskCompletionSource<JsonEnvelope>();
                Queue<JsonEnvelope> errorsReceived = new Queue<JsonEnvelope>();
                object errLock = new object();
                SemaphoreSlim errorSignal = new SemaphoreSlim(0, 10);

                server.OnClientConnected += () => serverClientConnectedTcs.TrySetResult(true);
                server.OnClientDisconnected += () => serverClientDisconnectedTcs.TrySetResult(true);
                client.OnLinkConfirmed += () => linkConfirmedTcs.TrySetResult(true);
                client.OnMessageReceived += env =>
                {
                    if (env.type == JsonEnvelope.SCENARIO_START)
                        broadcastReceivedTcs.TrySetResult(env);
                    else if (env.type == JsonEnvelope.SCORECARD_PAYLOAD)
                        scorecardRelayedTcs.TrySetResult(env);
                    else if (env.type == JsonEnvelope.ERROR)
                    {
                        lock (errLock) { errorsReceived.Enqueue(env); }
                        errorSignal.Release();
                    }
                };

                server.Start();
                Assert(server.IsRunning, "WsServer should be running on port 9700");

                // Split-screen duplicate bind guard check
                WsServer duplicateServer = new WsServer();
                duplicateServer.Port = WsServer.DefaultPort;
                bool duplicateBound = duplicateServer.StartServer();
                Assert(!duplicateBound, "Duplicate server bind on same port should return false cleanly without throwing");

                // Non-WebSocket HTTP GET request should receive HTTP 400 Bad Request
                HttpWebRequest httpReq = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:9700/");
                httpReq.Method = "GET";
                try
                {
                    using (WebResponse resp = httpReq.GetResponse())
                    {
                        Assert(false, "Expected HTTP 400 WebException for plain HTTP GET");
                    }
                }
                catch (WebException wex)
                {
                    HttpWebResponse httpResp = wex.Response as HttpWebResponse;
                    Assert(httpResp != null && httpResp.StatusCode == HttpStatusCode.BadRequest, "Expected HTTP 400 BadRequest on non-WS request");
                }

                client.Start();

                Task completed = await Task.WhenAny(linkConfirmedTcs.Task, Task.Delay(3000));
                Assert(completed == linkConfirmedTcs.Task && linkConfirmedTcs.Task.Result, "Timed out waiting for CLIENT_READY -> SERVER_HEARTBEAT_ACK handshake");

                Task connectedEvtDone = await Task.WhenAny(serverClientConnectedTcs.Task, Task.Delay(1000));
                Assert(connectedEvtDone == serverClientConnectedTcs.Task && serverClientConnectedTcs.Task.Result,
                    "Expected WsServer.OnClientConnected event to fire on CLIENT_READY");

                Assert(server.ConnectedCount == 1, "Expected ConnectedCount == 1 after handshake");
                Assert(client.IsLinkConfirmed, "Expected client.IsLinkConfirmed == true");

                statusUi.Update();
                Debug.Log("ConnectionStatusUI: \"" + statusUi.CurrentStatusText + "\"");
                Assert(statusUi.CurrentStatusText == "\u25CF LAN LINK CONFIRMED", "Expected ConnectionStatusUI to read '● LAN LINK CONFIRMED'");
                Assert(uiText.text == "\u25CF LAN LINK CONFIRMED", "Expected UnityEngine.UI.Text to read '● LAN LINK CONFIRMED'");

#if !UNITY_5_3_OR_NEWER
                lock (logLock)
                {
                    Assert(logs.Contains("[WsServer] CLIENT_READY received from TRAINEE_COCKPIT"),
                        "Expected exact log: [WsServer] CLIENT_READY received from TRAINEE_COCKPIT");
                    Assert(logs.Contains("[WsClient] SERVER_HEARTBEAT_ACK received \u2014 link green"),
                        "Expected exact log: [WsClient] SERVER_HEARTBEAT_ACK received — link green");
                }
#endif

                // Test server -> client BroadcastAsync (SCENARIO_START latency <= 200ms)
                long sendStartMs = JsonEnvelope.NowUnixMs();
                JsonEnvelope startEnvelope = JsonEnvelope.Create(
                    JsonEnvelope.SCENARIO_START,
                    "{\"sessionId\":\"sess-20261005-0001\",\"scenarioId\":\"punjab-fog-pincer-01\"}",
                    1,
                    sendStartMs);
                await server.BroadcastAsync(startEnvelope.ToJson());

                Task bcastDone = await Task.WhenAny(broadcastReceivedTcs.Task, Task.Delay(1000));
                Assert(bcastDone == broadcastReceivedTcs.Task, "Expected SCENARIO_START broadcast to arrive");
                long deliveryLatencyMs = JsonEnvelope.NowUnixMs() - sendStartMs;
                Assert(deliveryLatencyMs <= 200, "SCENARIO_START delivery exceeded 200ms budget: " + deliveryLatencyMs + "ms");
                Assert(broadcastReceivedTcs.Task.Result.GetPayloadString("sessionId") == "sess-20261005-0001", "Broadcast sessionId mismatch");

                // Test client -> server -> all clients SCORECARD_PAYLOAD relay via SendAsync alias (AA-P0-B contract)
                JsonEnvelope scorecardEnv = JsonEnvelope.Create(
                    JsonEnvelope.SCORECARD_PAYLOAD,
                    "{\"scorecard\":{\"sessionId\":\"sess-20261005-0001\",\"doctrinePassed\":true}}");
                await client.SendAsync(scorecardEnv.ToJson());

                Task relayDone = await Task.WhenAny(scorecardRelayedTcs.Task, Task.Delay(1000));
                Assert(relayDone == scorecardRelayedTcs.Task, "Expected SCORECARD_PAYLOAD relay from server");

#if !UNITY_5_3_OR_NEWER
                lock (logLock)
                {
                    Assert(logs.Contains("[WsServer] SCORECARD_PAYLOAD received \u2014 routing to all clients"),
                        "Expected exact log: [WsServer] SCORECARD_PAYLOAD received — routing to all clients");
                }
#endif

                // Test malformed JSON -> PAYLOAD_INVALID (fatal: false)
                await client.SendJsonAsync("{not-valid-json");
                Assert(await errorSignal.WaitAsync(1000), "Expected ERROR response for malformed JSON");
                JsonEnvelope badJsonErr;
                lock (errLock) { badJsonErr = errorsReceived.Dequeue(); }
                Assert(badJsonErr.GetPayloadString("code") == JsonEnvelope.ERR_PAYLOAD_INVALID, "Expected PAYLOAD_INVALID error code");
                bool badJsonFatal;
                Assert(badJsonErr.TryGetPayloadBool("fatal", out badJsonFatal) && !badJsonFatal, "PAYLOAD_INVALID must be fatal: false");

                // Test VERSION_MISMATCH error response (fatal: true)
                JsonEnvelope badVersionEnv = JsonEnvelope.Create(JsonEnvelope.CLIENT_HEARTBEAT, "{}", 2);
                await client.SendEnvelopeAsync(badVersionEnv);

                Assert(await errorSignal.WaitAsync(1000), "Expected ERROR response for VERSION_MISMATCH");
                JsonEnvelope verErr;
                lock (errLock) { verErr = errorsReceived.Dequeue(); }
                Assert(verErr.GetPayloadString("code") == JsonEnvelope.ERR_VERSION_MISMATCH, "Expected VERSION_MISMATCH error code");
                bool isFatal;
                Assert(verErr.TryGetPayloadBool("fatal", out isFatal) && isFatal, "VERSION_MISMATCH must be fatal: true");

                // Test 15s stale detection (degraded mode without scenario abort)
                long futureTimeMs = JsonEnvelope.NowUnixMs() + 20000;
                server.CheckStaleClients(futureTimeMs);
                Assert(server.StaleClientCount == 1, "Server should mark client stale after >15s silence without disconnecting");
                Assert(server.ConnectedCount == 1, "Stale client must remain connected in degraded mode");

                client.CheckHeartbeatTimeout(futureTimeMs);
                Assert(client.IsLinkStale, "Client should mark link stale after >15s silence without aborting");

                // Stop client and verify OnClientDisconnected fires on server
                client.Stop();
                Task disconnectedEvtDone = await Task.WhenAny(serverClientDisconnectedTcs.Task, Task.Delay(1000));
                Assert(disconnectedEvtDone == serverClientDisconnectedTcs.Task, "Expected WsServer.OnClientDisconnected event when client disconnects");
            }
            finally
            {
#if !UNITY_5_3_OR_NEWER
                Debug.OnLog -= onLog;
#endif
                client.Stop();
                server.Stop();
            }
        }

        private static async Task TestUnauthenticatedSocketNotCountedAsync()
        {
            int testPort = 9702;
            WsServer server = new WsServer();
            server.Port = testPort;
            server.Start();

            System.Net.WebSockets.ClientWebSocket rawSocket = new System.Net.WebSockets.ClientWebSocket();
            try
            {
                await rawSocket.ConnectAsync(new Uri("ws://127.0.0.1:" + testPort + "/"), CancellationToken.None);
                await Task.Delay(80);
                Assert(server.RawOpenSocketCount == 1, "Expected 1 raw open socket");
                Assert(server.ConnectedCount == 0, "Unauthenticated socket without CLIENT_READY must not increment ConnectedCount");
            }
            finally
            {
                try { rawSocket.Dispose(); } catch { }
                server.Stop();
            }
        }

        private static async Task TestMainThreadDispatchQueueAsync()
        {
            int testPort = 9703;
            WsServer server = new WsServer();
            server.Port = testPort;
            server.DispatchEventsOnMainThread = true;

            WsClient client = new WsClient();
            client.ServerUri = "ws://127.0.0.1:" + testPort + "/";
            client.DispatchEventsOnMainThread = true;

            bool serverConnectedFired = false;
            bool clientLinkConfirmedFired = false;
            server.OnClientConnected += () => serverConnectedFired = true;
            client.OnLinkConfirmed += () => clientLinkConfirmedFired = true;

            try
            {
                server.Start();
                client.Start();

                // Wait for background handshake to complete
                long deadline = JsonEnvelope.NowUnixMs() + 2000;
                while (!client.IsLinkConfirmed && JsonEnvelope.NowUnixMs() < deadline)
                    await Task.Delay(25);

                Assert(client.IsLinkConfirmed, "Expected background handshake to set IsLinkConfirmed");
                Assert(!serverConnectedFired, "Server event must remain queued until Update() drains main-thread queue");
                Assert(!clientLinkConfirmedFired, "Client event must remain queued until Update() drains main-thread queue");

                server.Update();
                client.Update();

                Assert(serverConnectedFired, "Expected server.Update() to drain OnClientConnected on main thread");
                Assert(clientLinkConfirmedFired, "Expected client.Update() to drain OnLinkConfirmed on main thread");
            }
            finally
            {
                client.Stop();
                server.Stop();
            }
        }

        private static async Task TestAutoReconnectOnServerRestartAsync()
        {
            int testPort = 9701;
            WsServer server = new WsServer();
            server.Port = testPort;
            server.HeartbeatIntervalMs = 200;

            WsClient client = new WsClient();
            client.ServerUri = "ws://127.0.0.1:" + testPort + "/";
            client.BaseReconnectDelayMs = 80;
            client.HeartbeatIntervalMs = 200;

            int confirmCount = 0;
            TaskCompletionSource<bool> firstConnectTcs = new TaskCompletionSource<bool>();
            TaskCompletionSource<bool> reconnectTcs = new TaskCompletionSource<bool>();

            client.OnLinkConfirmed += () =>
            {
                int count = Interlocked.Increment(ref confirmCount);
                if (count == 1)
                    firstConnectTcs.TrySetResult(true);
                else if (count >= 2)
                    reconnectTcs.TrySetResult(true);
            };

            try
            {
                server.Start();
                client.Start();

                Task first = await Task.WhenAny(firstConnectTcs.Task, Task.Delay(2000));
                Assert(first == firstConnectTcs.Task, "Initial connect on port 9701 failed");

                // Stop server to force disconnect, then restart server and verify auto-reconnect
                server.Stop();
                await Task.Delay(120);
                server.Start();

                Task reconnected = await Task.WhenAny(reconnectTcs.Task, Task.Delay(3000));
                Assert(reconnected == reconnectTcs.Task, "Client did not auto-reconnect after server restart");
                Assert(server.ConnectedCount == 1, "Expected ConnectedCount == 1 after auto-reconnect");
                Assert(client.IsLinkConfirmed, "Expected client.IsLinkConfirmed after auto-reconnect");
            }
            finally
            {
                client.Stop();
                server.Stop();
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message);
        }
    }
}
