---
name: brand-sprint
description: The 30-minute name-and-skin sprint — project name, one-liner, logo/favicon, OG image, applied everywhere. Makes the build feel finished before a word is spoken. Run after /kickoff once vocabulary freezes, or in the first polish slot.
---

# Brand Sprint

Field data from winners: a crisp name, logo, and OG image — 30 minutes with AI tooling —
moves judges more than an architecture slide. "Feels finished" is a scoring multiplier on
every rubric line. Owner: Herald (Maverick riffs names). Timebox: 30 minutes, HARD —
this is a sprint, not an identity workshop.

## Minute 0–10 — The name

Generate 15 candidates, kill to 1. Criteria, in order:
- ≤3 syllables, spellable on first hearing, chantable in a pitch ("we built ___")
- Says or evokes the core verb/user (GramSevak > GenericAI; a pun that EXPLAINS beats
  a pun that's clever)
- 10-second collision check: not the name of a known product in the same space, not
  taken by another team on the event's submission page today
- The one-liner attaches naturally: "<Name> — <does what> for <whom>"
Decide with the team lead in one message. Renaming later costs an hour of find-replace
across code, deck, and pitch — the PRD vocabulary freeze applies to the name too.

## Minute 10–25 — The skin

- **Logo/favicon:** one strong mark via AI image gen (flat, single-concept, legible at
  32px) or a styled initial. No mascots-with-gradients-and-particles.
- **OG/social image:** name + one-liner + the mark on the brand color. This is what the
  submission link unfurls as — judges skim submission lists; the unfurl is a first
  impression you control.
- **One accent color** — into the product theme (Tailwind token / tokens.css), the deck
  theme, and the README badge. One. Consistency reads as credibility.

## Minute 25–30 — Apply everywhere (the checklist)

- [ ] `index.html` — real `<title>` + favicon (both product and deck; no template
      placeholders left)
- [ ] Deck cover + README hero (name, one-liner, mark)
- [ ] Submission page draft title · STATUS.md header · demo script's spine sentence
- [ ] The team says the one-liner out loud once — if anyone stumbles, simplify it now

Ship it and stop. The brand's job is to make the demo feel inevitable — the demo still
does the winning.
