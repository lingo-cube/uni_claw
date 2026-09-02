# Design — observability-evidence-anchors

## Context

Post-discussion decision (B): carry frame/evidence references via the existing span-attribute channel instead of a schema change. The recorder already persists arbitrary tags; only the two frame-identity boundaries emit anchors.

## Goals / Non-Goals

Goals: observe/execute boundaries emit seq/frame/action tags; execution-tree surfaces anchors and joins AssetRefs by observation sequence; view models pass through.

Non-Goals: TraceRun schema v1 change; inference of frame ownership for spans without anchors (INFERRED, deferred); embedding asset bodies; any authority change.

## Decisions

### D1 — Tag channel, minimal emission surface
**Decision:** only `ObserveAsync` (seq/frame) and `ExecuteAsync` (action.kind) emit anchors; everything else stays clean. The frame token equals the observation sources' FrameReference (`capture:{seq}`) so the tag is a stored fact, not invented.
**Why:** minimal behavioral surface; schema untouched; recorder persists tags already.

### D2 — Asset join by observation sequence
**Decision:** execution-tree joins span.observation.seq → bundle AssetRefs of the same observationSeq (sorted). Empty when no match; never inferred in this slice.
**Why:** convergent key (spans via tags; assets via FrameId→records join) without inventing frame-token equality.

## Risks / Trade-offs

- [Tags are strings, no type safety, consistency by convention] → constrained to two emission points; review gate on new anchor sites.
- [No inference for unanchored spans yet] → deferred INFERRED window join; documented.

## Migration Plan

None — additive tags + read projections; existing traces read fine (absent tags ⇒ no anchors/empty join).

## Open Questions

None that would change the contract; INFERRED estimation and formal Ref fields await future schema v2 demand.
