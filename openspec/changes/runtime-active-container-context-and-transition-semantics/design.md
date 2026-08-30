## Context

See `proposal.md` for motivation. This design is grounded in the current working-tree implementation (including pre-existing uncommitted Runtime/DriverHost edits) and the frozen Architecture v1 / Runtime Contract boundaries:

- Agent owns WorldBelief, current Container management, Container switching, cross-Container progress, and high-level decisions.
- Container owns page-local `CurrentObservation`, local progress, and local completeness.
- Observation is evidence; WorldBelief is current accepted world belief; Runtime execution state is separate.
- Existing open-world execution uses `_activeContainer`, a method-local `parents` stack, a separately maintained `ancestry` set, a distinct `visited` set, and Agent-owned `_branchProgress`.
- `ConfirmScrollStabilityAsync` currently resolves a fresh page locally and returns `null` on a different known page before assigning `_belief`; this is the r5 First Divergence.
- The active DriverHost snapshot exposes `Belief.SemanticPage`, but active Container/observation remains unavailable and transition meaning is inferred from `DecisionRecord.Reason` strings.

This is a proposal-only Large change. No production or test implementation is authorized.

## Goals / Non-Goals

**Goals:**

- Replace scattered active execution state with one minimal Agent-owned context.
- Preserve fresh WorldBelief as the only current observed-location truth.
- Make legal `Observed != Execution` states explicit.
- Replace implicit/string transition meaning with immutable typed results.
- Make location acceptance and permitted execution/progress updates one atomic commit.
- Reduce mutable truth and preserve every existing owner and normal-path authority.
- Provide a truthful downstream read model for DriverHost and Runtime Debugging Toolchain.

**Non-Goals:**

- NavigationGraph, ContainerManager, page registry, persistent topology, route planner/search, or world-tree exhaustion.
- Automatic recovery, automatic re-entry, traversal strategy changes, new parent-return action, or new sibling selection.
- New action, destination, recovery, completion, GoalEvidence, or lifecycle authority.
- Copying Container observation, completeness, BranchProgress, BoundaryRelation, or history into the context.
- Implementing CLI/TUI commands in this gate.

## Decisions

### D1 — BEFORE_STATE_OWNERSHIP_MAP

`Mutable` below means the storage/reference may change during a Run; immutable values inside a mutable slot are still counted by their semantic role.

