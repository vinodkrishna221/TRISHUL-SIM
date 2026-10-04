# Stack Pack — Next.js / React / TypeScript (load on FE track)

Defaults assume **Next.js 15+ App Router, React 19, TS strict, Tailwind v4**.

## Hackathon defaults (decided — don't relitigate)

- **App Router, Server Components by default.** `"use client"` only for interactivity
  (forms, live UI, animation). Data fetching happens in server components or route
  handlers — never `useEffect`+fetch waterfalls.
- **Server Actions for mutations** (`"use server"`) before building API routes the frontend
  alone consumes. API routes exist for: webhooks, other clients, contract-shared endpoints.
- **UI kit: shadcn/ui + Tailwind.** Copy-in components, no heavyweight design systems.
  `npx shadcn@latest add button card dialog …` beats hand-rolling accessible primitives.
- **State:** URL params + server state first, `useState` second, Zustand only if genuinely
  global+client (e.g., live call UI). No Redux at a hackathon. Ever.
- **Forms:** simple `<form action={serverAction}>` + native validation; react-hook-form+zod
  only when a form is genuinely complex (multi-step).
- **Loading/error UX:** every demo-path route gets `loading.tsx` (skeleton) and an empty
  state. Judges WILL see your loading states on venue Wi-Fi.
- **Fonts/icons:** `next/font/google` (one display + one body max), `lucide-react`.
- **Animation:** CSS transitions first; framer-motion only on the wow moment, never sprinkled.
- **Images:** `next/image` with remote patterns configured immediately (this breaks demos late).

## Traps that kill demos

- Hydration mismatch from `Date.now()`/`Math.random()`/locale in server-rendered JSX → compute
  client-side in effect or pass from server as prop.
- `use client` on a page importing a server-only lib (mongo driver!) → keep data access in
  server components / actions / route handlers only.
- Env vars in browser need `NEXT_PUBLIC_` prefix; everything else stays server-side.
- Vercel deploy: dynamic APIs on Node runtime; check `next build` locally BEFORE the final hour.
- **Version hell (the #1 agent time-burn):** agents write last-major syntax with total
  confidence (pages-router patterns, React 18 idioms, Tailwind v3 config against v4).
  Pin majors at kickoff, commit the lockfile with the skeleton, and apply the 20-minute
  dependency-fight law — pin/downgrade and move, never spend an hour arguing with a codemod.

## Structure

```
app/(marketing)/page.tsx      # landing
app/(app)/…                   # the product; route groups keep layouts clean
components/ui/                # shadcn
components/<feature>/         # feature components, co-located
lib/ (db.ts, flags.ts, types.ts from contracts)
```
