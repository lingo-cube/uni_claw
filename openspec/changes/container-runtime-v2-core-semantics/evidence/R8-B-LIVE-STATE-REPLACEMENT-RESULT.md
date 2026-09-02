# R8-B Live State Replacement Result

STATUS: `CONTAINER_RUNTIME_V2_LIVE_STATE_REPLACEMENT_VALIDATED`

LEADER_NOTE: R8 executed across a Leader handoff (Sol → GLM). The new Leader
recovered repository state independently (git/evidence/symbols/tests), kept the
validated R8-A map and the already-implemented atomic flip, and completed the
remaining validation-only WorkItems without re-purchasing any semantics.

## PURCHASED

`CONTAINER_RUNTIME_V2_LIVE_STATE_REPLACEMENT_APPROVED_BOUNDED` (Option A):
staged, reversible Agent-module migration at the existing
`TryPrepareContainerReconciliation` → `CommitContainerReconciliation` seam, with
`ContainerRuntimeV2State` as sole Agent-owned physical-current /
Graph-occurrence aggregate, `_belief` removed as an independent mutable owner,
`ActiveContainerContext` kept as execution/completeness obligation path only,
legacy typed transitions as append-only compatibility/audit projection, Fast
live, Slow `Disabled`, and `NET_NEW_MUTABLE_TRUTH = 0`.

## HYPOTHESIS

If the accepted fresh-observation commit path routes through the stateless V2
facade and every compatibility read (Belief, legacy transition, ContainerContext,
DriverHost snapshot) derives one-way from the accepted V2 state, then the live
Agent keeps observable reconciliation behavior while physical-current ownership
collapses to exactly one mutable slot per owner budget and Phase 2.6 Fast-only
acceptance becomes exercisable.

## IMPLEMENTED

- `Agent._containerRuntimeV2State` — the sole Agent-owned immutable V2 state
  slot (exactly one field; exactly two assignment sites: initial
  `TryInitializeV2Belief` and `CommitContainerReconciliation`).
- `_belief` field deleted; `Agent.Belief` is now the pure
  `ProjectV2Belief(_containerRuntimeV2State)` compatibility read.
- All accepted paths route through V2: initial observation (OpenWorld / PlanRun
  / SemanticRun), fresh reconciliation (`TryPrepareContainerReconciliation`),
  fresh observed location and recovery/scroll (`TryCommitFreshObservedLocation`).
- `ContainerRuntimeV2.Start` + `CompleteDisabled` (Slow `Disabled`) is the
  production lifecycle; no advisor binding, await, or provider purchase.
- Legacy `ContainerTransition` is projected only after the accepted V2
  occurrence and fail-closed bound to it (occurrence ref == transition ref,
  observation ref, revision == sequence, trigger == evidence ref, source/
  destination semantics) before any commit.
- `ActiveContainerContext` keeps execution/path semantics; `EnterActiveChild`
  carries the parent's V2 `EntryContext` as `ParentEntryContext` evidence for
  path-relative verified return (no canonical parent, no reverse-edge truth).
- `ContainerTransitionReadModel` (Agent `ContainerContext`) exposes V2
  current/entry/occurrence/revision plus explicit
  `ContainerFastAssessmentAvailability` (`Unavailable` vs `NotRetained`).
- DriverHost `RunSnapshot` adds the eight classified V2 fields
  (`CurrentContainerNodeRef`, `CurrentSliceRef`, `EntrySourceNodeRef`,
  `EntryTransitionOccurrenceRef`, `EntryRelationRef`,
  `LatestTransitionOccurrence`, `EvidenceRevision`,
  `FastAssessmentAvailability`) as `DirectPublicProjection` from
  `Agent.ContainerContext`, with Fast assessment honestly
  `NotCurrentlyAvailable`; the frozen `RunSnapshot.cs` hash was mechanically
  updated in `HarnessSourceShapeGuardTests`.
- No new coordinator/host/subsystem; no wire/DTO/operation change; DriverHost
  holds no V2 mutable state or live Runtime handle.

## VALIDATED

First-hand Leader verification (independently re-run, not Worker-claimed):

- `dotnet build src/UniClaw.Runtime.sln` → 0 errors.
- Focused R8/R7/Stage/correction/resolver/advisor suites → 173/173 GREEN.
- Architecture guards + Agent + Recovery + Scenario → 1096/1103; all 7 failures
  RealDevice/RealEmulator (no online device).
- Read-model/R8/ContainerContext focused → 38/38 GREEN.
- Architecture (incl. read-seam guards) → 97/97; with HarnessSourceShapeGuard
  101/102 (single known RACCTS failure, below).
