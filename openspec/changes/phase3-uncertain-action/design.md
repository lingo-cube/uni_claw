## Context

The frozen Phase 2 vocabulary already represents dispatch uncertainty (`ActionResultOutcome.TimedOut`), post-action world evidence (`Observation`), the local Execute → Observe → Verify protocol, reconciliation, and GoalEvidence completion. Current Traversal behavior returns `Failed` for every non-`Dispatched` outcome before observing, so the implementation does not satisfy the existing external-world evidence principles for `TimedOut`.

SC-P3-001 is a behavior-only purchase. `Rejected` behavior is unchanged. Popup, Scroll, Fingerprint, Confidence, multi-container behavior, generic uncertainty handling, and retry policy are outside this change.

## Goals / Non-Goals

**Goals:**

- Route `TimedOut` through a fresh post-action Observation without duplicate dispatch.
- Reuse existing local verification and downstream world/Goal evidence processing.
- Preserve deterministic journal, Trace, ActionHistory, and GoalEvidence assertions.
- Keep the production model delta budget at zero.

**Non-Goals:**

- Define whether or when an unverified uncertain action may be retried.
- Change `ActionResult`, `Observation`, `TraversalStepResult`, journal, Trace, WorldBelief, or GoalEvidence shapes.
- Move semantic world decisions into Environment or Goal authority into Traversal.
- Purchase any other Phase 3 candidate.

## Decisions

### TimedOut continues to Observe; Rejected remains an immediate dispatch failure

`TimedOut` means the action may already have affected the world, so returning before Observe discards the only authoritative evidence capable of resolving the uncertainty. `Rejected` is a confirmed dispatch rejection and remains outside SC-P3-001.

Alternative rejected: treat `TimedOut` like SC-P2-002 retry. That retry occurs before action dispatch and cannot safely authorize a duplicate non-idempotent action.

### Existing local success semantics remain narrow

Traversal local verification continues to mean that a fresh post-action Observation was obtained and can be consumed by the existing world-evidence flow; it does not declare Goal success or make Observation semantic truth. Agent continues to reconcile the Observation, manage Container transition, evaluate GoalEvidence, and decide the final Run state.

Alternative rejected: add an action-success field or uncertainty state. The distinction is determined from `TimedOut` plus the subsequent Observation and existing verification/evidence processing, so new model state is not purchased.

### Negative behavior is bounded without selecting retry policy

If the fresh Observation does not establish the intended effect, the Runtime must not fabricate success and must not blindly redispatch. SC-P3-001 intentionally leaves later retry/escalation policy unspecified.

Alternative rejected: introduce a generic uncertain-action framework. No current Scenario purchases that complexity.

## Risks / Trade-offs

- [Risk] Existing Traversal verification checks freshness rather than arbitrary action-specific world predicates. → [Mitigation] Keep SC-P3-001 narrow: the deterministic scenario uses the existing semantic-page/Container and GoalEvidence flow; do not generalize the behavior.
- [Risk] A future retry policy could accidentally reuse the pre-dispatch retry counter. → [Mitigation] The spec explicitly separates post-dispatch uncertainty from SC-P2-002.
- [Risk] Trace lacks a dedicated dispatch-outcome field. → [Mitigation] Prove the configured `TimedOut` result through the deterministic Fake and use existing ActionHistory, journal, Observation, Trace, and GoalEvidence surfaces; do not add a field without a later Scenario Receipt.
