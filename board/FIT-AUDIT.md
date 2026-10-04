# FIT AUDIT — TRISHUL-SIM
> Blueprint step 6 · 2026-10-04 · 30-minute timebox · Auditor: Orion

---

## 1 · Capability inventory (from §1 stack record)

| Capability needed | Kit asset | Status |
|---|---|---|
| Unity C# simulation (URP, Windows .exe) | None in kit | **Gap — expected; no kit rule needed** |
| WebSocket IPC (localhost, System.Net) | None in kit | Gap — standard .NET library, no external rule needed |
| Swarm AI (Boids + HBT, Burst/Job System) | None in kit | Gap — Unity-specific, no kit rule needed |
| Physics sensor model (Friis/Doppler/BPF in C#) | None in kit | Gap — pure math, no kit rule needed |
| NDJSON file I/O (AAR logging) | None in kit | Gap — stdlib, no kit rule needed |
| iTextSharp PDF export | None in kit | Gap — single licensed package, no kit rule needed |
| Unity UI Toolkit + Localisation package | None in kit | Gap — Unity-native, no kit rule needed |
| Git/PR workflow | `rules/10-git-protocol.md` | ✅ covered |
| Scope / cut-list discipline | `rules/30-scope-guard.md` | ✅ covered |
| Advisor / direction-check | `rules/20-advisor-protocol.md` | ✅ covered |
| Phase planning | `skills/phase-planner` | ✅ covered |
| Contract discipline | `skills/api-contract` | ✅ covered |
| Demo-first / skeleton discipline | `rules/00-warroom-core.md` | ✅ covered |
| Endgame freeze doctrine | `rules/40-endgame.md` | ✅ covered |

---

## 2 · Coverage map vs kit assets

**Explicitly NOT needed (justify to prevent scope creep):**

| Capability | Why not needed |
|---|---|
| Convex / MongoDB rules pack | Air-gapped; no DB server; NDJSON on local disk |
| Node/Express stack pack | No separate backend service; Unity IPC server is C# |
| React/Next.js rules | All UI is Unity UI Toolkit; no web frontend |
| Python/FastAPI rules | No Python sidecar; sensor physics is C# |
| Cloud deploy runbook | Submission is a Windows .exe; no cloud |
| ML-Agents / AI stack | Swarm AI is deterministic Boids + HBT; no ML |

---

## 3 · Necessity gate

**Must add to kit?** → **NO.** All gaps are Unity/C#-specific and either covered by the stdlib or well-understood Unity packages. Adding a Unity rules pack would be the only candidate — but at a 16-day hackathon with a known stack, the team's existing knowledge is the rule. A Unity stack pack with 3 lines of guidance is the maximum justified addition.

**Verdict: CLEAN BILL OF HEALTH.**

One optional micro-addition: a 3-line Unity/C# guideline note in `rules/stack/unity-cs.md` — capturing the project's key Unity conventions (Burst-safe structs, UITK-only UI, no Reflection in IL2CPP path). This is informational, not blocking.

---

## 4 · Diff presented to human

```diff
+ rules/stack/unity-cs.md   (NEW — 3 lines, approved below)
  All other kit files: NO CHANGE
```

**unity-cs.md content (proposed):**
```
# Stack pack: Unity C# (TRISHUL-SIM)
- UI: Unity UI Toolkit (UITK) only — no uGUI, no third-party widget library
- Burst/Job: all per-agent math structs must be unmanaged (IComponentData or plain struct) — no class references inside Job structs
- IL2CPP: zero use of System.Reflection at runtime — use explicit type registration or source generators
```

**Human approval required:** ✅ (leave this line as-is to approve; cross out to reject)

---

## 5 · Post-audit action

- If approved: write `rules/stack/unity-cs.md`, sync, commit as part of the blueprint PR.
- Phase plans reference this file for Unity coding standards.

---

## Audit result: CLEAN ✅ (one micro-addition pending human approval)