- DriverHost/read-model/RunSnapshot → 129/129 GREEN.
- Full suite → 2587 passed / 12 failed (UniClaw.Runtime.Tests) plus 5-6
  Semantic benchmark-threshold failures (UniClaw.Semantic.Tests); every failure
  classified below, none is a V2 regression.
- `scripts/check-consistency.sh` ALL PASS; `openspec validate
  container-runtime-v2-core-semantics --type change --strict --no-interactive`
  valid; `git diff --check` clean.

### L1–L18 acceptance matrix

| L | Proof anchors |
|---|---|
| L1 normal navigation live V2 current owner | `NormalNavigationRetainsV2CurrentAndExecutionReadPaths`; `AgentBeliefProjectsFromTheSoleV2StateSlot`; `AcceptedRunRetainsImmutableOccurrenceAndCurrentProjection` |
| L2 r5 fresh SettingsRoot / execution Display unresolved | `ContainerReconciliationTests` r5 cases (V2 current `SettingsRoot`, `ActiveExecutionContainer=Display`); `ObservedCurrentDoesNotInventExecutionObligation`; `ObservedV2CurrentAndActiveExecutionAreSeparateReadableChannels`; `R5V2ProjectionShowsV2CurrentAndKeepsExecutionSeparate` |
| L3 Desktop/Search same destination, distinct EntryContext | `SameDestinationThroughDesktopAndSearchPreservesDistinctRelationsAndEntries`; `MultiEntrySameDestinationKeepsDistinctRelationsAndVerifiedReturnRestoresParentEntryContext`; `SameDestinationWithDifferentEntryContextsProjectsDistinctExactEntryRefs`; `MultiEntrySnapshotPreservesPathRelativeEntryRefs` |
| L4 path-relative Back expectation | `MultiEntrySameDestinationKeepsDistinctRelationsAndVerifiedReturnRestoresParentEntryContext`; `PrematureParentObservationPreservesExecutionObligation`; guard `ActivePathCarriesOnlyParentEntryEvidence` |
| L5 INITIALIZED working node before identity trust | `IndependentUnknownBoundaryDoesNotReuseUnknownNode`; `SameContainerUnknownContinuityPreservesWorkingNodeReference`; `WorkingUnprovenNodeCanCompletePhysicalOccurrence`; `AcceptedUnknownRemainsUnknownAndDoesNotAuthorizeAction`; `PlanRunAcceptsUnknownFreshBeliefWithoutRecoveryOrAdditionalAction` |
| L6 Fast working identity without Agent authority mutation | `FastContainerResolverTests`; `FastContainerResolverArchitectureGuardTests`; facade-integrated composition tests |
| L7 off-path physical current, no normal-edge fabrication | `OffPathOccurrenceHasNoNormalRelation`; `OffPathOccurrenceIsRetainedWithoutNormalRelation` |
| L8 Belief derived from V2 accepted current evidence | `AgentBeliefProjectsFromTheSoleV2StateSlot` (exact equality vs reflected sole state) |
| L9 Belief cannot independently diverge | guards `AgentHasExactlyOneV2StateFieldAndNoBeliefField`, `AgentSourcesContainNoIndependentBeliefAssignment`, `PostInitialV2WritesHaveNoSilentReplaceBypass` (exactly two write sites) |
| L10 legacy transition derived from same V2 occurrence | fail-closed occurrence↔transition binding in `TryPrepareContainerReconciliationCore`; `ContainerReconciliationTests` binding assertions; Stage A replay |
| L11 ActiveContainerContext not physical-current authority | `ActiveContainerContextArchitectureGuardTests`; `ActivePathCarriesOnlyParentEntryEvidence` (no canonical parent); r5 tests prove Current != ActiveExecution legal; read-model guard blacklists ActiveContainerContext surface |
| L12 _branchProgress unchanged | `SiblingBranchProgressScenarioTests`; `AgentSemanticCorrectionConsumerTests`; progress replaced only through validated `ContainerProgressReplacementIntent` |
| L13 GoalEvidence unchanged | `CompletePath_PreservesAWhileBExecutes_AndCompletesOnlyThroughGoalEvidence`; no V2 symbol participates in Goal evidence |
| L14 Action authorization unchanged | `UnresolvedNodeFailClosedPathTests`; `AcceptedUnknownRemainsUnknownAndDoesNotAuthorizeAction`; existing action suites green |
| L15 Recovery authority unchanged | `AgentRecoveryTests` (7/7); `RecoveryScenarioDoesNotCreateASecondCurrentAuthority`; recovery routes through `TryCommitFreshObservedLocation` |
| L16 Slow Disabled | guard `AgentBeliefIsReadOnlyAndProductionBuilderDisablesSlow` (source: `SlowContainerSemanticMode.Disabled`, `CompleteDisabled`, no await/Wait); `SlowContainerSemanticAdvisorTests` |
| L17 no mutable latest Fast/Slow/trust/correction/checkpoint | guards `AgentHasExactlyOneV2StateFieldAndNoBeliefField`, `AgentSourceContainsNoMutableLatestAssessmentOrCheckpointField` (all six Agent partials scanned), read-model type/name guards |
| L18 NET_NEW_MUTABLE_TRUTH = 0 | reflection: exactly one `ContainerRuntimeV2State` field, zero `_belief`; `DriverHostHoldsNoV2MutableStateCacheOrLiveHandle`; budget table below |

