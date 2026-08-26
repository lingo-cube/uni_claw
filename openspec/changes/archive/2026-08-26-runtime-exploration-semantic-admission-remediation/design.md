## Context

See `proposal.md` for motivation and the predecessor reverification decision for the failing evidence map. The governing constraints are Runtime Architecture Contract I-1..I-14, Architecture v1, the frozen Strategy Contract, and the predecessor exploration Spec. Option A was selected by the Human: preserve the existing Strategy wire/schema and freeze one internal interpretation of its existing closed fields.

Current implementation has four disconnected seams:

1. `StrategyContractCompiler` admits a `StrategyDirective` and creates `RuntimeExecutionIntent`, but does not derive exploration rules or boundary disposition.
2. Agent open-world classification applies category-to-handling policy without consuming `ExplorationRule`.
3. Ledger projection accepts caller-supplied Run/intent/depth metadata and compiles count-only unresolved/frontier inputs.
4. Strategy structural-progress facts exist on the pre-terminal evidence surface but do not participate in ledger correlation.

The remediation must connect these seams without creating a second Runtime loop, new wire contract, new evidence owner, or completion authority.

## Goals / Non-Goals

**Goals:**

- Freeze a total, deterministic Option A interpretation table at admission.
- Carry the admitted semantics through one Agent-owned Run and apply them at classification and depth boundaries.
- Bind ledger provenance to that accepted Run.
- Replace count-only reconciliation with identity-correct, fail-closed accounting.
- Correlate existing structural facts without promoting them to node or completion evidence.
- Preserve existing Agent/FSM/Traversal/GoalEvidence authority and legacy non-Strategy behavior.

**Non-Goals:**

- Change `StrategyDirective`, `run.strategy.start`, protocol versions, or the frozen external Strategy Contract.
- Add an explicit boundary-mode field; that was rejected Option B.
- Narrow the predecessor Spec to the current implementation; that was rejected Option C.
- Introduce Phase 3 Memory, Phase 4 dynamic depth, scenario knowledge, mid-Run strategy mutation, Planner authority, Multi-Run, or a new evidence/state owner.
- Implement production code before the successor Human Apply Gate.

## Decisions

### D1 — Freeze a total interpretation table from existing accepted fields

Admission derives an internal immutable `ExplorationExecutionSemantics` value from the already validated tuple:

| MaximumDepth | Accepted typed tuple | Container behavior | Boundary behavior | Leaf behavior |
|---:|---|---|---|---|
| `0` | either currently supported tuple | no child expansion | root-scope inventory `RecordOnly` | `RecordOnly` |
| `1` | either currently supported tuple | expand root containers | direct-child inventory `RecordOnly` | `RecordOnly` |
| `N >= 2` | `ExploreScope` + `ExhaustiveWithinScope` + `ExhaustiveCoverageWithinScope` | `ExpandContainer` below N | fail closed when required expansion exceeds N | `RecordOnly` |
| `N >= 2` | `InspectMatchesWithinScope` + matching exploration + `AllDiscoveredMatchesInspected` | `ExpandContainer` below N | `RecordOnly` + unknown frontier at N | `RecordOnly` |

The table is an interpretation of fields already frozen by the Strategy Contract. It is not a new strategy field and is not selected from Runtime observation. Any tuple outside the table remains an admission rejection.

Alternative: infer bounded/exhaustive behavior from numeric depth alone. Rejected because it cannot represent both semantics at arbitrary N and repeats the graduation defect.

Alternative: add a boundary-mode field. Rejected for this change because Human selected Option A and the public Strategy schema must remain unchanged.

### D2 — Carry admitted semantics in `RuntimeExecutionIntent`

`RuntimeExecutionIntent` owns no action or lifecycle authority but is the existing immutable Runtime-local interpretation boundary. It will carry the derived semantics alongside the accepted `StrategyDirective`, traversal specification, and Goal. The value contains only closed rules, depth-shape/boundary disposition, accepted Strategy identity/reference, and declared depth.

`IntentExecution` passes this immutable value to the Agent open-world entry. No component re-derives it from UI content, and the Agent cannot mutate it mid-Run.

Alternative: call helper methods independently in the ledger and depth branch. Rejected because independently repeated derivation is not admission binding and permits drift.

### D3 — Bind one immutable exploration context to the existing Agent Run lifecycle

At Strategy Run start, Agent records one immutable accepted exploration context containing the actual Run identity and `RuntimeExecutionIntent` reference plus the admitted semantics. Agent is already the Run/evidence owner; this is Run metadata inside the existing lifecycle, not a new state system or owner. It is assigned once, sealed with the Run, and unavailable on the legacy non-Strategy entry.

`CompileExplorationLedgerView` reads that bound context. It no longer accepts caller-substitutable Run identity, intent, exploration intent, or depth. A mismatch or an absent Strategy context fails closed.

Alternative: continue passing metadata as projection parameters and compare only strings. Rejected because the caller could relabel evidence and identical branch evidence could produce a misleading ledger digest.

### D4 — Use identity sets/maps and an exhaustive primary disposition partition

The compiler receives immutable per-scope evidence with identities, not detached counts:

- accepted `BranchProgressEvidence` inventory and verified completion/return evidence;
- unresolved identity set;
- record-only satisfaction identity-to-observation-sequence evidence;
- unknown-frontier identity set;
- revisit-coverage identities;
- optional accepted structural-progress facts and their Run correlation.

For each scope:

