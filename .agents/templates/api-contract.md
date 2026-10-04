# Contract: <domain>                    version: 1

> OWNER: <track/mission> · CONSUMERS: <tracks/missions>
> Change ritual (60s, mandatory): edit → bump version + changelog line → DECISIONS entry →
> ping consumers → THEN code. Warden blocks PRs that drift from this file.

## Endpoints

### <METHOD> <path>
REQUEST
```json
{ "field": "type — constraint · example" }
```
RESPONSE 200
```json
{ }
```
ERRORS: `400 {code:"…", message}` · `404 …` <the shapes actually returned>
NOTES: auth <none/header> · pagination <…> · latency budget <if demo-relevant>

## Types (PRD nouns, verbatim — one example object each)

```ts
Lead { id: string; name: string; score: number /* 0-100 */; createdAt: string /* ISO */ }
```

## Events (realtime/webhooks — same rigor, or delete section)

`<event.name>` payload: `{ … }` · emitted when: <…> · consumers: <…>

## Changelog

- v1 <date>: initial
