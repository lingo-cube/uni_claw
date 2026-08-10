# HUMAN_AUTHORIZE_CP12_MINIMUM_VERTICAL_SLICE_IMPLEMENTATION

> Date: 2026-08-10
> Authority: Human
> Development lane: `CAPABILITY_DELIVERY_FAST`
> Scenario: `SC-CP12-MVS-001`
> Capability: `GC-03 — Hypothesis-with-Fresh-Verification`

## Authorization

The Human authorizes exactly the production-shaped purchase recorded by the
current CP12 `HUMAN_IMPLEMENTATION_GATE_REQUIRED` boundary review:

- immutable `TargetGroundingEvidence` with `bool? Supported` and non-empty
  `string Reason`;
- immutable `TargetGroundingCriterion` with one candidate-evidence evaluator and
  one post-action-evidence evaluator;
- one optional `PlanStep.TargetGroundingCriterion` field;
- bounded existing-layer control-flow adjustments required to consume those
  values without changing ownership or authority.

## Frozen Responsibility Boundary

Traversal retains candidate membership and selection, qualitative grounding
sufficiency and ambiguity evaluation, enforcement of immutable existing safety
authorization receipts, Tap construction and dispatch, first fresh Observation,
expected-effect verification, journal evidence, and structured step result.

Agent does not perform local target selection, Tap dispatch, or local
expected-effect verification. Agent may only prepare immutable receipts from the
existing `CandidateAuthorizationEvaluator`, pass them downward, consume the
structured Traversal result, reconcile world evidence, evaluate GoalEvidence,
and retain final RunState authority.

Container gains no decision authority and only forwards immutable step inputs
and results within its existing local-state boundary.

## Explicitly Not Authorized

- numeric Confidence or Threshold;
- `Goal.TargetGroundingHypothesisEvaluator` or any GoalEvidence change;
- new mutable state, component, engine, enum, interface, owner, or authority;
- dependency-direction, safety-semantic, or architecture-invariant change;
- semantic expansion beyond GC-03 / RM-10 ER-25..ER-28;
- GroundingEngine/Manager, ranking/confidence framework, candidate registry,
  retry/redispatch/recovery/FSM, perception/coordinate/spatial/Fingerprint,
  Vision/VLM/Host integration, CP-11 correction, unsafe/irreversible hypothesis
  dispatch, or S1/S2/S3 work.

## Prior Result

The earlier zero-production-delta test-only result remains
`SUPERSEDED_BY_PRODUCTION_SHAPED_VALIDATION` and is not a canonical terminal
state.

## Continuation

Resume the same `CP12_MINIMUM_VERTICAL_SLICE_FAST_LOOP` checkpoint and
auto-continue through bounded implementation, testing, diagnosis, repair, full
validation, and freeze. Stop only at `VALIDATED` or a canonical Hard Gate.