| Current state | Semantic meaning | Mutable / derived | Owner | Lifecycle | Update point | Current readers | May disagree? | Candidate fate |
|---|---|---|---|---|---|---|---|---|
| `Agent._belief.SemanticPage` + source sequence/evidence | Fresh accepted belief about real UI semantic location | Mutable slot containing immutable `WorldBelief` | Agent | Run-local | Every accepted `Reconcile.FromObservation` path | Agent partials, public `Belief`, DriverHost snapshot | Yes, may legally differ from execution obligation | **KEEP** as `CurrentObservedLocation`; do not copy |
| `Agent._activeContainer` | Container currently used for execution/local obligation | Mutable reference | Agent | Run-local | Startup, child entry, known-page switch, return, recovery | Agent partials, trace ContainerId derivation | Today can disagree silently with `_belief` | **MOVE_INTO_CONTEXT**, then **DELETE** field |
| `RunOpenWorldAsync.parents` | Ordered `(parent Container, entered child obligation identity)` return chain | Mutable method-local stack | Agent execution method | One open-world invocation / Run | Push on admitted child entry; pop on verified return | depth, expected parent, return mechanics | Can drift from `_activeContainer` if updates split | **MOVE_INTO_CONTEXT** as `ActiveAncestorPath`, then **DELETE** local stack |
| `RunOpenWorldAsync.ancestry` | Membership of current active recursive chain for cycle rejection | Mutable method-local immutable-set slot | Agent execution method | One open-world invocation / Run | Add child on entry; remove child on return | inventory admission and child-entry cycle checks | Duplicates parent stack + active Container | **DERIVE** from path + active execution; **DELETE** slot |
| `RunOpenWorldAsync.visited` | Historical unique page identities accepted during this Run | Mutable method-local immutable-set slot | Agent execution method | One open-world invocation / Run | Add on accepted unique child entry | duplicate identity rejection | Yes; historical by design, not current world truth | **KEEP** outside context |
| `Agent._branchProgress` | Existing per-parent authorized obligations, completion evidence, boundary obligations/dispositions | Mutable Agent slot containing immutable snapshots | Agent | Run-local | inventory acceptance, authorization, verified return/boundary updates | Agent flow, ledger compiler, public read snapshot | May show incomplete child while parent is observed; legal | **KEEP**; context/transition only reference it |
| `BranchProgressEvidence.VerifiedBoundaryDispositions` | Fresh-evidence proof that an authorized external boundary returned to expected parent | Immutable values replaced through `_branchProgress` | Agent via progress ledger | Run-local evidence | exact-parent return after boundary | pending filtering, ledger/read model | Does not equal generic Container transition | **KEEP** |
| `BoundaryRelation` / `BoundaryObligation` | Authorized external crossing provenance and pending return obligation | Immutable values; enclosing progress slot changes | Agent via progress ledger | Run-local evidence | external foreground admission / verified return | external boundary handler, progress/ledger | May coexist with `EXTERNAL_EXIT`; distinct semantics | **KEEP** |
| local `page`, `childPage`, `newPage`, `currentBelief` values | Operation-local projection of observation/belief | Derived local | No persistent owner | Call/iteration | computed from fresh input/current belief | immediate branch | Can be stale if held across acceptance | **DERIVE** at use; never context fields |
| `ScrollStabilityClassification.LeftContainer`, reason strings, `_lastStabilityExhaustionDetail` | Quiescence outcome and string handoff to terminal reason | Enum/local plus mutable Agent string handoff | Agent | Run-local | stability confirmation / caller consume-clear | failure construction, trace reader | Can carry location semantics without updating belief | Typed transition replaces location meaning; immutable result replaces handoff; human text becomes **TRACE_ONLY** |
| `Container.CurrentObservation` | Latest accepted page-local observation for that Container | Mutable Container field | Container | Container instance within Run | Bind/same-Container acceptance/verified return acceptance | Container execution/grounding/completeness; Agent reads snapshot | May differ from global observed location when Container is inactive; legal | **KEEP**; never copied into context |
| `Container.IsLocalComplete` | Existing page-local completion state | Mutable Container field | Container | Container instance within Run | existing step execution/Bind | return eligibility, progress update | Must not be inferred from location | **KEEP**, reference only |
| `BranchProgressEvidence.IsSubtreeComplete*` | Derived coverage over existing obligation/completion evidence | Derived | No new owner | Run-local projection | computed on read | Agent/ledger/tests | Independent of observed parent location | **DERIVE / KEEP**, never copy |
| `Agent._suspendedContainer` / `_suspendedStepIndex` | Recovery bookmark, not current active execution truth | Mutable recovery bookkeeping | Agent | bounded recovery attempt | drift/recovery flow | recovery resume/failure detail | May differ from active execution during recovery by design | **KEEP**; Recovery change is excluded |
| `Agent._navigationEvidence`, `DecisionRecord` history | Accepted observation/decision history for projection | Append-only evidence | Agent | Run-local history | accepted transitions/decisions | DriverHost/Harness/debug | Historical context may differ from current truth | **TRACE_ONLY / KEEP** |
| `discoveryEpoch` and related exploration dictionaries | Frozen discovery/completeness evidence and bounded exploration bookkeeping | Mutable method-local maps | Agent execution method | open-world Run | exploration acceptance/revisit | normalization/completeness | Not current location or route state | **KEEP** outside context |

Evidence anchors in current code: `Agent.cs` fields; `Agent.OpenWorld.cs` initialization/entry/return/stability branches; `Agent.Recovery.cs` recovery bookmarks; `Container.cs` observation/completeness owner; `WorldBelief.cs`; `BranchProgressEvidence.cs`; `BoundaryRelation.cs`; DriverHost `AgentStateSnapshot`/`RunSnapshotProjector`.

### D2 — Exact mutable-truth count and acceptance budget

Two counts are frozen so field packaging cannot hide semantic duplication:

