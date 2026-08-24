# Runtime Exploration Ledger and Depth Control — Graduation Decision

> Status: GRADUATED (independent evidence-verified graduation; archive pending) | Decision: `GRADUATE_RUNTIME_EXPLORATION_LEDGER_AND_DEPTH_CONTROL` | Date: 2026-08-25
> Change: `openspec/changes/runtime-exploration-ledger-and-depth-control/`
> Authority: Runtime Architecture Contract (I-1..I-14) and Architecture v1 remain the governing baselines; this decision adds no architecture authority.

## 1. Buyer and exact claim boundary

**Buyer:** RuntimeAgent abstract exploration capability (Runtime Exploration
Roadmap Phase 2 — Exploration Runtime), consuming the Phase 1 graduated
strategy contract.

This receipt claims only that, for an accepted Strategy Run:

- `ExplorationLedgerView` is an immutable, deterministic, on-demand projection
  compiled from existing evidence records (branch-progress evidence, real
  per-scope unresolved-node evidence, revisit-coverage cross-check, unknown-
  frontier evidence, observation-sequence correlations). It is not a state
  system, owns no evidence, and mutates nothing.
- The closed `ExplorationRule` vocabulary (`ExpandContainer`, `RecordOnly`) is
  derived from the accepted strategy's exploration intent; RuntimeAgent never
  authors or invents rules.
- A discovered node whose classification is unavailable (a configured
  classifier returns null) fails closed on the real Agent run path: no
  authorization, no dispatch, recorded as unresolved ledger evidence, never
  guessed. The unconfigured-classifier legacy path is unchanged.
- `Visited` means rule-satisfied with evidence: RecordOnly by fresh-observation
  record (zero dispatch), ExpandContainer by verified subtree return or
  verified boundary disposition. A dispatch or click alone never counts.
- Depth semantics map at admission (0 root-record-only, 1 root + direct
  children record-only, N ≥ 2 bounded recursive) with the exhaustive fail-closed
  cutoff preserved verbatim; depth is Run-immutable; no dynamic depth.
- The ledger carries no GoalEvidence, FSM, completion, action, authorization, or
  recovery authority: a fully satisfied ledger (pending = 0, frontier = 0) never
  completes a Run without Agent GoalEvidence and FSM authorization.

It claims no exploration Memory (Phase 3), no dynamic depth or unknown-handling
strategies (Phase 4), no UniAgent Planner, no wire-method additions, no
per-scope structural-fact fusion semantics, and no scenario knowledge.

## 2. Validation evidence (2026-08-25, Leader-independent re-execution)

All commands re-executed by the Leader after final remediation (WI-RELC-003),
not read from worker or prior reports:

- Build: 0 errors (`dotnet build src/UniClaw.Runtime.sln`).
- Targeted ledger/depth/authority suites: 72/72 green
  (`ExplorationLedgerTests`, `ExplorationDepthBoundaryTests`,
  `UnresolvedNodeFailClosedPathTests`, `ExplorationLedgerAuthorityGuardTests`).
- Full deterministic suite excluding RealDevice/RealEmulator/RealityBaseline:
  2004/2004 Runtime + 32/32 Semantic green.
- Device-dependent limitation: 7 RealDevice/RealEmulator tests fail-closed on
  absent ADB device (hardware availability, by design); recorded, not hidden.
- Strict OpenSpec validation: `openspec validate
  runtime-exploration-ledger-and-depth-control --strict` PASS.
- Architecture/consistency: `scripts/check-consistency.sh` C1–C12 ALL PASS;
  `git diff --check` PASS.

## 3. Graduation verification history (honest record)

1. First Sol adversarial verification (2026-08-25): **NOT_EARNED** — the
   RecordOnly fresh-observation visited scenario had no production wiring.
   Remediated (R3/R4 overlap implementation) with real-path tests.
2. Leader independent verification (this session) found the first review had
   mis-classified two further gaps as non-blocking:
   - **Requirement 2 violated on the real path**: unclassifiable pending nodes
     were guessed into dispatch (CP-12 seam guard required a non-null category;
     `default: Tap` fallback), authorized into branch-progress evidence, and
     counted Visited; `CompileExplorationLedgerView` hardcoded `Unresolved: 0`.
     Proven by a probe run on the real Agent path (Fake World).
   - **Requirement 1 fusion incomplete**: revisit-coverage was not a compiler
     input.
3. Remediation WI-RELC-003 (UniFlow: validated WorkItem, development profile,
   runtime-core, single unicast worker; protocol deviation PV-2026-08-25-01
   recorded for the earlier Tool Only misuse) closed both gaps; acceptance was
   independent (scope containment, line-by-line implementation re-read, all
   gates re-executed by the Leader).

