## Context

See `proposal.md` for motivation and `docs/analysis/container-runtime-v2-purchase-reconciliation-ledger.md` for the repository-wide purchase audit. The current working tree already contains an unverified ActiveContainerContext/typed-transition implementation, extensive Container-local traversal/completeness behavior, DriverHost projections, and a graduated Fast Semantic candidate provider boundary.

The design is constrained by Architecture v1 and Runtime Contract I-1 through I-14:

- Observation remains evidence, not semantic truth; fresh accepted world evidence has precedence over expectations and historical models.
- Agent/UniAgent retain action, obligation, recovery, and GoalEvidence authority.
- Container retains page-local mutable evidence ownership; Graph must not become a competing local-state owner.
- Existing deterministic/real-run mechanisms are migrated surgically; the dirty working tree and historical evidence are preserved.
- This Large change is staged and reversible. The first Apply slice is a behavior-neutral core model and test seam, not full Agent integration.

## Goals / Non-Goals

**Goals:**

- Replace the old current/active semantic ambiguity with one thin current physical working-location contract.
- Record transition occurrences independently from action expectation, Graph relations, semantic identity trust, and execution obligations.
- Establish a bounded evidence-only Graph model with multi-entry relations and working unproven nodes.
- Reuse existing Container observation/history, reconciliation atomicity, completeness evidence, Fast semantic candidate evidence, and DriverHost read projection.
- Provide minimal provider-neutral seams for Fast resolution and Slow Shadow without purchasing a backend or action authority.
- Prove replacement with an explicit ownership map and `NET_NEW_MUTABLE_TRUTH = 0`.

**Non-Goals:**

- A navigation planner, route search, canonical parent, Graph-owned current location, action authorization, recovery executor, or completion authority.
- A global, Container-local, transition, trust, relation, checkpoint, or recovery FSM in the initial implementation.
- Persistent/cross-run Graph memory, stable item identity, global coordinates, or automatic relation merge learning.
- A concrete LLM/VLM provider, model purchase, cost policy, DSH transport, or external wire-schema change.
- Rewriting existing Agent/Container behavior in one change or deleting the frozen implementation before replacement tests pass.

## Decisions

### D1 — Purchase classes and lifecycle

| Capability | Class | Apply strength | Graduation source |
|---|---|---|---|
| CurrentContainer / expected-observed separation | `EVIDENCE_BACKED_PURCHASE` | production contract + staged migration | r5 replay + stateful tests + fresh real run |
| TransitionOccurrence / identity-trust separation | `EVIDENCE_BACKED_PURCHASE` | production contract + staged migration | unresolved-child/r5 replay + stale tests |
| Slice/LocalModel scope and fresh grounding | `EVIDENCE_BACKED_PURCHASE` | extend existing owners | Z4 + stale-bounds tests + real traversal |
| coverage != semantic/subtree complete | `EVIDENCE_BACKED_PURCHASE` | extend evidence projection | deep Unknown scenarios + real frontier evidence |
| evidence-only ContainerGraph / relations | `ARCHITECTURE_HYPOTHESIS` | Run-local reversible implementation | multi-entry/repeated-run cost and false-model evidence |
| working unproven node | `ARCHITECTURE_HYPOTHESIS` | bounded core implementation | unresolved-first-frame false-retention/fold rate |
| Fast resolver / derived trust | `ARCHITECTURE_HYPOTHESIS` | provider-neutral seam, then experiment | Fast-only false trust/latency/blocker rate |
| Slow Advisor | `ARCHITECTURE_HYPOTHESIS` | interface + Disabled/Shadow first | correction precision, blocker reduction, cost/latency |
| semantic correction → UniAgent | `ARCHITECTURE_HYPOTHESIS` | contract first; behavior later | wrong-child/directed-entry obligation repair |
| checkpoint | `ARCHITECTURE_HYPOTHESIS` | projection contract only | recovery-cost delta after correction works |

No hypothesis becomes mandatory Runtime baseline from unit tests alone.

### D2 — BEFORE / AFTER ownership and mutable-state budget

#### BEFORE (current working tree)

