# Runtime Exploration Phase 2 — Final Graduation Decision

> Status: `GRADUATED` / `NOT_ARCHIVED` | Decision: `GRADUATE_RUNTIME_EXPLORATION_PHASE_2_AFTER_OPTION_A_REMEDIATION` | Date: 2026-08-25
> Changes: `openspec/changes/runtime-exploration-ledger-and-depth-control/` and `openspec/changes/runtime-exploration-semantic-admission-remediation/`
> Approved implementation base: `e2d8dd44214632f50777992d58fb4fe318ad45f0`
> Supersedes: the current lifecycle conclusion in `runtime-exploration-ledger-and-depth-control-graduation-reverification-decision.md`; that revocation remains historical evidence for the gaps found at the verified base.
> Authority: Architecture v1, Runtime Architecture Contract I-1..I-14, and the approved predecessor/successor Specs remain governing. This graduation decision adds no architecture, protocol, ownership, or execution authority.

## 1. Independent conclusion

Runtime Exploration Roadmap **Phase 2 — Exploration Runtime is graduated**
within the exact predecessor plus Human-approved Option A successor scope. The
revocation findings are closed by implementation and real-path evidence, not by
narrowing or reinterpreting their SHALL/MUST requirements.

Both OpenSpec changes remain active and unarchived. Archive is a separate
lifecycle operation and was neither authorized nor performed here.

## 2. Exact graduated claim

For an accepted Strategy Run:

- admission derives one immutable exploration interpretation from the existing
  typed objective/exploration/completion/depth tuple and carries the same value
  to the Agent-owned Run; the Strategy and DriverHost wire contracts are unchanged;
- the real Agent path applies `ExpandContainer` or `RecordOnly` after generic
  classification and before authorization/dispatch; unavailable classification
  is unresolved with zero authorization and zero dispatch;
- `Visited` is identity-level rule satisfaction only: RecordOnly from a fresh
  accepted observation; ExpandContainer from verified subtree return or verified
  boundary disposition; authorization, dispatch, or click alone is insufficient;
- each accepted inventory identity is in exactly one primary disposition —
  Visited, Pending, or Unresolved — while unknown frontier is only an overlapping
  annotation on RecordOnly Visited;
- depth 0, depth 1, depth-N exhaustive fail-closed cutoff, and depth-N
  match-inspection bounded-record behavior follow the approved closed table;
- branch progress, revisit coverage, observation sequences, and optional existing
  structural-progress facts participate in deterministic ledger correlation;
  structural facts cannot change counts, assert exhaustion, create GoalEvidence,
  or complete a Run;
- the ledger is an immutable Agent-readable projection bound to the accepted Run
  context. It owns no evidence/state and carries no action, target, authorization,
  FSM, completion, recovery, or scenario authority.

## 3. Complete Spec → symbol → real-path test → executed evidence map

