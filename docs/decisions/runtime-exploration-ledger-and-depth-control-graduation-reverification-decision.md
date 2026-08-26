# Runtime Exploration Ledger and Depth Control — Graduation Reverification Decision

> Status: `SUPERSEDED` — historical `GRADUATION_REVOKED` evidence after successful Option A remediation | Decision: `DO_NOT_GRADUATE_RUNTIME_EXPLORATION_LEDGER_AND_DEPTH_CONTROL` | Date: 2026-08-25
> Change: `openspec/changes/runtime-exploration-ledger-and-depth-control/`
> Verified revision: `e2d8dd4`
> Supersedes: the lifecycle conclusion in `runtime-exploration-ledger-and-depth-control-graduation-decision.md`; that document remains historical evidence.
> SupersededBy: `runtime-exploration-phase2-final-graduation-decision.md` after Human-approved Option A Apply and independent full reverification.
> Authority: Runtime Architecture Contract I-1..I-14 and the approved change Spec remain unchanged. This decision adds no architecture authority.

## 1. Independent conclusion

Phase 2 graduation is **not earned** at the verified revision. The immutable
ledger model, configured-classifier fail-closed path, depth-0/1 boundary path,
exhaustive cutoff, and authority guards are real. However, four normative
requirements remain either violated or unproved on the accepted Strategy Run
path. The prior graduation decision converted explicit SHALL/MUST text into
non-blocking interpretation notes and therefore cannot remain the current
lifecycle conclusion.

This is not an ordinary test failure. The missing source of bounded-record
semantics cannot be chosen without deciding how the frozen Strategy Contract is
interpreted or extended. Production remediation stops at the architecture gate.

## 2. Spec → symbol → test → evidence map

| Spec requirement | Production symbols inspected | Existing tests | Reverification result |
|---|---|---|---|
| R1 Evidence-derived ledger | `ExplorationLedgerCompiler.CompileScope/Compile`; `Agent.CompileExplorationLedgerView`; `Agent.PreTerminalCycle` structural fact producer | `ExplorationLedgerTests`; complete-ledger tests; unresolved real-path test | **FAIL** — structural-progress facts are not compiler inputs; unresolved identities already belong to `ApprovedSiblingEvidence` but `CompileScope` adds `unresolvedCount` to discovered, double-counting the real path; caller-supplied Run/intent/depth values are not bound to the accepted Strategy Run. |
| R2 Closed rule vocabulary applied at admission/classification | `StrategyContractCompiler.Compile/Interpret`; `RuntimeExecutionIntent`; CP-12 classification in `Agent.OpenWorld` | direct `DeriveRules` unit test; `StrategyContractTests`; `UnresolvedNodeFailClosedPathTests` | **FAIL** — configured null classification correctly records unresolved and dispatches nothing, but admission neither derives nor stores an `ExplorationRule`; real classification applies `TypeLevelElementCategory → TypeLevelHandling`, not the closed rule vocabulary. |
| R3 Visited means rule-satisfied | `CompileScope`; `WithCompletedSibling`; `WithVerifiedBoundaryDisposition`; depth-boundary frontier writer | click-without-completion unit test; depth-0 zero-dispatch test; verified-return tests | **PARTIAL** — click/dispatch alone is not visited and depth-boundary RecordOnly is counted from observation. The general accepted-strategy leaf path is not governed by `ExplorationRule.RecordOnly`; it can dispatch `Inspect`/Tap, so the claimed rule application is not proved. |
| R4 Bounded semantic depth | `StrategyDirective`; `ExplorationIntent`; `DeriveDepthSemantics`; `Agent.OpenWorld` depth branch | depth 0/1/N tests; immutability guard | **FAIL** — depth 0/1 record-only and depth N>=2 exhaustive cutoff are implemented, but no accepted strategy field or frozen interpretation declares bounded-record versus exhaustive semantics for arbitrary N. The code substitutes a depth-threshold rule for the Spec's strategy-declared choice. |
| R5 Ledger is never completion authority | ledger model/compiler; Agent completion path | satisfied-ledger/unsatisfied-GoalEvidence test | **PASS** — ledger cannot complete a Run and does not mutate GoalEvidence/FSM. |
| R6 Neutrality and authority guards | ledger/rule model and compiler | `ExplorationLedgerAuthorityGuardTests` | **PASS** — no action, authorization, FSM, completion, recovery, or scenario-specific authority was found in ledger types. |

## 3. Blocking evidence

### B1 — Admission derivation and runtime rule application are absent

`StrategyContractCompiler.Interpret` constructs `RuntimeExecutionIntent` from
the original strategy, a traversal specification, and a Goal. It does not call
`ExplorationLedgerCompiler.DeriveRules` or `DeriveDepthSemantics`, and
`RuntimeExecutionIntent` carries no derived rule/depth semantics. The only
production uses of these derivation methods are ledger compilation and the
open-world depth-boundary branch. The CP-12 classification seam continues to
resolve `TypeLevelElementCategory` through `TypeLevelDispatchPolicy`.

Therefore the direct derivation unit tests prove a pure helper, not the Spec's
admission and real-path application requirements.

### B2 — Bounded-record declaration has no frozen source

