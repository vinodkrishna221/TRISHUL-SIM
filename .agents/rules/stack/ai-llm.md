# Stack Pack — AI / LLM Features (load on AI track)

RAG, agents, voice, structured extraction. The judges have seen 50 thin GPT-wrappers —
this pack is about shipping AI that *demos* reliably and reads as engineered, not wrapped.

## Hackathon defaults (decided — don't relitigate)

- **Provider via one thin client module** (`lib/llm.ts` / `services/llm.py`): model name,
  temperature, retries (2, exponential), timeout (20s), and a **DEMO_FALLBACK** switch that
  returns curated cached responses when the provider melts during judging. Build the fallback
  hour one, not after the first 429.
- **Structured outputs always**: zod/pydantic schema + the provider's structured/JSON mode.
  Free-text parsing of LLM output is a self-inflicted outage.
- **Streaming on any response > 2s** — perceived speed IS the demo. (Vercel AI SDK
  `streamText`/`useChat` on Next; SSE via FastAPI `StreamingResponse` otherwise.)
- **RAG, minimum viable ladder** (climb only as needed):
  1. Stuff the docs in the prompt (≤ ~50 pages total? DONE — this wins hackathons)
  2. One embeddings table + cosine top-k (pgvector/Convex/Mongo Atlas vector — whatever the
     DATA track already runs; no new infra for vectors)
  3. Managed vector DB — almost never justified in 36h; Direction Check required
  Chunking: ~500 tokens, 15% overlap, by heading when the source has structure. Show
  citations in the UI — sources on screen read as rigor to judges.
- **Agentic flows:** max 2–3 tool-calling steps, each step visible in the UI (steps stream =
  wow moment). Full autonomous loops are demo russian-roulette — cap iterations, log every
  tool call to the transcript panel.
- **Voice** (judged loudly, budget accordingly):
  - Fastest credible: browser `SpeechRecognition` (input) + provider TTS (output)
  - Real product feel: a realtime voice API/pipeline — but ONLY if voice IS the product's core
  - Always pre-record one flawless interaction as backup video (venue mics + Wi-Fi are hostile)
- **Cost/keys:** per-provider spending caps ON; keys server-side only (never `NEXT_PUBLIC_`);
  one `[llm]`-prefixed log line per call (model, ms, tokens) — it doubles as demo telemetry.

## Traps that kill demos

- Prompt drift: keep prompts in `prompts/` as versioned files, not inline strings edited in 6 places.
- Nondeterministic demo: temperature 0–0.3 + seeded inputs for the scripted demo path.
- Rate limits at the venue (everyone hits the same provider): retries + fallback model wired
  from the start (e.g., flagship → mini of the same family).
- "Agent" that's a chatbot: if tools never visibly fire, Oracle will flag the wow moment as fake.