| Normative requirement | Production symbols | Independent tests/falsifiers | Executed evidence |
|---|---|---|---|
| Immutable admission interpretation and exact D1 depth table | `StrategyContractCompiler.DeriveExplorationSemantics`; `ExplorationExecutionSemantics`; `RuntimeExecutionIntent.ExplorationSemantics`; `IntentExecution.RunStrategyOpenWorldAsync` | `ExploreStrategy_DerivesClosedAdmissionSemantics`; `InspectStrategy_DerivesRecordOnlyBoundarySemantics`; `UnsupportedObjectiveExplorationCompletionTuple_IsRejectedWithoutIntent`; `RuntimeExecutionIntentAndSemantics_AreImmutable` | Included in targeted 410/410 and deterministic 2052/2052 PASS |
| Accepted Strategy Run provenance is not caller-substitutable | `AcceptedExplorationRunContext`; `Agent.RunOpenWorldAsync`; zero-parameter `Agent.CompileExplorationLedgerView`; `ExplorationLedgerCompiler.Compile` | `StrategyExecution_BindsTheAcceptedSemanticsInstanceToTheRun`; `StrategyLedgerProjectionUsesOnlyTheAcceptedContext`; `MismatchedDeclaredDepthFailsBeforeRunStateTransition`; `LegacyOpenWorldExecutionHasNoAcceptedStrategyContext` | Included in targeted 410/410 and deterministic 2052/2052 PASS |
| Closed rules govern the real Agent path; unavailable classification is unresolved | `ExplorationRuleResolver.Resolve`; Strategy-bound classification branches in `Agent.OpenWorld.cs`; `RecordUnresolvedNode`; `RecordRecordOnlySatisfied` | `StrategyDepthOne_ClassifiesDirectChildLeafRecordOnlyBeforeAuthorization`; `StrategyDepthOne_UnclassifiableDirectChildRemainsUnresolvedBeforeAuthorization`; `UnclassifiableRequiredBranch_NeverDispatched_RecordedUnresolvedInLedger`; exact bounds assertions in `RealStrategyPath_ProducesExactIdentityPartitionAndUnresolvedEvidence` | Included in targeted 410/410 and deterministic 2052/2052 PASS |
| Visited means rule-satisfied, never clicked/authorized/dispatched | `ExplorationLedgerCompiler.CompileScope`; `BranchProgressEvidence.CompletedSiblingEvidence`; verified boundary dispositions; Agent record-only identity/sequence evidence | `Visited_RequiresCompletionEvidence_NotAuthorizationOrClick`; `Depth0_RecordOnlyNode_VisitedByObservation_WithZeroDispatch`; `Depth1_RootExpands_DirectChildrenRecordOnly_ReturnVerified`; `RealClassifiedUnsatisfiedPathRemainsPendingWithoutDispatch` | Included in targeted 410/410 and deterministic 2052/2052 PASS |
| Identity-correct exhaustive accounting and fail-closed contradictions | `ExplorationScopeEvidence`; `CompileScope`; identity digest material in `ExplorationScopeLedger`; accepted Agent branch inventory | `RealStrategyPath_ProducesExactIdentityPartitionAndUnresolvedEvidence` proves 2/1/0/1; `ActualBoundaryEvidenceWithContradictoryUnresolvedIdentityFailsClosed`; `OutOfInventoryIdentity_FailsClosed`; overlap, sequence, revisit, and canonical-order tests in `ExplorationLedgerTests` | Real-path falsifier 4/4 PASS; included in targeted 410/410 and deterministic 2052/2052 PASS |
| Depth 0/1/N and bounded-record versus exhaustive cutoff | admission D1 mapping; Strategy-bound depth branch and `RecordStrategyBoundaryEvidence` in `Agent.OpenWorld.cs` | `StrategyDepthZero_RecordsBoundaryIdentityAsRecordOnlyFrontier`; `StrategyDepthOne_ClassifiesDirectChildLeafRecordOnlyBeforeAuthorization`; `StrategyDepthTwoExhaustive_UsesTheExistingCutoffReason`; `StrategyDepthTwoInspect_BoundaryRecordsFrontierWithoutDispatch`; `DepthIsRunImmutable_AgentPublicSurface_ExposesNoDepthMutationPath` | Included in targeted 410/410 and deterministic 2052/2052 PASS |
| Structural progress participates by validated correlation only | `Agent.TryEvaluatePreTerminalCheckpointAsync`; `_latestAcceptedStrategyExecutionEvidenceView`; `ExplorationLedgerCompiler.ValidateStructuralEvidence`; canonical structural correlation | `StructuralEvidenceChangesOnlyCorrelation_NotAccounting`; `StructuralFactsAreCanonicalAcrossInputOrder`; `StructuralEvidenceBindingAndRevisionAreFailClosed`; `UnsatisfiedGoalEvidenceDoesNotBecomeCompletedFromStructuralFacts` | Included in targeted 410/410 and deterministic 2052/2052 PASS |
| Ledger never owns completion, FSM, action, target, recovery, or scenario semantics | ledger/semantics/context records and pure static compiler; existing Agent/FSM/Traversal paths | `CompleteLedger_PendingZeroFrontierZero_GoalUnsatisfied_RunStillFails`; `RuntimeExplorationSemanticAdmissionArchitectureGuardTests`; `ExplorationLedgerAuthorityGuardTests`; existing pre-terminal, external-neutrality, Strategy wire, Agent, FSM, and Traversal regressions | New plus existing compatibility/authority/wire guards 29/29 PASS; complete targeted suite 410/410 PASS |

## 4. Final independent verification receipt

Leader re-read both Specs, the roadmap Phase 2 boundary, every changed production
symbol, and the real-path tests. Worker reports and task checkboxes were treated
as untrusted until independently reproduced.

- `dotnet build src/UniClaw.Runtime.sln --no-restore --verbosity minimal`:
  **PASS**, 0 warnings / 0 errors.
- Targeted Strategy admission/execution, ledger, depth, unresolved, OpenWorld,
  GoalEvidence, FSM/Agent, Traversal, authority, neutrality, and wire suites:
  **410/410 PASS**.
- Runtime deterministic suite excluding FullyQualifiedName matches for
  RealDevice, RealEmulator, and RealityBaseline: **2052/2052 PASS**.
- Full Semantic suite: **32/32 PASS**.
- `openspec validate runtime-exploration-semantic-admission-remediation --strict`:
  **PASS**.
- `openspec validate runtime-exploration-ledger-and-depth-control --strict`:
  **PASS**.
- UniFlow workflow validation: **PASS**.
- `scripts/check-consistency.sh`: **C1-C12 ALL PASS**.
- Frozen StrategyDirective, Strategy Run request/wire, and DriverHost protocol
  source files: **no diff** from approved base.
- `git diff --check`: **PASS**.

## 5. Limits and exclusions

RealDevice, RealEmulator, and RealityBaseline paths were deliberately excluded
from the deterministic graduation command and were not exercised in this
session. The decision makes no hardware/device availability claim.

No wire/schema addition, Evidence owner, mutable state system, scenario
knowledge, Phase 3 Exploration Memory, Phase 4 dynamic depth, mid-Run strategy
mutation, new completion fact, or new Runtime authority is implemented or
authorized. No unrelated baseline failure appeared in the executed deterministic
or Semantic suites.

## 6. Lifecycle disposition

- `runtime-exploration-ledger-and-depth-control`: **GRADUATED / ACTIVE / NOT_ARCHIVED**.
- `runtime-exploration-semantic-admission-remediation`: **GRADUATED / ACTIVE / NOT_ARCHIVED**.
- Runtime Exploration Roadmap Phase 2: **GRADUATED**.
- Phase 3 Exploration Memory and Phase 4 dynamic depth: **NOT_AUTHORIZED**.
- Repository operations performed: no archive, commit, merge, clean, or reset.

The next real gate is Human authorization for any later lifecycle archive or a
new Phase 3/Phase 4 OpenSpec. This decision supplies neither.
