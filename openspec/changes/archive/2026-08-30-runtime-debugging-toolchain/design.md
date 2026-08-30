# Design — runtime-debugging-toolchain (umbrella, contracts only)

## Context

See proposal.md — Why. Current state (evidence):

- Frozen P0 work-contract: `docs/analysis/runtime-debugging-capability-p0-contract.md` + canonical files in `.ai/skills/evidence-driven-debugging/references/runtime/` (debug-ir-schema, evidence-packet, trace-analysis, differential-analysis, tooling-contract, acceptance-examples + machine schemas). These are NON-AUTHORITATIVE input formats — this change references them, never duplicates them.
- Existing assets to reuse: spans (16 components) + DecisionRecord journal (≈100 sites) + RuntimeEventEnvelope store + Timeline read model (`GetRunTimeline`) + capture bundle/artifacts (sha256-checked) + replay/ScenarioCatalog + casebook (6 cases) + validation harness tiers.
- Parallel implementation slice `runtime-debug-p1a-summarize-occurrence` already gated: `runtime-debug` CLI with `summarize` + `occurrence`, Python stdlib-only `tools/runtime_debug/`, P0 five fixtures for contract tests.
- Governed exclusions (from the Foundation gate): no Generic Trace architecture refactor, no Runtime authority/execution-semantics change, no Phase-2.6 traversal resume.

## Goals / Non-Goals

Goals: freeze the unified Data Model + Ref family + AssetRef, the Query Core contract, the CLI/TUI single-core contract, the Analysis contract and Skill routing — as OpenSpec capability specs for the toolchain. Everything is read-only, deterministic, non-authoritative, prune-only.

Non-Goals: any implementation beyond contracts (P1a lives in its own change); TUI framework choice; Replay/minimization execution; trace/identity changes; runtime behavior changes; OTLP/sampling/Links (trace model untouched).

## Decisions

### D1 — Umbrella change vs per-slice changes
**Decision:** one long-lived umbrella change (`runtime-debugging-toolchain`) freezes the four capability contracts; implementation slices (P1a, P2, …) are separate changes with their own gates, referencing this umbrella's specs.
**Alternatives:** fold everything into P1a — rejected: interleaves contract and implementation gates; long-lived capabilities need a stable contract home. One mega-change P0–P5 — rejected: violates the "not all at once" mandate.

### D2 — AssetRef as first-class, bodies excluded
**Decision:** AssetRef schema lives in the data-model capability; Debug IR carries only refs; capture-bundle artifacts (sha256) are the canonical asset backing, with a read-only projection layer mapping bundle artifacts → AssetRefs. Asset indexing into the IR chain is part of P1/P2 slices, not this contract.
**Alternatives:** embed asset metadata inline in packets — rejected: the gate mandates refs-not-bodies; embedding invites drift and large IRs.

### D3 — Single core, closed statuses
**Decision:** CLI and TUI consume one Query/Analysis core; the closed status vocabulary (`OK / INVALID_INPUT / EVIDENCE_UNAVAILABLE / IDENTITY_MISMATCH / AMBIGUOUS_OCCURRENCE / INSUFFICIENT_TRACE_COVERAGE / SCHEMA_VIOLATION`) is inherited from P0 tooling-contract and enforced by the query-core capability.
**Why:** prevents three divergent diagnostics implementations (explicit gate requirement).

### D4 — Causal tree is the FDP surface
**Decision:** the CAUSAL/EVIDENCE tree (Observation→Occurrence→Evidence→OperatorDecision→SemanticAdmission→Affordance→RuntimeState→Terminal) is the FDP main view, distinct from the EXECUTION tree; both are projections with prune-only semantics.
**Why:** matches the gate's two-tree requirement and the trace-analysis P0 contract; execution spans are structural, not causal.

## Risks / Trade-offs

- [Umbrella drift: contracts silently expand] → proposals for slices must reference the umbrella specs; TRACE_GAP discipline (missing trace fields recorded, buyer-gated separately).
- [Parallel P1a overlap] → the umbrella references the P0 contract and defers all command-shape details to P1a's own gate; no duplication of packet schemas.
- [Asset indexing cost] → deferred to P1/P2 slices; the contract only fixes the schema and projections; no storage service in scope.
- [TUI reimplements logic] → the tooling-surface spec forbids local correlation/analysis; review gate on P3.

## Migration Plan

None (contracts only; no runtime or wire migration). Slices adopt the specs as their input contract.

## Open Questions

None that would change the contracts; framework choice for TUI and tooling stack details are deliberately deferred to the implementing slices.