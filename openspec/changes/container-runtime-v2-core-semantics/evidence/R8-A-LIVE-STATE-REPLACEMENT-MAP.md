# R8-A Live State Replacement Map

STATUS: `CONTAINER_RUNTIME_V2_LIVE_STATE_REPLACEMENT_APPROVED_BOUNDED`

## Current production flow and first divergence

```text
Authorized action / initial observation
→ fresh Observation
→ Reconcile.FromObservation
→ TryPrepareContainerReconciliation where applicable
→ CommitContainerReconciliation
→ independently written _belief
→ ActiveContainerContext execution/path replacement
→ legacy ContainerTransition appended to _trace
→ no ContainerRuntimeV2 production call
```

The first divergence is the existing Agent preparation/commit seam. `ContainerRuntimeV2.Start`, `CompleteSlow`, and `ComposeAsync` have no production caller. The facade and Agent correction consumer are validated contracts, but the live Agent still owns physical current through the old path.

## Ownership replacement map

| Symbol | Before owner / meaning | Before write path | Before read path | After owner / meaning | Compatibility projection | Mutable-truth result | Migration evidence |
|---|---|---|---|---|---|---|---|
| `Agent._belief` | Agent; independently stored latest semantic-current interpretation | 20 assignments across `Agent.SemanticRun`, `Agent.PlanRun`, `Agent.OpenWorld`, `Agent.Recovery`, and `CommitContainerReconciliation` | `Agent.Belief`, drift/recovery/plan decisions, `ContainerContext`, DriverHost snapshot | DELETE mutable field; `Agent.Belief` derives from accepted V2 CurrentContainer node semantic candidate plus current occurrence/Slice revision | `ContainerRuntimeV2State → WorldBelief` pure read | semantic-current mutable owners 1→1; no dual write | L8/L9 + source guard forbidding `_belief` field/assignment |
| `ActiveContainerContext` | Agent; active execution Container and ordered ancestor/child obligation path | `StartRunActiveExecutionContext`, `ReplaceActiveExecutionContainer`, child entry, verified return | execution, completeness, return, loop prevention | KEEP; execution/completeness obligation and path only | exposed beside V2 current to preserve `Observed != Execution` | execution owner 1→1; never physical current | L2/L11 + comments/guards forbid physical-current claim |
| `Container.CurrentObservation` / viewport history | Container; current page-local observation and accepted local history | `Bind`, `AcceptPreparedObservation`, continuity acceptance | grounding, local inventory/model/completeness | KEEP unchanged as Slice/local evidence owner | V2 CurrentSlice stores only opaque ref/evidence link | local evidence owner 1→1; no copied LocalModel | L12-L15 regression + local-owner guard |
| legacy `ContainerTransition` | Agent trace; mixed expected/observed/execution compatibility record | independently classified by `ContainerTransitionClassifier` from legacy input | latest transition/context/DriverHost/evidence catalog | MOVE to append-only compatibility/audit projected from the same accepted V2 occurrence and legacy expectation context | V2 occurrence → legacy trace value | current occurrence owners 1→1; history preserved | L10 + exact ref/revision/observed destination tests |
| `_trace` | Agent; append-only causal/audit history | `DecisionRecord` append sites including transition commit | Agent public read, DriverHost, evidence catalog | KEEP append-only history; transition member becomes compatibility evidence, not current truth | same accepted V2 occurrence may emit one trace record | audit store retained; no current slot | duplicate prevention + deterministic read tests |
| `_branchProgress` | Agent; sole obligation/progress/completion evidence | existing Agent policy and correction consumer immutable replacement | traversal, completion, ledger, DriverHost | KEEP unchanged | none; V2/correction may only call existing bounded Agent consumer | progress owner 1→1 | L12-L15 plus before/after snapshots |
| `ContainerRuntimeV2State` | no production owner; test-only immutable aggregate | reducer/facade test calls | tests/read projection only | CREATE one Agent-owned immutable replacement slot for physical CurrentContainer, Graph occurrence evidence, and TransitionOccurrences | source for Belief/legacy/read projections | replaces old semantic-current physical ownership; no second current slot | L1-L18 + reflection/source guard |

## One-way live flow

```text
fresh accepted Observation + existing authorized context
→ Agent-private immutable lifecycle input builder
→ ContainerRuntimeV2.Start (Fast live, Slow Disabled)
→ ContainerRuntimeV2.CompleteSlow
→ accepted immutable ContainerRuntimeV2State
→ one Agent commit
   ├─ sole physical CurrentContainer
   ├─ evidence-only Graph occurrence state
   ├─ TransitionOccurrence history
   ├─ derived Agent.Belief compatibility read
   ├─ derived legacy transition audit record
   └─ existing ActiveContainerContext execution/path commit
```

No compatibility consumer writes back into V2. No latest Fast/Slow/trust/correction/checkpoint value is stored.

## Atomic staging rule

R8-B may add pure builders/projectors and tests only; it adds no live state slot. R8-C is one atomic ownership flip: introduce the sole Agent V2 state field, remove `_belief`, and replace all accepted-current write paths in the same Worker result. A result that leaves both `_containerRuntimeV2State` and independently assigned `_belief` is rejected even if tests pass.

Initial observation, same-Container refresh, child entry, verified return, unexpected/off-path observation, external boundary, SemanticRun, PlanRun, and Recovery must all either commit V2 state or explicitly remain non-accepted/provisional evidence. No accepted fresh-current path may bypass the sole V2 owner.

## Mutable truth budget

```text
physical current owner: Agent 1 → Agent 1
semantic current mutable owner: _belief 1 → ContainerRuntimeV2State 1
execution obligation owner: ActiveContainerContext 1 → 1
node-local observation owner: Container 1 → 1
progress owner: _branchProgress 1 → 1
current occurrence owner: legacy interpretation 1 → V2 occurrence 1
mutable latest Fast: 0 → 0
mutable latest Slow: 0 → 0
mutable trust: 0 → 0
mutable correction: 0 → 0
mutable checkpoint: 0 → 0
NET_NEW_MUTABLE_TRUTH = 0
```

## KEEP / MOVE / DELETE / DEFER

| Decision | Symbols/capability |
|---|---|
| KEEP | `ActiveContainerContext` as execution/path, Container local observation/history, `_branchProgress`, GoalEvidence, action authorization, recovery, evidence catalog, correction consumer |
| MOVE | physical-current and occurrence ownership to Agent-held V2 state; Belief/legacy transition/ContainerContext become one-way compatibility projections |
| DELETE | `_belief` field and every independent `_belief =` assignment; any physical-current language/guard on ActiveContainerContext; run-global semantic-page duplicate rejection after relation-aware replacement proof |
| DEFER | Slow Shadow/provider, mutable or production checkpoint behavior, cross-run Graph memory, Graph routing, provider/backend selection, external Driver protocol expansion |

## R8 acceptance matrix

L1-L18 are exactly the Human-approved matrix. Required additional failure checks: invalid/stale lifecycle candidate changes neither V2 state, Container local evidence, ActiveContainerContext, progress, nor trace; Slow availability is Disabled; reflection/source guards find exactly one Agent V2 state field and zero `_belief`/latest-assessment fields.

STATUS: `R8_A_OWNERSHIP_MAP_COMPLETE`

NEXT_WORKITEM: one continuous Luna WorkItem for R8-B through R8-I within Agent/Model compatibility and authorized tests, with DriverHost migration held until the Runtime owner flip passes its own Leader Gate.
