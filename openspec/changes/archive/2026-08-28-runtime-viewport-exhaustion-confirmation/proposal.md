## Why

Human Gate `PROJECT_LEADER_RUNTIME_VIEWPORT_EXHAUSTION_CONFIRMATION_CONTRACT_GATE` (2026-08-28)
authorized a Runtime contract change for **IR-G1** (STOP-2:
`evidence/STOP-2-viewport-union-exhaustion-edge.md` in
`runtime-iterative-full-traversal-acceptance`): the frozen viewport-union normalization
contract requires every accepted window to EXTEND the accumulated source union. On a
real, stable, bounded list, the final scroll produces a zero-new-source confirmation
window that necessarily violates that assumption → `Source normalization is unresolved`
→ true exhaustion can never be proven (run 1 passed only via perception-instability
luck; the now-stable perception makes the edge deterministic — runs 1–6 evidence).

Phase 2.6 remains STOPPED and must NOT compensate inside its validation harness. This
change buys exactly one contract capability:
**the Runtime must be able to distinguish "I have not found anything new yet" from "I
have confirmed, with fresh, stable, consistent evidence, that there is genuinely nothing
new here."**

## What Changes

- `SourceEquivalenceNormalizer.Normalize` gains a closed three-way per-window
  classification after the first window:
  - **EXTENDING_WINDOW** — unique suffix(union)↔prefix(window) overlap (existing
    semantics, unchanged);
  - **CONSISTENT_CONFIRMATION_WINDOW** — zero genuinely new logical sources AND strict
    provable consistency with the canonical union tail (conditions frozen in the spec;
    any miss → unresolved);
  - **UNRESOLVED_WINDOW** — fail-closed (existing behavior for everything else).
  Zero-new-source alone NEVER equals exhausted.
- Completeness (`TryBuildContainerInventoryCompleteness` path) accepts a normalization
  result whose trailing windows are confirmations: confirmations add no sources, create
  no dispatch authority, and are recorded as exhaustion-confirmation backing on the
  completeness evidence. Bounded consecutive-confirmation count (explicit constant).
- No other behavior: discovery, grounding, authorization, visiting, completion, recovery,
  wire, API — all untouched.

## Capabilities

### New Capabilities

- `VIEWPORT_EXHAUSTION_CONFIRMATION`: the Runtime can prove bounded-list exhaustion on
  stable real observations by classifying a strictly-consistent zero-new-source terminal
  window as exhaustion-confirmation evidence — without weakening any identity,
  ambiguity, or fail-closed rule.

### Modified Capabilities

- None beyond the normalization/completeness seam above (same module, same owners).

## Impact

- Production scope: `src/UniClaw.Runtime/World/SourceEquivalenceNormalizer.cs` +
  the completeness consumer seam in `Agent.OpenWorld` (evidence recording only) +
  `SourceNormalizationResult` shape (additive window classification). Runtime-internal;
  no wire/DTO/Strategy Contract/GoalEvidence/FSM change.
- Phase 2.6 resume condition (frozen): ONLY after this change is implemented, regressed
  (incl. the deterministic STOP-2 reproduction: old-contract-red → new-contract-green),
  and independently graduated, does the reentry campaign restart **from the STOP-2
  layer** (fresh Stage A run; never resumed mid-stage).
- Non-goals (explicitly NOT purchased): generic recovery, Memory, Planner, Assisted
  Exploration, dynamic depth, new wire/API, any Phase 2.6 acceptance-criteria change.
