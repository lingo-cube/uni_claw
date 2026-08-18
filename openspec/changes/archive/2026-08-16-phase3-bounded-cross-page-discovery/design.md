## Context

SC-P3-CAND-006 can classify candidates from one fresh Observation as authorized, rejected, or unresolved, but authorization explicitly does not make a candidate required work. SC-P3-CAND-004 can preserve a complete approved sibling inventory and completion evidence, but current production initializes that inventory by intersecting fresh Observation elements with exact Tap targets already present in Plan. SC-P3-CAND-007 retains bounded accepted same-Container Observation evidence and distinguishes continued exploration, positive exhaustion, and unresolved evidence, but it does not decide which observed branches belong to Goal-required work across Containers.

SC-P3-CAND-008 fills only that missing semantic middle. The approved Gate permits one immutable two-field inventory evidence value and one optional Goal criterion. It adds no mutable state field or owner and does not purchase a generic dynamic planner, route structure, or backtracking framework.

## Goals / Non-Goals

**Goals:**

- Represent whether bounded accepted evidence positively proves the complete required child-branch inventory for the active semantic Container.
- Distinguish a proven empty leaf from unresolved inventory completeness.
- Preserve required-work membership as distinct from visibility, authorization, selection, dispatch, and completion.
- Let Agent nominate at most one unresolved required and independently authorized branch through existing Tap mechanics.
- Repeat the bounded decision after fresh evidence reconciles to a new semantic Container without pre-encoding the complete route.
- Enforce Goal scope/depth without treating viewport movement, action count, or Plan index as semantic depth.
- Reuse existing Agent-owned branch progress, Container-owned accepted evidence, Traversal execution, and GoalEvidence authority.
- Replay inventories, progress, actions, journal, Trace, evidence, and final state deterministically.

**Non-Goals:**

- Implement SC-S0-CAPSTONE-001.
- Add a generic planner/re-planner, graph, tree, stack, hierarchy, route model, manager, workflow engine, or FSM.
- Add a new mutable route/frontier/depth field or owner.
- Make authorization imply required-work membership.
- Treat Plan, Observation membership, action dispatch, local completion, or inventory exhaustion as Goal completion.
- Add generic backtracking, a new Back action, Recovery semantics, Fingerprint, Confidence, Vision/VLM behavior, Harness changes, S1/S2/S3 work, or Runtime refactoring.

## Decisions

### Add one immutable inventory evidence value

Add `BranchInventoryEvidence` with exactly two immutable fields:

```csharp
ImmutableDictionary<string, long>? RequiredBranchEvidence
string Reason
```

A non-null map means the supplied bounded accepted evidence positively proves the complete required branch inventory. An empty non-null map positively proves a bounded leaf. A null map means completeness remains unresolved. Map values reference the source Observation sequence supporting each branch identity. `Reason` must be non-empty and deterministic.

The value is Goal-scoped evidence consumed by Agent. It is not authorization, Plan, route state, branch completion, a navigation graph, or GoalEvidence.

Alternative rejected: reuse `CandidateAuthorizationEvidence`. Its frozen meaning is executable eligibility, not required-work membership or inventory completeness.

Alternative rejected: reuse `BranchProgressEvidence` as the evaluator result. That value combines approved inventory with Agent-owned completion evidence; an injected Goal criterion must not manufacture completion state.

Alternative rejected: nullable collection without a reason. It cannot explain unresolved route continuation or produce deterministic evidence without hardcoding Scenario strings into Agent.

### Carry one optional inventory criterion on Goal

Add one optional immutable Goal field with semantic shape:

```csharp
Func<ImmutableArray<Observation>, int, BranchInventoryEvidence>?
    BranchInventoryEvaluator
```

The Observation sequence is the bounded accepted same-Container evidence currently owned by Container; the final item is current and fresh. The integer is Agent-derived semantic depth. The evaluator must be deterministic, side-effect-free, and use only those inputs plus immutable Goal scope captured by the caller. It cannot read or mutate Runtime owners, call Environment, dispatch, authorize a candidate, or set RunState.

