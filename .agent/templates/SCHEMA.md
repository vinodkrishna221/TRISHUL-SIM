# Schema — <PROJECT NAME>

> Store: <MongoDB Atlas | Convex> (DECISIONS #n) · PRD nouns verbatim · IDs cross the
> contract boundary as strings, dates as ISO-8601.

## <Entity>  (collection/table: `<name>`)

| Field | Type | Notes |
|---|---|---|
| _id / id | | |
| | | |

**Example document:**
```json
{ }
```
**Demo queries → index that serves each:**
- <list screen filters/sorts by X> → index: `{ X: 1, createdAt: -1 }`
**Embedded vs referenced:** <what's embedded here and why (shown together in the demo)>

<!-- repeat per entity — every entity carries its queries; a schema without queries is decoration -->

## Seed plan (`scripts/seed.*` — the most demo-critical file in the repo)

Hero records: <the named rows the pitch points at> · Volume: <30+ rows where lists show> ·
Edge rows: <empty/long/weird for state testing> · Idempotent: re-run = same story.

## Change ritual

Schema change = contract change: bump here, DECISIONS entry, reseed, ping consumers.
Freeze schema at 60% time; defend it after.
