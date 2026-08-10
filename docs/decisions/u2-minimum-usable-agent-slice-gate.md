# U2_MINIMUM_USABLE_AGENT_SLICE_GATE_RESULT

> Date: 2026-08-10
> Development intent: open-world Settings traversal within `depth <= N`
> Development lane: `SEMANTIC_DISCOVERY_AUTOPILOT` → Human boundary
> Status: `HUMAN_IMPLEMENTATION_AUTHORIZED_REBASED`
> Runtime changes: `NONE`

## Evidence-Backed Gate Reconciliation

`U2_EXISTING_ASSET_FEEDBACK_RESULT` supersedes the original three-part
production purchase. Existing executable evidence proves that Agent can gate
final Goal evaluation on its already-owned immutable branch progress; the Goal
does not need a second public evaluator that accepts that progress.

The revised frozen purchase is therefore:

- add `Planning/IntentSemanticEnvelopeExecution.cs`;
- modify only `Agent.cs` for the bounded open-world execution path;
- preserve the existing `Goal.EvidenceEvaluator`, `BranchInventoryEvidence`,
  and `BranchProgressEvidence` unchanged;
- represent `VerifiedBoundedTraversalCompletion` only as an Agent-derived
  semantic condition;
- use only method-local parent frames composed from existing values.

The prior `Goal.BranchProgressEvidenceEvaluator` and `Goal.cs` modification are
superseded and are not authorized implementation scope. Task 1.1 remains DONE;
the rebased Human authorization now permits Task 2.1 to execute within this
revised scope.

## Owner Architecture Prior — Fast Falsification

| Prior | Result | Repository evidence |
|---|---|---|
| Reuse CC-04 + `TypeLevelTraversalSpecification` | CONFIRMED | CP-14 parent slice validates the envelope and truthful open-world representation. |
| Agent remains semantic control plane | CONFIRMED | Charter §5 and frozen CAND-004/CAND-008 assign inventory, semantic depth, branch progress, Container switching, GoalEvidence consumption, and final RunState to Agent. |
| Traversal remains local execution/verification owner | CONFIRMED | Charter §7 and current `Traversal.ExecuteStep` retain Select → Check → Execute → fresh Observe → Verify. |
| No FSM unless executable pressure proves it | CONFIRMED | Run-local bounded traversal frames are sufficient; no FSM pressure was found. |
| Prefer smallest production-shaped short chain | CONFIRMED | SC-U2-MUS-001 below crosses Planning → Agent → Container → Traversal → Environment with a two-sibling depth-1 world. |
| Promote meaningful assets | CONFIRMED | The scenario is classified `NEW_VARIANT`, target level `L2_SHORT_CHAIN_INTEGRATION`, with explicit positive and negative oracles. |

The prior is adopted as the working architecture direction. No repository
evidence contradicts it.

## Existing Semantic Coverage

No new CP or Reality Model is required:

- CP-14 / RM-11: Intent and execution representation remain distinct;
- CC-04: `IntentSemanticEnvelope.Resolved` carries the Goal plus exactly one
  truthful execution representation;
- `TypeLevelTraversalSpecification`: scope, target categories, depth, safety,
  completion requirement, and entry are represented without concrete route;
- CP-07 / RM-06: the declared depth bound constrains discovery;
- CP-04 / RM-02 + CAND-004: sibling progress cannot be discarded or treated as
  complete while a required sibling remains;
- CAND-006: discovered candidate safety remains separately authorized;
- CAND-008: fresh inventory, authorization, dispatch, Container transition,
  positive leaf evidence, GoalEvidence, and completion remain distinct;
- CP-12: Traversal retains local target selection and first fresh effect
  verification when a grounding criterion is present.

This is an accepted-semantics composition gap, not a new Reality Distinction.

## Production-Shaped Falsification

Repository production truth currently cannot execute U2 honestly:

1. `IntentSemanticEnvelope.OpenWorldTypeLevel` preserves the specification but
   has no production execution seam.
2. `Agent.RunAsync` accepts only a concrete `Plan`.
3. `RunBoundedCrossPageDiscovery` can discover a forward P → A → C chain, but
   positive empty inventory with unsatisfied GoalEvidence terminates as Failed;
   it has no bounded parent return and sibling continuation path.
