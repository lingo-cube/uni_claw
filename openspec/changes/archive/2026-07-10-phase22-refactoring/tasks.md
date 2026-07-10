## 1. Guard Tests (C-3, C-4, C-9, C-10)

- [x] 1.1 Add `NamespaceIsolationGuardTests` inner class to ArchitectureGuardTests.cs with `Domain_Subdomains_ZeroCrossImport` test (C-3: scan Domain/Vision/, Domain/Content/, Domain/Common/ for cross-domain `using` statements; exception: Domain/Mappings/)
- [x] 1.2 Add `FSMs_DoNotShareTypes` test to NamespaceIsolationGuardTests (C-4: check TraversalFSM.cs and GlobalFSM.cs for cross-FSM type references; exception: ITraversalContext)
- [x] 1.3 Add `CodingConventionGuardTests` inner class with `AllRecords_AreSealedRecordClass` test (C-9: scan Domain/, StateMachine/, Traversal/, Graph/ for unsealed `record class`; exception: TraversalRuntimeContext is `sealed class`)
- [x] 1.4 Add `Domain_UsesDomainValidationException` test to CodingConventionGuardTests (C-10: scan Domain/ for `throw new InvalidOperationException` or `throw new ArgumentException`; note: ElementTypeMapper uses graceful fallback, not throw)
- [x] 1.5 Update constitution/constraints.md: C-3 Guard → `NamespaceIsolationGuardTests.Domain_Subdomains_ZeroCrossImport`, C-4 → `NamespaceIsolationGuardTests.FSMs_DoNotShareTypes`, C-9 → `CodingConventionGuardTests.AllRecords_AreSealedRecordClass`, C-10 → `CodingConventionGuardTests.Domain_UsesDomainValidationException`
- [x] 1.6 `dotnet test` — all 4 new Guard tests pass, existing tests unaffected

## 2. Handler Wrapper (D-16)

- [x] 2.1 Add `ContainerHandler` sealed class to ContainerHandler.cs with HandleContainer(CompletionContext, bool canContinue, string nodeId, ITraversalContext traversalContext) method — 3-step pipeline (detect→decide→execute) + pipeline-level try/catch fallback returning ContainerActionResult(Back, false, "Unhandled exception...")
- [x] 2.2 Add `ErrorHandler` sealed class to ErrorHandler.cs with HandleError(ErrorClassificationContext, StrategySelectionContext, Exception? exception=null) method — 3-step pipeline (classify→select→execute) + pipeline-level try/catch fallback returning ErrorRecoveryResult(Abort, Failure, 0, "Unhandled exception...")
- [x] 2.3 Extend ErrorRecoveryResult: add `string? Description = null` field (backward compatible, default null)
- [x] 2.4 Add ContainerHandler wrapper tests: (1) normal pipeline execution, (2) pipeline-level fallback with injected throwing sub-component, (3) constructor injection with custom sub-components
- [x] 2.5 Add ErrorHandler wrapper tests: (1) normal pipeline execution, (2) pipeline-level fallback with injected throwing sub-component, (3) constructor injection with custom sub-components, (4) Exception? parameter passes to ErrorRecoveryContext, (5) strategyCtx.RetryCount used (not classificationCtx.RetryCount)
- [x] 2.6 Update handler-pipeline.md: Pipeline orchestrator column for Container and Error, fallback chain from 1-layer to 2-layer, D-16 status → Fixed
- [x] 2.7 Update decisions/log.md: D-16 from `Deferred · Phase 2.3` → `Fixed`, fill commit hash
- [x] 2.8 `dotnet test` — all wrapper tests pass, existing tests unaffected

## 3. Trace Minimal (SpanType/PageTransition)

- [x] 3.1 Add SpanType enum (11 values: DfsForward, DfsBacktrack, RestoreOp, SkipDangerous, PopupHandling, ContainerHandling, ErrorHandling, PageAnalysis, CacheOp, AICall, StateDecision) to ITraceRecorder.cs (Observability namespace), each with `<summary>` XML doc
- [x] 3.2 Add `sealed record class PageTransition(FromPage, ToPage, TransitionType, NodeId?, DurationMs?, Timestamp, Metadata?)` to ITraceRecorder.cs
- [x] 3.3 Extend ExecutionRecord: add `SpanType? SpanType = null` field after Status (backward compatible)
- [x] 3.4 Extend ITraceRecorder interface: add `RecordPageTransitionAsync(PageTransition, CancellationToken)` and `GetPageTransitionsAsync(CancellationToken)` returning `Task<List<PageTransition>>`
- [x] 3.5 Add `EnumValueGuardTests.SpanType_Has11Values` test to ArchitectureGuardTests.cs
- [x] 3.6 Update constitution/locked-enums.md: add SpanType = 11 值 (火山级)
- [x] 3.7 Add D-E8 to decisions/log.md: SpanType 11 值锁定
- [x] 3.8 Update D-E4 note in decisions/log.md: operation_rules and trace_integrity fields now available (verification logic is future change)
- [x] 3.9 Update SimulationBaselineTests.cs or InMemoryTraceRecorder if it exists to implement the 2 new ITraceRecorder methods
- [x] 3.10 `dotnet test` — SpanType_Has11Values passes, all existing tests unaffected

## 4. Final Verification

- [x] 4.1 `dotnet test src/UniClaw.Core.sln` — 0 errors, 0 warnings, all tests pass (baseline + new)
- [x] 4.2 Verify no existing test broke during any of the 3 implementation phases