### Mutable truth budget (realized)

```text
physical current owner: Agent 1 → Agent 1 (ContainerRuntimeV2State)
semantic current mutable owner: _belief 1 → ContainerRuntimeV2State 1
execution obligation owner: ActiveContainerContext 1 → 1
node-local observation owner: Container 1 → 1
progress owner: _branchProgress 1 → 1
current occurrence owner: legacy interpretation 1 → V2 occurrence 1
mutable latest Fast/Slow/trust/correction/checkpoint: 0 → 0
NET_NEW_MUTABLE_TRUTH = 0
```

### Failure classification (none V2)

- 7 × RealDevice/RealEmulator Scenario tests — `ENVIRONMENT_GATE`: no online ADB
  device (emulator launch previously blocked by host usage limit).
- 3 × `VisionHostFactoryCompositionTests` CORR_HOST03/04/09 — real
  process-readiness environment failures.
- 1 × `ScrollStabilityConfirmationTests.TitleOff_StableRows_...` —
  pre-existing dirty-tree exploration-evidence area, unrelated surface.
- 1 × `HarnessSourceShapeGuardTests.ScenarioKnowledgeTokens_...` — RACCTS
  change scope: token `settingsroot` in `RowIdentityContextDomainTests.cs`
  outside fixture whitelist; owned by `runtime-active-container-context-and-
  transition-semantics`, not this change.
- 5-6 × `Semantic.Tests` benchmark qualification thresholds — statistical
  perception qualification history, separate test project.

### Worker routing record

- Read-seam validation WorkItem executed on the Leader-session default (GLM)
  before the routing directive; result independently re-verified.
- DriverHost projection WorkItem dispatched through
  `tools/dsh_profile_adapter.py dispatch` (requested binding
  `opencode-go/deepseek-v4-flash/high`, role `implementation_efficient`,
  `DISPATCH_OK`; the run-scoped dispatch record lives under the profile
  adapter session state for this change set). Actual worker session log
  confirms `opencode-go` / `deepseek-v4-flash`;
  `receipt` returns `ROUTING_RECEIPT_PARTIAL` (`model_receipt_missing:
  actual_reasoning`) because the workflow spawn channel does not emit a
  structured reasoning header — model+provider verified from the raw session
  log, reasoning tier unverifiable through this channel and honestly left
  unconfirmed.

## DEFERRED

- Slow Shadow / AsyncAdvisory / any provider or backend purchase (requires the
  separately gated experiment).
- Production/mutable checkpoint behavior; cross-run Graph memory; Graph routing.
- Phase 2.6 fresh real-device campaign (tasks 10.1–10.3) — blocked only by
  `ENVIRONMENT_GATE` (no online device), not by Runtime state.
- Task 9.4 change-level final symbol map (closeout artifact after acceptance).

## RISKS

- Record equality on `ContainerTransitionReadModel`/`RunSnapshot` is
  reference-based for `ImmutableArray` fields; consumers must compare
  field-wise (tests already do).
- The read-model guard whitelist/blacklist enumerates the public surface
  explicitly; any future legitimate field trips the guard first and needs an
  explicit architecture decision (by design).
- `FastAssessmentAvailability` is partial evidence while production retains no
  latest Fast slot; if a later stage retains Fast values the classification
  must be re-audited.
- RACCTS whitelist failure and Semantic benchmark thresholds remain open
  environment/other-change items and must not be presented as V2 state.

## NEXT_WORKITEM

`CONTAINER_RUNTIME_V2_READY_FOR_PHASE_2_6_FAST_ONLY_ACCEPTANCE`:

1. Task 10.1 — prepare deterministic E2 / stateful async E3 fixtures for the
   declared r5 / multi-entry / unknown / off-path / Fast-Slow / wrong-branch /
   coverage+Unknown / stale-bounds scenarios.
2. Task 10.2 — fresh real-device Phase 2.6 campaign once a device is online
   (`ENVIRONMENT_GATE` currently open); Fast-only vs frozen baseline per
   task 10.3 falsifiers.
3. Not GRADUATED: lifecycle advancement remains a separate Human decision
   (task 10.4).
