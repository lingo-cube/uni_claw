# Design — runtime-debug-p2d-execution-tree

## Context

The bundle adapter (post-graduation-conformance-repair) is now strict-Harness-faithful (camelCase, manifest/records identity, byte-verified artifacts). P2b/P2c cover the causal/packet side; the execution tree needed the trace file — now read by this change from the same validated bundle.

## Goals / Non-Goals

Goals: EXECUTION tree projection; structural absolute pruning; filter pruning with spine; fail-closed trace validation; projection-only guarantee.

Non-Goals: trace mutation; span-level timing math; cross-bundle execution compare (P2a covers bundle records); Event child nodes (TraceRun currently stores events inside spans; exposing them is a later projection).

## Decisions

### D1 — Structural hides are absolute, filters keep the spine
**Decision:** layer/component/name hides cut the node and its entire subtree (absolute exclusions); only-errors and time-window are filters whose hidden nodes are re-kept only when they are ancestors of a surviving node (causal spine preserved). Filter-hidden leaves that are nobody's ancestor stay excluded.
**Why:** matches the gate's "隐藏≠删除" (cut) plus "因果脊柱保留" (FDP view) semantics without collapsing unrelated branches.

### D2 — Trace is a validated bundle member, not an ambient file
**Decision:** `observability-trace.json` is read through the same fail-closed reader as every other bundle file (regular file, no symlink, schema-validated); `CaptureBundle.trace` is the single projection entry.
**Why:** one bundle model, one trust boundary; no secondary IO surface.

## Risks / Trade-offs

- [Event-level children not yet projected] → documented; spans carry events in TraceRun, exposing them is a follow-up projection without schema change.

## Migration Plan

None — additive command; strict reader changes already landed (parallel conformance-repair work).

## Open Questions

None that would change the contract.
