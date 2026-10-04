# Decisions — TRISHUL-SIM

> ADR-lite: 3 lines per entry, 30 seconds to write, saves the 2am "why is it like this".
> Log: irreversible choices, contract/schema changes, pivots, Direction Checks overruled.

---

### 1 · Idea choice + Scale Gate                     2026-10-04 · by team (/kickoff)
CHOSE: TRISHUL-SIM — physics-grounded C-UAS tactical simulation trainer + AAR engine; FULL mode PRD.
BECAUSE: SIH26247 MoD problem statement; 16 days to submission; 4–6 builders warranting full 3-page PRD and phase plans.
AFFECTS: All tracks — vocabulary frozen (Scenario, Sensor Suite, Swarm, AAR Engine, Scorecard, Trainee, Instructor, Kill Chain).

### 2 · Primary demo user                            2026-10-04 · by Orion (/kickoff)
CHOSE: Havildar Rajan (Jawan/Sentry) as the ONE judge-facing user; Instructor (JCO) is supporting infrastructure.
BECAUSE: Emotional demo arc requires a single underdog succeeding — not a commander orchestrating.
AFFECTS: Demo Narrative beat order, UI copy tone, Herald pitch spine sentence.

### 3 · Mandatory tech stack                         2026-10-04 · by team (/kickoff)
CHOSE: Unity 2022/6 LTS URP + Windows .exe + localhost WebSocket IPC; XR plugin scaffolded for future VR.
BECAUSE: SIH26247 constraint (Unity mandatory), MoD air-gap requirement (no cloud), team capability.
AFFECTS: All tracks — Unreal Engine banned; no cloud API calls permitted at any layer.