| State / evidence | Owner | Meaning | Problem for V2 |
|---|---|---|---|
| `WorldBelief.SemanticPage` | Agent | accepted semantic location evidence | cannot represent an independent destination before semantic identity resolves |
| `ActiveContainerContext.ActiveExecutionContainer` | Agent | active execution/completeness obligation | name/read model can be mistaken for current physical location |
| `ActiveAncestorPath` | Agent | recursive parent obligation path | useful path evidence but tree/parent interpretation is too strong |
| `Container.CurrentObservation` | Container | current page-local accepted observation | correct local owner; not itself a cross-Container current context |
| `ViewportExplorationObservations` | Container | accepted local observation history | useful LocalModel input, not stable identity |
| `_branchProgress` / ledger / GoalEvidence | Agent | obligations/completion evidence | correct authority; must stay outside Graph |
| `ContainerTransition` history | Agent trace | typed expected/observed reconciliation evidence | closed kinds mix occurrence, expectation, and execution disposition |
| `visited` semantic identities | Agent method | old tree duplicate/cycle guard | rejects legal same-node/different-relation entry |

#### AFTER target

```text
Agent
├─ CurrentContainerState                 # sole current physical working location
│  ├─ NodeRef
│  ├─ CurrentSliceRef
│  └─ EntryContext?
├─ ContainerRuntimeV2 evidence state
│  ├─ immutable Graph snapshot           # nodes/relations/evidence only
│  └─ append-only TransitionOccurrences
├─ existing obligation/progress/GoalEvidence
└─ existing Recovery and decision authority

Container
├─ CurrentObservation / CurrentSlice source
├─ accepted local observation history / LocalModel source
└─ local progress/completeness evidence

Semantic providers
└─ immutable revision-bound assessments only
```

Budget:

```text
current physical-location mutable slots: old ambiguous 2 → V2 authoritative 1
current physical-location owners: Agent 1 → Agent 1
Graph current-location slots: 0 → 0
mutable latest-transition/trust/checkpoint slots: 0 → 0
NET_NEW_MUTABLE_TRUTH = 0
```

The Graph aggregate is mutable evidence through immutable replacement or append-only recording; it is not mutable world truth. Trust, relation assessment, checkpoint, coverage, and latest occurrence are derived projections.

### D3 — Core references are opaque and independent from semantic identity

The core model introduces opaque Run-local references:

```text
ContainerNodeRef
ContainerRelationRef
TransitionOccurrenceRef
ContainerSliceRef
SemanticEvidenceRevision
```

References identify records, not world truth. A `ContainerNodeRef` can exist with no semantic identity candidate. A semantic identity may later bind or reconcile nodes through evidence; identity text is never used as the NodeRef.

Alternative rejected: use `Container.SemanticPageName` as node identity. The existing `Container` requires a proven non-empty semantic name and therefore cannot represent the unresolved-first-frame buyer without fabricating identity.

Alternative rejected: create a global Container registry/manager. That adds a new subsystem and mutable owner before a buyer proves it necessary.

### D4 — First Apply slice is an immutable core model and pure reducer

The first production slice adds a compact model surface under the existing Runtime Model responsibility:

```text
ContainerRuntimeV2State
├─ ContainerGraphSnapshot
├─ CurrentContainerSnapshot?
└─ TransitionOccurrences

ContainerRuntimeV2Reducer.Prepare(...)
→ accepted next immutable state OR explicit no-commit rejection
```

It does not add an Agent field or alter behavior. It proves the semantics and atomic validation with pure deterministic tests before touching the heavily edited Agent path.

The reducer accepts only already-correlated evidence refs and already-authorized effect intent. It performs structural validation, stale-revision rejection, Graph occurrence recording, CurrentContainer replacement, and relation-evidence eligibility. It cannot observe, dispatch, recover, complete, or call a provider.

Alternative rejected: directly replace ActiveContainerContext in the first patch. The current working tree has broad overlapping uncommitted Agent changes and no independently validated V2 contract seam; direct migration would combine architecture purchase, behavior change, and conflict resolution.

### D5 — Minimal Graph responsibilities and interfaces

The Graph uses read/record responsibility separation only after the immutable model is proven:

