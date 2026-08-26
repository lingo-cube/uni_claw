## 1. Human Apply Gate and Dispatch Freeze

- [x] 1.1 Obtain explicit Human approval to apply the exact Option A proposal/design/spec revision; record the approved revision/digest before any production or test edits.
- [x] 1.2 Record Sol Leader as architecture/lifecycle/graduation owner and create one validated UniFlow WorkItem per bounded implementation or test increment, using `runtime-core` with `development` or `test-authoring`; no Tool Only source write.
- [x] 1.3 Re-read the predecessor reverification decision and confirm no implementation task requires a new Strategy wire field, evidence owner, state system, lifecycle, authority, scenario knowledge, dynamic depth, or Phase 3 Memory; otherwise stop with `ARCHITECTURE_DECISION_REQUIRED`.

### 1.4 Human Apply authorization receipt (2026-08-25)

- Human explicitly approved Apply for the current Option A successor artifacts and
  explicitly excluded new wire/schema, Evidence owner, state system, scenario
  knowledge, Phase 3 Memory, and Phase 4 dynamic depth.
- Approved repository base: `e2d8dd44214632f50777992d58fb4fe318ad45f0`.
- Approved pre-Apply artifact content IDs, in proposal/design/spec/tasks order:
  `3617e61850ed3a928d2c5790c389f13ac441f864`,
  `18c293ba5b32965b271172c5bbce658058aae884`,
  `ea1a9d7efc769dbb5f2acac1efda63c27057c895`, and
  `a97f269b83a6085aa3615aaae490660dd37052a4`.
- Sol Leader re-read the predecessor reverification decision and confirmed that
  the approved tasks can begin within existing Agent ownership, evidence seams,
  lifecycle, authority, and frozen Strategy wire/schema. Any contrary pressure
  remains an immediate `ARCHITECTURE_DECISION_REQUIRED` stop.
- Sol Leader retains architecture, lifecycle, graduation, dispatch, and final
  verification ownership. First bounded implementation dispatch is the validated
  `docs/work/active/workitems/WI-RESAR-001.json` (`runtime-core` + `development`,
  one worker owner); no Tool Only Agent was created for a source write.

## 2. Admission-derived Exploration Semantics

- [x] 2.1 Add one immutable internal exploration-semantics value carrying the closed container/leaf rules, depth shape, boundary disposition, accepted Strategy identity/reference, and declared depth without DeviceAction, FSM, completion, recovery, or mutation members.
- [x] 2.2 Derive the exact D1 Option A table inside Strategy admission from the already validated objective/exploration/completion/depth tuple; reject any unsupported tuple and remove independent post-admission semantic guessing.
- [x] 2.3 Carry the admitted semantics in `RuntimeExecutionIntent` and pass the same immutable value through `IntentExecution` to the Strategy Agent Run.
- [x] 2.4 Add admission tests for depth 0, depth 1, depth N exhaustive, depth N match-inspection bounded-record, unsupported tuple rejection, and Run immutability.

### 2.5 Admission increment evidence (WI-RESAR-001)

- Leader accepted the WorkResult only after schema correction and independent
  source/diff inspection. `ExplorationExecutionSemantics` lives in the existing
  Model exploration-rule layer, so Agent need not reverse-depend on Planning.
- Leader independently re-ran `StrategyContractTests`: 21/21 PASS, and
  `dotnet build src/UniClaw.Runtime.sln --no-restore`: 0 warnings / 0 errors.
- Task 2.3 was subsequently closed by WI-RESAR-002: the same immutable instance
  crosses `IntentExecution` and is bound to the Strategy Agent Run.

## 3. Accepted Strategy Run Binding

- [x] 3.1 Bind one immutable accepted exploration context to the existing Agent-owned Run lifecycle at Strategy Run start; preserve one owner and introduce no second state system.
- [x] 3.2 Replace caller-substitutable ledger Run/intent/rule/depth parameters with the bound accepted context and fail closed on absent or mismatched provenance.
- [x] 3.3 Keep the legacy non-Strategy open-world entry behavior unchanged and prove it cannot expose or fabricate a Strategy-bound ledger.
- [x] 3.4 Add correlation tests proving one accepted Strategy identity, runtime-execution-intent reference, Run identity, and depth from admission through sealed ledger projection.

