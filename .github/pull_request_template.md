## Mission

<!-- Mission + task card, e.g. M2 / FE-3 "Results feed" -->
Mission: `missions/________-mission.md` · Task: `___`

## What & Why (2 lines max)

-

## Contract Impact

- [ ] Touches **no** contract in `board/contracts/`
- [ ] Changes a contract → change proposed in `board/DECISIONS.md` and consumers pinged

## Proof It Works

<!-- Command output, screenshot, or curl — the ONE check that fails if this breaks -->

## Warden Checklist (reviewer fills — fresh session only)

- [ ] Matches its task card's deliverable + test
- [ ] No ponytail-ladder violations (unrequested abstraction, dead config, new dep for a one-liner)
- [ ] Contracts honored; types match `docs/SCHEMA.md`
- [ ] No secrets, no `.env` committed
- [ ] Demo path still green (`/standup` smoke or manual click-through)
