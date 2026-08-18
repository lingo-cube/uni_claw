## Why

`ActionResult.TimedOut` already means that dispatch outcome is uncertain, but the current Traversal path stops before obtaining the post-action Observation required to determine what happened in the external world. SC-P3-001 purchases only the missing behavior: observe and verify after a dispatch timeout without blindly repeating a possibly non-idempotent Click.

## What Changes

- Add the SC-P3-001 contract for a Click whose world effect occurs even though dispatch reports `TimedOut`.
- Require a fresh Observation before any verdict on that uncertain action.
- Permit continuation without duplicate dispatch only when existing verification semantics can observe the intended local world effect.
- Require the unresolved negative branch to avoid fabricating success; retry policy beyond this scenario remains unspecified.
- Preserve Goal completion through existing `GoalEvidence` only.
- Add no production model type, field, enum value, interface, component, or mutable state.

## Capabilities

### New Capabilities

- `uncertain-action-verification`: Defines the narrow post-dispatch-timeout observe-and-verify behavior purchased by SC-P3-001.

### Modified Capabilities

None. Existing Environment, Container/Traversal, and Run Lifecycle requirements already define dispatch outcomes, the Execute → Observe → Verify protocol, and GoalEvidence completion. This change adds only the missing TimedOut-specific normative delta.

## Impact

- Expected implementation surface: `src/UniClaw.Runtime/Traversal/` behavior only, subject to task planning from repository truth.
- Expected verification surface: deterministic Scenario Fake/Harness and SC-P3-001 scenario tests.
- Production model delta budget: zero.
- No Popup, Scroll, Fingerprint, Confidence, multi-container, generic uncertainty framework, or retry-policy purchase.