4. CAND-008 explicitly did not purchase generic backtracking, a parent-return
   framework, stack/graph, or leaf-as-completion.
5. The existing `Goal.EvidenceEvaluator(Observation)` must not receive partial
   Agent progress through a hidden closure. It remains sufficient when Agent
   invokes it only after independently deriving
   `VerifiedBoundedTraversalCompletion` from its existing immutable progress
   evidence and the current fresh root Observation.
6. The S0 Capstone completes by test-side composition of a generated concrete
   execution sequence. It is valuable evidence, but it is not a production path
   from the type-level representation and cannot be relabeled as U2.

Therefore a test-only U2 would be vacuous. One bounded public semantic/API
purchase is required.

## Minimum Falsifying Scenario — SC-U2-MUS-001

### Structured Input

```text
Intent: 遍历 Settings 中深度 <= 1 的所有安全配置项
→ IntentSemanticEnvelope.Resolved
→ OpenWorldTypeLevel(TypeLevelTraversalSpecification)
```

The specification declares:

- application/root scope: Settings / SettingsRoot;
- target category: navigable Container;
- maximum semantic depth: 1;
- safety: navigation only, no state-changing interaction;
- completion: exhaustive within scope;
- entry: Settings / SettingsRoot.

It contains no concrete page, target, coordinate, route, work inventory, or
future action list.

### Dynamic World

- Fresh root evidence exposes required safe siblings A and B plus one visible
  dangerous state-changing candidate.
- A and B are absent from any pre-execution concrete Plan.
- A and B each expose positive empty bounded child inventory plus exactly one
  safe parent-return target.
- A world variant may expose a deeper candidate at depth 1; it remains outside
  the declared bound and receives zero dispatch.

### Positive Oracle

```text
fresh root inventory {A, B}
→ authorize and execute exactly one A Tap
→ fresh A evidence, bounded leaf proven
→ exactly one safe parent-return Tap
→ fresh root evidence, record A subtree evidence
→ B remains pending (no false completion)
→ authorize and execute exactly one B Tap
→ fresh B evidence, bounded leaf proven
→ exactly one safe parent-return Tap
→ fresh root evidence, record B subtree evidence
→ derive VerifiedBoundedTraversalCompletion
→ existing fresh GoalEvidence satisfied
→ Agent alone sets Completed
```

Required proof:

- no concrete route or inventory is pre-enumerated;
- every required branch is discovered from fresh accepted evidence;
- A completion remains preserved while B is pending;
- parent-return target membership/authorization is unique before Traversal
  performs local selection and dispatch;
- every branch/return action is followed by fresh Observation and exact Agent
  semantic reconciliation;
- dangerous and beyond-depth candidates receive zero dispatch;
- plan exhaustion, empty inventory, stack/frame exhaustion, or dispatch success
  never independently completes the Run;
- equal inputs replay equal progress, actions, Observations, journal, Trace,
  GoalEvidence, and RunState.

### Negative Oracles

- incomplete/unresolved inventory → zero discovered-branch dispatch;
- rejected/ambiguous parent-return target → no blind Tap, no progress mutation,
  no fabricated completion;
- parent-return post-action evidence does not reconcile to the expected parent
  → no branch completion and explicit failure/escalation evidence;
- A complete while B pending → no final Goal evaluation and no completion;
- visible state-changing candidate → zero dispatch;
- visible candidate below the maximum depth → zero dispatch.

## Minimum Production Purchase Requiring Human Authority

### New public semantic/API surface

Add one public static execution seam in exactly one new production file:

```text
Planning/IntentSemanticEnvelopeExecution.cs

RunOpenWorldAsync(
  Agent,
  IntentSemanticEnvelope.Resolved,
  runId,
  CancellationToken)
```

The seam only validates/destructures the already-authoritative open-world
envelope and forwards the declared boundary to Agent. It is not an NL parser,
Compiler, Planner, route generator, observation owner, or decision authority.

### Existing production behavior adjustment

Modify only `Agent.cs`:

- add one internal bounded open-world run path used by the public Planning seam;
- use a method-local parent frame stack containing only the retained parent
  `Container` and selected child identity; derive semantic depth from stack
  count; add no frame type, mutable field, or owner;
- reuse `BranchInventoryEvidence`, `BranchProgressEvidence`, existing candidate
  authorization, existing Tap, existing Container switching, and existing
  Traversal step execution;
