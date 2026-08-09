## Why

The current Runtime can execute caller-preauthorized Plan steps, but it cannot prove why a newly observed actionable Settings candidate is authorized, rejected, or unresolved before dispatch. SC-P3-CAND-006 requires the smallest bounded distinction so fresh candidate evidence remains separate from semantic execution authority while denied and unresolved candidates produce zero device actions.

## What Changes

- Add the approved SC-P3-CAND-006 formal contract for one bounded read-only Settings Container and one fresh candidate-classification round.
- Add exactly one immutable `CandidateAuthorizationEvidence` value with `bool? Authorized` and non-empty `string Reason` fields.
- Add exactly one optional immutable `Goal.CandidateAuthorizationEvaluator: Func<Observation, ObservedElement, CandidateAuthorizationEvidence>?` field.
- Define `true` as positive bounded authorization, `false` as positive rejection, and `null` as unresolved evidence that grants no authorization.
- Require Agent to remain the sole semantic authorization authority and to record rejected/unresolved outcomes in existing Trace before dispatch.
- Permit only an authorized safe navigation candidate to enter the existing Container/Traversal Tap protocol; normal post-action Observation and GoalEvidence requirements remain unchanged.
- Keep observed candidates, authorized candidates, required work, dispatched actions, world effects, and Goal completion semantically distinct.

## Capabilities

### New Capabilities

- `bounded-candidate-safety`: Defines Agent-owned, deterministic, pre-dispatch authorization of freshly observed Settings candidates, including safe, destructive, state-changing, unresolved, zero-dispatch, completion-boundary, and replay behavior.

### Modified Capabilities

None.

## Impact

- Expected production surface: one immutable two-field Model value, one optional immutable Goal field, and existing Agent control-flow behavior only.
- Expected verification surface: deterministic candidate evidence fixture and SC-P3-CAND-006 positive/destructive/state-changing/unresolved/replay proofs.
- Production delta budget: model types +1; fields +3 total; enums +0; interfaces +0; components +0; mutable-state owners +0.
- Ownership delta: none. Authority delta: none.
- Existing fixed-Plan Runs remain backward-compatible when the optional evaluator is absent.
- No SafetyManager, RiskEngine, policy/rule engine, SafeActionExecutor, authorization manager, RiskLevel, Confidence, policy hash, coordinate, Fingerprint, Vision/VLM judgement, navigation graph/stack, dynamic planner, generalized candidate discovery, universal interception, mutable safety owner, Capstone implementation, Harness change, or Runtime refactor is purchased.
