# Git Protocol (always on when committing)

Main is the demo. **Main is always demoable.** Everything else is a branch.

## Branching

- One branch per mission task: `feat/<track>-<task-id>-<slug>` → `feat/fe-3-results-feed`
- Fixes: `fix/<track>-<slug>`. Never commit straight to `main` after the walking skeleton lands.
- Rebase on `main` before opening the PR (`git pull --rebase origin main`). Small conflicts
  every few hours beat one apocalyptic merge at hour 30.

## PRs

- Open a PR per task using the template. Small PRs: one task card = one PR.
- **Review = Warden persona in a FRESH agent session** (clean context sees what the builder
  session cannot). Solo mode: this is mandatory, not optional — you are your own reviewer's
  blind spot.
- Merge order follows the dependency map in `plans/` — integration tasks merge in
  dependency order (schema/contracts → backend → frontend → polish), enforced during `/integrate`.
- A red demo path blocks ALL merges until green. Fix forward or revert immediately;
  `git revert` beats archaeology under time pressure.

## Parallel agents — worktrees (solo mode & power users)

Run independent missions in parallel without file collisions:

```bash
git worktree add ../hack-be feat/be-2-scoring-api    # terminal 1: Claude Code / OpenCode
git worktree add ../hack-fe feat/fe-3-results-feed   # terminal 2: Antigravity
git worktree list                                     # sanity
git worktree remove ../hack-be                        # after merge
```

One agent session per worktree. Contracts in `board/contracts/` are the only shared surface;
if two missions need the same file, they were split wrong — fix the mission split, don't
fight the merge.

## Dependency ownership (parallel sessions)

Contracts prevent semantic collisions — not `package-lock.json` collisions. Per phase,
**ONE track owns dependency changes** (usually the track with the most installs planned).
Everyone else files a deps-request: one STATUS line (`DEPS-REQ: <pkg> for <card>, <track>`),
owner installs + pushes within 30 minutes. Lockfile conflicts are never hand-merged —
take main's lockfile and re-run the install. Two parallel worktrees both adding deps is a
40-minute conflict tax; this rule is cheaper.

## The kit is frozen during events (one sanctioned exception)

`.agents/` files are read-only from T-0 to the retro — a mid-event "rule improvement"
syncs into every teammate's agent instantly and un-tested. Confusing rule? Note it in
STATUS, work around it, fix it at `/retro`. (Board files, plans, missions, contracts are
the LIVE surface and change constantly — the freeze is the brain, not the battlefield.)

**The one exception: `/fit-audit`** — the structured self-modification window at
blueprint step 6 (and manually after a stack-changing pivot). It edits core kit files
ONLY through its ritual: necessity gate → human-approved diff → sync → one commit → the
freeze re-engages. Any `.agents/` change outside that door is still a violation.

## Sync rituals (remote team)

- Push at least every 45–60 min (small WIP commits are fine on your branch).
- After every merge to `main`: announce in team chat with the `/standup` one-liner.
- Start of any session: `git fetch && git log --oneline origin/main -10` + read `board/STATUS.md`.
