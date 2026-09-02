# R7-A — Composition and Agent Obligation Ownership Audit

> Status: `CONTAINER_RUNTIME_V2_COMPOSITION_AUDIT_COMPLETE`
> Date: 2026-09-01
> Authority: `CONTAINER_RUNTIME_V2_AGENT_CORRECTION_CONSUMER_APPROVED_BOUNDED` + `CONTAINER_RUNTIME_V2_COMPOSITION_CONVERGENCE_REQUIRED`
> Scope: read-only audit before R7 implementation; no production behavior was changed by this audit.

## 1. CURRENT_V2_FLOW_MAP

### 1.1 Current production truth

```text
Authorized Action
  -> existing Agent / Traversal path
  -> old ContainerTransition + Agent.ContainerReconciliation
  -> WorldBelief + ActiveContainerContext + existing Container observation/progress

ContainerRuntimeV2State + ContainerRuntimeV2Reducer + ContainerGraphQuery
  -> no production caller

FastContainerResolver
  -> no production caller

SlowContainerSemanticConsumer / ISlowContainerSemanticAdvisor
  -> no production caller or concrete mandatory provider

ContainerSemanticCorrectionProjector
  -> ContainerObligationReevaluationInput
  -> STOP: no Agent-owned consumer
```

Repository-wide production-symbol search found every V2 invocation only in its defining file or in tests. There is therefore no competing V2 orchestration path to merge. The present defect is absence of a production composition lifecycle, not multiple live V2 coordinators.

### 1.2 Required target flow

```text
caller-supplied authorized action prior + accepted fresh evidence
  -> ContainerRuntimeV2 composition facade
     -> exact lifecycle evidence context
     -> ContainerRuntimeV2Reducer (immutable evidence replacement)
     -> FastContainerResolver (derived working interpretation)
     -> SlowContainerSemanticConsumer (Disabled/Shadow/AsyncAdvisory)
     -> revision/binding validation at consumption time
     -> ContainerSemanticCorrectionProjector
     -> immutable unified read projection
  -> Agent-owned correction consumer
     -> validate exact V2 occurrence + owner obligation binding
     -> replace only existing Agent-owned BranchProgressEvidence when required
     -> derive remaining obligation through existing progress/ledger rules
  -> existing Agent decision / action authorization
```

The facade must be stateless: previous immutable V2 state is an input and the accepted next immutable state is an output. This gives one lifecycle entry without adding another mutable current/trust/correction owner.

### 1.3 Audit questions

| Question | Evidence-backed answer |
|---|---|
| Is `ContainerRuntimeV2.cs` already an orchestration seam? | No. It contains the immutable aggregate, reduction input/preparation, pure reducer, and pure Graph queries. There is no type named `ContainerRuntimeV2` and no Fast/Slow/correction composition. |
| Who directly calls Graph/Fast/Slow/Transition today? | V2 Graph/reducer, Fast, Slow, and correction are called only by component/architecture tests. Existing production Agent paths call the old `ContainerTransitionClassifier` and `Agent.ContainerReconciliation`, not V2. |
| Are there multiple production V2 orchestration paths? | No. There are zero. Existing old reconciliation is a compatibility/legacy runtime path, not a V2 composition path. |
| Is current Container interpreted more than once? | Yes at the repository level: `WorldBelief.SemanticPage`, `ActiveContainerContext.ActiveExecutionContainer`, and `Container.CurrentObservation` serve distinct old responsibilities and can be misread as current physical truth. V2 has one thin `CurrentContainer`, but it has no production owner yet. The R7 facade must not add a state slot or claim that migration task 4.2 is complete. |
| Are Fast/Slow/Correction uniformly revision-bound? | Partially. Fast and Slow carry `SemanticEvidenceRevision`; Slow request/result also bind Observation/Node/Source/Trigger/Transition; correction copies those refs. Missing today are one facade-owned validation point, an explicit RunRef, one accepted occurrence check against the candidate V2 state, and a unified read result. |
| Where does correction stop? | `ContainerSemanticCorrectionProjector.ProjectObligationInput(...)`. The returned value explicitly reports zero mutation/action/recovery/completion and has no Agent consumer. |
| What moves behind the facade? | Production orchestration of reducer, Fast, Slow acquisition/projection, correction projection, checkpoint projection, and unified read projection. |
| Which direct calls remain valid? | Pure reducer/query/resolver/projector unit tests, Slow provider adapters/fakes, and narrow component tests. Guards must target production orchestration, not prohibit these seams. |

