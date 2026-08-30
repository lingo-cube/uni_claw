# Design — runtime-debug-p1b-causal-diff-projection

## Context

P1a delivered a fail-closed P0 packet reader and `summarize` / `occurrence` projections with a five-fixture test baseline. The packet already stores everything P1b projects: `EvidenceChain` (dict ordered by stage), `GoodComparison` / `BadComparison` (status/label/summary/axes/evidenceRefs), `LastGood` / `FirstBad` (status/stage/summary/evidenceRefs). No new data source is introduced; the Query Core consumes the same `EvidencePacket` model.

## Goals / Non-Goals

Goals: causal/evidence tree projection (prune-only + decision/evidence filters), evidence-chain query, packet-scoped differential view. Deterministic, read-only, closed statuses; thin CLI.

Non-Goals: trace spans (EXECUTION tree) projection — requires span data not present in packets (future source adapter); FDP/Owner/Disposition computation; multi-run `run compare`; replay/minimization; any Runtime/wire/Trace change.

## Decisions

### D1 — Chain is a dict ordered by stage, projected as the causal tree
**Decision:** `EvidenceChain` is a dict whose insertion order is the chain order; the causal tree projects each stage entry in that order with the closed vocabulary. The dict order is the contract (deterministic); no re-sorting by status.
**Alternatives:** order by stage name — rejected: loses the causal direction encoded by packet writers.

### D2 — Prune hides, never deletes; filters are closed flags
**Decision:** `--prune` takes a comma list validated against the closed stage vocabulary; unknown stage names are INVALID_INPUT diagnostics (not silent). `--only-decisions` / `--only-evidence` are boolean filters combined with prune.
**Why:** the "隐藏 != 删除" gate rule is enforceable at the tool boundary only if the projection reports what it pruned (included in result), which the tests assert against byte-immutable inputs.

### D3 — Differential projection is stored-facts-only
**Decision:** `diff` projects stored `Good/BadComparison` + `LastGood/FirstBad` verbatim; it never recomputes any semantic conclusion. Missing comparison facts fail closed with `INSUFFICIENT_TRACE_COVERAGE`.
**Why:** matches the Diagnostic-Engine boundary (Agent judges on top of Debug IR); the tool only makes stored facts deterministically visible.

## Risks / Trade-offs

- [Chain stages could grow beyond the closed vocabulary] → unknown stages still project (forward-compatible), while prune validation only accepts the closed set; documented.
- [Diff vs summarize overlap on LastGood/FirstBad] → summarize deliberately excludes them (P1a contract); diff is the dedicated surface, keeping command scope clean.

## Migration Plan

None — additive commands in the existing package; no schema/wire/Runtime change.

## Open Questions

None that would change the contracts; execution-tree projection and multi-run compare await dedicated source adapters in later slices.