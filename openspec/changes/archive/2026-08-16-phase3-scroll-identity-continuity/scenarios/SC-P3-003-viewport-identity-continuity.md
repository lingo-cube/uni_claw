# SC-P3-003 — Viewport Movement Preserves Container Identity

> Phase 3 | Semantic Gate: `SEMANTIC_PURCHASE_REQUIRED`
> Approved Production Model Delta: one immutable bounded-forward-viewport `DeviceAction` variant
> Fields: `+0` | Enums: `+0` | Interfaces: `+0` | Components: `+0` | Mutable State: `+0`
> Ownership Delta: `NONE` | Authority Delta: `NONE`
> Consumer: `specs/viewport-identity-continuity/spec.md`

## Goal

Prove that one bounded forward viewport movement may replace the visible element set while the underlying semantic page remains the same logical Container, and that the Runtime advances fresh local evidence without resetting progress or converting snapshot change into navigation.

## Given

- Runtime is Running with a valid active Container.
- The Container has a current Observation and recorded local progress.
- Observation 1 exposes viewport elements A/B/C.
- Existing semantic identity evidence binds Observation 1 to the active Container.
- The plan contains one approved bounded forward viewport-movement step.

## When

Traversal dispatches the approved viewport action once and Environment supplies post-action evidence.

## Then

1. ActionHistory and journal record exactly one targetless bounded-forward viewport action.
2. Runtime obtains Observation 2 whose sequence strictly advances beyond Observation 1.
3. Observation 2 exposes a visibly different element set D/E/F.
4. Snapshot change alone does not cause navigation, PressBack, Container replacement, or Recovery.
5. Compatible foreground evidence, the existing `IsStillMine` rule, and reconciled semantic-page evidence jointly prove that Observation 2 still belongs to the active Container.
6. The same Container remains active and its current Observation advances to Observation 2 without `Bind`.
7. All local progress recorded before the viewport movement remains present and execution may continue.
8. Viewport dispatch or continuity does not itself satisfy GoalEvidence or complete the Run.
9. Agent retains rebind, Recovery, GoalEvidence, and final RunState authority.

## Negative / Escalation Branch

If dispatch is rejected, post-action evidence is absent or stale, foreground is incompatible, `IsStillMine` rejects Observation 2, or reconciled semantic-page evidence contradicts the active Container:

- Runtime does not fabricate viewport progress or same-Container continuity.
- The viewport action is not blindly redispatched.
- Existing local progress is not silently reset.
- Container produces structured Container-scope evidence.
- Agent alone decides rebind, Agent Recovery, Run failure, or later continuation.

## Evidence Required

1. One active Container exists before and after the verified-positive movement.
2. Pre-movement local progress is observable and remains preserved.
3. ActionHistory records exactly one viewport action and no fabricated element target.
4. Post-action Observation sequence is strictly newer and its visible elements differ.
5. Foreground, `IsStillMine`, and reconciled semantic-page evidence jointly prove continuity.
6. Container.CurrentObservation advances without a progress-resetting bind or replacement.
7. Negative branches show no fabricated success, blind redispatch, or silent progress reset and expose Container-scope evidence to Agent.
8. GoalEvidence and final RunState remain Agent-controlled.
9. Equal RunId, Environment inputs, and action sequence replay to equal ActionHistory, Observation sequence, journal, Trace, identity evidence, progress, GoalEvidence, and final state.

## Identity Boundary

- Different visible elements prove snapshot movement, not Container change.
- Same or different screenshot is not semantic identity.
- Fingerprint is neither purchased nor an identity authority.
- Existing fresh external evidence plus the injected semantic identity rule remains authoritative.

## Ownership and Authority

- Environment reports external Observation and dispatch outcome only.
- Traversal owns one bounded action's deterministic Execute → Observe → Verify protocol.
- Container owns local Observation, progress, semantic identity continuity, and Container-scope insufficiency evidence.
- Agent owns active Container changes, higher-scope interpretation, Agent Recovery initiation, GoalEvidence, and final RunState.
- Recovery retains its frozen mechanism ownership and does not depend on Container or Traversal.

## Explicitly Deferred

- Fingerprint or Fingerprint identity authority.
- Direction/coordinate/distance/duration gesture model.
- Reverse scrolling, automatic repeated scrolling, scroll progress, or end-of-list detection.
- ScrollManager, viewport component, generic continuity/retry/recovery framework, or FSM.
- Multi-container progress, real-device/Vision behavior, new Recovery semantics, and Runtime refactoring.