| Count | BEFORE | AFTER | Delta |
|---|---:|---:|---:|
| Semantic current location/execution facts | 4: observed belief, active execution, active parent path, separately maintained ancestry membership | 3: observed belief, active execution, active ancestor path (ancestry derived) | **-1** |
| Mutable storage slots for those facts | 4: `_belief`, `_activeContainer`, `parents`, `ancestry` | 2: `_belief`, `_activeContainerContext` | **-2** |
| Mutable semantic owners | 1 Agent | 1 Agent | **0** |
| Mutable diagnostic handoff slots | 1 `_lastStabilityExhaustionDetail` | 0; immutable operation result | **-1** |

`visited`, Container local observation/completeness, `_branchProgress`, boundary evidence, recovery bookmarks, and trace history are intentionally excluded from the first count because they are historical evidence, page-local state, progress evidence, recovery bookkeeping, or history rather than competing current Container-location truth. They remain listed in the BEFORE/AFTER maps to prove they are not silently copied.

Acceptance is therefore:

```text
NET_NEW_MUTABLE_TRUTH = -1
OWNER_COUNT_DELTA = 0
MUTABLE_STORAGE_SLOT_DELTA = -2
```

If implementation keeps `_activeContainer`, `parents`, or `ancestry` after adding `_activeContainerContext`, the design fails and Apply must stop.

### D3 — Minimal ActiveContainerContext

Conceptual shape:

```text
ActiveContainerContext
├─ ActiveExecutionContainer
└─ ActiveAncestorPath[]
   └─ existing (ParentExecutionContainer, EnteredChildObligationIdentity)
```

- The value is structurally immutable; updates replace the Agent-owned context reference.
- Existing Container references remain references to Container-owned local state; putting them in the context/path does not transfer or duplicate Container ownership.
- `ActiveExecutionContainer` means the Container whose traversal/completeness obligation the Agent is currently responsible for.
- `ActiveAncestorPath` is root-to-immediate-parent order. It is bounded by active recursion, appended only on already-authorized child entry, popped only on verified return, and discarded with the Run.
- Ancestry membership is `path parent semantic identities ∪ active execution semantic identity`.
- Semantic depth is the path length.
- The existing `visited` set remains separate historical identity-safety evidence.
- `CurrentObservedLocation` is not a context field. It is the semantic role of `WorldBelief.SemanticPage` plus its source/freshness evidence; Unknown remains allowed.

Alternative rejected: add `CurrentObservedLocation`, completeness, latest transition, visited, recovery bookmark, or BranchProgress into the context. Each duplicates an existing truth/evidence owner and risks I-13 God Context growth.

Alternative rejected: store only semantic identities and introduce a Container registry to recover instances. That would buy a ContainerManager/page registry without a buyer.

### D4 — AFTER_STATE_OWNERSHIP_MAP and exact replacement plan

```text
Agent
├─ WorldBelief
│  └─ CurrentObservedLocation (existing SemanticPage + evidence/freshness)
├─ ActiveContainerContext
│  ├─ ActiveExecutionContainer
│  └─ ActiveAncestorPath (ordered existing parent/child-obligation entries)
├─ visited identity evidence (open-world historical, Run-local)
├─ BranchProgress (existing completeness/boundary evidence)
├─ recovery bookmarks (existing, Recovery-only)
└─ append-only Decision/transition evidence

Container
├─ CurrentObservation
└─ local progress/completeness

DriverHost / Debug Toolchain
└─ immutable read-only projection (no authority)
```

| BEFORE item | AFTER source | Action |
|---|---|---|
| `_belief.SemanticPage` | `WorldBelief.SemanticPage` as `CurrentObservedLocation` | KEEP; require every accepted fresh grounded/Unknown belief to commit honestly |
| `_activeContainer` | `ActiveContainerContext.ActiveExecutionContainer` | MOVE, replace every read/write, DELETE old field |
| `parents` stack | `ActiveContainerContext.ActiveAncestorPath` | MOVE exact parent + child-obligation values, DELETE local stack |
| `ancestry` set | derived ancestry view | DELETE and DERIVE; no cache |
| `visited` set | existing run-local visited evidence | KEEP outside context |
| `_branchProgress` | same existing field/value contract | KEEP |
| verified-return / BoundaryRelation evidence | same progress ledger | KEEP |
| local page variables | fresh belief/context reads | DERIVE |
| left-container semantic string | `ContainerTransition.Kind` | REPLACE semantic use; render message from typed result only |
| `_lastStabilityExhaustionDetail` | immutable viewport/reconciliation operation result | DELETE mutable handoff |
| `Container.CurrentObservation` | same Container owner | KEEP; accept only through validated commit plan |
| completeness/subtree | same Container/progress evidence | KEEP/DERIVE; transition stores only ref |
| `_suspendedContainer` | same Recovery bookmark | KEEP; not folded into context in this change |