### 3.5 Run-binding increment evidence (WI-RESAR-002)

- Existing public `IntentExecution.RunOpenWorldAsync` signature remains unchanged;
  an assembly-internal Strategy seam transports the same admitted semantics
  instance and the Agent binds one immutable `AcceptedExplorationRunContext`
  before its first Run state transition.
- Legacy execution binds no Strategy context, and a depth mismatch fails while
  Agent remains `Idle`. Leader independently re-ran the Strategy binding,
  admission, and reasoning tests: 43/43 PASS; Runtime solution build: 0 warnings /
  0 errors.
- Tasks 3.2-3.4 were subsequently closed by WI-RESAR-005: the public Agent
  projection has no caller-substitutable provenance parameters, legacy Runs
  fail closed, and the sealed ledger metadata is asserted against the bound
  accepted context.

## 4. Real-path Exploration Rule Application

- [x] 4.1 Apply the admitted exploration rule immediately after generic classification and before Agent authorization/dispatch: null classification remains unresolved; expandable container continues to authorization; leaf/boundary RecordOnly never dispatches.
- [x] 4.2 Record immutable identity-to-observation-sequence satisfaction evidence for every RecordOnly node from the fresh accepted observation, including non-boundary leaves.
- [x] 4.3 Preserve verified subtree-return and verified boundary-disposition evidence for `ExpandContainer` Visited; prove dispatch/click/authorization alone remains non-Visited.
- [x] 4.4 Implement the D1 depth table exactly: depth 0 root inventory record-only, depth 1 direct-child inventory record-only, depth N exhaustive cutoff for `ExploreScope`, and depth N bounded-record frontier for `InspectMatchesWithinScope`.
- [x] 4.5 Add real Agent/Fake World tests for leaf zero-dispatch, container authorization, unclassifiable zero-dispatch, depth 0/1/N divergence, and no state-changing side effect from exploration RecordOnly.

### 4.6 Real-path rule increment evidence (WI-RESAR-003/004)

- Strategy-bound classification now resolves the closed typed category directly
  to the immutable admitted `ExplorationRule` before dispatch-policy lookup or
  Agent authorization. `RecordOnly` writes identity-to-fresh-observation
  evidence and exits with zero action; only `ExpandContainer` may proceed to
  the existing policy, grounding, authorization, and Traversal seams.
- Boundary processing uses the same typed rule resolver. Record-only boundary
  identities are satisfied from their accepted source observation, while only
  identities whose underlying rule is `ExpandContainer` receive the overlapping
  unknown-frontier annotation. The legacy entry remains isolated.
- Leader rejected the first WI-RESAR-004 report because its tests did not fully
  prove the frozen acceptance conditions, required the same worker to complete
  the missing bounds/authorization/return/exact-reason falsifiers, then
  independently inspected the repaired source and re-ran the combined targeted
  suite: 58/58 PASS. Runtime solution build: 0 warnings / 0 errors; `git diff
  --check`: PASS.

## 5. Identity-correct Ledger Accounting

- [x] 5.1 Replace detached unresolved/frontier counts with immutable identity-correlated evidence inputs under the existing Agent owner; validate every identity against the accepted per-scope inventory.
- [x] 5.2 Compile `DiscoveredIds`, `VisitedIds`, `PendingIds`, and `UnresolvedIds` as the exhaustive primary partition defined by design D4; keep `UnknownFrontierIds` only as a subset annotation on RecordOnly Visited.
- [x] 5.3 Remove count clamping and fail closed on identity outside inventory, incompatible disposition overlap, invalid observation sequence, or frontier not contained in RecordOnly satisfaction.
- [x] 5.4 Preserve revisit-coverage as a fail-closed identity consistency input and prove valid coverage changes no primary disposition.
- [x] 5.5 Include the bound Run semantics/provenance and identity-derived scopes in the deterministic ledger digest; identical evidence must yield identical ledgers and mismatched evidence must not compile.

