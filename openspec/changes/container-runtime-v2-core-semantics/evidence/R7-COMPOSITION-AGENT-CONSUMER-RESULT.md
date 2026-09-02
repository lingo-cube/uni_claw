# R7 Composition and Agent Consumer Result

STATUS: CONTAINER_RUNTIME_V2_CORE_PURCHASE_IN_PROGRESS

PURCHASED: Stateless composition over the immutable reducer, Fast assessment, Slow acquisition/consumption, correction projection, relation projection, checkpoint projection, and one unified read projection. The Agent remains the sole owner of obligation progress.

HYPOTHESIS: A bounded composition facade and a single assessment-bound Agent consumer reduce orchestration seams exposed by Phase 2.6 without making Slow, Graph, correction, or observed destination an action/completion authority. Falsifier: later runtime evidence showing no convergence benefit, or requiring the facade to choose a backend or mutate Goal/action/recovery state.

IMPLEMENTED: `ContainerRuntimeV2.ComposeAsync` is stateless and rejects mismatched lifecycle bindings atomically. `Agent.ConsumeContainerSemanticCorrection` validates exact accepted occurrence evidence, retracts only the owner-attributed completion for traversal, and treats directed wrong-branch evidence as read-only reevaluation requiring a separate owner decision. `BranchProgressEvidence.WithoutCompletedSibling` preserves inventory, authorization, and boundary evidence.

VALIDATED: Targeted composition, Agent consumer, scenario, architecture, and existing V2 regression tests pass. Runtime clean rebuild has 0 errors and 105 existing warnings; solution rebuild has 0 errors and 136 existing warnings. No new mutable Runtime V2 truth was added. This is not a claim that C1-C24 are all implemented: C2 has no exact V2 scroll-facade test, C22 is NOT_YET_VALIDATED, and C23 is NOT_YET_IMPLEMENTED/DEFERRED.

DEFERRED: GoalEvidence completion, action authorization, recovery execution, provider/backend selection, real-device Phase 2.6 validation, and migration of the old live current path remain outside this bounded WorkItem.

RISKS: The facade is an integration seam only; it does not yet prove production wiring or fresh-device blocker reduction. Exact owner event binding is intentionally fail-closed when unavailable.

NEXT_WORKITEM: Leader-controlled controlled Apply wiring and fresh Phase 2.6 acceptance after independent Gate review.

## Acceptance coverage