### D5 — Immutable ContainerTransition contract

Conceptual schema:

```text
ContainerTransition
├─ TransitionRef                 # derived RunId + FreshObservationRef; not mutable identity truth
├─ FromObservedLocation          # previous accepted WorldBelief location, nullable
├─ ToObservedLocation            # candidate accepted location, nullable/Unknown
├─ ActiveExecutionContainer      # pre-commit semantic identity
├─ ActiveParentAtObservation?    # immediate parent identity when present
├─ FreshObservationRef           # existing observation sequence/ref
├─ CompletenessRef?              # ref to existing Container/progress evidence
├─ Kind                          # closed vocabulary
└─ Disposition                   # closed vocabulary
```

`TransitionRef` has a Debug Toolchain buyer and is deterministically derived; it creates no mutable truth. `PreviousContainer` is not stored. Full Observation, screenshot bodies, completeness booleans, route edges, policy decisions, and recovery decisions are excluded.

Closed `Kind` vocabulary:

1. `SAME_CONTAINER`
2. `ENTER_CHILD`
3. `VERIFIED_RETURN_TO_ACTIVE_PARENT`
4. `PREMATURE_RETURN_TO_ACTIVE_PARENT`
5. `KNOWN_NON_PARENT_TRANSITION`
6. `EXTERNAL_EXIT`
7. `UNKNOWN_TRANSITION`

`SAME_CONTAINER` is retained because the atomic seam processes every accepted fresh observation; an explicit no-location-change result avoids a parallel null/string path and supports behavior-neutral freshness projection.

Closed `Disposition` vocabulary:

1. `OBSERVED_AND_EXECUTION_ADVANCED`
2. `OBSERVED_AND_EXECUTION_RESUMED`
3. `OBSERVED_EXECUTION_PRESERVED`
4. `NO_COMMIT_FAIL_CLOSED`

Disposition describes the committed state effect, not the next policy. `KNOWN_DESTINATION != AUTHORIZED_DESTINATION` and `TRANSITION_CLASSIFICATION != RECOVERY_AUTHORIZATION` remain invariant.

### D6 — Atomic reconciliation and commit model

```text
Fresh Observation
→ validate freshness / evidence boundary
→ build candidate WorldBelief (known or Unknown)
→ read and validate ActiveContainerContext
→ read existing completeness/progress snapshot by reference
→ classify immutable candidate ContainerTransition
→ prepare candidate context + optional existing progress replacement
→ prepare optional Container observation acceptance
→ validate all invariants
→ one synchronous Agent-owned commit
   ├─ accept permitted Container observation (prevalidated total operation)
   ├─ replace WorldBelief
   ├─ replace ActiveContainerContext
   ├─ replace existing progress snapshot when verified-return contract permits
   └─ append immutable transition evidence
→ Agent independently chooses next policy
```

Preparation performs all work that may fail: semantic grounding, identity comparison, completeness lookup, path consistency, authorization receipt checks, exact-parent/continuity proof, and construction of immutable replacements. The commit contains no `await`, device I/O, observation, action, recovery, policy choice, or validation that can ordinarily fail. Existing Container acceptance mutators are invoked only after all preconditions are proven and must be total for the prepared input.

Rollback/fail-closed rules:

- Grounding creates a candidate, not acceptance. If classification or context validation fails, no live value changes; emit only non-authoritative failure diagnostics with `NO_COMMIT_FAIL_CLOSED`.
- If a transition validates, accepted WorldBelief cannot be skipped. This prohibits `FRESH_GROUNDED_LOCATION_ACCEPTED AND ACCEPTED_WORLD_BELIEF_NOT_UPDATED`.
- A prepared commit must never partially apply belief while leaving context/progress at an unvalidated state, or vice versa.
- Unexpected runtime exceptions before commit leave all live state unchanged. Ordinary commit operations must be designed non-throwing after preparation; catastrophic process failure is outside logical rollback claims.
- No shared mutable transaction owner is introduced.

