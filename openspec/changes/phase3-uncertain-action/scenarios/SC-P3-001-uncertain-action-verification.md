# SC-P3-001 — Uncertain Action Verification After Dispatch Timeout

> Phase 3 | Semantic Gate: `BEHAVIOR_PURCHASE_ONLY`
> Production Model Delta Budget: `0`
> Consumer: `specs/uncertain-action-verification/spec.md`

## Goal

Verify that a non-idempotent Click whose dispatch reports `TimedOut` is resolved from a fresh Observation rather than blindly repeated or treated as world success.

## Given

- Runtime is Running with a valid active Container.
- The Click target is grounded from the current Observation.
- The deterministic Environment applies the Click effect and transitions to the intended local world.
- The same dispatch returns `ActionResultOutcome.TimedOut`.

## When

Traversal receives the uncertain dispatch outcome.

## Then

1. Runtime does not immediately dispatch the Click again.
2. Runtime obtains a fresh Observation after the `TimedOut` result.
3. The existing Execute → Observe → Verify flow and existing world-evidence processing determine what is now observable.
4. When the intended local world effect is observable, execution continues without a duplicate Click.
5. ActionHistory contains exactly one Click for the uncertain step.
6. `TimedOut` itself is not treated as proof of local effect, world success, or Goal completion.
7. Run completion still requires a satisfied `GoalEvidence` derived from Observation evidence.
8. Repeating the same run with the same deterministic input produces the same ActionHistory, Observation sequence, journal, Trace, and final state.

## Negative Branch

Given the same grounded Click and `TimedOut` dispatch result, but the fresh Observation does not show the intended local world effect:

- Runtime must not fabricate the intended effect or Goal success.
- Runtime must not blindly repeat the Click.
- This Scenario does not purchase a retry policy or a generic uncertainty framework; the run may remain unresolved or follow an existing explicit failure path.

## Evidence Required

1. The configured dispatch result for the uncertain step is `TimedOut`.
2. ActionHistory contains exactly one matching Click.
3. Traversal journal carries the original dispatched action and a fresh post-action Observation for the uncertain step.
4. The positive branch continues using the observed target world.
5. The negative branch produces no fabricated `GoalEvidence.Satisfied` and no `Completed` transition caused by `TimedOut`.
6. Trace and journal retain their existing shapes; no new model field is required.
7. Deterministic replay produces equal evidence sequences.

## Ownership and Authority

- Environment reports dispatch outcome and provides Observation only; it does not decide semantic action success.
- Traversal owns the local Execute → Observe → Verify continuation and must not blindly redispatch after `TimedOut`.
- Agent retains world reconciliation, Container transition, Goal completion, and final Run authority.

## Explicitly Deferred

- Retry after an unverified uncertain action.
- Generic uncertainty state or framework.
- New production type, field, enum value, interface, component, or mutable state.
- Popup, Scroll, Fingerprint, Confidence, and multi-container capabilities.