### 5.6 Accepted-context identity-ledger evidence (WI-RESAR-005)

- `Agent.CompileExplorationLedgerView()` is now a zero-parameter projection.
  It reads Run/reference/rules/depth exclusively from the same immutable accepted
  Strategy context; an Idle or legacy non-Strategy Agent cannot expose the
  Strategy-bound ledger.
- The compiler receives internal immutable identity evidence, derives
  `Discovered` solely from approved inventory, derives `Visited` only from
  verified completion/return or fresh RecordOnly evidence, computes `Pending`
  as the exact complement, and treats unknown frontier only as a RecordOnly
  subset annotation. Authorization and dispatch remain correlation evidence,
  never Visited evidence.
- Leader rejected two incomplete WI-RESAR-005 reports: the first introduced a
  public evidence/API surface and lacked the promised falsifiers; the second
  omitted boundary/authorization observation sequences and two explicit overlap
  tests. After repair, Leader independently confirmed internal-only evidence,
  no clamp/remainder correction, complete identity/sequence/revisit/boundary
  canonical digest material, and all requested fail-closed tests. Combined
  targeted suite: 91/91 PASS; Runtime build: 0 warnings / 0 errors; `git diff
  --check`: PASS.

## 6. Structural-progress Correlation

- [x] 6.1 Admit existing `StrategyStructuralProgressFact` records as an optional immutable compiler input associated with the bound accepted Strategy Run evidence surface.
- [x] 6.2 Validate defined kind, non-negative monotonic revision, non-empty evidence reference, and Run/progress correlation; invalid facts fail closed.
- [x] 6.3 Preserve structural facts only in correlation/digest evidence and prove they never change node counts, exhaustion, GoalEvidence, FSM, or completion.
- [x] 6.4 Add tests for valid correlated facts, explicitly absent facts, mismatched Run/progress, invalid revision/reference, unchanged counts, and unsatisfied GoalEvidence despite structural progress.

### 6.5 Structural-correlation evidence (WI-RESAR-006)

- Agent retains the existing immutable `StrategyExecutionEvidenceView` only
  after the existing pre-terminal validator accepts, the evaluator commits, and
  the Run remains `Running`. Rejected, unsupported, failed, and legacy paths do
  not fabricate accepted structural evidence.
- Compiler validation correlates contract/Run/intent and validates defined kind,
  non-negative non-decreasing revision not ahead of the accepted view revision,
  and nonblank references. Explicit absence and an explicit empty fact set are
  both valid. Canonical structural material affects only ledger equality/digest;
  the opaque accepted-view digest remains internal correlation material and does
  not make semantically identical reordered facts nondeterministic.
- Leader rejected the first WI-RESAR-006 report for missing real unsatisfied-Goal
  and constructor-boundary evidence, then found and repaired an order-sensitive
  equality defect in the second report. Final independent execution: 97/97
  targeted tests PASS; Runtime build: 0 warnings / 0 errors; `git diff --check`:
  PASS. A real Strategy Run with accepted structural facts and unsatisfied fresh
  GoalEvidence remains `Failed` before and after ledger projection.

## 7. Authority, Compatibility, and Neutrality Guards

- [x] 7.1 Prove `StrategyDirective`, `run.strategy.start`, wire DTOs, and public protocol versions are byte/shape compatible with the predecessor frozen contract.
- [x] 7.2 Extend reflection/dependency guards so admitted semantics, Run context, ledger, compiler, and rule-satisfaction evidence carry no action, target, authorization, transition, recovery, completion, GoalEvidence mutation, or external capability authority.
- [x] 7.3 Extend scenario-neutrality guards over every new/changed Runtime source; no labels, routes, selectors, fixed paths, coordinates, UI text, or scenario-specific classification.
- [x] 7.4 Prove Agent remains the sole Run/evidence/authorization/completion owner, FSM remains transition authority, Traversal remains execution owner, and no new evidence/state owner exists.

