# SC-P3-002 — Popup Obstruction Recovery with Container Continuity

> Phase 3 | Semantic Gate: `BEHAVIOR_PURCHASE_ONLY`
> Production Model Delta Budget: `0`
> Ownership Delta: `NONE` | Authority Delta: `NONE`
> Consumer: `specs/popup-local-recovery/spec.md`

## Goal

Prove that an external Popup or Overlay may obstruct interaction while the underlying semantic page remains the same logical Container, and that the Runtime can handle the local obstruction only within approved Container scope before continuing or escalating from fresh world evidence.

## Given

- Runtime is Running with a valid active Container.
- The current Observation is bound to that Container.
- Local execution is in progress and the Container has recorded local progress.
- A deterministic external Popup or Overlay appears and blocks local interaction.
- The obstruction does not itself prove that the underlying semantic page changed.

## When

The Runtime encounters the local obstruction while attempting to continue work in the active Container.

## Then

1. Runtime treats the condition as a Container-scope obstruction hypothesis rather than immediate Agent-scope drift.
2. Container authorizes only approved bounded local handling through the existing execution direction.
3. Runtime obtains a fresh Observation after handling; the Observation sequence strictly advances beyond the obstruction evidence.
4. Runtime verifies that the foreground application remains compatible, the active Container's existing `IsStillMine` rule accepts the Observation, and reconciled semantic-page evidence does not contradict the active Container.
5. When continuity is proven, the same active Container and its pre-obstruction local progress are preserved.
6. Existing execution may continue without an unconditional Agent recovery.
7. Successful dismissal or local continuation does not itself satisfy GoalEvidence or complete the Run.
8. Agent retains active Container transition, Goal completion, Agent recovery, and final RunState authority.

## Negative / Escalation Branch

If the Popup cannot be handled, the post-handling Observation is absent or stale, the foreground application is incompatible, `IsStillMine` rejects it, or reconciled semantic evidence remains Unknown/conflicting:

- Container does not fabricate local handling success.
- Runtime performs no blind or unbounded local handling repeat.
- Existing local progress is not silently reset.
- Container escalates structured evidence to Agent.
- Agent alone decides rebind, Agent recovery, or Run failure.
- Lower scope does not invoke or duplicate the frozen Recovery mechanism.

## Evidence Required

1. The same active Container exists before obstruction and after verified handling.
2. Pre-obstruction local progress is observable and remains preserved on the verified-positive branch.
3. ActionHistory proves only approved bounded local handling occurred.
4. A post-handling Observation exists and its sequence is strictly newer than the obstruction Observation.
5. Foreground, `IsStillMine`, and reconciled semantic-page evidence jointly support Container continuity.
6. The positive branch continues without unconditional Agent recovery or Container rebind.
7. The escalation branch produces no fabricated success, no silent progress reset, and explicit evidence for Agent.
8. GoalEvidence and final RunState remain Agent-controlled.
9. Equal RunId, deterministic Environment input, and action sequence replay to equal ActionHistory, Observation sequence, journal, Trace, continuity evidence, preserved progress, GoalEvidence, and final state.

## Identity Boundary

- Same screenshot is not Container identity.
- Same Observation is not fresh continuity evidence.
- Fingerprint is not purchased and cannot be used as identity authority.
- Container continuity must be proven from fresh external evidence plus the existing semantic identity rule.

## Ownership and Authority

- Environment owns external-world simulation and reports Observation/dispatch outcome only.
- Traversal owns deterministic local execution mechanics and does not decide Container identity or global recovery.
- Container owns local obstruction classification, bounded local handling authority, local continuity proof, local progress, and the decision that local proof is insufficient.
- Agent owns active Container binding changes, interpretation of escalated evidence, Agent recovery initiation, GoalEvidence evaluation, and final RunState.
- Recovery retains its frozen mechanism ownership and does not depend on Container or Traversal.

## Explicitly Deferred

- PopupManager, PopupRecoveryEngine, RecoveryPlanner, ContainerRecoveryManager, or another new recovery component.
- Popup/Overlay production model, new Trap kind, new result state, or other production type/field/enum/interface/component/mutable state.
- Generic retry, generic uncertainty, or generic recovery framework.
- Fingerprint, new Confidence behavior, Scroll, multi-container progress, and SC-P3-003.
- Real-device, Vision, Popup classification algorithm, and implementation tasks.
