# Design — runtime-debug-p4b-replay-projection

## Context

P4a established the `runtime-debug-replay.v0` fixture and validation. P4b consumes it for a deterministic dry-run view — the mechanical trajectory that a future minimizer/RED loop can assert against.

## Goals / Non-Goals

Goals: ordered trajectory projection, counts, mechanical first failure; deterministic; validation-first.

Non-Goals: state simulation, execution against any environment, minimization, mutation.

## Decisions

### D1 — Mechanical failure = stored outcome outside the OK set
**Decision:** `firstMechanicallyFailedStep` uses stored `resultOutcome` ∉ {Dispatched, Succeeded}; explicitly labelled mechanical, never semantic.
**Why:** deterministic and honest; semantic first-divergence stays the Agent's job.

### D2 — One read/validation path shared with `replay`
**Decision:** `replay-run` reuses the same fixture reader + validator; only the result shape differs.
**Why:** single source of truth for fixture trust.

## Risks / Trade-offs

- [OK-set vocabulary limited to stored outcomes] → deterministic; richer semantics need producer-labelled outcomes (deferred).
- [Not an executable replay yet] → the dry-run trajectory is the assertion surface for P4/P4b+ minimizer; documented.

## Migration Plan

None — additive command.

## Open Questions

None that would change the contract.