### 7.5 Compatibility and authority-guard evidence (WI-RESAR-008)

- The dedicated guard freezes the exact eight-property/eight-constructor-parameter
  `StrategyDirective` public shape, the exact Strategy Run request and admission
  DTO shapes, the closed eight-key `run.strategy.start` Strategy payload, and
  both Strategy and DriverHost public protocol version `1`.
- Recursive reflection covers public and non-public declared fields, properties,
  constructors, method returns, and method parameters on the admitted semantics,
  accepted Run context, identity evidence, ledger view/scopes, and compiler. It
  rejects action, target-selection, RunState, GoalEvidence, FSM, Traversal,
  recovery, completion, authorization, dispatch, and transition authority.
- Source-shape guards scan all seven changed Runtime production files after
  comment removal, require typed `TypeLevelElementCategory` rule resolution,
  and reject scenario tokens or string/integer category interpretation. They
  also require one Agent-owned declaration for every new evidence field, limit
  all assignments to Agent partials, require the only production ledger compile
  call to remain in `Agent.cs`, and keep the compiler pure static.
- Leader rejected three incomplete guard revisions before accepting the exact
  closed shape and complete signature/owner checks. Independent execution of the
  new guard plus existing ledger-authority, external-neutrality, pre-terminal,
  and Strategy wire suites: 29/29 PASS. The frozen Strategy/wire/request/protocol
  source files have no diff from approved base
  `e2d8dd44214632f50777992d58fb4fe318ad45f0`; `git diff --check`: PASS.

## 8. Deterministic and Regression Verification

- [x] 8.1 Add a real-path exact-accounting falsifier: two discovered identities, one verified visited and one unclassifiable, MUST produce discovered 2 / visited 1 / pending 0 / unresolved 1.
- [x] 8.2 Add real-path tests proving a classified but unsatisfied identity remains Pending, a record-only boundary is Visited plus unknown frontier, and contradictory identity evidence fails closed.
- [x] 8.3 Re-run targeted Strategy admission/execution, ledger, depth, unresolved, OpenWorld, GoalEvidence, FSM, Traversal, and authority/neutrality suites.
- [x] 8.4 Run `dotnet build src/UniClaw.Runtime.sln` and the full deterministic Runtime suite excluding RealDevice, RealEmulator, and RealityBaseline plus the full Semantic suite.
- [x] 8.5 Run `openspec validate runtime-exploration-semantic-admission-remediation --strict`, `openspec validate runtime-exploration-ledger-and-depth-control --strict`, `scripts/check-consistency.sh`, and `git diff --check`.
- [x] 8.6 Record real-device limitations and any unrelated baseline failure honestly; targeted or arithmetic green slices do not establish graduation.

### 8.7 Real-path ledger falsifier evidence (WI-RESAR-007)

- The exact-accounting fixture enters through accepted Strategy admission and the
  real Agent path with two inventory identities. One expandable container is
  authorized, dispatched, and verified on parent return; one identity remains
  unclassifiable with no authorization or dispatch. The compiled root scope is
  exactly discovered 2 / visited 1 / pending 0 / unresolved 1 / frontier 0.
- Separate real-path fixtures prove that a classified but denied container is
  pending (1/0/1/0), and that the depth-zero RecordOnly boundary is visited plus
  unknown frontier (1/1/0/0/frontier 1), both with zero Tap at the relevant
  unique bounds. Overlaying the actual boundary identity as unresolved causes
  `ExplorationLedgerCompiler.CompileScope` to throw rather than clamp.
- Leader rejected two incomplete worker results before accepting the repaired
  assertions, inspected the final test source independently, and re-ran
  `StrategyExplorationLedgerRealPathTests`: 4/4 PASS; `git diff --check`: PASS.

### 8.8 Final regression evidence (Leader independent execution)