### D7 — Completeness-reference rules

- `CompletenessRef` points to existing Container-local or `BranchProgressEvidence` evidence used for classification.
- No completeness boolean, subtree state, obligation array, or progress map is copied into transition/context.
- `RETURN_TO_PARENT != SUBTREE_COMPLETE`.
- `OBSERVED_AT_PARENT != VERIFIED_RETURN`.
- `PREMATURE_RETURN != RECOVERY_PERMISSION`.
- Verified return can resume execution only when the pre-existing completion/obligation contract and fresh exact-parent continuity both pass.
- Premature return preserves unresolved execution obligation; it neither invalidates nor completes existing evidence.

### D8 — Seven buyer mappings

| Case | BeforeObserved | BeforeExecution | ActiveAncestorPath | Fresh evidence | Completeness | Transition | AfterObserved | AfterExecution | Agent decision authority |
|---|---|---|---|---|---|---|---|---|---|
| 1 Enter Child | SettingsRoot | SettingsRoot | `[]` | fresh grounded Display after existing authorized child action | parent progress/authorization unchanged | `ENTER_CHILD / OBSERVED_AND_EXECUTION_ADVANCED` | Display | Display; path adds Root entry | Existing Agent authorization decided entry; transition adds none |
| 2 Verified Return | Display | Display | `[SettingsRoot→Display obligation]` | fresh exact SettingsRoot + parent continuity | Display existing completion/obligation proof present | `VERIFIED_RETURN_TO_ACTIVE_PARENT / OBSERVED_AND_EXECUTION_RESUMED` | SettingsRoot | SettingsRoot; path pops | Existing verified-return contract only |
| 3 r5 Premature Return | Display | Display | `[SettingsRoot→Display obligation]` | fresh grounded SettingsRoot (seq28 evidence) | Display incomplete/unresolved | `PREMATURE_RETURN_TO_ACTIVE_PARENT / OBSERVED_EXECUTION_PRESERVED` | SettingsRoot | Display; path unchanged | Agent may fail/stop under existing policy; no automatic recovery/re-entry |
| 4 Known Non-parent | Display | Display | active path as-is | fresh known other Container not immediate parent | reference only; no implication | `KNOWN_NON_PARENT_TRANSITION / OBSERVED_EXECUTION_PRESERVED` | known destination | Display/path unchanged | Agent must separately authorize any response |
| 5 External Exit | Display | Display | active path as-is | fresh different foreground | existing BoundaryRelation/obligation ref when applicable | `EXTERNAL_EXIT / OBSERVED_EXECUTION_PRESERVED` | external/unknown semantic location | Display/path unchanged | Existing external-boundary policy only |
| 6 Unknown Destination | Display | Display | active path as-is | accepted fresh observation with semantic page Unknown | reference only | `UNKNOWN_TRANSITION / OBSERVED_EXECUTION_PRESERVED` | Unknown | Display/path unchanged | Existing fail-closed policy; no fabricated Container |
| 7 Sibling Continuation | SettingsRoot after verified return | SettingsRoot | parent path after pop | fresh parent inventory/progress | returned child complete; sibling pending | prior return event only; no route event selects sibling | SettingsRoot until next accepted transition | SettingsRoot until separately authorized child entry | Agent independently authorizes next sibling from existing evidence |

### D9 — Normal-path equivalence argument

The consolidation changes storage and adds immutable evidence, not the controlling predicates:

- Enter child still requires the same candidate authorization/action and fresh child grounding; push and active switch move into one context replacement.
- Verified return still requires child-local/progress completeness plus exact fresh parent continuity; progress update and path pop occur in one commit.
- Sibling continuation still reads `_branchProgress` and independently authorizes the next child.
- Authorized external boundary still uses `BoundaryRelation`, pending obligation, one existing return action, and exact-parent verification.
- Same-Container acceptance keeps current Container evidence and execution unchanged.
- GoalEvidence, RunState, action count, recovery, and terminal authority are untouched.

