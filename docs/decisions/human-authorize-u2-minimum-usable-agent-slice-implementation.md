# HUMAN_AUTHORIZE_U2_MINIMUM_USABLE_AGENT_SLICE_IMPLEMENTATION_REBASED

> Date: 2026-08-10
> Authority: Human
> Development lane: `CAPABILITY_DELIVERY_FAST`
> Scenario: `SC-U2-MUS-001`
> Status: `AUTHORIZED`

## Authorized Production Scope

1. Add exactly one production file:
   `src/UniClaw.Runtime/Planning/IntentSemanticEnvelopeExecution.cs`.
2. Modify only `src/UniClaw.Runtime/Agent/Agent.cs` for the bounded open-world
   execution path.
3. Use only method-local parent continuation frames composed as
   `(parent Container, child identity)`. Semantic depth is derived from the
   method-local frame count.

The Planning seam consumes an already-resolved
`TypeLevelTraversalSpecification`. It only validates/destructures and forwards
the immutable boundary. It does not parse intent, infer requirements, generate
a route, discover inventory, select targets, observe, dispatch, or decide
completion.

## Frozen Completion Semantic

For traversal-shaped Goals only:

```text
VerifiedBoundedTraversalCompletion
+ existing fresh GoalEvidence
= Goal completion
```

Agent MUST derive verified bounded traversal completion before invoking the
existing `Goal.EvidenceEvaluator` on the current fresh root Observation. The
following remain insufficient:

- visited known nodes;
- local branch exhaustion;
- observation failure;
- ambiguity;
- depth or safety cutoff described as discovered-world exhaustion.

Non-traversal Goal completion remains unchanged.

## Explicitly Not Authorized

- `Goal.cs` changes;
- `Goal.BranchProgressEvidenceEvaluator`;
- a new Frame type, Graph, FSM, Planner, or navigation framework;
- a new state owner, persistent traversal state, or new mutable field;
- ownership, authority, dependency-direction, safety-semantic, or architecture
  invariant changes;
- changes to existing `Agent.RunAsync(Goal, Plan, ...)` semantics;
- viewport expansion, Recovery/Popup changes, generic retry/uncertainty, U3,
  Harness changes, or another Scenario.

## Execution Policy

The Execution Worker owns bounded implementation, tests, ordinary diagnosis,
repair, validation, evidence collection, and reusable test-asset preparation.
The Project Leader retains architecture, semantic, ownership, authority, scope,
Human-Gate, corpus-promotion, and final validation decisions.

Auto-continue through ordinary compile, test, fixture, documentation, and
bounded regression repair. Stop only for a canonical architecture, semantic,
safety, authority, scope, or product decision boundary.

## Continuation

Resume the same `u2-open-world-settings-traversal` OpenSpec change at Task 2.1.
Task 1.1 remains DONE. This receipt supersedes the earlier evaluator-bearing U2
authorization; it does not authorize implementation outside the rebased scope.