- Runtime solution build: 0 warnings / 0 errors.
- Targeted Strategy admission/execution, ledger, depth, unresolved, OpenWorld,
  GoalEvidence, FSM/Agent, Traversal, authority, and neutrality suites: 410/410
  PASS. This includes the exact real-path falsifiers; no arithmetic-only slice
  is used as graduation proof.
- Full deterministic Runtime suite with FullyQualifiedName exclusions for
  RealDevice, RealEmulator, and RealityBaseline: 2052/2052 PASS. Full Semantic
  suite: 32/32 PASS.
- Successor and predecessor strict OpenSpec validation: PASS. UniFlow workflow
  validation: PASS. `scripts/check-consistency.sh`: C1-C12 ALL PASS.
  `git diff --check`: PASS.
- Device-dependent paths were deliberately excluded and not exercised in this
  session; no hardware capability claim is made. No unrelated baseline failure
  appeared in the executed deterministic or Semantic suites.

## 9. Independent Graduation Reverification

- [x] 9.1 Have Sol Leader independently rebuild the complete predecessor + successor Spec → production symbol → real-path test → executed evidence map without trusting task checkboxes or Worker reports.
- [x] 9.2 Reopen any overstated tasks and stop on any protocol, evidence-owner, lifecycle, authority, safety, scenario-knowledge, or dynamic-depth pressure; do not reinterpret SHALL/MUST text as non-blocking.
- [x] 9.3 Only after all gates pass, create a new Phase 2 graduation decision that supersedes the revocation, reconcile both changes' task truth, and sync current gates/latest snapshot.
- [x] 9.4 Do not archive, commit, merge, clean, reset, or begin Phase 3 production work as part of this change.

### 9.5 Final graduation reverification receipt

- Sol Leader rebuilt the full predecessor plus successor mapping directly from
  the two normative Specs, production symbols, real Agent-path tests, and fresh
  command output. Worker self-reports and task checkboxes were not accepted as
  evidence; WI-RESAR-004 through WI-RESAR-008 each required one or more rejected
  incomplete revisions before independent acceptance.
- No new wire/schema, Evidence owner, mutable state system, scenario knowledge,
  completion authority, lifecycle authority, Phase 3 Memory, or Phase 4 dynamic
  depth was required or introduced. Frozen Strategy/wire source remains byte-
  identical to approved base and the dedicated guard freezes its exact shape.
- All technical gates passed before lifecycle documents were written. The new
  `docs/decisions/runtime-exploration-phase2-final-graduation-decision.md`
  supersedes the revocation as the current lifecycle conclusion and contains
  the complete Spec → symbol → real-path test → executed evidence map.
- Both changes remain active and not archived. Active/Archive projections remain
  18/42. No archive, commit, merge, clean, reset, or Phase 3 production work was
  performed.

## Design Docs

> Auto-generated from proposal Impact section and refined for this Runtime module.
> Implementation agents: read these before starting.

| Module / concern | Design Doc |
|---|---|
| Successor scope and buyer | `openspec/changes/runtime-exploration-semantic-admission-remediation/proposal.md` |
| Option A interpretation and evidence design | `openspec/changes/runtime-exploration-semantic-admission-remediation/design.md` |
| Normative successor behavior | `openspec/changes/runtime-exploration-semantic-admission-remediation/specs/runtime-exploration-semantic-admission-remediation/spec.md` |
| Predecessor frozen behavior | `openspec/changes/runtime-exploration-ledger-and-depth-control/specs/runtime-exploration-ledger-and-depth-control/spec.md` |
| Graduation revocation and falsifiers | `docs/decisions/runtime-exploration-ledger-and-depth-control-graduation-reverification-decision.md` |
| Runtime authority | `docs/system/constitution/runtime-architecture-contract.md` |
| Strategy Contract authority | `openspec/changes/uniagent-runtimeagent-strategy-contract/specs/uniagent-runtimeagent-strategy-contract/spec.md` |
| Runtime module map | `src/UniClaw.Runtime/AGENTS.md` |
| Test module map | `tests/UniClaw.Runtime.Tests/AGENTS.md` |