Agent is the sole consumer. When the evaluator is absent, fixed-Plan behavior remains unchanged.

Alternative rejected: place the criterion in Container. Container owns page-local evidence but not Goal-required branch scope or cross-Container selection authority.

Alternative rejected: place it in Traversal. Traversal receives an already selected step and cannot decide route inventory or Plan revision.

### Reuse BranchProgressEvidence as accepted inventory state

After a non-null result is validated against the accepted evidence sequences and current semantic page, Agent creates or refreshes the existing `BranchProgressEvidence` snapshot for that parent scope. Existing valid completed-sibling evidence is preserved only when it remains a subset of the newly proven inventory. A null result, stale sequence, conflicting page identity, or ambiguous parent association cannot replace valid progress.

No new route/frontier/depth field is added. Agent remains the sole owner of `_branchProgress`; the new value is an immutable evaluator receipt.

### Keep inventory and authorization separate

For each required branch not already proven complete, Agent locates its source Observation and candidate in deterministic evidence order and invokes the existing SC-P3-CAND-006 authorization criterion.

- required plus authorized: Agent may nominate at most one existing Tap step;
- required plus rejected/unresolved: zero dispatch and route remains unresolved;
- authorized but absent from required inventory: not selected merely because it is executable;
- observed but absent from the complete required inventory: not required work for this bounded Goal scope.

No lower scope may reverse either Agent-owned semantic result.

### Continue only from fresh reconciled Container evidence

After the nominated Tap, existing Container/Traversal mechanics dispatch, observe, and verify. Agent reconciles the post-action Observation. Only a valid newly entered semantic Container can begin another inventory decision. Dispatch success, changed pixels, or a new target string does not itself establish the child Container or its inventory.

The initial immutable Plan may supply initial intent, existing actions, and fixed mechanics, but the formal Scenario's concrete P → A → C forward route is absent. The bounded one-step nomination is not a generic planner: it does not synthesize arbitrary action sequences, persist a route structure, or modify the Plan model.

### Derive semantic depth without a new field

Root depth is zero. A child depth is derived from a fresh accepted parent-to-child semantic transition associated with the existing Agent-owned parent inventory. Plan index, action count, Observation sequence, and viewport movement are not semantic depth. An ambiguous parent relation leaves the inventory decision unresolved.

The injected Goal criterion captures the approved maximum depth and must return a proven empty inventory or an unresolved result at the boundary as supported by evidence. It cannot classify incomplete evidence as an empty leaf. SC-P3-CAND-007 same-Container viewport exploration never increments semantic depth.

### Preserve completion authority

A non-null empty inventory proves only that no required child branch remains in the current bounded scope. It does not set Container completion, parent completion, GoalEvidence, or RunState. Only Agent consumption of independently satisfied GoalEvidence may complete the Run.

Required rejected/unresolved branches, null inventory evidence, ambiguous depth, or conflicting continuity remain explicit non-completion conditions.

## Risks / Trade-offs

- [Risk] An injected evaluator could omit a visible required branch and return a complete map. → Formal fixtures require complete bounded evidence and negative tests force incomplete/ambiguous evidence to null; the evaluator remains Goal-scoped authority, not external-world truth.
- [Risk] Semantic branch text may not be globally unique. → The bounded S0 Scenario requires unique branch identities within one parent scope and source Observation sequences; no global identity algorithm is purchased.
- [Risk] Deriving depth without stored route state can become ambiguous. → Ambiguity must stop as unresolved; implementation may not add a field or route structure without another Gate.
- [Risk] Agent control flow gains another bounded decision path. → Keep it opt-in, one-step-at-a-time, and inside existing Agent authority; record structural pressure without refactoring.
- [Risk] The behavior could drift into generic dynamic planning. → Formal proof uses only existing Tap mechanics, one complete inventory at a time, and no persistent route model or arbitrary action synthesis.
