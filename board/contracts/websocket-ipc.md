# Contract: websocket-ipc
> version: 1 · 2026-10-04
> OWNER: unity-sim
> CONSUMERS: swarm-ai · aar-engine · ui-ux

Wire: `ws://127.0.0.1:9700` (plain WebSocket, no TLS — localhost only, air-gapped)
Framing: every message is a JSON string. No binary frames. Max message size: 64 KB.
Encoding: UTF-8.

All messages share a common envelope:

```
Envelope {
  type:      string    // message type constant — see registry below
  v:         number    // protocol version, currently 1
  ts:        number    // sender wall-clock Unix ms (for latency diagnostics)
  payload:   object    // message-specific — see each section
}
```

**Example envelope:**
```json
{ "type": "SCENARIO_START", "v": 1, "ts": 1759600021000, "payload": { ... } }
```

---

## Message registry

| type constant | Direction | Defined in contract |
|---|---|---|
| `SCENARIO_START` | Server → Client | scenario-events.md |
| `SCENARIO_TICK` | Server → Client | scenario-events.md |
| `SCENARIO_END` | Server → Client | scenario-events.md |
| `SCORECARD_PAYLOAD` | Client → Server → both | scorecard.md |
| `CLIENT_READY` | Client → Server | this file §1 |
| `CLIENT_HEARTBEAT` | Client → Server | this file §1 |
| `SERVER_HEARTBEAT_ACK` | Server → Client | this file §1 |
| `ERROR` | either direction | this file §2 |

---

## §1 · Connection lifecycle

### CLIENT_READY  (Client → Server, on connect)
```
payload {
  clientRole:   string   // "TRAINEE_COCKPIT"
  buildVersion: string   // "0.1.0"
}
```
**Example:**
```json
{ "type": "CLIENT_READY", "v": 1, "ts": 1759600010000, "payload": { "clientRole": "TRAINEE_COCKPIT", "buildVersion": "0.1.0" } }
```

Server responds with `SERVER_HEARTBEAT_ACK` immediately. The green "LAN link confirmed" icon in C2 UI fires on receipt.

### CLIENT_HEARTBEAT  (Client → Server, every 5 s)
```
payload {}
```

### SERVER_HEARTBEAT_ACK  (Server → Client)
```
payload {
  serverTimeMs: number   // server Unix ms at send — client uses to detect clock skew
}
```
**Example:**
```json
{ "type": "SERVER_HEARTBEAT_ACK", "v": 1, "ts": 1759600015000, "payload": { "serverTimeMs": 1759600015000 } }
```

**Timeout rule:** Client shows ⚠️ "Link lost" if no `SERVER_HEARTBEAT_ACK` for 15 s; does NOT abort scenario — trainee continues in degraded mode.

---

## §2 · Error shape

```
ERROR payload {
  code:    string   // machine-readable — see codes below
  message: string   // human-readable, English
  fatal:   boolean  // true = scenario must abort; false = warning only
}
```

| code | fatal | meaning |
|---|---|---|
| `SCENARIO_NOT_FOUND` | true | scenarioId in SCENARIO_START not present on client |
| `VERSION_MISMATCH` | true | client `v` != server `v` |
| `PAYLOAD_INVALID` | false | malformed JSON or missing required field |
| `SCENARIO_ALREADY_RUNNING` | false | second SCENARIO_START received mid-run (ignored) |

**Example:**
```json
{ "type": "ERROR", "v": 1, "ts": 1759600099000, "payload": { "code": "VERSION_MISMATCH", "message": "Server is v1, client sent v2 — update client binary.", "fatal": true } }
```

---

## Latency budget

| Message | Budget |
|---|---|
| `SCENARIO_START` delivery | ≤ 200 ms after Instructor triggers |
| `SCENARIO_TICK` delivery | ≤ 50 ms (localhost; budget is for parse + render) |
| `SCORECARD_PAYLOAD` delivery | ≤ 500 ms after AAR Engine writes Scorecard |

---

## Changelog
- v1 · 2026-10-04 · initial — Orion
