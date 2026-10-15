# ============================================================
# serve-gyro.ps1 — Serve vr-gyro-sender.html to phone browser
# TRISHUL-SIM · VG-P1-D · vr-gamepad track (Sohail)
#
# Usage:  powershell -ExecutionPolicy Bypass -File docs/serve-gyro.ps1
#         Then on phone (connected to laptop hotspot):
#         Chrome → http://LAPTOP_IP:8080
# ============================================================

$ErrorActionPreference = "Stop"

# ── Resolve laptop IP ──────────────────────────────────────
function Get-LaptopIp {
    # Try to find the WiFi or hotspot adapter IP
    $candidates = Get-NetIPAddress -AddressFamily IPv4 |
        Where-Object {
            $_.IPAddress -ne "127.0.0.1" -and
            $_.PrefixOrigin -ne "WellKnown" -and
            $_.InterfaceAlias -notmatch "Loopback"
        } |
        Sort-Object -Property InterfaceMetric |
        Select-Object -ExpandProperty IPAddress

    if ($candidates -and $candidates.Count -gt 0) {
        # Prefer 192.168.x.x (hotspot range)
        foreach ($ip in $candidates) {
            if ($ip -match "^192\.168\.") { return $ip }
        }
        # Fallback to first non-loopback
        if ($candidates -is [array]) { return $candidates[0] }
        return $candidates
    }

    # Last resort: parse ipconfig
    $ipconfig = ipconfig | Select-String "IPv4 Address" | ForEach-Object {
        ($_ -split ":\s*")[1].Trim()
    } | Where-Object { $_ -ne "127.0.0.1" }
    if ($ipconfig) {
        if ($ipconfig -is [array]) { return $ipconfig[0] }
        return $ipconfig
    }

    return "127.0.0.1"
}

$laptopIp = Get-LaptopIp
$port = 8080
$prefix = "http://+:$port/"

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host " TRISHUL-SIM · Gyro Sender Server" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host " Laptop IP:  $laptopIp" -ForegroundColor Green
Write-Host " Serving on: http://${laptopIp}:${port}/" -ForegroundColor Green
Write-Host ""
Write-Host " On phone (connected to laptop hotspot):" -ForegroundColor Yellow
Write-Host "   Chrome -> http://${laptopIp}:${port}/" -ForegroundColor Yellow
Write-Host ""
Write-Host " Press Ctrl+C to stop." -ForegroundColor DarkGray
Write-Host ""

# ── Read HTML template ─────────────────────────────────────
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$htmlPath  = Join-Path $scriptDir "vr-gyro-sender.html"

if (-not (Test-Path $htmlPath)) {
    Write-Error "ERROR: Cannot find vr-gyro-sender.html at $htmlPath"
    exit 1
}

$htmlTemplate = [System.IO.File]::ReadAllText($htmlPath, [System.Text.Encoding]::UTF8)

# Replace LAPTOP_IP placeholder with actual IP
$htmlContent = $htmlTemplate -replace "LAPTOP_IP", $laptopIp
$htmlBytes   = [System.Text.Encoding]::UTF8.GetBytes($htmlContent)

# ── Start HttpListener ─────────────────────────────────────
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add($prefix)

try {
    $listener.Start()
} catch {
    Write-Host ""
    Write-Host "ERROR: Could not bind to port $port." -ForegroundColor Red
    Write-Host "Try running PowerShell as Administrator, or check if port $port is in use." -ForegroundColor Red
    Write-Host ""
    Write-Host "To allow non-admin binding, run (once, as Admin):" -ForegroundColor Yellow
    Write-Host "  netsh http add urlacl url=http://+:$port/ user=Everyone" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host "[serve-gyro] Listening... waiting for phone connection." -ForegroundColor Green

# ── Serve loop ─────────────────────────────────────────────
try {
    while ($listener.IsListening) {
        $context  = $listener.GetContext()
        $request  = $context.Request
        $response = $context.Response

        $clientIp = $request.RemoteEndPoint.Address.ToString()
        $url      = $request.Url.LocalPath
        $ts       = Get-Date -Format "HH:mm:ss"

        Write-Host "[$ts] $($request.HttpMethod) $url from $clientIp" -ForegroundColor DarkGray

        # Serve the HTML for any GET request
        $response.ContentType     = "text/html; charset=utf-8"
        $response.ContentLength64 = $htmlBytes.Length
        $response.StatusCode      = 200

        # CORS headers (phone may need these)
        $response.Headers.Add("Access-Control-Allow-Origin", "*")

        $response.OutputStream.Write($htmlBytes, 0, $htmlBytes.Length)
        $response.OutputStream.Close()
    }
} finally {
    $listener.Stop()
    $listener.Close()
    Write-Host "[serve-gyro] Server stopped." -ForegroundColor Yellow
}