| Contract | Evidence |
|---|---|
| C1 | `ContainerRuntimeV2CompositionTests.ComposeProducesOneCorrelatedReadProjection` — normal unified composition |
| C2 | NOT_YET_VALIDATED — exact same-scroll unified-facade coverage is not implemented by this WI |
| C3 | `ContainerRuntimeV2CoreModelTests.WorkingUnprovenNodeCanCompletePhysicalOccurrence`; `FastContainerResolverTests.MayEnterWithIndependentFreshSemanticSupportProducesTrustedNewAssessment` |
| C4 | `ContainerRuntimeV2CoreModelTests.SameDestinationThroughDesktopAndSearchPreservesDistinctRelationsAndEntries` |
| C5 | PARTIAL — `ContainerRuntimeV2CoreModelTests.CurrentLocationIsSeparateFromPendingObligationAndTrust`; full path-relative return integration remains NOT_YET_VALIDATED |
| C6 | `ContainerRuntimeV2CoreModelTests.OffPathOccurrenceIsRetainedWithoutNormalRelation` |
| C7 | `ContainerRuntimeV2CompositionTests.ProducedFastAssessmentIsBoundIntoSlowAndSlowDRemainsCurrentWinner` |
| C8 | `ContainerRuntimeV2CompositionTests.StartReturnsFastBeforeSlowAndCompleteMarksOlderSlowStale` |
| C9 | `ContainerRuntimeV2AgentCorrectionScenarioTests.CompositionThenAgentConsumerRetractsCAndPreservesABAndCompletedD` |
| C10 | `ContainerRuntimeV2AgentCorrectionScenarioTests.CompositionThenAgentConsumerRetractsCAndPreservesABAndCompletedD` — C reopens as pending |
| C11 | `ContainerRuntimeV2AgentCorrectionScenarioTests.CompositionThenAgentConsumerDoesNotAddUncompletedObservedD` |
| C12 | `AgentSemanticCorrectionConsumerTests.DirectedWrongBranchIsReadOnlyAndRequiresSeparateOwnerDecision` |
| C13 | `AgentSemanticCorrectionConsumerTests.DirectedWrongBranchIsReadOnlyAndRequiresSeparateOwnerDecision`; zero-effect flags in `ProjectionExposesPendingOnlyAndAgentResultExposesZeroEffectFlags` |
| C14 | `ContainerRuntimeV2AgentCorrectionScenarioTests.CompositionThenAgentConsumerRetractsCAndPreservesABAndCompletedD` — remaining obligations preserved |
| C15 | `ContainerRuntimeV2AgentCorrectionScenarioTests.CompositionThenAgentConsumerRetractsCAndPreservesABAndCompletedD` — unrelated completion preserved |
| C16 | `AgentSemanticCorrectionConsumerTests.TraversalCorrectionRetractsOnlyExactCompletionAttribution` |
| C17 | `AgentSemanticCorrectionConsumerTests.DuplicatePendingCorrectionIsIdempotentAndDoesNotAddObservedBranch` |
| C18 | `ContainerRuntimeV2CompositionTests.WrongTransitionReferenceFailsClosedAtStart` |
| C19 | `ContainerRuntimeV2CompositionTests.WrongTriggerOccurrenceReferenceFailsClosedAtStart` |
| C20 | `AgentSemanticCorrectionConsumerTests.HistoricalCorrectionCanRetractExactO17AttributionWhileKeepingO23State` |
| C21 | Existing regression: `SettingsSiblingSubtreeLedgerTests.SL8_FreshBoundsRequiredForBDispatch`; `OpenWorldBoundedSourceRevisitTests.RVT278_TapUsesCurrentFreshStructuredBounds` |
| C22 | NOT_YET_VALIDATED — no exact stale LocalModel bounds dispatch test is claimed by this WI |
| C23 | NOT_YET_IMPLEMENTED / DEFERRED — coverage-complete plus Unknown integration is outside this WI |
| C24 | `ContainerRuntimeV2CompositionArchitectureGuardTests.FacadeIsStaticAndOwnsNoMutableState`; `ContainerRuntimeV2CompositionArchitectureGuardTests.ProductionFacadeDoesNotBecomeAnAuthorityCoordinator`; `ContainerRuntimeV2CompositionArchitectureGuardTests.ProjectionExposesPendingOnlyAndAgentResultExposesZeroEffectFlags` |

NEW_SYMBOL_JUSTIFICATION: `ContainerRuntimeV2EvidenceContext`, lifecycle input/read/result are required because no existing type correlates the already-approved immutable components at one evidence boundary; extending the reducer or Slow consumer would create mixed ownership. `AgentSemanticCorrectionConsumptionResult` is required because no existing Agent result represents exact correction consumption and idempotent fail-closed outcomes. `BranchProgressEvidence.WithoutCompletedSibling` is the smallest immutable replacement operation and does not create a new owner.

AUTHORITY_DELTA: NONE
BEHAVIOR_DELTA: NONE outside the explicitly purchased bounded correction consumption
NET_NEW_MUTABLE_TRUTH: 0

## Sol independent acceptance — 2026-09-01

- Focused Runtime V2 / Fast / Slow / correction / transition / reconciliation / Agent-consumer coverage: **148/148 passed**.
- Architecture guards: **93/93 passed**.
- `UniClaw.Runtime` clean rebuild: **0 errors**, 105 pre-existing warnings.
- Solution clean rebuild: **0 errors**, 136 pre-existing warnings.
- Consistency guard: **ALL PASS**.
- Strict OpenSpec validation: **valid**.
- `git diff --check`: **passed**.
- Full Runtime suite: **2550/2562 passed**. The 12 failures match the previously classified failure surface: seven require an eligible ADB device, three are Vision model-identity configuration mismatches, one is the existing ValidationHarness fixture-whitelist violation, and one is the existing scroll-stability failure. No failure names a Runtime V2 composition, correction, Agent-consumer, or new architecture-guard test.

SOL_GATE_RESULT: `CONTAINER_RUNTIME_V2_AGENT_INTEGRATION_VALIDATED` for the bounded composition and correction-consumer contract only.

PRODUCTION_WIRING: `NOT_YET_AUTHORIZED`. Repository search finds no production caller of `ContainerRuntimeV2.Start`, `CompleteSlow`, or `ComposeAsync`; the approved Gate explicitly excludes migration of the old live current/execution path. This result therefore does not claim production Runtime V2 acceptance or graduation.
