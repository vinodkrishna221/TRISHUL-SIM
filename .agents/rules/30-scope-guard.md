# Scope Guard (always on)

The #1 hackathon killer is not bad code — it's a beautiful architecture that is 60% done at
judging time. These rules exist to make sure there is ALWAYS something to demo.

## The Walking Skeleton rule

By **30% of hackathon time** (hour 7 of 24 / hour 11 of 36 / hour 14 of 48), `main` must
contain a thin end-to-end slice of the core loop: real UI → real API → real store → back,
deployed or locally demoable with seeded data. Ugly is fine. Fake data is fine. Missing
features are fine. A skeleton that walks beats an organ collection.

If the skeleton isn't walking at the 30% mark → mandatory `/pivot` session. No exceptions,
no "just one more hour".

## Timeboxes

- Every task card carries a timebox. **The norm is 90–120 minutes; 4h is a cap, not a
  size** — a 4-hour card is usually two cards hiding. Nothing bigger exists; split it.
- At 1× timebox: report status honestly at next standup.
- At 2× timebox: STOP. Direction Check yourself: descope the task, take the ponytail-lazier
  path, or move it to the cut list. Sunk cost is not a demo feature.

## The Cut List

`board/STATUS.md` carries a **Cut List** section from hour one — features we WANT but will
cut without ceremony, pre-ranked. When time pressure hits, cutting is a lookup, not a debate.
Everything in the "polish" phase is born on the cut list.

## Demo-first arithmetic

Before starting any task, answer in one line: *"what does the judge see because this task
happened?"* No answer → the task goes to the cut list and you take the next one. Judges see:
the 3-minute demo, the pitch, the README/submission page. They do not see: your admin panel,
your migration system, your 94% test coverage, your microservices.

## Polish ordering (when core is done and time remains)

1. Demo-path error states (empty states, loading states on screens the demo shows)
2. Seed data that tells the story (realistic names, numbers judges recognize)
3. One wow moment (Oracle owns it: the 10 seconds people remember)
4. Landing/first screen polish (first impression = anchoring bias)
5. Everything else stays on the cut list. Refactors after judging.