### 1.4 Flow reconciliation

| Symbol / responsibility | Decision | Reason |
|---|---|---|
| `ContainerRuntimeV2State` | `KEEP` | Immutable evidence aggregate; no mutable world authority. |
| `ContainerRuntimeV2Reducer` | `MOVE_BEHIND_FACADE` for production; `KEEP` direct pure tests | It is the accepted atomic evidence replacement seam, but callers must not separately compose the lifecycle. |
| `ContainerGraphQuery` | `MOVE_BEHIND_FACADE` for production read projection; `KEEP` pure query tests | Derived evidence only; no route/action API. |
| `FastContainerResolver` | `MOVE_BEHIND_FACADE` for production; `KEEP` direct provider/component tests | Fast remains synchronous and authority-free. |
| `SlowContainerSemanticConsumer` | `MOVE_BEHIND_FACADE` for production; `KEEP` adapter/fake tests | Facade must own request/result freshness orchestration, not provider authority. |
| `ContainerSemanticCorrectionProjector` | `MOVE_BEHIND_FACADE` for production; `KEEP` pure projection tests | Correction must originate from the same lifecycle evidence binding. |
| checkpoint projection | `MOVE_BEHIND_FACADE` as optional derived output | Still no checkpoint state, action, or recovery lifecycle. |
| old `ContainerTransition` / `ActiveContainerContext` path | `DEFER` compatibility migration | It remains the live production execution path. R7 must not silently claim it has been replaced or duplicate its mutable current truth. |
| a second `RuntimeV2Coordinator`/host | `DELETE_DUPLICATE` / forbidden | No buyer; `ContainerRuntimeV2` is the purchased composition name and boundary. |
| mandatory Slow backend | `DEFER` | Human approval covers Disabled/Shadow/AsyncAdvisory seam only. |

No existing production V2 duplicate is eligible for physical deletion in this stage.

## 2. AGENT_OBLIGATION_OWNERSHIP_MAP

### 2.1 Existing owner and meanings

| Concept | Current owner / symbol | Exact meaning | Correction rule |
|---|---|---|---|
| discovered/approved child | `Agent._branchProgress[parent].ApprovedSiblingEvidence` | Accepted parent inventory item with source observation sequence | Preserve unless the correction contract explicitly reopens the exact intended completion; do not create a second inventory. |
| authorized child obligation | `AuthorizedSiblingEvidence` | Agent authorized and dispatched recursive work | Correction cannot invent authorization. |
| completed child obligation | `CompletedSiblingEvidence` | Existing Agent policy recorded verified child-local completion/return evidence | Observing a destination does not add this evidence. |
| traversal pending candidate | Derived in `Agent.OpenWorld` from approved minus completed/boundary-verified/record-only/unresolved | Work still selectable under current policy | No stored pending set. |
| pending authorized obligation | Derived as `AuthorizedSiblingEvidence - CompletedSiblingEvidence` | Authorized work not yet verified complete | This is the relevant meaning for intended C after correction. |
| method-local `visited` | `Agent.OpenWorld` local immutable set | Semantic page already entered in this traversal, used as a duplicate/cycle guard | It is not completion or obligation truth and must not be mutated by the correction consumer. |
| exploration ledger `Visited` | `ExplorationLedgerCompiler` derived union of completed, verified-boundary, and record-only evidence | Reporting projection over accepted dispositions | It is recomputed; it is not a correction write target. |
| Goal completion | caller-owned `GoalEvidence` returned by `EvidenceEvaluator`; Agent completes only when its existing checks pass | Goal satisfaction from fresh evidence | Correction consumer cannot mutate or manufacture it. |
| current execution path | `ActiveContainerContext` compatibility state | Existing execution obligation/path, not V2 physical-current truth | R7 correction of historical attribution must not rewrite it. |

### 2.2 Required distinctions

```text
observed child D
  = V2 TransitionOccurrence / correction evidence

entered D in this traversal
  = method-local cycle policy evidence

authorized obligation D
  = AuthorizedSiblingEvidence[D]

obligation D satisfied
  = CompletedSiblingEvidence[D] written by existing Agent completion policy
```

Therefore:

