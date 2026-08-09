# SC-P3-003 Capability Closeout

> Status: Frozen Capability | Date: 2026-08-08
> Scope: SC-P3-003 only — this is not a Phase 3 freeze.
> Authority: acceptance receipt for `openspec/changes/phase3-scroll-identity-continuity/`; it does not replace the Scenario, Spec, Architecture Contract, or frozen Phase 2 decisions.

## Capability

**Viewport Movement Preserves Container Identity**

Semantic Gate: `SEMANTIC_PURCHASE_REQUIRED`

## Proven Behavior

```text
one bounded forward viewport movement
→ fresh Observation
→ existing semantic continuity proof
→ preserve Container progress or escalate
```

The accepted slice proves:

- Traversal dispatches exactly one targetless `ScrollForward` action and obtains post-action evidence without blind redispatch.
- A changed visible element set is snapshot evidence; it is not semantic navigation, Container identity, viewport progress, or Goal success.
- Fresh continuity requires a strictly newer Observation, compatible foreground evidence, acceptance by the existing `Container.IsStillMine` rule, and the same reconciled semantic page.
- When continuity is proven, the same Container advances `CurrentObservation` without `Bind`, preserves pre-movement local progress, and can continue execution from the new viewport.
- Rejected, stale, or identity-conflicting evidence does not fabricate continuity or silently reset progress; it produces Container-scope evidence for Agent handling.
- Agent retains active-Container rebind, Recovery initiation, GoalEvidence evaluation, and final RunState authority.
- Goal completion still requires satisfied `GoalEvidence`; viewport dispatch or local continuity does not complete the Run.
- Equal RunId, deterministic Environment input, and action sequence replay to equal ActionHistory, Observation sequence, journal, Trace, identity evidence, progress, GoalEvidence, and final state.

## Production Delta

- Approved model purchase: exactly one immutable, parameterless `DeviceAction.ScrollForward` variant.
- Allowed behavior change: existing Traversal, Container, and Agent methods/control flow only.
- Additional production model types beyond the approved variant: +0.
- Production fields: +0.
- Enums: +0.
- Interfaces: +0.
- Components: +0.
- Mutable state: +0.

## Ownership and Authority

- Ownership delta: **NONE**.
- Authority delta: **NONE**.
- Environment remains the external dispatch and Observation boundary.
- Traversal remains the owner of deterministic Execute → Observe → Verify mechanics.
- Container remains the owner of local Observation, local progress, semantic continuity, and Container-scope evidence.
- Agent remains the authority for active Container changes, higher-scope interpretation, Agent Recovery, GoalEvidence, and final RunState.
- Recovery ownership remains frozen; Recovery → Container/Traversal remains **FORBIDDEN**.

## Frozen Boundary

| Branch | Frozen meaning |
|---|---|
| Continuity proven | Preserve the same Container and local progress, advance its current Observation without `Bind`, and allow existing execution to continue. |
| Continuity unproven | Preserve available local progress, emit Container-scope evidence, do not repeat the viewport action blindly, and leave the higher-scope response to Agent. |

## Explicitly Not Purchased

- Fingerprint or Fingerprint identity authority;
- direction, coordinate, distance, duration, or gesture geometry fields;
- reverse or automatic repeated scrolling;
- viewport progress or end-of-list detection;
- ScrollManager, viewport component, or generic continuity framework;
- generic retry or uncertainty framework;
- multi-container progress;
- real-device or Vision behavior;
- new Recovery semantics;
- FSM or state-machine abstraction;
- Runtime structural refactor;
- another Phase 3 Scenario or Phase completion.

## Structural Pressure

Agent post-action evidence plumbing and journal temporal coupling remain observable structural pressure. They did not require semantic, ownership, authority, or production-budget growth in SC-P3-003 and therefore did not authorize a refactor.

## Acceptance Receipt

- OpenSpec: strict validation passed; state `all_done`.
- Tasks: 4/4 complete.
- Independent validation: PASS.
- Build: 0 warnings, 0 errors.
- Tests: 209/209 passed.
- SC-P3-003 formal tests: 6/6 passed.
- Architecture Guards: 8/8 passed.
- Consistency checks: ALL PASS.
- Production model delta: exactly one approved parameterless action variant; all other model deltas 0.
- Ownership delta: NONE.
- Authority delta: NONE.
- Semantic drift: NONE.

## State

```text
SC_P3_003_FROZEN_CAPABILITY
```

This state does **not** mean `PHASE_3_FROZEN` or `PHASE_COMPLETE`. Other Phase 3 candidate scenarios remain outside this auto-continue run and require their own authority.