```text
IContainerGraphReader
  ReadSnapshot()
  FindCandidates(evidence-only query)

IContainerGraphRecorder
  PrepareOccurrenceRecord(previousSnapshot, occurrence, assessment)
```

`IContainerGraphRecorder` returns a proposed immutable snapshot/result; it does not mutate CurrentContainer or authorize anything. A production Agent adapter and deterministic in-memory test implementation are the two real consumers/implementations that justify the seam.

No interface is initially created for:

| Candidate interface | Decision | Reason |
|---|---|---|
| `ICurrentContainerContext` | `REUSE/DEFER` | one Agent owner plus immutable snapshot is sufficient; no replaceable service buyer |
| `IContainerTransitionTracker` | `REUSE` | immutable occurrence history + reducer already provide the seam |
| `IContainerLocalModelReducer` | `REUSE/EXTEND` | existing Container history and normalization functions own this responsibility |
| `IContainerSemanticAssessmentReader` | `DEFER` | Graph/current read projection can expose immutable assessment refs initially |
| `IContainerTrustProjection` | `DO_NOT_CREATE` | trust is a pure derived function, not a service |
| `ITraversalCoverageAnalyzer` | `EXTEND_FIRST` | existing completeness evidence/analyzers are the owner; create only if two consumers emerge |

`NEW_SYMBOL_JUSTIFICATION`: Graph read and record boundaries cannot reuse the existing DriverHost `RunExecutionGraph` because that is a downstream execution-trace projection, not Runtime Container-world evidence, and allowing it to record would reverse authority/dependency direction.

### D6 — CurrentContainer stays thin

```text
CurrentContainerSnapshot
├─ NodeRef
├─ CurrentSliceRef
└─ EntryContext?

ContainerEntryContext
├─ SourceNodeRef
├─ EntryTransitionOccurrenceRef
└─ EntryRelationRef?     # only when a relation has evidence; optional
```

No LocalModel, identity truth, history, parent, completion, obligation, plan, action, recovery, or trust is copied into CurrentContainer.

On r5-style evidence, CurrentContainer advances to a working node for the fresh accepted location while the incomplete Display obligation remains in the existing Agent obligation/progress evidence. The old ActiveExecutionContainer may temporarily serve only as a compatibility projection of pending execution obligation; it is never the V2 current physical location.

### D7 — TransitionOccurrence and relation recording are separate

```text
ContainerTransitionOccurrence
├─ OccurrenceRef
├─ SourceNodeRef?
├─ TriggerOccurrenceRef?
├─ DestinationNodeRef?
├─ FreshObservationRef
├─ EvidenceRevision
├─ BoundaryObservation     # same/new/transient/ambiguous/unresolved evidence
└─ IsCompleted
```

The first version avoids a large outcome ontology. `IsCompleted` means the physical occurrence is sufficiently observed, not that identity is trusted or the relation is normal. Fast/Slow classifications are separate assessment records.

Graph relation recording requires an explicit eligibility assessment. Every occurrence is retained; an off-path or transient occurrence can remain evidence without creating a reusable normal relation.

Compatibility migration maps old TransitionRef/EvidenceRef/AssetRef into occurrence refs. Old kinds remain readable during migration but do not become V2 relation truth.

### D8 — Working node reconciliation is evidence-driven

The reducer supports four structural outcomes for an `INITIALIZED` node:

1. retain as a distinct working node;
2. bind/reconcile to an existing node with explicit evidence;
3. fold the provisional evidence into the source/current node when same-Container is proven;
4. remain unresolved.

No outcome deletes original evidence. Binding/folding is a proposed immutable state transition with falsifiable refs, not a semantic-name dictionary shortcut.

### D9 — EntryContext and return

A node carries no parent. Current entry context is an occurrence-relative value. The execution path is a derived ordered projection of entry occurrences/current obligations, not Graph topology.

Back handling:

```text
Current EntryContext
→ derive ReturnExpectation
→ authorized Back action (existing authority)
→ fresh accepted observation
→ TransitionOccurrence
→ verify actual CurrentContainer
```