Required replay proof for Apply: equal deterministic inputs produce semantically equal action history, accepted observations, branch/boundary evidence, GoalEvidence, and terminal result. The only additive output is typed transition/read-model evidence. Therefore `NORMAL_PATH_CONTROL_FLOW_DELTA = 0`.

### D10 — NavigationGraph non-goal proof

| Navigation/graph property | ActiveContainerContext capability |
|---|---|
| Stores arbitrary edges/destinations | No; only currently active parent entries and active execution Container |
| Supports lookup/search/path planning | No API and no stored data for it |
| Reusable after pop or Run end | No; popped entries disappear and context is Run-local |
| Persists cross-session topology | No persistence or cross-session owner |
| Represents discovered world | No; visited/discovery evidence remains separate and non-topological |
| Proves graph/world exhaustion | No; completion remains existing evidence/GoalEvidence |
| Authorizes navigation/recovery | No; transition/path are evidence/execution bookkeeping only |
| Treats transition as edge | No; immutable occurrence result is not reusable topology |

Thus `ACTIVE_CONTAINER_CONTEXT != NAVIGATION_GRAPH`, `ACTIVE_ANCESTOR_PATH != ROUTE_PLAN`, and `CONTAINER_TRANSITION != NAVIGATION_EDGE` are structurally enforced, not merely naming claims.

### D11 — Read model and Debug Toolchain integration

Runtime/DriverHost projection:

```text
CurrentObservedLocation      ← direct WorldBelief projection
ActiveExecutionContainer    ← immutable Agent context snapshot
ActiveAncestorPath          ← immutable identity/obligation projection
LatestObservedTransition    ← derived from latest committed transition event
CompletenessRef             ← ref to existing progress/local evidence
EvidenceRef                 ← transition observation evidence chain
AssetRef                    ← existing capture/debug asset index when available
```

No live Container reference crosses the public read boundary. Older runs without structured events report the field unavailable; reason strings are never parsed into facts.

Future separately gated CLI candidates:

```text
runtime-debug container context <run>
runtime-debug container transitions <run>
runtime-debug container transition <transition-ref>
```

Future TUI `Container Context` panel fields: Observed Location, Active Execution Container, Active Ancestor Path, Latest Transition, CompletenessRef, EvidenceRef, AssetRef. These surfaces must consume the shared Runtime Debugging Toolchain Query/Analysis core; this change does not implement or freeze a new independent analysis engine.

Asset chain:

```text
ContainerTransition
→ FreshObservationRef
→ EvidenceRef
→ AssetRef (frame/screenshot; optional crop/overlay)
```

Missing r5 screenshot/logcat assets remain explicit `MISSING_EVIDENCE`; existing structured observation evidence is not upgraded into an asset.

### D12 — Authority, architecture, and ownership deltas

| Dimension | Result |
|---|---|
| `AuthorityDelta` | `NONE` — Agent, Container, Traversal, Recovery, GoalEvidence, and action authorities unchanged |
| `ArchitectureDelta` | `SUBORDINATE_CONSOLIDATION_ONLY` — one Agent-internal context value, typed immutable result, and atomic seam within the already approved Agent ownership; no top-level Architecture v1 or Contract invariant change |
| `OwnershipDelta` | `NONE` — Agent remains the only owner of WorldBelief/execution context/progress; Container keeps local state; DriverHost/Trace remain read-only evidence consumers |
| `BehaviorDelta` | normal path `NONE`; unexpected-transition reconciliation becomes honest only in Stage C after a separate Apply Human Gate |
| `PersistenceDelta` | `NONE` for execution truth; append-only transition evidence/read projections only |

## Staged Migration Plan

No Apply is authorized. If Human Gate later authorizes implementation, use additive/reversible stages with `NET_NEW_MUTABLE_TRUTH <= 0` at every stage:

### Stage A — Behavior-neutral semantic seam

- Add immutable transition kind/result and pure classifier/prepare result.
- Add behavior-neutral structured transition event/read projection and focused tests.
- Thread results through existing flow without adding a mutable latest field or changing control flow.
- Convert `_lastStabilityExhaustionDetail` to an immutable return result where the seam reaches quiescence.
- Exit criteria: normal-path replay equal; mutable location truth unchanged or reduced; no Runtime authority delta.