```text
DiscoveredIds = ApprovedInventory.Keys
VisitedIds = VerifiedCompletedOrReturned ∪ RecordOnlySatisfied
UnresolvedIds = ClassificationUnavailable
PendingIds = DiscoveredIds - VisitedIds - UnresolvedIds
UnknownFrontierIds ⊆ RecordOnlySatisfied ⊆ VisitedIds
```

The compiler validates subset/disjointness and source sequences, then reports counts from set cardinality. It never adds unresolved to discovered, never uses dispatch/authorization as visited, and never clamps contradictory counts. Inconsistency throws/fails closed.

This may require replacing `_unknownFrontierBeyondDepth` count values with identity-correlated immutable evidence and adding record-only satisfaction evidence under the same Agent owner. These are evidence records purchased by the predecessor rules; they do not create a new owner or state system.

Alternative: subtract unresolved counts heuristically from approved counts. Rejected because counts cannot prove identity membership, overlap, or corruption.

### D5 — Apply rules before authorization and dispatch

The existing generic classifier remains the semantic seam:

1. configured classifier returns null → record unresolved, no rule, no authorization, no dispatch;
2. classified semantic container below an expandable boundary → `ExpandContainer`, then existing grounding/authorization may admit traversal;
3. classified leaf or boundary node → `RecordOnly`, record the fresh accepted observation sequence, no DeviceAction;
4. exhaustive boundary requiring deeper expansion → existing fail-closed cutoff;
5. bounded-record boundary → record-only satisfaction plus unknown-frontier identity.

`TypeLevelDispatchPolicy` remains relevant only to work that the admitted exploration rule permits to proceed to the existing Agent authorization seam. It cannot override `RecordOnly` into `Inspect`, Tap, or mutation.

Alternative: treat `TypeLevelHandling.Inspect` dispatch as record-only satisfaction. Rejected because a Tap receipt is not fresh-observation rule satisfaction and violates `Visited != Clicked`.

### D6 — Structural-progress facts are correlation inputs, not count inputs

The compiler accepts the existing `StrategyStructuralProgressFact` shape when available. It validates:

- association with the bound accepted Strategy Run evidence view;
- defined kind;
- non-negative, monotonic revision not ahead of the Run's accepted progress revision;
- non-empty evidence reference.

The facts contribute only to a deterministic correlation/digest input. They do not add/remove identities, assert exhaustion, or influence GoalEvidence/FSM. An explicitly empty fact set is valid when the optional pre-terminal evidence surface produced none. Invalid facts fail closed.

Alternative: invent per-scope node meaning for `BoundedScopeEntered`. Rejected because that would create new structural evidence semantics not purchased by Option A.

### D7 — Keep legacy non-Strategy execution isolated

Existing direct `IntentSemanticEnvelope`/open-world callers retain their behavior and can continue without Strategy admission semantics. They cannot call the Strategy-bound ledger projection. Compatibility tests must prove existing `run.start` and non-Strategy execution are unchanged.

Alternative: synthesize a Strategy context for legacy calls. Rejected because it would fabricate provenance and silently expand the Strategy Contract.

### D8 — Graduation requires adversarial evidence, not green arithmetic tests

Each SHALL/MUST behavioral claim requires a real Agent path test using a generic Fake World and the accepted Strategy admission/intent handoff. Pure compiler tests supplement but never substitute for admission, dispatch, or completion-path evidence. Sol/Leader independently re-runs the gates and performs the final Spec-to-symbol-to-test-to-evidence mapping.

## Risks / Trade-offs

- [The Option A tuple mapping may surprise clients that treated `InspectMatchesWithinScope` as physical Tap] → Freeze that inspection means semantic observation unless an independently authorized non-exploration action contract says otherwise; add compatibility and zero-dispatch tests.
- [Identity-level evidence adds fields under Agent ownership] → Use immutable snapshots, one existing owner, no persistence, and authority/dependency guards; do not introduce a ledger owner.
- [Legacy and Strategy paths may diverge] → Keep separate entry contracts and add paired regression tests proving only Strategy-bound behavior changes.
- [Structural facts may be absent in some valid Runs] → Represent absence explicitly; validate present facts without manufacturing per-scope meaning.
- [A complete ledger may be mistaken for completion] → Preserve satisfied-ledger/unsatisfied-GoalEvidence tests and reflection guards.
- [Existing predecessor tasks and successor tasks can become duplicate truth] → The predecessor stays active with reopened tasks; successor tasks reference exactly which predecessor gaps they close, and only a later independent decision may reconcile graduation.

## Migration Plan

1. Obtain explicit Human Apply approval for this successor revision.
2. Freeze the approved proposal/design/spec digest and create validated UniFlow WorkItems, one owner at a time.
3. Implement admission semantics and immutable Run binding before changing ledger counting.
4. Implement real-path rule application and identity-correlated evidence under Agent ownership.
5. Update the pure compiler and structural-fact correlation; do not alter completion/FSM/action authority.
6. Add real-path and guard tests, then run targeted, full deterministic excluding device/reality suites, full Semantic, strict OpenSpec, consistency, and diff checks.
7. Have Sol independently verify the complete Spec-to-evidence map. If any new owner, protocol field, public schema, lifecycle, completion authority, scenario knowledge, or dynamic depth is required, stop with `ARCHITECTURE_DECISION_REQUIRED`.
8. On failure, roll back only successor implementation deltas while retaining the predecessor reverification history; do not archive or rewrite frozen Specs.
