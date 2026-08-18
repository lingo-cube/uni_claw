## Why

The Runtime currently has no approved behavior for an external Popup or Overlay that obstructs interaction while the underlying semantic page remains continuous. SC-P3-002 purchases the missing Container-scope behavior: handle the local obstruction, obtain fresh world evidence, prove Container continuity, and escalate when continuity cannot be proven without reinterpreting the obstruction as immediate Agent-scope drift.

## What Changes

- Add the SC-P3-002 contract for a Popup or Overlay that blocks local interaction without itself proving that the underlying semantic page changed.
- Require local obstruction handling to remain bounded by existing Container-local execution authority.
- Require a fresh post-handling Observation before declaring the obstruction handled.
- Preserve the same Container and its local progress only when existing semantic identity evidence proves continuity.
- Require explicit escalation evidence when dismissal or continuity verification fails; lower scope must not perform Agent recovery or fabricate success.
- Preserve Agent ownership of active Container transitions, Goal completion, recovery initiation, and final Run state.
- Add no production model type, field, enum value, interface, component, or mutable state.

## Capabilities

### New Capabilities

- `popup-local-recovery`: Defines the narrow Container-scope obstruction handling, continuity verification, progress preservation, and escalation behavior purchased by SC-P3-002.

### Modified Capabilities

None. Existing Observation, WorldBelief, Container identity and local progress, Trap, RecoveryResult, local Execute → Observe → Verify, and Agent escalation vocabulary are sufficient. This change adds only the missing Popup-specific normative behavior.

## Impact

- Expected production behavior surface: existing Container/Agent/Traversal control flow only, subject to later approved task planning.
- Expected verification surface: deterministic Scenario Fake/Harness and SC-P3-002 positive, escalation, and replay tests.
- Production model delta budget: zero; ownership delta: none; authority delta: none.
- Frozen Phase 2 Recovery ownership and the Recovery → Container/Traversal prohibition remain unchanged.
- No Popup manager, recovery engine, planner, FSM, Fingerprint, new Confidence behavior, generic retry, generic uncertainty, Scroll, multi-container, or SC-P3-003 purchase.