## 4. Spec-to-evidence map (frozen revision, six requirements)

| Requirement | Implementation symbols | Verification |
|---|---|---|
| 1 Evidence-derived ledger projection | `ExplorationLedgerView`, `ExplorationScopeLedger`, `ExplorationLedgerCompiler.Compile/CompileScope`, `Agent.CompileExplorationLedgerView` (feeds `_branchProgress`, `_unresolvedNodes`, `_unknownFrontierBeyondDepth`, `_revisitCoverage`) | `CompileScope_ReportsUnifiedAccountingFromEvidence`, `IdenticalEvidence_ProducesIdenticalLedgerAndDigest`, `DifferentEvidence_ProducesDifferentDigest`, `RevisitCoverage_IdentityOutsideApprovedInventory_FailsClosed`, `ValidRevisitCoverage_DoesNotChangeTheFiveCounts`, `CompleteLedger_PendingZeroFrontierZero_*` |
| 2 Closed rule vocabulary + fail-closed classification | `ExplorationRule`, `DeriveRules`, CP-12 seam guard in `Agent.OpenWorld.cs`, `Agent._unresolvedNodes` | `DeriveRules_ClosedVocabulary_*`, `UnresolvedNodeFailClosedPathTests` (real path), `UnclassifiableNode_FailsClosedToUnresolved_NeverGuessed`, `LedgerSources_AreScenarioNeutral` |
| 3 Visited means rule-satisfied | `CompileScope` visited term, `WithCompletedSibling` write sites (verified parent return / leaf completion), `WithVerifiedBoundaryDisposition`, frontier record-visited overlap | `Visited_RequiresCompletionEvidence_NotAuthorizationOrClick`, `Depth0_RecordOnlyNode_VisitedByObservation_WithZeroDispatch`, `Depth1_RootExpands_DirectChildrenRecordOnly_ReturnVerified`, `BoundedRecordBoundary_RecordsUnknownFrontier_AndCountsRecordVisited` |
| 4 Bounded semantic depth control | `DeriveDepthSemantics`, depth-boundary branch in `Agent.OpenWorld.cs` (BoundedRecursive cutoff verbatim; 0/1 record-only + frontier) | `Depth0_BoundedRecord_ContainersRecordedNotFailed_FrontierLedgered`, `Depth0_BoundedRecord_UnsatisfiedGoal_FailsWithBoundaryReason_NotCutoff`, `Depth1_*`, `Depth2_Exhaustive_CutoffStillFailsClosed`, `DepthIsRunImmutable_AgentPublicSurface_ExposesNoDepthMutationPath`, `HypothesisReconcilerTests` cutoff assertion unchanged |
| 5 Ledger never completion authority | `CompileExplorationLedgerView` read-only surface; no GoalEvidence/FSM references | `CompleteLedger_PendingZeroFrontierZero_GoalUnsatisfied_RunStillFails`, `LedgerTypes_CarryNoAuthorityMembers` |
| 6 Neutrality and authority guards | Guard test suite over Model + Agent surfaces | `ExplorationLedgerAuthorityGuardTests` 5/5, `LedgerTypes_DoNotReferenceMutableWorldOrActionTypes`, `Compiler_IsPure_NoStatefulDependencies`, `ScopeLedger_RejectsDispositionOverCount` |

## 5. Documented interpretation boundary (surfaced, not self-extended)

Requirement 1's evidence-family enumeration lists "structural-progress facts".
The only existing producer of structural facts emits a single Run-level
synthetic `BoundedScopeEntered` revision marker with an opaque reference and no
per-scope node content; there is no node-accounting content to fuse, and
inventing per-scope structural-fact semantics would be a new evidence design
(architecture decision, not undertaken). The Req-1 normative scenario —
branch-progress and coverage evidence producing consistent per-scope counts —
is satisfied and tested. This boundary is recorded here and presented at the
Phase 3 Human Gate rather than resolved unilaterally.

## 6. Deferred scope

Exploration Memory (Phase 3) including Safety Knowledge and Known Environment
Knowledge; per-scope structural-fact fusion semantics; dynamic depth and
unknown handling (Phase 4); UniAgent Planner; wire-method additions; multi-Run
continuation. None is authorized by this graduation.

## 7. Final lifecycle conclusion

`runtime-exploration-ledger-and-depth-control` is **GRADUATED** on the frozen
spec/design revision (human apply authorization recorded at tasks 1.1/1.2,
2026-08-24). Graduation authorizes no deferred scope and no new architecture
authority. Archive is a separate pending lifecycle operation; the change
remains listed under active changes until archived.