### Stage B — Ownership consolidation

- Introduce the structurally immutable `ActiveContainerContext` as the replacement slot.
- Migrate every `_activeContainer` read/write.
- Move the exact existing parent-stack values into `ActiveAncestorPath`.
- Derive ancestry membership and semantic depth; delete local `parents` and `ancestry` slots.
- Keep `visited`, `_branchProgress`, discovery/completeness evidence, boundary evidence, and recovery bookmarks separate.
- Exit criteria: old fields absent, semantic truth `4→3`, storage slots `4→2`, owner count unchanged.

### Stage C — Unexpected transition reconciliation

- Route accepted fresh location through the atomic prepare/commit seam.
- Fix r5 class: accepted SettingsRoot updates WorldBelief while Display execution obligation stays unresolved.
- Preserve existing fail-closed policy; do not add recovery, automatic re-entry, or traversal strategy.
- Prove known non-parent, external, and Unknown branches without destination authorization.
- Exit criteria: r5 contract test passes; all normal-path equivalence tests remain green.

Rollback is stage-local: revert the stage's code and projection while retaining prior stages only if their tests/contract remain valid. No data migration or persisted topology cleanup exists. Transition trace/read-model additions are additive evidence and can be ignored by older consumers.

## Risks / Trade-offs

- [Context becomes a God Context] → Enforce the exact two-field semantic budget; completeness, visited, progress, recovery, belief, observation, and latest transition are forbidden fields.
- [Wrapper hides duplicated old state] → Architecture/structural tests must prove `_activeContainer`, method-local parent stack, and mutable ancestry set are removed before Stage B acceptance.
- [Immutable context contains mutable Container references] → Treat references as existing owner handles only; expose identities/snapshots across read boundaries and never transfer Container local-state ownership.
- [Atomic commit still throws after partial mutation] → Precompute all immutable replacements and validate all ordinary failure conditions; keep commit synchronous, total for prepared input, and free of I/O/await/policy.
- [Transition classification becomes policy] → Closed kinds/dispositions describe evidence/state effect only; tests forbid action, recovery, completion, route, and re-entry decisions from classifier output.
- [r5 fix accidentally completes or re-enters Display] → Dedicated negative Scenario asserts `Observed=SettingsRoot`, `Execution=Display`, incomplete obligation preserved, zero automatic action.
- [Historical specs still require a mutable ancestry set] → This change includes a full `open-world-traversal-identity-safety` delta replacing it with a derived ancestry view while preserving cycle and visited behavior.
- [Debug projection becomes a second truth store] → Latest transition is derived from immutable events; unavailable remains unavailable; Trace/AssetRef are evidence only.
- [Dirty-worktree evidence drifts before Apply] → Apply must redo the BEFORE inventory against then-current source and stop if counts, owners, or buyers changed.

## Next Human Gate

This proposal recommends **PROPOSAL_ACCEPTED / APPLY_NOT_AUTHORIZED** only if the Human answers all ten questions affirmatively:

1. Does `ActiveContainerContext` genuinely replace, rather than duplicate, scattered state? **Design answer: yes.**
2. Which old mutable fields are deleted? **`_activeContainer`, method-local `parents`, separately maintained `ancestry`, and mutable `_lastStabilityExhaustionDetail` handoff.**
3. What are mutable-truth counts? **Semantic 4→3; storage slots 4→2; owners 1→1; diagnostic handoff 1→0.**
4. Are observed location and active execution strictly separate? **Yes; WorldBelief versus context.**
5. Can transition remain immutable? **Yes; operation result + append-only event, no latest field.**
6. Is there new authority? **No.**
7. Did NavigationGraph semantics enter? **No; proof in D10.**
8. Is normal-path behavior preserved? **Contractually yes; Apply must prove replay equivalence.**
9. Can r5 be honest without automatic recovery? **Yes; parent observed, child execution obligation unresolved.**
10. Is it worth entering Apply? **Leader recommendation: yes, staged A→B→C, but only after explicit Human authorization.**

After artifact verification, stop and wait for that Human Gate.
