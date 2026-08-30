# Design — runtime-debug-p4c-minimize

## Context

P4b established the mechanical failure predicate (`firstMechanicallyFailedStep`). P4c consumes it with a greedy delta-debugging-style loop, all read-only and deterministic.

## Goals / Non-Goals

Goals: minimal failure-preserving slice; deterministic; no mutation; no-op on clean fixtures.

Non-Goals: semantic minimization (sufficiency requires producer-labelled semantics), state simulation, execution against any environment.

## Decisions

### D1 — Predicate reuse = same Core projection
**Decision:** every candidate step-set is re-projected through `project_replay_run`; "still fails" means the same non-OK order remains. No second failure rule.
**Why:** one mechanical definition across replay (P4b) and minimization (P4c); no divergence.

### D2 — Greedy backwards, failing step fixed
**Decision:** trailing steps (after the failing order) are dropped outright; earlier steps are greedily tried in reverse, keeping deletions that preserve the predicate. The failing step itself is never dropped.
**Why:** deterministic, bounded (≤ step count iterations), and truthful for a stored-outcome-only predicate.

## Risks / Trade-offs

- [Mechanical minimal ≠ semantically minimal] → explicit note + deferred semantic sufficiency; the slice is the falsifier *input*, not a repair.

## Migration Plan

None — additive command.

## Open Questions

None that would change the contract.
