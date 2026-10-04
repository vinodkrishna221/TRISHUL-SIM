# TRISHUL-SIM Teammate Agent Prompt

> Use this at the start of every new Antigravity, Claude Code or OpenCode card session.
> Replace only `MEMBER_NAME` and `CARD_ID`. Do not paste prior chat history.

## Copy-paste prompt

```text
I am MEMBER_NAME. I am working on TRISHUL-SIM card CARD_ID.

Treat the repository as the source of truth. Ignore assumptions from previous chat sessions.

Before editing anything, do this preflight:

1. Read AGENTS.md and START-HERE.md.
2. Read board/STATUS.md, board/TEAM.md and board/DECISIONS.md.
3. Read missions/MISSION-MAP.md.
4. Locate the mission file whose heading exactly names MEMBER_NAME and read it completely.
5. Locate CARD_ID inside that mission and its matching plans/*-phase-plan.md file.
6. Read docs/PRD.md, docs/ARCHITECTURE.md and docs/SCHEMA.md only as needed by the card.
7. Load every stack rule, skill and board/contracts file named by the card or mission.
8. Inspect git status and the current branch without modifying files.

Identity and assignment gates:

- Confirm MEMBER_NAME exists in board/TEAM.md and the mission heading.
- Confirm CARD_ID appears in that member’s mission.
- Confirm the mission owns every file surface the card will touch.
- If the name/card pairing is wrong, STOP. Report the correct owner and request a formal reassignment through Vinod + board/DECISIONS.md.
- If the card is already complete, STOP and report the next assigned dependency-safe card.
- If a dependency is not merged, use the mission’s written mock path or move to the next assigned dependency-safe card. Never wait silently.

Report this preflight block before implementation:

MEMBER:
MISSION:
CARD:
PERSONA TO ADOPT:
CARD STATUS:
DEPENDENCIES AND THEIR STATE:
MOCK PATH IF A DEPENDENCY IS NOT LIVE:
OWNED FILES I WILL TOUCH:
FILES I MUST NOT TOUCH:
CONTRACTS OWNED / CONSUMED:
BRANCH REQUIRED BY THE MISSION:
DELIVERABLE:
EXACT TEST COMMAND:
DEMO RESULT THIS CARD ENABLES:
RISKS OR CONTRADICTIONS FOUND:

If and only if every gate passes, proceed with CARD_ID:

1. Create or switch to the exact branch written in the mission. One card per branch.
2. Implement only the card’s DELIVERABLE. Put extra ideas in the parking lot; do not expand scope.
3. Do not change a contract silently. Contract change ritual: edit contract → bump version/changelog → board/DECISIONS.md entry → Discord ping to consumers → then code.
4. Run the card’s exact TEST. Do not mark complete when the test is red or unrun.
5. Check git diff for files outside the mission boundary; revert accidental cross-track edits.
6. Update the card/mission checkbox and append one factual line to board/STATUS.md only after the TEST passes.
7. Commit using the card ID and prepare one PR. Do not merge it yourself.
8. Request a fresh-session Warden review. The reviewing session must not be the implementation session.

Blocker protocol:

- Blocked for more than 20 minutes: add the blocker to board/STATUS.md, post it in Discord, ping Vinod on WhatsApp only if critical, then switch to the next dependency-safe assigned card.
- Never take another member’s card without a formal reassignment in board/DECISIONS.md and both mission files.

Finish with this handoff:

CARD RESULT: PASS | BLOCKED
BRANCH:
FILES CHANGED:
TEST COMMAND + RESULT:
CONTRACT VERSION CHANGES:
STATUS/MISSION UPDATED: yes | no
PR READY: yes | no
WARDEN REVIEW REQUEST:
NEXT SAFE CARD:
BLOCKERS:
```

## Example — Venu

Replace the first line with:

```text
I am Venu. I am working on TRISHUL-SIM card SA-P0-A.
```

## Example — Rajeswari

```text
I am Rajeswari. I am working on TRISHUL-SIM card UX-P1-D.
```

## Fresh Warden review prompt

Run this in a new agent session after the implementation session finishes:

```text
Act as Warden for TRISHUL-SIM card CARD_ID implemented by MEMBER_NAME.

Read AGENTS.md, board/STATUS.md, the member’s mission, the complete CARD_ID block in the phase plan, every referenced contract/rule and the git diff for this card branch.

Review only; do not trust the implementation chat. Verify:
- file ownership and no cross-track edits
- deliverable completeness
- exact TEST command and result
- contract/schema consistency
- unity-sim/uitk runtime rules where relevant
- fallback/demo behavior
- scope discipline and no hidden roadmap claims

Return one verdict: MERGE or BLOCK.
If BLOCK, provide the smallest ordered fix list and identify which test must turn green.
Do not merge the PR yourself.
```
