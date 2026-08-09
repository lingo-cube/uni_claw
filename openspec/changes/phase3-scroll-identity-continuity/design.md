## Context

The current Runtime can represent fresh Observations, semantic-page belief, Container identity and local progress, but `DeviceAction` has no viewport-movement variant and Traversal accepts only element-targeted Tap/SetSwitch tokens. A normal same-Container post-action path also does not advance `Container.CurrentObservation` without `Bind`, which would reset progress. SC-P3-003 therefore requires one approved action semantic plus bounded behavior across existing Model, Traversal, Container, Agent, and deterministic test surfaces.

The existing Agent has confirmed structural pressure from repeated post-action evidence plumbing and journal temporal coupling. That pressure is not an ownership or authority conflict and does not authorize a Runtime refactor in this change.

## Goals / Non-Goals

**Goals:**

- Represent exactly one bounded forward viewport movement as a first-class immutable action.
- Execute it once through Traversal and obtain a strictly newer post-action Observation.
- Prove that changed visible elements can remain within the same semantic Container.
- Advance the active Container's current Observation without resetting its existing local progress.
- Escalate stale, absent, or identity-conflicting evidence without blind redispatch or fabricated continuity.
- Preserve deterministic ActionHistory, Observation sequence, journal, Trace, progress, GoalEvidence, and final-state replay.

**Non-Goals:**

- Add Fingerprint or treat snapshot similarity/difference as identity authority.
- Add direction, coordinates, distance, duration, scroll progress, end-of-list, reverse scrolling, or repeated-scroll policy.
- Add a ScrollManager, viewport component, FSM, generic continuity framework, new interface, enum, mutable state, or Recovery semantic.
- Refactor Agent/Container/Traversal structure or remove existing temporal coupling.
- Implement real-device gesture or Vision behavior.

## Decisions

### Add one parameterless bounded-forward viewport action

The approved production-model purchase is one immutable `DeviceAction` variant with no fields. Its meaning is one bounded forward viewport movement. Traversal recognizes one protocol token and dispatches the action through the unchanged `IEnvironment.ExecuteAsync(DeviceAction, ...)` port.

The action is intentionally targetless. Traversal records `SelectedElementIndex = null` because no observed element is selected, while `DispatchedAction` records the viewport action and distinguishes it from a failed Select. The action still follows Execute → Observe → Verify and is dispatched exactly once.

Alternative rejected: encode viewport movement as `Tap` or `SetSwitch`. That would make ActionHistory, Trace, and Environment semantics false.

Alternative rejected: add direction/geometry fields. SC-P3-003 purchases only one forward movement and supplies no evidence requiring a broader gesture model.

### Snapshot movement is evidence, not Container identity

The deterministic positive branch changes the visible element set between two strictly ordered Observations. Existing compatible foreground evidence, `Container.IsStillMine`, and reconciled semantic-page evidence must jointly accept the new Observation. No same-screenshot, same-Observation, element-set equality, or Fingerprint rule is identity authority.

Alternative rejected: add `Observation.Fingerprint`. Direct Observation evidence already proves the snapshot changed, while existing semantic identity rules decide whether the Container remains valid.

### Advance local Observation without rebinding

On verified continuity, Container updates its owned current Observation through a narrow existing-class behavior and preserves `ExecutedSteps` and other local progress. Agent must not call `Bind` on the positive branch because `Bind` intentionally resets progress.

The new behavior remains within Container's semantic-page local state domain. It adds no field, state owner, component, or interface.

Alternative rejected: rebind a freshly created or existing Container after every viewport move. That would misclassify local movement as navigation and silently reset progress.

### Failed continuity escalates; Agent owns the response

If dispatch is rejected, post-action evidence is missing/stale, foreground is incompatible, `IsStillMine` rejects it, or reconciled semantic-page evidence contradicts the active Container, Container cannot accept continuity. Existing Trap vocabulary carries Container-scope evidence; Agent retains the separate decision to rebind, invoke Agent Recovery, or fail the Run.

Traversal does not retry a dispatched viewport action. Goal completion remains exclusively driven by Agent evaluation of GoalEvidence.

Alternative rejected: let Container rebind itself or invoke Recovery. That would cross frozen ownership and authority boundaries.

### Preserve current structure despite refactor pressure

Implementation may add only the minimum Scenario-specific branches and private helpers required by the approved behavior. It must record existing mechanical duplication/temporal coupling as structural pressure and must not extract a new pipeline or component under this change.

## Risks / Trade-offs

- [Risk] A parameterless action cannot express reverse or variable-distance movement. → Mitigation: those semantics remain deferred until another Scenario proves them necessary.
- [Risk] `SelectedElementIndex = null` previously appeared only on selection failure. → Mitigation: `DispatchedAction` and Result already distinguish a targetless dispatched action from an undispatched failure; formal tests lock this distinction.
- [Risk] Additional Agent branching increases confirmed structural pressure. → Mitigation: keep the delta narrow, record the pressure, and defer architecture-neutral extraction to a separately purchased refactor.
- [Risk] Changed element sets may tempt identity inference from snapshots. → Mitigation: require existing semantic identity evidence and explicitly prohibit Fingerprint authority.
