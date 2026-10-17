import socket
import threading
import time
import hashlib
import base64
import struct
import json
import urllib.request
import importlib.util

# Load serve-gyro
spec = importlib.util.spec_from_file_location('serve_gyro', 'docs/serve-gyro.py')
sg = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sg)

GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"

def compute_accept(key):
    sha1 = hashlib.sha1((key + GUID).encode('utf-8')).digest()
    return base64.b64encode(sha1).decode('utf-8')

def build_ws_frame(payload_bytes, opcode=1, mask=True):
    length = len(payload_bytes)
    frame = bytearray()
    frame.append(0x80 | (opcode & 0x0F))
    mask_bit = 0x80 if mask else 0x00
    if length < 126:
        frame.append(mask_bit | length)
    elif length < 65536:
        frame.append(mask_bit | 126)
        frame.extend(struct.pack("!H", length))
    else:
        frame.append(mask_bit | 127)
        frame.extend(struct.pack("!Q", length))

    if mask:
        mask_key = b"\x5a\x6b\x7c\x8d"
        frame.extend(mask_key)
        masked = bytearray(length)
        for i in range(length):
            masked[i] = payload_bytes[i] ^ mask_key[i % 4]
        frame.extend(masked)
    else:
        frame.extend(payload_bytes)
    return bytes(frame)

def parse_ws_frame(recv_exact_fn):
    hdr = recv_exact_fn(2)
    if not hdr or len(hdr) < 2:
        return None, None
    b1, b2 = hdr[0], hdr[1]
    opcode = b1 & 0x0F
    is_masked = (b2 & 0x80) != 0
    length = b2 & 0x7F
    if length == 126:
        ext = recv_exact_fn(2)
        length = struct.unpack("!H", ext)[0]
    elif length == 127:
        ext = recv_exact_fn(8)
        length = struct.unpack("!Q", ext)[0]
    mask_key = recv_exact_fn(4) if is_masked else None
    payload = bytearray(recv_exact_fn(length)) if length > 0 else bytearray()
    if is_masked and mask_key:
        for i in range(len(payload)):
            payload[i] ^= mask_key[i % 4]
    return opcode, bytes(payload)

class MockUnityReceiver:
    """Implements the exact logic of GyroReceiver.cs including leftover buffer handling."""
    def __init__(self, port=9800):
        self.port = port
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.sock.bind(('127.0.0.1', self.port))
        self.sock.listen(5)
        self.running = True
        self.received_messages = []
        self.client_count = 0

    def start(self):
        self.thread = threading.Thread(target=self._accept_loop, daemon=True)
        self.thread.start()

    def _accept_loop(self):
        while self.running:
            try:
                conn, _ = self.sock.accept()
                self.client_count += 1
                t = threading.Thread(target=self._handle_client, args=(conn,), daemon=True)
                t.start()
            except Exception:
                break

    def _handle_client(self, conn):
        try:
            conn.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            handshake_buf = bytearray(4096)
            total_read = 0
            header_end = -1

            while total_read < len(handshake_buf):
                r = conn.recv_into(memoryview(handshake_buf)[total_read:])
                if r <= 0:
                    break
                total_read += r
                idx = handshake_buf[:total_read].find(b"\r\n\r\n")
                if idx != -1:
                    header_end = idx + 4
                    break

            if header_end == -1:
                conn.close()
                return

            req = handshake_buf[:header_end].decode('utf-8', errors='ignore')
            key_idx = req.lower().find("sec-websocket-key:")
            if key_idx == -1:
                conn.close()
                return

            val_start = key_idx + len("sec-websocket-key:")
            line_end = req.find("\r\n", val_start)
            if line_end == -1:
                line_end = len(req)
            key = req[val_start:line_end].strip()
            accept_val = compute_accept(key)

            resp = (
                f"HTTP/1.1 101 Switching Protocols\r\n"
                f"Upgrade: websocket\r\n"
                f"Connection: Upgrade\r\n"
                f"Sec-WebSocket-Accept: {accept_val}\r\n\r\n"
            ).encode('utf-8')
            conn.sendall(resp)

            # Leftover buffer management
            leftover_count = total_read - header_end
            leftover_offset = header_end

            def read_exact(count):
                nonlocal leftover_count, leftover_offset
                result = bytearray()
                if leftover_count > 0:
                    take = min(count, leftover_count)
                    result.extend(handshake_buf[leftover_offset:leftover_offset + take])
                    leftover_offset += take
                    leftover_count -= take
                while len(result) < count:
                    chunk = conn.recv(count - len(result))
                    if not chunk:
                        return None
                    result.extend(chunk)
                return bytes(result)

            while self.running:
                opcode, payload = parse_ws_frame(read_exact)
                if opcode is None:
                    break
                if opcode == 8: # Close
                    conn.sendall(build_ws_frame(b"", opcode=8, mask=False))
                    break
                if opcode == 9: # Ping -> Pong
                    conn.sendall(build_ws_frame(payload, opcode=10, mask=False))
                    continue
                if opcode == 1: # Text
                    text = payload.decode('utf-8', errors='ignore')
                    self.received_messages.append(text)
        except Exception:
            pass
        finally:
            try: conn.close()
            except Exception: pass

    def stop(self):
        self.running = False
        try: self.sock.close()
        except Exception: pass

