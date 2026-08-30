# Design — runtime-debug-artifact-out

## Context

The end-to-end demo surfaced the extraction friction; this change makes artifact land-on-disk a first-class, policy-bounded operation.

## Goals / Non-Goals

Goals: optional `--out` on the two generator commands; append-only, bundle-external, atomic.

Non-Goals: general file output for other commands; overwrite/update semantics; path expansion beyond one file.

## Decisions

### D1 — Append-only external atomic output
**Decision:** reject existing paths and bundle-internal paths; write via temp+rename. No `--out` → unchanged behavior.
**Why:** matches capture-store append-only ethos and keeps the trust boundary (outputs never touch the validated bundle).

## Risks / Trade-offs

- [TOCTOU race on exists-check] → atomic rename still guarantees no partial writes; overwrite protection is best-effort, documented.

## Migration Plan

None — additive option.

## Open Questions

None that would change the contract.
