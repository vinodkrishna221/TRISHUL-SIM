# Stack Pack — FastAPI / Python (load on BE-PY track)

Defaults assume **Python 3.12+, FastAPI, pydantic v2, uv** for env/deps.

## Hackathon defaults (decided — don't relitigate)

- **uv for everything**: `uv init && uv add fastapi uvicorn` — seconds, not minutes.
  Run: `uv run uvicorn app.main:app --reload`.
- **pydantic v2 models mirror `board/contracts/`** — request/response models are THE
  contract. `model_config = ConfigDict(from_attributes=True)` when mapping DB objects.
- **Routers per domain** (`app/routers/leads.py` → `router = APIRouter(prefix="/leads")`),
  registered in `main.py`. No god-file after the skeleton phase.
- **Async by default**; sync only for CPU-bound or sync-only SDKs (then `run_in_threadpool`).
- **CORS immediately** (this is the #1 "frontend can't reach backend" hackathon bug):
  `CORSMiddleware` with the deployed frontend origin + localhost, configured hour one.
- **Settings:** `pydantic-settings` `BaseSettings` reading `.env` — no bare `os.environ` scattered.
- **Errors:** raise `HTTPException` with contract-shaped detail `{code, message}`; one
  global exception handler that logs loud and returns soft.
- **Background work:** `BackgroundTasks` for fire-and-forget (emails, logging). A real queue
  (arq/celery) is almost never hackathon-worthy — Direction Check if tempted.
- **The one check:** every router gets one `httpx.AsyncClient(app=…)` smoke test or an
  `assert`-based `__main__` check. `/health` endpoint from minute one (deploy platforms need it).

## Traps that kill demos

- Deployed URL is `https` + different origin → CORS + cookie flags checked on REAL deploy early.
- Pydantic v1-style code from stale LLM memory (`.dict()`, `@validator`) → v2 (`.model_dump()`,
  `@field_validator`). Same disease everywhere: pin majors at kickoff (`uv lock` committed
  with the skeleton) + the 20-minute dependency-fight law from core rules.
- Blocking call (requests, heavy loop) inside async route freezes every request → httpx/async
  or threadpool.
- Uvicorn workers on free tiers: 1 worker, keep state out of process memory (use the DB).

## Structure

```
app/main.py (app, CORS, health, routers)
app/routers/<domain>.py
app/models.py        # pydantic contracts
app/db.py            # client/session
app/services/<domain>.py  # logic, testable without HTTP
scripts/seed.py
```
