## Why

The Runtime cannot currently express or verify a bounded viewport movement whose visible elements change while the underlying semantic page remains the same logical Container. SC-P3-003 purchases the minimum missing action semantic and evidence-driven continuity behavior so snapshot change is not misinterpreted as navigation or recovery.

## What Changes

- Add the approved SC-P3-003 contract for one bounded forward viewport movement within an active Container.
- Add exactly one immutable production `DeviceAction` variant for that movement, with no new fields.
- Require the existing Traversal execution protocol to dispatch the action once and obtain a strictly newer Observation.
- Require Container continuity to be proven from existing foreground, semantic-page, and `IsStillMine` evidence rather than snapshot equality or Fingerprint.
- Preserve the same Container and its existing local progress when continuity is proven, while advancing its current Observation without a progress-resetting rebind.
- Escalate without fabricated progress, blind redispatch, or lower-scope recovery when fresh continuity evidence is absent or contradictory.
- Preserve Agent ownership of active Container changes, GoalEvidence, Recovery decisions, and final RunState.

## Capabilities

### New Capabilities

- `viewport-identity-continuity`: Defines the bounded forward viewport action, fresh post-action evidence, same-Container continuity, progress preservation, escalation, and deterministic replay purchased by SC-P3-003.

### Modified Capabilities

None. Existing Observation, semantic-page belief, Container identity/local progress, Traversal journal, Trap escalation, GoalEvidence, and RunState semantics remain authoritative. The only production-model addition is the explicitly approved action variant.

## Impact

- Expected production surface: `src/UniClaw.Runtime/Model/Actions/DeviceAction.cs` plus existing Traversal/Container/Agent control flow, subject to approved task planning.
- Expected verification surface: deterministic Scenario Fake/Harness and SC-P3-003 positive, contradictory-identity/stale-evidence, and replay proofs.
- Production model delta budget: one immutable `DeviceAction` variant; fields +0, enums +0, interfaces +0, components +0, mutable state +0.
- Ownership delta: none. Authority delta: none.
- Frozen Recovery ownership and Recovery → Container/Traversal prohibition remain unchanged.
- No Fingerprint, coordinate/gesture geometry, scroll-distance/progress model, ScrollManager, automatic repeated scrolling, end-of-list detection, reverse scrolling, multi-container progress, new Recovery semantics, or Runtime refactor is purchased.