Forward relations are not reversed to prove Back. A return occurrence may later support its own relation only if a real buyer and evidence assessment choose to record it.

### D10 — Existing Container is extended before a parallel LocalModel abstraction

The current `Container` remains the unique owner of current local observation, accepted viewport observations, bindings, state beliefs, progress, and local completeness. During migration:

- `CurrentObservation` supplies CurrentSlice.
- `ViewportExplorationObservations` supplies accepted lifecycle LocalModel evidence.
- existing normalizers and completeness analyzers remain in place.
- NodeRef linkage is added only after the pure core model is validated.

`NEW_SYMBOL_JUSTIFICATION` for a future explicit LocalModel value would be required if existing Container history cannot expose immutable inventory/coverage evidence to Graph/assessment consumers without leaking mutable Container references. No such value is created in the first slice.

### D11 — Coverage is a derived evidence view, not an FSM

Existing `ContainerInventoryCompletenessEvidence`, discovery epoch, viewport exhaustion, stability confirmation, and post-completeness validation are reused. The migration adds a derived view that separates:

```text
CoverageComplete
SemanticResolution
SubtreeCompletion
GoalCompletion
```

Unknown items remain explicit semantic obligations. The current local-complete flag is not silently reinterpreted as coverage-complete until its evidence contract is migrated.

### D12 — Fast resolver and trust seam

After core state integration, add:

```text
IFastContainerResolver
  Resolve(FastContainerResolutionRequest) → FastContainerAssessment
```

The request contains immutable action context, Source/Current refs, fresh Slice evidence, and authority-free Graph candidates. It does not accept Goal completion or action authorization callbacks. The result is revision-bound and includes boundary interpretation, candidate semantics, support, and hard conflict.

The existing Fast Semantic provider, deterministic/no-op test provider, and future BGE-backed provider are consumers/implementations that justify the interface. Existing candidate/evidence/fusion types are reused; no parallel vector index or embedding abstraction is created.

Fast Trust is a pure projection:

```text
IndependentContainerSupport
AND SemanticSupport
AND NoHardConflict
```

No mutable `_latestFastTrust` field is allowed.

### D13 — Slow Advisor seam begins Shadow-only

```text
ISlowContainerSemanticAdvisor
  AssessAsync(SlowContainerSemanticRequest, CancellationToken)
    → SlowContainerSemanticAssessment

SlowContainerSemanticMode
  Disabled | Shadow | AsyncAdvisory
```

Every request/result binds ObservationRef, evidence revision, NodeRef, SourceNodeRef, TriggerOccurrenceRef, and TransitionOccurrenceRef as applicable. Slow outputs scene, Container/trigger/relation semantics, evidence usefulness, mismatch, and suggested disposition only.

`NEW_SYMBOL_JUSTIFICATION`: the existing Fast `ISemanticProvider` returns bounded semantic candidate evidence and its frozen scope explicitly excludes Slow async scene/correction output. Extending it would mix latency/lifecycle/result responsibilities and violate the prior boundary; a separate advisor seam has an independent Shadow implementation and test fake buyer.

Slow is not wired to production action/control flow until Shadow evidence justifies a later consumption gate. Provider/backend selection is outside this change.

### D14 — Semantic correction reaches UniAgent as fact, not command

Accepted corrections are immutable revision-bound facts. Runtime owns semantic reconciliation; UniAgent consumes the corrected fact and recomputes its existing obligation projection. No `REVISIT`, `REENTER`, `RESET`, or `RECOVER` Slow state machine is introduced.

The bounded Agent consumer Human Gate has passed as `CONTAINER_RUNTIME_V2_AGENT_CORRECTION_CONSUMER_APPROVED_BOUNDED`. The consumer reuses the existing Agent-owned `_branchProgress` immutable replacement and may retract only an exactly bound, wrongly attributed intended-child completion. Observed D remains V2 occurrence/correction evidence and does not become completed merely because it was observed. A valid D completion written earlier or later by existing Agent policy is preserved. Directed-entry correction returns an unsatisfied intended obligation view and requires a separate Agent decision.

The consumer proves:

- exact evidence binding;
- stale rejection;
- traversal mis-click keeps/reopens intended C pending without promoting observed D;
- directed-entry wrong branch keeps target C unsatisfied;
- duplicate identical correction is idempotent without a consumed-correction registry;
- A/B and unrelated branch/boundary progress is unchanged;
- zero Slow action/recovery/completion authority.

It does not change GoalEvidence, existing action authorization, recovery policy, or current physical truth.

### D15 — Checkpoint remains derived and deferred

Checkpoint is `last sufficiently confirmed node on the correct execution path`. It is not stored in Graph or an FSM. The interface may carry a proposed NodeRef after correction is proven, but no production checkpoint state or recovery behavior is implemented until R6 evidence shows a buyer.

### D16 — Verification ladder

| Level | Required proof |
|---|---|
| E1 contract | strict OpenSpec, source-shape guards, no forbidden authority/mutable state |
| E2 deterministic | multi-entry same-node, r5 current/obligation split, working unresolved node, off-path occurrence, stale bounds, coverage+Unknown |
| E3 stateful async | stale Slow result, Fast/Slow conflict, Shadow no-effect, occurrence-to-relation separation, atomic rollback |
| E4 integration | Agent/Container migration, DriverHost read projection, UniAgent correction boundary without action bypass |
| Production | fresh Phase 2.6 campaign: blocker migration, completion/depth, wrong-branch correction, Unknown rate, false identity/trust, latency/cost |

Focused green is never sufficient for graduation.

### D17 — ContainerRuntimeV2 is a stateless composition facade

The composition Human decision `CONTAINER_RUNTIME_V2_COMPOSITION_CONVERGENCE_REQUIRED` purchases one lifecycle entry named `ContainerRuntimeV2` in the existing `Model/ContainerRuntimeV2.cs` ownership surface. It sequences the already purchased immutable reducer/Graph, Fast resolver, Slow acquisition/projection, correction projection, optional derived checkpoint, and unified read projection.

Previous immutable state and exact lifecycle evidence are inputs; the accepted next immutable state and assessment/projection values are outputs. The facade stores no latest state, trust, Slow result, correction, checkpoint, visited, pending, action, recovery, or Goal value. Pure reducer/query/resolver/provider/projector seams remain directly callable by unit tests and adapters.

One lifecycle evidence context binds RunRef, ObservationRef/revision, TransitionOccurrenceRef, TriggerOccurrenceRef, source/destination NodeRefs, CurrentSliceRef, and optional owner obligation context. Every supplied Fast/Slow/reducer/correction value must match that context or the lifecycle fails closed. A Slow result for O17 may remain historical evidence after O23 but cannot replace O23 current-world projection.

`NEW_SYMBOL_JUSTIFICATION`: no current type owns cross-component lifecycle correlation or a unified authority-free result. Extending the reducer would mix structural state validation with asynchronous semantic acquisition; extending Slow would give a provider-side seam orchestration responsibility. A stateless facade composes them without a second mutable subsystem.

### D18 — Bounded live state replacement is approved

The Human decision `CONTAINER_RUNTIME_V2_LIVE_STATE_REPLACEMENT_APPROVED_BOUNDED` authorizes migration of the production physical-current path at the existing `TryPrepareContainerReconciliation` / `CommitContainerReconciliation` atomic seam.

The replacement is one-way:

```text
accepted immutable ContainerRuntimeV2State
→ derived WorldBelief compatibility projection
→ derived legacy ContainerTransition audit projection
```

It is not dual-write reconciliation. The Agent may own one immutable `ContainerRuntimeV2State` slot. The old `_belief` field must be removed in the same atomic implementation stage that introduces that slot; `Agent.Belief` remains only as a derived compatibility read. `ActiveContainerContext` remains the execution/completeness obligation and ordered path, never physical current. `Container.CurrentObservation` remains the node-local fresh Slice/evidence owner. `_branchProgress`, GoalEvidence, action authorization, recovery, and Driver authority remain unchanged.

The first live slice runs Fast through the stateless facade and fixes Slow mode to `Disabled`. It stores no latest Fast, Slow, trust, correction, or checkpoint value. A Slow provider, Shadow/AsyncAdvisory production consumption, and any blocking semantic authority remain outside this decision.

