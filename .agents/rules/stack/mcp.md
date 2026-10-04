# Stack Pack — MCP (Model Context Protocol) (load when building or consuming MCP)

Two distinct uses at a hackathon — be explicit about which one you're doing:

## A) CONSUMING MCP servers (your agents get superpowers)

- Canonical server list lives in `.agents/mcp/servers.json`; `scripts/sync.sh` mirrors it to
  `.mcp.json` (Claude Code), `opencode.json` (OpenCode), and Antigravity's MCP config.
- Hackathon-useful servers: GitHub (issues/PRs from chat), Convex (DB introspection while
  coding), browser/playwright (E2E smoke via agent), filesystem extras. Add per project —
  every server you add eats context; 3–5 good ones beat 12.
- Secrets for servers go in env, referenced from config — never hardcoded in the JSON.

## B) BUILDING an MCP server (it IS the product — judges love this in 2026)

- **TS:** `@modelcontextprotocol/sdk` + `McpServer` + zod schemas per tool. **Py:** `FastMCP`
  (`mcp` package) — decorator per tool, docstrings become tool descriptions (write them for
  the MODEL, not for humans: when to use, args, failure modes).
- Transport: **stdio** for local/demo (zero infra), Streamable HTTP only if a hosted judge
  demo truly needs it.
- Tools are the contract: name + schema + description reviewed by Warden like any API.
  3 excellent tools > 10 vague ones — models fumble ambiguous toolsets, live, on stage.
- Demo pattern that lands: split screen — left, a normal client (Claude/tool) talking; right,
  your system visibly reacting (DB rows appearing, dashboard updating). MCP demos die when
  the effect is invisible.
- Test harness: `npx @modelcontextprotocol/inspector` (or a 20-line stdio client script) in
  `scripts/` — never debug an MCP server through a full agent conversation.

## Traps that kill demos

- stdio servers must NEVER print to stdout (breaks the protocol) — log to stderr.
- Tool descriptions that assume context the model lacks ("the current project") — make args explicit.
- Version-pin the SDK; the ecosystem still moves fast in 2026.
