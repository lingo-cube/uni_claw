# Design — runtime-debug-p4a-replay-facts

## Context

Foundation §13 reserves replay extract/replay/minimize. The bundle adapter already validates records/artifacts/trace; P4a reuses those stored facts to produce the fixture (failure evidence → replay fixture) and validate it. Minimization needs an execution engine — out of scope here.

## Goals / Non-Goals

Goals: fixture extraction + validation + summary; deterministic; fail-closed.

Non-Goals: executing replay (needs an environment/replay driver), minimization (minimal falsifier → RED → repair → GREEN), any mutation.

## Decisions

### D1 — Fixture is facts-only, extractable from the same bundle model
**Decision:** steps are the validated records in order; assets are AssetRefs; trace is a count+id summary; digest follows the P0 sorted-lines convention. No inferred state (target states are echoed only when stored).
**Why:** same trust boundary and identity discipline as every other projection.

### D2 — Minimization stays a contract, not code
**Decision:** this slice implements extract + validate only; `runtime-debug minimize` remains reserved, documented in the proposal.
**Why:** "不要为了赶进度一次实现全部" (Foundation §13).

## Risks / Trade-offs

- [Fixture cannot drive an actual replay engine yet] → documented; the fixture is the input contract for P4b.
- [scope.applicationIdentity/semanticRoot null] → stored facts only; later producer-labelled slices fill them.

## Migration Plan

None — additive commands; harness replay types untouched (fixture schema is tool-side).

## Open Questions

None that would change the contract; the replay execution engine and minimizer are P4b+ gates.