The implementation order is staged but the ownership invariant is atomic:

1. Add/test pure Agent-private lifecycle-input and compatibility-projection helpers without adding a live V2 slot.
2. In one replacement stage, add the sole Agent V2 state slot, remove `_belief`, and route every accepted initial/fresh/recovery observation through the V2 preparation/commit path.
3. Derive legacy transition/read compatibility from the same accepted occurrence; do not classify the world twice.
4. Migrate DriverHost to the authority-free V2 projection and keep explicit unavailable classifications where evidence is absent.

Mutable truth budget:

```text
physical-current owners: Agent 1 → Agent 1
semantic-current mutable owners: _belief 1 → V2 state 1
execution-obligation owners: ActiveContainerContext 1 → 1
progress owners: _branchProgress 1 → 1
mutable latest trust/correction/checkpoint slots: 0 → 0
NET_NEW_MUTABLE_TRUTH = 0
```

## Risks / Trade-offs

- [Working nodes may accumulate false hypotheses] → Keep them Run-local, evidence-backed, foldable, and measurable; no identity/action/completion implication.
- [Graph becomes a hidden planner] → Expose no route/action APIs; keep recorder evidence-only; add architecture guards and adversarial tests.
- [CurrentContainer duplicates belief or ActiveContainerContext] → Stage migration with one ownership map, compatibility projection only, and delete old current semantics before behavior graduation.
- [Same-node re-entry causes traversal loops] → Move loop prevention to explicit Agent obligation/relation evidence; do not use semantic identity equality as Graph existence truth.
- [Fast false trust] → Require no-hard-conflict, preserve fresh evidence precedence, measure false trust, and keep authorization gates independent.
- [Slow latency/cost/false correction] → Disabled/Shadow first, revision-bound stale rejection, falsifier metrics, no provider purchase in core.
- [Coverage-complete with Unknown hides meaningful work] → Expose semantic unresolved obligation separately; Goal/subtree completion remains independently gated.
- [Dirty-tree overlap causes accidental rewrite] → First Worker owns new core-model/test files only; Agent/Container migration is a later WorkItem after revalidation.
- [Interface proliferation] → Use the explicit REUSE/EXTEND/CREATE matrix in D5 and require `NEW_SYMBOL_JUSTIFICATION` for every new interface/type.

## Migration Plan

1. Freeze old purchase and preserve all history/evidence.
2. Add immutable V2 core references, snapshots, occurrence model, and pure reducer with no Runtime behavior change.
3. Add evidence-only Graph read/record seam and in-memory implementation after core model tests pass.
4. Adapt existing transition/read projection to emit V2 occurrences while retaining old fields as compatibility read-only projections.
5. Introduce the sole Agent CurrentContainer slot and migrate accepted fresh observations; keep execution obligations in existing progress evidence.
6. Link existing Container local state to NodeRef and migrate LocalModel/coverage projections without duplicating mutable state.
7. Replace run-global duplicate semantic identity rejection with relation/obligation-aware safety; delete compatibility visited semantics after regression proof.
8. Add Fast resolver/derived trust, then Slow Disabled/Shadow seam.
9. Compose reducer/Graph/Fast/Slow/correction/read projections through the stateless `ContainerRuntimeV2` facade and prove exact evidence binding.
10. Consume revision-bound correction through the single bounded Agent owner; do not add production recovery behavior.
11. Run E2/E3/E4 gates, then a fresh Phase 2.6 campaign. Archive/graduation requires explicit Human lifecycle authorization.

Rollback at every stage removes or disables the newest adapter/seam and returns consumers to the frozen behavior. Historical evidence and prior tests are retained. Rollback must never restore rejected semantics as new authority.

## Open Questions

- Whether working NodeRef generation should use RunId + first accepted ObservationRef or a separate monotonic Run-local sequence can be decided in the pure model slice; either choice is opaque and does not change the spec.
- Whether an accepted return occurrence should ever support a long-lived Graph relation remains deferred until a repeated-reuse buyer exists.
- Concrete Slow provider, model, latency budget, cost budget, and deployment mode remain separate product/dependency decisions.