```text
OBSERVED_CHILD != OBLIGATION_SATISFIED
ENTERED_CHILD != OBLIGATION_SATISFIED
CORRECTION != COMPLETION_EVIDENCE
```

The existing model can express the required distinction without a second ledger. Actual D remains in V2 occurrence/correction evidence. D becomes satisfied only if `CompletedSiblingEvidence[D]` already exists or is later written through existing verified completion policy. The correction consumer must not promote D merely because Slow identified it.

### 2.3 Answers to ownership questions

1. `visited` is overloaded terminology but not one state: OpenWorld's local `visited` means entered semantic identity/cycle guard; ExplorationLedger `Visited` is a derived reporting disposition. Neither is direct obligation satisfaction authority.
2. C's pending truth is derived from the sole `_branchProgress` entry. For an already-authorized intended child, C remains pending exactly when C is in `AuthorizedSiblingEvidence` and absent from `CompletedSiblingEvidence`.
3. Observed D and satisfied D are already representable as separate evidence. What is missing is the consumer that can retract a wrongly attributed completion of C while retaining the observed occurrence as history.
4. Existing immutable replacement primitives exist (`BranchProgressEvidence` records, immutable dictionaries, Agent-only replacement). The current reconciliation commit seam validates only live child-return/boundary transitions and bundles belief/context replacement; it is not safe for a stale historical correction. R7 must add one narrow Agent-owned historical-correction consumer rather than fabricate a current transition.
5. Yes. The consumer can reuse `_branchProgress` as sole mutable owner, create one validated immutable replacement, and remain idempotent by deriving no change when C is already pending. No consumed-correction set is needed.

### 2.4 Correction replacement contract

Traversal correction is permitted to do only this:

```text
before: CompletedSiblingEvidence contains intended C for the exact owner-bound event
after:  the exact C completion attribution is removed
        all A/B/unrelated inventory, authorization, completion, and boundary evidence is byte-for-byte preserved
        observed D remains V2 evidence
        D completion is not added by correction
```

If D was already completed by existing Agent policy, that unrelated valid completion is preserved. If D was only observed, it remains observed only. Directed-entry correction does not have a branch-progress completion to rewrite: it returns an immutable owner view saying intended C remains unsatisfied and requires a separate Agent decision.

The consumer must fail closed when the exact parent scope, intended obligation, ObservationRef/revision, TransitionRef, TriggerOccurrenceRef, source/destination NodeRefs, or owner context does not match. The current `ContainerObligationContext` lacks enough explicit correlation to locate that historical replacement without guessing. R7 may extend this existing context/result contract minimally; it must not parse free text or introduce a mutable correction registry.

## 3. R7 purchase decision and mutable-state budget

### Purchased now

- Stateless `ContainerRuntimeV2` lifecycle facade in the existing `Model/ContainerRuntimeV2.cs` ownership surface.
- One immutable lifecycle input/result and one authority-free unified read projection, only where the existing types cannot express composition output.
- One explicit evidence context that binds Run/Observation/revision/Transition/Trigger/source/destination/current Slice and optional owner context.
- One Agent-owned semantic-correction consumer that reuses `_branchProgress` immutable replacement and emits no action/recovery/completion.
- Focused deterministic/stateful tests and source/architecture guards for C1-C24 where the current bounded seam can exercise them.

### Not purchased

- A new Agent V2 mutable state slot in R7.
- Replacement of `ActiveContainerContext`, `WorldBelief`, or existing production dispatch.
- D completion from observation/correction alone.
- GoalEvidence, action authorization, recovery, checkpoint state, mutable trust, planner, persistent Graph memory, or mandatory provider/backend.

### Mutable-state proof

```text
before Agent correction owners: _branchProgress = 1
after Agent correction owners:  _branchProgress = 1
new V2 current-location fields on Agent: 0
new latest Fast/Slow/correction/trust/checkpoint fields: 0
new visited/pending/completion ledgers: 0
NET_NEW_MUTABLE_TRUTH = 0
```

## 4. Continuous implementation boundary for Luna

The next Worker item may touch only:

- existing V2 model/Fast/Slow/correction contracts needed for stateless composition;
- the existing Agent correction/reconciliation partial for the single consumer;
- focused Unit/Scenario/Architecture tests and this active OpenSpec evidence/tasks.

It must not migrate the live old execution path, create a new coordinator, or change Goal/action/recovery authority. A conflict with those constraints is a return to Sol, not Worker-owned architecture expansion.