The accepted `StrategyDirective` has two `ExplorationIntent` values:
`ExhaustiveWithinScope` and `InspectMatchesWithinScope`. It has no boundary-mode
field. The current execution entry also projects both strategies to the existing
`TypeLevelCompletionRequirement.ExhaustiveWithinScope` representation.

The implementation treats depth 0/1 as bounded-record and depth N>=2 as
exhaustive solely from the numeric depth. That does not implement the Spec's
condition "when the strategy declares bounded-record semantics" for arbitrary
N. Choosing an implicit mapping from the frozen objective/exploration/completion
tuple, or adding an explicit contract field, is a material semantic decision.

### B3 — Real-path unresolved accounting double-counts discovered

Branch inventory acceptance creates `ApprovedSiblingEvidence` before node
classification. An unclassifiable node is consequently already part of the
approved discovered inventory. `RecordUnresolvedNode` records that same identity
in `_unresolvedNodes`, but `CompileScope` computes:

```
discovered = ApprovedSiblingEvidence.Count + unresolvedCount
```

The current real-path test asserts only `Unresolved >= 1` and `Visited == 1`; it
does not assert the exact discovered count. With its two-node Root inventory and
one unresolved identity, the compiler formula reports three discovered nodes.
This is inconsistent unified accounting, even though the existing test passes.

### B4 — Structural-progress facts are named by the normative requirement but omitted

R1 requires compilation from branch progress, revisit coverage,
structural-progress facts, and observation-sequence correlations.
`ExplorationLedgerCompiler.Compile` has no structural-fact input. The only real
producer currently emits a Run-level `BoundedScopeEntered` revision marker with
an opaque reference. The prior graduation decision treated its lack of
per-scope node content as permission to omit it. That interpretation cannot
override the requirement; defining how it participates is an evidence-semantics
decision.

## 4. Executed verification

- Current worktree was clean before verification; HEAD matched `origin/uni-agent`
  at `e2d8dd4`.
- Targeted Runtime tests covering ledger, depth, unresolved real path, authority
  guards, and Strategy Contract: **44/44 passed**.
- `openspec validate runtime-exploration-ledger-and-depth-control --strict`:
  **PASS**.
- `git diff --check` before documentation reconciliation: **PASS**.

These green gates show that the current tests and OpenSpec document validator do
not cover B1-B4. They are not graduation evidence for the missing semantics.
Full deterministic/semantic regression was not re-run because a normative
architecture gate was already established; regression green cannot resolve it.

## 5. Human decision packet

### Goal

Choose the authoritative source of bounded-record semantics and the honest
evidence-fusion boundary so Phase 2 can be remediated without silently changing
the frozen Strategy Contract.

### What changed / was discovered

The current implementation has useful Phase 2 pieces but does not apply its
closed rule vocabulary at admission/runtime, cannot represent bounded-record at
arbitrary N, double-counts real unresolved nodes, and omits a normative evidence
family. The prior graduation conclusion is revoked.

### Architecture impact

- No current Runtime ownership or completion authority has been transferred.
- Remediation must decide whether bounded-record is an interpretation of existing
  Strategy fields or a new protocol field.
- Structural-progress participation must be frozen without inventing a second
  evidence owner.
- Phase 3 Memory and Phase 4 dynamic depth remain unauthorized.

### Material trade-off

1. **Option A — frozen-contract internal interpretation (recommended):** create a
   successor OpenSpec that freezes an explicit mapping from the existing closed
   Strategy objective/exploration/completion tuple to `Exhaustive` versus
   `BoundedRecord`; carry the derived rules/depth mode in
   `RuntimeExecutionIntent`, apply them on the real path, bind the ledger to the
   accepted Run, and define structural facts as a fail-closed correlation input.
   No wire/schema change, but the mapping requires Human semantic approval.
2. **Option B — explicit Strategy Contract extension:** add a typed boundary-mode
   field such as `Exhaustive` / `BoundedRecord`. This is clearer and supports all
   N directly, but changes the frozen public Strategy Contract and requires a
   larger protocol/OpenSpec gate.
3. **Option C — narrow the Phase 2 claim:** amend the Spec to a ledger-only
   projection and remove admission rule-application, structural-fact fusion, and
   arbitrary-N bounded-record claims. This matches more of the current code but
   does not deliver the roadmap's full Phase 2 capability.

### Human option-selection receipt

On 2026-08-25, Human selected **Option A**. This authorizes preparation and
strict validation of the successor OpenSpec
`runtime-exploration-semantic-admission-remediation` using the frozen-contract
internal interpretation. It does **not** authorize production or test apply.

The successor proposal, design, Spec, and tasks are complete and strictly valid.
Human subsequently approved Apply for the exact pre-Apply artifact content IDs
recorded in the successor `tasks.md`, while explicitly excluding new wire/schema,
Evidence owner, state system, scenario knowledge, Phase 3 Memory, and Phase 4
dynamic depth. Production implementation may now proceed only within that
envelope. `runtime-exploration-ledger-and-depth-control` remains active, not
graduated, not archive-eligible, and Phase 3 must not start unless a later
independent graduation decision says otherwise.
