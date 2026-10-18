import socket
import threading
import os
import sys
import time
import traceback

def log_error(msg):
    try:
        sys.stderr.write(f"{msg}\n")
        sys.stderr.flush()
    except Exception:
        pass

def get_laptop_ip():
    """
    Detect the laptop's LAN / Hotspot IP address.
    Tries UDP route discovery, hostname resolution, and local interface scans.
    Falls back to 127.0.0.1 if isolated.
    """
    # 1. Try UDP routing query (fastest, works when a gateway exists)
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(('8.8.8.8', 80))
        ip = s.getsockname()[0]
        s.close()
        if ip and not ip.startswith('127.'):
            return ip
    except Exception:
        pass

    # 2. Try socket.gethostbyname with local hostname
    try:
        hostname = socket.gethostname()
        ip = socket.gethostbyname(hostname)
        if ip and not ip.startswith('127.'):
            return ip
    except Exception:
        pass

    # 3. Try socket.getaddrinfo
    try:
        for info in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET):
            ip = info[4][0]
            if ip and not ip.startswith('127.'):
                return ip
    except Exception:
        pass

    return '127.0.0.1'

PORT = 8080
UNITY_HOST = '127.0.0.1'
UNITY_PORT = 9800
laptop_ip = get_laptop_ip()
script_dir = os.path.dirname(os.path.abspath(__file__))
html_path = os.path.join(script_dir, "vr-gyro-sender.html")
err_log_path = os.path.join(script_dir, "serve-gyro.log")

def get_html_bytes():
    try:
        with open(html_path, "r", encoding="utf-8") as f:
            template = f.read()
        rendered = template.replace("LAPTOP_IP", laptop_ip).encode("utf-8")
        return (
            b"HTTP/1.1 200 OK\r\n"
            b"Content-Type: text/html; charset=utf-8\r\n"
            b"Content-Length: " + str(len(rendered)).encode("ascii") + b"\r\n"
            b"Access-Control-Allow-Origin: *\r\n"
            b"Connection: close\r\n\r\n" + rendered
        )
    except Exception as e:
        err = f"HTTP/1.1 500 Internal Error\r\n\r\n{e}".encode("utf-8")
        return err

def pipe(src, dst):
    """
    Bidirectional streaming pipe between client socket and Unity socket.
    Exits and closes both endpoints gracefully on EOF or socket termination.
    """
    try:
        while True:
            data = src.recv(4096)
            if not data:
                break
            dst.sendall(data)
    except Exception:
        pass
    finally:
        try: src.close()
        except Exception: pass
        try: dst.close()
        except Exception: pass

