---
name: ppt-builder
description: Generate prescribed-format hackathon decks (SIH idea PPT, evaluation-round decks, finale deck) from WarRoom docs — slide-by-slide content with speaker notes, maintained as markdown and exported to the required template. Use at internal-round submission, /prep, and every /eval-round.
---

# PPT Builder

Purpose: SIH-style events are deck-first — the idea PPT gates selection, and every
evaluation round opens with slides. Decks are DERIVED, never hand-written twice: WarRoom
docs are the source; this skill maps them to slides. Owner: Herald (+ Oracle for the
differentiation and scoring slides).

## Source → slide mapping (the whole trick)

| Slide (SIH idea-PPT format) | Source of truth |
|---|---|
| Title / team / PS id | manifest + TEAM.md |
| Problem (with the pain number) | PRD §1 (Scout's sourced number) |
| Proposed solution + novelty | IDEA-BRIEF one-liner + **unfair-advantage line** (the novelty criterion answer) |
| Technical approach (diagram) | ARCHITECTURE §3 mermaid → rendered image |
| Feasibility & viability | ARCHITECTURE §1 stack record + spike results (DECISIONS from /prep) |
| Impact & beneficiaries | PRD §2 user + Intel seat's numbers (district cost, beneficiary scale) |
| Offline / privacy / sustainability | SIH arena §3 answers (DPDP line, Bhashini/India-Stack hook, IVR path) |
| What's built vs roadmap (eval rounds) | STATUS tracks table + EVAL-PACK delta |
| "Unlike other approaches" (finale) | judge-lens Mode 4 rival simulation |

## Engines (pick per deck — the source→slide map feeds both)

**Engine A — bolt-slides (default when the format is FREE):** interactive web deck via
the bundled `bolt-slides` skill (stackblitz/bolt-slides, MIT). Scaffold once with
`./scripts/new-deck.sh` → author `deck/src/App.tsx` from the map above; present with
`npm run dev` from the demo laptop (presenter mode, grid view, annotations built in);
deployed link doubles as a submission artifact. Use for: internal-round pitch, eval-round
decks, finale deck, demo days. Load bolt-slides + this skill TOGETHER when authoring —
that one governs HOW slides are built, this one governs WHAT goes on them.

**Engine B — prescribed-template paste (when a format is MANDATED):** SIH idea-PPT and
any judged-format deck. Maintain `docs/deck/<round>.md` markdown (below) as the source,
paste into the official template. Formats are scored; never freestyle a prescribed deck,
and never submit a web link where a .pptx is demanded.

## Working format (Engine B, and the content source for Engine A)

- Maintain `docs/deck/<round>.md`: one `## Slide n — <title>` per slide, bullet content
  ≤5 lines, `NOTES:` speaker notes under each (the speaker rehearses from this file).
- Export: paste into the PRESCRIBED template (SIH publishes one — formats are scored;
  never freestyle the idea PPT). Options: manual paste (20 min, reliable), Marp/pandoc
  if the template allows, or an office-agent tool if available. The markdown is the
  source; the .pptx is a build artifact.
- Diagrams: mermaid → PNG once per major change (screenshot is fine), reused across decks.

## Deck discipline

- One deck lineage, patched per round (E1 → E2 adds delta+feedback slides; E3 adds
  differentiation + roadmap) — never three decks drifting apart.
- Numbers on slides come from STATUS (real) — a slide claiming what the demo can't show
  is a viva landmine; Warden's hygiene gate applies to decks before every round.
- 10-15 words max per bullet; the demo is the star, slides are the frame. Caveman NEVER
  writes slides (Herald's exemption holds — decks are committed prose).