def run_tests():
    print("=" * 60)
    print(" TRISHUL-SIM End-To-End Gyro Tracking Verification Suite")
    print("=" * 60)
    passed = 0
    total = 0

    # Test 1: RFC 6455 Accept Key Test Vector
    total += 1
    rfc_key = "dGhlIHNhbXBsZSBub25jZQ=="
    rfc_expected = "s3pPLMBiTxaQ9kYGzzhZRbK+xOo="
    actual_accept = compute_accept(rfc_key)
    assert actual_accept == rfc_expected, f"Expected {rfc_expected}, got {actual_accept}"
    print(f"[PASS] Test 1: RFC 6455 Handshake accept key hash ({actual_accept})")
    passed += 1

    # Test 2: IP Resolution (No link-local, prefers LAN)
    total += 1
    detected_ip = sg.get_laptop_ip()
    assert not detected_ip.startswith("127.") or detected_ip == "127.0.0.1", "Invalid loopback"
    assert not detected_ip.startswith("169.254."), "Must not use link-local APIPA"
    print(f"[PASS] Test 2: Laptop IP resolved cleanly to: {detected_ip}")
    passed += 1

    # Start serve-gyro proxy server on port 8080 in background thread
    proxy_server_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    proxy_server_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    proxy_server_sock.bind(("0.0.0.0", 8080))
    proxy_server_sock.listen(10)

    proxy_running = True
    def proxy_loop():
        while proxy_running:
            try:
                csock, caddr = proxy_server_sock.accept()
                t = threading.Thread(target=sg.handle_client, args=(csock, caddr), daemon=True)
                t.start()
            except Exception:
                break

    proxy_thread = threading.Thread(target=proxy_loop, daemon=True)
    proxy_thread.start()
    time.sleep(0.1)

    # Test 3: HTTP GET / serving vr-gyro-sender.html
    total += 1
    req = urllib.request.urlopen("http://127.0.0.1:8080/")
    assert req.status == 200
    html_data = req.read().decode('utf-8')
    assert "TRISHUL-SIM" in html_data
    assert "deviceorientation" in html_data
    assert "status-tracking" in html_data
    print(f"[PASS] Test 3: HTTP GET / returns vr-gyro-sender.html ({len(html_data)} bytes)")
    passed += 1

    # Test 4: HTTP GET /favicon.ico returns 204
    total += 1
    favicon_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    favicon_sock.connect(("127.0.0.1", 8080))
    favicon_sock.sendall(b"GET /favicon.ico HTTP/1.1\r\nHost: 127.0.0.1:8080\r\n\r\n")
    fav_resp = favicon_sock.recv(1024)
    favicon_sock.close()
    assert b"204 No Content" in fav_resp
    print("[PASS] Test 4: HTTP GET /favicon.ico returns 204 No Content")
    passed += 1

    # Test 5: Unity Offline returns 502 Bad Gateway
    total += 1
    offline_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    offline_sock.connect(("127.0.0.1", 8080))
    upgrade_req = (
        b"GET / HTTP/1.1\r\n"
        b"Host: 127.0.0.1:8080\r\n"
        b"Upgrade: websocket\r\n"
        b"Connection: Upgrade\r\n"
        b"Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n"
        b"Sec-WebSocket-Version: 13\r\n\r\n"
    )
    offline_sock.sendall(upgrade_req)
    offline_resp = offline_sock.recv(1024)
    offline_sock.close()
    assert b"502 Bad Gateway" in offline_resp
    print("[PASS] Test 5: WebSocket upgrade returns 502 Bad Gateway when Unity receiver is offline")
    passed += 1

    # Now start Mock Unity Receiver on port 9800
    unity_sim = MockUnityReceiver(9800)
    unity_sim.start()
    time.sleep(0.1)

    # Test 6: Pipelined Handshake + Immediate Frame 0 (Edge Case Verification)
    total += 1
    pipe_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    pipe_sock.connect(("127.0.0.1", 8080))
    frame0 = build_ws_frame(b'{"a":0.0,"b":0.0,"g":0.0}', opcode=1, mask=True)
    pipe_sock.sendall(upgrade_req + frame0)

    # Read 101 response
    resp101 = pipe_sock.recv(1024)
    assert b"101 Switching Protocols" in resp101
    assert b"Sec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=" in resp101
    time.sleep(0.1)
    assert len(unity_sim.received_messages) >= 1, "Immediate pipelined frame 0 was dropped!"
    assert '"a":0.0' in unity_sim.received_messages[0]
    print("[PASS] Test 6: Pipelined handshake + immediate frame 0 decoded without byte drops")
    passed += 1

    # Test 7: 60 Hz Continuous Streaming Relay (120 frames = 2 seconds)
    total += 1
    start_count = len(unity_sim.received_messages)
    for i in range(120):
        yaw = float(i) % 360.0
        pitch = float(i % 60) - 30.0
        roll = float(i % 30) - 15.0
        payload = json.dumps({"a": yaw, "b": pitch, "g": roll}).encode('utf-8')
        pipe_sock.sendall(build_ws_frame(payload, opcode=1, mask=True))
        time.sleep(1.0 / 60.0) # 60 Hz cadence

    time.sleep(0.2)
    end_count = len(unity_sim.received_messages)
    streamed = end_count - start_count
    assert streamed == 120, f"Expected 120 frames streamed, got {streamed}"
    print(f"[PASS] Test 7: Streamed {streamed}/120 frames at 60 Hz with 0 drops")
    passed += 1

    # Test 8: RFC 6455 Ping / Pong Relay
    total += 1
    ping_payload = b"trishul-ping-1234"
    pipe_sock.sendall(build_ws_frame(ping_payload, opcode=9, mask=True))
    pong_buf = bytearray()
    while len(pong_buf) < 2 + len(ping_payload):
        chunk = pipe_sock.recv(64)
        if not chunk: break
        pong_buf.extend(chunk)
    assert pong_buf[0] == 0x8A # FIN + Opcode 10 (Pong)
    assert ping_payload in pong_buf
    print("[PASS] Test 8: RFC 6455 Ping frame replied with matching Pong")
    passed += 1

    # Test 9: Graceful Disconnect & Reconnect Loop Absence
    total += 1
    # Client sends close frame
    pipe_sock.sendall(build_ws_frame(b"", opcode=8, mask=True))
    time.sleep(0.1)
    pipe_sock.close()

    # Reconnect new client
    client2 = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    client2.connect(("127.0.0.1", 8080))
    client2.sendall(upgrade_req)
    resp101_c2 = client2.recv(1024)
    assert b"101 Switching Protocols" in resp101_c2

    # Send 30 frames from reconnected client
    for i in range(30):
        payload = json.dumps({"a": 180.0, "b": 10.0, "g": -5.0}).encode('utf-8')
        client2.sendall(build_ws_frame(payload, opcode=1, mask=True))
        time.sleep(1.0 / 60.0)

    time.sleep(0.1)
    client2.close()
    assert len(unity_sim.received_messages) >= 151
    print(f"[PASS] Test 9: Client reconnect succeeded seamlessly; total frames parsed: {len(unity_sim.received_messages)}")
    passed += 1

    # Cleanup
    unity_sim.stop()
    proxy_running = False
    proxy_server_sock.close()
    time.sleep(0.2)

    print("=" * 60)
    print(f" ALL TESTS PASSED: {passed}/{total}")
    print("=" * 60)

if __name__ == "__main__":
    run_tests()