def handle_client(client_sock, client_addr):
    try:
        # Read HTTP request header
        initial = client_sock.recv(4096)
        if not initial:
            try: client_sock.close()
            except Exception: pass
            return

        initial_lower = initial.lower()

        # Check if WebSocket upgrade request
        if b"upgrade" in initial_lower and b"websocket" in initial_lower:
            client_ip = client_addr[0]
            print(f"[{client_ip}] WebSocket Upgrade -> Proxying to Unity ({UNITY_HOST}:{UNITY_PORT})...")
            sys.stdout.flush()

            # Set TCP_NODELAY on client socket for minimum latency
            try:
                client_sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            except Exception:
                pass

            # Connect to Unity with retry (handles scene transitions or brief play-mode reloads)
            unity_sock = None
            for attempt in range(5):
                try:
                    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                    s.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                    s.connect((UNITY_HOST, UNITY_PORT))
                    unity_sock = s
                    break
                except Exception:
                    if unity_sock:
                        try: unity_sock.close()
                        except Exception: pass
                        unity_sock = None
                    if attempt < 4:
                        time.sleep(0.2)

            if unity_sock is None:
                print(f"[{client_ip}] Unity GyroReceiver not responding on {UNITY_HOST}:{UNITY_PORT}. Is Unity in Play mode?")
                sys.stdout.flush()
                try:
                    client_sock.sendall(
                        b"HTTP/1.1 502 Bad Gateway\r\n"
                        b"Content-Type: text/plain\r\n"
                        b"Connection: close\r\n\r\n"
                        b"Unity GyroReceiver is not running on port 9800. Please start Play mode in Unity."
                    )
                except Exception:
                    pass
                try:
                    client_sock.close()
                except Exception:
                    pass
                return

            # Forward the initial handshake request to Unity
            try:
                unity_sock.sendall(initial)
            except Exception as e:
                print(f"[{client_ip}] Handshake forwarding failed: {e}")
                sys.stdout.flush()
                try: unity_sock.close()
                except Exception: pass
                try: client_sock.close()
                except Exception: pass
                return

            # Start bidirectional proxy threads
            t1 = threading.Thread(target=pipe, args=(client_sock, unity_sock), daemon=True)
            t2 = threading.Thread(target=pipe, args=(unity_sock, client_sock), daemon=True)
            t1.start()
            t2.start()

        elif b"get /favicon.ico" in initial_lower:
            # Handle favicon quickly without sending full HTML
            try:
                client_sock.sendall(b"HTTP/1.1 204 No Content\r\nConnection: close\r\n\r\n")
            except Exception:
                pass
            try:
                client_sock.close()
            except Exception:
                pass
        else:
            # Serve vr-gyro-sender.html
            try:
                client_sock.sendall(get_html_bytes())
            except Exception:
                pass
            try:
                client_sock.close()
            except Exception:
                pass
    except Exception as e:
        print(f"[{client_addr[0]}] Connection error: {e}")
        sys.stdout.flush()
        try: client_sock.close()
        except Exception: pass

def main():
    print("")
    print("================================================")
    print(" TRISHUL-SIM - Gyro Sender & Relay Server")
    print("================================================")
    print("")
    print(f" Laptop IP:    {laptop_ip}")
    print(f" HTTP Server:  http://{laptop_ip}:{PORT}/")
    print(f" Unity Target: ws://{UNITY_HOST}:{UNITY_PORT}/")
    print("")
    print(" On phone (connected to laptop Wi-Fi/Hotspot):")
    print(f"   Chrome -> http://{laptop_ip}:{PORT}/")
    print("")
    print(" Press Ctrl+C to stop.")
    print("")
    sys.stdout.flush()

    server_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server_sock.bind(("0.0.0.0", PORT))
    server_sock.listen(10)

    print(f"[serve-gyro] Listening on port {PORT} (serving HTML + relaying WebSockets)...")
    sys.stdout.flush()

    try:
        while True:
            try:
                client_sock, client_addr = server_sock.accept()
            except (ConnectionAbortedError, ConnectionResetError):
                continue
            except OSError as e:
                # WSAEINTR or similar during shutdown
                if e.errno in (10004, 10038, 10053, 10054):
                    continue
                log_error(f"[serve-gyro] Accept OSError: {e}")
                time.sleep(0.1)
                continue
            except Exception as e:
                log_error(f"[serve-gyro] Accept error: {e}")
                time.sleep(0.1)
                continue

            t = threading.Thread(target=handle_client, args=(client_sock, client_addr), daemon=True)
            t.start()
    except KeyboardInterrupt:
        print("\n[serve-gyro] Server stopped.")
        sys.stdout.flush()
    finally:
        try:
            server_sock.close()
        except Exception:
            pass

if __name__ == '__main__':
    try:
        main()
    except BaseException as ex:
        if not isinstance(ex, (KeyboardInterrupt, SystemExit)):
            try:
                with open(err_log_path, "a", encoding="utf-8") as f:
                    f.write(f"\n[{time.strftime('%Y-%m-%d %H:%M:%S')}] FATAL: {type(ex).__name__}: {ex}\n")
                    traceback.print_exc(file=f)
            except Exception:
                pass
        raise