- derive a return target only when the current fresh Observation contains
  exactly one candidate whose text matches the expected parent semantic identity
  and the existing authorization evaluator positively authorizes it; otherwise
  dispatch nothing and fail/escalate explicitly;
- interpret subtree exhaustion only from the conjunction of accepted complete
  inventory, recursively complete child progress, verified fresh parent return,
  and correct frame association; empty inventory alone remains non-completion;
- at root, first derive `VerifiedBoundedTraversalCompletion`, then invoke the
  existing `Goal.EvidenceEvaluator` on the current fresh root Observation;
  only their conjunction may complete the traversal-shaped Goal;
- enforce the specification entry/scope/application/depth/navigation-only
  boundary without manufacturing concrete work.

### Exact delta budget

```text
Production files added: 1
Production files modified: 1 (Agent.cs)
New public types: 1 static execution seam
New Goal values: 0
New enums: 0
New interfaces: 0
New engines/managers: 0
New mutable fields: 0
New mutable owners: 0
FSM/graph/navigation manager/planner/compiler: 0
Agent.RunAsync(Goal, Plan, ...): unchanged
Ownership delta: NONE
Authority delta: NONE
Dependency-direction delta: NONE
Architecture-invariant delta: NONE
Safety-semantic delta: NONE
```

### Expected test scope

- one L2 short-chain Scenario fixture and formal Scenario test file;
- focused Planning/API and insufficient/wrong-representation tests;
- Architecture Guard proving Agent still does not depend on
  `IntentSemanticEnvelope` or `UniClaw.Runtime.Planning`;
- CAND-004/CAND-006/CAND-008/CP-12/CP-14 regression;
- full build/test/consistency/OpenSpec strict validation.

## Architecture Fit

```text
ArchitectureFit: CONFIRMED
OwnerPrior: NOT_CONTRADICTED
Agent: semantic inventory/depth/frame/progress/GoalEvidence control plane
Container: unchanged page-local state owner
Traversal: unchanged local select/check/execute/fresh verify owner
Environment: unchanged Observation/dispatch boundary
Recovery: unchanged
FSM pressure: NONE
```

The rebased production shape fits existing architecture. The stop is solely the
revised public execution seam and bounded Agent behavior purchase reserved for
Human authority by `.ai/development-protocol.md`.

## Evidence Asset Receipt

```text
Classification: NEW_VARIANT
Level: L2_SHORT_CHAIN_INTEGRATION
Source: U2 production-shaped falsification over existing CAND-004/CAND-008/S0 assets
Oracle: explicit positive/negative SC-U2-MUS-001 branches above
Promotion: NOT_PROMOTED — requires authorized implementation and replay pass
```

## Human Authorization Receipt

```text
HUMAN_AUTHORIZE_U2_MINIMUM_USABLE_AGENT_SLICE_IMPLEMENTATION_REBASED
```

## Authorized Rebased Human Implementation Purchase

```text
HUMAN_IMPLEMENTATION_AUTHORIZED_REBASED

Goal:
Authorize the minimum production-shaped U2 path from one resolved
OPEN_WORLD_TYPE_LEVEL envelope through bounded runtime discovery, verified
parent continuation, and existing fresh GoalEvidence.

WhatChangedOrWasDiscovered:
Existing executable assets prove that Goal.BranchProgressEvidenceEvaluator is
not required. Agent can derive VerifiedBoundedTraversalCompletion from existing
BranchInventoryEvidence and BranchProgressEvidence, then invoke the existing
Goal.EvidenceEvaluator on fresh root evidence.

ArchitectureImpact:
NONE

MaterialTradeOff:
The smaller purchase avoids a new public Goal API and hidden progress closure,
while requiring the bounded open-world Agent path to enforce strict invocation
ordering: no Goal evaluation before verified bounded traversal completion.

ExactAuthorizedScope:
Exactly one new Planning/IntentSemanticEnvelopeExecution.cs public static seam
and one bounded Agent.cs control-flow modification. No Goal.cs change, frame
type, graph, FSM, Planner, mutable field, or state owner.
```

No Runtime, architecture, CP, RM, or U2 test implementation was modified by
the reconciliation itself. The rebased Human receipt now authorizes the exact
bounded production purchase above and resumes the same U2 Fast Lane.
